using System;
using Alta.Networking;
using Alta.Networking.Servers;
using Alta.Serialization;
using HarmonyLib;

namespace TavernLib.Patches
{
    [HarmonyPatch]
    public static class SecurityPatch
    {
        [HarmonyPatch(typeof(ServerPlayerConnectionHandlerOld), nameof(ServerPlayerConnectionHandlerOld)), HarmonyPrefix]
        public static bool RejectFlyCam(Connection connection, Stream stream, ServerPlayerConnectionHandlerOld __instance)
        {
            try
            {
                using var readingStream = stream.Clone() as Stream;

                var requestJoinMessage = new RequestJoinMessage();
                requestJoinMessage.Serialize(connection, stream);

                if (requestJoinMessage.PlayerMode is PlayerMode.Fly or PlayerMode.AutoCam or PlayerMode.Unassigned)
                {
                    _ = ServerPlayerConnectionHandlerOld.PlayerDenied(connection, "Bizarre mode detected.");
                    return false;
                }
            }
            catch (Exception e)
            {
                Tavern.Logger.Error($"Error in SecurityPatch {e}");
                _ = ServerPlayerConnectionHandlerOld.PlayerDenied(connection, "Unhandled error.");
                throw;
            }

            return true;
        }
    }
}