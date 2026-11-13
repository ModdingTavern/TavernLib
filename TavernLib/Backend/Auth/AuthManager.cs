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
    private const int Port = 1762;
    private const int AccountsPerAddress = 5;
    private const int MaxTokenLength = 128;
    private const int ApplicationsPerAddress = 3;
    private const int ApplicationsMax = 100;
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);

    private readonly TavernManager _manager;
    private readonly AuthLimits _limits = new();
    private TcpListener _listener;
    private volatile bool _stopping;


    public AuthManager(TavernManager manager)
    {
        _manager = manager;

        _ = StartAuthCycle();
    }


    private async Task StartAuthCycle()
    {
        TavernLogger.Msg("Starting auth cycle");

        try
        {
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start();
        }
        catch (Exception e)
        {
            TavernLogger.Error($"Could not listen for joins on port {Port}, so nobody can join this server: {e.Message}. Is another server or TavernLauncher using it?");
            return;
        }

        UnityEngine.Application.quitting += StopListening;

        TavernLogger.Msg("Starting auth listening cycle");
        while (!_stopping)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync();
            }
            catch (Exception) when (_stopping)
            {
                break;
            }
            catch (Exception e)
            {
                TavernLogger.Error($"Could not accept a connection on port {Port}: {e.Message}");
                await Task.Delay(1000);
                continue;
            }

            _ = InterpretTcpStream(client);
        }
    }

    private void StopListening()
    {
        _stopping = true;
        _listener.Stop();
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

        throw new InvalidDataException("Invalid or incomplete payload received.");
    }

    private async Task InterpretTcpStream(TcpClient client)
    {
        string address = null;
        var opened = false;
        using var timeout = new CancellationTokenSource();

        try
        {
            address = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString();
            if (address == null || !_limits.TryOpen(address)) return;
            opened = true;

            TavernLogger.Msg($"Connected to TcpClient ({address}), interpreting stream");

            // Mono's ReadAsync doesn't stop for the token, so the timeout closes the socket instead.
            timeout.Token.Register(client.Close);
            timeout.CancelAfter(ConnectionTimeout);

            using var stream = client.GetStream();
            var payload = await ReadFullPayload(stream, timeout.Token);

            // Read first, then refuse: closing with the request unread would reset the connection
            // and lose the answer.
            if (_limits.HeldOff(address))
            {
                await WriteResponse(stream, new AuthPayloads.GenericFail("Too many attempts."));
                return;
            }

            TavernLogger.Msg("Finding payload type");

            var jsonPayload = JObject.Parse(payload);

            if (jsonPayload.ContainsKey("ping")) await WritePongResponse(stream);
            else if (jsonPayload.ContainsKey("register_whitelist_application")) await ManageWhitelistApplicationRequest(stream, jsonPayload, address);
            else if (jsonPayload.ContainsKey("username")) await ManageAuthRequest(stream, jsonPayload, address);
            else
            {
                TavernLogger.Warn($"Unknown payload sent to AuthManager, keys: {string.Join(", ", jsonPayload.Properties().Select(p => p.Name))}");
            }
        }
        catch (Exception) when (timeout.IsCancellationRequested)
        {
            _limits.Fail(address);
            TavernLogger.Msg($"Connection from {address} timed out");
        }
        catch (InvalidDataException e)
        {
            _limits.Fail(address);
            TavernLogger.Warn($"Connection from {address}: {e.Message}");
        }
        catch (Exception e)
        {
            TavernLogger.Error($"Error decoding TCP stream {e}");
        }

        finally
        {
            if (opened)
            {
                _limits.Close(address);
                TavernLogger.Msg("Closing connection to joining user");
            }

            client.Close();
        }
    }

    // Every request reads server_settings.json afresh, so a new name or password applies with no
    // restart. Null when it can't be read.
    private ServerSettings ReadSettings()
    {
        try
        {
            return _manager.ServerConfig.Read();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task WritePongResponse(Stream stream)
    {
        var settings = ReadSettings() ?? _manager.ServerConfig.LastRead;
        var response = new AuthPayloads.PingResponse(
            settings.Name,
            !string.IsNullOrWhiteSpace(settings.PasswordHash),
            JoinRules.WhitelistOn(settings, _manager.UserConfig.LastRead),
            1757); // TODO

        await WriteResponse(stream, response);
    }


    private async Task ManageAuthRequest(Stream stream, JObject payload, string address)
    {
        var typedPayload = JsonConvert.DeserializeObject<AuthPayloads.AuthenticateRequest>(payload.ToString());
        if (string.IsNullOrWhiteSpace(typedPayload.Token) || string.IsNullOrWhiteSpace(typedPayload.Username))
        {
            await WriteResponse(stream, new AuthPayloads.GenericFail("Malformed authentication data"));
            return;
        }

        TavernLogger.Msg($"User {typedPayload.Username} at {address} joining server");

        await WriteResponse(stream, ResolveJoin(typedPayload, address));
    }

    // The checks and the registration run under users.json's lock on a fresh read, so two joins at
    // once can't mint the same id or drop each other's entry. A file that can't be read refuses
    // the join rather than reading as empty lists.
    private object ResolveJoin(AuthPayloads.AuthenticateRequest payload, string ip)
    {
        var settings = ReadSettings();
        if (settings == null) return new AuthPayloads.GenericFail("The server can't read its settings right now");

        object response = null;
        try
        {
            _manager.UserConfig.Update(config =>
            {
                response = CheckIfPermitted(config, settings, ip, payload);
                if (response != null) return false;

                return Register(config, settings, payload, ip, out response);
            });
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            return new AuthPayloads.GenericFail("The server can't read its user list right now");
        }

        return response;
    }

    // Returns true when users.json changed and must be written.
    private bool Register(UserConfig config, ServerSettings settings, AuthPayloads.AuthenticateRequest payload, string ip, out object response)
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
                _limits.Fail(ip);
                TavernLogger.Msg($"Joining user at IP {ip} tried to take username");
                response = new AuthPayloads.GenericFail("Name taken by someone else, or you lost the token to your account!");
                return false;
            }
        }
        else
        {
            // Only new names: an account that already exists keeps working whatever its name.
            if (!JoinRules.IsValidName(payload.Username))
            {
                response = new AuthPayloads.GenericFail($"Usernames can only have letters, numbers, spaces, hyphens and underscores, up to {JoinRules.UsernameMaxLength} characters.");
                return false;
            }

            if (payload.Token.Length > MaxTokenLength)
            {
                response = new AuthPayloads.GenericFail("Invalid token.");
                return false;
            }

            // Counted for new names only, so the accounts already there keep joining.
            if (settings.EnforceIpLimit && config.Users.Values.Count(user => user?.RegisteredFrom == ip) >= AccountsPerAddress)
            {
                _limits.Fail(ip);
                TavernLogger.Msg($"Joining user at IP {ip} refused: {AccountsPerAddress} accounts are registered from it already");
                response = new AuthPayloads.GenericFail("Too many accounts registered from your address.");
                return false;
            }

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

        response = new AuthPayloads.AuthenticateOk(userData.UserId, settings.QuestScene, settings.TutorialScene);
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

    // Returns the refusal to send, or null when the joiner may continue. The order is the
    // launcher's: blacklist, password, whitelist.
    private object CheckIfPermitted(UserConfig config, ServerSettings settings, string joinerIp, AuthPayloads.AuthenticateRequest payload)
    {
        TavernLogger.Msg($"Checking if joining user {joinerIp} can join");

        config.Users.TryGetValue(payload.Username.ToLowerInvariant(), out var existing);
        var userId = existing?.UserId;

        // Blacklist
        if (JoinRules.IsBlacklisted(config, payload.Username, userId, joinerIp))
        {
            TavernLogger.Msg($"Joining user at {joinerIp} was on the blacklist");
            return new AuthPayloads.GenericFail("Blacklisted");
        }

        // Password
        if (!string.IsNullOrWhiteSpace(settings.PasswordHash))
        {
            if (string.IsNullOrWhiteSpace(payload.Password))
            {
                TavernLogger.Msg($"Joining user at {joinerIp} gave no password to server");
                return new AuthPayloads.NeedsPassword();
            }

            if (_limits.PasswordHeldOff(joinerIp))
                return new AuthPayloads.GenericFail("Too many failed password attempts. Try again later.");

            // Hash the user provided password again to match Tavern Launcher's password setup
            if (BackendUtils.HashDigest(payload.Password) != settings.PasswordHash)
            {
                _limits.PasswordFail(joinerIp);
                TavernLogger.Msg($"Joining user at {joinerIp} gave the wrong password");
                return new AuthPayloads.WrongPassword();
            }
        }

        // Whitelist
        if (JoinRules.WhitelistOn(settings, config) &&
            !JoinRules.IsWhitelisted(config, payload.Username, userId, joinerIp, existing?.RegisteredFrom ?? joinerIp))
        {
            TavernLogger.Msg($"Joining user at {joinerIp} was not on the whitelist");
            return new AuthPayloads.NotWhitelisted();
        }

        TavernLogger.Msg($"Joining user at {joinerIp} passed all authentication checks");
        return null;
    }

    private async Task ManageWhitelistApplicationRequest(Stream stream, JObject payload, string joinerIp)
    {
        if (CommandLineArguments.Contains(TavernArgs.DontManageAuth))
        {
            TavernLogger.Msg("Ignoring whitelist application -- TavernLauncher is managing auth for this instance.");
            return;
        }

        try
        {
            var username = payload.Value<string>("username")?.Trim();
            var refusal = ApplicationRefusal(username, joinerIp);
            var added = false;

            if (refusal == null)
            {
                _manager.WhitelistRequests.Update(list =>
                {
                    list.Requests ??= new();
                    if (list.Requests.Exists(r => string.Equals(r.Username, username, StringComparison.OrdinalIgnoreCase) && r.Ip == joinerIp))
                        return false;

                    if (list.Requests.Count(r => r.Ip == joinerIp) >= ApplicationsPerAddress)
                        refusal = "You already have the most applications this server takes from one address.";
                    else if (list.Requests.Count >= ApplicationsMax)
                        refusal = "This server has too many applications waiting. Try again later.";
                    if (refusal != null) return false;

                    list.Requests.Add(new WhitelistRequestList.WhitelistRequest
                    {
                        Username = username,
                        Ip = joinerIp,
                        AppliedAt = DateTime.UtcNow.ToString("o"),
                    });
                    added = true;
                    return true;
                });
            }

            if (refusal != null)
            {
                _limits.Fail(joinerIp);
                TavernLogger.Msg($"Whitelist application from {joinerIp} refused: {refusal}");
                await WriteResponse(stream, new AuthPayloads.GenericFail(refusal));
                return;
            }

            if (added) TavernLogger.Msg($"Whitelist application from {username} at {joinerIp}");
            await WriteResponse(stream, new AuthPayloads.WhitelistApplicationReceived(added));
        }
        catch (Exception e)
        {
            TavernLogger.Error($"Error managing whitelist application {e}");
            await WriteResponse(stream, new AuthPayloads.GenericFail("Error processing application"));
        }
    }

    // The launcher's checks, in its order. Null when the application may be filed.
    private string ApplicationRefusal(string username, string ip)
    {
        if (string.IsNullOrWhiteSpace(username)) return "Missing username";
        if (!JoinRules.IsValidName(username)) return "That username can't join this server.";

        var settings = ReadSettings();
        if (settings == null) return "The server can't read its settings right now";

        var config = _manager.UserConfig.Read();
        if (JoinRules.IsBlacklisted(config, username, null, ip)) return "You are not permitted.";
        if (!JoinRules.WhitelistOn(settings, config)) return "This server has no whitelist. You can join without applying.";

        return null;
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
