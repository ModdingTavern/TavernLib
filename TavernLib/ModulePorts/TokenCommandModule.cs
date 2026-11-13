using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Alta.Console;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TavernLib.Backend.RemoteConsole;
using TavernLib.Patches;

namespace TavernLib.ModulePorts;

// Named console tokens. Only the owner token and the server itself manage them; any token can ask
// token whoami. Errors answer as text, since a thrown error reaches the console only as
// "Exception has been thrown by the target of an invocation."
[ServerOnly]
[Module(ModuleName, "Named console tokens: which commands each one may run")]
public static class TokenCommandModule
{
    public const string ModuleName = "token";
    private const int NameMaxLength = 32;
    private const int PresetMaxLength = 16;
    private const int MaxDays = 3650;
    private const string IdAlphabet = "abcdefghjkmnpqrstuvwxyz23456789";
    private const string OwnerId = "owner";

    public class TokenInfo
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("preset")] public string Preset { get; set; }
        [JsonProperty("access")] public List<string> Access { get; set; }
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("created")] public DateTime? Created { get; set; }
        [JsonProperty("expires")] public DateTime? Expires { get; set; }
        [JsonProperty("last_used")] public DateTime? LastUsed { get; set; }
        [JsonProperty("sessions")] public int Sessions { get; set; }

        public override string ToString() =>
            $"{Id}  {Name}  {Preset}  {Status}  access: {string.Join(", ", Access)}  expires: {Date(Expires, "never")}  last used: {Date(LastUsed, "never")}  sessions: {Sessions}";
    }

    public class TokenList
    {
        [JsonProperty("tokens")] public List<TokenInfo> Tokens { get; set; }

        public override string ToString() => Tokens.Count == 0 ? "No named tokens" : string.Join("\n", Tokens);
    }

    public class LogLines
    {
        [JsonProperty("entries")] public List<JObject> Entries { get; set; }

        public override string ToString() => Entries.Count == 0
            ? "Nothing logged"
            : string.Join("\n", Entries.Select(e => string.Join("  ", new[] { "time", "event", "token", "name", "ip", "command", "result", "reason" }
                .Select(key => e.Value<string>(key)).Where(value => !string.IsNullOrEmpty(value)))));
    }

    [Command("whoami", "Which token this console logged in with, and what it may run")]
    private static object WhoAmI()
    {
        var id = CallerTokenId();
        if (id == null)
            return new TokenInfo { Id = OwnerId, Name = OwnerId, Preset = ConsoleToken.FullPreset, Access = ["everything"], Status = "active" };

        var token = ConsoleTokenStore.Find(id);
        return token == null ? "Unknown token " + id : Info(token);
    }

    [Command("list", "Every named token: access, status, last use and open sessions")]
    private static object List()
    {
        if (OwnerOnly() is { } refusal) return refusal;
        return new TokenList { Tokens = ConsoleTokenStore.All.Select(Info).ToList() };
    }

    [Command("create", "Makes a named token: token create <name> <access> [days] [preset]. Access is full, or command and module paths split by commas, such as \"player kick,player list\". Days 0 never expire. Shows the token")]
    private static string Create(string name, string access, int days = 0, string preset = "custom")
    {
        if (OwnerOnly() is { } refusal) return refusal;

        name = name?.Trim();
        if (!ValidName(name, NameMaxLength)) return $"Refused: a name has letters, numbers, spaces, hyphens and underscores, up to {NameMaxLength}.";
        if (days < 0 || days > MaxDays) return $"Refused: days must be 0 (never expires) to {MaxDays}.";
        if (ParseAccess(access, preset, out var label, out var paths) is { } problem) return "Refused: " + problem;

        ConsoleToken created = null;
        if (Store(list =>
        {
            if (list.Tokens.Any(t => t.Revoked == null && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))) return false;

            var now = DateTime.UtcNow;
            created = new ConsoleToken
            {
                Id = NewId(list.Tokens),
                Name = name,
                Preset = label,
                Access = paths,
                Created = now,
                Expires = days > 0 ? now.AddDays(days) : null,
            };
            list.Tokens.Add(created);
            return true;
        }) is { } failed) return failed;

        if (created == null) return $"Refused: a token called {name} already exists.";
        return $"Created {name} ({created.Id}). Its token:\n{ConsoleAuth.Mint(created)}";
    }

    [Command("edit", "Changes what a named token may run: token edit <id> <access> [days] [preset]. Days -1 keep the expiry, 0 never expire. A token can't change between full and limited")]
    private static string Edit(string id, string access, int days = -1, string preset = null)
    {
        if (OwnerOnly() is { } refusal) return refusal;
        if (days < -1 || days > MaxDays) return $"Refused: days must be -1 (keep), 0 (never expires) or up to {MaxDays}.";

        var token = ConsoleTokenStore.Find(id);
        if (token == null) return "Refused: no token " + id;
        if (ParseAccess(access, preset ?? (token.IsFull ? "custom" : token.Preset), out var label, out var paths) is { } problem) return "Refused: " + problem;
        if ((label == ConsoleToken.FullPreset) != token.IsFull) return "Refused: a token can't change between full and limited. Make a new one.";

        string answer = null;
        if (Store(list =>
        {
            var row = list.Tokens.FirstOrDefault(t => t.Id == id);
            if (row == null || row.Revoked != null)
            {
                answer = "Refused: token " + id + " is revoked";
                return false;
            }

            row.Preset = label;
            row.Access = paths;
            if (days >= 0) row.Expires = days > 0 ? DateTime.UtcNow.AddDays(days) : null;
            answer = "Changed " + row.Name + " (" + id + ")";
            return true;
        }) is { } failed) return failed;
        return answer;
    }

    [Command("revoke", "Stops a named token for good and closes its sessions")]
    private static string Revoke(string id)
    {
        if (OwnerOnly() is { } refusal) return refusal;

        string name = null;
        if (Store(list =>
        {
            var row = list.Tokens.FirstOrDefault(t => t.Id == id);
            if (row == null || row.Revoked != null) return false;

            row.Revoked = DateTime.UtcNow;
            name = row.Name;
            return true;
        }) is { } failed) return failed;

        if (name == null) return "Refused: no active token " + id;
        return $"Revoked {name} ({id}); closed {ConsoleTokenPatches.CloseSessions(id)} session(s).";
    }

    [Command("rotate", "Gives a named token, or the owner token (token rotate owner), new text: the old text stops working and its sessions close")]
    private static string Rotate(string id)
    {
        if (OwnerOnly() is { } refusal) return refusal;
        if (id == OwnerId) return RotateOwner();

        ConsoleToken rotated = null;
        if (Store(list =>
        {
            var row = list.Tokens.FirstOrDefault(t => t.Id == id);
            if (row == null || row.Revoked != null) return false;

            row.Generation++;
            rotated = row;
            return true;
        }) is { } failed) return failed;

        if (rotated == null) return "Refused: no active token " + id;
        var closed = ConsoleTokenPatches.CloseSessions(id);
        return $"Rotated {rotated.Name} ({id}); closed {closed} session(s). Its new token:\n{ConsoleAuth.Mint(ConsoleTokenStore.Find(id))}";
    }

    // The session that asked stays open; every other owner session closes. Ids never hold an o,
    // so no named token is called owner.
    private static string RotateOwner()
    {
        if (ConsoleAuth.Secret == null) return "Refused: server_secret.key can't be read";
        if (Store(list =>
        {
            list.OwnerGeneration++;
            return true;
        }) is { } failed) return failed;

        var text = ConsoleAuth.CurrentOwnerToken();
        if (text == null) return "Refused: console_tokens.json can't be read";

        var written = ConsoleAuth.WriteOwnerToken(text);
        var closed = ConsoleTokenPatches.CloseOwnerSessions(WebSocketCommandHandler.Current);
        var file = written == null ? "console_token.txt has it" : $"console_token.txt could not be written ({written}), so put it there by hand";
        return $"Rotated the owner token; closed {closed} other session(s). Bots and tools that use it need the new one; {file}. Its new token:\n{text}";
    }

    [Command("show", "Shows a named token's text again")]
    private static string Show(string id)
    {
        if (OwnerOnly() is { } refusal) return refusal;

        var token = ConsoleTokenStore.Find(id);
        if (token == null || token.Revoked != null) return "Refused: no active token " + id;
        return $"{token.Name} ({id}):\n{ConsoleAuth.Mint(token)}";
    }

    [Command("log", "The newest console log lines: token log [id, or owner] [count]")]
    private static object Log(string id = "", int count = 50)
    {
        if (OwnerOnly() is { } refusal) return refusal;
        return new LogLines { Entries = ConsoleTokenLog.Tail(id, Math.Max(1, Math.Min(count, 500))) };
    }

    // Runs a change to console_tokens.json; the refusal to answer when it can't be read or written.
    private static string Store(Func<ConsoleTokenList, bool> change)
    {
        try
        {
            ConsoleTokenStore.Update(change);
            return null;
        }
        catch (Exception e)
        {
            return "Refused: console_tokens.json can't be read or written: " + e.Message;
        }
    }

    private static string CallerTokenId() => ConsoleAuth.TokenIdOf(CommandService.CurrentContext?.CallingUser);

    // The gate already keeps named tokens out of these; this is the second lock.
    private static string OwnerOnly() => CallerTokenId() == null ? null : "Refused: only the owner token manages named tokens.";

    private static TokenInfo Info(ConsoleToken token) => new()
    {
        Id = token.Id,
        Name = token.Name,
        Preset = token.Preset,
        Access = token.IsFull ? ["everything but token and repeat"] : token.Access ?? [],
        Status = token.Status(DateTime.UtcNow),
        Created = token.Created,
        Expires = token.Expires,
        LastUsed = token.LastUsed,
        Sessions = ConsoleTokenPatches.CountSessions(token.Id),
    };

    // "full", or paths of commands and modules this server has, split by commas.
    private static string ParseAccess(string access, string preset, out string label, out List<string> paths)
    {
        paths = [];
        label = string.IsNullOrWhiteSpace(preset) ? "custom" : preset.Trim().ToLowerInvariant();

        var entries = (access ?? "").Split(',').Select(ConsoleTokenStore.NormalizePath).Where(entry => entry.Length > 0).Distinct().ToList();
        if (entries.Count == 0) return "give full, or at least one command or module";

        if (entries.Count == 1 && entries[0] == ConsoleToken.FullPreset)
        {
            label = ConsoleToken.FullPreset;
            return null;
        }

        if (label == ConsoleToken.FullPreset) return "the full preset is for access full";
        if (!ValidName(label, PresetMaxLength) || label.Contains(' ')) return $"a preset is one word of letters, numbers, hyphens and underscores, up to {PresetMaxLength}";

        var known = KnownPaths();
        foreach (var entry in entries)
        {
            if (entry == ConsoleToken.FullPreset) return "full can't be mixed with commands";
            if (ConsoleTokenStore.NeverGranted(entry)) return entry + " can't be given to a named token";
            if (!known.Contains(entry)) return "this server has no command or module called " + entry;
        }

        paths = entries;
        return null;
    }

    private static HashSet<string> KnownPaths()
    {
        var paths = new HashSet<string>();

        void Walk(Module module)
        {
            paths.Add(ConsoleTokenStore.PathOf(module));
            foreach (var command in module.Commands) paths.Add(ConsoleTokenStore.PathOf(command));
            foreach (var submodule in module.Submodules) Walk(submodule);
        }

        foreach (var module in CommandService.CommandCollection.Modules.ToList()) Walk(module);
        return paths;
    }

    private static bool ValidName(string name, int maxLength) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= maxLength &&
        name.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or ' ' or '-' or '_');

    private static string NewId(List<ConsoleToken> existing)
    {
        var bytes = new byte[8];
        using var rng = RandomNumberGenerator.Create();
        while (true)
        {
            rng.GetBytes(bytes);
            var id = new string(bytes.Select(b => IdAlphabet[b % IdAlphabet.Length]).ToArray());
            if (existing.All(token => token.Id != id)) return id;
        }
    }

    private static string Date(DateTime? value, string none) => value?.ToString("yyyy-MM-dd HH:mm") ?? none;
}
