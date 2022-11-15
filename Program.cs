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

        static readonly string ClonePath = "./clone/";

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
                ForceDeleteDirectory(ClonePath);

            var gitPath = Repository.Clone($"http://github.com/bluepilledgreat/Roblox-DeployHistory-Tracker.git", ClonePath);
            Console.WriteLine(gitPath);
            var repo = new Repository(gitPath);

            // stop caching
            Client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true
            };

            // collect information about channels
            var channelsText = await Client.GetStringAsync("https://raw.githubusercontent.com/bluepilledgreat/RDHT-Backend/main/channels.txt");
            var channels = channelsText.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var channel in channels)
            {
                var output = "";
                foreach (var binaryType in BinaryTypes)
                {
                    var req = await Client.GetAsync($"https://clientsettings.roblox.com/v2/client-version/{binaryType}/channel/{channel}");
                    if (!req.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[{channel}] {binaryType} Failure");
                        continue;
                    }
                    var json = JsonSerializer.Deserialize<ClientVersion>(await req.Content.ReadAsStringAsync());
                    output += $"{binaryType}: {json?.VersionGuid} [{json?.Version}]\r\n";
                    Console.WriteLine($"[{channel}] {binaryType} Success");
                }

                var channelFile = $"{channel}.txt";
                var channelPath = ClonePath + channelFile;
                if ((await File.ReadAllTextAsync(channelPath)) != output)
                {
                    Console.WriteLine($"[{channel}] Changes detected");
                    Changed.Add(channel);
                    await File.WriteAllTextAsync(channelPath, output);
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
                    File.Delete(file);
                    repo.Index.Remove(fileName);
                }
            }

            try
            {
                var signature = new Signature("Roblox DeployHistory Bot", "rdhb@rdht.local", DateTimeOffset.Now);
                var commit = repo.Commit($"Update channels ({string.Join(", ", Changed)})", signature, signature);
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