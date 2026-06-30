using MelonLoader;
using TavernLib.Debugging;


[assembly: MelonInfo(typeof(TavernLib.TavernCore), "TavernLib", "0.0.1", "Tavern Team", "https://github.com/ModdingTavern/TavernLib")]
namespace TavernLib
{
    public class TavernCore : MelonPlugin
    {
        internal static MelonLogger.Instance Logger { get; private set; }
        internal static DebugHelper DebugHelper { get; private set; }


        public override void OnLateInitializeMelon() => Setup();
        
        private void Setup()
        {
            Logger = LoggerInstance;
            if (CommandLineArguments.Contains("/debug_helper")) DebugHelper = new();
        }

        public override void OnGUI()
        {
            DebugHelper?.OnGui();
        }
    }
}