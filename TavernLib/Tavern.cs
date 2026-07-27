using System;
using System.IO;
using System.Reflection;
using Alta.Console.Commands;
using MelonLoader;
using MonoMod.RuntimeDetour;
using TavernLib.Backend.Api;
using TavernLib.Debugging;
using TavernLib.Patches;
using TavernLib.Services;


[assembly: MelonInfo(typeof(TavernLib.Tavern), "TavernLib", "v1.4.0", "Tavern Team", "https://github.com/ModdingTavern/TavernLib")]
namespace TavernLib;

public class Tavern : MelonPlugin
{
    internal static MelonLogger.Instance Logger { get; private set; }
    public const string Version = "v1.4.0";


    public override void OnEarlyInitializeMelon()
    {
        Logger = LoggerInstance;

        SetupServices();

        var consolePath = Path.Combine(TavernDirectories.ModdingTavern, "console_token.txt");
        if (File.Exists(consolePath))
        {
            if (File.ReadAllText(consolePath) == "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJVc2VySWQiOiIwIiwiVXNlcm5hbWUiOiJTZXJ2ZXIiLCJyb2xlIjoiQWNjZXNzIiwiaXNfdmVyaWZpZWQiOiJUcnVlIiwiaXNfbWVtYmVyIjoiVHJ1ZSIsIlBvbGljeSI6WyJvZmZsaW5lIiwicGxheV9vZmZsaW5lIiwic2VydmVyX2FjY2Vzc19wcmVfYWxwaGEiLCJnYW1lX2FjY2Vzc19wdWJsaWMiLCJzZXJ2ZXJfb3duZXIiLCJkZWJ1Z19mZWF0dXJlcyIsImRhdGFiYXNlX2FkbWluIiwicmV1c2VfcmVmcmVzaF90b2tlbnMiXSwiZXhwIjo5OTk5OTk5OTk5LCJpc3MiOiJBbHRhV2ViQVBJIiwiYXVkIjoiQWx0YUNsaWVudCJ9.wLKduc-OVFM0jgi_aeHwzazy70AO8KXyT5-YVkpPm4g")
            {
                File.Delete(consolePath); // Regenerate unsigned tokens
            }
        }
    }

    public override void OnInitializeMelon()
    {
        Alta.Console.CommandService.CommandCollection.Collect(Assembly.GetExecutingAssembly());
    }

    public override void OnLateInitializeMelon()
    {
        SetupSelectionFixPatch();
    }


    private void SetupSelectionFixPatch()
    {
        var findObjectsMethod = typeof(SelectionCommandModule).GetMethod(nameof(SelectionCommandModule.FindObjects), BindingFlags.Static | BindingFlags.NonPublic);
        var selectionFixMethod = typeof(SelectFixPatch).GetMethod(nameof(SelectFixPatch.SelectionFix), BindingFlags.Static | BindingFlags.Public);
        
        _ = new Hook(findObjectsMethod, selectionFixMethod);
    }
    
    private void SetupServices()
    {
        try
        {
            if (CommandLineArguments.Contains("/debug_helper")) TavernServices.AddService(new DebugHelper());

            if (CommandLineArguments.Contains(CommandLineArguments.StartServerArgument))
            {
                TavernLogger.Msg("Booting TavernLib in server mode");
                if (!CommandLineArguments.Contains(TavernArgs.DontManageAuth))
                    TavernLib.Patches.TeenyPatches.EnsureConsoleToken();
                TavernServices.AddService(new TavernApiManager());
            }

        }
        catch (Exception e)
        {
            Logger.BigError($"Error when setting up base TavernLib services!!!!! {e}");
            throw;
        }
    }
}