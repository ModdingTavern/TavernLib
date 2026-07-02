using System;
using System.Collections.Generic;
using System.IO;

namespace TavernLib.ServerBrowser
{
    public class ServerMounter
    {
        public List<CustomServerReference> ServerReferences { get; } = new();

        internal ServerMounter()
        {
            if (!Directory.Exists(TavernDirectories.ServerPath)) Directory.CreateDirectory(TavernDirectories.ServerPath);
        }
        
        
        public void LoadAllReferences()
        {
            var newReferenceList = new List<CustomServerReference>();
            try
            {
                var directoryInfo = new DirectoryInfo(TavernDirectories.ServerPath);

                foreach (var fileInfo in directoryInfo.GetFiles())
                {
                    var serverReference = new CustomServerReference(fileInfo.Name, true);
                    newReferenceList.Add(serverReference);
                }
            }

            catch (Exception e)
            {
                Tavern.Logger.Error($"Problem when loading server references :( {e}");
                throw;
            }

            ServerReferences.Clear();
            ServerReferences.AddRange(newReferenceList);
        }
    }
}