using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Alta.Api.DataTransferModels.Extensions;
using TavernLib.Backend.Server.Configs;
using UnityEngine;

namespace TavernLib.Backend.Server
{
    public class ServerListingController
    {
        private readonly HttpClient _apiClient;
        public UserConfigFile UserConfig { get; private set; }
        public ServerSettingsConfig ServerConfig { get; private set; }


        public ServerListingController()
        {
            _apiClient = new HttpClient
            {
                BaseAddress = new Uri(BackendUtils.TavernApi),
                Timeout = TimeSpan.FromSeconds(6)
            };
            
            UserConfig = new UserConfigFile(Path.Combine(TavernDirectories.ModdingTavern, "users.json"));
            ServerConfig = new ServerSettingsConfig(Path.Combine(TavernDirectories.ModdingTavern, "server_settings.json"));
            
            UserConfig.ReadFromFile();
            UserConfig.ReadFromFile();
            
            _ = HeartbeatAsync();
            Application.quitting += CloseListing;
        }
        
        
        private async Task HeartbeatAsync()
        {
            try
            {
                await OpenListing();
            }
            catch (Exception e)
            {
                Tavern.Logger.Error($"Error when publishing data to api! {e}");
                throw;
            }
            
            while (true)
            {
                try
                {
                    await Ping();
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
                catch (Exception e)
                {
                    Tavern.Logger.Error($"Error when pinging server! {e}");
                    throw;
                }
            }
        }
        
        public async Task Ping()
        {
            try
            {
                var payload = ServerListingPayload.FromConfig(ServerConfig);
                await _apiClient.PostAsync(BackendUtils.ServerUri, new HttpClientExtensions.JsonContent(payload));
            }
            catch (Exception e)
            {
                Tavern.Logger.Error($"Error when pinging servers endpoint! {e}");
                throw;
            }
        }

        public async Task OpenListing() => await Ping();
        
        
        public void CloseListing()
        {
            var payload = new
            {
                listing_token = ServerConfig.LastRead.CommunityListingToken
            };
            
            _apiClient.DeleteAsync(BackendUtils.ServerUri, new HttpClientExtensions.JsonContent(payload));
        }
    }
}