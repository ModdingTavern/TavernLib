using System;
using System.Collections;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Alta.Api.Client.HighLevel;
using Alta.Api.DataTransferModels.Converters;
using Alta.Api.DataTransferModels.Models.Responses;
using Alta.Api.DataTransferModels.Utility;
using Alta.Customization;
using Alta.Global;
using Alta.Networking;
using Alta.Networking.Scripts.Player;
using Alta.Networking.Servers;
using Alta.QuickAccessActions;
using HarmonyLib;
using MelonLoader.Logging;

namespace TavernLib.Patches
{
    [HarmonyPatch]
    public class TeenyPatches
    {
        #region OfflineUserApiClient

        [HarmonyPatch(typeof(OfflineUserApiClient), nameof(OfflineUserApiClient.GetAllCosmeticsPresets)), HarmonyPrefix]
        public static bool LocalGetAllCosmeticPresets(ref Task<IEnumerable<UserPresetDataInfo>> __result)
        {
            var allPresets = new List<UserPresetDataInfo>();
            try
            {
                var presetsDir = Path.Combine(TavernDirectories.ATTSave, "Presets");
                if (Directory.Exists(presetsDir))
                {
                    var files = Directory.GetFiles(presetsDir, "*.preset");
                    foreach (string preset in files)
                    {
                        try
                        {
                            var presetId = int.Parse(Path.GetFileNameWithoutExtension(preset));
                            var presetData = File.ReadAllBytes(preset);
                            var array2 = new uint[(presetData.Length + 3) / 4];
                            Buffer.BlockCopy(presetData, 0, array2, 0, presetData.Length);
                            allPresets.Add(new UserPresetDataInfo
                            {
                                PresetId = presetId,
                                ByteSize = presetData.Length,
                                Data = array2
                            });
                        }
                        catch (Exception exception)
                        {
                            Tavern.Logger.Warning($"Failed to read preset with name {preset}! {exception}");
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Tavern.Logger.Error($"Failed to fetch presets! {exception}");
                __result = Task.FromResult(Array.Empty<UserPresetDataInfo>().AsEnumerable());
            }

            __result = Task.FromResult((IEnumerable<UserPresetDataInfo>)allPresets);

            return false;
        }


        [HarmonyPatch(typeof(OfflineUserApiClient), nameof(OfflineUserApiClient.CreateCosmeticsPreset)), HarmonyPrefix]
        public static bool LocalCreateCosmeticPreset(int presetId, uint[] data, int byteSize, ref Task<UserPresetDataInfo> __result)
        {
            try
            {
                string text = Path.Combine(TavernDirectories.ATTSave, "Presets");
                Directory.CreateDirectory(text);
                byte[] array = new byte[byteSize];
                Buffer.BlockCopy(data, 0, array, 0, byteSize);
                File.WriteAllBytes(Path.Combine(text, presetId + ".preset"), array);
            }
            catch (Exception exception)
            {
                Tavern.Logger.Error($"Failed to create preset with ID {presetId}! {exception}");
            }

            __result = Task.FromResult(new UserPresetDataInfo
            {
                PresetId = presetId,
                ByteSize = byteSize,
                Data = data
            });

            return false;
        }


        [HarmonyPatch(typeof(OfflineUserApiClient), nameof(OfflineUserApiClient.CreateCosmeticsPreset)), HarmonyPrefix]
        public static bool LocalDeleteCosmeticPreset(int presetId, ref Task __result)
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "A Township Tale", "presets", presetId + ".preset");
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                Tavern.Logger.Error($"Failed to delet preset with ID {presetId}! {exception}");
            }

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
        }

        #endregion

        #region ServerConsoleManager

        [HarmonyPatch(typeof(ServerConsoleManager), nameof(ServerConsoleManager.ValidateConsoleToken)), HarmonyPrefix]
        public static bool ValidateConsoleToken(JwtSecurityToken token, ref Task<bool> __result)
        {
            __result = Task.FromResult(token.Claims.FirstOrDefault((Claim c) => c.Type == "Policy" && c.Value == "server_owner") != null);
            return false;
        }

        #endregion

        #region ServerPlayerConnectionHandlerOld

        [HarmonyPatch(typeof(ServerPlayerConnectionHandlerOld), nameof(ServerPlayerConnectionHandlerOld.CheckIfPlayerIsAllowedCustom)), HarmonyPrefix]
        public static bool ValidateConsoleToken(string tokenString, int playerId, ref Task<ServerPlayerConnectionHandlerOld.PlayerJoinResult> __result)
        {
            try
            {
                var jwtToken = JWTUtility.CreateFromString(tokenString);
                var value = jwtToken.Claims.First(c => c.Type == "Username").Value;
                
                if (int.Parse(jwtToken.Claims.First(c => c.Type == "UserId").Value) != playerId)
                {
                    __result = Task.FromResult(ServerPlayerConnectionHandlerOld.PlayerJoinResult.CreateDeniedResult("Token was for a different user"));
                }

                var result = new UserInfoAndRole(new UserInfo(playerId, value), UserRolesUtility.GetRolesFromIdentityToken(tokenString));
                __result = Task.FromResult(ServerPlayerConnectionHandlerOld.PlayerJoinResult.CreateSuccessResult(result));
            }
            catch (Exception)
            {
                __result = Task.FromResult(ServerPlayerConnectionHandlerOld.PlayerJoinResult.CreateDeniedResult("Error reading token"));
            }

            return false;
        }

        #endregion

        #region Player

        [HarmonyPatch(typeof(Player), nameof(Player.SyncCosmetics)), HarmonyPrefix]
        public static bool LocalDeleteCosmeticPreset(IPlayer player, Alta.Serialization.Stream stream, Player __instance)
        {
            Tavern.Logger.Msg(ColorARGB.Azure, "SyncCosmetics patch!");
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
            Tavern.Logger.Msg(ColorARGB.Azure, "SyncCosmetics patch done!");
            return false;
        }

        #endregion
    }
}