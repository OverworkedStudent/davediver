using HarmonyLib;
using UnityEngine;

namespace VanillaPlus.Features;

// StickRLQTE is the "rock the stick, then mash the button" struggle shown after spearing a fish.
// Whenever one of its input-driven methods raises the player's score, the raise is multiplied.
// Score never moves without the player's own input and decay is left alone, so nothing auto-completes.
[HarmonyPatch(typeof(StickRLQTE))]
internal static class HarpoonStrugglePatch
{
    // Update, OnDirectHandler and OnInputSuccessByPushing may call each other; only the outermost call scales.
    private static int depth;

    private static int boostedGains;
    private static float vanillaGain;
    private static float bonusGain;
    private static int gainsInUpdate, gainsInDirect, gainsInPush;

    [HarmonyPatch(nameof(StickRLQTE.Update))]
    [HarmonyPrefix]
    private static void UpdatePrefix(StickRLQTE __instance, out float __state) => Enter(__instance, out __state);

    [HarmonyPatch(nameof(StickRLQTE.Update))]
    [HarmonyPostfix]
    private static void UpdatePostfix(StickRLQTE __instance, float __state) => Exit(__instance, __state, ref gainsInUpdate);

    [HarmonyPatch(nameof(StickRLQTE.OnDirectHandler))]
    [HarmonyPrefix]
    private static void DirectPrefix(StickRLQTE __instance, out float __state) => Enter(__instance, out __state);

    [HarmonyPatch(nameof(StickRLQTE.OnDirectHandler))]
    [HarmonyPostfix]
    private static void DirectPostfix(StickRLQTE __instance, float __state) => Exit(__instance, __state, ref gainsInDirect);

    [HarmonyPatch(nameof(StickRLQTE.OnInputSuccessByPushing))]
    [HarmonyPrefix]
    private static void PushPrefix(StickRLQTE __instance, out float __state) => Enter(__instance, out __state);

    [HarmonyPatch(nameof(StickRLQTE.OnInputSuccessByPushing))]
    [HarmonyPostfix]
    private static void PushPostfix(StickRLQTE __instance, float __state) => Exit(__instance, __state, ref gainsInPush);

    [HarmonyPatch(nameof(StickRLQTE.OnEnable))]
    [HarmonyPostfix]
    private static void OnEnablePostfix(StickRLQTE __instance)
    {
        depth = 0;
        boostedGains = 0;
        vanillaGain = bonusGain = 0f;
        gainsInUpdate = gainsInDirect = gainsInPush = 0;
        Plugin.Logger.LogInfo($"Struggle started: type={__instance.GetIl2CppType().Name}, maxValue={__instance.maxValue:0.###}, " +
                              $"multiplier={ModConfig.StruggleMultiplier.Value:0.##}");
    }

    [HarmonyPatch(nameof(StickRLQTE.OnDisable))]
    [HarmonyPostfix]
    private static void OnDisablePostfix()
    {
        Plugin.Logger.LogInfo($"Struggle ended: boostedGains={boostedGains}, vanillaGain={vanillaGain:0.###}, bonusGain={bonusGain:0.###}, " +
                              $"gains by method: Update={gainsInUpdate}, OnDirectHandler={gainsInDirect}, OnInputSuccessByPushing={gainsInPush}");
    }

    private static void Enter(StickRLQTE qte, out float before)
    {
        depth++;
        before = qte._currentPlayerScore;
    }

    private static void Exit(StickRLQTE qte, float before, ref int methodCounter)
    {
        depth--;
        if (depth > 0) return;
        depth = 0;

        float after = qte._currentPlayerScore;
        float gain = after - before;
        if (gain <= 0f) return;

        float boosted = after + gain * (ModConfig.StruggleMultiplier.Value - 1f);
        float max = qte.maxValue;
        if (max > after) boosted = Mathf.Min(boosted, max);
        qte._currentPlayerScore = boosted;

        methodCounter++;
        boostedGains++;
        vanillaGain += gain;
        bonusGain += boosted - after;
    }
}
