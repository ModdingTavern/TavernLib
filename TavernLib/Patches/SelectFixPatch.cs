using Alta.Console.Commands;
using HarmonyLib;

namespace TavernLib.Patches;

[HarmonyPatch(typeof(SelectionCommandModule))]
public class SelectFixPatch
{
    [HarmonyPatch(nameof(SelectionCommandModule.FindObjects))]
    public static void Postfix()
    {
        
    }
}