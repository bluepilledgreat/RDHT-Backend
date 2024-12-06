using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RDHT_Backend.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum ClientSettingsUrl
    {
        /// <summary>
        /// clientsettings.roblox.com
        /// </summary>
        Roblox = 0,

        /// <summary>
        /// clientsettingscdn.roblox.com
        /// </summary>
        CDN = 1,
        Cdn = 1,

        /// <summary>
        /// Switch between clientsettings & clientsettingscdn
        /// </summary>
        Both = 2,
    }
}
