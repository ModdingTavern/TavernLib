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
    
    
}