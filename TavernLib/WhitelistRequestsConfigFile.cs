using System.Collections.Generic;
using Newtonsoft.Json;

namespace TavernLib.Backend.Server.Configs;

public class WhitelistRequestList
{
    [JsonProperty("requests")] public List<WhitelistRequest> Requests { get; set; } = new();

    public class WhitelistRequest
    {
        [JsonProperty("username")] public string Username { get; set; }
        [JsonProperty("ip")] public string Ip { get; set; }
        [JsonProperty("applied_at")] public string AppliedAt { get; set; }
    }
}

public class WhitelistRequestsConfigFile(string filePath) : ServerConfigFile<WhitelistRequestList>(filePath);
