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
                
            }
            catch (Exception e)
            {
                Tavern.Logger.Error($"Error when filtering join request! {e}");
                throw;
            }
        }
        
        private static async Task<bool> FilterFlyCam(Connection connection, Stream stream)
        {
            try
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
            
                ServerHandler.Current.playerJoinHandler.CheckApproved(connection, stream);
                return true;
            }
            catch (Exception e)
            {
                Tavern.Logger.Error($"Error when handling FilterFlyCam security patch! {e}");
                throw;
            }
        }
    }
}