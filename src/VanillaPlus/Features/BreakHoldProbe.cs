using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VanillaPlus.Features;

// The hold needed to break down a picked-up weapon is not one of the game's HoldAmountEventUI buttons (checked
// in play), so it most likely lives on the "BreakItem" input action as a hold interaction. This runs once per
// dive session: it logs how that action is set up and, where the hold is defined on the bindings, shortens it.
[HarmonyPatch(typeof(PlayerCharacter), nameof(PlayerCharacter.Update))]
internal static class BreakHoldProbe
{
    private static bool done;

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (done) return;
        done = true;
        try
        {
            Run();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"BreakHold: probe failed: {e.Message}");
        }
    }

    private static void Run()
    {
        Plugin.Logger.LogInfo($"BreakHold: input system default hold time {InputSystem.settings.defaultHoldTime:0.##}s");

        int found = 0;
        foreach (var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
        {
            if (asset == null) continue;
            InputAction action = null;
            try { action = asset.FindAction("BreakItem", false); } catch { }
            if (action == null) continue;
            found++;

            string actionInteractions = action.interactions ?? "";
            Plugin.Logger.LogInfo($"BreakHold: asset '{asset.name}' action '{action.name}' type {action.type}, action interactions '{actionInteractions}'");

            try
            {
                var bindings = action.bindings;
                for (int i = 0; i < bindings.Count; i++)
                {
                    var binding = bindings[i];
                    Plugin.Logger.LogInfo($"BreakHold:   binding {i}: path '{binding.path}', interactions '{binding.interactions}'");
                }
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning($"BreakHold:   could not list bindings: {e.Message}");
            }
        }
        if (found == 0) Plugin.Logger.LogInfo("BreakHold: no input action named BreakItem is loaded");
    }
}
