using System.Linq;
using MelonLoader;
using UnityEngine;

namespace TavernLib.Debugging
{
    public class DebugHelper
    {
        private NLogCatcher _logCatcher = new();

        
        internal DebugHelper()
        {
            MelonEvents.OnGUI.Subscribe(OnGui);
        }
        
        public void OnGui()
        {
            for (int i = 0; i < _logCatcher.Target.LoggingLevels.Count; i++)
            {
                var pair = _logCatcher.Target.LoggingLevels.ElementAt(i);
                _logCatcher.Target.LoggingLevels[pair.Key] = GUILayout.Toggle(pair.Value, pair.Key);
            }
        }
    }
}