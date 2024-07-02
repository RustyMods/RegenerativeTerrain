using System;
using UnityEngine;

namespace RegenerativeTerrain.Behaviors;

public class MineralGrowth : MonoBehaviour, Hoverable
{
    private ZNetView? m_znv;
    private Destructible? m_destructible;
    private MineRock? m_mineRock;

    private HoverText? m_hoverText;

    private string m_name = "";

    private float m_timer;
    private const float m_increment = 0.05f;
    public void Awake()
    {
        m_znv = GetComponent<ZNetView>();
        m_destructible = GetComponent<Destructible>();
        m_mineRock = GetComponent<MineRock>();
        m_hoverText = GetComponent<HoverText>();
        SetTimeCreated();

        if (m_hoverText != null)
        {
            m_name = m_hoverText.m_text;
        }
        
        Destroy(m_destructible);
        Destroy(m_hoverText);
        SetSize(GetSize());
    }

    public void Update()
    {
        float dt = Time.deltaTime;
        Grow(dt);
    }

    private void Grow(float dt)
    {
        if (m_znv == null) return;
        m_timer += dt;
        if (m_timer < GetTimerRate()) return;
        m_timer = 0.0f;

        if (GetSize() >= 1f)
        {
            Respawn();
            m_znv.Destroy();
        }
        SetSize(GetSize() + m_increment);
    }

    private float GetTimerRate() => RegenerativeTerrainPlugin._GrowthRate.Value * 60f / (1 / m_increment);

    private float GetTimeRemaining()
    {
        var remainder = 1f - GetSize();
        return GetTimerRate() * (remainder * (1 / m_increment));
    }

    private void Respawn()
    {
        if (!ZNetScene.instance) return;
        GameObject prefab = ZNetScene.instance.GetPrefab(GetName());
        if (!prefab) return;
        Transform transform1 = gameObject.transform;
        Instantiate(prefab, transform1.position, transform1.rotation);
    }

    private string GetName() => gameObject.name.Replace("(Clone)", string.Empty).Replace("_GROW", string.Empty);

    private float GetSize()
    {
        if (m_znv == null) return 1f;
        if (!m_znv.IsValid() || m_znv.GetZDO() == null) return 1f;
        return m_znv.GetZDO().GetFloat("mineralSize".GetStableHashCode(), 1f);
    }

    private void SetSize(float size)
    {
        if (m_znv == null) return;
        if (!m_znv.IsValid() || m_znv.GetZDO() == null) return;
        gameObject.transform.localScale = new Vector3(size, size, size);
        m_znv.GetZDO().Set("mineralSize".GetStableHashCode(), size);
    }

    private void SetTimeCreated()
    {
        if (m_znv == null) return;
        if (!m_znv.IsValid() || m_znv.GetZDO() == null) return;
        if (GetTimeCreated() != 0L) return;
        m_znv.GetZDO().Set(ZDOVars.s_terrainModifierTimeCreated, (long)ZNet.instance.GetTimeSeconds());
        m_znv.GetZDO().Set("mineralSize".GetStableHashCode(), 0.05f);
    }

    private long GetTimeCreated()
    {
        if (m_znv == null) return 0L;
        if (!m_znv.IsValid() || m_znv.GetZDO() == null) return 0L;
        return m_znv.GetZDO().GetLong(ZDOVars.s_terrainModifierTimeCreated);
    }

    public string GetHoverText()
    {
        TimeSpan time = TimeSpan.FromSeconds(GetTimeRemaining());
        string timer = time.Hours > 0 ? $"{time.Hours:0}:{time.Minutes:00}:{time.Seconds:00}" : time.Minutes > 0 ? $"{time.Minutes:0}:{time.Seconds:00}" : time.Seconds > 0 ? $"{time.Seconds:0}" : "";
        return Localization.instance.Localize(m_name) + "\n" + $"<color=orange>{timer}</color>";
    }

    public string GetHoverName() => m_name;
}