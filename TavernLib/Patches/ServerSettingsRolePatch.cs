using Alta.Networking;
using Alta.QuickAccessActions;
using HarmonyLib;
using TavernLib.Backend;
using TavernLib.Backend.Api;
using TavernLib.Services;

namespace TavernLib.Patches;

[HarmonyPatch]
public class ServerSettingsRolePatch
{
    [HarmonyPatch(typeof(SettingsQuickAccessMenu), nameof(SettingsQuickAccessMenu.InitializeForPlayer)), HarmonyPostfix]
    public static void ValidateUserPermissions(Player player, bool isDev, SettingsQuickAccessMenu __instance)
    {
        // TODO: Is most of this even necessary?
        if (NetworkSceneManager.IsServer)
        {
            // Allow a user to sync server settings if they're a moderator
            __instance.hasPermission = TavernServices.GetService<TavernManager>().UserConfig.GetUser(player.UserInfo.Username).IsModerator;
        }

        else
        {
            __instance.hasPermission = TavernServices.GetService<EntranceMessageHandler>().LocalUser.IsModerator;
        }
    }
    
    // Send a user their roles, used to toggle the quick access server board, respective of their roles
    [HarmonyPatch(typeof(Player), nameof(Player.InitializePlayerOnServer)), HarmonyPostfix]
    public static void SendRolesToUser(Player __instance)
    {
        var roles = TavernServices.GetService<TavernManager>().UserConfig.GetUser(__instance.UserInfo.Username).Roles;
        
        __instance.ConnectionToRemotePlayer.Send(null, (MessageType)TavernMessages.ReceiveRoles, (_, stream) =>
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