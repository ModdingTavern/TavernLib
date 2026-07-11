using System;
using System.IO;
using System.Net;
using kcp2k;
using MelonLoader;
using MelonLoader.Logging;
using TavernLib.Services;
using TavernLib.Services.Server;
using YamlDotNet.Serialization;


[assembly: MelonInfo(typeof(TavernLib.Tavern), "TavernLib", "0.0.1", "Tavern Team", "https://github.com/ModdingTavern/TavernLib")]
namespace TavernLib
{
    public class Tavern : MelonPlugin
    {
        internal static MelonLogger.Instance Logger { get; private set; }


        public override void OnEarlyInitializeMelon()
        {
            TavernServices.Init();
            Logger = LoggerInstance;
            
            if (!CommandLineArguments.Contains(TavernArgs.NoApi) && CommandLineArguments.Contains(CommandLineArguments.StartServerArgument))
            {
                TrySetupServerConfig();
            }
        }
        
        private void TrySetupServerConfig()
        {
            Logger.Msg(ColorARGB.Azure, "Attempting to read server config YAML");
                
            if (File.Exists(TavernDirectories.ServerConfig))
            {
                try
                {
                    string data = File.ReadAllText(TavernDirectories.ServerConfig);
                        
                    var deserializer = new DeserializerBuilder().Build();
                    var output = deserializer.Deserialize<ServerConfig>(data);

                    Logger.Msg(ColorARGB.Azure, "Instantiating server entry for API");
                    TavernServices.ActiveListing = new ServerListing(output);
                }
                    
                catch (Exception e)
                {
                    Logger.Error($"Error when loading server config! {e}");
                    throw;
                }
            }

            else
            {
                Logger.Warning("The server was told to look for server-config.yaml, but one doesn't exist!");
                Logger.Warning("Your server will NOT show up on the public discovery :/");
            }
        }
        
        public override void OnLateInitializeMelon()
        {
            
        }
    }
}