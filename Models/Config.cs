using RDHT_Backend.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RDHT_Backend.Models
{
    internal class Config
    {
        public static Config Instance { get; private set; } = null!;

        public int WorkerCount { get; set; } = 3;
        public int MaxChannelGetRetries { get; set; } = 3;
        public int RatelimitWaitTime { get; set; } = 15;
        public ClientSettingsUrl ClientSettingsUrl { get; set; } = ClientSettingsUrl.Both;

        public static async Task Fetch()
        {
            const string url = $"https://raw.githubusercontent.com/{Globals.BackendTrackerRepositoryPath}/refs/heads/main/Config.json";
            string contents;

            if (File.Exists("Config.json"))
            {
                Console.WriteLine("Using local config");
                contents = File.ReadAllText("Config.json");
            }
            else
            {
                Console.WriteLine($"Fetching config from {url}");
                contents = await Globals.Client.GetStringAsync(url);
            }

            Instance = JsonSerializer.Deserialize<Config>(contents) ?? throw new Exception($"Failed to deserialize config");
        }
    }
}
