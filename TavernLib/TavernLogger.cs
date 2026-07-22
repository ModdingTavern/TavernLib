using System.Runtime.CompilerServices;
using MelonLoader.Logging;

namespace TavernLib;

internal static class TavernLogger
{
    private static void Log(ColorARGB color, string callerName, object msg, string prefix = "")
    {
        Tavern.Logger.Msg(color, $"{prefix} [{callerName}]: {msg}");
    }
    
    public static void Msg(object msg, [CallerMemberName] string callerName = "")
    {
        Log(ColorARGB.MediumSpringGreen, callerName, msg);
    }
    
    public static void Warn(object msg, [CallerMemberName] string callerName = "")
    {
        Log(ColorARGB.SandyBrown, callerName, msg, "WARNING");
    }
    
    public static void Error(object msg, [CallerMemberName] string callerName = "")
    {
        Log(ColorARGB.Tomato, callerName, msg, "ERROR");
    }
}