using MelonLoader;


[assembly: MelonInfo(typeof(TavernLib.Tavern), "TavernLib", "0.0.1", "Tavern Team", "https://github.com/ModdingTavern/TavernLib")]
namespace TavernLib
{
    public class Tavern : MelonPlugin
    {
        internal static MelonLogger.Instance Logger { get; private set; }
        public static TavernServices Services { get; private set; }


        public override void OnEarlyInitializeMelon()
        {
            Services = new();
        }
        
        public override void OnLateInitializeMelon()
        {
            Logger = LoggerInstance;
        }
    }
}