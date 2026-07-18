using Newtonsoft.Json;

namespace TavernLib.Backend.Server.Configs;

public class ServerSettings
{
    [JsonProperty(PropertyName = "name")] public string Name { get; private set; }
    [JsonProperty(PropertyName = "password_hash")] public string PasswordHash { get; private set; }
    [JsonProperty(PropertyName = "whitelist_enabled")] public bool WhitelistEnabled { get; private set; }
    [JsonProperty(PropertyName = "community_listed")] public bool CommunityListed { get; private set; }
    [JsonProperty(PropertyName = "max_players")] public uint MaxPlayers { get; private set; }
    [JsonProperty(PropertyName = "community_listing_token")] public string CommunityListingToken { get; private set; }
}

public class ServerSettingsConfig(string filePath) : ServerConfigFile<ServerSettings>(filePath)
{
        
}