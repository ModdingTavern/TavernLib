using Alta.Networking.Servers;
using Newtonsoft.Json;
using TavernLib.Backend.Server.Configs;

namespace TavernLib.Backend.Server;

public struct ServerListingPayload
{
    [JsonProperty(PropertyName = "listing_token")] public string ListingToken { get; private set; }
    [JsonProperty(PropertyName = "name")] public string Name { get; private set; }
    [JsonProperty(PropertyName = "port")] public int Port { get; private set; }
    [JsonProperty(PropertyName = "player_limit")] public int PlayerLimit { get; private set; }
    [JsonProperty(PropertyName = "has_password")] public bool HasPassword { get; private set; }
    [JsonProperty(PropertyName = "player_count")] public int PlayerCount { get; private set; }
    [JsonProperty(PropertyName = "community_listed")] public bool CommunityListed { get; private set; }
    [JsonProperty(PropertyName = "hostname")] public string HostName { get; private set; }
    [JsonProperty(PropertyName = "version")] public string Version { get; private set; }
    [JsonProperty(PropertyName = "region")] public string Region { get; private set; }
    [JsonProperty(PropertyName = "quest")] public bool Quest { get; private set; }
    [JsonProperty(PropertyName = "tutorial")] public bool Tutorial { get; private set; }

    public static ServerListingPayload FromConfig(ServerSettingsConfig config, TavernServerConfig tavernConfig)
    {
        var scene = ResolveScene(config);
        return new ServerListingPayload
        {
            ListingToken = config.LastRead.CommunityListingToken,
            Name = config.LastRead.Name,
            Port = tavernConfig.LastRead.ServerPort,
            PlayerLimit = ServerHandler.Current?.PlayerLimit ?? config.LastRead.MaxPlayers,
            HasPassword = !string.IsNullOrWhiteSpace(config.LastRead.PasswordHash),
            PlayerCount = ServerHandler.Current?.Connections ?? 0,
            CommunityListed = config.LastRead.CommunityListed,
            HostName = config.LastRead.PublicHostname,
            Version = Tavern.Version,
            Region = config.LastRead.Region,
            Quest = scene == SceneKind.Quest,
            Tutorial = scene == SceneKind.Tutorial
        };
    }

    private enum SceneKind { Normal, Quest, Tutorial }

    private static SceneKind ResolveScene(ServerSettingsConfig config)
    {
        // The scene launch args decide what actually runs (tutorial wins over
        // quest, same as TavernLauncher); the settings are only the fallback.
        if (CommandLineArguments.Contains(TavernArgs.TutorialScene)) return SceneKind.Tutorial;
        if (CommandLineArguments.Contains(TavernArgs.QuestScene)) return SceneKind.Quest;
        if (config.LastRead.TutorialScene) return SceneKind.Tutorial;
        if (config.LastRead.QuestScene) return SceneKind.Quest;
        return SceneKind.Normal;
    }
}