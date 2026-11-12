using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Alta.Api.DataTransferModels.Models.Responses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TavernLib.Backend.Api;
using TavernLib.Backend.Auth;
using TavernLib.Services;

namespace TavernLib.Backend.RemoteConsole;

// Console logins: the server secret, the owner token in console_token.txt, named tokens, and the
// check every console login goes through.
internal static class ConsoleAuth
{
    public const string TokenPolicy = "tavern_token:";

    private static readonly string SecretPath = Path.Combine(TavernDirectories.ModdingTavern, "server_secret.key");
    private static readonly string OwnerTokenPath = Path.Combine(TavernDirectories.ModdingTavern, "console_token.txt");
    private static readonly string Header = B64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
    private static readonly string[] BasePolicies =
        ["offline", "play_offline", "server_access_pre_alpha", "game_access_public", "debug_features", "database_admin", "reuse_refresh_tokens"];

    private static readonly object SecretSync = new();
    private static byte[] _secret;
    private static bool _secretErrorLogged;
    private static readonly ConditionalWeakTable<UserInfoWithPermissions, string> TokenIds = new();
    private static readonly AuthLimits LoginLimits = new();

    public enum LoginPath { WebSocket, Other }

    #region Secret

    // Every console token is signed with this, so an existing file is never replaced: a file that
    // can't be read refuses console logins until it can, and says so once.
    public static byte[] Secret
    {
        get
        {
            lock (SecretSync)
            {
                if (_secret != null) return _secret;

                _secret = LoadOrCreateSecret();
                if (_secret != null) _secretErrorLogged = false;
                else if (!_secretErrorLogged)
                {
                    _secretErrorLogged = true;
                    TavernLogger.Error("server_secret.key can't be read, so every console login is refused until it can. TavernLib never replaces it, since every console token is signed with it.");
                }

                return _secret;
            }
        }
    }

    private static byte[] LoadOrCreateSecret()
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                if (!File.Exists(SecretPath) && TryCreateSecret(out var created)) return created;

                var secret = HexToBytes(File.ReadAllText(SecretPath).Trim());
                if (secret?.Length == 32) return secret;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            // Another program (the launcher) may be writing it right now.
            Thread.Sleep(100);
        }

        return null;
    }

    // Written to a temp file and moved in, which fails if the file appeared meanwhile; then the
    // existing one is read instead.
    private static bool TryCreateSecret(out byte[] secret)
    {
        secret = new byte[32];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(secret);

        Directory.CreateDirectory(Path.GetDirectoryName(SecretPath));
        var temp = SecretPath + ".tmp";
        File.WriteAllText(temp, BytesToHex(secret));
        try
        {
            File.Move(temp, SecretPath);
            return true;
        }
        catch (IOException)
        {
            File.Delete(temp);
            return false;
        }
    }

    #endregion

    #region Tokens

    // The owner token: the same text the launcher writes, so either one can write the file.
    private static string OwnerToken(byte[] secret) => Sign(B64Url(Encoding.UTF8.GetBytes(
        "{\"UserId\":\"0\",\"Username\":\"Server\",\"role\":\"Access\"," +
        "\"is_verified\":\"True\",\"is_member\":\"True\",\"server_id\":\"-1\"," +
        "\"Policy\":[\"offline\",\"play_offline\",\"server_access_pre_alpha\"," +
        "\"game_access_public\",\"server_owner\",\"debug_features\"," +
        "\"database_admin\",\"reuse_refresh_tokens\"]," +
        "\"exp\":9999999999,\"iss\":\"AltaWebAPI\",\"aud\":\"AltaClient\"}")), secret);

    // Writes console_token.txt when it is missing or no longer matches the secret.
    public static void EnsureOwnerToken()
    {
        try
        {
            var secret = Secret;
            if (secret == null) return;

            if (File.Exists(OwnerTokenPath) && Verify(File.ReadAllText(OwnerTokenPath).Trim(), secret, out _)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(OwnerTokenPath));
            var temp = OwnerTokenPath + ".tmp";
            File.WriteAllText(temp, OwnerToken(secret));
            if (File.Exists(OwnerTokenPath)) File.Replace(temp, OwnerTokenPath, null);
            else File.Move(temp, OwnerTokenPath);
        }
        catch (Exception e)
        {
            TavernLogger.Error($"Could not write console_token.txt: {e.Message}");
        }
    }

    // A named token's text depends only on its row, so the same row always gives the same text.
    // The payload is standard base64: the game reads the payload with Convert.FromBase64String, so
    // base64url's '-' and '_' would stop the login before TavernLib sees it.
    public static string Mint(ConsoleToken token)
    {
        var secret = Secret ?? throw new InvalidOperationException("server_secret.key can't be read");
        var policies = new JArray(BasePolicies);
        if (token.IsFull) policies.Add("server_owner");
        policies.Add(TokenPolicy + token.Id);

        var payload = new JObject
        {
            ["UserId"] = "0",
            ["Username"] = "token " + token.Id,
            ["role"] = "Access",
            ["is_verified"] = "True",
            ["is_member"] = "True",
            ["server_id"] = "-1",
            ["Policy"] = policies,
            ["jti"] = token.Id,
            ["gen"] = token.Generation,
            ["iat"] = new DateTimeOffset(DateTime.SpecifyKind(token.Created, DateTimeKind.Utc)).ToUnixTimeSeconds(),
            ["exp"] = 9999999999L,
            ["iss"] = "AltaWebAPI",
            ["aud"] = "AltaClient",
        };
        var body = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload.ToString(Formatting.None))).TrimEnd('=');
        return Sign(body, secret);
    }

    private static string Sign(string payload, byte[] secret)
    {
        var signed = Header + "." + payload;
        using var hmac = new HMACSHA256(secret);
        return signed + "." + B64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(signed)));
    }

    // Checks the signature in constant time and hands back the signed payload's claims.
    private static bool Verify(string raw, byte[] secret, out JObject claims)
    {
        claims = null;
        var parts = raw.Split('.');
        if (parts.Length != 3) return false;

        byte[] expected;
        using (var hmac = new HMACSHA256(secret))
            expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(parts[0] + "." + parts[1]));

        var given = FromB64(parts[2].Trim());
        if (given == null || given.Length != expected.Length) return false;

        var difference = 0;
        for (var i = 0; i < expected.Length; i++) difference |= expected[i] ^ given[i];
        if (difference != 0) return false;

        var json = FromB64(parts[1]);
        if (json == null) return false;

        claims = JObject.Parse(Encoding.UTF8.GetString(json));
        return true;
    }

    #endregion

    #region Login

    // The game hands the validator a wrapper it built from the token: the payload's claims, then a
    // "raw" claim with the token text. Only that text is verified, and every claim read comes from
    // its signed payload. A payload with a raw claim of its own is refused, since it would be found
    // first and let an unsigned wrapper borrow another token's signature.
    public static bool Validate(JwtSecurityToken wrapper, LoginPath path, string address, out string tokenId, out string reason)
    {
        tokenId = null;

        if (address != null && LoginLimits.HeldOff(address))
        {
            reason = "too many failed logins";
            return false;
        }

        reason = Check(wrapper, path, address, ref tokenId);
        if (reason == null) return true;

        if (address != null) LoginLimits.Fail(address);
        return false;
    }

    private static string Check(JwtSecurityToken wrapper, LoginPath path, string address, ref string tokenId)
    {
        var raws = wrapper.Claims.Where(claim => claim.Type == "raw").ToList();
        if (raws.Count != 1) return "the token carries a raw claim of its own";

        var secret = Secret;
        if (secret == null) return "server_secret.key can't be read";

        JObject claims;
        try
        {
            if (!Verify(raws[0].Value, secret, out claims)) return "bad signature";
        }
        catch (JsonException)
        {
            return "unreadable payload";
        }

        if (claims["exp"] != null && (!long.TryParse(claims.Value<string>("exp"), out var exp) || exp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
            return "expired";

        var policies = PoliciesOf(claims);
        var named = policies.Where(policy => policy.StartsWith(TokenPolicy, StringComparison.Ordinal)).ToList();
        if (named.Count > 1) return "more than one token id";

        if (named.Count == 0)
        {
            if (!policies.Contains("server_owner")) return "not a console token";
            if (OwnerLocalOnly() && !IsLoopback(address)) return "the owner token is local only (console_owner_local_only)";
            return null;
        }

        var id = named[0].Substring(TokenPolicy.Length);
        tokenId = id;

        // Port 1758 runs every command as the server itself, whatever token logged in.
        if (path != LoginPath.WebSocket) return "named tokens work on port 1760 only";

        if (!ConsoleTokenStore.Loaded) return "console_tokens.json can't be read";
        var token = ConsoleTokenStore.Find(id);
        if (token == null) return "unknown token";

        var status = token.Status(DateTime.UtcNow);
        if (status != "active") return status;
        if (claims.Value<string>("gen") != token.Generation.ToString()) return "replaced by a rotated token";
        if (policies.Contains("server_owner") != token.IsFull) return "access changed since it was made";

        ConsoleTokenStore.Touch(id);
        return null;
    }

    private static List<string> PoliciesOf(JObject claims)
    {
        var token = claims["Policy"];
        var values = token is JArray array ? array.Select(item => item.Value<string>()) : [token?.Value<string>()];
        return values.Where(value => value != null).SelectMany(value => value.Split(',')).ToList();
    }

    private static bool OwnerLocalOnly()
    {
        var config = TavernServices.GetService<TavernManager>()?.ServerConfig;
        if (config == null) return false;

        try
        {
            return config.Read().ConsoleOwnerLocalOnly;
        }
        catch (Exception)
        {
            return config.LastRead.ConsoleOwnerLocalOnly;
        }
    }

    private static bool IsLoopback(string address) =>
        address != null && IPAddress.TryParse(address, out var ip) && IPAddress.IsLoopback(ip);

    // The named token id a console login carries, or null for the owner token and the server itself.
    public static string TokenIdOf(UserInfoWithPermissions user)
    {
        if (user == null) return null;

        var id = TokenIds.GetValue(user, u =>
            u.Policies?.FirstOrDefault(policy => policy.StartsWith(TokenPolicy, StringComparison.Ordinal))?.Substring(TokenPolicy.Length) ?? "");
        return id.Length == 0 ? null : id;
    }

    #endregion

    #region Encoding

    public static string B64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    // Reads base64 and base64url alike; null when it isn't either.
    private static byte[] FromB64(string text)
    {
        try
        {
            text = text.Replace('-', '+').Replace('_', '/');
            return Convert.FromBase64String(text.PadRight(text.Length + (4 - text.Length % 4) % 4, '='));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static byte[] HexToBytes(string hex)
    {
        if (hex.Length % 2 != 0) return null;

        var result = new byte[hex.Length / 2];
        for (var i = 0; i < result.Length; i++)
            if (!byte.TryParse(hex.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out result[i])) return null;
        return result;
    }

    private static string BytesToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    #endregion
}
