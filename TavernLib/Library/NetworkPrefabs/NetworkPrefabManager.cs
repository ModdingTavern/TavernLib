using Alta.Networking;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TavernLib.Library.NetworkPrefabs {
    internal static class NetworkPrefabManager {
        [DllImport("user32.dll")]
        internal static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        internal static extern int MessageBox(IntPtr hwnd, string text, string caption, uint type);

        public static MessageType JsonSync = (MessageType)18;

        private static int[] _existingHashIDs;
        public static int[] ExistingHashIDs => _existingHashIDs;

        public static void ReadHashIDsFile() {
            using (System.IO.Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TavernLib.Library.NetworkPrefabs.ExistingHashIDs.txt")) {

                using (System.IO.StreamReader reader = new System.IO.StreamReader(stream)) {

                    string text = reader.ReadToEnd();

                    string[] list = text
                        .Replace("\r", "")
                        .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    _existingHashIDs = new int[list.Length];

                    for (int i = 0; i < list.Length; i++) {
                        if (int.TryParse(list[i], out int value))
                            _existingHashIDs[i] = value;

                        else
                            Tavern.Logger.Error($"Failed to parse '{list[i]}' from existing hash IDs.");
                    }
                }
            }
        }

        internal static void JsonSerialize(Connection connection, Alta.Serialization.Stream stream) {
            string json = NetworkPrefabRegistry.clientSerializableJsonData;

            if (NetworkSceneManager.IsServer) {
                Tavern.Logger.Msg($"Sending Json data to player.");
            }

            stream.SerializeString(ref json);
            if (stream.IsReading && !NetworkSceneManager.IsServer) {
                Tavern.Logger.Msg($"Received custom item json data from server!");

                if (string.IsNullOrWhiteSpace(json)) {
                    Tavern.Logger.Error($"An error occured while getting Json data from the server. Please try joining again.");

                    MessageBox(GetActiveWindow(), $"An error occured while getting custom data from the server. Please try joining again.", "TavernLib - Server Error, (A Township Tale)", 0);

                    Application.Quit();
                }

                NetworkPrefabRegistry.recievedSyncData = true;

                NetworkPrefabRegistry.jsonData = json;
            }
        }
    }
}