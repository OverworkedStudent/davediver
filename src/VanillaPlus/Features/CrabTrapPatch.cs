using System.Collections.Generic;
using HarmonyLib;

namespace VanillaPlus.Features;

// A placed crab trap counts a timer up to its completeTime before it can be collected. That target is capped
// at the configured number of seconds. What the trap catches, and how much, is untouched.
[HarmonyPatch(typeof(CrabTrapObject), nameof(CrabTrapObject.Update))]
internal static class CrabTrapPatch
{
    private const int MaxLogs = 20;

    private static readonly HashSet<int> seen = new();
    private static int logs;

    [HarmonyPrefix]
    private static void Prefix(CrabTrapObject __instance)
    {
        if (!Il2CppGuard.Is<CrabTrapObject>(__instance)) return;

        float cap = ModConfig.CrabTrapSeconds.Value;
        float current = __instance.completeTime;
        if (seen.Add(__instance.GetInstanceID()) && logs++ < MaxLogs)
            Plugin.Logger.LogInfo($"CrabTrap: trap found, vanilla wait {current:0.#}s (timer at {__instance.timer:0.#}s, state {__instance.currentState}), capping at {cap:0.#}s");

        if (current > cap) __instance.completeTime = cap;
    }
}
