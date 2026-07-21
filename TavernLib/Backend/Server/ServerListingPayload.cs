using Alta.Networking.Servers;
using Newtonsoft.Json;
using TavernLib.Backend.Server.Configs;

namespace TavernLib.Backend.Server
{
    public struct ServerListingPayload
    {
        [JsonProperty(PropertyName = "listing_token")] public string ListingToken { get; private set; }
        [JsonProperty(PropertyName = "name")] public string Name { get; private set; }
        [JsonProperty(PropertyName = "port")] public ushort Port { get; private set; }
        [JsonProperty(PropertyName = "player_limit")] public int PlayerLimit { get; private set; }
        [JsonProperty(PropertyName = "has_password")] public bool HasPassword { get; private set; }
        [JsonProperty(PropertyName = "player_count")] public int PlayerCount { get; private set; }
        [JsonProperty(PropertyName = "community_listed")] public bool CommunityListed { get; private set; }
        
        public static ServerListingPayload FromConfig(ServerSettingsConfig config)
        {
            return new ServerListingPayload
            {
                ListingToken = config.LastRead.CommunityListingToken,
                Name = config.LastRead.Name,
                Port = 1757, // TODO
                PlayerLimit = ServerHandler.Current?.PlayerLimit ?? config.LastRead.MaxPlayers,
                HasPassword = !string.IsNullOrWhiteSpace(config.LastRead.PasswordHash),
                PlayerCount = ServerHandler.Current?.Connections ?? 0,
                CommunityListed = config.LastRead.CommunityListed
            };
        }
    }
}