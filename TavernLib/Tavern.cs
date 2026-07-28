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


[assembly: MelonInfo(typeof(TavernLib.Tavern), "TavernLib", "v1.4.2", "Tavern Team", "https://github.com/ModdingTavern/TavernLib")]
namespace TavernLib;

public class Tavern : MelonPlugin
{
    internal static MelonLogger.Instance Logger { get; private set; }
    public const string Version = "v1.4.2";


    public override void OnEarlyInitializeMelon()
    {
        Logger = LoggerInstance;
        SetupServices();
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