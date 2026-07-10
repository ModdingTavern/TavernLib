using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Alta.Api.DataTransferModels.Extensions;

namespace TavernLib.Services.Server
{
    public class ServerEntry : IApiServer
    {
        private HttpClient _apiClient;
        private ServerConfig _config;
        
        
        public ServerEntry(ServerConfig config)
        {
            _apiClient = new HttpClient
            {
                BaseAddress = new Uri(ServiceUtils.TavernApi),
                Timeout = TimeSpan.FromSeconds(6)
            };

            _config = config;
            _ = HeartbeatAsync();
        }
        
        
        private async Task HeartbeatAsync()
        {
            try
            {
                await PublishDataToApi();
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

        public async Task PublishDataToApi() => await Ping();
    }
}