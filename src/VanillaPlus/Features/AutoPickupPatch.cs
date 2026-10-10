using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace VanillaPlus.Features;

// Adapted from the AutoPickup feature of WhiteMinds/dave-diver-expansion (MIT licensed):
// https://github.com/WhiteMinds/dave-diver-expansion
//
// Picks things up through the game's own interaction calls, exactly as if the player had pressed the
// button while standing next to them. Nothing is spawned and nothing is collected that the player could
// not have collected by hand.
[HarmonyPatch]
internal static class AutoPickupPatch
{
    // Oxygen chests leave an oxygen zone where they stood, so opening one from far away would waste it.
    private const float OxygenChestRadius = 1f;
    private const float UnlockCooldown = 1f;
    private const float PurgeInterval = 2f;
    private const int MaxPickupLogs = 40;

    private static readonly HashSet<PickupInstanceItem> items = new();
    private static readonly HashSet<InstanceItemChest> chests = new();
    private static readonly HashSet<GameObject> pending = new();
    // Picking something up can disable it at once, which edits the sets above, so loops run over copies.
    private static readonly List<PickupInstanceItem> itemBuffer = new();
    private static readonly List<InstanceItemChest> chestBuffer = new();
    private static readonly List<FishInteractionBody> fishBuffer = new();

    private static bool wasLocked;
    private static float unlockTime;
    private static float nextPurge;
    private static int pickupLogs;

    [HarmonyPatch(typeof(PickupInstanceItem), nameof(PickupInstanceItem.OnEnable))]
    [HarmonyPostfix]
    private static void ItemEnabled(PickupInstanceItem __instance) => items.Add(__instance);

    [HarmonyPatch(typeof(PickupInstanceItem), nameof(PickupInstanceItem.OnDisable))]
    [HarmonyPostfix]
    private static void ItemDisabled(PickupInstanceItem __instance) => items.Remove(__instance);

    [HarmonyPatch(typeof(InstanceItemChest), nameof(InstanceItemChest.OnEnable))]
    [HarmonyPostfix]
    private static void ChestEnabled(InstanceItemChest __instance) => chests.Add(__instance);

    [HarmonyPatch(typeof(InstanceItemChest), nameof(InstanceItemChest.SuccessInteract))]
    [HarmonyPostfix]
    private static void ChestOpened(InstanceItemChest __instance) => chests.Remove(__instance);


    [HarmonyPatch(typeof(PlayerCharacter), nameof(PlayerCharacter.Update))]
    [HarmonyPostfix]
    private static void PlayerUpdate(PlayerCharacter __instance)
    {
        float now = Time.time;
        if (now >= nextPurge)
        {
            nextPurge = now + PurgeInterval;
            items.RemoveWhere(i => i == null);
            chests.RemoveWhere(c => c == null);
            FishRegistry.All.RemoveWhere(f => f == null);
            pending.RemoveWhere(g => g == null);
        }

        // Stay out of the way during cutscenes and dialogue, and for a moment after they end.
        if (__instance.IsActionLock || __instance.IsScenarioPlaying)
        {
            wasLocked = true;
            return;
        }
        if (wasLocked)
        {
            wasLocked = false;
            unlockTime = now;
        }
        if (now - unlockTime < UnlockCooldown) return;

        Vector3 playerPos = __instance.transform.position;
        float radius = ModConfig.PickupRadius.Value;

        if (ModConfig.AutoPickupItems.Value) PickupItems(__instance, playerPos, radius);
        if (ModConfig.AutoPickupFish.Value) PickupFish(__instance, playerPos, radius);
        if (ModConfig.AutoOpenChests.Value) OpenChests(__instance, playerPos, radius);
    }

    private static void PickupItems(PlayerCharacter player, Vector3 playerPos, float radius)
    {
        itemBuffer.Clear();
        itemBuffer.AddRange(items);
        foreach (var item in itemBuffer)
        {
            if (item == null) continue;
            GameObject go = item.gameObject;
            if (go == null || pending.Contains(go)) continue;
            if (item.isNeedSwapSetID != 0) continue; // swap-indicator ghost copy
            Vector3 pos = item.transform.position;
            if (pos == Vector3.zero || Vector3.Distance(playerPos, pos) > radius) continue;

            // Weapons and harpoon heads swap with what Dave is holding, which would loop forever.
            string name = go.name;
            if (name.StartsWith("PickupInstance") || name.Contains("HarpoonHead")) continue;

            if (name.Contains("BulletBox"))
            {
                if (!ModConfig.AutoPickupAmmoBox.Value) continue;
                var gun = player.CurrentInstanceItemInventory?.gunHandler;
                if (gun != null && gun.IsBulletFull()) continue;
            }

            // Sea urchins hurt without gloves of a high enough level.
            var seaUrchin = item.TryCast<PickupInstanceItem_SeaUrchin>();
            if (seaUrchin != null)
            {
                var grab = player.grabHandler;
                if (grab == null || grab.grabLevel < seaUrchin._grabLevel) continue;
            }

            if (!item.CheckAvailableInteraction(player)) continue;
            item.SuccessInteract(player);
            pending.Add(go);
            LogPickup("item", name);
        }
    }

    private static void PickupFish(PlayerCharacter player, Vector3 playerPos, float radius)
    {
        fishBuffer.Clear();
        fishBuffer.AddRange(FishRegistry.All);
        foreach (var body in fishBuffer)
        {
            if (body == null) continue;
            GameObject go = body.gameObject;
            if (go == null || pending.Contains(go)) continue;
            Vector3 pos = body.transform.position;
            if (pos == Vector3.zero || Vector3.Distance(playerPos, pos) > radius) continue;
            if (body.InteractionType != FishInteractionBody.FishInteractionType.Pickup) continue;
            if (!body.isInteractable) continue;

            if (!body.CheckAvailableInteraction(player)) continue;
            body.SuccessInteract(player);
            pending.Add(go);
            LogPickup("fish", go.name);
        }
    }

    private static void OpenChests(PlayerCharacter player, Vector3 playerPos, float radius)
    {
        chestBuffer.Clear();
        chestBuffer.AddRange(chests);
        foreach (var chest in chestBuffer)
        {
            if (chest == null) continue;
            GameObject go = chest.gameObject;
            if (go == null || pending.Contains(go) || chest.IsOpen) continue;
            Vector3 pos = chest.transform.position;
            if (pos == Vector3.zero) continue;
            float distance = Vector3.Distance(playerPos, pos);
            if (distance > radius) continue;

            string name = go.name;
            bool isOxygen = name.Contains("O2") || name.Contains("ShellFish004");
            if (isOxygen && (!ModConfig.AutoPickupOxygenBox.Value || distance > OxygenChestRadius)) continue;

            chest.SuccessInteract(player);
            pending.Add(go);
            LogPickup("chest", name);
        }
    }

    private static void LogPickup(string kind, string name)
    {
        if (pickupLogs >= MaxPickupLogs) return;
        pickupLogs++;
        Plugin.Logger.LogInfo($"AutoPickup: {kind} '{name}'{(pickupLogs == MaxPickupLogs ? " (further pickups not logged)" : "")}");
    }
}
