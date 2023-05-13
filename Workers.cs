using LibGit2Sharp;
using RDHT_Backend.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RDHT_Backend
{
    internal static class Workers
    {
        public static readonly Queue<string> ChannelQueue = new Queue<string>();

        static readonly List<string> BinaryTypes = new List<string>
        {
            // CJV = LUOBU
            // WINDOWS
            "WindowsPlayer",
            "WindowsPlayerCJV",
            "WindowsStudio",
            "WindowsStudioCJV",
            "WindowsStudio64",
            "WindowsStudio64CJV",
            // MAC
            "MacPlayer",
            "MacPlayerCJV",
            "MacStudio",
            "MacStudioCJV"
        };

        public static async Task Create(Repository repository, List<string> changed)
        {
            while (ChannelQueue.Count > 0)
            {
                string channel = ChannelQueue.Dequeue();

                List<string> output = new List<string>();
                foreach (var binaryType in BinaryTypes)
                {
                    for (int i = 1; i <= 3; i++)
                    {
                        var req = await Globals.Client.GetAsync($"https://clientsettings.roblox.com/v2/client-version/{binaryType}/channel/{channel}");
                        string response = await req.Content.ReadAsStringAsync();

                        // sometimes clientsettings dies and responds with '{"errors":[{"code":0,"message":"InternalServerError"}]}'
                        // retry if InternalServerError is the status code and "InternalServerError" is in response, since i dont want to parse json
                        if (req.StatusCode == HttpStatusCode.InternalServerError && response.Contains("InternalServerError"))
                        {
                            Console.WriteLine($"[{channel}] {binaryType} Death, retry {i} ({req.StatusCode}) [{response}]");
                            continue;
                        }
                        else if (!req.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"[{channel}] {binaryType} Failure ({req.StatusCode}) [{response}]");
                            break;
                        }

                        var json = JsonSerializer.Deserialize<ClientVersion>(response);
                        output.Add($"{binaryType}: {json?.VersionGuid} [{json?.Version}]");
                        Console.WriteLine($"[{channel}] {binaryType} Success");
                        break;
                    }
                }

                var channelFile = $"{channel}.txt";
                var channelPath = Path.Combine(Globals.ClonePath, channelFile);

                // check for any changes
                if (!File.Exists(channelPath) || !Enumerable.SequenceEqual(await File.ReadAllLinesAsync(channelPath), output.ToArray()))
                {
                    Console.WriteLine($"[{channel}] Changes detected");
                    changed.Add(channel);
                    await File.WriteAllTextAsync(channelPath, string.Join("\n", output));
                }
                repository.Index.Add(channelFile);
            }
        }
    }
}
