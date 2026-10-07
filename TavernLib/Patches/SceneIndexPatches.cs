using Alta.Api.DataTransferModels.Models.Responses;
using HarmonyLib;

namespace TavernLib.Patches;

[HarmonyPatch]
public class SceneIndexPatches
{
    [HarmonyPatch(typeof(JoinedServerGameMode), MethodType.Constructor, [typeof(GameServerInfo)]), HarmonyPostfix]
    public static void OverrideSceneIndex(GameServerInfo server)
    {
        if (CommandLineArguments.Contains(TavernArgs.QuestScene))
        {
            server.SceneIndex = 4; // Switch to Overworld Chunked [Q]
        }
    }
}