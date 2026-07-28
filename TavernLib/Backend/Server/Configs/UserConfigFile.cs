using System;
using System.Collections.Generic;
using System.Linq;
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
        [JsonProperty("roles")] public List<string> Roles { get; set; } = new();

        public bool HasRole(string role) =>
            Roles != null && Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
    }

    public class ListConfig
    {
        [JsonProperty("usernames")] public List<string> Usernames { get; set; } = [];
        [JsonProperty("ips")] public List<string> Ips { get; set; } = [];
        [JsonProperty("user_ids")] public List<ulong> UserIds { get; set; } = [];
    }
}

public class UserConfigFile(string filePath) : ServerConfigFile<UserConfig>(filePath);
