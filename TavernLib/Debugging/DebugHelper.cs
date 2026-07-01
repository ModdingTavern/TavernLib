using System.Linq;
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
        public void OnGui()
        {
            _name = GUILayout.TextField(_name);
            _description = GUILayout.TextField(_description);
            _ipAddress = GUILayout.TextField(_ipAddress);
            
            if (GUILayout.Button("Serialize"))
            {
                var serverInfo = DevGameServerInfo.GetDevServer(_ipAddress, 1757, 0);
                serverInfo.Description = _description;
                serverInfo.Name = _name;

                new CustomServerReference(_name, false).Serialize(serverInfo);
            }
            
            
            for (int i = 0; i < _logCatcher.Target.LoggingLevels.Count; i++)
            {
                var pair = _logCatcher.Target.LoggingLevels.ElementAt(i);
                _logCatcher.Target.LoggingLevels[pair.Key] = GUILayout.Toggle(pair.Value, pair.Key);
            }
        }
    }
}