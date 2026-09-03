using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Core stats and status effect container for all combat entities (Heroes and Enemies).
/// Acts as the Single Source of Truth for attributes, emitting state updates via GameEvents.Fighter.
/// </summary>
public class FighterStats : MonoBehaviour
{
    #region Events & Signals

    /// <summary>
    /// Raised whenever any stat modifier or base value changes.
    /// Used by derived classes (like HeroStats) to invalidate their calculation caches.
    /// </summary>
    public event Action OnStatsChanged;

    #endregion

    #region Serialized Fields

    [Header("Fighter Identity & Basic Info")]
    [SerializeField] protected string fighterName;
    [SerializeField] protected Sprite turnBarImage;

    [Header("Base Stats Configuration")]
    [SerializeField] protected int baseMaxHp = 100;
    [SerializeField] protected int baseMaxPp = 50;
    [SerializeField] protected int baseAttack = 20;
    [SerializeField] protected int baseDefense = 15;
    [SerializeField] protected int baseAgility = 10;
    [SerializeField] protected int baseLuck = 5;

    [Header("Elemental Power Base Stats")]
    [SerializeField] protected int basePowerVenus = 100;
    [SerializeField] protected int basePowerMars = 100;
    [SerializeField] protected int basePowerJupiter = 100;
    [SerializeField] protected int basePowerMercury = 100;

    [Header("Elemental Resistance Base Stats")]
    [SerializeField] protected int baseResistVenus = 100;
    [SerializeField] protected int baseResistMars = 100;
    [SerializeField] protected int baseResistJupiter = 100;
    [SerializeField] protected int baseResistMercury = 100;

    #endregion

    #region Private & Protected Fields

    protected Dictionary<StatType, Stat> statsMap = new Dictionary<StatType, Stat>();
    protected List<PsynergySO> psynergies = new List<PsynergySO>();
    protected List<ActiveStatusEffect> activeStatusEffects = new List<ActiveStatusEffect>();

    private int currHp;
    private int currPp;

    #endregion

    #region Centralized Resource Event Dispatchers

    /// <summary>
    /// Central pipeline for HP recalculation, boundary clamping, death state triggers, and UI notification.
    /// </summary>
    public virtual void NotifyHpChanged()
    {
        currHp = Mathf.Clamp(currHp, 0, MaxHp);
        GameEvents.Fighter.TriggerHpChanged(gameObject, currHp, MaxHp);

        if (IsDead)
        {
            ClearAllStatusEffects();
            GameEvents.Fighter.TriggerFighterDied(gameObject);
        }
    }

    /// <summary>
    /// Central pipeline for PP recalculation, boundary clamping, and UI notification.
    /// </summary>
    public virtual void NotifyPpChanged()
    {
        currPp = Mathf.Clamp(currPp, 0, MaxPp);
        GameEvents.Fighter.TriggerPpChanged(gameObject, currPp, MaxPp);
    }

    #endregion

    #region Properties - Identity & Resources

    public string Name { get => fighterName; set => fighterName = value; }
    public Sprite TurnBarImage { get => turnBarImage; set => turnBarImage = value; }
    public List<PsynergySO> Psynergies { get => psynergies; set => psynergies = value; }
    public IReadOnlyList<ActiveStatusEffect> ActiveStatusEffects => activeStatusEffects;

    public bool IsDead => currHp <= 0;

    public int CurrHp
    {
        get => currHp;
        set
        {
            int clampedHp = Mathf.Clamp(value, 0, MaxHp);
            if (currHp != clampedHp)
            {
                currHp = clampedHp;
                NotifyHpChanged();
            }
        }
    }

    public int CurrPp
    {
        get => currPp;
        set
        {
            int clampedPp = Mathf.Clamp(value, 0, MaxPp);
            if (currPp != clampedPp)
            {
                currPp = clampedPp;
                NotifyPpChanged();
            }
        }
    }

    #endregion

    #region Properties - Core & Elemental Stats Accessors

    public virtual int MaxHp => GetStatValue(StatType.MaxHP);
    public virtual int MaxPp => GetStatValue(StatType.MaxPP);
    public virtual int Attack => GetStatValue(StatType.Attack);
    public virtual int Defense => GetStatValue(StatType.Defense);
    public virtual int Agility => GetStatValue(StatType.Agility);
    public virtual int Luck => GetStatValue(StatType.Luck);

    public virtual int PowerVenus => GetStatValue(StatType.PowerVenus);
    public virtual int PowerMars => GetStatValue(StatType.PowerMars);
    public virtual int PowerJupiter => GetStatValue(StatType.PowerJupiter);
    public virtual int PowerMercury => GetStatValue(StatType.PowerMercury);

    public virtual int ResistVenus => GetStatValue(StatType.ResistVenus);
    public virtual int ResistMars => GetStatValue(StatType.ResistMars);
    public virtual int ResistJupiter => GetStatValue(StatType.ResistJupiter);
    public virtual int ResistMercury => GetStatValue(StatType.ResistMercury);

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        InitializeStats();
    }

    protected virtual void Start()
    {
        currHp = MaxHp;
        currPp = MaxPp;

        NotifyHpChanged();
        NotifyPpChanged();
    }

    #endregion

    #region Stats Initialization & Base Operations

    protected virtual void InitializeStats()
    {
        statsMap.Clear();

        statsMap.Add(StatType.MaxHP, new Stat(StatType.MaxHP, baseMaxHp));
        statsMap.Add(StatType.MaxPP, new Stat(StatType.MaxPP, baseMaxPp));
        statsMap.Add(StatType.Attack, new Stat(StatType.Attack, baseAttack));
        statsMap.Add(StatType.Defense, new Stat(StatType.Defense, baseDefense));
        statsMap.Add(StatType.Agility, new Stat(StatType.Agility, baseAgility));
        statsMap.Add(StatType.Luck, new Stat(StatType.Luck, baseLuck));

        statsMap.Add(StatType.PowerVenus, new Stat(StatType.PowerVenus, basePowerVenus));
        statsMap.Add(StatType.PowerMars, new Stat(StatType.PowerMars, basePowerMars));
        statsMap.Add(StatType.PowerJupiter, new Stat(StatType.PowerJupiter, basePowerJupiter));
        statsMap.Add(StatType.PowerMercury, new Stat(StatType.PowerMercury, basePowerMercury));

        statsMap.Add(StatType.ResistVenus, new Stat(StatType.ResistVenus, baseResistVenus));
        statsMap.Add(StatType.ResistMars, new Stat(StatType.ResistMars, baseResistMars));
        statsMap.Add(StatType.ResistJupiter, new Stat(StatType.ResistJupiter, baseResistJupiter));
        statsMap.Add(StatType.ResistMercury, new Stat(StatType.ResistMercury, baseResistMercury));
    }

    public Stat GetStat(StatType type)
    {
        if (statsMap.TryGetValue(type, out var stat))
        {
            return stat;
        }

        Debug.LogError($"[FighterStats] StatType '{type}' not found on {gameObject.name}.");
        return null;
    }

    public int GetStatValue(StatType type)
    {
        if (statsMap.TryGetValue(type, out var stat) && stat != null)
        {
            return stat.Value;
        }
        return 0;
    }

    public void SetStatBaseValue(StatType type, int newBaseValue)
    {
        var stat = GetStat(type);
        if (stat != null)
        {
            stat.BaseValue = newBaseValue;
            OnStatsChanged?.Invoke();

            if (type == StatType.MaxHP) NotifyHpChanged();
            else if (type == StatType.MaxPP) NotifyPpChanged();
        }
    }

    #endregion

    #region Health & Resource Operations

    public bool RestoreHP(int amount)
    {
        if (IsDead || amount <= 0 || CurrHp >= MaxHp) return false;

        CurrHp += amount;
        GameEvents.Fighter.TriggerHealed(gameObject, amount);
        return true;
    }

    public bool RestorePP(int amount)
    {
        if (IsDead || amount <= 0 || CurrPp >= MaxPp) return false;

        CurrPp += amount;
        return true;
    }

    public void ConsumePP(int amount)
    {
        if (IsDead || amount <= 0) return;
        CurrPp -= amount;
    }

    public bool HasEnoughPP(int amount)
    {
        return !IsDead && CurrPp >= amount;
    }

    public void TakeDamage(int rawAmount, bool isCritical = false, ElementalType element = ElementalType.None)
    {
        if (IsDead || rawAmount <= 0) return;

        float mitigationPct = GetDamageMitigationPercentage();
        int finalDamage = Mathf.Max(1, Mathf.FloorToInt(rawAmount * (1f - (mitigationPct / 100f))));

        CurrHp -= finalDamage;
        GameEvents.Fighter.TriggerDamaged(gameObject, finalDamage, isCritical, element);

        CheckDamageBreakStatusEffects();
    }

    public bool Revive(int healthAmount)
    {
        if (!IsDead) return false;

        currHp = Mathf.Clamp(healthAmount, 1, MaxHp);
        NotifyHpChanged();
        GameEvents.Fighter.TriggerFighterRevived(gameObject);
        return true;
    }

    public void AddPermanentStatBoost(StatType type, int amount)
    {
        var stat = GetStat(type);
        if (stat != null)
        {
            stat.BaseValue += amount;
            OnStatsChanged?.Invoke();

            if (type == StatType.MaxHP) CurrHp += amount;
            else if (type == StatType.MaxPP) CurrPp += amount;
        }
    }

    #endregion

    #region Status Effect Management API

    public bool ApplyStatusEffect(StatusEffectSO effectSO, FighterStats caster = null, int overrideDuration = -2)
    {
        if (effectSO == null || IsDead) return false;

        ActiveStatusEffect existingEffect = GetActiveStatusEffect(effectSO);

        if (existingEffect != null)
        {
            if (effectSO.IsStackable)
            {
                bool stacked = existingEffect.AddStack();
                ApplyStatusStatModifiers(existingEffect);
                Debug.Log($"[FighterStats] {fighterName} increased stack count for status: {effectSO.EffectName} (Stacks: {existingEffect.CurrentStacks}).");
                return stacked;
            }
            else
            {
                existingEffect.RefreshDuration(overrideDuration);
                Debug.Log($"[FighterStats] {fighterName} refreshed duration for status: {effectSO.EffectName}.");
                return true;
            }
        }

        ActiveStatusEffect newEffect = new ActiveStatusEffect(effectSO, caster, overrideDuration);
        activeStatusEffects.Add(newEffect);
        ApplyStatusStatModifiers(newEffect);

        GameEvents.Fighter.TriggerStatusEffectAdded(gameObject, newEffect);
        Debug.Log($"[FighterStats] {fighterName} acquired status effect: '{effectSO.EffectName}'.");
        return true;
    }

    public bool RemoveStatusEffect(StatusEffectSO effectSO)
    {
        if (effectSO == null) return false;

        ActiveStatusEffect target = GetActiveStatusEffect(effectSO);
        if (target != null)
        {
            RemoveStatusStatModifiers(target);
            activeStatusEffects.Remove(target);
            GameEvents.Fighter.TriggerStatusEffectRemoved(gameObject, target);
            Debug.Log($"[FighterStats] {fighterName} cured/lost status effect: '{effectSO.EffectName}'.");
            return true;
        }

        return false;
    }

    public int PurgeCategory(StatusEffectCategory category)
    {
        List<ActiveStatusEffect> toRemove = activeStatusEffects.FindAll(e => e.Data != null && e.Data.Category == category);
        int count = 0;

        foreach (var effect in toRemove)
        {
            if (RemoveStatusEffect(effect.Data))
            {
                count++;
            }
        }

        return count;
    }

    public void ClearCombatOnlyStatusEffects()
    {
        List<ActiveStatusEffect> toRemove = activeStatusEffects.FindAll(e => e.Data != null && !e.Data.PersistsAfterBattle);

        foreach (var effect in toRemove)
        {
            RemoveStatusStatModifiers(effect);
            activeStatusEffects.Remove(effect);
            GameEvents.Fighter.TriggerStatusEffectRemoved(gameObject, effect);
        }

        Debug.Log($"[FighterStats] Cleared {toRemove.Count} combat-only status effects on {fighterName}.");
    }

    public void ClearAllStatusEffects()
    {
        if (activeStatusEffects.Count == 0) return;

        foreach (var effect in activeStatusEffects)
        {
            RemoveStatusStatModifiers(effect);
        }

        activeStatusEffects.Clear();
        GameEvents.Fighter.TriggerStatusEffectsCleared(gameObject);
    }

    public bool HasStatusEffect(StatusEffectSO effectSO)
    {
        return GetActiveStatusEffect(effectSO) != null;
    }

    public ActiveStatusEffect GetActiveStatusEffect(StatusEffectSO effectSO)
    {
        return activeStatusEffects.Find(e => e.Data == effectSO || (e.Data != null && e.Data.EffectSlug == effectSO.EffectSlug));
    }

    #endregion

    #region Turn & Overworld Step Execution

    public void ProcessTurnStartStatusEffects()
    {
        if (IsDead) return;

        List<ActiveStatusEffect> expired = new List<ActiveStatusEffect>();

        for (int i = 0; i < activeStatusEffects.Count; i++)
        {
            ActiveStatusEffect effect = activeStatusEffects[i];
            if (effect.Data == null) continue;

            foreach (var regen in effect.Data.ResourceRegens)
            {
                ApplyResourceRegen(regen, effect.CurrentStacks);
            }

            foreach (var dot in effect.Data.DamagesOverTime)
            {
                ApplyDamageOverTime(dot, effect.CurrentStacks);
            }

            bool isExpired = effect.TickTurn();
            if (isExpired)
            {
                expired.Add(effect);
            }
        }

        foreach (var exp in expired)
        {
            RemoveStatusEffect(exp.Data);
        }
    }

    public void ProcessOverworldStep()
    {
        if (IsDead) return;

        for (int i = 0; i < activeStatusEffects.Count; i++)
        {
            ActiveStatusEffect effect = activeStatusEffects[i];
            if (effect.Data == null || !effect.Data.TicksInOverworld) continue;

            bool triggerTick = effect.OnOverworldStep();
            if (triggerTick)
            {
                foreach (var dot in effect.Data.DamagesOverTime)
                {
                    ApplyDamageOverTime(dot, effect.CurrentStacks);
                }

                foreach (var regen in effect.Data.ResourceRegens)
                {
                    ApplyResourceRegen(regen, effect.CurrentStacks);
                }
            }
        }
    }

    #endregion

    #region Crowd Control & Defense Queries

    public bool IsIncapacitated()
    {
        if (IsDead) return true;

        foreach (var effect in activeStatusEffects)
        {
            if (effect.Data != null && effect.Data.EffectType == StatusEffectType.Incapacitate)
            {
                IncapacitateType type = effect.Data.IncapacitateType;
                if (type == IncapacitateType.Stun || type == IncapacitateType.Sleep || type == IncapacitateType.Freeze)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public IncapacitateType GetIncapacitateType()
    {
        foreach (var effect in activeStatusEffects)
        {
            if (effect.Data != null && effect.Data.EffectType == StatusEffectType.Incapacitate)
            {
                return effect.Data.IncapacitateType;
            }
        }

        return IncapacitateType.None;
    }

    public float GetDamageMitigationPercentage()
    {
        float totalMitigation = 0f;

        foreach (var effect in activeStatusEffects)
        {
            if (effect.Data != null && effect.Data.EffectType == StatusEffectType.DamageMitigation)
            {
                totalMitigation += effect.Data.DamageMitigationPercentage;
            }
        }

        return Mathf.Clamp(totalMitigation, 0f, 100f);
    }

    #endregion

    #region Private Helper Calculations

    private void ApplyStatusStatModifiers(ActiveStatusEffect activeEffect)
    {
        if (activeEffect?.Data == null) return;

        RemoveModifiersFromSource(activeEffect);

        foreach (StatBonus bonus in activeEffect.Data.StatModifiers)
        {
            if (bonus == null) continue;

            // Zero-allocation modifier construction
            float scaledValue = bonus.value * activeEffect.CurrentStacks;
            StatModifier mod = new StatModifier(scaledValue, bonus.modifierType, activeEffect, isTemporary: true);

            AddStatModifier(bonus.statType, mod);
        }
    }

    private void RemoveStatusStatModifiers(ActiveStatusEffect activeEffect)
    {
        if (activeEffect != null)
        {
            RemoveModifiersFromSource(activeEffect);
        }
    }

    private void ApplyResourceRegen(StatusResourceRegen regen, int stacks)
    {
        if (regen == null) return;

        int amount = 0;
        if (regen.targetResource == ResourceTarget.HP)
        {
            amount = (regen.valueType == StatModifierType.PercentTotal)
                ? Mathf.FloorToInt((MaxHp * regen.NormalizedAmount) * stacks)
                : Mathf.FloorToInt(regen.amountPerTurn * stacks);

            if (amount > 0) RestoreHP(amount);
            else if (amount < 0) TakeDamage(Mathf.Abs(amount));
        }
        else if (regen.targetResource == ResourceTarget.PP)
        {
            amount = (regen.valueType == StatModifierType.PercentTotal)
                ? Mathf.FloorToInt((MaxPp * regen.NormalizedAmount) * stacks)
                : Mathf.FloorToInt(regen.amountPerTurn * stacks);

            if (amount > 0) RestorePP(amount);
            else if (amount < 0) ConsumePP(Mathf.Abs(amount));
        }
    }

    private void ApplyDamageOverTime(StatusDamageOverTime dot, int stacks)
    {
        if (dot == null) return;

        int damage = (dot.damageType == StatModifierType.PercentTotal)
            ? Mathf.FloorToInt((MaxHp * dot.NormalizedDamage) * stacks)
            : Mathf.FloorToInt(dot.damagePerTurn * stacks);

        if (damage > 0)
        {
            TakeDamage(damage);
            Debug.Log($"[FighterStats] {fighterName} suffered {damage} DoT damage.");
        }
    }

    private void CheckDamageBreakStatusEffects()
    {
        List<ActiveStatusEffect> toBreak = activeStatusEffects.FindAll(e => e.Data != null && e.Data.CureOnPhysicalDamage);

        foreach (var effect in toBreak)
        {
            Debug.Log($"[FighterStats] {fighterName}'s {effect.Data.EffectName} broke due to taking physical damage!");
            RemoveStatusEffect(effect.Data);
        }
    }

    #endregion

    #region Base Modifier Operations

    public void AddStatModifier(StatType type, StatModifier modifier)
    {
        var stat = GetStat(type);
        if (stat == null) return;

        stat.AddModifier(modifier);
        OnStatsChanged?.Invoke();
    }

    public void RemoveModifiersFromSource(object source)
    {
        bool removedAny = false;
        foreach (var stat in statsMap.Values)
        {
            if (stat.RemoveAllModifiersFromSource(source))
            {
                removedAny = true;
            }
        }

        if (removedAny)
        {
            OnStatsChanged?.Invoke();
        }
    }

    public void TickTurnModifiers()
    {
        foreach (var stat in statsMap.Values)
        {
            stat.TickTurnModifiers();
        }
        OnStatsChanged?.Invoke();
    }

    public void RemoveAllTemporaryModifiers()
    {
        foreach (var stat in statsMap.Values)
        {
            stat.RemoveAllTemporaryModifiers();
        }
        OnStatsChanged?.Invoke();
    }

    #endregion
}