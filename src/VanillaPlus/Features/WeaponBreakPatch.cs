using System.Collections.Generic;
using HarmonyLib;

namespace VanillaPlus.Features;

// Breaking down a picked-up weapon during a dive is an interaction command (BreakObjectCommand_SO) held for
// its commandDuration. Every pickup item carries a reference to that command, so when an item appears the
// command's duration is set to the vanilla value times the configured multiplier. The command asset is
// shared, so the vanilla value is remembered per asset and the multiplier never compounds.
//
// (The button itself is a plain press in the game's input bindings, and none of the game's hold-to-confirm
// buttons is involved; both were checked before settling on this.)
[HarmonyPatch(typeof(PickupInstanceItem), nameof(PickupInstanceItem.OnEnable))]
internal static class WeaponBreakPatch
{
    private const int MaxLogs = 10;

    private static readonly Dictionary<int, float> vanillaDuration = new();
    private static int logs;

    [HarmonyPostfix]
    private static void Postfix(PickupInstanceItem __instance)
    {
        if (!Il2CppGuard.Is<PickupInstanceItem>(__instance)) return;

        var command = __instance.breakItemCommand;
        if (command == null) return;

        int id = command.GetInstanceID();
        if (!vanillaDuration.TryGetValue(id, out float vanilla))
        {
            vanilla = command.commandDuration;
            vanillaDuration[id] = vanilla;
            if (logs++ < MaxLogs)
                Plugin.Logger.LogInfo($"WeaponBreak: '{command.name}' hold is {vanilla:0.##}s in vanilla, now " +
                                      $"{vanilla * ModConfig.WeaponBreakHoldMultiplier.Value:0.##}s");
        }

        float wanted = vanilla * ModConfig.WeaponBreakHoldMultiplier.Value;
        if (command.commandDuration != wanted) command.commandDuration = wanted;
    }
}
