using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Alta.Console;
using HarmonyLib;

namespace TavernLib.Patches;

// How a command's outcome reaches the console, for every command: the game's, TavernLib's and
// other mods'.
[HarmonyPatch]
public static class CommandResultPatches
{
    private static readonly ConditionalWeakTable<PlayerList, string> Typed = new();

    // The converter skips names it can't find, so a list can come out empty. This keeps what was
    // typed, for the refusal.
    [HarmonyPatch(typeof(PlayerListConverter), nameof(PlayerListConverter.ConvertFrom),
        [typeof(ITypeDescriptorContext), typeof(CultureInfo), typeof(object)]), HarmonyPostfix]
    public static void RememberTypedPlayers(object value, object __result)
    {
        if (__result is not PlayerList list || value is not string text) return;

        Typed.Remove(list);
        Typed.Add(list, text.Trim());
    }

    // A command given players that matched nobody ran on nobody and answered Success.
    [HarmonyPatch(typeof(CommandService), nameof(CommandService.HandleMatchedCommand)), HarmonyPrefix]
    public static bool RefuseEmptyPlayerList(CommandSearchResult matchedCommand, ref Task<CommandResult> __result)
    {
        var empty = matchedCommand.arguments?.OfType<PlayerList>().FirstOrDefault(list => !list.Any());
        if (empty == null) return true;

        Typed.TryGetValue(empty, out var text);
        var message = text == "*" ? "No players are online" : text == null ? "No online player matched" : $"No online player matches \"{text}\"";
        __result = Task.FromResult(CommandResult.ErrorResult(new CommandException(message), matchedCommand.command));
        return false;
    }

    // Reflection wraps a synchronous command's error in a TargetInvocationException, and its
    // message was all the console showed: "Exception has been thrown by the target of an
    // invocation." The command's own error is the one it meant to give.
    [HarmonyPatch(typeof(CommandService), nameof(CommandService.HandleMatchedCommand)), HarmonyPostfix]
    public static void UnwrapCommandError(ref Task<CommandResult> __result) => __result = Unwrap(__result);

    private static async Task<CommandResult> Unwrap(Task<CommandResult> pending)
    {
        var result = await pending;
        if (result?.Exception is TargetInvocationException { InnerException: { } inner }) result.Exception = inner;
        return result;
    }
}
