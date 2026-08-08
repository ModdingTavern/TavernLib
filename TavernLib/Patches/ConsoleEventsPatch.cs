using Alta.Console;
using Alta.Networking.Servers;
using HarmonyLib;

namespace TavernLib.Patches;

[HarmonyPatch]
public class ConsoleEventsPatch
{
    [HarmonyPatch(typeof(ServerPlayerConnectionHandlerOld), MethodType.Constructor, [typeof(ServerHandler)]), HarmonyPostfix]
    public static void PlayerLeavePatch(ServerPlayerConnectionHandlerOld __instance)
    {
        __instance.UserLeft += connection =>
        {
            if (connection.player != null) ConsoleEvents.PlayerLeft.Invoke(() => new PlayerJoinLeaveData(connection.Player));
        };
    }
    
    [HarmonyPatch(typeof(Player), nameof(Player.InitializePlayerOnServer)), HarmonyPostfix]
    public static void PlayerJoinPatch(Player __instance)
    {
        ConsoleEvents.PlayerJoined.Invoke(() => new PlayerJoinLeaveData(__instance));
    }
}