using System.Collections.Generic;
using Alta.Networking;
using HarmonyLib;
using TavernLib.Backend;
using TavernLib.Backend.Api;
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