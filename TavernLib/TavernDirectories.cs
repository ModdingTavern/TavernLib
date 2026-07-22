using System;
using System.IO;

namespace TavernLib
{
    public static class TavernDirectories
    {
        public static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public static string ATTSave => Path.Combine(AppData, "A Township Tale");
        
        public static string ModdingTavern => Path.Combine(AppData, "TheModdingTavern");
        public static string Blacklist => Path.Combine(ModdingTavern, "blacklist.json");
        public static string ServerSettings => Path.Combine(ModdingTavern, "server_settings.json");
        public static string Users => Path.Combine(ModdingTavern, "users.json");
        public static string Whitelist => Path.Combine(ModdingTavern, "whitelist.json");
        public static string TavernServer => Path.Combine(ModdingTavern, "tavern_server.json");
    }
}