using System;
using System.Collections.Generic;
using MelonLoader.Logging;

namespace TavernLib.Services
{
    public static class TavernServices
    {
        private static readonly Dictionary<Type, IService> ServiceEntries = new();


        public static void AddService<T>(T instance) where T : IService
        {
            if (ServiceEntries.ContainsKey(typeof(T)))
            {
                TavernLogger.Warn("Cannot add multiple services of the same type!");
                return;
            }

            ServiceEntries[typeof(T)] = instance;
        }
        
        public static T GetService<T>() where T : class, IService
        {
            if (ServiceEntries.TryGetValue(typeof(T), out var result)) return result as T;
            
            TavernLogger.Error($"Service of type {nameof(T)} was not found!");
            return null;
        }
    }
}