using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Alta.Api.DataTransferModels.Models.Responses;
using Alta.Console;
using HarmonyLib;
using MelonLoader;
using NLog;
using NLog.Config;
using NLog.Filters;
using TavernLib.Backend.RemoteConsole;
using TavernLib.ModulePorts;

namespace TavernLib.Patches;

[HarmonyPatch]
public static class ConsoleTokenPatches
{
    private static readonly FieldInfo ConnectionField = AccessTools.Field(typeof(WebSocketCommandHandler), "connection");

    [ThreadStatic] private static WebSocketCommandHandler _authorizing;
    [ThreadStatic] private static bool? _verdict;

    // Server only: the connect and disconnect lines, and the filter that keeps refused tokens out
    // of the game's own log.
    internal static void Start()
    {
        ServerConsoleManager.ConsoleConnected += (user, handler) =>
            ConsoleTokenLog.Write("connect", ConsoleAuth.TokenIdOf(user), AddressOf(handler));
        ServerConsoleManager.ConsoleDisconnected += (user, handler) =>
            ConsoleTokenLog.Write("disconnect", ConsoleAuth.TokenIdOf(user), AddressOf(handler));

        MelonEvents.OnApplicationLateStart.Subscribe(() =>
        {
            RefusedLoginFilter.AddTo(LogManager.Configuration);
            LogManager.ConfigurationChanged += (_, args) => RefusedLoginFilter.AddTo(args.ActivatedConfiguration);
        });
    }

    // The game checks the token from inside ProcessAuthorization, on the same thread and before
    // its first await, so this tells the check that the login came through 1760, and from where.
    [HarmonyPatch(typeof(WebSocketCommandHandler), nameof(WebSocketCommandHandler.ProcessAuthorization)), HarmonyPrefix]
    public static void MarkWebSocketLogin(WebSocketCommandHandler __instance) => _authorizing = __instance;

    [HarmonyPatch(typeof(WebSocketCommandHandler), nameof(WebSocketCommandHandler.ProcessAuthorization)), HarmonyFinalizer]
    public static void UnmarkWebSocketLogin() => _authorizing = null;

    [HarmonyPatch(typeof(ServerConsoleManager), nameof(ServerConsoleManager.ValidateConsoleToken)), HarmonyPrefix]
    public static bool CheckConsoleLogin(JwtSecurityToken token, ref Task<bool> __result)
    {
        var handler = _authorizing;
        var address = AddressOf(handler);
        string tokenId = null;
        string reason;
        bool ok;

        try
        {
            ok = ConsoleAuth.Validate(token, handler != null ? ConsoleAuth.LoginPath.WebSocket : ConsoleAuth.LoginPath.Other, address, out tokenId, out reason);
        }
        catch (Exception e)
        {
            ok = false;
            reason = "error: " + e.Message;
        }

        if (!ok && reason != "too many failed logins")
        {
            TavernLogger.Warn($"Console login refused from {address ?? "an unknown address"}: {reason}");
            ConsoleTokenLog.Write("bad_login", tokenId, address, reason: reason);
        }

        _verdict = ok;
        __result = Task.FromResult(ok);
        return false;
    }

    // TavernLib owns the verdict: a postfix runs after every prefix, so another mod's prefix can't
    // overwrite it.
    [HarmonyPatch(typeof(ServerConsoleManager), nameof(ServerConsoleManager.ValidateConsoleToken)), HarmonyPostfix]
    public static void KeepConsoleVerdict(ref Task<bool> __result)
    {
        if (_verdict is { } verdict) __result = Task.FromResult(verdict);
        _verdict = null;
    }

    // Every command and module is checked here while a command is looked up and while help lists
    // them, so a named token can neither run nor see what it wasn't given.
    [HarmonyPatch(typeof(CommandPart), nameof(CommandPart.RequirementsAreMet)), HarmonyPostfix]
    public static void GateNamedTokens(CommandPart __instance, CommandContext context, ref bool __result)
    {
        if (!__result) return;

        var id = ConsoleAuth.TokenIdOf(context?.CallingUser);
        if (id == null) return;

        try
        {
            __result = ConsoleTokenStore.Allows(id, __instance);
        }
        catch (Exception)
        {
            __result = false;
        }
    }

    // Logs each 1760 command, and keeps token text out of the CommandExecuted event every
    // subscriber gets.
    [HarmonyPatch(typeof(WebSocketCommandHandler), nameof(WebSocketCommandHandler.TryLogConsoleCommandExecutionWithEventStream)), HarmonyPrefix]
    public static void LogConsoleCommand(WebSocketCommandHandler __instance, string command, bool wasSuccessful, ref string output)
    {
        try
        {
            output = ConsoleTokenLog.Redact(output);
            var id = ConsoleAuth.TokenIdOf(__instance.UserInfo);
            var result = wasSuccessful ? "ok" : id != null && WouldMatchUnrestricted(command, __instance.UserInfo) ? "refused" : "failed";
            ConsoleTokenLog.Write("command", id, AddressOf(__instance), command, result);
        }
        catch (Exception e)
        {
            TavernLogger.Warn($"Could not log a console command: {e.Message}");
        }
    }

    // The game logs every command result at Trace; the token commands' results hold token text.
    [HarmonyPatch(typeof(CommandResult), nameof(CommandResult.Log)), HarmonyPrefix]
    public static bool QuietTokenResults(CommandResult __instance) => __instance.Command?.Parent?.Name != TokenCommandModule.ModuleName;

    // A named token's failed command was refused rather than failed when the same text finds a
    // command once the token's own limits are left out.
    private static bool WouldMatchUnrestricted(string command, UserInfoWithPermissions user)
    {
        var policies = user.Policies.Where(policy => !policy.StartsWith(ConsoleAuth.TokenPolicy, StringComparison.Ordinal));
        var context = new CommandContext(new UserInfoWithPermissions(user.Identifier, user.Username, true, policies));
        var found = CommandService.Search(CommandService.CommandCollection, command, context);
        return found != null && found.Any(result => result.success == CommandResultSuccess.Success);
    }

    // Closes every 1760 session of a named token. Returns how many.
    internal static int CloseSessions(string id) => Close(handler => ConsoleAuth.TokenIdOf(handler.UserInfo) == id, "console token " + id);

    // Closes every 1760 session logged in with the owner token but keep. Returns how many.
    internal static int CloseOwnerSessions(WebSocketCommandHandler keep) =>
        Close(handler => handler != keep && handler.UserInfo != null && ConsoleAuth.TokenIdOf(handler.UserInfo) == null, "the owner token");

    private static int Close(Func<IRemoteConsoleHandler, bool> which, string whose)
    {
        var closed = 0;
        foreach (var handler in ConnectedHandlers())
        {
            if (!which(handler) || handler is not WebSocketCommandHandler socket) continue;

            try
            {
                var connection = ConnectionField?.GetValue(socket);
                AccessTools.Method(connection?.GetType(), "Close", Type.EmptyTypes)?.Invoke(connection, null);
                closed++;
            }
            catch (Exception e)
            {
                TavernLogger.Warn($"Could not close a session of {whose}: {e.Message}");
            }
        }

        return closed;
    }

    internal static int CountSessions(string id) => ConnectedHandlers().Count(handler => ConsoleAuth.TokenIdOf(handler.UserInfo) == id);

    // The game adds and removes handlers from other threads.
    private static IRemoteConsoleHandler[] ConnectedHandlers()
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return ServerConsoleManager.ConnectedHandlers.ToArray();
            }
            catch (InvalidOperationException) when (attempt < 5)
            {
            }
        }
    }

    // "ip:port" as the game keeps it; null for a session that isn't on 1760.
    private static string AddressOf(IRemoteConsoleHandler handler)
    {
        var text = (handler as WebSocketCommandHandler)?.connectionString;
        if (string.IsNullOrEmpty(text)) return null;

        var colon = text.LastIndexOf(':');
        return colon > 0 ? text.Substring(0, colon) : text;
    }

    // The game logs the whole text of a refused console token (and 1758 logs every token it
    // gets). Those lines are dropped; TavernLib logs the refusal itself, without the token.
    private sealed class RefusedLoginFilter : NLog.Filters.Filter
    {
        public static void AddTo(LoggingConfiguration config)
        {
            if (config == null) return;

            foreach (var rule in config.LoggingRules) Add(rule);
            LogManager.ReconfigExistingLoggers();
        }

        private static void Add(LoggingRule rule)
        {
            if (!rule.Filters.Any(filter => filter is RefusedLoginFilter))
            {
                // A rule with no filters writes everything; once it has one, whatever no filter
                // decides takes FilterDefaultAction, which NLog 5 sets to Ignore. Neutral keeps the
                // rule writing what it wrote before.
                if (rule.Filters.Count == 0) rule.FilterDefaultAction = FilterResult.Neutral;
                rule.Filters.Insert(0, new RefusedLoginFilter { Action = FilterResult.Ignore });
            }

            foreach (var child in rule.ChildRules) Add(child);
        }

        protected override FilterResult Check(LogEventInfo logEvent)
        {
            var message = logEvent.Message;
            if (message == null) return FilterResult.Neutral;

            var drop = logEvent.LoggerName switch
            {
                "Alta.Console.WebSocketCommandHandler" =>
                    message.StartsWith("[WebSocketCommandHandler]: Invalid authorization for ", StringComparison.Ordinal) ||
                    message.StartsWith("[WebSocketCommandHandler]: Authorization failed for ", StringComparison.Ordinal) ||
                    message.StartsWith("[WebSocketCommandHandler]: Failed authorizing websocket for ", StringComparison.Ordinal),
                "ServerRemoteConsole" => message.StartsWith("Received token: ", StringComparison.Ordinal),
                "Alta.WebServer.AltaConsoleModule" => message.StartsWith("Invalid REST console authorization: ", StringComparison.Ordinal),
                _ => false,
            };
            return drop ? Action : FilterResult.Neutral;
        }
    }
}
