using Alta.Api.Client.HighLevel;
using Alta.Api.DataTransferModels.Converters;
using Alta.Api.DataTransferModels.Models.Responses;
using Alta.Api.DataTransferModels.Utility;
using Alta.Customization;
using Alta.Networking;
using Alta.Networking.Scripts.Player;
using Alta.Networking.Servers;
using Alta.QuickAccessActions;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TavernLib.ModdingApi;

namespace TavernLib.Patches;

[HarmonyPatch]
public class TeenyPatches
{
    #region OfflineUserApiClient

    [HarmonyPatch(typeof(UserApiClient), nameof(UserApiClient.GetAllCosmeticsPresets)), HarmonyPrefix]
    public static bool LocalGetAllCosmeticPresets(ref Task<IEnumerable<UserPresetDataInfo>> __result)
    {
        List<UserPresetDataInfo> list = new List<UserPresetDataInfo>();
        try
        {
            string folder = Path.Combine(TavernDirectories.ATTSave, "presets");
            if (Directory.Exists(folder))
            {
                foreach (string file in Directory.GetFiles(folder, "*.preset"))
                {
                    try
                    {
                        int presetId = int.Parse(Path.GetFileNameWithoutExtension(file));
                        byte[] bytes = File.ReadAllBytes(file);
                        uint[] data = new uint[(bytes.Length + 3) / 4];
                        Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
                        list.Add(new UserPresetDataInfo { PresetId = presetId, ByteSize = bytes.Length, Data = data });
                    }
                    catch (Exception) { }
                }
            }
        }
        catch (Exception) { }
        __result = Task.FromResult<IEnumerable<UserPresetDataInfo>>(list);
        return false;
    }


    [HarmonyPatch(typeof(UserApiClient), nameof(UserApiClient.CreateCosmeticsPreset)), HarmonyPrefix]
    public static bool LocalCreateCosmeticPreset(int presetId, uint[] data, int byteSize, ref Task<UserPresetDataInfo> __result)
    {
        try
        {
            string folder = Path.Combine(TavernDirectories.ATTSave, "presets");
            Directory.CreateDirectory(folder);
            byte[] bytes = new byte[byteSize];
            Buffer.BlockCopy(data, 0, bytes, 0, byteSize);
            File.WriteAllBytes(Path.Combine(folder, presetId + ".preset"), bytes);
        }
        catch (Exception) { }

        __result = Task.FromResult(new UserPresetDataInfo { PresetId = presetId, ByteSize = byteSize, Data = data });
        return false;
    }


    [HarmonyPatch(typeof(UserApiClient), nameof(UserApiClient.DeleteCosmeticPreset)), HarmonyPrefix]
    public static bool LocalDeleteCosmeticPreset(int presetId, ref Task __result)
    {
        try
        {
            string file = Path.Combine(TavernDirectories.ATTSave, "presets", presetId + ".preset");
            if (File.Exists(file)) File.Delete(file);
        }
        catch (Exception) { }

        __result = Task.CompletedTask;
        return false;
    }

    #endregion

    #region PurchasedList

    [HarmonyPatch(typeof(PurchasedList), nameof(PurchasedList.UpdateCurrentRotation))]
    public static bool LocalDeleteCosmeticPreset(ref Task __result)
    {
        PurchasedList.CurrentOnStore.Clear();
        PurchasedList.CurrentRotationToSets.Clear();
        __result = Task.CompletedTask;
        return false;
    }

    [HarmonyPatch(typeof(PurchasedList), nameof(PurchasedList.ValidatePlayersOwnedItems)), HarmonyPrefix]
    public static bool AlwaysAllowAnyItem(ref Task<bool> __result)
    {
        __result = Task.FromResult(true);
        return false;
    }

    [HarmonyPatch(typeof(PurchasedList), nameof(PurchasedList.Refresh)), HarmonyPrefix]
    public static bool DisallowPaidCosmetics(ref Task __result, PurchasedList __instance)
    {
        PurchasedList.Initialize();
        PurchasedList.owned.Clear();
        PurchasedList.owned = PurchasedList.owned.Concat(PurchasedList.OwnedByAll).ToHashSet();
        PurchasedList.available.Clear();
        PurchasedList.available = PurchasedList.available.Concat(PurchasedList.OwnedByAll).ToHashSet();

        __result = Task.CompletedTask;
        return false;
    }

    #endregion

    #region ReturnToMainMenuAction

    [HarmonyPatch(typeof(ReturnToMainMenuAction), nameof(ReturnToMainMenuAction.LetGoValid)), HarmonyPrefix]
    public static bool QuitOnMenuReturn(ReturnToMainMenuAction __instance)
    {
        __instance.ClearOrb();
        _ = GameModeManager.StopCurrentModeAsync("return to menu", isReturningToMenu: false);
        ApplicationManager.ExternalOnApplicationQuit(new ShutdownReason("Player exited", isExpected: true));
        return false;
    }

    #endregion

    #region ApplicationStartupManager

    [HarmonyPatch(typeof(ApplicationStartupManager), nameof(ApplicationStartupManager.RunStartupActions)), HarmonyPrefix]
    public static bool LocalJoinArg()
    {
        if (!CommandLineArguments.Contains(TavernArgs.JoinLocalServer)) return true;

        string ip = CommandLineArguments.TryGetNextArguments(TavernArgs.DevServerIp, 1, out var ipArgs)
            ? ipArgs[0]
            : IPAddress.Loopback.ToString();

        int port = CommandLineArguments.TryGetNextArguments(TavernArgs.DevServerPort, 1, out var portArgs) && int.TryParse(portArgs[0], out var parsedPort)
            ? parsedPort
            : 1757;

        _ = GameModeManager.JoinServer(DevGameServerInfo.GetDevServer(ip, port, 0));
        return false;
    }

    #endregion

    #region ServerPlayerConnectionHandlerOld

    [HarmonyPatch(typeof(ServerPlayerConnectionHandlerOld), nameof(ServerPlayerConnectionHandlerOld.CheckIfPlayerIsAllowedCustom)), HarmonyPrefix]
    public static bool CheckJoinToken(string tokenString, int playerId, ref Task<ServerPlayerConnectionHandlerOld.PlayerJoinResult> __result)
    {
        __result = Task.FromResult(JoinResult(tokenString, playerId));
        return false;
    }

    private static ServerPlayerConnectionHandlerOld.PlayerJoinResult JoinResult(string tokenString, int playerId)
    {
        try
        {
            var claims = JWTUtility.CreateFromString(tokenString).Claims.ToList();
            var ids = claims.Where(c => c.Type == "UserId").ToList();
            var names = claims.Where(c => c.Type == "Username").ToList();

            if (ids.Count != 1 || names.Count != 1)
                return ServerPlayerConnectionHandlerOld.PlayerJoinResult.CreateDeniedResult("Error reading token");

            if (!int.TryParse(ids[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var tokenId) || tokenId != playerId)
            {
                TavernLogger.Warn($"Join denied: the token is for user {ids[0].Value}, the join asked for {playerId}");
                return ServerPlayerConnectionHandlerOld.PlayerJoinResult.CreateDeniedResult("Token was for a different user");
            }

            var username = names[0].Value;
            var user = new UserInfoAndRole(new UserInfo(playerId, username), MemberStatusFromRoles(username));
            return ServerPlayerConnectionHandlerOld.PlayerJoinResult.CreateSuccessResult(user);
        }
        catch (Exception)
        {
            return ServerPlayerConnectionHandlerOld.PlayerJoinResult.CreateDeniedResult("Error reading token");
        }
    }

    // The client writes its own join token, so its Policy claims prove nothing. The game's
    // statuses come from users.json roles named like the claims the game reads.
    internal static PlayerSubscriberStatus MemberStatusFromRoles(string username)
    {
        var roles = Roles.GetRoles(username);
        bool Has(string role) => roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));

        if (Has("dev")) return PlayerSubscriberStatus.Developer;
        if (Has("mod")) return PlayerSubscriberStatus.AltaMod;
        if (Has("supporter")) return PlayerSubscriberStatus.Subscriber;
        return PlayerSubscriberStatus.None;
    }

    #endregion

    #region Player

    [HarmonyPatch(typeof(Player), nameof(Player.SyncCosmetics)), HarmonyPrefix]
    public static bool LocalDeleteCosmeticPreset(IPlayer player, Alta.Serialization.Stream stream, Player __instance)
    {
        TavernLogger.Msg("SyncCosmetics patch!");
        try
        {
            if (stream.IsReadingOnServerNonLocalTest() && !ReferenceEquals(__instance, player))
            {
                Player.logger.Error("[Player] Received message to alter cosmetics from {0} to {1}", player, __instance);
                StreamAuthorityHelper.LogUnauthorizedMessage(player);
                return false;
            }
            if (NetworkSceneManager.IsServer && stream.IsWriting)
            {
                if (__instance.saveData != null && __instance.saveData.CosmeticBytes > 4)
                {
                    Alta.Serialization.StreamWriter writer = stream as Alta.Serialization.StreamWriter;
                    if (writer != null)
                    {
                        Alta.Serialization.StreamReader reader = new Alta.Serialization.StreamReader(__instance.saveData.CosmeticData, __instance.saveData.CosmeticBytes);
                        int bitCount = __instance.saveData.CosmeticBytes * 8;
                        for (int b = 0; b < bitCount; b += 32)
                        {
                            int remaining = Math.Min(32, bitCount - b);
                            uint chunk = 0U;
                            reader.SerializeBits(ref chunk, remaining);
                            writer.SerializeBits(ref chunk, remaining);
                        }
                        return false;
                    }
                }
                _ = CustomizationWrapperSerializer.SerializeStreamAsync(__instance.Customization.Cosmetics, stream, false, CancellationToken.None);
                return false;
            }
            _ = CustomizationWrapperSerializer.SerializeStreamAsync(__instance.Customization.Cosmetics, stream, !NetworkSceneManager.IsServer, CancellationToken.None);
        }
        catch (Exception exception)
        {
            Player.logger.Error(exception, "[Player] Error syncing customization");
        }
        TavernLogger.Msg("SyncCosmetics patch done!");
        return false;
    }

    #endregion

    #region GameServerInfo
    [HarmonyPatch(typeof(MenuSettings), nameof(MenuSettings.LocalGameServerInfo)), HarmonyPostfix]
    public static void LocalGameServerInfo_Postfix(int sceneIndex, ref GameServerInfo __result)
    {
        if (__result is not DevGameServerInfo devInfo) return;

        if (SceneIndexPatches.ForcedSceneIndex() is int index)
        {
            __result.SceneIndex = index;
            if (index == 4) __result.Target |= (int)PlatformTarget.Quest;
        }

        int port = CommandLineArguments.TryGetNextArguments(TavernArgs.DevServerPort, 1, out var portArgs) && int.TryParse(portArgs[0], out var parsedPort)
            ? parsedPort
            : 1757;

        devInfo.ConnectionInfo = new ConnectionInfo
        {
            Address = IPAddress.Loopback,
            GamePort = port
        };

        __result = devInfo;
    }
    #endregion

    #region ApiAccess
    [HarmonyPatch(typeof(ApiAccess), nameof(ApiAccess.IsConnectedToInternetInternal)), HarmonyPostfix]
    public static void IsConnectedToInternetInternal_Postfix(ref bool __result)
    {
        __result = true;
    }
    #endregion
}
