using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TavernLib.Backend.Server.Configs;

public class TavernServer
{
    [JsonProperty(PropertyName = "server_port")] public int ServerPort { get; private set; }
    [JsonExtensionData] private IDictionary<string, JToken> Extra { get; set; }
}

public class TavernServerConfig(string filePath) : ServerConfigFile<TavernServer>(filePath);