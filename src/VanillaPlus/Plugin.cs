using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace VanillaPlus;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BasePlugin
{
    public const string Guid = "vanillaplus.davethediver";
    public const string Name = "VanillaPlus";
    public const string Version = "0.1.0";

    // BepInEx prefixes every line with the source name, so all output reads "[Info   :VanillaPlus] ...".
    internal static ManualLogSource Logger;
    internal static Harmony Harmony;

    public override void Load()
    {
        Logger = Log;
        ModConfig.Bind(Config);
        Harmony = new Harmony(Guid);

        Logger.LogInfo($"{Name} {Version} loading");
        Logger.LogInfo($"Game version: {Safe(() => Application.version)} (Unity {Safe(() => Application.unityVersion)})");

        LogFeature("Clarity", ModConfig.ClarityEnabled.Value, null);
        LogFeature("FPS unlock", ModConfig.FpsEnabled.Value && ModConfig.TargetFPS.Value > 0,
            $"TargetFPS={ModConfig.TargetFPS.Value}");
        LogFeature("Harpoon struggle", ModConfig.StruggleEnabled.Value,
            $"StruggleMultiplier={ModConfig.StruggleMultiplier.Value:0.##}");
        LogFeature("Perfect pour tip", ModConfig.PerfectPourTipEnabled.Value,
            $"PerfectPourTipMultiplier={ModConfig.PerfectPourTipMultiplier.Value:0.##}");

        // Feature patch classes live in Features/ and are applied here, one PatchAll per enabled feature.
        // None are implemented yet: see README "Status".
    }

    private static void LogFeature(string feature, bool on, string detail)
    {
        Logger.LogInfo($"Feature {feature}: {(on ? "ON" : "OFF")}{(detail == null ? "" : $" ({detail})")}");
    }

    private static string Safe(Func<string> read)
    {
        try { return read(); }
        catch (Exception e) { return $"unknown ({e.GetType().Name})"; }
    }
}
