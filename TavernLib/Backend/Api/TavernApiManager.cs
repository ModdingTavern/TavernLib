using System;
using System.IO;
using MelonLoader.Logging;
using TavernLib.Backend.Auth;
using TavernLib.Backend.Server;
using TavernLib.Services;
using YamlDotNet.Serialization;

namespace TavernLib.Backend.Api
{
    public class TavernApiManager : IService
    {
        internal AuthManager AuthManager { get; private set; }
        public ServerListing ActiveListing { get; private set; }
        
        
        public TavernApiManager()
        {
            if (TryCreateServerListing())
            {
                AuthManager = new AuthManager(this);
            }
        }
        
        
        private bool TryCreateServerListing()
        {
            Tavern.Logger.Msg(ColorARGB.Azure, "Attempting to read server config YAML");
                
            if (File.Exists(TavernDirectories.ServerConfig))
            {
                try
                {
                    var data = File.ReadAllText(TavernDirectories.ServerConfig);
                    var deserializer = new DeserializerBuilder().Build();
                    var output = deserializer.Deserialize<ServerConfig>(data);

                    Tavern.Logger.Msg(ColorARGB.Azure, "Instantiating server entry for TavernApiManager");
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