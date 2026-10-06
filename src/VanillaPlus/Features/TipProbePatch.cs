using HarmonyLib;
using SushiBar.Customer;
using SushiBar.QTE;

namespace VanillaPlus.Features;

// Temporary, log-only. Changes nothing in the game. It records how drink pours, customer payment and the
// tip chance formula relate, so the perfect pour tip buff can be written against real behaviour.
[HarmonyPatch]
internal static class TipProbePatch
{
    private const int MaxLines = 600;
    private static int lines;

    private static void Log(string message)
    {
        if (lines >= MaxLines) return;
        lines++;
        Plugin.Logger.LogInfo($"TipProbe: {message}{(lines == MaxLines ? " (line limit reached, probe silent from here)" : "")}");
    }

    private static bool mapping;
    private static bool mapped;
    private static float lastFactor;

    // One-off: evaluates the game's own formula over a grid so its shape and cap can be read from the log.
    private static void MapFormula(GameFormulaManager formulas)
    {
        if (mapped || lastFactor <= 0f) return;
        mapped = true;
        mapping = true;
        try
        {
            foreach (int level in new[] { 1, 3, 5 })
            {
                var row = new System.Text.StringBuilder();
                foreach (double charm in new[] { 0d, 10d, 20d, 30d, 40d, 50d, 60d, 80d, 100d, 150d, 300d })
                    row.Append($" {charm:0}:{formulas.GiveTipChanceInternal(lastFactor, charm, level):0.###}");
                Log($"Formula map, factor={lastFactor:0.####}, customerLevel={level}, charm:chance ->{row}");
            }
            var factors = new System.Text.StringBuilder();
            foreach (float factor in new[] { 0.001f, 0.005f, 0.0075f, 0.01f, 0.02f })
                factors.Append($" {factor:0.####}:{formulas.GiveTipChanceInternal(factor, 60.5, 3):0.###}");
            Log($"Formula map, charm=60.5, customerLevel=3, factor:chance ->{factors}");
        }
        catch (System.Exception e)
        {
            Log($"Formula map failed: {e.Message}");
        }
        finally
        {
            mapping = false;
        }
    }

    private static string Who(SushiBarCustomer customer) => customer == null ? "null" : $"{customer.name}#{customer.GetInstanceID()}";

    [HarmonyPatch(typeof(GameFormulaManager), nameof(GameFormulaManager.GiveTipChance))]
    [HarmonyPostfix]
    private static void GiveTipChance(GameFormulaManager __instance, double averageHallStaffCharm, int customerLevel, float __result)
    {
        if (mapping) return;
        Log($"GiveTipChance(charm={averageHallStaffCharm:0.###}, customerLevel={customerLevel}) = {__result:0.####}");
        MapFormula(__instance);
    }

    [HarmonyPatch(typeof(GameFormulaManager), nameof(GameFormulaManager.GiveTipChanceInternal))]
    [HarmonyPostfix]
    private static void GiveTipChanceInternal(float giveTipChanceFactor, double averageHallStaffCharm, int customerLevel, float __result)
    {
        if (mapping) return;
        Log($"GiveTipChanceInternal(factor={giveTipChanceFactor:0.####}, charm={averageHallStaffCharm:0.###}, customerLevel={customerLevel}) = {__result:0.####}");
        lastFactor = giveTipChanceFactor;
    }

    [HarmonyPatch(typeof(QTECoreLiquidSettings), nameof(QTECoreLiquidSettings.GetTotalPay))]
    [HarmonyPostfix]
    private static void GetTotalPay(QTEType type, QTEResult result, int __result) =>
        Log($"Pour finished: GetTotalPay(type={type}, result={result}) = {__result}");

    [HarmonyPatch(typeof(QTEBuffSettings), nameof(QTEBuffSettings.GetBuff))]
    [HarmonyPostfix]
    private static void GetBuff(QTEType type, QTEResult result, int __result) =>
        Log($"Pour buff: GetBuff(type={type}, result={result}) = buff TID {__result}");

    [HarmonyPatch(typeof(SushiBarCustomer), nameof(SushiBarCustomer.ServedDrink))]
    [HarmonyPostfix]
    private static void ServedDrink(SushiBarCustomer __instance, bool __result) =>
        Log($"ServedDrink -> {__result} for {Who(__instance)}, lastOrderDrink={__instance.LastOrderDrink}");

    [HarmonyPatch(typeof(SushiBarCustomer), nameof(SushiBarCustomer.GetDrinkPayment))]
    [HarmonyPostfix]
    private static void GetDrinkPayment(SushiBarCustomer __instance, int pay, int __result) =>
        Log($"GetDrinkPayment(pay={pay}) = {__result} for {Who(__instance)}");

    [HarmonyPatch(typeof(SushiBarCustomer), nameof(SushiBarCustomer.AddBuff))]
    [HarmonyPostfix]
    private static void AddBuff(SushiBarCustomer __instance, int TID)
    {
        if (TID != 0) LogBuff(__instance, TID);
    }

    [HarmonyPatch(typeof(SushiBarCustomer), nameof(SushiBarCustomer.Served))]
    [HarmonyPrefix]
    private static void ServedBegin(SushiBarCustomer __instance) => Log($"> Served begin for {Who(__instance)}");

    [HarmonyPatch(typeof(SushiBarCustomer), nameof(SushiBarCustomer.Served))]
    [HarmonyPostfix]
    private static void ServedEnd(SushiBarCustomer __instance, bool __result) => Log($"< Served end ({__result}) for {Who(__instance)}");

    [HarmonyPatch(typeof(SushiBarCustomer), nameof(SushiBarCustomer.StartEat))]
    [HarmonyPrefix]
    private static void StartEatBegin(SushiBarCustomer __instance) => Log($"> StartEat begin for {Who(__instance)}");

    [HarmonyPatch(typeof(SushiBarCustomer), nameof(SushiBarCustomer.StartEat))]
    [HarmonyPostfix]
    private static void StartEatEnd(SushiBarCustomer __instance) => Log($"< StartEat end for {Who(__instance)}");

    [HarmonyPatch(typeof(SushiBarCustomer), nameof(SushiBarCustomer.EatFnished))]
    [HarmonyPrefix]
    private static void EatFinishedBegin(SushiBarCustomer __instance) => Log($"> EatFnished begin for {Who(__instance)}");

    [HarmonyPatch(typeof(SushiBarCustomer), nameof(SushiBarCustomer.EatFnished))]
    [HarmonyPostfix]
    private static void EatFinishedEnd(SushiBarCustomer __instance) => Log($"< EatFnished end for {Who(__instance)}");

    [HarmonyPatch(typeof(SushiBarCustomer), nameof(SushiBarCustomer.AfterEatingBehavior))]
    [HarmonyPrefix]
    private static void AfterEatingBegin(SushiBarCustomer __instance) => Log($"> AfterEatingBehavior begin for {Who(__instance)}");

    [HarmonyPatch(typeof(SushiBarCustomer), nameof(SushiBarCustomer.AfterEatingBehavior))]
    [HarmonyPostfix]
    private static void AfterEatingEnd(SushiBarCustomer __instance) => Log($"< AfterEatingBehavior end for {Who(__instance)}");

    [HarmonyPatch(typeof(SushiBarAnalyticsTodayData), nameof(SushiBarAnalyticsTodayData.AddStaffTips))]
    [HarmonyPostfix]
    private static void AddStaffTips(int addValue) => Log($"AddStaffTips(addValue={addValue})");

    [HarmonyPatch(typeof(SushiBarStaff), nameof(SushiBarStaff.IsActivateTipMaster))]
    [HarmonyPostfix]
    private static void TipMaster(SushiBarStaff __instance, bool __result) => Log($"IsActivateTipMaster = {__result} for staff {__instance.name}, charm={__instance.Charm}");

    private static void LogBuff(SushiBarCustomer __instance, int TID) =>
        Log($"AddBuff(TID={TID}) on {Who(__instance)}, revenueBuffParameter={__instance.RevenueBuffParameter:0.###}");

    [HarmonyPatch(typeof(SushiBarCustomer), nameof(SushiBarCustomer.Payment))]
    [HarmonyPrefix]
    private static void PaymentBegin(SushiBarCustomer __instance) =>
        Log($"Payment begin for {Who(__instance)}, lastOrderDrink={__instance.LastOrderDrink}");

    [HarmonyPatch(typeof(SushiBarCustomer), nameof(SushiBarCustomer.Payment))]
    [HarmonyPostfix]
    private static void PaymentEnd(SushiBarCustomer __instance) =>
        Log($"Payment end for {Who(__instance)}, payment={__instance.m_Payment}, paymentType={__instance.m_PaymentType}, giveLike={__instance.m_IsGiveLike}");

    [HarmonyPatch(typeof(SushiBarCustomer), nameof(SushiBarCustomer.SetGiveLike))]
    [HarmonyPostfix]
    private static void SetGiveLike(SushiBarCustomer __instance, bool value)
    {
        if (value) Log($"SetGiveLike(True) on {Who(__instance)}");
    }
}
