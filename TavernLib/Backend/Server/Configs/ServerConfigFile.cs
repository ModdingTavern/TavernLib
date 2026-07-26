using System;
using System.IO;
using Newtonsoft.Json;

namespace TavernLib.Backend.Server.Configs
{
    public abstract class ServerConfigFile<T>(string filePath) where T : class, new()
    {
        private string FilePath { get; set; } = filePath;
        public T LastRead { get; private set; } = new();

        
        public virtual void ReadFromFile()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    LastRead = new T();
                    
                    using var stream = File.CreateText(FilePath);
                    stream.WriteAsync(JsonConvert.SerializeObject(LastRead, Formatting.Indented));
                    
                    return;
                }

                var config = File.ReadAllText(FilePath);
                var result = JsonConvert.DeserializeObject<T>(config);
                LastRead = result;
            }
            catch (Exception e)
            {
                TavernLogger.Error($"Error when managing file responsible for type {nameof(T)}! {e}");
                throw;
            }
        }
        
        public virtual void WriteToFile()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    LastRead ??= new T();
                    
                    using var stream = File.CreateText(FilePath);
                    stream.WriteAsync(JsonConvert.SerializeObject(LastRead, Formatting.Indented));
                    
                    return;
                }

                File.WriteAllText(FilePath, JsonConvert.SerializeObject(LastRead, Formatting.Indented));
            }
            catch (Exception e)
            {
                TavernLogger.Error($"Error when managing file responsible for type {nameof(T)}! {e}");
                throw;
            }
        }
    }
}