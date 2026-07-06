using Alta.Api.Client.HighLevel;
using HarmonyLib;
using TavernLib.Api.Implementations;

namespace TavernLib.Patches
{
    [HarmonyPatch]
    class ApiPatches
    {
        [HarmonyPatch(typeof(HighLevelApiClientFactory), nameof(HighLevelApiClientFactory.CreateHighLevelClient)), HarmonyPrefix]
        static bool HighLevelApiShim(ref IHighLevelApiClient __result)
        {
            __result = new ModdedHighLevelApiClient();
            return false;
        }
    }
}