using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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
