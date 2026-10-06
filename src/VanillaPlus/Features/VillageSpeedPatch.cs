using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VanillaPlus.Features;

// Makes Dave move faster in the Sea People Village only.
//
// The game multiplies Dave's move speed by BuffDataContainer.MoveSpeedParameter, a plain dictionary of
// multipliers that charms and food also feed. One extra entry is added while Dave is in the village and
// removed when he leaves, so dives are unaffected. (Technique documented by WhiteMinds/dave-diver-expansion;
// PlayerCharacter.DetermineMoveSpeed itself cannot be hooked reliably.)
//
// If the village pins Dave to a forced speed instead, that forced value is scaled, and put back afterwards.
// Scripted scenes are left at vanilla speed so nothing gets out of step.
[HarmonyPatch(typeof(PlayerCharacter), nameof(PlayerCharacter.Update))]
internal static class VillageSpeedPatch
{
    private const int SpeedParamId = 987650101;
    private const int MaxLogs = 30;
    private const int MaxAreaLogs = 25;

    private static int playerId;
    private static bool sceneIsVillage;
    private static bool paramApplied;
    private static bool forceApplied;
    private static float forceVanilla;
    private static float forceSet;
    private static int logs;
    private static int areaLogs;

    [HarmonyPostfix]
    private static void Postfix(PlayerCharacter __instance)
    {
        if (!Il2CppGuard.Is<PlayerCharacter>(__instance)) return;

        // Entering another area creates a fresh PlayerCharacter with fresh buff data.
        int id = __instance.GetInstanceID();
        if (id != playerId)
        {
            playerId = id;
            paramApplied = false;
            forceApplied = false;
            sceneIsVillage = IsVillageScene();
            if (areaLogs++ < MaxAreaLogs)
                Plugin.Logger.LogInfo($"VillageSpeed: new area, scene='{SceneName()}', location={__instance.locationState}, " +
                                      $"village scene={sceneIsVillage}, forced speed={(__instance.isForceMoveSpeedOn ? __instance.m_ForceMoveSpeed.ToString("0.###") : "off")}");
        }

        bool inVillage = sceneIsVillage || __instance.locationState == PlayerLocationState.Village;
        bool want = inVillage && !__instance.IsScenarioPlaying;
        bool forced = __instance.isForceMoveSpeedOn;

        SetParam(__instance, want && !forced);
        SetForced(__instance, want && forced);
    }

    private static void SetParam(PlayerCharacter player, bool on)
    {
        if (on == paramApplied) return;

        var buffs = player.m_PlayerBuffHandler?.GetBuffComponents;
        if (buffs == null) return;

        float before = buffs.MoveSpeedParameter;
        buffs.RemoveMoveSpeedParam(SpeedParamId);
        if (on) buffs.AddMoveSpeedParam(SpeedParamId, ModConfig.VillageSpeedMultiplier.Value);
        paramApplied = on;

        Log($"VillageSpeed: boost {(on ? "ON" : "OFF")} via speed parameter, total move multiplier {before:0.###} -> " +
            $"{buffs.MoveSpeedParameter:0.###} (location={player.locationState}, scene='{SceneName()}')");
    }

    private static void SetForced(PlayerCharacter player, bool on)
    {
        float current = player.m_ForceMoveSpeed;

        if (on)
        {
            // Our own value still in place: nothing to do. Anything else is a new vanilla value from the game.
            if (forceApplied && Mathf.Approximately(current, forceSet)) return;

            forceVanilla = current;
            forceSet = current * ModConfig.VillageSpeedMultiplier.Value;
            player.m_ForceMoveSpeed = forceSet;
            forceApplied = true;
            Log($"VillageSpeed: boost ON via forced speed, {forceVanilla:0.###} -> {forceSet:0.###} " +
                $"(location={player.locationState}, scene='{SceneName()}')");
        }
        else if (forceApplied)
        {
            if (Mathf.Approximately(current, forceSet)) player.m_ForceMoveSpeed = forceVanilla;
            forceApplied = false;
            Log($"VillageSpeed: boost OFF, forced speed back to {forceVanilla:0.###}");
        }
    }

    private static bool IsVillageScene()
    {
        string name = SceneName();
        return name.Contains("MermanVillage") || name.StartsWith("MV_");
    }

    private static string SceneName()
    {
        try { return SceneManager.GetActiveScene().name ?? ""; }
        catch { return ""; }
    }

    private static void Log(string message)
    {
        if (logs++ < MaxLogs) Plugin.Logger.LogInfo(message);
    }
}
