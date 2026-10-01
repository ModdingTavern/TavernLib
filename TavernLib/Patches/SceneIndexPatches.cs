using Alta.Api.DataTransferModels.Models.Responses;
using HarmonyLib;

namespace TavernLib.Patches;

[HarmonyPatch]
public class SceneIndexPatches
{
    internal static int? ForcedSceneIndex()
    {
        if (CommandLineArguments.Contains(TavernArgs.TutorialScene)) return 1;
        if (CommandLineArguments.Contains(TavernArgs.QuestScene)) return 4;
        return null;
    }

    [HarmonyPatch(typeof(JoinedServerGameMode), MethodType.Constructor, [typeof(GameServerInfo)]), HarmonyPostfix]
    public static void OverrideSceneIndex(GameServerInfo server)
    {
        if (ForcedSceneIndex() is int index) server.SceneIndex = index;
    }
}