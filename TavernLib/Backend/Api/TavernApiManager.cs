using System;
using System.IO;
using MelonLoader.Logging;
using TavernLib.Backend.Auth;
using TavernLib.Backend.Server;
using YamlDotNet.Serialization;

namespace TavernLib.Backend.Api
{
    public class TavernApiManager : IApiManager
    {
        public IAuthManager AuthManager { get; private set; }
        public ServerListing ActiveListing { get; private set; }
        
        
        public TavernApiManager()
        {
            if (TrySetupServerListing())
            {
                AuthManager = new AuthManager(this);
            }
        }
        
        
        private bool TrySetupServerListing()
        {
            Tavern.Logger.Msg(ColorARGB.Azure, "Attempting to read server config YAML");
                
            if (File.Exists(TavernDirectories.ServerConfig))
            {
                try
                {
                    string data = File.ReadAllText(TavernDirectories.ServerConfig);
                        
                    var deserializer = new DeserializerBuilder().Build();
                    var output = deserializer.Deserialize<ServerConfig>(data);

                    Tavern.Logger.Msg(ColorARGB.Azure, "Instantiating server entry for API");
                    ActiveListing = new ServerListing(output);
                }
                    
                catch (Exception e)
                {
                    Tavern.Logger.Error($"Error when loading server config! {e}");
                    return false;
                }
            }

            else
            {
                Tavern.Logger.Warning("The server was told to look for server-config.yaml, but one doesn't exist!");
                Tavern.Logger.Warning("Your server will NOT show up on the public discovery");

                ActiveListing = null;
            }

            return true;
        }
    }
}