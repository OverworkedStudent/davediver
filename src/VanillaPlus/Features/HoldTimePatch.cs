using HarmonyLib;

namespace VanillaPlus.Features;

// The game's hold-to-confirm buttons are HoldAmountEventUI components, each with the action it listens for and
// a maxHoldTime. Only the one for breaking down a picked-up weapon during a dive (action "BreakItem") is
// shortened. Every hold button found is logged once so the right one can be confirmed from the log.
[HarmonyPatch(typeof(HoldAmountEventUI), nameof(HoldAmountEventUI.Awake))]
internal static class HoldTimePatch
{
    private const string BreakAction = "BreakItem";
    private const int MaxLogs = 30;
    private static int logs;

    [HarmonyPostfix]
    private static void Postfix(HoldAmountEventUI __instance)
    {
        if (!Il2CppGuard.Is<HoldAmountEventUI>(__instance)) return;

        string action = __instance.actionName.ToString();
        float vanilla = __instance.maxHoldTime;
        bool isBreak = action == BreakAction;
        if (isBreak) __instance.maxHoldTime = vanilla * ModConfig.WeaponBreakHoldMultiplier.Value;

        if (logs++ < MaxLogs)
            Plugin.Logger.LogInfo($"HoldTime: '{__instance.gameObject.name}' action {action}, hold {vanilla:0.##}s" +
                                  (isBreak ? $" -> {__instance.maxHoldTime:0.##}s" : " (left alone)"));
    }
}
