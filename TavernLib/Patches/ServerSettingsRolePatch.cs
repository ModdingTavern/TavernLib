using System.Collections.Generic;
using System.Threading.Tasks;
using Alta.Networking;
using Alta.Networking.Scripts.Player;
using ATT.Character.QuickAccessMenu;
using HarmonyLib;
using TavernLib.Backend;
using TavernLib.Backend.Api;
using TavernLib.ModdingApi;
using TavernLib.Services;

namespace TavernLib.Patches;

[HarmonyPatch]
public class ServerSettingsRolePatch
{
    // Send a user their roles, used to toggle the quick access server board, respective of their roles
    [HarmonyPatch(typeof(Player), nameof(Player.InitializePlayerOnServer)), HarmonyPostfix]
    public static void SendRolesToUser(Player __instance)
    {
        var user = TavernServices.GetService<TavernManager>().UserConfig.GetUser(__instance.UserInfo.Username);
        var roles = user?.Roles ?? new List<string>();
        
        SendRoles(__instance.ConnectionToRemotePlayer, roles);
    }

    // The game lets any player's settings board change the server's settings: IsServerSyncPermitted
    // is always true. Only owners and moderators may, the same roles that are shown the board.
    [HarmonyPatch(typeof(ServerSettingsSync), nameof(ServerSettingsSync.IsServerSyncPermitted)), HarmonyPostfix]
    public static void OnlyModeratorsChangeSettings(IPlayer player, ref Task<bool> __result)
    {
        var connection = (player as Player)?.ConnectionToRemotePlayer;
        var allowed = Roles.HasRole(connection, "owner") || Roles.HasRole(connection, "moderator");
        if (!allowed) TavernLogger.Warn($"Server settings change from {connection?.UserInfo?.Username} refused: not an owner or moderator");

        __result = Task.FromResult(allowed);
    }

    internal static void SendRoles(Connection connection, IReadOnlyList<string> roles)
    {
        connection.Send(null, (MessageType)TavernMessages.ReceiveRoles, (_, stream) =>
        {
            var roleCount = roles.Count;
            stream.SerializeInteger(ref roleCount);

            if (roleCount < 1) return;
            for (var i = 0; i < roleCount; i++)
            {
                var roleAtIndex = roles[i];
                stream.SerializeString(ref roleAtIndex);
            }
        });
    }
}