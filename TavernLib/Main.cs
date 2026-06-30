using MelonLoader;
using MelonLoader.Logging;


[assembly: MelonInfo(typeof(TavernLib.Main), "TavernLib", "0.0.1", "Tavern Team", "https://github.com/ModdingTavern/TavernLib")]
namespace TavernLib
{
    public class Main : MelonPlugin
    {
        internal static MelonLogger.Instance Logger { get; private set; }
        
        
        public override void OnEarlyInitializeMelon()
        {
            Logger = LoggerInstance;
            Logger.MsgPastel(ColorARGB.Azure, "Hello from TavernLib!");
        }
    }
}