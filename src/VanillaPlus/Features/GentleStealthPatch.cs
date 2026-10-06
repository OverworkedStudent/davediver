using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace VanillaPlus.Features;

// The patrolling creatures in the glacier passage spot Dave through GadonSight: while he is in view an alert
// gauge fills at _alertIncresaseSpeed, and he is caught when it is full. Only that fill speed is scaled down.
// The creature's patrol, its field of view and how fast the gauge drains are untouched, so it still catches
// a player who stays in sight.
[HarmonyPatch(typeof(GadonSight), nameof(GadonSight.Update))]
internal static class GentleStealthPatch
{
    private const int MaxLogs = 20;

    // Vanilla fill speed per creature, so the multiplier is always applied to the original value.
    private static readonly Dictionary<int, float> vanillaFillSpeed = new();
    private static int logs;

    [HarmonyPrefix]
    private static void Prefix(GadonSight __instance)
    {
        if (!Il2CppGuard.Is<GadonSight>(__instance)) return;

        int id = __instance.GetInstanceID();
        if (!vanillaFillSpeed.TryGetValue(id, out float vanilla))
        {
            vanilla = __instance._alertIncresaseSpeed;
            vanillaFillSpeed[id] = vanilla;
            if (logs++ < MaxLogs)
                Plugin.Logger.LogInfo($"Stealth: creature sight found, alert fill speed {vanilla:0.###} -> " +
                                      $"{vanilla * ModConfig.StealthDetectionMultiplier.Value:0.###}, drain speed " +
                                      $"{__instance._alertDecreaseSpeed:0.###}, gauge max {__instance._alertGaugeMax:0.###}");
        }

        float wanted = vanilla * ModConfig.StealthDetectionMultiplier.Value;
        if (!Mathf.Approximately(__instance._alertIncresaseSpeed, wanted))
            __instance._alertIncresaseSpeed = wanted;
    }
}
