using Newtonsoft.Json;

namespace TavernLib.Backend.Auth;

internal static class AuthPayloads
{
    public struct PingRequest
    {
        [JsonProperty(PropertyName = "ping")] private bool Ping { get; set; }
    }


    public struct PingResponse(string serverName, bool passwordRequired, bool whitelistEnabled, int gamePort)
    {
        [JsonProperty(PropertyName = "status")] private string Pong => "pong";
        [JsonProperty(PropertyName = "server_name")] private string ServerName { get; set; } = serverName;
        [JsonProperty(PropertyName = "password_required")] private bool PasswordRequired { get; set; } = passwordRequired;
        [JsonProperty(PropertyName = "whitelist_enabled")] private bool WhitelistEnabled { get; set; } = whitelistEnabled;
        [JsonProperty(PropertyName = "game_port")] private int GamePort { get; set; } = gamePort;
    }


    public struct AuthenticateRequest
    {
        [JsonProperty(PropertyName = "username")] public string Username { get; private set; }
        [JsonProperty(PropertyName = "token")] public string Token { get; private set; }
        [JsonProperty(PropertyName = "password")] public string Password { get; private set; }
    }


    public readonly struct AuthenticateOk(ulong userId, bool questSceneRequired)
    {
        [JsonProperty(PropertyName = "status")] private string Status => "ok";
        [JsonProperty(PropertyName = "user_id")] private ulong UserId => userId;
        [JsonProperty(PropertyName = "quest_scene_required")] private bool QuestSceneRequired => questSceneRequired;
    }


    public readonly struct NeedsPassword
    {
        [JsonProperty(PropertyName = "status")] private string Status => "needs_password";
    }


    public readonly struct WrongPassword
    {
        [JsonProperty(PropertyName = "status")] private string Status => "wrong_password";
        [JsonProperty(PropertyName = "message")] private string Message => "Wrong Password";
    }


    public readonly struct NotWhitelisted
    {
        [JsonProperty(PropertyName = "status")] private string Status => "not_whitelisted";
        [JsonProperty(PropertyName = "message")] private string Message => "Not Whitelisted";
    }


    public readonly struct WhitelistApplicationReceived(bool wasNew)
    {
        [JsonProperty(PropertyName = "status")] private string Status => "whitelist_application_received";
        [JsonProperty(PropertyName = "already_pending")] private bool AlreadyPending => !wasNew;
    }


    public readonly struct GenericFail(string message)
    {
        [JsonProperty(PropertyName = "status")] private string Status => "error";
        [JsonProperty(PropertyName = "message")] private string Message => message;
    }
}