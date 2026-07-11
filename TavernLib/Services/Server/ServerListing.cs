using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alta.Api.DataTransferModels.Extensions;
using Newtonsoft.Json;
using UnityEngine;

namespace TavernLib.Services.Server
{
    public class ServerListing : IApiServer
    {
        private HttpClient _apiClient;
        public readonly ServerConfig _config;
        
        
        public ServerListing(ServerConfig config)
        {
            _apiClient = new HttpClient
            {
                BaseAddress = new Uri(ServiceUtils.TavernApi),
                Timeout = TimeSpan.FromSeconds(6)
            };

            _config = config;
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
                var minimalConfig = MinimalServerConfig.FromServerConfig(_config);
                await _apiClient.PostAsync(ServiceUtils.ServerUri, new HttpClientExtensions.JsonContent(minimalConfig));
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
                listing_token = _config.ListingToken
            };
            
            _apiClient.DeleteAsync(ServiceUtils.ServerUri, new HttpClientExtensions.JsonContent(payload));
        }
    }
}