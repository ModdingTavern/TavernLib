using Alta.Networking.Servers;
using HarmonyLib;
using TavernLib.Backend.Api;
using TavernLib.Services;

namespace TavernLib.Patches;

[HarmonyPatch]
public class PlayerLimitPatch
{
    [HarmonyPatch(typeof(ServerHandler), nameof(ServerHandler.PlayerLimit), MethodType.Getter), HarmonyPostfix]
    public static void SetPlayerLimit(ref int __result)
    {
        __result = TavernServices.GetService<TavernApiManager>().ServerConfig.LastRead?.MaxPlayers ?? 0;
    }
}