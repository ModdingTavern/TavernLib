using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TavernLib.Backend.Server.Configs;

public class WhitelistRequestList
{
    [JsonProperty("requests")] public List<WhitelistRequest> Requests { get; set; } = new();
    [JsonExtensionData] private IDictionary<string, JToken> Extra { get; set; }

    public class WhitelistRequest
    {
        [JsonProperty("username")] public string Username { get; set; }
        [JsonProperty("ip")] public string Ip { get; set; }
        [JsonProperty("applied_at")] public string AppliedAt { get; set; }
        [JsonExtensionData] private IDictionary<string, JToken> Extra { get; set; }
    }
}

public class WhitelistRequestsConfigFile(string filePath) : ServerConfigFile<WhitelistRequestList>(filePath);
