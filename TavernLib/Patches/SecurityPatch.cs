using System;
using System.Threading.Tasks;
using Alta.Networking;
using Alta.Networking.Servers;
using Alta.Serialization;
using HarmonyLib;
using MelonLoader.Logging;

namespace TavernLib.Patches
{
    [HarmonyPatch]
    public static class SecurityPatch
    {
        [HarmonyPatch(typeof(ServerPlayerConnectionHandlerOld), nameof(ServerPlayerConnectionHandlerOld.InitializeConnection)), HarmonyPrefix]
        public static bool FlyCamFilter(Connection connection)
        {
            connection.SetHandler(MessageType.RequestJoin, FilterJoinRequest);
            return false;
        }

        private static async void FilterJoinRequest(Connection connection, Stream stream)
        {
            Tavern.Logger.Msg(ColorARGB.Chartreuse, $"Filtering join request for user at IP {connection.IpAddress}");

            try
            {
                Tavern.Logger.Msg(ColorARGB.Chartreuse, "Filtering flycam joiners");
                if (!await FilterFlyCam(connection, stream)) return;

                Tavern.Logger.Msg("User passed flycam check, checking for valid account token");
                if (!await FilterInvalidTokens(connection, stream)) return;

                Tavern.Logger.Msg("User passed token check, moving onto vanilla check");
                ServerHandler.Current.playerJoinHandler.CheckApproved(connection, stream);
            }
            catch (Exception e)
            {
                Tavern.Logger.Error($"Error when filtering join request! {e}");
                await ServerPlayerConnectionHandlerOld.PlayerDenied(connection, "Error when checking authenticity");
                throw;
            }
        }

        private static async Task<bool> FilterInvalidTokens(Connection connection, Stream stream)
        {
        }

        private static async Task<bool> FilterFlyCam(Connection connection, Stream stream)
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
                    return false;
                }
            }
            catch (Exception e)
            {
                Tavern.Logger.Error($"Error in SecurityPatch {e}");
                await ServerPlayerConnectionHandlerOld.PlayerDenied(connection, "Unhandled error.");
                return false;
            }

            return true;
        }
    }
}