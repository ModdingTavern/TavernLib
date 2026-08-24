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
        [JsonProperty("roles")] public List<string> Roles { get; set; } = [];

        public bool HasRole(string role) =>
            Roles != null && Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
        
        public bool IsModerator => HasRole("moderator") || HasRole("owner");
        public bool CanEnterFlyMode => IsModerator || HasRole("fly");
    }

    public class ListConfig
    {
        [JsonProperty("usernames")] public List<string> Usernames { get; set; } = [];
        [JsonProperty("ips")] public List<string> Ips { get; set; } = [];
        [JsonProperty("user_ids")] public List<ulong> UserIds { get; set; } = [];
    }
}

public class UserConfigFile(string filePath) : ServerConfigFile<UserConfig>(filePath)
{
    public UserConfig.User GetUser(string username) =>
        LastRead.Users.TryGetValue(username.ToLowerInvariant(), out var user) ? user : null;
    public UserConfig.User GetUser(int identifier) =>
        LastRead.Users.Values.FirstOrDefault(user => user.UserId == (ulong)identifier);

    public bool TryGetUser(string username, out UserConfig.User user)
    {
        user = GetUser(username);
        return user != null;
    }
    
    public bool TryGetUser(int identifier, out UserConfig.User user)
    {
        user = GetUser(identifier);
        return user != null;
    }
}
