using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using MelonLoader.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;
using UnityEngine;

namespace TavernLib.Debugging
{
    internal class NLogCatcher
    {
        public MelonTarget Target { get; private set; }


        public NLogCatcher()
        {
            MelonEvents.OnApplicationLateStart.Subscribe(() =>
            {
                Target = new MelonTarget { Name = "Melon" };
                var melonRule = new LoggingRule("*", LogLevel.Trace, LogLevel.Fatal, Target);
            
                LogManager.Configuration.AddTarget(Target.Name, Target);
                LogManager.Configuration.LoggingRules.Insert(0, melonRule);
                LogManager.ReconfigExistingLoggers();

                LogManager.ConfigurationChanged += (sender, args) =>
                {
                    var newConfig = args.ActivatedConfiguration;
                    if (newConfig == null || newConfig.FindTargetByName("Melon") != null) return;

                    newConfig.AddTarget(Target.Name, Target);
                    newConfig.LoggingRules.Insert(0, melonRule);

                    LogManager.ReconfigExistingLoggers();
                };
            });
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
                        Tavern.Logger.Msg(ColorARGB.CornflowerBlue, msg);

                    if (logEvent.Level == LogLevel.Warn || logEvent.Level == LogLevel.Debug)
                        Tavern.Logger.Msg(ColorARGB.Yellow, msg);

                    if (logEvent.Level == LogLevel.Error || logEvent.Level == LogLevel.Fatal)
                        Tavern.Logger.Msg(ColorARGB.Red, msg);
                }
            }
        }
    }
}