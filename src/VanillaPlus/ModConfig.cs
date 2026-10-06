using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace VanillaPlus;

internal static class ModConfig
{
    public static ConfigEntry<bool> ClarityEnabled;

    public static ConfigEntry<bool> FpsEnabled;
    public static ConfigEntry<int> TargetFPS;

    public static ConfigEntry<bool> StruggleEnabled;
    public static ConfigEntry<float> StruggleMultiplier;

    public static ConfigEntry<bool> PerfectPourTipEnabled;
    public static ConfigEntry<float> PerfectPourTipMultiplier;

    public static ConfigEntry<bool> AutoPickupEnabled;
    public static ConfigEntry<bool> AutoPickupItems;
    public static ConfigEntry<bool> AutoPickupAmmoBox;
    public static ConfigEntry<bool> AutoPickupFish;
    public static ConfigEntry<bool> AutoOpenChests;
    public static ConfigEntry<bool> AutoPickupOxygenBox;
    public static ConfigEntry<float> PickupRadius;

    public static ConfigEntry<bool> StealthEnabled;
    public static ConfigEntry<float> StealthDetectionMultiplier;

    public static ConfigEntry<bool> VillageSpeedEnabled;
    public static ConfigEntry<float> VillageSpeedMultiplier;

    public static ConfigEntry<bool> DiveMapEnabled;
    public static ConfigEntry<Features.DiveMapMode> DiveMapStartMode;
    public static ConfigEntry<KeyCode> DiveMapToggleKey;
    public static ConfigEntry<Features.DiveMapControllerToggle> DiveMapControllerToggleCombo;
    public static ConfigEntry<Features.DiveMapCorner> DiveMapMiniCorner;
    public static ConfigEntry<float> DiveMapMiniSize;
    public static ConfigEntry<float> DiveMapMiniRadius;
    public static ConfigEntry<float> DiveMapOpacity;

    private static readonly Dictionary<string, ConfigEntry<bool>> clarityEffects = new();

    // Unknown effect types (colour grading, bloom, ...) are never touched.
    public static bool ClarityShouldDisable(string effectTypeName) =>
        clarityEffects.TryGetValue(effectTypeName, out var entry) && entry.Value;

    public static void Bind(ConfigFile cfg)
    {
        ClarityEnabled = cfg.Bind("Clarity", "Enabled", true,
            "Master toggle: remove the blur and darkening at the screen edges. The per-effect toggles below only apply while this is on.");
        BindClarityEffect(cfg, "Vignette", true, "Darkened screen edges.");
        BindClarityEffect(cfg, "VerticalBlur", true, "The game's own blur bands at the top and bottom of the screen.");
        BindClarityEffect(cfg, "DepthOfField", true, "Depth of field blur.");
        BindClarityEffect(cfg, "ChromaticAberration", true, "Colour fringing towards the screen edges.");
        BindClarityEffect(cfg, "LensDistortion", false, "Lens warping. Off by default because it is not a blur or darkening effect.");

        AutoPickupEnabled = cfg.Bind("AutoPickup", "Enabled", true,
            "Master toggle: pick up nearby things while diving, as if the interact button had been pressed.");
        AutoPickupItems = cfg.Bind("AutoPickup", "AutoPickupItems", true,
            "Pick up dropped items and materials. Weapons and harpoon heads are never picked up automatically.");
        AutoPickupAmmoBox = cfg.Bind("AutoPickup", "AutoPickupAmmoBox", true,
            "Pick up ammo boxes, unless the current gun is already full. Needs AutoPickupItems.");
        AutoPickupFish = cfg.Bind("AutoPickup", "AutoPickupFish", false,
            "Pick up dead or sleeping fish that only need the interact button.");
        AutoOpenChests = cfg.Bind("AutoPickup", "AutoOpenChests", false,
            "Open chests automatically.");
        AutoPickupOxygenBox = cfg.Bind("AutoPickup", "AutoPickupOxygenBox", true,
            "Include oxygen chests when AutoOpenChests is on. They always use a radius of 1.0 so the oxygen is not wasted.");
        PickupRadius = cfg.Bind("AutoPickup", "PickupRadius", 1f,
            new ConfigDescription("Distance from Dave within which things are picked up, in game units.",
                new AcceptableValueRange<float>(0.5f, 5f)));

        DiveMapEnabled = cfg.Bind("DiveMap", "Enabled", true,
            "Master toggle for the dive map. While the map is switched Off in game it costs nothing.");
        DiveMapStartMode = cfg.Bind("DiveMap", "StartMode", Features.DiveMapMode.Off,
            "How the map starts each session: Off, Mini (round corner map) or Big (full level). The toggle cycles Off -> Mini -> Big -> Off.");
        DiveMapToggleKey = cfg.Bind("DiveMap", "ToggleKey", KeyCode.M,
            "Keyboard key that cycles the map.");
        DiveMapControllerToggleCombo = cfg.Bind("DiveMap", "ControllerToggle", Features.DiveMapControllerToggle.BothStickClicks,
            "Controller shortcut that cycles the map: BothStickClicks (press L3 and R3 together), SelectPlusRightStickClick, or None.");
        DiveMapMiniCorner = cfg.Bind("DiveMap", "MiniCorner", Features.DiveMapCorner.TopRight,
            "Screen corner for the round map. TopRight is the corner the dive HUD leaves free; item pickup pop-ups use the bottom right.");
        DiveMapMiniSize = cfg.Bind("DiveMap", "MiniSize", 0.2f,
            new ConfigDescription("Diameter of the round map as a share of screen height.",
                new AcceptableValueRange<float>(0.12f, 0.4f)));
        DiveMapMiniRadius = cfg.Bind("DiveMap", "MiniRadius", 15f,
            new ConfigDescription("How far the round map sees around Dave, in game units. Larger shows more but smaller.",
                new AcceptableValueRange<float>(8f, 40f)));
        DiveMapOpacity = cfg.Bind("DiveMap", "Opacity", 1f,
            new ConfigDescription("Opacity of the map picture.", new AcceptableValueRange<float>(0.4f, 1f)));

        StealthEnabled = cfg.Bind("Stealth", "Enabled", true,
            "Make the patrolling creatures in the glacier passage slower to spot Dave. They can still catch him.");
        StealthDetectionMultiplier = cfg.Bind("Stealth", "DetectionSpeedMultiplier", 0.7f,
            new ConfigDescription("How fast their alert gauge fills while Dave is in view. 1.0 = vanilla, lower = more time to hide.",
                new AcceptableValueRange<float>(0.25f, 1f)));

        VillageSpeedEnabled = cfg.Bind("VillageSpeed", "Enabled", true,
            "Move faster in the Sea People Village. Dives are not affected.");
        VillageSpeedMultiplier = cfg.Bind("VillageSpeed", "SpeedMultiplier", 1.5f,
            new ConfigDescription("Move speed multiplier in the village. 1.0 = vanilla.",
                new AcceptableValueRange<float>(1f, 3f)));

        FpsEnabled = cfg.Bind("FPS", "Enabled", true,
            "Allow this mod to change the frame cap. Has no effect while TargetFPS is 0.");
        TargetFPS = cfg.Bind("FPS", "TargetFPS", 0,
            new ConfigDescription("Frame cap. 0 = vanilla (frame rate settings are left untouched).",
                new AcceptableValueRange<int>(0, 360)));

        StruggleEnabled = cfg.Bind("HarpoonStruggle", "Enabled", true,
            "Multiply the progress each stick rock / button mash gives while landing a speared fish. Never auto-completes.");
        StruggleMultiplier = cfg.Bind("HarpoonStruggle", "StruggleMultiplier", 1.5f,
            new ConfigDescription("Progress multiplier per input. 1.0 = vanilla.",
                new AcceptableValueRange<float>(1f, 3f)));

        PerfectPourTipEnabled = cfg.Bind("PerfectPourTip", "Enabled", true,
            "Slightly raise the tip chance after a perfect green tea or beer pour.");
        PerfectPourTipMultiplier = cfg.Bind("PerfectPourTip", "PerfectPourTipMultiplier", 1.15f,
            new ConfigDescription("Tip chance multiplier on a perfect pour, clamped to the game's maximum. 1.0 = vanilla.",
                new AcceptableValueRange<float>(1f, 2f)));
    }

    private static void BindClarityEffect(ConfigFile cfg, string effectTypeName, bool defaultValue, string description)
    {
        clarityEffects[effectTypeName] = cfg.Bind("Clarity", "Disable" + effectTypeName, defaultValue, description);
    }

    public static string ClaritySummary()
    {
        var parts = new List<string>();
        foreach (var pair in clarityEffects) parts.Add($"{pair.Key}={(pair.Value.Value ? "off" : "vanilla")}");
        return string.Join(", ", parts);
    }
}
