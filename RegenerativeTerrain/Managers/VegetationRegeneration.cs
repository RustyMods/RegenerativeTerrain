using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using RegenerativeTerrain.Behaviors;
using UnityEngine;

namespace RegenerativeTerrain.Managers;

public static class VegetationRegeneration
{
    private static readonly List<string> m_exclude = new()
    {
        "fenrirhide_hanging", "fenrirhide_hanging_door",
        "HugeRoot1", "Leviathan", "LeviathanLava",
        "stoneblock_fracture"
    };
    
    private static readonly Dictionary<string, string> m_vegetationMap = new();
    private static readonly Dictionary<string, string> m_mineRockNames = new();
    private static readonly Dictionary<string, string> m_treeStubMap = new();
    private static readonly Dictionary<string, List<string>> m_plantMap = new();

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class MapVegetation
    {
        private static void Postfix(ZNetScene __instance)
        {
            if (!__instance) return;

            List<GameObject> prefabsToRegister = new();
            foreach (GameObject? prefab in __instance.m_prefabs)
            {
                if (m_exclude.Contains(prefab.name)) continue;
                if (prefab.GetComponent<MineRock>())
                {
                    CreateMineral(prefab, ref prefabsToRegister);
                    continue;
                }

                if (prefab.TryGetComponent(out TreeBase treeBase))
                {
                    if (treeBase.m_stubPrefab == null) continue;
                    m_treeStubMap[treeBase.m_stubPrefab.name] = prefab.name;
                    continue;
                }

                if (prefab.TryGetComponent(out Plant plant))
                {
                    m_plantMap[prefab.name] = plant.m_grownPrefabs.Select(x => x.name).ToList();
                    continue;
                }
                
                if (!prefab.TryGetComponent(out Destructible component)) continue;
                if (component.m_spawnWhenDestroyed == null)
                {
                    if (prefab.GetComponent<SpawnArea>() || prefab.GetComponent<CreatureSpawner>()) continue;
                    if (!prefab.GetComponent<HoverText>()) continue;

                    if (component.m_destructibleType is DestructibleType.Default)
                    {
                        CreateMineral(prefab, ref prefabsToRegister);
                    }
                }
                else
                {
                    if (!component.m_spawnWhenDestroyed.TryGetComponent(out MineRock5 mineRock5)) continue;
                    m_vegetationMap[component.m_spawnWhenDestroyed.name] = prefab.name;
                    if (mineRock5.m_name.IsNullOrWhiteSpace() || mineRock5.m_name == "Rock")
                    {
                        mineRock5.m_name = $"KEY_{prefab.name}";
                    }
                    m_mineRockNames[mineRock5.m_name] = component.m_spawnWhenDestroyed.name;
                }
            }

            foreach (GameObject? mineral in prefabsToRegister)
            {
                if (__instance.m_prefabs.Contains(mineral)) continue;
                __instance.m_prefabs.Add(mineral);
                __instance.m_namedPrefabs[mineral.name.GetStableHashCode()] = mineral;
            }
        }
    }

    [HarmonyPatch(typeof(Destructible), nameof(Destructible.Destroy))]
    private static class Destructible_Destroy_Patch
    {
        private static void Postfix(Destructible __instance)
        {
            if (RegenerativeTerrainPlugin._RegenerateVegetation.Value is RegenerativeTerrainPlugin.Toggle.Off) return;
            if (!__instance || !ZNetScene.instance) return;
            if (!CheckArea(__instance.transform.position)) return;
            if (IsExcluded(__instance.gameObject)) return;

            if (__instance.m_destructibleType is DestructibleType.Tree)
            {
                SpawnSapling(__instance);
            }
            else
            {
                if (RegenerativeTerrainPlugin._ExcludeOres.Value is RegenerativeTerrainPlugin.Toggle.On && IsMineOre(__instance)) return;
                SpawnMineral(__instance);
            }
            
        }
    }

    private static void SpawnSapling(Destructible __instance)
    {
        if (!m_treeStubMap.TryGetValue(__instance.name.Replace("(Clone)", string.Empty), out string tree)) return;
        string sapling = GetSapling(tree.Replace("_aut", string.Empty));
        if (sapling.IsNullOrWhiteSpace()) return;
        Debug.LogWarning("found sapling " + sapling);
        GameObject prefab = ZNetScene.instance.GetPrefab(sapling);
        if (!prefab) return;

        Transform transform = __instance.transform;
        Object.Instantiate(prefab, transform.position, transform.rotation);
    }

    private static string GetSapling(string tree)
    {
        foreach (KeyValuePair<string, List<string>> kvp in m_plantMap.Where(kvp => kvp.Value.Contains(tree)))
        {
            return kvp.Key;
        }

        return "";
    }

    [HarmonyPatch(typeof(MineRock), nameof(MineRock.AllDestroyed))]
    private static class MineRock_Patch
    {
        private static void Postfix(MineRock __instance, bool __result)
        {
            if (!__instance || !__result || !__instance.m_removeWhenDestroyed) return;
            if (RegenerativeTerrainPlugin._RegenerateVegetation.Value is RegenerativeTerrainPlugin.Toggle.Off) return;
            if (!CheckArea(__instance.transform.position)) return;
            if (IsExcluded(__instance.gameObject)) return;
            if (RegenerativeTerrainPlugin._ExcludeOres.Value is RegenerativeTerrainPlugin.Toggle.On && IsMineOre(__instance)) return;
            SpawnMineral(__instance);
        }
    }

    [HarmonyPatch(typeof(Localization), nameof(Localization.Localize), typeof(string))]
    private static class Localization_Localize_Patch
    {
        private static void Postfix(string text, ref string __result)
        {
            if (m_mineRockNames.ContainsKey(text))
            {
                __result = "";
            }
        }
    }
    
    
    [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.CheckForUpdate))]
    private static class MineRock5_Patch
    {
        private static void Postfix(MineRock5 __instance)
        {
            if (RegenerativeTerrainPlugin._RegenerateVegetation.Value is RegenerativeTerrainPlugin.Toggle.Off) return;
            if (!__instance) return;
            Respawn(__instance);
        }
    }

    private static bool IsExcluded(GameObject prefab)
    {
        if (RegenerativeTerrainPlugin._ExclusionMap.Value.IsNullOrWhiteSpace()) return false;
        string[] map = RegenerativeTerrainPlugin._ExclusionMap.Value.Split(':');
        return map.Contains(prefab.name.Replace("(Clone)", string.Empty));
    }

    private static void CreateMineral(GameObject prefab, ref List<GameObject> prefabsToRegister)
    {
        GameObject mineral = Object.Instantiate(prefab, RegenerativeTerrainPlugin._Root.transform, false);
        mineral.name = prefab.name + "_GROW";
        mineral.AddComponent<MineralGrowth>();
        prefabsToRegister.Add(mineral);
    }

    private static void SpawnMineral(Destructible __instance)
    {
        string name = __instance.name.Replace("(Clone)", string.Empty) + "_GROW";
        Debug.LogWarning(name);
        GameObject mineral = ZNetScene.instance.GetPrefab(name);
        if (!mineral) return;
        Transform transform = __instance.transform;
        Object.Instantiate(mineral, transform.position, transform.rotation);
    }

    private static void SpawnMineral(MineRock __instance)
    {
        GameObject mineral = ZNetScene.instance.GetPrefab(__instance.name.Replace("(Clone)", string.Empty) + "_GROW");
        if (!mineral) return;
        Transform transform = __instance.transform;
        Object.Instantiate(mineral, transform.position, transform.rotation);
    }

    private static void Respawn(MineRock5 __instance)
    {
        if (!ZNet.instance) return;
        if (!CheckArea(__instance.transform.position)) return;
        if (!__instance.m_nview || !__instance.m_nview.IsValid() || __instance.m_nview.GetZDO() == null) return;
        if (RegenerativeTerrainPlugin._ExcludeOres.Value is RegenerativeTerrainPlugin.Toggle.On && IsMineOre(__instance)) return;
        if (GetTimeCreated(__instance) == 0L) SetTimeCreated(__instance, (long)ZNet.instance.GetTimeSeconds());
        if (GetTimeCreated(__instance) + RegenerativeTerrainPlugin._RespawnTime.Value > ZNet.instance.GetTimeSeconds()) return;
        if (!Spawn(__instance)) return;
        QueueDestruction(__instance);
    }

    private static void QueueDestruction(MineRock5 __instance)
    {
        __instance.m_nview.Destroy();
        __instance.m_allDestroyed = true;
    }

    private static bool Spawn(MineRock5 __instance)
    {
        if (!m_vegetationMap.TryGetValue(GetName(__instance), out string parentPrefab)) return false;
        GameObject parent = ZNetScene.instance.GetPrefab(parentPrefab);
        if (!parent) return false;
        if (IsExcluded(parent)) return false;
        Transform transform = __instance.transform;
        GameObject clone = Object.Instantiate(parent, transform.position, transform.rotation);
        clone.transform.localScale = __instance.gameObject.transform.localScale;
        return true;
    }

    private static bool IsMineOre(MineRock5 __instance)
    {
        if (RegenerativeTerrainPlugin._ExcludeOres.Value is RegenerativeTerrainPlugin.Toggle.Off) return false;
        foreach (DropTable.DropData drop in __instance.m_dropItems.m_drops)
        {
            if (!drop.m_item.TryGetComponent(out ItemDrop component)) continue;
            if (component.m_itemData.m_shared.m_teleportable) continue;
            return true;
        }
        return false;
    }

    private static bool IsMineOre(Destructible __instance)
    {
        if (!__instance.TryGetComponent(out DropOnDestroyed component)) return false;
        foreach (DropTable.DropData drop in component.m_dropWhenDestroyed.m_drops)
        {
            if (!drop.m_item.TryGetComponent(out ItemDrop itemDrop)) continue;
            if (itemDrop.m_itemData.m_shared.m_teleportable) continue;
            return true;
        }

        return false;
    }

    private static bool IsMineOre(MineRock __instance)
    {
        foreach (DropTable.DropData drop in __instance.m_dropItems.m_drops)
        {
            if (!drop.m_item.TryGetComponent(out ItemDrop itemDrop)) continue;
            if (itemDrop.m_itemData.m_shared.m_teleportable) continue;
            return true;
        }
        return false;
    }
    
    private static bool CheckArea(Vector3 position)
    {
        if (RegenerativeTerrainPlugin._CraftingStationBlocks.Value is RegenerativeTerrainPlugin.Toggle.Off) return true;
        if (!ZNetScene.instance) return false;
        foreach (ZDO? temp in ZNetScene.instance.m_tempCurrentObjects)
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(temp.m_prefab);
            if (!prefab) continue;
            if (!prefab.GetComponent<CraftingStation>()) continue;
            if (Utils.DistanceXZ(position, temp.m_position) > RegenerativeTerrainPlugin._CraftingStationRadius.Value) continue;
            return false;
        }
        return true;
    }

    private static long GetTimeCreated(MineRock5 __instance) => __instance.m_nview.GetZDO().GetLong(ZDOVars.s_terrainModifierTimeCreated);

    private static void SetTimeCreated(MineRock5 __instance, long time) => __instance.m_nview.GetZDO().Set(ZDOVars.s_terrainModifierTimeCreated, time);

    private static string GetName(MineRock5 __instance)
    {
        return __instance.m_name.IsNullOrWhiteSpace() ? __instance.gameObject.name :
            m_mineRockNames.TryGetValue(__instance.m_name, out string name) ? name : "";
    }
}