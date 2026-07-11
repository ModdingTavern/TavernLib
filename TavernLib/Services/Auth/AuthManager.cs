using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TavernLib.Services.Api;

namespace TavernLib.Services.Auth
{
    public class AuthManager : IAuthManager
    {
        private IApiManager _manager;
        private TcpListener _listener;


        public AuthManager(IApiManager manager)
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
                    catch (JsonReaderException) { }
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
                else if (jsonPayload.ContainsKey("username")) await WriteAuthOk(stream);
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
                    _manager.ActiveListing._config.Name,
                    false,
                    false,
                    _manager.ActiveListing._config.Game);

                var serializedResponse = JsonConvert.SerializeObject(response);
                var encodedResponse = Encoding.UTF8.GetBytes(serializedResponse);

                await stream.WriteAsync(encodedResponse, 0, encodedResponse.Length);
            }
            catch (Exception e)
            {
                Tavern.Logger.Error($"Error when sending pong response {e}");
            }
        }

        private async Task WriteAuthOk(Stream stream)
        {
            try
            {
                var response = new AuthPayloads.AuthenticateOk();
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