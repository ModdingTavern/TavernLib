using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Alta.Alchemy;
using Alta.Caves;
using Alta.Console;
using Alta.Console.Commands;
using Alta.Networking;
using Alta.Networking.Scripts.Player;
using HarmonyLib;

namespace TavernLib.Patches;

// The game's commands that answered Success having done or read nothing. An error thrown here
// reaches the console as the command's own (CommandResultPatches).
public static class GameCommandPatches
{
    // Applied once Unity is up (OnLateInitializeMelon), not with the attribute patches when the
    // plugin loads: patching compiles game code that calls into the engine, and compiled before
    // the engine registers its calls, Mono keeps them missing for good. Seen on the harness: every
    // transform access then failed and the server never finished loading.
    internal static void Apply(HarmonyLib.Harmony harmony)
    {
        Patch(harmony, typeof(TimeCommandModule), "Set", prefix: nameof(CheckTimeSet));
        Patch(harmony, typeof(SettingsCommandModules), "ListPossivleValues", postfix: nameof(CheckPossibleValues));
        Patch(harmony, typeof(SelectionCommandModule.MovingModule), "Exact", prefix: nameof(CheckMoveSelection));
        Patch(harmony, typeof(SelectionCommandModule.RotatingModule), "Exact", prefix: nameof(CheckRotateSelection));
        Patch(harmony, typeof(WackyCommandModule), "Destroy", prefix: nameof(CheckDestroy));
        Patch(harmony, typeof(WorldCommandModule.CaveCommandModule), nameof(WorldCommandModule.CaveCommandModule.CaveTeleport), prefix: nameof(ReplaceCaveTeleport));
        Replace("time", "ofday", AccessTools.Method(typeof(GameCommandPatches), nameof(TimeOfDay)));
    }

    private static void Patch(HarmonyLib.Harmony harmony, Type type, string name, string prefix = null, string postfix = null)
    {
        var original = AccessTools.DeclaredMethod(type, name);
        if (original == null)
        {
            TavernLogger.Warn($"The game has no {type.Name}.{name} to patch");
            return;
        }

        try
        {
            harmony.Patch(original,
                prefix == null ? null : new HarmonyMethod(typeof(GameCommandPatches), prefix),
                postfix == null ? null : new HarmonyMethod(typeof(GameCommandPatches), postfix));
        }
        catch (Exception e)
        {
            TavernLogger.Warn($"Could not patch {type.Name}.{name}: {e.Message}");
        }
    }

    #region time

    // time ofday logged the time and answered nothing. Its replacement answers it; the game's own
    // constructor builds it with the old command's requirements, aliases and priority, so help
    // and permissions read as before.
    private static float TimeOfDay(bool isNormalised = false)
    {
        var time = TimeManager();
        return isNormalised ? time.CurrentNormalizedTime : time.CurrentTime;
    }

    // time set took a keyword it doesn't know, or ran with no day and night, and answered Success.
    public static void CheckTimeSet(string time)
    {
        var manager = TimeManager();
        if (float.TryParse(time, out _)) return;

        var names = manager.AvailableKeyWords.Select(keyword => keyword.Name).ToList();
        if (!names.Any(name => string.Equals(name, time, StringComparison.InvariantCultureIgnoreCase)))
            throw new CommandException($"No time called {time}. Give an hour (0 to 24) or one of: {string.Join(", ", names)}");
    }

    private static GameTimeManager TimeManager()
    {
        var manager = GameTimeManager.Instance;
        if (manager == null) throw new CommandException("This server has no day and night");
        return manager;
    }

    #endregion

    #region settings, select, wacky

    // settings possible answered Success with nothing for a setting it doesn't have, or one that
    // isn't a set of choices.
    public static void CheckPossibleValues(string settingsTarget, string setting, object __result)
    {
        if (__result == null) throw new CommandException($"The {settingsTarget} settings have no {setting} with a set of choices");
    }

    // select move exact and select rotate exact did nothing with nothing selected.
    public static void CheckMoveSelection() => RequireSelection();

    public static void CheckRotateSelection() => RequireSelection();

    private static void RequireSelection()
    {
        if (CommandService.CurrentContext?.Selection?.Current == null) throw new CommandException("Nothing is selected");
    }

    // wacky destroy logged why it couldn't destroy something and answered Success; an id that
    // doesn't exist threw from inside the lookup.
    public static void CheckDestroy(uint id)
    {
        NetworkEntity entity;
        try
        {
            entity = NetworkSceneManager.Current.GetEntity(id);
        }
        catch (Exception)
        {
            entity = null;
        }

        if (entity == null) throw new CommandException($"No entity with id {id}");
        if (entity.Parent != null) throw new CommandException($"{entity} can't be destroyed: it is parented to {entity.ChunkingParent}");
        if (entity.PrefabRoot.Identifier != entity.Identifier) throw new CommandException($"{entity} isn't a prefab's root: destroy {entity.PrefabRoot}");
    }

    #endregion

    #region caves

    // world caves teleport moved nobody when the layer already existed, and nobody when it couldn't
    // be made. This one answers who it moved.
    public static bool ReplaceCaveTeleport(PlayerList players, int layer, ref Task __result)
    {
        __result = TeleportToCaveLayer(players, layer);
        return false;
    }

    private static async Task<string> TeleportToCaveLayer(PlayerList players, int layer)
    {
        var manager = UnityEngine.Object.FindObjectOfType<CaveLayerManager>();
        if (manager == null) throw new CommandException("This world has no caves");

        if (!manager.Layers.TryGetValue(layer, out var caveLayer))
            caveLayer = await ProgressCommandModule.GenerateCavesTo(layer, ServerDiagnosticsMessage.SetMessage);
        if (caveLayer == null) throw new CommandException($"Cave layer {layer} could not be made");

        var position = caveLayer.PortalTransform.position;
        var moved = new List<string>();
        foreach (Player player in players)
        {
            if (player.PlayerController == null) continue;

            var effects = player.PlayerController.GetComponent<PlayerEffectController>();
            if (effects != null) effects.Teleport(position, 1f);
            else player.PlayerController.LocomotionController.MoveTo(position, LocomotionFunction.Reposition);
            moved.Add(player.UserInfo.Username);
        }

        if (moved.Count == 0) throw new CommandException("None of them has a body to teleport");
        return $"Teleported {string.Join(", ", moved)} to cave layer {layer}";
    }

    #endregion

    private static void Replace(string moduleName, string name, MethodInfo method)
    {
        var module = CommandService.CommandCollection.Modules.FirstOrDefault(m => m.Parent == null && m.Name == moduleName);
        var index = module?.Commands.FindIndex(c => c.Name == name) ?? -1;
        if (index < 0)
        {
            TavernLogger.Warn($"The game has no {moduleName} {name} command to replace");
            return;
        }

        var old = module.Commands[index];
        var command = new Command(method, module, old.Attribute) { Priority = old.Priority };
        command.Aliases.AddRange(old.Aliases);
        command.Requirements.AddRange(old.Requirements);
        CommandCollection.GetParams(method, command);
        module.Commands[index] = command;
    }
}
