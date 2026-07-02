using System;
using System.IO;
using System.Text;
using Alta.Api.DataTransferModels.Models.Responses;

namespace TavernLib.ServerBrowser
{
    public class CustomServerReference
    {
        public DevGameServerInfo ServerInfo { get; private set; }
        private readonly string _fileName;
        
        
        public CustomServerReference(string fileName, bool exists)
        {
            _fileName = fileName;
            if (exists) Deserialize();
        }
        
        
        public void Serialize(DevGameServerInfo instance)
        {
            try
            {
                using var stream = File.OpenWrite(Path.Combine(TavernDirectories.ServerPath, _fileName));
                using var writer = new BinaryWriter(stream, Encoding.UTF8, false);
                
                writer.Write(instance.Name);
                writer.Write(instance.Description);
                writer.Write(instance.ConnectionInfo.Address.ToString());
            }
            catch (Exception e)
            {
                Tavern.Logger.Error($"Problem with serializing CustomServerReference! {e}");
                throw;
            }
        }

        private void Deserialize()
        {
            try
            {
                using var stream = File.OpenRead(Path.Combine(TavernDirectories.ServerPath, _fileName));
                using var reader = new BinaryReader(stream, Encoding.UTF8, false);
                
                var name = reader.ReadString();
                var description = reader.ReadString();
                var ipAddress = reader.ReadString();

                ServerInfo = DevGameServerInfo.GetDevServer(ipAddress, 1757, 0);
                ServerInfo.Description = description;
                ServerInfo.Name = name;
                ServerInfo.OnlinePlayers = Array.Empty<UserInfo>();
            }
            catch (Exception e)
            {
                Tavern.Logger.Error($"Problem with deserializing CustomServerReference! {e}");
                throw;
            }
        }
    }
}