using TavernLib.Debugging;
using TavernLib.ServerBrowser;

namespace TavernLib
{
    public class TavernServices
    {
        public DebugHelper DebugHelper { get; }
        public ServerMounter ServerMounter { get; } = new();


        internal TavernServices()
        {
            if (CommandLineArguments.Contains("/debug_helper")) DebugHelper = new();
        }
    }
}