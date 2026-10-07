using System.Collections.Generic;
using HarmonyLib;

namespace VanillaPlus.Features;

// Every fish body the game has created, shared by auto pickup and the dive map.
// Destroyed fish are purged by whoever reads the set.
[HarmonyPatch(typeof(FishInteractionBody), nameof(FishInteractionBody.Awake))]
internal static class FishRegistry
{
    public static readonly HashSet<FishInteractionBody> All = new();

    [HarmonyPostfix]
    private static void Postfix(FishInteractionBody __instance) => All.Add(__instance);
}
