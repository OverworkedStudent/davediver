using System.Collections.Generic;
using HarmonyLib;
using SushiBar.Customer;
using SushiBar.QTE;
using UnityEngine;

namespace VanillaPlus.Features;

// How the game decides tips (read from in-game logs):
//   chance = clamp(giveTipChanceFactor * averageHallStaffCharm / customerLevel, 0.35, 0.75)
// It is rolled once per customer inside SushiBarCustomer.Served, when their dish arrives. Pour quality is not
// an input. This patch remembers which customers were handed a perfect green tea or beer and multiplies the
// chance for that customer's roll only, never past the game's own maximum.
//
// Green tea is served before the meal, so it normally applies. Beer is usually ordered after the meal has been
// served, in which case that customer's roll is already over and nothing changes.
[HarmonyPatch]
internal static class PerfectPourTipPatch
{
    private const float FallbackMaxChance = 0.75f;
    private const int MaxLogs = 60;

    private static readonly HashSet<int> perfectCustomers = new();

    private static QTEType lastPourType;
    private static QTEResult lastPourResult;
    private static int lastPourFrame = -1;

    private static int servingCustomer;
    private static bool probingMax;
    private static float maxChance = -1f;
    private static int logs;

    [HarmonyPatch(typeof(QTECoreLiquidSettings), nameof(QTECoreLiquidSettings.GetTotalPay))]
    [HarmonyPostfix]
    private static void PourResult(QTEType type, QTEResult result)
    {
        lastPourType = type;
        lastPourResult = result;
        lastPourFrame = Time.frameCount;
    }

    // Called for the customer receiving the drink, straight after the pour result is priced.
    [HarmonyPatch(typeof(SushiBarCustomer), nameof(SushiBarCustomer.GetDrinkPayment))]
    [HarmonyPostfix]
    private static void DrinkDelivered(SushiBarCustomer __instance)
    {
        if (lastPourFrame != Time.frameCount || lastPourResult != QTEResult.Perfect) return;
        if (lastPourType != QTEType.GreenTea && lastPourType != QTEType.Beer) return;
        if (!Il2CppGuard.Is<SushiBarCustomer>(__instance)) return;

        if (perfectCustomers.Count > 256) perfectCustomers.Clear();
        perfectCustomers.Add(__instance.GetInstanceID());
        Log($"PerfectPourTip: perfect {lastPourType} for {__instance.name}, tip chance will be boosted if their dish is still to come");
    }

    [HarmonyPatch(typeof(SushiBarCustomer), nameof(SushiBarCustomer.Served))]
    [HarmonyPrefix]
    private static void ServedBegin(SushiBarCustomer __instance)
    {
        servingCustomer = 0;
        if (!Il2CppGuard.Is<SushiBarCustomer>(__instance)) return;
        int id = __instance.GetInstanceID();
        if (perfectCustomers.Contains(id)) servingCustomer = id;
    }

    [HarmonyPatch(typeof(SushiBarCustomer), nameof(SushiBarCustomer.Served))]
    [HarmonyPostfix]
    private static void ServedEnd() => servingCustomer = 0;

    [HarmonyPatch(typeof(GameFormulaManager), nameof(GameFormulaManager.GiveTipChance))]
    [HarmonyPostfix]
    private static void BoostChance(GameFormulaManager __instance, ref float __result)
    {
        if (probingMax || servingCustomer == 0) return;

        // One boost per perfect pour.
        perfectCustomers.Remove(servingCustomer);
        servingCustomer = 0;

        float vanilla = __result;
        float boosted = Mathf.Min(vanilla * ModConfig.PerfectPourTipMultiplier.Value, Mathf.Max(vanilla, MaxChance(__instance)));
        __result = boosted;
        Log($"PerfectPourTip: tip chance {vanilla:0.####} -> {boosted:0.####} (game max {maxChance:0.##})");
    }

    // Asks the game's own formula for its ceiling instead of hard-coding it.
    private static float MaxChance(GameFormulaManager formulas)
    {
        if (maxChance > 0f) return maxChance;
        probingMax = true;
        try
        {
            maxChance = formulas.GiveTipChanceInternal(1f, 1000000d, 1);
            if (maxChance <= 0f || maxChance > 1f) maxChance = FallbackMaxChance;
        }
        catch
        {
            maxChance = FallbackMaxChance;
        }
        finally
        {
            probingMax = false;
        }
        return maxChance;
    }

    private static void Log(string message)
    {
        if (logs >= MaxLogs) return;
        logs++;
        Plugin.Logger.LogInfo(message);
    }
}
