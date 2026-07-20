using System;
using System.IO;
using MelonLoader.Logging;
using TavernLib.Backend.Auth;
using TavernLib.Backend.Server;
using TavernLib.Backend.Server.Configs;
using TavernLib.Services;
using YamlDotNet.Serialization;

namespace TavernLib.Backend.Api
{
    public class TavernApiManager : IService
    {
        internal AuthManager AuthManager { get; private set; }
        internal ServerListingController ListingController { get; private set; }
        
        public UserConfigFile UserConfig { get; private set; }
        public ServerSettingsConfig ServerConfig { get; private set; }
        
        
        public TavernApiManager()
        {
            UserConfig = new UserConfigFile(Path.Combine(TavernDirectories.ModdingTavern, "users.json"));
            ServerConfig = new ServerSettingsConfig(Path.Combine(TavernDirectories.ModdingTavern, "server_settings.json"));
            
            UserConfig.ReadFromFile();
            ServerConfig.ReadFromFile();
            
            ListingController = new ServerListingController(this);
            AuthManager = new AuthManager(this);
        }
    }
}