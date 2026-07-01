using TavernLib.Debugging;
using TavernLib.ServerBrowser;

namespace TavernLib
{
    public class TavernServices
    {
        public DebugHelper DebugHelper { get; }


        internal TavernServices()
        {
            if (CommandLineArguments.Contains("/debug_helper")) DebugHelper = new();
        }
    }
}