using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alta.Api.DataTransferModels.Models.Responses;
using Alta.Console;
using Alta.WebServer;
using ATT.Character.QuickAccessMenu;
using HarmonyLib;

namespace TavernLib.Patches;

[HarmonyPatch]
public class CommandSecurityPatches
{
    [HarmonyPatch(typeof(WebServerThread), MethodType.Constructor), HarmonyPrefix]
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
    
    [HarmonyPatch(typeof(ServerConsoleManager), nameof(ServerConsoleManager.StartRemoteConsole)), HarmonyPrefix]
    public static bool InstantCloseRemoteConsole(ServerConsoleManager __instance)
    {
        TavernLogger.Msg($"Closing ServerRemoteConsole instantly (unnecessary handler)");
        __instance.CloseConsole(__instance.remoteConsole);
        return false;
    }
    
    [HarmonyPatch(typeof(WebSocketCommandHandler), nameof(WebSocketCommandHandler.GetCurrentServerPermissionsForLoggedInUser)), HarmonyPrefix]
    public static bool SortPermissions(ref Task<IEnumerable<GroupPermissions>> __result)
    {
        __result = Task.FromResult(new List<GroupPermissions>
        {
            GroupPermissions.Console
        }.AsEnumerable());
        return false;
    }
}