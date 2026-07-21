using System;
using System.Net.Http;
using System.Threading.Tasks;
using Alta.Api.DataTransferModels.Extensions;
using MelonLoader.Logging;
using TavernLib.Backend.Api;
using UnityEngine;

namespace TavernLib.Backend.Server
{
    public class ServerListingController
    {
        private readonly HttpClient _apiClient;
        private TavernApiManager _manager;


        public ServerListingController(TavernApiManager manager)
        {
            _manager = manager;
            
            _apiClient = new HttpClient
            {
                BaseAddress = new Uri(BackendUtils.TavernApi),
                Timeout = TimeSpan.FromSeconds(6)
            };
            
            _ = HeartbeatAsync();
            Application.quitting += CloseListing;
            
        }
        
        
        private async Task HeartbeatAsync()
        {
            Tavern.Logger.Msg(ColorARGB.Chartreuse, "Started server listing lifecycle");
            
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
                    await Task.Delay(TimeSpan.FromSeconds(3));
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
            Tavern.Logger.Msg(ColorARGB.Chartreuse, "Server listing heartbeat");
            
            try
            {
                var payload = ServerListingPayload.FromConfig(_manager.ServerConfig);
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
            Tavern.Logger.Msg(ColorARGB.Chartreuse, "Server listing closing");
            var payload = new
            {
                listing_token = _manager.ServerConfig.LastRead.CommunityListingToken
            };
            
            _apiClient.DeleteAsync(BackendUtils.ServerUri, new HttpClientExtensions.JsonContent(payload));
        }
    }
}