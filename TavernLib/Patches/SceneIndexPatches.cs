using Alta.Api.DataTransferModels.Models.Responses;
using HarmonyLib;

namespace TavernLib.Patches;



public class SceneIndexPatches
{
    [HarmonyPatch(typeof(JoinedServerGameMode), MethodType.Constructor), HarmonyPostfix]
    public static void OverrideSceneIndex(GameServerInfo server)
    {
        if (CommandLineArguments.Contains(TavernArgs.QuestScene))
        {
            server.SceneIndex = 4; // Switch to Overworld Chunked [Q]
        }
    }
}