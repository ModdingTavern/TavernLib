using System;
using System.Net.Http;
using System.Threading.Tasks;
using Alta.Api.DataTransferModels.Extensions;
using TavernLib.Backend.Api;
using UnityEngine;
using TavernLib.Backend;

namespace TavernLib.Backend.Server;

public class ServerListingController
{
    private readonly HttpClient _apiClient;
    private TavernApiManager _manager;


    public ServerListingController(TavernApiManager manager)
    {
        _manager = manager;

        var handler = new HttpClientHandler();
        var systemProxy = WindowsProxy.CreateSystemProxy();
        if (systemProxy != null)
        {
            handler.Proxy = systemProxy;
            handler.UseProxy = true;
        }

        _apiClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(BackendUtils.TavernApi),
            Timeout = TimeSpan.FromSeconds(6)
        };
            
        _ = HeartbeatAsync();
        Application.wantsToQuit += StartClosingListing;
    }
        
        
    private async Task HeartbeatAsync()
    {
        TavernLogger.Msg("Started server listing lifecycle");
            
        try
        {
            await OpenListing();
        }
        catch (Exception e)
        {
            TavernLogger.Error($"Error when publishing data to api! {e}");
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
                TavernLogger.Error($"Error when pinging server! {e}");
            }
        }
    }
        
    public async Task Ping()
    {
        TavernLogger.Msg("Server listing heartbeat");
            
        try
        {
            var payload = ServerListingPayload.FromConfig(_manager.ServerConfig, _manager.TavernConfig);
            await _apiClient.PostAsync(BackendUtils.ServerUri, new HttpClientExtensions.JsonContent(payload));
        }
        catch (Exception e)
        {
            TavernLogger.Error($"Error when pinging servers endpoint! {e}");
            throw;
        }
    }

    public async Task OpenListing() => await Ping();
        
        
    public bool StartClosingListing()
    {
        CloseListing();
        return false;
    }
        
    public void CloseListing()
    {
        TavernLogger.Msg("Server listing closing");
        var payload = new
        {
            listing_token = _manager.ServerConfig.LastRead.CommunityListingToken
        };
            
        _apiClient.DeleteAsync(BackendUtils.ServerUri, new HttpClientExtensions.JsonContent(payload));
    }
}