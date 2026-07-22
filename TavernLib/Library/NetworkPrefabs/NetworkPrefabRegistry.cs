using Alta;
using Alta.Inventory;
using Alta.Loot;
using Alta.Networking;
using Alta.Networking.Internal;
using Alta.Pages;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static TavernLib.Library.NetworkPrefabs.Enums;

namespace TavernLib.Library.NetworkPrefabs {
    public class ItemSettings {
        public string Description;

        public Enums.PhysicalMaterial PhysicalMaterial;

        internal PhysicalMaterial _physicalMaterial;

        public Glyph glyph;

        /// <summary>
        /// When scaled in dock
        /// </summary>
        public Vector3 StoreScale = new Vector3(0.5f, 0.5f, 0.5f);

        /// <summary>
        /// When scaled in dock
        /// </summary>
        public Item.DockPositioning scaledPositioning = new Item.DockPositioning(Vector3.zero, Vector3.zero);

        /// <summary>
        /// When not scaled in a dock
        /// </summary>
        public Item.DockPositioning normalPositioning = new Item.DockPositioning(Vector3.zero, Vector3.zero);

        /// <summary>
        /// a null value for PickupDockSettings will act as a default if no specific setting is found
        /// </summary>
        public Alta.Inventory.Item.CustomDockPositioning[] customPositions = new Item.CustomDockPositioning[0];

        /// <summary>
        /// a null value for PickupDockSettings will act as a default if no specific setting is found
        /// </summary>
        public Alta.Inventory.Item.CustomDockPositioning[] customPositionsWhenCrafted = new Item.CustomDockPositioning[0];

        public bool isStackable;

        public bool isStackableWhenCrafted;

        public bool destroyWhenDocked;

        public int size;

        public int dockedStackSize = 30;

        public float weight = 1f;

        public Enums.LootValue LootValue = Enums.LootValue.Common;

        public LootCategory LootCategory = LootCategory.NoneOrCustom;

        public Enums.PickupTag[] PickupTags = new Enums.PickupTag[] { Enums.PickupTag.All };

        /// <summary>
        /// Leave as None to use default sound clip
        /// </summary>
        public SoundEffect overridePickUpSoundEffectType = SoundEffect.None;

        /// <summary>
        /// Leave as None to use default sound clip
        /// </summary>
        public SoundEffect overrideLetGoSoundEffectType = SoundEffect.None;

        public bool isUsingShrunkenVisual = true;

        public PooledObjectDefinition shrunkenVisual;

        public bool isOverridingShrunkenScale;

        public float overrideShrunkenScale = 1f;

        public bool isOverridingDefaultVisualScale;

        public float overrideScaleInsideVisual = 1f;

        public bool isSnappingToHand;

        public bool isAssistGrabBlocked;

        public bool wrapHandForPickup = true;

        public RotationMode PickupGrabPointRotationMode = RotationMode.Free;
    }

    public class HashRegistryFile {
        public int FormatVersion = 1;

        public int NextHashId = 1;

        public List<ModHashRegistry> Mods = new();
    }

    public class ModHashRegistry {
        public string ModId { get; set; }

        public string LastKnownVersion { get; set; }

        public bool Installed { get; set; }

        public Dictionary<string, int> ItemHashes { get; set; } = new();

        public ModHashRegistry(string modId, string version) {
            ModId = modId;
            LastKnownVersion = version;
            Installed = true;
        }
    }

    /// <summary>
    /// A class designed to assist in easily registering prefabs as NetworkPrefabs in Alta's systems for later use.
    /// </summary>

    // This class is a bit of a mess...
    public static class NetworkPrefabRegistry {
        public static List<NetworkPrefab> RegisteredCustomPrefabs = new List<NetworkPrefab>();

        public static Dictionary<Item, ItemSettings> RegisteredCustomItems = new Dictionary<Item, ItemSettings>();

        internal static HashRegistryFile Registry { get; set; }

        internal static string clientSerializableJsonData { get; set; }

        //internal static Dictionary<string, Dictionary<string, int>> HashIds { get; set; }

        internal static string jsonData = null;

        public static EntityManager entityManager = null;

        private static bool inRegistryProcess;

        /// <summary>
        /// Called when custom prefabs are being registered.
        /// </summary>
        public static Action RegisterPrefabs;

        internal static bool recievedSyncData = false;

        private static ModHashRegistry GetOrCreateNewModHashIds(this MelonMod mod) {
            string id = $"{mod.Info.Name}.{mod.Info.Author}";

            var registry = Registry.Mods.FirstOrDefault(x => x.ModId == id);

            if (registry == null) {
                registry = new ModHashRegistry(
                    id,
                    mod.Info.Version
                );

                Registry.Mods.Add(registry);
            }

            registry.LastKnownVersion = mod.Info.Version;
            registry.Installed = true;

            return registry;
        }

        internal static void RefreshInstalledMods() {
            foreach (var mod in Registry.Mods)
                mod.Installed = false;

            foreach (var melon in MelonMod.RegisteredMelons) {
                string id = $"{melon.Info.Name}.{melon.Info.Author}";

                var registry =
                    Registry.Mods.FirstOrDefault(x => x.ModId == id);

                if (registry != null) {
                    registry.Installed = true;
                    registry.LastKnownVersion = melon.Info.Version;
                }
            }
        }

        internal static void ReadJsonFile() {
            if (!NetworkSceneManager.IsServer) {
                if (string.IsNullOrWhiteSpace(jsonData)) {
                    Tavern.Logger.Error("Client has no custom item registry.");

                    return;
                }

                Registry = JsonConvert.DeserializeObject<HashRegistryFile>(jsonData);

                return;
            }

            string path = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "..",
                    "modHashIDs.json"
                )
            );

            if (!File.Exists(path)) {
                Registry = new HashRegistryFile();
                return;
            }

            string json = File.ReadAllText(path);

            Registry = JsonConvert.DeserializeObject<HashRegistryFile>(json);
        }

        internal static bool hasRegistered = false;

        private static HashSet<int> takenHashIds;

        internal static void RegisterIntoGame() {
            if (hasRegistered)
                return;

            if (!NetworkSceneManager.IsServer && !recievedSyncData)
                return;

            Tavern.Logger.Msg("Registering custom prefabs...");

            ReadJsonFile();

            takenHashIds = new HashSet<int>(NetworkPrefabManager.ExistingHashIDs);

            foreach (var mod in Registry.Mods) {
                foreach (var hash in mod.ItemHashes.Values) {
                    takenHashIds.Add(hash);
                }
            }

            entityManager = Traverse.Create(NetworkSceneManager.Current).Field("entityManager").GetValue<EntityManager>();

            HashedGeneralValue<Item>.CheckItems();

            inRegistryProcess = true;

            RegisterPrefabs?.Invoke();

            inRegistryProcess = false;

            if (NetworkSceneManager.IsServer) {
                string modPrefabFilePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "modHashIDs.json"));

                string json = JsonConvert.SerializeObject(Registry, Formatting.Indented);

                jsonData = json;

                File.WriteAllText(modPrefabFilePath, json);

                var clientData = new HashRegistryFile {
                    FormatVersion = Registry.FormatVersion,
                    Mods = Registry.Mods.Where(x => x.Installed).ToList()
                };

                string clientJson = JsonConvert.SerializeObject(clientData, Formatting.Indented);

                clientSerializableJsonData = clientJson;
            }

            hasRegistered = true;
        }

        internal static int GetNextAvailableHashId() {
            while (takenHashIds.Contains(Registry.NextHashId))
                Registry.NextHashId++;

            int id = Registry.NextHashId++;

            takenHashIds.Add(id);

            return id;
        }

        private static void ExecuteAllGetComponentAtts(params Component[] comps) {
            foreach (var item in comps) {
                if (item is null)
                    continue;

                Type type = item.GetType();

                List<FieldInfo> allFields = new List<FieldInfo>();

                while (type != null) {
                    FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                    allFields.AddRange(fields);

                    type = type.BaseType;
                }

                foreach (FieldInfo field in allFields) {
                    PrefabApplyAttribute att = field.GetCustomAttribute<PrefabApplyAttribute>();

                    if (att is null)
                        continue;

                    att.HandleApply(item, field, item.gameObject);
                }
            }
        }

        private static void MessageError(MelonMod mod, string message, bool doMessageBox = true) {
            string from = mod != null ? $"[FROM: '{mod.Info.Name}']" : "[FROM: 'Unknown']";

            Tavern.Logger.Error($"{from} {message}");

            if (doMessageBox) {
                string extraMessage = NetworkSceneManager.IsServer ?
                    "The server will be closed to prevent data corruption." :
                    "A Township Tale will be closed to prevent any further errors.";

                NetworkPrefabManager.MessageBox(
                    NetworkPrefabManager.GetActiveWindow(),
                    $"{from} {message}\n\n{extraMessage}",
                    "TavernLib - Error Occurred, (A Township Tale)",
                    0
                );

                Application.Quit();
            }
        }

        private static void SetValue(this Item item, Traverse traverse, string field, object value) {
            traverse.Field(field).SetValue(value);
        }

        public static Item CreateNewItem(string itemName, ItemSettings settings) {
            Item item = ScriptableObject.CreateInstance<Item>();

            item.name = itemName;

            Traverse traverse = Traverse.Create(item);

            item.SetValue(traverse, "Description", settings.Description);

            // Set physical material
            if (settings.PhysicalMaterial != Enums.PhysicalMaterial.NoneOrCustom) {
                string PhysicalMaterialName = settings.PhysicalMaterial.ToString().Replace('_', ' ');

                PhysicalMaterial physicalMaterial = Resources.FindObjectsOfTypeAll<PhysicalMaterial>()
                    .FirstOrDefault(c => c.name == PhysicalMaterialName);

                settings._physicalMaterial = physicalMaterial;
            }

            // Set Loot Category
            if (settings.LootCategory != LootCategory.NoneOrCustom) {
                string LootCategory = settings.LootCategory.ToString().Replace('_', ' ');

                Category category = Resources.FindObjectsOfTypeAll<Category>()
                    .FirstOrDefault(c => c.name == LootCategory);

                item.SetValue(traverse, "lootCategory", category);
            }

            // Set Loot Value
            if (settings.LootValue != Enums.LootValue.NoneOrCustom) {
                string LootValue = settings.LootValue.ToString();

                LootValue lootValue = Resources.FindObjectsOfTypeAll<LootValue>()
                    .FirstOrDefault(c => c.name.Contains(LootValue));

                item.SetValue(traverse, "lootValue", lootValue);
            }

            // Set Pickup Tags
            if (settings.PickupTags != null && settings.PickupTags.Length > 0) {
                List<Alta.Inventory.PickupTag> pickupTags = new List<Alta.Inventory.PickupTag>();

                foreach (var tagEnum in settings.PickupTags) {
                    string tag = tagEnum.ToString().Replace('_', ' ');

                    Alta.Inventory.PickupTag pickupTag = Resources.FindObjectsOfTypeAll<Alta.Inventory.PickupTag>()
                        .FirstOrDefault(c => c.name == tag);

                    pickupTags.Add(pickupTag);
                }

                if (pickupTags != null && pickupTags.Count > 0)
                    item.SetValue(traverse, "tags", pickupTags);
            }

            item.SetValue(traverse, "glyph", settings.glyph);
            item.SetValue(traverse, "storeScale", settings.StoreScale);
            item.SetValue(traverse, "scaledPositioning", settings.scaledPositioning);
            item.SetValue(traverse, "normalPositioning", settings.normalPositioning);

            item.SetValue(traverse, "customPositions", settings.customPositions);
            item.SetValue(traverse, "customPositionsWhenCrafted", settings.customPositionsWhenCrafted);

            item.SetValue(traverse, "isStackable", settings.isStackable);
            item.SetValue(traverse, "isStackableWhenCrafted", settings.isStackableWhenCrafted);
            item.SetValue(traverse, "size", settings.size);
            item.SetValue(traverse, "dockedStackSize", settings.dockedStackSize);
            item.SetValue(traverse, "weight", settings.weight);
            item.SetValue(traverse, "isUsingShrunkenVisual", settings.isUsingShrunkenVisual);

            if (settings.shrunkenVisual != null)
                item.SetValue(traverse, "shrunkenVisual", settings.shrunkenVisual);

            item.SetValue(traverse, "isOverridingDefaultShrunkenScale", settings.isOverridingDefaultVisualScale);
            item.SetValue(traverse, "overrideShrunkenScale", settings.overrideShrunkenScale);
            item.SetValue(traverse, "isOverridingDefaultVisualScale", settings.isOverridingDefaultVisualScale);
            item.SetValue(traverse, "overrideScaleInsideVisual", settings.overrideScaleInsideVisual);
            item.SetValue(traverse, "isSnappingToHand", settings.isSnappingToHand);
            item.SetValue(traverse, "isAssistGrabBlocked", settings.isAssistGrabBlocked);

            RegisteredCustomItems.Add(item, settings);

            return item;
        }

        private static void ApplyPickupGrabPoints(this Pickup pickup) {
            Transform transform = pickup.transform;

            Transform grabPointsParent = null;

            foreach (Transform child in transform) {
                if (child.name is "GrabPoints") {
                    grabPointsParent = child;

                    break;
                }
            }

            if (grabPointsParent is null || grabPointsParent.childCount <= 0) {
                GrabPoint[] defaultGrabPoints = new GrabPoint[1];

                defaultGrabPoints[0] = new GrabPoint();

                pickup.grabPoints = defaultGrabPoints;

                return;
            }

            GrabPoint[] grabPoints = new GrabPoint[grabPointsParent.childCount];

            ItemSettings settings = RegisteredCustomItems[pickup.item];

            for (int i = 0; i < grabPointsParent.childCount; i++) {
                Transform child = grabPointsParent.GetChild(i);

                if (!child.gameObject.activeSelf)
                    continue;

                GrabPoint grabPoint = new GrabPoint();

                grabPoint.position = child.localPosition;
                grabPoint.rotationEuler = child.localEulerAngles;

                if (settings != null) {
                    grabPoint.rotationMode = settings.PickupGrabPointRotationMode;
                }

                grabPoints[i] = grabPoint;
            }

            pickup.grabPoints = grabPoints;
        }

        // Will work on this later, I am tired as heck.

        //internal static bool AttachNetworkComponentsToChild(this GameObject prefab, out NetworkPrefab networkprefab, out NetworkEntity entity, NetworkEntity parent, bool includeNetworkPrefab = false) {
        //    networkprefab = null;
        //    entity = null;

        //    if (includeNetworkPrefab) {
        //        networkprefab = prefab.GetComponent<NetworkPrefab>();

        //        if (networkprefab is null)
        //            networkprefab = prefab.AddComponent<NetworkPrefab>();
        //        else {
        //            return false;
        //        }
        //    }

        //    entity = prefab.GetComponent<NetworkEntity>();
        //    if (entity is null)
        //        entity = prefab.AddComponent<NetworkEntity>();
        //    else {
        //        return false;
        //    }

        //    entity.Parent = parent;


        //    return true;
        //}

        internal static bool AttachNetworkComponentsToBase(this GameObject prefab, out NetworkPrefab networkprefab, out NetworkEntity entity) {
            networkprefab = null;
            entity = null;

            networkprefab = prefab.GetComponent<NetworkPrefab>();

            if (networkprefab is null)
                networkprefab = prefab.AddComponent<NetworkPrefab>();
            else {
                return false;
            }

            entity = prefab.GetComponent<NetworkEntity>();
            if (entity is null)
                entity = prefab.AddComponent<NetworkEntity>();
            else {
                return false;
            }

            return true;
        }

        private static ModHashRegistry GetHashOwner(int hash, ModHashRegistry owner) {
            foreach (var mod in Registry.Mods) {
                if (mod == owner)
                    continue;

                if (mod.ItemHashes.Values.Contains(hash))
                    return mod;
            }

            return null;
        }

        /// <summary>
        /// Registers this prefab as a NetworkPrefab in Alta's sytems for later use.
        /// <br></br>
        /// <br></br>
        /// <b>BEWARE!:</b> If the client has this mod, but the server doesn't, this function WILL return null, please take care of that accordingly.
        /// <br></br>
        /// <br></br>
        /// The CustomPrefabId only matters to TavernLib, please do not change it after publishing your mod.
        /// <br></br>
        /// Your prefab is automatically assigned a Hash upon first registry per server.
        /// <br></br>
        /// <br></br>
        /// If you need to attach other classes or NetworkBehaviours to the NetworkPrefab, use the 'AdditionalClasses' parameter.
        /// </summary>
        public static NetworkPrefab? RegisterAsNetworkPrefab(this GameObject prefab, MelonMod mod, string CustomPrefabId, Item item/*, GameObject[] ChildNetworkEntities*/, params Type[] AdditionalClasses) {
            if (mod is null) {
                MessageError(mod, "Attempted to register a custom prefab. However the 'mod' parameter was null.");

                return null;
            }

            if (!inRegistryProcess) {
                MessageError(mod, "Attempted to register a custom prefab too late.", false);

                return null;
            }

            if (Registry is null) {
                MessageError(mod, "Attempted to register a custom prefab. But the HashIds reference was null.");

                return null;
            }

            if (takenHashIds is null) {
                MessageError(mod, "Attempted to register a custom prefab. But the takenHashIds reference was null.");

                return null;
            }

            if (entityManager is null) {
                MessageError(mod, "Attempted to register a custom prefab. But the entityManager reference was null.");

                return null;
            }

            if (string.IsNullOrWhiteSpace(CustomPrefabId) || CustomPrefabId.Length < 3) {
                MessageError(mod, "Attempted to register a custom prefab with an invalid CustomPrefabId. Please make sure your CustomPrefabId has at least three characters.");

                return null;
            }

            if (prefab is null) {
                MessageError(mod, "Attempted to register a custom prefab that was null.");

                return null;
            }

            bool success = prefab.AttachNetworkComponentsToBase(out NetworkPrefab networkprefab, out NetworkEntity entity);

            if (!success) {
                MessageError(mod, "Attempted to register a prefab that's already registered.", false);

                return null;
            }

            List<Component> allComps = new List<Component>();

            allComps.Add(networkprefab);
            allComps.Add(entity);

            if (item != null) {
                ItemSettings settings = RegisteredCustomItems[item];

                if (settings != null && settings._physicalMaterial != null) {
                    PhysicalMaterialPart part = prefab.AddComponent<PhysicalMaterialPart>();

                    part.physicalMaterial = settings._physicalMaterial;

                    allComps.Add(part);
                }
            }

            foreach (Type type in AdditionalClasses) {
                if (type is null)
                    continue;

                Component comp = prefab.AddComponent(type);

                if (comp is Pickup pickup) {
                    if (RegisteredCustomItems.TryGetValue(item, out ItemSettings settings)) {
                        if (settings.wrapHandForPickup)
                            pickup.isTightGrab = true;
                    }

                    pickup.Item = item;
                    pickup.ApplyPickupGrabPoints();
                }

                allComps.Add(comp);
            }

            ExecuteAllGetComponentAtts(allComps.ToArray());

            ModHashRegistry hashIds = mod.GetOrCreateNewModHashIds();

            bool SetHashId(int HashId) {
                FieldInfo fieldInfoHash = typeof(NetworkPrefab).GetField("hash", BindingFlags.NonPublic | BindingFlags.Instance);
                fieldInfoHash.SetValue(networkprefab, HashId);

                ModHashRegistry existingOwner = GetHashOwner(HashId, hashIds);

                if (existingOwner != null) {
                    MessageError(mod, $"Hash '{HashId}' for '{prefab.name}' is already registered by '{existingOwner.ModId}'", false);

                    return false;
                }

                takenHashIds.Add(HashId);

                if (item != null)
                    item.Prefab = networkprefab;

                networkprefab.Initialize();
                entity.OnValidate();

                return true;
            }

            if (hashIds is null) {
                MessageError(mod, "Attempted to register a custom prefab, but something unexpected happened.");

                return null;
            }

            bool alreadyHadId = false;

            foreach (var data in hashIds.ItemHashes) {
                if (data.Key == CustomPrefabId) {
                    bool done = SetHashId(data.Value);

                    if (!done)
                        return null;

                    alreadyHadId = true;

                    break;
                }
            }

            if (!alreadyHadId) {
                int nextAvaliable = GetNextAvailableHashId();

                if (nextAvaliable <= 0) {
                    MessageError(mod, "Attempted to register a custom prefab, but GetNextAvailableHashId returned an error.");

                    return null;
                }

                hashIds.ItemHashes.Add(CustomPrefabId, nextAvaliable);

                bool done = SetHashId(nextAvaliable);

                if (!done)
                    return null;
            }

            NetworkPrefab[] prefabArray = new NetworkPrefab[] { networkprefab };

            MethodInfo methodInfo = typeof(PrefabManager).GetMethod("AddToPrefabMap", BindingFlags.NonPublic | BindingFlags.Static);
            methodInfo.Invoke(null, new object[] { prefabArray });

            RegisteredCustomPrefabs.Add(networkprefab);

            Traverse.Create(typeof(HashedGeneralValue<Item>))
                .Field("items")
                .GetValue<Dictionary<uint, Item>>()
                .Add(networkprefab.Hash, item);

            Tavern.Logger.Msg($"Successfully registered {CustomPrefabId}!");

            return networkprefab;
        }
    }
}