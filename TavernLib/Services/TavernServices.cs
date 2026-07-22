using System;
using System.Collections.Generic;
using MelonLoader.Logging;
using TavernLib.Debugging;
using TavernLib.Services.Server;

namespace TavernLib.Services
{
    public static class TavernServices
    {
        public static DebugHelper DebugHelper { get; private set; }
        public static ServerEntry ActiveEntry { get; set; }

            ServiceEntries[typeof(T)] = instance;
        }

        internal static void Init()
        {
            if (CommandLineArguments.Contains("/debug_helper")) DebugHelper = new();
        }
    }
}