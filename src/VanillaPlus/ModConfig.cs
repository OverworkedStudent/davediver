using BepInEx.Configuration;

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

    public static void Bind(ConfigFile cfg)
    {
        ClarityEnabled = cfg.Bind("Clarity", "Enabled", true,
            "Master toggle: remove the blur and darkening at the screen edges.");

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
}
