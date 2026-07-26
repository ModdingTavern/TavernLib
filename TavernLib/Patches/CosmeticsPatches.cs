using System.Diagnostics;
using Alta.Customization;
using HarmonyLib;

namespace TavernLib.Patches
{
    [HarmonyPatch]
    public class CosmeticsPatches
    {
        [HarmonyPatch(typeof(ServerHostingGameMode), nameof(ServerHostingGameMode.OnStartSucceeded)), HarmonyPostfix]
        public static void LoadCosmeticsIntoRamIfAppropriate()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            TavernLogger.Msg("Loading cosmetics into RAM for server...");
            Purchasable.LoadAllIntoRAM();
            stopwatch.Stop();
            TavernLogger.Msg($"Time taken to load cosmetics into RAM: {stopwatch.Elapsed.TotalMilliseconds}ms");
        }
    }
}