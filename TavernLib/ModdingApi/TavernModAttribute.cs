using System;
using System.Reflection;
using MelonLoader;

namespace TavernLib.ModdingApi;

public enum ModSide
{
    None,
    Server,
    Client,
    Both,
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class TavernModAttribute : Attribute
{
    public ModSide Side { get; }
    public TavernModAttribute(ModSide side)
    {
        Side = side;
    }
}

public static class TavernMelonBaseExtension{

    public static ModSide GetModSide(this MelonBase melonMod)
    {
        TavernModAttribute modInfo = melonMod.GetType().GetCustomAttribute<TavernModAttribute>();
        return modInfo?.Side ?? ModSide.None;
    }
}
