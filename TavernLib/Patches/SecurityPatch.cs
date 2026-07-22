using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Alta.Api.DataTransferModels.Converters;
using Alta.Networking;
using Alta.Networking.Servers;
using Alta.Serialization;
using HarmonyLib;
using MelonLoader.Logging;
using TavernLib.Backend.Api;
using TavernLib.Services;

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
            TavernLogger.Msg($"Filtering join request for user at IP {connection.IpAddress}");

            try
            {
                TavernLogger.Msg("Filtering flycam joiners");
                if (!await FilterFlyCam(connection, stream)) return;

                TavernLogger.Msg("User passed flycam check, checking for valid account token");
                if (!await FilterInvalidTokens(connection, stream)) return;

                TavernLogger.Msg("User passed token check, moving onto vanilla check");
                ServerHandler.Current.playerJoinHandler.CheckApproved(connection, stream);
            }
            catch (Exception e)
            {
                TavernLogger.Error($"Error when filtering join request! {e}");
                await ServerPlayerConnectionHandlerOld.PlayerDenied(connection, "Error when checking authenticity");
                throw;
            }
        }

        private static async Task<bool> FilterInvalidTokens(Connection connection, Stream stream)
        {
            try
            {
                using var readingStream = stream.Clone() as Stream;
                var requestJoinMessage = new RequestJoinMessage();
                requestJoinMessage.Serialize(connection, readingStream);

                TavernLogger.Msg($"User trying to join with JWT {requestJoinMessage.UserCredentials}");

                var token = JWTUtility.CreateFromString(requestJoinMessage.UserCredentials, true);
                var tavernToken = token.Claims.FirstOrDefault(claim => claim.Type == "TavernToken")?.Value;
                var id = ulong.Parse(token.Claims.FirstOrDefault(claim => claim.Type == "UserId")?.Value ?? "0");
                var username = token.Claims.FirstOrDefault(claim => claim.Type == "Username")?.Value ?? "";
                
                var users = TavernServices.GetService<TavernApiManager>().UserConfig.LastRead.Users;
                if (users.TryGetValue(username.ToLowerInvariant(), out var user) && user.UserId == id && user.Token == tavernToken) return true;
                
                await ServerPlayerConnectionHandlerOld.PlayerDenied(connection, "Bad token");
                return false;

            }

            catch (Exception e)
            {
                TavernLogger.Error($"Error in FilterInvalidTokens! {e}");
                throw;
            }
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
                    TavernLogger.Warn($"User kicked for bizarre mode");
                    await ServerPlayerConnectionHandlerOld.PlayerDenied(connection, "Bizarre mode detected.");
                    return false;
                }
            }
            catch (Exception e)
            {
                TavernLogger.Error($"Error in FilterFlyCam {e}");
                await ServerPlayerConnectionHandlerOld.PlayerDenied(connection, "FlyCam filter error.");
                return false;
            }

            return true;
        }
    }
}