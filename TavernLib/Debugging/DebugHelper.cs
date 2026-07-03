using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Alta.Api.DataTransferModels.Models.Responses;
using MelonLoader;
using TavernLib.ServerBrowser;
using UnityEngine;

namespace TavernLib.Debugging
{
    public class DebugHelper
    {
        private readonly NLogCatcher _logCatcher = new();

        
        internal DebugHelper()
        {
            MelonEvents.OnGUI.Subscribe(OnGui);
        }

        private string _name, _description, _ipAddress;
        private bool _automaticIpAddress;

        private void OnGui()
        {
            _name = GUILayout.TextField(_name);
            _description = GUILayout.TextField(_description);
            _automaticIpAddress = GUILayout.Toggle(_automaticIpAddress, "Automatically detect IP");
            if (!_automaticIpAddress) _ipAddress = GUILayout.TextField(_ipAddress);
            
            
            if (GUILayout.Button("Serialize"))
            {
                _ = SerializeServer();
            }
            
            
            for (int i = 0; i < _logCatcher.Target.LoggingLevels.Count; i++)
            {
                var pair = _logCatcher.Target.LoggingLevels.ElementAt(i);
                _logCatcher.Target.LoggingLevels[pair.Key] = GUILayout.Toggle(pair.Value, pair.Key);
            }
        }
        
        private async Task SerializeServer()
        {
            string ip;

            if (!_automaticIpAddress) ip = _ipAddress;
            else
            {
                using var httpClient = new HttpClient();
                var result = await httpClient.GetStringAsync("https://icanhazip.com");
                result = result.Replace("\\r\\n", "").Replace("\\n", "").Trim();
                ip = result;
            }
            
            var serverInfo = DevGameServerInfo.GetDevServer(ip, 1757, 0);
            serverInfo.Description = _description;
            serverInfo.Name = _name;

            new CustomServerReference(_name, false).Serialize(serverInfo);
        }
    }
}