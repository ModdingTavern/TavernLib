using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alta.Api.Client.LowLevel;
using Alta.Api.DataTransferModels.Models.Responses;
using Alta.Console;
using Alta.WebServer;
using ATT.Character.QuickAccessMenu;
using HarmonyLib;

namespace TavernLib.Patches;

[HarmonyPatch]
public class CommandSecurityPatches
{
    [HarmonyPatch(typeof(WebServerThread), MethodType.Constructor, [typeof(int)]), HarmonyPrefix]
    public static bool CancelWebServerThread()
    {
        TavernLogger.Msg("Cancelling instantiation of WebServerThread");
        return false;
    }

    [HarmonyPatch(typeof(CommandSync), nameof(CommandSync.RouteCommand)), HarmonyPrefix]
    public static bool CancelRouteCommand(string command) // Unsure of if this is necessary
    {
        TavernLogger.Msg($"Cancelling RouteCommand attempt: {command}");
        return false;
    }

    [HarmonyPatch(typeof(ServerConsoleManager), nameof(ServerConsoleManager.StartRemoteConsole)), HarmonyPostfix]
    public static void InstantCloseRemoteConsole(ServerConsoleManager __instance)
    {
        TavernLogger.Msg($"Closing ServerRemoteConsole instantly (unnecessary handler)");
        __instance.CloseConsole(__instance.remoteConsole);
    }


    [HarmonyPatch(typeof(WebSocketCommandHandler), nameof(WebSocketCommandHandler.GetCurrentServerPermissionsForLoggedInUser)), HarmonyPrefix]
    public static bool SortPermissions(ref Task<IEnumerable<GroupPermissions>> __result)
    {
        TavernLogger.Msg("Bypassing GetCurrentServerPermissionsForLoggedInUser (temp?)");
        
        __result = Task.FromResult(new List<GroupPermissions>
        {
            GroupPermissions.Console
        }.AsEnumerable());
        return false;
    }

    [HarmonyPatch(typeof(LowLevelApiClient), nameof(LowLevelApiClient.ValidateIdentityToken)), HarmonyPrefix]
    public static bool SkipValidateIdentityToken(ref Task<BooleanResponse> __result)
    {
        TavernLogger.Msg("Bypassing ValidateIdentityToken (we have no central validator!)");
        __result = Task.FromResult(new BooleanResponse(true));
        return false;
    }
}