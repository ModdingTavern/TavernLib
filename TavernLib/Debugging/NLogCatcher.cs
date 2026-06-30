using System.Collections.Generic;
using System.Linq;
using MelonLoader.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;
using UnityEngine;

namespace TavernLib.Debugging
{
    internal class NLogCatcher
    {
        private readonly MelonTarget _melonTarget;
        
        
        public NLogCatcher()
        {
            _melonTarget = new MelonTarget { Name = "Melon" };
            var melonRule = new LoggingRule("*", LogLevel.Trace, LogLevel.Fatal, _melonTarget);
            
            LogManager.Configuration.AddTarget(_melonTarget.Name, _melonTarget);
            LogManager.Configuration.LoggingRules.Insert(0, melonRule);
            LogManager.ReconfigExistingLoggers();

            LogManager.ConfigurationChanged += (sender, args) =>
            {
                var newConfig = args.ActivatedConfiguration;
                if (newConfig == null || newConfig.FindTargetByName("Melon") != null) return;

                newConfig.AddTarget(_melonTarget.Name, _melonTarget);
                newConfig.LoggingRules.Insert(0, melonRule);

                LogManager.ReconfigExistingLoggers();
            };
        }

        public void OnGui()
        {
            for (int i = 0; i < _melonTarget.LoggingLevels.Count; i++)
            {
                var pair = _melonTarget.LoggingLevels.ElementAt(i);
                _melonTarget.LoggingLevels[pair.Key] = GUILayout.Toggle(pair.Value, pair.Key);
            }
        }


        public class MelonTarget : TargetWithLayout
        {
            public Dictionary<string, bool> LoggingLevels { get; } = new()
            {
                { "Info", true },
                { "Trace", true },
                { "Debug", true },
                { "Warn", true },
                { "Error", true },
                { "Fatal", true }
            };
            

            protected override void Write(LogEventInfo logEvent)
            {
                string msg = $"[{logEvent.Level}] {Layout.Render(logEvent)}";

                if (LoggingLevels.TryGetValue(logEvent.Level.Name, out bool levelIsEnabled))
                {
                    if (!levelIsEnabled) return;
                    
                    if (logEvent.Level == LogLevel.Trace || logEvent.Level == LogLevel.Info)
                        TavernCore.Logger.Msg(ColorARGB.CornflowerBlue, msg);

                    if (logEvent.Level == LogLevel.Warn || logEvent.Level == LogLevel.Debug)
                        TavernCore.Logger.Msg(ColorARGB.Yellow, msg);

                    if (logEvent.Level == LogLevel.Error || logEvent.Level == LogLevel.Fatal)
                        TavernCore.Logger.Msg(ColorARGB.Red, msg);
                }
            }
        }
    }
}