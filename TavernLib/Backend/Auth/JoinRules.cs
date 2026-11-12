using System;
using System.Linq;
using TavernLib.Backend.Server.Configs;

namespace TavernLib.Backend.Auth;

// Who may join, checked on 1762 and again at the game port, which a client can reach without 1762.
internal static class JoinRules
{
    public const int UsernameMaxLength = 16;

    // whitelist_enabled turns the whitelist on, as in the launcher. On a headless server a filled
    // whitelist keeps it on as well: TavernLib never read whitelist_enabled before, and an update
    // must not open those servers to everyone.
    public static bool WhitelistOn(ServerSettings settings, UserConfig config) =>
        settings.WhitelistEnabled ||
        (!CommandLineArguments.Contains(TavernArgs.DontManageAuth) && HasEntries(config.Whitelist));

    public static bool IsBlacklisted(UserConfig config, string username, ulong? userId, string address) =>
        Matches(config.Blacklist, username, userId, address, null);

    // An IP entry lets in anyone joining from that address, and an account registered from it
    // wherever it joins from later, so a home address that changes doesn't lock its players out.
    public static bool IsWhitelisted(UserConfig config, string username, ulong? userId, string address, string registeredFrom) =>
        Matches(config.Whitelist, username, userId, address, registeredFrom);

    // The launcher's rule for names: ASCII letters and digits, space, hyphen and underscore.
    public static bool IsValidName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= UsernameMaxLength &&
        name.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or ' ' or '-' or '_');

    private static bool Matches(UserConfig.ListConfig list, string username, ulong? userId, string address, string registeredFrom) =>
        list != null &&
        ((list.Usernames?.Any(name => string.Equals(name, username, StringComparison.OrdinalIgnoreCase)) ?? false) ||
         (userId.HasValue && (list.UserIds?.Contains(userId.Value) ?? false)) ||
         (address != null && (list.Ips?.Contains(address) ?? false)) ||
         (registeredFrom != null && (list.Ips?.Contains(registeredFrom) ?? false)));

    private static bool HasEntries(UserConfig.ListConfig list) =>
        list != null && ((list.Usernames?.Count ?? 0) > 0 || (list.Ips?.Count ?? 0) > 0 || (list.UserIds?.Count ?? 0) > 0);
}
