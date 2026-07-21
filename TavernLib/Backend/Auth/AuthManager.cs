using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TavernLib.Backend.Api;

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
            _listener.Start();

            var stopOnQuit = new CancellationTokenSource();
            UnityEngine.Application.quitting += stopOnQuit.Cancel;

            while (!stopOnQuit.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                _ = InterpretTcpStream(client);
            }
        }

        private async Task<string> ReadFullPayload(Stream stream, CancellationToken token)
        {
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
            using var stream = client.GetStream();
            using var timeout = new CancellationTokenSource();
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            var payload = await ReadFullPayload(stream, timeout.Token);

            try
            {
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
                client.Close();
            }
        }

        private async Task WritePongResponse(Stream stream)
        {
            try
            {
                var response = new AuthPayloads.PingResponse(
                    _manager.ServerConfig.LastRead.Name,
                    !string.IsNullOrWhiteSpace(_manager.ServerConfig.LastRead.PasswordHash),
                    _manager.UserConfig.LastRead.Whitelist.Usernames.Count > 0 || _manager.UserConfig.LastRead.Whitelist.Ips.Count > 0,
                    1757); // TODO

                var serializedResponse = JsonConvert.SerializeObject(response);
                var encodedResponse = Encoding.UTF8.GetBytes(serializedResponse);

                await stream.WriteAsync(encodedResponse, 0, encodedResponse.Length);
            }
            catch (Exception e)
            {
                Tavern.Logger.Error($"Error when sending pong response {e}");
            }
        }


        private async Task ManageAuthRequest(Stream stream, JObject payload, TcpClient joiner)
        {
            try
            {
                var typedPayload = JsonConvert.DeserializeObject<AuthPayloads.AuthenticateRequest>(payload.ToString());
                var joinerIp = ((IPEndPoint)joiner.Client.RemoteEndPoint).Address.ToString();

                // Check if user is on the blacklist or whitelist, and if they have the right password
                if (!await CheckIfPermitted(joinerIp, typedPayload, stream)) return;
                // User has passed all authentication checks, set them up for joining
                
                await WriteResponse(stream, new AuthPayloads.AuthenticateOk());
            }
            catch (Exception e)
            {
                Tavern.Logger.Error($"Error when managing join request {e}");
                throw;
            }
        }

        private async Task<bool> CheckIfPermitted(string joinerIp, AuthPayloads.AuthenticateRequest payload, Stream stream)
        {
            // Whitelist
            if (_manager.UserConfig.LastRead.Whitelist.Ips.Count > 0 || _manager.UserConfig.LastRead.Whitelist.Usernames.Count > 0)
            {
                var ipAllowed = _manager.UserConfig.LastRead.Whitelist.Ips.Contains(joinerIp);
                var nameAllowed = _manager.UserConfig.LastRead.Whitelist.Usernames.Contains(payload.Username);
                
                if (!ipAllowed && !nameAllowed)
                {
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
                    await WriteResponse(stream, new AuthPayloads.GenericFail("Blacklisted"));
                    return false;
                }
            }
            
            //Password
            if (!string.IsNullOrWhiteSpace(_manager.ServerConfig.LastRead.PasswordHash))
            {
                if (string.IsNullOrWhiteSpace(payload.Password))
                {
                    await WriteResponse(stream, new AuthPayloads.NeedsPassword());
                    return false;
                }
                    
                // Hash the user provided password again to match Tavern Launcher's password setup
                if (BackendUtils.HashDigest(payload.Password) != _manager.ServerConfig.LastRead.PasswordHash)
                {
                    await WriteResponse(stream, new AuthPayloads.WrongPassword());
                    return false;
                }
            }

            return true;
        }
        
        private async Task WriteResponse(Stream stream, object response)
        {
            try
            {
                var serializedResponse = JsonConvert.SerializeObject(response);
                var encodedResponse = Encoding.UTF8.GetBytes(serializedResponse);

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