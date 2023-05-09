using LibGit2Sharp;
using RDHT_Backend.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace RDHT_Backend
{
    internal class Program
    {
        static readonly string? PersonalToken = Environment.GetEnvironmentVariable("RDHT_TOKEN");
        static readonly string? AuthUsername = Environment.GetEnvironmentVariable("RDHT_USER");

        static readonly HttpClient Client = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });
        static List<string> Changed = new List<string>();
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

        static readonly string ClonePath = "clone";

        const string TRACKER_REPOSITORY_PATH = "bluepilledgreat/Roblox-DeployHistory-Tracker";

        // https://stackoverflow.com/a/8714329
        // git clone generates read only folders & files
        static void ForceDeleteDirectory(string path)
        {
            var directory = new DirectoryInfo(path) { Attributes = FileAttributes.Normal };

            foreach (var info in directory.GetFileSystemInfos("*", SearchOption.AllDirectories))
                info.Attributes = FileAttributes.Normal;

            directory.Delete(true);
        }

        static async Task<int> Main(string[] args)
        {
            Console.WriteLine($"RDHT-Backend {Assembly.GetExecutingAssembly().GetName().Version}");

            if (string.IsNullOrEmpty(PersonalToken) || string.IsNullOrEmpty(AuthUsername))
            {
                Console.WriteLine("Please add the environment variables!");
                return 1;
            }

            if (Directory.Exists(ClonePath))
            {
                Console.WriteLine("Deleting existing clone folder...");
                ForceDeleteDirectory(ClonePath);
            }

            Console.WriteLine("Cloning...");
            var gitPath = Repository.Clone($"http://github.com/{TRACKER_REPOSITORY_PATH}.git", ClonePath);
            Console.WriteLine(gitPath);
            var repo = new Repository(gitPath);

            // stop caching
            Client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true
            };

            // get channel list
            string channelsPath = Path.Combine(ClonePath, "Channels.json");
            string channelsStr = await File.ReadAllTextAsync(channelsPath);
            List<string> channels = JsonSerializer.Deserialize<List<string>>(channelsStr) ?? throw new Exception("Failed to deserialise channels list");

            Console.WriteLine("Starting!");
            // collect information about channels
            foreach (var channel in channels)
            {
                List<string> output = new List<string>();
                foreach (var binaryType in BinaryTypes)
                {
                    for (int i = 1; i <= binaryType.Length; i++)
                    {
                        var req = await Client.GetAsync($"https://clientsettings.roblox.com/v2/client-version/{binaryType}/channel/{channel}");
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
                    }
                }

                var channelFile = $"{channel}.txt";
                var channelPath = Path.Combine(ClonePath, channelFile);

                // check for any changes
                if (!File.Exists(channelPath) || !Enumerable.SequenceEqual(await File.ReadAllLinesAsync(channelPath), output.ToArray()))
                {
                    Console.WriteLine($"[{channel}] Changes detected");
                    Changed.Add(channel);
                    await File.WriteAllTextAsync(channelPath, string.Join("\n", output));
                }
                repo.Index.Add(channelFile);
            }

            // delete channels removed from the list
            foreach (var file in Directory.GetFiles(ClonePath, "*.txt"))
            {
                var fileName = Path.GetFileName(file);
                var channel = Path.GetFileNameWithoutExtension(file);
                if (!channels.Contains(channel))
                {
                    Console.WriteLine($"Removing unused channel file `{file}` `{fileName}`");
                    Changed.Add(channel);
                    File.Delete(file);
                    repo.Index.Remove(fileName);
                }
            }

            try
            {
                var time = DateTimeOffset.Now;
                var signature = new Signature("Roblox DeployHistory Bot", "rdhb@rdht.local", time);
                var commit = repo.Commit($"{time.ToString("dd/MM/yyyy HH:mm:ss")} [{string.Join(", ", Changed)}]", signature, signature);
                Console.WriteLine("Committing!");

                var remote = repo.Network.Remotes["origin"];
                var options = new PushOptions
                {
                    CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials { Username = AuthUsername, Password = PersonalToken }
                };
                var pushRefSpec = "refs/heads/main";
                repo.Network.Push(remote, pushRefSpec, options);
            }
            catch (EmptyCommitException) // any better way?
            {
                Console.WriteLine("No changes");
            }

            return 0;
        }
    }
}