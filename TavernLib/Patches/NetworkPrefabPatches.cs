using Alta.Networking;
using Alta.Networking.Servers;
using HarmonyLib;
using System;
using TavernLib.Library.NetworkPrefabs;

namespace TavernLib.Patches {
    internal static class NetworkPrefabPatches {
        private static MessageType jsonSync = NetworkPrefabManager.JsonSync;
        private static SerializeConnectionMethod handler = NetworkPrefabManager.JsonSerialize;

        [HarmonyPatch(typeof(Socket), "CreateConnection", new Type[] { typeof(string), typeof(int) })]
        internal static class ISocketPatch {
            private static void Postfix(ref Connection __result) {
                Tavern.Logger.Msg("Connection created to server, waiting for HashId json data...");

                __result.SetHandler(jsonSync, handler);
            }
        }

        [HarmonyPatch(typeof(PrefabManager), "PrepareSpawnSetups")]
        internal static class OrefabManagerPatch {
            private static void Postfix() {
                if (!NetworkSceneManager.IsServer) {
                    NetworkPrefabRegistry.RegisterIntoGame();
                }
            }
        }

        [HarmonyPatch(typeof(NetworkScene), "InitializeAsServer")]
        internal static class NetworkScenePatch {
            private static void Postfix() {
                PrefabManager.PrepareSpawnSetups();
                NetworkPrefabRegistry.RegisterIntoGame();
            }
        }

        [HarmonyPatch(typeof(ServerHandler), "ConnectionCreated", new Type[] { typeof(Connection) })]
        internal static class ServerHandlerPatch {
            private static void Postfix(Connection connection) {
                Tavern.Logger.Msg("Player is connecting, sending json data.");

                connection.SetHandler(jsonSync, handler);

                bool result = connection.Send(null, jsonSync, handler);

                if (!result) {
                    Tavern.Logger.Error("Failed to send json data to client.");
                }
            }
        }
    }
}
