using Alta.QuickAccessActions;
using HarmonyLib;
using TavernLib.Backend.Api;
using TavernLib.Services;

namespace TavernLib.Patches;

[HarmonyPatch]
public class ServerSettingsRolePatch
{
    [HarmonyPatch(typeof(SettingsQuickAccessMenu), nameof(SettingsQuickAccessMenu.InitializeForPlayer)), HarmonyPostfix]
    public static void ValidateUserPermissions(Player player, bool isDev, SettingsQuickAccessMenu __instance)
    {
        __instance.hasPermission = TavernServices.GetService<TavernApiManager>().UserConfig.GetUser(player.UserInfo.Username).IsModerator;
    }
}