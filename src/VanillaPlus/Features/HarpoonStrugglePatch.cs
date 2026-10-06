using HarmonyLib;
using UnityEngine;

namespace VanillaPlus.Features;

// StickRLQTE is the "rock the stick, then mash the button" struggle shown after spearing a fish.
// Each struggle carries a StickRLQTEValue with PlayerGuageIncValuePerOneQTEAction: the gauge gained per
// input. That one number is multiplied. The fish's pull, the starting value and the target are untouched,
// and the gauge still only moves when the player gives input, so nothing auto-completes.
[HarmonyPatch(typeof(StickRLQTE))]
internal static class HarpoonStrugglePatch
{
    private static float vanillaGain;
    private static float boostedGain;
    private static float lastScore;
    private static float largestJump;
    private static int jumps;
    private static int setupLogs;

    [HarmonyPatch(nameof(StickRLQTE.OnEnable))]
    [HarmonyPostfix]
    private static void OnEnablePostfix(StickRLQTE __instance)
    {
        setupLogs = 0;
        largestJump = 0f;
        jumps = 0;
        Begin(__instance, "OnEnable");
    }

    [HarmonyPatch(nameof(StickRLQTE.SetLevel))]
    [HarmonyPostfix]
    private static void SetLevelPostfix(StickRLQTE __instance) => Begin(__instance, "SetLevel");

    [HarmonyPatch(nameof(StickRLQTE.Update))]
    [HarmonyPrefix]
    private static void UpdatePrefix(StickRLQTE __instance)
    {
        // The game may copy fresh level data in at any point; re-apply if our value was replaced.
        var value = __instance.stickRLQTEValue;
        if (!Mathf.Approximately(value.PlayerGuageIncValuePerOneQTEAction, boostedGain))
            Begin(__instance, "Update");

        float score = __instance._currentPlayerScore;
        float jump = score - lastScore;
        if (jump > 0.0001f)
        {
            jumps++;
            if (jump > largestJump) largestJump = jump;
        }
        lastScore = score;
    }

    [HarmonyPatch(nameof(StickRLQTE.OnDisable))]
    [HarmonyPostfix]
    private static void OnDisablePostfix(StickRLQTE __instance)
    {
        Plugin.Logger.LogInfo($"Struggle ended: gauge rises seen={jumps}, largest single rise={largestJump:0.###} " +
                              $"(vanilla per input={vanillaGain:0.###}, boosted={boostedGain:0.###}), final score={__instance._currentPlayerScore:0.###}");
    }

    private static void Begin(StickRLQTE qte, string from)
    {
        var value = qte.stickRLQTEValue;
        float vanilla = VanillaGainFor(qte, value.Level, value.PlayerGuageIncValuePerOneQTEAction);
        float boosted = vanilla * ModConfig.StruggleMultiplier.Value;

        vanillaGain = vanilla;
        boostedGain = boosted;
        value.PlayerGuageIncValuePerOneQTEAction = boosted;
        qte.stickRLQTEValue = value;

        lastScore = qte._currentPlayerScore;
        if (++setupLogs > 4) return;
        Plugin.Logger.LogInfo($"Struggle set up ({from}): type={qte.GetIl2CppType().Name}, level={value.Level}, " +
                              $"gain per input {vanilla:0.###} -> {boosted:0.###}, start={value.PlayerFirstValue:0.###}, " +
                              $"fish pull per sec={value.EnemyGuageDescValuePerOneSec:0.###}, target={value.MaxValue:0.###}");
    }

    // Always derived from the game's own level table, so re-applying never compounds the multiplier.
    private static float VanillaGainFor(StickRLQTE qte, int level, float current)
    {
        var table = qte.levelDatas?.datas;
        if (table != null)
        {
            for (int i = 0; i < table.Count; i++)
            {
                var row = table[i];
                if (row.Level == level) return row.PlayerGuageIncValuePerOneQTEAction;
            }
        }

        // No table row: fall back to the live value, undoing our own boost if it is already applied.
        return Mathf.Approximately(current, boostedGain) && vanillaGain > 0f ? vanillaGain : current;
    }
}
