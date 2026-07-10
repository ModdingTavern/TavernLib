using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TavernLib.Services.Api
{
    public class AuthManager : IAuthManager
    {
        private IApiManager _manager;
        private TcpListener _listener;
        
        
        public AuthManager(IApiManager manager)
        {
            _manager = manager;
            
            _listener = new(IPAddress.Any, 1762);

            _ = AuthLifetime();
        }
        
        
        private async Task AuthLifetime()
        {
            _listener.Start();

            while (true)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                _ = SetupAuthConfirmation(client);
            }
        }
        
        private async Task SetupAuthConfirmation(TcpClient client)
        {
            using (var stream = client.GetStream())
            {
                // TODO: Implement authentication for non-launcher ran servers
            }
        }


        private struct PingRequest
        {
            [JsonProperty(PropertyName = "ping")] public bool Ping { get; set; } 
        }
    }
}