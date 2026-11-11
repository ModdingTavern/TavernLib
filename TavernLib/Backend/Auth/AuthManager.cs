using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TavernLib.Backend.Api;
using TavernLib.Backend.Server.Configs;

namespace TavernLib.Backend.Auth;

internal class AuthManager
{
    private TavernManager _manager;
    private TcpListener _listener;


    public AuthManager(TavernManager manager)
    {
        _manager = manager;
        _listener = new(IPAddress.Any, 1762);

        _ = StartAuthCycle();
    }


    private async Task StartAuthCycle()
    {
        TavernLogger.Msg("Starting auth cycle");

        _listener.Start();

        var stopOnQuit = new CancellationTokenSource();
        UnityEngine.Application.quitting += stopOnQuit.Cancel;

        TavernLogger.Msg("Starting auth listening cycle");
        while (!stopOnQuit.IsCancellationRequested)
        {
            TcpClient client = await _listener.AcceptTcpClientAsync();
            _ = InterpretTcpStream(client);
        }
    }

    private async Task<string> ReadFullPayload(Stream stream, CancellationToken token)
    {
        TavernLogger.Msg("Reading payload");

        var buffer = new byte[4096];
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer, totalRead, buffer.Length - totalRead, token);
            if (bytesRead == 0) break;

            totalRead += bytesRead;
            var currentText = Encoding.UTF8.GetString(buffer, 0, totalRead).Trim();

            if (currentText.StartsWith("{") && currentText.EndsWith("}"))
            {
                try
                {
                    JObject.Parse(currentText);
                    return currentText;
                }
                catch (JsonReaderException)
                {
                }
            }
        }

        throw new Exception("Invalid or incomplete payload received.");
    }

    private async Task InterpretTcpStream(TcpClient client)
    {
        TavernLogger.Msg($"Connected to TcpClient ({(client.Client.RemoteEndPoint as IPEndPoint)?.Address}), interpreting stream");

        using var stream = client.GetStream();
        using var timeout = new CancellationTokenSource();
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        var payload = await ReadFullPayload(stream, timeout.Token);

        try
        {
            TavernLogger.Msg("Finding payload type");

            var jsonPayload = JObject.Parse(payload);

            if (jsonPayload.ContainsKey("ping")) await WritePongResponse(stream);
            else if (jsonPayload.ContainsKey("register_whitelist_application")) await ManageWhitelistApplicationRequest(stream, jsonPayload, client);
            else if (jsonPayload.ContainsKey("username")) await ManageAuthRequest(stream, jsonPayload, client);
            else
            {
                TavernLogger.Warn($"Unknown payload sent to AuthManager, keys: {string.Join(", ", jsonPayload.Properties().Select(p => p.Name))}");
            }
        }
        catch (Exception e)
        {
            TavernLogger.Error($"Error decoding TCP stream {e}");
        }

        finally
        {
            TavernLogger.Msg("Closing connection to joining user");
            client.Close();
        }
    }

    private async Task WritePongResponse(Stream stream)
    {
        var response = new AuthPayloads.PingResponse(
            _manager.ServerConfig.LastRead.Name,
            !string.IsNullOrWhiteSpace(_manager.ServerConfig.LastRead.PasswordHash),
            _manager.UserConfig.LastRead.Whitelist.Usernames.Count > 0 || _manager.UserConfig.LastRead.Whitelist.Ips.Count > 0,
            1757); // TODO

        await WriteResponse(stream, response);
    }


    private async Task ManageAuthRequest(Stream stream, JObject payload, TcpClient joiner)
    {
        try
        {
            var typedPayload = JsonConvert.DeserializeObject<AuthPayloads.AuthenticateRequest>(payload.ToString());
            if (string.IsNullOrWhiteSpace(typedPayload.Token) || string.IsNullOrWhiteSpace(typedPayload.Username))
            {
                await WriteResponse(stream, new AuthPayloads.GenericFail("Malformed authentication data"));
                return;
            }


            var joinerIp = ((IPEndPoint)joiner.Client.RemoteEndPoint).Address.ToString();

            TavernLogger.Msg($"User {typedPayload.Username} at {joinerIp} joining server");

            await WriteResponse(stream, ResolveJoin(typedPayload, joinerIp));
        }
        catch (Exception e)
        {
            TavernLogger.Error($"Error when managing join request {e}");
            throw;
        }
    }

    // The checks and the registration run under users.json's lock on a fresh read, so two joins at
    // once can't mint the same id or drop each other's entry. A file that can't be read refuses
    // the join rather than reading as empty lists.
    private object ResolveJoin(AuthPayloads.AuthenticateRequest payload, string ip)
    {
        if (!_manager.ServerConfig.Readable && !TryRead(_manager.ServerConfig.ReadFromFile))
            return new AuthPayloads.GenericFail("The server can't read its settings right now");

        object response = null;
        try
        {
            _manager.UserConfig.Update(config =>
            {
                response = CheckIfPermitted(config, ip, payload);
                if (response != null) return false;

                return Register(config, payload, ip, out response);
            });
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            return new AuthPayloads.GenericFail("The server can't read its user list right now");
        }

        return response;
    }

    private static bool TryRead(Action read)
    {
        try
        {
            read();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // Returns true when users.json changed and must be written.
    private bool Register(UserConfig config, AuthPayloads.AuthenticateRequest payload, string ip, out object response)
    {
        var key = payload.Username.ToLowerInvariant();
        var changed = false;

        if (config.Users.TryGetValue(key, out var userData) && userData != null)
        {
            if (string.IsNullOrWhiteSpace(userData.Token))
            {
                userData.Token = payload.Token;
                changed = true;
            }
            else if (payload.Token != userData.Token)
            {
                TavernLogger.Msg($"Joining user at IP {ip} tried to take username");
                response = new AuthPayloads.GenericFail("Name taken by someone else, or you lost the token to your account!");
                return false;
            }
        }
        else
        {
            TavernLogger.Msg($"Joining user at IP {ip} being allotted a slot in Users.json");
            userData = new UserConfig.User
            {
                RegisteredFrom = ip,
                Token = payload.Token,
                UserId = NextUserId(config)
            };
            config.Users[key] = userData;
            changed = true;
        }

        response = new AuthPayloads.AuthenticateOk(userData.UserId, _manager.ServerConfig.LastRead.QuestScene, _manager.ServerConfig.LastRead.TutorialScene);
        return changed;
    }

    // One past the highest id in the file, never the user count: after a user is removed, the
    // count would hand their id, and their save, to the next newcomer.
    private static ulong NextUserId(UserConfig config)
    {
        var highest = config.Users.Values.Where(user => user != null).Select(user => user.UserId).DefaultIfEmpty(0UL).Max();
        return Math.Max(FirstUserId, highest + 1);
    }

    private const ulong FirstUserId = 1000000000UL;

    // Returns the refusal to send, or null when the joiner may continue.
    private object CheckIfPermitted(UserConfig config, string joinerIp, AuthPayloads.AuthenticateRequest payload)
    {
        TavernLogger.Msg($"Checking if joining user {joinerIp} can join");
        // Whitelist
        if (config.Whitelist.Ips.Count > 0 || config.Whitelist.Usernames.Count > 0)
        {
            var ipAllowed = config.Whitelist.Ips.Contains(joinerIp);
            var nameAllowed = config.Whitelist.Usernames.Contains(payload.Username);

            if (!ipAllowed && !nameAllowed)
            {
                TavernLogger.Msg($"Joining user at {joinerIp} was not on the whitelist");
                return new AuthPayloads.NotWhitelisted();
            }
        }

        // Blacklist
        if (config.Blacklist.Ips.Count > 0 || config.Blacklist.Usernames.Count > 0)
        {
            var ipBlocked = config.Blacklist.Ips.Contains(joinerIp);
            var nameBlocked = config.Blacklist.Usernames.Any(u => string.Equals(u, payload.Username, StringComparison.OrdinalIgnoreCase));

            if (ipBlocked || nameBlocked)
            {
                TavernLogger.Msg($"Joining user at {joinerIp} was on the blacklist");
                return new AuthPayloads.GenericFail("Blacklisted");
            }
        }

        // Password
        if (!string.IsNullOrWhiteSpace(_manager.ServerConfig.LastRead.PasswordHash))
        {
            if (string.IsNullOrWhiteSpace(payload.Password))
            {
                TavernLogger.Msg($"Joining user at {joinerIp} gave no password to server");
                return new AuthPayloads.NeedsPassword();
            }

            // Hash the user provided password again to match Tavern Launcher's password setup
            if (BackendUtils.HashDigest(payload.Password) != _manager.ServerConfig.LastRead.PasswordHash)
            {
                TavernLogger.Msg($"Joining user at {joinerIp} gave the wrong password");
                return new AuthPayloads.WrongPassword();
            }
        }

        // IP limit
        if (_manager.ServerConfig.LastRead.EnforceIpLimit)
        {
            var matchingIpCount = config.Users.Count(user => user.Value?.RegisteredFrom == joinerIp);
            if (matchingIpCount > 4)
            {
                return new AuthPayloads.GenericFail("Too many accounts with same origin");
            }
        }

        TavernLogger.Msg($"Joining user at {joinerIp} passed all authentication checks");
        return null;
    }

    private async Task ManageWhitelistApplicationRequest(Stream stream, JObject payload, TcpClient joiner)
    {
        if (CommandLineArguments.Contains(TavernArgs.DontManageAuth))
        {
            TavernLogger.Msg("Ignoring whitelist application -- TavernLauncher is managing auth for this instance.");
            return;
        }

        try
        {
            var username = payload.Value<string>("username");
            if (string.IsNullOrWhiteSpace(username))
            {
                await WriteResponse(stream, new AuthPayloads.GenericFail("Missing username"));
                return;
            }

            var joinerIp = ((IPEndPoint)joiner.Client.RemoteEndPoint).Address.ToString();
            TavernLogger.Msg($"Whitelist application from {username} at {joinerIp}");

            var alreadyPending = false;
            _manager.WhitelistRequests.Update(list =>
            {
                alreadyPending = list.Requests.Exists(r =>
                    string.Equals(r.Username, username, StringComparison.OrdinalIgnoreCase) && r.Ip == joinerIp);
                if (alreadyPending) return false;

                list.Requests.Add(new WhitelistRequestList.WhitelistRequest
                {
                    Username = username,
                    Ip = joinerIp,
                    AppliedAt = DateTime.UtcNow.ToString("o"),
                });
                return true;
            });

            await WriteResponse(stream, new AuthPayloads.WhitelistApplicationReceived(!alreadyPending));
        }
        catch (Exception e)
        {
            TavernLogger.Error($"Error managing whitelist application {e}");
            await WriteResponse(stream, new AuthPayloads.GenericFail("Error processing application"));
        }
    }

    private async Task WriteResponse(Stream stream, object response)
    {
        try
        {
            var serializedResponse = JsonConvert.SerializeObject(response);
            var encodedResponse = Encoding.UTF8.GetBytes(serializedResponse);

            TavernLogger.Msg($"Writing response to joining client: {serializedResponse}");

            await stream.WriteAsync(encodedResponse, 0, encodedResponse.Length);
        }
        catch (Exception e)
        {
            TavernLogger.Error($"Error when sending auth OK {e}");
            throw;
        }
    }
}