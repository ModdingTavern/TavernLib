using System;
using System.IO;
using MelonLoader.Utils;

namespace TavernLib
{
    public static class TavernDirectories
    {
        public static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public static string ATTSave => Path.Combine(AppData, "A Township Tale");
        public static string ServerConfig => Path.Combine(MelonEnvironment.GameRootDirectory, "server-config.yaml");
    }
}