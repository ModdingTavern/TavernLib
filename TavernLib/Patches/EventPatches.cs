using Alta.Networking;
using Alta.NetworkingTransport;
using HarmonyLib;
using TavernLib.Utils;

namespace TavernLib.Patches;

[HarmonyPatch]
public class EventPatches
{
    [HarmonyPatch(typeof(Socket), MethodType.Constructor, [typeof(ITransport), typeof(int), typeof(bool), typeof(ConnectionChannel[]), typeof(ConnectionChannel)]), HarmonyPostfix]
    public static void SocketCreatedEvent()
    {
        TavernEvents.SocketCreated.Invoke(Socket.Current);
    }
}