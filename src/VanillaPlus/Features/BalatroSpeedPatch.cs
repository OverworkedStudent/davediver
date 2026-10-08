using System.Collections.Generic;
using Balatro;
using HarmonyLib;

namespace VanillaPlus.Features;

// The card mini-game keeps its pace in a BalatroSpeedData asset: a default time scale, and a higher one it
// ramps up to while a hand is being scored. Both are multiplied here, always from the vanilla values, each
// time the score board appears. Nothing about the cards, scores or money is touched.
[HarmonyPatch(typeof(BalatroScoreBoard), nameof(BalatroScoreBoard.OnEnable))]
internal static class BalatroSpeedPatch
{
    private static readonly Dictionary<int, (float normal, float max)> vanilla = new();

    [HarmonyPostfix]
    private static void Postfix(BalatroScoreBoard __instance)
    {
        if (!Il2CppGuard.Is<BalatroScoreBoard>(__instance)) return;
        var speed = __instance._speedData;
        if (speed == null) return;

        int id = speed.GetInstanceID();
        if (!vanilla.TryGetValue(id, out var original))
        {
            original = (speed._defaultTimeScale, speed._maxTimeScale);
            vanilla[id] = original;
            Plugin.Logger.LogInfo($"Balatro: vanilla pace is time scale {original.normal:0.##}, ramping by {speed._increaseTimeScale:0.##} " +
                                  $"after {speed._increaseStartCount} steps up to {original.max:0.##}");
        }

        float multiplier = ModConfig.BalatroSpeedMultiplier.Value;
        speed._defaultTimeScale = original.normal * multiplier;
        speed._maxTimeScale = original.max * multiplier;
        Plugin.Logger.LogInfo($"Balatro: pace set to {speed._defaultTimeScale:0.##}, up to {speed._maxTimeScale:0.##}");
    }
}
