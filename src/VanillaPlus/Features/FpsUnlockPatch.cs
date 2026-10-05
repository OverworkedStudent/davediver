using HarmonyLib;
using UnityEngine;

namespace VanillaPlus.Features;

// The game owns frame pacing in CameraResolution (VSync option plus a watchdog that re-applies its limiter),
// so the cap is re-asserted right after CameraResolution.Update each frame instead of being set once.
// VSync has to be off for Application.targetFrameRate to take effect.
[HarmonyPatch(typeof(CameraResolution))]
internal static class FpsUnlockPatch
{
    private const int MaxReapplyLogs = 10;

    private static bool loggedVanilla;
    private static int reapplyCount;

    [HarmonyPatch(nameof(CameraResolution.Update))]
    [HarmonyPostfix]
    private static void UpdatePostfix(CameraResolution __instance)
    {
        int target = ModConfig.TargetFPS.Value;
        int currentTarget = Application.targetFrameRate;
        int currentVSync = QualitySettings.vSyncCount;

        if (!loggedVanilla)
        {
            loggedVanilla = true;
            Plugin.Logger.LogInfo($"FPS: vanilla state targetFrameRate={currentTarget}, vSyncCount={currentVSync}, " +
                                  $"game VSync option={__instance.m_VSyncValue}, display={Screen.currentResolution.refreshRateRatio.value:0.##}Hz. " +
                                  $"Applying targetFrameRate={target}, vSyncCount=0");
        }
        else if (currentTarget != target || currentVSync != 0)
        {
            reapplyCount++;
            if (reapplyCount <= MaxReapplyLogs)
                Plugin.Logger.LogInfo($"FPS: game reset frame pacing (targetFrameRate={currentTarget}, vSyncCount={currentVSync}), " +
                                      $"re-applying {target} (#{reapplyCount}{(reapplyCount == MaxReapplyLogs ? ", further resets not logged" : "")})");
        }

        if (currentVSync != 0) QualitySettings.vSyncCount = 0;
        if (currentTarget != target) Application.targetFrameRate = target;
    }
}
