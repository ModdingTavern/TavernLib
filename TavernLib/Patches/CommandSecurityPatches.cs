using Alta.WebServer;
using HarmonyLib;

namespace TavernLib.Patches;

[HarmonyPatch]
public class CommandSecurityPatches
{
    [HarmonyPatch(typeof(WebServerThread), MethodType.Constructor), HarmonyPrefix]
    public static void CancelWebServerThread()
    {
        TavernLogger.Msg("Cancelling instantiation of WebServerThread");
    }
    
    
}