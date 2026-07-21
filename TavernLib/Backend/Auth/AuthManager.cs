using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MelonLoader.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TavernLib.Backend.Api;
using TavernLib.Backend.Server.Configs;

namespace TavernLib.Backend.Auth
{
    internal class AuthManager
    {
        private TavernApiManager _manager;
        private TcpListener _listener;


        public AuthManager(TavernApiManager manager)
        {
            _manager = manager;
            _listener = new(IPAddress.Any, 1762);

            _ = StartAuthCycle();
        }


        private async Task StartAuthCycle()
        {
            Tavern.Logger.Msg(ColorARGB.Chartreuse, "Starting auth cycle");
            
            _listener.Start();

            var stopOnQuit = new CancellationTokenSource();
            UnityEngine.Application.quitting += stopOnQuit.Cancel;

            Tavern.Logger.Msg(ColorARGB.Chartreuse, "Starting auth listening cycle");
            while (!stopOnQuit.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                _ = InterpretTcpStream(client);
            }
        }

        private async Task<string> ReadFullPayload(Stream stream, CancellationToken token)
        {
            Tavern.Logger.Msg(ColorARGB.Chartreuse, "Reading payload");
            
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
            Tavern.Logger.Msg(ColorARGB.Chartreuse, $"Connected to TcpClient ({(client.Client.RemoteEndPoint as IPEndPoint)?.Address}), interpreting stream");
            
            using var stream = client.GetStream();
            using var timeout = new CancellationTokenSource();
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            var payload = await ReadFullPayload(stream, timeout.Token);

            try
            {
                Tavern.Logger.Msg(ColorARGB.Chartreuse, "Finding payload type");
                
                var jsonPayload = JObject.Parse(payload);

                if (jsonPayload.ContainsKey("ping")) await WritePongResponse(stream);
                else if (jsonPayload.ContainsKey("username")) await ManageAuthRequest(stream, jsonPayload, client);
                else
                {
                    Tavern.Logger.Warning($"Unknown payload sent to AuthManager {jsonPayload}");
                }
            }
            catch (Exception e)
            {
                Tavern.Logger.Error($"Error decoding TCP stream {e}");
            }

            finally
            {
                Tavern.Logger.Msg(ColorARGB.Chartreuse, "Closing connection to joining user");
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
                _manager.UserConfig.ReadFromFile(); // Read to make sure validation can be up-to date
                
                var typedPayload = JsonConvert.DeserializeObject<AuthPayloads.AuthenticateRequest>(payload.ToString());
                var joinerIp = ((IPEndPoint)joiner.Client.RemoteEndPoint).Address.ToString();
                
                Tavern.Logger.Msg(ColorARGB.Chartreuse, $"User at {joinerIp} joining server");
                Tavern.Logger.Msg(ColorARGB.Chartreuse, $"With payload {payload}");
                
                if (!await CheckIfPermitted(joinerIp, typedPayload, stream)) return;

                await PostPermissionCheck(stream, typedPayload, joinerIp);
            }
            catch (Exception e)
            {
                Tavern.Logger.Error($"Error when managing join request {e}");
                throw;
            }
        }

        private async Task PostPermissionCheck(Stream stream, AuthPayloads.AuthenticateRequest payload, string ip)
        {
            if (_manager.UserConfig.LastRead.Users.TryGetValue(payload.Username.ToLower(), out var userData))
            {
                if (string.IsNullOrWhiteSpace(userData.Token)) userData.Token = payload.Token;
                else if (payload.Token != userData.Token)
                {
                    Tavern.Logger.Msg(ColorARGB.Chartreuse, $"Joining user at IP {ip} tried to take username");
                    await WriteResponse(stream, new AuthPayloads.GenericFail("Name taken by someone else, or you lost the token to your account!"));
                    return;
                }
            }

            else
            {
                Tavern.Logger.Msg(ColorARGB.Chartreuse, $"Joining user at IP {ip} being allotted a slot in Users.json");
                _manager.UserConfig.LastRead.Users[payload.Username.ToLower()] = new UserConfig.User
                {
                    RegisteredFrom = ip,
                    Token = payload.Token,
                    UserId = 1000000000U + (ulong)_manager.UserConfig.LastRead.Users.Count
                };

                userData = _manager.UserConfig.LastRead.Users[payload.Username.ToLower()];
            }
            
            Tavern.Logger.Msg(ColorARGB.Chartreuse, "Writing any potential changes during join to file");
            _manager.UserConfig.WriteToFile();
            await WriteResponse(stream, new AuthPayloads.AuthenticateOk(userData.UserId));
        }
        
        private async Task<bool> CheckIfPermitted(string joinerIp, AuthPayloads.AuthenticateRequest payload, Stream stream)
        {
            Tavern.Logger.Msg(ColorARGB.Chartreuse, $"Checking if joining user {joinerIp} can join");
            // Whitelist
            if (_manager.UserConfig.LastRead.Whitelist.Ips.Count > 0 || _manager.UserConfig.LastRead.Whitelist.Usernames.Count > 0)
            {
                var ipAllowed = _manager.UserConfig.LastRead.Whitelist.Ips.Contains(joinerIp);
                var nameAllowed = _manager.UserConfig.LastRead.Whitelist.Usernames.Contains(payload.Username);
                
                if (!ipAllowed && !nameAllowed)
                {
                    Tavern.Logger.Msg(ColorARGB.Chartreuse, $"Joining user at {joinerIp} was not on the whitelist");
                    await WriteResponse(stream, new AuthPayloads.NotWhitelisted());
                    return false;
                } 
            }
                
            // Blacklist
            if (_manager.UserConfig.LastRead.Blacklist.Ips.Count > 0 || _manager.UserConfig.LastRead.Blacklist.Usernames.Count > 0)
            {
                var ipBlocked = _manager.UserConfig.LastRead.Blacklist.Ips.Contains(joinerIp);
                var nameBlocked = _manager.UserConfig.LastRead.Blacklist.Usernames.Contains(payload.Username);
                
                if (ipBlocked || nameBlocked)
                {
                    Tavern.Logger.Msg(ColorARGB.Chartreuse, $"Joining user at {joinerIp} was on the blacklist");
                    await WriteResponse(stream, new AuthPayloads.GenericFail("Blacklisted"));
                    return false;
                }
            }
            
            //Password
            if (!string.IsNullOrWhiteSpace(_manager.ServerConfig.LastRead.PasswordHash))
            {
                if (string.IsNullOrWhiteSpace(payload.Password))
                {
                    Tavern.Logger.Msg(ColorARGB.Chartreuse, $"Joining user at {joinerIp} gave no password to server");
                    await WriteResponse(stream, new AuthPayloads.NeedsPassword());
                    return false;
                }
                    
                // Hash the user provided password again to match Tavern Launcher's password setup
                if (BackendUtils.HashDigest(payload.Password) != _manager.ServerConfig.LastRead.PasswordHash)
                {
                    Tavern.Logger.Msg(ColorARGB.Chartreuse, $"Joining user at {joinerIp} gave the wrong password");
                    await WriteResponse(stream, new AuthPayloads.WrongPassword());
                    return false;
                }
            }

            Tavern.Logger.Msg(ColorARGB.Chartreuse, $"Joining user at {joinerIp} passed all authentication checks");
            return true;
        }
        
        private async Task WriteResponse(Stream stream, object response)
        {
            try
            {
                var serializedResponse = JsonConvert.SerializeObject(response);
                var encodedResponse = Encoding.UTF8.GetBytes(serializedResponse);

                Tavern.Logger.Msg(ColorARGB.Chartreuse, $"Writing response to joining client: {serializedResponse}");
                
                await stream.WriteAsync(encodedResponse, 0, encodedResponse.Length);
            }
            catch (Exception e)
            {
                Tavern.Logger.Error($"Error when sending auth OK {e}");
                throw;
            }
        }
    }
}