using HarmonyLib;
using UnityEngine;

namespace RegenerativeTerrain.Managers;

public static class TerrainRegeneration
{
    private static void SetTimeModified(TerrainComp __instance)
    {
        if (!ZNet.instance) return;
        if (!__instance.m_nview || !__instance.m_nview.IsValid() || __instance.m_nview.GetZDO() == null) return;
        __instance.m_nview.ClaimOwnership();
        __instance.m_nview.GetZDO().Set(ZDOVars.s_terrainModifierTimeCreated, (long)ZNet.instance.GetTimeSeconds());
    }
    
    private static bool CheckArea(TerrainComp __instance)
    {
        if (RegenerativeTerrainPlugin._CraftingStationBlocks.Value is RegenerativeTerrainPlugin.Toggle.Off) return true;
        if (!ZNetScene.instance) return false;
        foreach (ZDO? temp in ZNetScene.instance.m_tempCurrentObjects)
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(temp.m_prefab);
            if (!prefab) continue;
            if (!prefab.GetComponent<CraftingStation>()) continue;
            if (Utils.DistanceXZ(__instance.gameObject.transform.position, temp.m_position) > RegenerativeTerrainPlugin._CraftingStationRadius.Value) continue;

            long lastModified = __instance.m_nview.GetZDO().GetLong(ZDOVars.s_terrainModifierTimeCreated);
            if (lastModified + 60 < ZNet.instance.GetTimeSeconds())
            {
                __instance.m_nview.GetZDO().Set(ZDOVars.s_terrainModifierTimeCreated, (long)ZNet.instance.GetTimeSeconds());
            }
            return false;
        }
        return true;
    }

    private static void Regenerate(TerrainComp __instance, bool poke = false)
    {
        float modifier = GetModifier(__instance);
        if (modifier >= 0.95f) return;
        RegenerateTerrain(__instance, modifier);
        RegeneratePaint(__instance, modifier);
        if (!poke) RegenerateGrass(__instance, modifier);
        SaveAndRemove(__instance, modifier);
        if (poke) __instance.m_hmap.Poke(true);
        
        RegenerativeTerrainPlugin.RegenerativeTerrainLogger.LogDebug($"Regenerated terrain: {(int)((1 - modifier) * 100)}%");
    }

    private static void SaveAndRemove(TerrainComp __instance, float modifier)
    {
        __instance.Save();
        if (modifier > 0f) return;
        __instance.m_nview.GetZDO().RemoveLong(ZDOVars.s_terrainModifierTimeCreated);
    }

    private static float GetModifier(TerrainComp __instance)
    {
        if (!ZNet.instance) return 1f;
        long modifiedTime = __instance.m_nview.GetZDO().GetLong(ZDOVars.s_terrainModifierTimeCreated);
        if (modifiedTime == 0L) return 1f;
        double difference = ZNet.instance.GetTimeSeconds() - modifiedTime;
        return Mathf.Clamp01(1 - (float)difference / (RegenerativeTerrainPlugin._RegenerationTime.Value * 30f));
    }

    private static void RegenerateGrass(TerrainComp __instance, float modifier)
    {
        if (!ClutterSystem.instance) return;
        if (modifier > 0f) return;
        ClutterSystem.instance.ResetGrass(__instance.m_lastOpPoint, __instance.m_lastOpRadius);
    }

    private static void RegenerateTerrain(TerrainComp __instance, float modifier)
    {
        for (int index = 0; index < __instance.m_modifiedHeight.Length; index++)
        {
            if (!__instance.m_modifiedHeight[index]) continue;
                    
            __instance.m_levelDelta[index] *= modifier;
            __instance.m_smoothDelta[index] *= modifier;

            if (modifier <= 0f)
            {
                __instance.m_modifiedHeight[index] = false;
            }
        }
    }

    private static void RegeneratePaint(TerrainComp __instance, float modifier)
    {
        if (!ModifyPaint()) return;
        for (int index = 0; index < __instance.m_modifiedPaint.Length; ++index)
        {
            if (!__instance.m_modifiedPaint[index]) continue;

            Color color = __instance.m_paintMask[index];

            __instance.m_paintMask[index] = new Color()
            {
                r = RegenerativeTerrainPlugin._ResetDirt.Value is RegenerativeTerrainPlugin.Toggle.On ? color.r * modifier : color.r,
                g = RegenerativeTerrainPlugin._ResetCultivate.Value is RegenerativeTerrainPlugin.Toggle.On ? color.g * modifier : color.g,
                b = RegenerativeTerrainPlugin._ResetPaved.Value is RegenerativeTerrainPlugin.Toggle.On ? color.b * modifier : color.b,
                a = color.a
            };

            if (modifier <= 0f && SwitchPaintBool())
            {
                __instance.m_modifiedPaint[index] = false;
            }
        }
    }

    private static bool ModifyPaint()
    {
        return RegenerativeTerrainPlugin._ResetDirt.Value is RegenerativeTerrainPlugin.Toggle.On ||
               RegenerativeTerrainPlugin._ResetPaved.Value is RegenerativeTerrainPlugin.Toggle.On ||
               RegenerativeTerrainPlugin._ResetCultivate.Value is RegenerativeTerrainPlugin.Toggle.On;
    }

    private static bool SwitchPaintBool()
    {
        return RegenerativeTerrainPlugin._ResetDirt.Value is RegenerativeTerrainPlugin.Toggle.On &&
               RegenerativeTerrainPlugin._ResetPaved.Value is RegenerativeTerrainPlugin.Toggle.On &&
               RegenerativeTerrainPlugin._ResetCultivate.Value is RegenerativeTerrainPlugin.Toggle.On;
    }
    
    [HarmonyPatch(typeof(TerrainComp), nameof(TerrainComp.ApplyOperation))]
    private static class TerrainComp_Apply_Patch
    {
        private static void Postfix(TerrainComp __instance)
        {
            if (!__instance) return;
            SetTimeModified(__instance);
        }
    }

    [HarmonyPatch(typeof(TerrainComp), nameof(TerrainComp.Load))]
    private static class TerrainComp_Load_Patch
    {
        private static void Postfix(TerrainComp __instance, bool __result)
        {
            if (RegenerativeTerrainPlugin._Enabled.Value is RegenerativeTerrainPlugin.Toggle.Off) return;
            if (!__instance || !__result) return;
            if (!__instance.m_nview || !__instance.m_nview.IsValid() || __instance.m_nview.GetZDO() == null) return;
            if (!CheckArea(__instance)) return;
            Regenerate(__instance);
        }
    }

    private static float m_timer;
    [HarmonyPatch(typeof(TerrainComp), nameof(TerrainComp.Update))]
    private static class TerrainComp_Update_Patch
    {
        private static void Postfix(TerrainComp __instance)
        {
            if (!__instance) return;
            if (RegenerativeTerrainPlugin._Enabled.Value is RegenerativeTerrainPlugin.Toggle.Off) return;
            if (!__instance.m_nview || !__instance.m_nview.IsValid() || __instance.m_nview.GetZDO() == null) return;
            m_timer += Time.fixedDeltaTime;
            if (m_timer < RegenerativeTerrainPlugin._UpdateDelay.Value) return;
            m_timer = 0.0f;
            if (!CheckArea(__instance)) return;
            Regenerate(__instance, true);
        }
    }
}