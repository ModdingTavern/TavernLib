using Alta.Networking.Servers;
using Newtonsoft.Json;

namespace TavernLib.Backend.Server.Configs;

public class ServerSettings
{
    [JsonProperty(PropertyName = "name")] public string Name { get; private set; } = "My Tavern Server";
    [JsonProperty(PropertyName = "password_hash")] public string PasswordHash { get; private set; } = "";
    [JsonProperty(PropertyName = "whitelist_enabled")] public bool WhitelistEnabled { get; private set; }
    [JsonProperty(PropertyName = "enforce_ip_limit")] public bool EnforceIpLimit { get; private set; }
    [JsonProperty(PropertyName = "community_listed")] public bool CommunityListed { get; private set; }
    [JsonProperty(PropertyName = "max_players")] public int MaxPlayers { get; private set; }
    [JsonProperty(PropertyName = "community_listing_token")] public string CommunityListingToken { get; internal set; } = "";
}

public class ServerSettingsConfig(string filePath) : ServerConfigFile<ServerSettings>(filePath)
{
    public override void ReadFromFile()
    {
        base.ReadFromFile();
        if (!string.IsNullOrWhiteSpace(LastRead.CommunityListingToken)) return;
        
        LastRead.CommunityListingToken = BackendUtils.TokenUrlSafe(24);
        WriteToFile();
    }
}