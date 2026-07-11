using System.Linq;
using Alta.Networking.Servers;
using Newtonsoft.Json;

namespace TavernLib.Backend.Auth
{
    public static class AuthPayloads
    {
        public struct PingRequest
        {
            [JsonProperty(PropertyName = "ping")] private bool Ping { get; set; }
        }
        
        
        public struct PingResponse
        {
            [JsonProperty(PropertyName = "status")] private string Pong => "pong";
            [JsonProperty(PropertyName = "server_name")] private string ServerName { get; set; }
            [JsonProperty(PropertyName = "password_required")] private bool PasswordRequired { get; set; }
            [JsonProperty(PropertyName = "whitelist_enabled")] private bool WhitelistEnabled { get; set; }
            [JsonProperty(PropertyName = "game_port")] private int GamePort { get; set; }
            
            public PingResponse(string serverName, bool passwordRequired, bool whitelistEnabled, int gamePort)
            {
                ServerName = serverName;
                PasswordRequired = passwordRequired;
                WhitelistEnabled = whitelistEnabled;
                GamePort = gamePort;
            }
        }


        public struct AuthenticateRequest
        {
            [JsonProperty(PropertyName = "username")] private string Username { get; set; }
            [JsonProperty(PropertyName = "token")] private string Token { get; set; }
            [JsonProperty(PropertyName = "password")] private string Password { get; set; }
        }


        public struct AuthenticateOk
        {
            [JsonProperty(PropertyName = "status")] private string Status => "ok";
            [JsonProperty(PropertyName = "user_id")] private ulong UserId => 2000000000 + (ulong)ServerHandler.Current.SaveUtility.PlayerSaveUtility.PlayerFolder.AllFiles.Count();
        }
    }
}