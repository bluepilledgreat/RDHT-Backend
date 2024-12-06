using LibGit2Sharp;
using RDHT_Backend.Enums;
using RDHT_Backend.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RDHT_Backend
{
    internal class WorkerFactory
    {
        private static uint _SubCount = 0;

        private Queue<string> _Channels;
        private Repository _Repository;
        private List<string> _Changed;

        public WorkerFactory(Queue<string> channels, Repository repository, List<string> changed)
        {
            _Channels = channels;
            _Repository = repository;
            _Changed = changed;
        }

        private static string GetClientSettingsSub()
        {
            switch (Config.Instance.ClientSettingsUrl)
            {
                case ClientSettingsUrl.Roblox:
                    return "clientsettings";
                case ClientSettingsUrl.CDN:
                    return "clientsettingscdn";
                case ClientSettingsUrl.Both:
                    uint i = Interlocked.Increment(ref _SubCount);
                    return i % 2 == 0 ? "clientsettings" : "clientsettingscdn";
                default:
                    throw new Exception($"Unknown url type: {Config.Instance.ClientSettingsUrl}");
            }
        }

        private async Task<IEnumerable<string>> GetOutput(string channel)
        {
            List<string> output = new List<string>();

            foreach (string binaryType in Globals.BinaryTypes)
            {
                for (int i = 1; i <= Config.Instance.MaxChannelGetRetries; i++)
                {
                    string url = $"https://{GetClientSettingsSub()}.roblox.com/v2/client-version/{binaryType}/channel/{channel}";
                    //Console.WriteLine(url);

                    var req = await Globals.Client.GetAsync(url);
                    string response = await req.Content.ReadAsStringAsync();

                    // sometimes clientsettings dies and responds with '{"errors":[{"code":0,"message":"InternalServerError"}]}'
                    // retry if InternalServerError is the status code and "InternalServerError" is in response, since i dont want to parse json
                    if (req.StatusCode == HttpStatusCode.InternalServerError && response.Contains("InternalServerError"))
                    {
                        Console.WriteLine($"[{channel}] {binaryType} Death, retry {i} ({req.StatusCode}) [{response}] [{url}]");
                        continue;
                    }
                    // sometimes clientsettings dies [2]
                    // this time, we cant do anything about it
                    else if (response.Contains("Adaptive Concurrency Limits Enforced"))
                    {
                        Console.WriteLine("CLIENTSETTINGS IS DEAD!!!");
                        Environment.Exit(1337);
                        throw new Exception("CLIENTSETTINGS IS DEAD!!!");
                    }
                    else if (response.Contains("Too many requests"))
                    {
                        Console.WriteLine($"[{channel}] {binaryType} Ratelimited! Waiting for {Config.Instance.RatelimitWaitTime}s... ({i}) [{url}]");
                        await Task.Delay(Config.Instance.RatelimitWaitTime * 1000);
                        continue;
                    }
                    else if (!req.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[{channel}] {binaryType} Failure ({req.StatusCode}) [{response}] [{url}]");
                        break;
                    }

                    var json = JsonSerializer.Deserialize<ClientVersion>(response);
                    output.Add($"{binaryType}: {json?.VersionGuid} [{json?.Version}]");
                    Console.WriteLine($"[{channel}] {binaryType} Success");
                    break;
                }
            }

            return output;
        }

        private async Task Handle(string channel)
        {
            IEnumerable<string> output = await GetOutput(channel);

            var channelFile = channel + ".txt";
            var channelPath = Path.Combine(Globals.ClonePath, channelFile);

            // check for any changes
            if (!File.Exists(channelPath) || !Enumerable.SequenceEqual(await File.ReadAllLinesAsync(channelPath), output))
            {
                Console.WriteLine($"[{channel}] Changes detected");
                _Changed.Add(channel);
                await File.WriteAllTextAsync(channelPath, string.Join("\n", output));
            }
            _Repository.Index.Add(channelFile);
        }

        public async Task Create()
        {
            while (_Channels.TryDequeue(out string? channel))
            {
                if (channel == null)
                    continue;

                await Handle(channel);
            }
        }
    }
}
