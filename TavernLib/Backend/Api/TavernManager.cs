using System;
using System.IO;
using TavernLib.Backend.Auth;
using TavernLib.Backend.Server;
using TavernLib.Backend.Server.Configs;
using TavernLib.Services;

namespace TavernLib.Backend.Api;

public class TavernManager : IService
{
    internal AuthManager AuthManager { get; private set; }
    internal ServerListingController ListingController { get; private set; }

    public UserConfigFile UserConfig { get; private set; }
    public ServerSettingsConfig ServerConfig { get; private set; }
    public TavernServerConfig TavernConfig { get; private set; }
    public WhitelistRequestsConfigFile WhitelistRequests { get; private set; }


    public TavernManager()
    {
        TavernLogger.Msg("Creating configs");
        UserConfig = new UserConfigFile(Path.Combine(TavernDirectories.ModdingTavern, TavernDirectories.Users));
        ServerConfig = new ServerSettingsConfig(Path.Combine(TavernDirectories.ModdingTavern, TavernDirectories.ServerSettings));
        TavernConfig = new TavernServerConfig(Path.Combine(TavernDirectories.ModdingTavern, TavernDirectories.TavernServer));
        WhitelistRequests = new WhitelistRequestsConfigFile(Path.Combine(TavernDirectories.ModdingTavern, TavernDirectories.WhitelistRequests));

        TavernLogger.Msg("Reading configs");
        TryRead(UserConfig.ReadFromFile, TavernDirectories.Users);
        TryRead(ServerConfig.ReadFromFile, TavernDirectories.ServerSettings);
        TryRead(TavernConfig.ReadFromFile, TavernDirectories.TavernServer);
        TryRead(WhitelistRequests.ReadFromFile, TavernDirectories.WhitelistRequests);

        TavernLogger.Msg("Creating controllers");
        if (ServerConfig.LastRead.CommunityListed) ListingController = new ServerListingController(this);
        if (!CommandLineArguments.Contains(TavernArgs.DontManageAuth)) AuthManager = new AuthManager(this);

        TavernLogger.Msg($"Listing Is Active: {ListingController != null}, Managing Auth: {AuthManager != null}");
    }

    // A file that can't be read doesn't stop TavernLib loading. Joins that need it are refused
    // until it reads again, since every join reads it afresh.
    private static void TryRead(Action read, string path)
    {
        try
        {
            read();
        }
        catch (Exception)
        {
            TavernLogger.Error($"TavernLib keeps running without {Path.GetFileName(path)}; joins that need it are refused until it reads.");
        }
    }
}