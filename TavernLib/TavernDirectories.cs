using System.IO;
using MelonLoader.Utils;

namespace TavernLib
{
    public static class TavernDirectories
    {
        public static string ServerPath => Path.Combine(MelonEnvironment.GameRootDirectory, "Custom Servers");
    }
}