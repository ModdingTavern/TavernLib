using System;
using System.IO;
using MelonLoader.Utils;

namespace TavernLib
{
    public static class TavernDirectories
    {
        public static string ServerPath => Path.Combine(MelonEnvironment.GameRootDirectory, "Custom Servers");
        public static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public static string ATTSave => Path.Combine(AppData, "A Township Tale");
    }
}