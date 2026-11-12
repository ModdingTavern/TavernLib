using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TavernLib.Backend.Api;
using UnityEngine;

namespace TavernLib.Backend.Server;

public class ServerListingController
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);
    private const double LongestWaitSeconds = 60;

    private readonly HttpClient _apiClient;
    private TavernManager _manager;
    private volatile bool _closed;


    public ServerListingController(TavernManager manager)
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


    // One loop for the whole run: it keeps going when a ping fails, even the first one, waits
    // longer after each failure (up to a minute) and logs only when the answer changes.
    private async Task HeartbeatAsync()
    {
        TavernLogger.Msg("Started server listing lifecycle");
        string last = null;
        var logged = false;
        var failures = 0;

        while (!_closed)
        {
            var answer = await Post();
            if (!logged || answer != last)
            {
                if (answer == null) TavernLogger.Msg("Server listing accepted");
                else TavernLogger.Warn($"Server listing failed: {answer}");
                logged = true;
            }

            last = answer;
            failures = answer == null ? 0 : failures + 1;

            var wait = failures == 0
                ? Interval
                : TimeSpan.FromSeconds(Math.Min(LongestWaitSeconds, Interval.TotalSeconds * Math.Pow(2, Math.Min(failures, 5))));
            await Task.Delay(wait);
        }
    }

    // Null when the listing took the heartbeat, otherwise what went wrong.
    private async Task<string> Post()
    {
        try
        {
            var payload = ServerListingPayload.FromConfig(_manager.ServerConfig, _manager.TavernConfig);
            using var response = await _apiClient.PostAsync(BackendUtils.ServerUri, JsonBody(payload));
            if (response.IsSuccessStatusCode) return null;

            var body = response.Content == null ? "" : await response.Content.ReadAsStringAsync();
            return $"{(int)response.StatusCode} {(body.Length > 200 ? body.Substring(0, 200) : body)}";
        }
        catch (Exception e)
        {
            return $"no answer ({e.GetBaseException().GetType().Name})";
        }
    }

    public async Task Ping()
    {
        var answer = await Post();
        if (answer != null) throw new HttpRequestException(answer);
    }

    public async Task OpenListing() => await Ping();


    // Closes the listing and lets the quit go on: answering false here would cancel the quit.
    public bool StartClosingListing()
    {
        CloseListing();
        return true;
    }

    // Waits up to two seconds for the close to reach the listing. The request runs on a pool
    // thread, so a quit on the main thread can't deadlock on it.
    public void CloseListing()
    {
        if (_closed) return;
        _closed = true;

        TavernLogger.Msg("Server listing closing");
        var payload = new
        {
            listing_token = _manager.ServerConfig.LastRead.CommunityListingToken
        };

        try
        {
            var sent = Task.Run(async () =>
            {
                using var response = await _apiClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete, BackendUtils.ServerUri) { Content = JsonBody(payload) });
            }).Wait(TimeSpan.FromSeconds(2));

            if (!sent) TavernLogger.Warn("Server listing close got no answer in 2 s");
        }
        catch (Exception e)
        {
            TavernLogger.Warn($"Server listing close failed: {e.GetBaseException().Message}");
        }
    }

    // Not the game's HttpClientExtensions: its JsonContent and DeleteAsync write the whole body,
    // listing token included, to the game's log on every call.
    private static StringContent JsonBody(object payload) =>
        new(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
}
