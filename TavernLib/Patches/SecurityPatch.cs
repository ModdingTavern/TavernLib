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
        [HarmonyPatch(typeof(ServerPlayerConnectionHandlerOld), nameof(ServerPlayerConnectionHandlerOld.InitializeConnection)), HarmonyPrefix]
        public static bool FlyCamFilter(Connection connection)
        {
            connection.SetHandler(MessageType.RequestJoin, FilterFlyCam);
            return false;
        }
        
        private static async void FilterFlyCam(Connection connection, Stream stream)
        {
            try
            {
                using var readingStream = stream.Clone() as Stream;

                var requestJoinMessage = new RequestJoinMessage();
                requestJoinMessage.Serialize(connection, readingStream);

                if (requestJoinMessage.PlayerMode is PlayerMode.Fly or PlayerMode.AutoCam or PlayerMode.Unassigned)
                {
                    Tavern.Logger.Warning($"User kicked for bizarre mode");
                    await ServerPlayerConnectionHandlerOld.PlayerDenied(connection, "Bizarre mode detected.");
                    return;
                }
            }
            catch (Exception e)
            {
                Tavern.Logger.Error($"Error in SecurityPatch {e}");
                await ServerPlayerConnectionHandlerOld.PlayerDenied(connection, "Unhandled error.");
                return;
            }
            
            ServerHandler.Current.playerJoinHandler.CheckApproved(connection, stream);
        }
    }
}