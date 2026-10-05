using HarmonyLib;
using SushiBar.Customer;
using SushiBar.QTE;

namespace VanillaPlus.Features;

// Temporary, log-only. Changes nothing in the game. It records how drink pours, customer payment and the
// tip chance formula relate, so the perfect pour tip buff can be written against real behaviour.
[HarmonyPatch]
internal static class TipProbePatch
{
    private const int MaxLines = 400;
    private static int lines;

    private static void Log(string message)
    {
        if (lines >= MaxLines) return;
        lines++;
        Plugin.Logger.LogInfo($"TipProbe: {message}{(lines == MaxLines ? " (line limit reached, probe silent from here)" : "")}");
    }

    private static string Who(SushiBarCustomer customer) => customer == null ? "null" : $"{customer.name}#{customer.GetInstanceID()}";

    [HarmonyPatch(typeof(GameFormulaManager), nameof(GameFormulaManager.GiveTipChance))]
    [HarmonyPostfix]
    private static void GiveTipChance(double averageHallStaffCharm, int customerLevel, float __result) =>
        Log($"GiveTipChance(charm={averageHallStaffCharm:0.###}, customerLevel={customerLevel}) = {__result:0.####}");

    [HarmonyPatch(typeof(GameFormulaManager), nameof(GameFormulaManager.GiveTipChanceInternal))]
    [HarmonyPostfix]
    private static void GiveTipChanceInternal(float giveTipChanceFactor, double averageHallStaffCharm, int customerLevel, float __result) =>
        Log($"GiveTipChanceInternal(factor={giveTipChanceFactor:0.####}, charm={averageHallStaffCharm:0.###}, customerLevel={customerLevel}) = {__result:0.####}");

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
    private static void AddBuff(SushiBarCustomer __instance, int TID) =>
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
    private static void SetGiveLike(SushiBarCustomer __instance, bool value) =>
        Log($"SetGiveLike({value}) on {Who(__instance)}");
}
