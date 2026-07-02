using System.Diagnostics;
using Alta.Customization;
using HarmonyLib;
using MelonLoader.Logging;

namespace TavernLib.Cosmetics
{
    [HarmonyPatch]
    public class CosmeticsPatches
    {
        [HarmonyPatch(typeof(ServerHostingGameMode), nameof(ServerHostingGameMode.OnStartSucceeded)), HarmonyPostfix]
        public static void LoadCosmeticsIntoRamIfAppropriate()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            Tavern.Logger.Msg(ColorARGB.Chartreuse, "Loading cosmetics into RAM for server...");
            Purchasable.LoadAllIntoRAM();
            stopwatch.Stop();
            Tavern.Logger.Msg(ColorARGB.Chartreuse, $"Time taken to load cosmetics into RAM: {stopwatch.Elapsed.TotalMilliseconds}ms");
        }
    }
}