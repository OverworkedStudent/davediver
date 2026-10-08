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
        SessionLog.Start(Log);
        ModConfig.Bind(Config);
        Harmony = new Harmony(Guid);

        Logger.LogInfo($"{Name} {Version} loading");
        Logger.LogInfo($"Game version: {Safe(() => Application.version)} (Unity {Safe(() => Application.unityVersion)})");

        LogFeature("Clarity", ModConfig.ClarityEnabled.Value, ModConfig.ClaritySummary());
        LogFeature("FPS unlock", ModConfig.FpsEnabled.Value && ModConfig.TargetFPS.Value > 0,
            $"TargetFPS={ModConfig.TargetFPS.Value}");
        LogFeature("Harpoon struggle", ModConfig.StruggleEnabled.Value,
            $"StruggleMultiplier={ModConfig.StruggleMultiplier.Value:0.##}");
        LogFeature("Perfect pour tip", ModConfig.PerfectPourTipEnabled.Value,
            $"PerfectPourTipMultiplier={ModConfig.PerfectPourTipMultiplier.Value:0.##}");
        LogFeature("Auto pickup", ModConfig.AutoPickupEnabled.Value,
            $"items={ModConfig.AutoPickupItems.Value}, ammo={ModConfig.AutoPickupAmmoBox.Value}, fish={ModConfig.AutoPickupFish.Value}, " +
            $"chests={ModConfig.AutoOpenChests.Value}, oxygen={ModConfig.AutoPickupOxygenBox.Value}, radius={ModConfig.PickupRadius.Value:0.##}");
        LogFeature("Gentler stealth", ModConfig.StealthEnabled.Value,
            $"DetectionSpeedMultiplier={ModConfig.StealthDetectionMultiplier.Value:0.##}");
        LogFeature("Village speed", ModConfig.VillageSpeedEnabled.Value,
            $"SpeedMultiplier={ModConfig.VillageSpeedMultiplier.Value:0.##}");
        LogFeature("Dive map", ModConfig.DiveMapEnabled.Value,
            $"start={ModConfig.DiveMapStartMode.Value}, key={ModConfig.DiveMapToggleKey.Value}, controller={ModConfig.DiveMapControllerToggleCombo.Value}, " +
            $"corner={ModConfig.DiveMapMiniCorner.Value}");

        // Feature patch classes live in Features/ and are applied here, one PatchAll per enabled feature.
        if (ModConfig.ClarityEnabled.Value)
            Apply("Clarity", typeof(Features.ClarityPatch));
        if (ModConfig.FpsEnabled.Value && ModConfig.TargetFPS.Value > 0)
            Apply("FPS unlock", typeof(Features.FpsUnlockPatch));
        if (ModConfig.StruggleEnabled.Value && ModConfig.StruggleMultiplier.Value > 1f)
            Apply("Harpoon struggle", typeof(Features.HarpoonStrugglePatch));
        if (ModConfig.AutoPickupEnabled.Value || ModConfig.DiveMapEnabled.Value)
            Apply("Fish registry", typeof(Features.FishRegistry));
        if (ModConfig.AutoPickupEnabled.Value)
            Apply("Auto pickup", typeof(Features.AutoPickupPatch));
        if (ModConfig.PerfectPourTipEnabled.Value && ModConfig.PerfectPourTipMultiplier.Value > 1f)
            Apply("Perfect pour tip", typeof(Features.PerfectPourTipPatch));
        if (ModConfig.FishSpawnEnabled.Value && ModConfig.FishSpawnMultiplier.Value > 1f)
            Apply("Fish spawn odds", typeof(Features.FishSpawnPatch));
        if (ModConfig.WeaponBreakEnabled.Value && ModConfig.WeaponBreakHoldMultiplier.Value < 1f)
            Apply("Weapon break hold time", typeof(Features.HoldTimePatch));
        if (ModConfig.CrabTrapEnabled.Value)
            Apply("Crab trap timer", typeof(Features.CrabTrapPatch));
        if (ModConfig.BalatroSpeedEnabled.Value && ModConfig.BalatroSpeedMultiplier.Value > 1f)
            Apply("Balatro speed", typeof(Features.BalatroSpeedPatch));
        if (ModConfig.StealthEnabled.Value && ModConfig.StealthDetectionMultiplier.Value < 1f)
            Apply("Gentler stealth", typeof(Features.GentleStealthPatch));
        if (ModConfig.VillageSpeedEnabled.Value && ModConfig.VillageSpeedMultiplier.Value > 1f)
            Apply("Village speed", typeof(Features.VillageSpeedPatch));

        if (ModConfig.DiveMapEnabled.Value)
        {
            try
            {
                Features.DiveMap.Start();
                Harmony.PatchAll(typeof(Features.DiveMapPlayerTick));
                Logger.LogInfo("Started: Dive map");
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to start Dive map, feature disabled: {e}");
            }
        }
    }

    private static void Apply(string feature, Type patchClass)
    {
        try
        {
            Harmony.PatchAll(patchClass);
            Logger.LogInfo($"Patched: {feature}");
        }
        catch (Exception e)
        {
            Logger.LogError($"Failed to patch {feature}, feature disabled: {e}");
        }
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
