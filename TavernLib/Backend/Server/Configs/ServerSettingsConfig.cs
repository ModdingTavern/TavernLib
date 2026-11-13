using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
    [JsonProperty(PropertyName = "public_hostname")] public string PublicHostname { get; private set; }
    [JsonProperty(PropertyName = "quest_scene")] public bool QuestScene { get; private set; }
    [JsonProperty(PropertyName = "tutorial_scene")] public bool TutorialScene { get; private set; }
    [JsonProperty(PropertyName = "region")] public string Region { get; private set; } = "unknown";
    [JsonProperty(PropertyName = "garbage_collection_timer")] public float GarbageCollectionTimer { get; private set; } = 30f;
    [JsonProperty(PropertyName = "server_backup_time")] public float ServerBackupTime { get; private set; }
    [JsonProperty(PropertyName = "server_backup_max_backups")] public int ServerBackupMaxBackups { get; private set; }
    [JsonProperty(PropertyName = "console_owner_local_only")] public bool ConsoleOwnerLocalOnly { get; private set; }
    [JsonExtensionData] private IDictionary<string, JToken> Extra { get; set; }
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