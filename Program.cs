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
#if RELEASE
        static readonly string? PersonalToken = Environment.GetEnvironmentVariable("RDHT_TOKEN");
        static readonly string? AuthUsername = Environment.GetEnvironmentVariable("RDHT_USER");
#endif

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

#if RELEASE
            if (string.IsNullOrEmpty(PersonalToken) || string.IsNullOrEmpty(AuthUsername))
            {
                Console.WriteLine("Please add the environment variables!");
                return 1;
            }
#endif

            if (Directory.Exists(Globals.ClonePath))
            {
                Console.WriteLine("Deleting existing clone folder...");
                ForceDeleteDirectory(Globals.ClonePath);
            }

            Console.WriteLine("Cloning...");
            var gitPath = Repository.Clone($"http://github.com/{Globals.TrackerRepositoryPath}.git", Globals.ClonePath);
            Console.WriteLine(gitPath);
            var repo = new Repository(gitPath);

            // get channel list
            string channelsPath = Path.Combine(Globals.ClonePath, "Channels.json");
            string channelsStr = await File.ReadAllTextAsync(channelsPath);

            //List<string> channels = new List<string> { "ZIntegration", "ZCanary", "ZNext" };  // FOR TESTING
            List<string> channels = JsonSerializer.Deserialize<List<string>>(channelsStr) ?? throw new Exception("Failed to deserialise channels list");
            foreach (string channel in channels)
                Workers.ChannelQueue.Enqueue(channel);

            Console.WriteLine("Starting!");

            List<string> changed = new List<string>();

            // start the workers
            List<Task> workers = new List<Task>();

            // start the appropriate number of workers
            for (int i = 1; i <= Globals.Workers; i++)
                workers.Add(Workers.Create(repo, changed));

            // wait for workers to complete
            Task.WaitAll(workers.ToArray());

            // sort changed list
            changed.Sort();

            // delete channels removed from the list
            foreach (var file in Directory.GetFiles(Globals.ClonePath, "*.txt"))
            {
                var fileName = Path.GetFileName(file);
                var channel = Path.GetFileNameWithoutExtension(file);
                if (!channels.Contains(channel))
                {
                    Console.WriteLine($"Removing unused channel file `{file}` `{fileName}`");
                    changed.Add(channel);
                    File.Delete(file);
                    repo.Index.Remove(fileName);
                }
            }

#if RELEASE
            try
            {
                var time = DateTimeOffset.Now;
                var signature = new Signature("Roblox DeployHistory Bot", "rdhb@rdht.local", time);
                var commit = repo.Commit($"{time.ToString("dd/MM/yyyy HH:mm:ss")} [{string.Join(", ", changed)}]", signature, signature);
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
#else
            Console.WriteLine("Debug mode has committing disabled.");

            Console.WriteLine("Changed:");
            if (changed.Count == 0)
                Console.WriteLine("Nothing!");
            foreach (string channel in changed)
                Console.WriteLine(channel);

            Console.WriteLine();

            Console.WriteLine("Repository index:");
            if (repo.Index.Count == 0)
                Console.WriteLine("Nothing... not supposed to be like that!");
            foreach (var entry in repo.Index)
                Console.WriteLine(entry.Path);
#endif

            return 0;
        }
    }
}