using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TavernLib.Backend.RemoteConsole;

// console_token_log.jsonl: one line per console connect, disconnect, command and refused login,
// for the owner token and every named one. Never a token's text. At 5 MB it moves to .1.
internal static class ConsoleTokenLog
{
    private const long RollAt = 5 * 1024 * 1024;
    private const int MaxCommandLength = 500;
    private static readonly string FilePath = Path.Combine(TavernDirectories.ModdingTavern, "console_token_log.jsonl");
    private static readonly object Sync = new();
    private static readonly Regex TokenText = new(@"eyJ[A-Za-z0-9_\-+/=]+\.[A-Za-z0-9_\-+/=]+\.[A-Za-z0-9_\-+/=]*", RegexOptions.Compiled);

    public static void Write(string type, string tokenId, string address, string command = null, string result = null, string reason = null)
    {
        var entry = new JObject
        {
            ["time"] = DateTime.UtcNow.ToString("o"),
            ["event"] = type,
            ["token"] = tokenId ?? "owner",
            ["name"] = tokenId == null ? "owner" : ConsoleTokenStore.Find(tokenId)?.Name,
            ["ip"] = address,
        };
        if (command != null) entry["command"] = Redact(command.Length > MaxCommandLength ? command.Substring(0, MaxCommandLength) : command);
        if (result != null) entry["result"] = result;
        if (reason != null) entry["reason"] = reason;

        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > RollAt)
                {
                    var old = FilePath + ".1";
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(FilePath, old);
                }

                File.AppendAllText(FilePath, entry.ToString(Formatting.None) + "\n", new UTF8Encoding(false));
            }
        }
        catch (Exception e)
        {
            TavernLogger.Warn($"Could not write console_token_log.jsonl: {e.Message}");
        }
    }

    // The newest lines, oldest first, for one token ("owner" for the owner token) or all of them.
    public static List<JObject> Tail(string tokenId, int count)
    {
        lock (Sync)
        {
            if (!File.Exists(FilePath)) return [];

            var entries = new List<JObject>();
            foreach (var line in File.ReadAllLines(FilePath).Reverse())
            {
                JObject entry;
                try
                {
                    using var reader = new JsonTextReader(new StringReader(line)) { DateParseHandling = DateParseHandling.None };
                    entry = JObject.Load(reader);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(tokenId) && entry.Value<string>("token") != tokenId) continue;
                entries.Add(entry);
                if (entries.Count >= count) break;
            }

            entries.Reverse();
            return entries;
        }
    }

    // Anything shaped like a token, in a command or its output, becomes [token].
    public static string Redact(string text) => text == null ? null : TokenText.Replace(text, "[token]");
}
