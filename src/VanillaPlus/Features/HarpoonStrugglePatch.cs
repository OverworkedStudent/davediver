using HarmonyLib;

namespace VanillaPlus.Features;

// StickRLQTE is the "rock the stick, then mash the button" struggle shown after spearing a fish.
// The game raises the gauge from its input callbacks, between frames. Each frame this patch looks at how
// much the gauge rose since the last frame and adds a share of that on top. The gauge only rises when the
// player gives input and the fish's pull is untouched, so nothing auto-completes.
[HarmonyPatch(typeof(StickRLQTE))]
internal static class HarpoonStrugglePatch
{
    private static float lastScore;
    private static int rises;
    private static float vanillaTotal;
    private static float bonusTotal;

    [HarmonyPatch(nameof(StickRLQTE.OnEnable))]
    [HarmonyPostfix]
    private static void OnEnablePostfix(StickRLQTE __instance)
    {
        if (!Il2CppGuard.Is<StickRLQTE>(__instance)) return;
        lastScore = __instance._currentPlayerScore;
        rises = 0;
        vanillaTotal = bonusTotal = 0f;
    }

    [HarmonyPatch(nameof(StickRLQTE.Update))]
    [HarmonyPrefix]
    private static void UpdatePrefix(StickRLQTE __instance)
    {
        if (!Il2CppGuard.Is<StickRLQTE>(__instance)) return;

        float score = __instance._currentPlayerScore;
        float rise = score - lastScore;
        if (rise > 0.0001f)
        {
            float bonus = rise * (ModConfig.StruggleMultiplier.Value - 1f);
            score += bonus;
            __instance._currentPlayerScore = score;

            rises++;
            vanillaTotal += rise;
            bonusTotal += bonus;
        }
        lastScore = score;
    }

    [HarmonyPatch(nameof(StickRLQTE.OnDisable))]
    [HarmonyPostfix]
    private static void OnDisablePostfix(StickRLQTE __instance)
    {
        if (!Il2CppGuard.Is<StickRLQTE>(__instance)) return;
        // The short first-phase-only variant opens for most speared fish without any input; keep those quiet.
        if (rises == 0) return;
        Plugin.Logger.LogInfo($"Struggle ended: type={__instance.GetIl2CppType().Name}, inputs boosted={rises}, " +
                              $"vanilla gain={vanillaTotal:0.###}, bonus gain={bonusTotal:0.###}, " +
                              $"final score={__instance._currentPlayerScore:0.###} of {__instance.maxValue:0.###}");
    }
}
