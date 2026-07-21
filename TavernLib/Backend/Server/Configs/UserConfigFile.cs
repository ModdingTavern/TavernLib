using System.Collections.Generic;
using Newtonsoft.Json;

namespace TavernLib.Backend.Server.Configs;

public class UserConfig
{
    [JsonProperty("users")] public Dictionary<string, User> Users { get; set; } = new();
    [JsonProperty("whitelist")] public ListConfig Whitelist { get; set; } = new();
    [JsonProperty("blacklist")] public ListConfig Blacklist { get; set; } = new();

    public class User
    {
        [JsonProperty("user_id")] public ulong UserId { get; set; }
        [JsonProperty("token")] public string Token { get; set; }
        [JsonProperty("registered_from")] public string RegisteredFrom { get; set; }
    }

    public class ListConfig
    {
        [JsonProperty("usernames")] public List<string> Usernames { get; set; } = [];
        [JsonProperty("ips")] public List<string> Ips { get; set; } = [];
    }
}

public class UserConfigFile(string filePath) : ServerConfigFile<UserConfig>(filePath)
{
    public override void ReadFromFile()
    {
        base.ReadFromFile();
        
    }
}