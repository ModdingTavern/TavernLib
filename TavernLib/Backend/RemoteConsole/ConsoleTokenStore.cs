using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Alta.Console;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TavernLib.Backend.Server.Configs;

namespace TavernLib.Backend.RemoteConsole;

public class ConsoleTokenList
{
    // Raised by token rotate owner. The launcher reads it to write console_token.txt.
    [JsonProperty("owner_generation")] public int OwnerGeneration { get; set; }
    [JsonProperty("tokens")] public List<ConsoleToken> Tokens { get; set; } = new();
    [JsonExtensionData] private IDictionary<string, JToken> Extra { get; set; }
}

// A named console token. Only its id and generation travel in the token; what it may run lives
// here, so an edit applies at once with no new token to hand out.
public class ConsoleToken
{
    public const string FullPreset = "full";

    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("preset")] public string Preset { get; set; }
    [JsonProperty("access")] public List<string> Access { get; set; } = new();
    [JsonProperty("generation")] public int Generation { get; set; } = 1;
    [JsonProperty("created")] public DateTime Created { get; set; }
    [JsonProperty("expires")] public DateTime? Expires { get; set; }
    [JsonProperty("revoked")] public DateTime? Revoked { get; set; }
    [JsonProperty("last_used")] public DateTime? LastUsed { get; set; }
    [JsonExtensionData] private IDictionary<string, JToken> Extra { get; set; }

    [JsonIgnore] public bool IsFull => string.Equals(Preset, FullPreset, StringComparison.OrdinalIgnoreCase);

    public string Status(DateTime now) =>
        Revoked != null ? "revoked" : Expires != null && Expires <= now ? "expired" : "active";
}

public class ConsoleTokenFile(string filePath) : ServerConfigFile<ConsoleTokenList>(filePath);

// console_tokens.json, written only by the server. Reads go to an in-memory copy, rebuilt after
// every write, so the per-command check never touches the disk.
internal static class ConsoleTokenStore
{
    private static readonly string FilePath = Path.Combine(TavernDirectories.ModdingTavern, "console_tokens.json");
    private static readonly ConsoleTokenFile TokenFile = new(FilePath);
    private static readonly ConditionalWeakTable<CommandPart, string> Paths = new();
    private static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(5);
    private static volatile Dictionary<string, Entry> _byId = new();
    private static volatile int _ownerGeneration;
    private static DateTime _lastLoad = DateTime.MinValue;
    private static bool _loadErrorLogged;

    private sealed class Entry(ConsoleToken token)
    {
        public readonly ConsoleToken Token = token;
        public readonly HashSet<string> Granted = new((token.Access ?? []).Select(NormalizePath).Where(path => path.Length > 0));
        public readonly HashSet<string> Above = new((token.Access ?? []).Select(NormalizePath).SelectMany(Ancestors));
    }

    public static bool Loaded { get; private set; }

    // The owner token's generation: 0 until it is first rotated.
    public static int OwnerGeneration => _ownerGeneration;

    // A missing file means no named tokens yet and an owner token never rotated; it is created by
    // the first token create or rotate. A file that can't be read leaves every console token
    // refused, the owner's too, since it holds the owner token's generation.
    public static void Load()
    {
        _lastLoad = DateTime.UtcNow;
        if (!File.Exists(FilePath))
        {
            Rebuild(new ConsoleTokenList());
            return;
        }

        try
        {
            Rebuild(TokenFile.Read());
        }
        catch (Exception)
        {
            if (_loadErrorLogged) return;
            _loadErrorLogged = true;
            TavernLogger.Error("console_tokens.json can't be read, so every console token is refused until it can, the owner token too: the file holds its generation.");
        }
    }

    // True once the file has been read. One that couldn't be is tried again at most every few
    // seconds, so a fixed file applies with no restart.
    public static bool EnsureLoaded()
    {
        if (Loaded) return true;
        if (DateTime.UtcNow - _lastLoad >= RetryEvery) Load();
        return Loaded;
    }

    public static ConsoleToken Find(string id) =>
        id != null && _byId.TryGetValue(id, out var entry) ? entry.Token : null;

    public static IEnumerable<ConsoleToken> All => _byId.Values.Select(entry => entry.Token).OrderBy(token => token.Created);

    // Reads the file, changes it and writes it back under its lock, then refreshes the copy once
    // the write went through.
    public static void Update(Func<ConsoleTokenList, bool> change)
    {
        var changed = false;
        TokenFile.Update(list =>
        {
            list.Tokens ??= new();
            return changed = change(list);
        });
        if (changed) Rebuild(TokenFile.LastRead);
    }

    public static void Touch(string id)
    {
        try
        {
            Update(list =>
            {
                var token = list.Tokens.FirstOrDefault(t => t.Id == id);
                if (token == null) return false;
                token.LastUsed = DateTime.UtcNow;
                return true;
            });
        }
        catch (Exception e)
        {
            TavernLogger.Warn($"Could not record when console token {id} was used: {e.Message}");
        }
    }

    private static void Rebuild(ConsoleTokenList list)
    {
        _byId = (list.Tokens ?? []).Where(token => !string.IsNullOrEmpty(token?.Id))
            .GroupBy(token => token.Id).ToDictionary(group => group.Key, group => new Entry(group.First()));
        _ownerGeneration = list.OwnerGeneration;
        Loaded = true;
        _loadErrorLogged = false;
    }

    // The gate: may this named token see and run this command or module? A module passes when a
    // granted command lies inside it, so the search can reach that command.
    public static bool Allows(string id, CommandPart part)
    {
        if (!_byId.TryGetValue(id, out var entry) || entry.Token.Status(DateTime.UtcNow) != "active") return false;

        var path = PathOf(part);
        if (path == "token whoami") return true;
        if (path == "token") return part is Module;
        if (NeverGranted(path)) return false;
        if (entry.Token.IsFull) return true;

        return Granted(entry.Granted, path) || (part is Module && entry.Above.Contains(path));
    }

    // repeat runs the last command anyone matched, past the only check of who may run it, and the
    // token commands manage tokens. Neither can be given to a named token.
    public static bool NeverGranted(string path) =>
        path == "repeat" || path.StartsWith("repeat ", StringComparison.Ordinal) ||
        path == "token" || path.StartsWith("token ", StringComparison.Ordinal);

    private static bool Granted(HashSet<string> granted, string path)
    {
        if (granted.Contains(path)) return true;
        for (var cut = path.LastIndexOf(' '); cut > 0; cut = path.LastIndexOf(' ', cut - 1))
            if (granted.Contains(path.Substring(0, cut))) return true;
        return false;
    }

    // "player progression list" for a command, "player progression" for a module: what is typed.
    // A command's own ToString adds its parameters, so its path is built from its module's.
    public static string PathOf(CommandPart part) =>
        Paths.GetValue(part, p => NormalizePath(p is Command && p.Parent != null ? p.Parent + " " + p.Name : p.ToString()));

    public static string NormalizePath(string path) =>
        string.Join(" ", (path ?? "").Trim().ToLowerInvariant().Split([' '], StringSplitOptions.RemoveEmptyEntries));

    private static IEnumerable<string> Ancestors(string path)
    {
        for (var cut = path.LastIndexOf(' '); cut > 0; cut = path.LastIndexOf(' ', cut - 1))
            yield return path.Substring(0, cut);
    }
}
