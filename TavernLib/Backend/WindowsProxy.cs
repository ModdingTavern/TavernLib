using System;
using System.Linq;
using System.Net;
using Microsoft.Win32;

namespace TavernLib.Backend;

public static class WindowsProxy
{
    private const string InternetSettingsKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    /// <summary>
    /// Builds an IWebProxy from the current user's WinINet proxy settings (the same
    /// settings Windows Settings, Fiddler, and mitmproxy's "system proxy" mode write to
    /// HKCU\...\Internet Settings). Returns null if no proxy is configured/enabled.
    ///
    /// We read the registry directly rather than relying on HttpClientHandler's default
    /// system-proxy detection: this code runs inside Unity's Mono runtime (via MelonLoader),
    /// and Mono's WinINet autodetection is not reliable on Windows, unlike full .NET Framework.
    ///
    /// Note: this does not evaluate PAC/auto-config scripts (AutoConfigURL) - only an
    /// explicitly set "ProxyServer" is honored.
    /// </summary>
    public static IWebProxy CreateSystemProxy()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(InternetSettingsKey);
            if (key == null) return null;

            var enabled = key.GetValue("ProxyEnable") as int? ?? 0;
            if (enabled == 0) return null;

            var proxyServer = key.GetValue("ProxyServer") as string;
            if (string.IsNullOrWhiteSpace(proxyServer)) return null;

            var address = ParseProxyServer(proxyServer, "http");
            if (address == null) return null;

            var proxy = new WebProxy(address, false);

            var overrideList = key.GetValue("ProxyOverride") as string;
            if (!string.IsNullOrWhiteSpace(overrideList))
            {
                var entries = overrideList.Split(';')
                    .Select(e => e.Trim())
                    .Where(e => e.Length > 0);

                proxy.BypassList = entries
                    .Where(e => !e.Equals("<local>", StringComparison.OrdinalIgnoreCase))
                    .Select(RegexEscapeForBypass)
                    .ToArray();

                proxy.BypassProxyOnLocal = entries.Any(e => e.Equals("<local>", StringComparison.OrdinalIgnoreCase));
            }

            TavernLogger.Msg($"Using Windows system proxy for API calls: {address}");
            return proxy;
        }
        catch (Exception e)
        {
            TavernLogger.Warn($"Failed to read Windows system proxy settings, continuing without a proxy: {e}");
            return null;
        }
    }

    /// <summary>
    /// ProxyServer is either a single "host:port" applied to all protocols, or a
    /// per-protocol list like "http=host:port;https=host:port;ftp=host:port".
    /// </summary>
    private static Uri ParseProxyServer(string proxyServer, string protocol)
    {
        string hostPort;
        if (proxyServer.Contains("="))
        {
            hostPort = proxyServer.Split(';')
                .Select(entry => entry.Split(new[] { '=' }, 2))
                .Where(parts => parts.Length == 2 && parts[0].Trim().Equals(protocol, StringComparison.OrdinalIgnoreCase))
                .Select(parts => parts[1].Trim())
                .FirstOrDefault();
        }
        else
        {
            hostPort = proxyServer.Trim();
        }

        if (string.IsNullOrWhiteSpace(hostPort)) return null;

        // Some proxy setups write a bare "host:port" per WinINet's usual convention, but
        // others (seen here: a per-protocol entry like "http=http://host:port") already
        // include a scheme. Only prepend "http://" when one isn't already present, otherwise
        // we double up the scheme and produce an unparseable/unresolvable host.
        var candidate = hostPort.Contains("://") ? hostPort : $"http://{hostPort}";
        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ? uri : null;
    }

    private static string RegexEscapeForBypass(string wildcardEntry)
    {
        // WebProxy.BypassList entries are regexes; ProxyOverride entries are wildcard-style
        // hostnames (e.g. "*.contoso.com"), so escape regex metacharacters and translate "*".
        var escaped = System.Text.RegularExpressions.Regex.Escape(wildcardEntry).Replace(@"\*", ".*");
        return $"^{escaped}$";
    }
}
