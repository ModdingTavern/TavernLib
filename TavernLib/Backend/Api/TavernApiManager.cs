using System.IO;
using MelonLoader.Logging;
using TavernLib.Backend.Auth;
using TavernLib.Backend.Server;
using TavernLib.Backend.Server.Configs;
using TavernLib.Services;

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
            Tavern.Logger.Msg(ColorARGB.Chartreuse, "Creating configs");
            UserConfig = new UserConfigFile(Path.Combine(TavernDirectories.ModdingTavern, "users.json"));
            ServerConfig = new ServerSettingsConfig(Path.Combine(TavernDirectories.ModdingTavern, "server_settings.json"));
            
            Tavern.Logger.Msg(ColorARGB.Chartreuse, "Reading configs");
            UserConfig.ReadFromFile();
            ServerConfig.ReadFromFile();
            
            Tavern.Logger.Msg(ColorARGB.Chartreuse, "Creating controllers");
            if (ServerConfig.LastRead.CommunityListed) ListingController = new ServerListingController(this);
            if (!CommandLineArguments.Contains(TavernArgs.DontManageAuth)) AuthManager = new AuthManager(this);
            
            Tavern.Logger.Msg(ColorARGB.Chartreuse, $"Listing Is Active: {ListingController != null}, Managing Auth: {AuthManager != null}");
        }
    }
}