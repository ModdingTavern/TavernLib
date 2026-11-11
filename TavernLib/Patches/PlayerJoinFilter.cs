using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Alta.Api.DataTransferModels.Converters;
using Alta.Networking;
using Alta.Networking.Servers;
using Alta.Serialization;
using HarmonyLib;
using Newtonsoft.Json;
using TavernLib.Backend.Api;
using TavernLib.Services;

namespace TavernLib.Patches;

[HarmonyPatch]
public static class PlayerJoinFilter
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
        }
    }

    private static async Task<bool> FilterInvalidTokens(Connection connection, Stream stream)
    {
        try
        {
            using var readingStream = stream.Clone() as Stream;
            var requestJoinMessage = new RequestJoinMessage();
            requestJoinMessage.Serialize(connection, readingStream);

            var claims = JWTUtility.CreateFromString(requestJoinMessage.UserCredentials, true).Claims.ToList();
            var tavernTokens = claims.Where(claim => claim.Type == "TavernToken").ToList();
            var ids = claims.Where(claim => claim.Type == "UserId").ToList();
            var names = claims.Where(claim => claim.Type == "Username").ToList();

            if (tavernTokens.Count == 1 && ids.Count == 1 && names.Count == 1 &&
                ulong.TryParse(ids[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                // Refused here rather than in CheckApproved, which logs the whole join token when it refuses.
                if (requestJoinMessage.PlayerId < 0 || (ulong)requestJoinMessage.PlayerId != id)
                {
                    TavernLogger.Warn($"Join denied for {names[0].Value}: the token is for user {id}, the join asked for {requestJoinMessage.PlayerId}");
                    await ServerPlayerConnectionHandlerOld.PlayerDenied(connection, "Token was for a different user");
                    return false;
                }

                var users = TavernServices.GetService<TavernManager>().UserConfig.Read().Users;
                if (users.TryGetValue(names[0].Value.ToLowerInvariant(), out var user) && user != null && user.UserId == id && !string.IsNullOrEmpty(user.Token) && user.Token == tavernTokens[0].Value) return true;
            }

            await ServerPlayerConnectionHandlerOld.PlayerDenied(connection, "Data mismatch or account not found");
            return false;
        }

        catch (Exception e)
        {
            TavernLogger.Error($"Error in FilterInvalidTokens! {e}");
            await ServerPlayerConnectionHandlerOld.PlayerDenied(connection, "Error when checking authenticity");
            return false;
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
                var token = JWTUtility.CreateFromString(requestJoinMessage.UserCredentials, true);
                
                var username = token.Claims.FirstOrDefault(c => c.Type == "Username")?.Value ?? "";
                var users = TavernServices.GetService<TavernManager>().UserConfig.Read().Users;

                if (users.TryGetValue(username.ToLowerInvariant(), out var user) && user != null && user.CanEnterFlyMode)
                {
                    TavernLogger.Msg($"User {username} allowed unorthodox role via role");
                    return true;
                }

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

    [HarmonyPatch(typeof(ConfirmJoinMessage), MethodType.Constructor, [typeof(bool), typeof(bool), typeof(ClientJoinResult), typeof(JoinedServerInfo), typeof(string)]), HarmonyPostfix]
    public static void LogDisconnectAttempts(bool isAllowed, string error)
    {
        if (!isAllowed) TavernLogger.Msg($"Join refused: {error}");
    }
}