using System.Reflection;
using HarmonyLib;
using TavernLib.Backend.Api;
using TavernLib.Services;

namespace TavernLib.Patches;

// GarbageCollectTimer's interval field is Inspector data baked into a
// prefab, not code. This overrides it with garbage_collection_timer from
// server_settings.json, so server owners can tune it themselves --
// defaults to 30f (the game's own shipped value) if that key isn't
// present in the file.
[HarmonyPatch]
public class GarbageCollectIntervalPatch
{
    private static readonly FieldInfo IntervalField =
        typeof(GarbageCollectTimer).GetField("interval", BindingFlags.NonPublic | BindingFlags.Instance);

    [HarmonyPatch(typeof(GarbageCollectTimer), "Start"), HarmonyPrefix]
    public static void OverrideInterval(GarbageCollectTimer __instance)
    {
        var manager = TavernServices.GetService<TavernManager>();
        if (manager == null) return;

        manager.ServerConfig.ReadFromFile();
        IntervalField?.SetValue(__instance, manager.ServerConfig.LastRead.GarbageCollectionTimer);
    }
}
