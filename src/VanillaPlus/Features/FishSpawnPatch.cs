using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace VanillaPlus.Features;

// How the game picks fish: each spawn point (FishAllocator) either has one fixed fish, or a weighted list
// (FishPrefabOrGroups) from which one entry is drawn when Dave gets near. A fish's odds at a spawn point are
// its weight divided by the sum of the weights there.
//
// This patch multiplies the weight of the chosen species at every spawn point that lists them, before the
// draw happens. It never adds a fish to a spawn point that could not already produce it, and it never spawns
// anything itself. It also logs the vanilla and boosted odds so the real numbers can be read from the log.
[HarmonyPatch(typeof(PlayerCharacter), nameof(PlayerCharacter.Update))]
internal static class FishSpawnPatch
{
    private const int MaxLogs = 60;
    private const float RescanInterval = 0.5f;

    private static readonly HashSet<int> seen = new();
    private static string[] wanted;
    private static int logs;
    private static int managerId;
    private static int lastCount = -1;
    private static float nextScan;
    private static int listed, fixedOnly, boosted, tooLate;

    [HarmonyPostfix]
    private static void Postfix()
    {
        try
        {
            Scan();
        }
        catch (Exception e)
        {
            if (logs++ < MaxLogs) Plugin.Logger.LogError($"FishSpawn: {e.Message}");
        }
    }

    private static void Scan()
    {
        var manager = Singleton<InGameManager>._instance;
        if (manager == null) return;

        int id = manager.GetInstanceID();
        if (id != managerId)
        {
            if (managerId != 0 && listed + fixedOnly > 0)
                Log($"FishSpawn: level summary: {listed} spawn points listing a boosted fish ({boosted} boosted in time, {tooLate} had already spawned), {fixedOnly} with one as their fixed fish");
            managerId = id;
            seen.Clear();
            lastCount = -1;
            listed = fixedOnly = boosted = tooLate = 0;
        }

        var allocators = manager.FishAllocators;
        if (allocators == null) return;
        wanted ??= ParseNames(ModConfig.FishSpawnBoostedFish.Value);
        if (wanted.Length == 0) return;

        // Spawn points are looked at the moment the list changes, and now and then besides. Walking the
        // whole list every frame would be wasted work on a handheld.
        int count = allocators.Count;
        float now = Time.unscaledTime;
        if (count == lastCount && now < nextScan) return;
        lastCount = count;
        nextScan = now + RescanInterval;

        for (int i = 0; i < count; i++)
        {
            var allocator = allocators[i];
            if (allocator == null || !seen.Add(allocator.GetInstanceID())) continue;
            Process(allocator);
        }
    }

    private static void Process(FishAllocator allocator)
    {
        var list = allocator.FishPrefabOrGroups?.list;
        if (list == null || list.Count == 0)
        {
            var only = allocator.FishPrefabOrGroup;
            if (only != null && Matches(only.name))
            {
                fixedOnly++;
                Log($"FishSpawn: '{allocator.gameObject.name}' always spawns '{only.name}' (no odds involved)");
            }
            return;
        }

        int total = 0;
        bool any = false;
        for (int i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            if (entry == null) continue;
            total += entry.weight;
            var prefab = entry.data;
            if (prefab != null && Matches(prefab.name)) any = true;
        }
        if (!any || total <= 0) return;

        listed++;
        bool alreadySpawned = allocator.IsInstanced;
        float multiplier = ModConfig.FishSpawnMultiplier.Value;
        var before = new StringBuilder();
        var after = new StringBuilder();
        int newTotal = 0;
        var newWeights = new int[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            if (entry == null) continue;
            var prefab = entry.data;
            bool match = prefab != null && Matches(prefab.name);
            newWeights[i] = match ? Math.Max(entry.weight, (int)Math.Round(entry.weight * multiplier)) : entry.weight;
            newTotal += newWeights[i];
        }
        for (int i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            if (entry == null) continue;
            string name = entry.data != null ? entry.data.name : "(nothing)";
            before.Append($" {name} {100f * entry.weight / total:0.#}%");
            after.Append($" {name} {100f * newWeights[i] / newTotal:0.#}%");
            if (!alreadySpawned) entry.weight = newWeights[i];
        }

        if (alreadySpawned) tooLate++;
        else boosted++;
        Log($"FishSpawn: '{allocator.gameObject.name}' vanilla odds:{before} | {(alreadySpawned ? "already spawned, left alone" : "now:" + after)}");
    }

    private static bool Matches(string prefabName)
    {
        for (int i = 0; i < wanted.Length; i++)
            if (prefabName.IndexOf(wanted[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private static string[] ParseNames(string raw)
    {
        var names = new List<string>();
        foreach (var part in (raw ?? "").Split(','))
        {
            string name = part.Trim();
            if (name.Length > 0) names.Add(name);
        }
        return names.ToArray();
    }

    private static void Log(string message)
    {
        if (logs++ < MaxLogs) Plugin.Logger.LogInfo(message);
    }
}
