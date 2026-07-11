using TavernLib.Debugging;
using TavernLib.Services.Server;

namespace TavernLib.Services
{
    public static class TavernServices
    {
        public static DebugHelper DebugHelper { get; private set; }
        public static ServerListing ActiveListing { get; set; }


        internal static void Init()
        {
            if (CommandLineArguments.Contains("/debug_helper")) DebugHelper = new();
        }
    }
}