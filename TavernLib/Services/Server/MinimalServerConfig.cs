using Newtonsoft.Json;

namespace TavernLib.Services.Server
{
    public class MinimalServerConfig
    {
        private MinimalServerConfig() {}
        
        [JsonProperty(PropertyName = "listing_token")] public string ListingToken { get; private set; }
        [JsonProperty(PropertyName = "name")] public string Name { get; private set; }
        [JsonProperty(PropertyName = "port")] public int Port { get; private set; }
        [JsonProperty(PropertyName = "player_limit")] public int PlayerLimit { get; private set; }
        [JsonProperty(PropertyName = "has_password")] public bool HasPassword { get; private set; }
        [JsonProperty(PropertyName = "kind")] public string Kind => "headless";
        
        
        public static MinimalServerConfig FromServerConfig(ServerConfig config)
        {
            return new MinimalServerConfig()
            {
                ListingToken = config.ListingToken,
                Name = config.Name,
                PlayerLimit = config.MaxPlayers,
                Port = config.Game,
                HasPassword = false // TODO
            };
        }
    }
}