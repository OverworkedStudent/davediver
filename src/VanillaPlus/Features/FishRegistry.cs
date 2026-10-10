using System;
using System.Collections.Generic;
using HarmonyLib;

namespace VanillaPlus.Features;

// Every fish body the game has created, for the two optional features that need them (auto pickup of fish
// and fish dots on the dive map). Only patched in when one of those is switched on.
// Destroyed fish are dropped here as the set grows, so it stays small however long the session runs.
[HarmonyPatch(typeof(FishInteractionBody), nameof(FishInteractionBody.Awake))]
internal static class FishRegistry
{
    private const int MinPurgeSize = 128;

    public static readonly HashSet<FishInteractionBody> All = new();
    private static int purgeAt = MinPurgeSize;

    [HarmonyPostfix]
    private static void Postfix(FishInteractionBody __instance)
    {
        All.Add(__instance);
        if (All.Count < purgeAt) return;

        All.RemoveWhere(f => f == null);
        purgeAt = Math.Max(MinPurgeSize, All.Count * 2);
    }
}
