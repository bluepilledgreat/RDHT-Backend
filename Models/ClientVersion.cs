using System.Text.Json.Serialization;

namespace RDHT_Backend.Models
{
    internal class ClientVersion
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("clientVersionUpload")]
        public string? VersionGuid { get; set; }

        [JsonPropertyName("bootstrapperVersion")]
        public string? Bootstrapper { get; set; }
    }
}
