using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace VanillaPlus.Features;

// The game's post-processing is URP Volume overrides. Each Volume's profile is checked a couple of times a
// second and the overrides selected in the config are switched off. Nothing is destroyed or written to disk,
// so removing the plugin restores the vanilla look.
[HarmonyPatch(typeof(Volume))]
internal static class ClarityPatch
{
    private const float RecheckSeconds = 0.5f;

    private static readonly Dictionary<IntPtr, float> nextCheck = new();
    private static readonly HashSet<string> logged = new();

    [HarmonyPatch(nameof(Volume.OnEnable))]
    [HarmonyPostfix]
    private static void OnEnablePostfix(Volume __instance) => Sweep(__instance, true);

    [HarmonyPatch(nameof(Volume.Update))]
    [HarmonyPostfix]
    private static void UpdatePostfix(Volume __instance) => Sweep(__instance, false);

    private static void Sweep(Volume volume, bool force)
    {
        float now = Time.unscaledTime;
        IntPtr key = volume.Pointer;
        if (!force && nextCheck.TryGetValue(key, out float due) && now < due) return;
        if (nextCheck.Count > 256) nextCheck.Clear();
        nextCheck[key] = now + RecheckSeconds;

        // profileRef is the instantiated profile when the game made one, otherwise the shared asset.
        VolumeProfile profile = volume.profileRef;
        if (profile == null) return;

        var components = profile.components;
        if (components == null) return;

        for (int i = 0; i < components.Count; i++)
        {
            VolumeComponent component = components[i];
            if (component == null) continue;

            string effect = component.GetIl2CppType().Name;
            bool disable = ModConfig.ClarityShouldDisable(effect);
            bool wasActive = component.active;
            if (disable && wasActive) component.active = false;

            if (logged.Add($"{volume.name}|{profile.name}|{effect}"))
                Plugin.Logger.LogInfo($"Clarity: volume '{volume.name}' profile '{profile.name}' has {effect} " +
                                      $"(active={wasActive}) -> {(disable ? "disabled" : "left alone")}");
        }
    }
}
