using System;
using MelonLoader;
using TavernLib.Backend.Api;
using TavernLib.Debugging;
using TavernLib.Services;


[assembly: MelonInfo(typeof(TavernLib.Tavern), "TavernLib", "0.0.1", "Tavern Team", "https://github.com/ModdingTavern/TavernLib")]
namespace TavernLib
{
    public class Tavern : MelonPlugin
    {
        internal static MelonLogger.Instance Logger { get; private set; }


        public override void OnEarlyInitializeMelon()
        {
            Logger = LoggerInstance;
            
            SetupServices();
        }
        
        private void SetupServices()
        {
            try
            {
                if (CommandLineArguments.Contains("/debug_helper")) TavernServices.AddService(new DebugHelper());
                
                if (CommandLineArguments.Contains(CommandLineArguments.StartServerArgument))
                {
                    TavernServices.AddService(new TavernApiManager());
                }

            }
            catch (Exception e)
            {
                Logger.BigError($"Error when setting up base TavernLib services!!!!! {e}");
                throw;
            }
        }
    }
}