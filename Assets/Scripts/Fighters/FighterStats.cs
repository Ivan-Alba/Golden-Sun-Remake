using System;
using System.Collections.Generic;
using UnityEngine;

public class FighterStats : MonoBehaviour
{
    #region Events

    // Events for UI and system updates (passes current value and max value)
    public event Action<int, int> OnHpChanged;
    public event Action<int, int> OnPpChanged;

    // Events for Status Effects HUD & combat feedback
    public event Action<ActiveStatusEffect> OnStatusEffectAdded;
    public event Action<ActiveStatusEffect> OnStatusEffectRemoved;
    public event Action OnStatusEffectsCleared;

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
                OnHpChanged?.Invoke(currHp, MaxHp);
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
                OnPpChanged?.Invoke(currPp, MaxPp);
            }
        }
    }

    #endregion

    #region Properties - Core Stats Accessors

    public int MaxHp => GetStatValue(StatType.MaxHP);
    public int MaxPp => GetStatValue(StatType.MaxPP);
    public int Attack => GetStatValue(StatType.Attack);
    public int Defense => GetStatValue(StatType.Defense);
    public int Agility => GetStatValue(StatType.Agility);
    public int Luck => GetStatValue(StatType.Luck);

    // Elemental Power Accessors
    public int PowerVenus => GetStatValue(StatType.PowerVenus);
    public int PowerMars => GetStatValue(StatType.PowerMars);
    public int PowerJupiter => GetStatValue(StatType.PowerJupiter);
    public int PowerMercury => GetStatValue(StatType.PowerMercury);

    // Elemental Resistance Accessors
    public int ResistVenus => GetStatValue(StatType.ResistVenus);
    public int ResistMars => GetStatValue(StatType.ResistMars);
    public int ResistJupiter => GetStatValue(StatType.ResistJupiter);
    public int ResistMercury => GetStatValue(StatType.ResistMercury);

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        InitializeStats();
    }

    protected virtual void Start()
    {
        // Set current resources to max on start
        currHp = MaxHp;
        currPp = MaxPp;

        // Force UI initial notification
        OnHpChanged?.Invoke(currHp, MaxHp);
        OnPpChanged?.Invoke(currPp, MaxPp);
    }

    #endregion

    #region Stats Initialization & Helper Methods

    protected virtual void InitializeStats()
    {
        statsMap.Clear();

        // Core Stats
        statsMap.Add(StatType.MaxHP, new Stat(StatType.MaxHP, baseMaxHp));
        statsMap.Add(StatType.MaxPP, new Stat(StatType.MaxPP, baseMaxPp));
        statsMap.Add(StatType.Attack, new Stat(StatType.Attack, baseAttack));
        statsMap.Add(StatType.Defense, new Stat(StatType.Defense, baseDefense));
        statsMap.Add(StatType.Agility, new Stat(StatType.Agility, baseAgility));
        statsMap.Add(StatType.Luck, new Stat(StatType.Luck, baseLuck));

        // Elemental Powers
        statsMap.Add(StatType.PowerVenus, new Stat(StatType.PowerVenus, basePowerVenus));
        statsMap.Add(StatType.PowerMars, new Stat(StatType.PowerMars, basePowerMars));
        statsMap.Add(StatType.PowerJupiter, new Stat(StatType.PowerJupiter, basePowerJupiter));
        statsMap.Add(StatType.PowerMercury, new Stat(StatType.PowerMercury, basePowerMercury));

        // Elemental Resistances
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
        var stat = GetStat(type);
        return stat != null ? stat.Value : 0;
    }

    /// <summary>
    /// Changes the base value of a stat and automatically updates resources/UI if necessary.
    /// </summary>
    public void SetStatBaseValue(StatType type, int newBaseValue)
    {
        var stat = GetStat(type);
        if (stat != null)
        {
            stat.BaseValue = newBaseValue;

            if (type == StatType.MaxHP)
            {
                CurrHp = Mathf.Clamp(CurrHp, 0, MaxHp);
                OnHpChanged?.Invoke(CurrHp, MaxHp);
            }
            else if (type == StatType.MaxPP)
            {
                CurrPp = Mathf.Clamp(CurrPp, 0, MaxPp);
                OnPpChanged?.Invoke(CurrPp, MaxPp);
            }
        }
    }

    #endregion

    #region Health & Resource Operations

    /// <summary>
    /// Restores HP to the fighter if alive and not already at max HP.
    /// </summary>
    public bool RestoreHP(int amount)
    {
        if (IsDead || amount <= 0 || CurrHp >= MaxHp) return false;

        CurrHp += amount;
        return true;
    }

    /// <summary>
    /// Restores PP to the fighter if alive and not already at max PP.
    /// </summary>
    public bool RestorePP(int amount)
    {
        if (IsDead || amount <= 0 || CurrPp >= MaxPp) return false;

        CurrPp += amount;
        return true;
    }

    /// <summary>
    /// Consumes the specified amount of PP from the fighter.
    /// </summary>
    public void ConsumePP(int amount)
    {
        if (IsDead || amount <= 0) return;
        CurrPp -= amount;
    }

    /// <summary>
    /// Checks if the fighter is alive and has at least the specified amount of PP.
    /// </summary>
    public bool HasEnoughPP(int amount)
    {
        return !IsDead && CurrPp >= amount;
    }

    /// <summary>
    /// Applies damage to the fighter, taking active damage mitigation shields into account.
    /// Also checks for physical damage breaking active status effects (e.g. Sleep).
    /// </summary>
    public void TakeDamage(int rawAmount)
    {
        if (IsDead || rawAmount <= 0) return;

        // Apply mitigation percentage (e.g., Granite, Protection shields)
        float mitigationPct = GetDamageMitigationPercentage();
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(rawAmount * (1f - (mitigationPct / 100f))));

        CurrHp -= finalDamage;

        // Check if physical damage breaks effects like Sleep
        CheckDamageBreakStatusEffects();
    }

    /// <summary>
    /// Revives a defeated fighter with the specified health amount.
    /// </summary>
    public bool Revive(int healthAmount)
    {
        if (!IsDead) return false;

        CurrHp = Mathf.Clamp(healthAmount, 1, MaxHp);
        return true;
    }

    public void AddPermanentStatBoost(StatType type, int amount)
    {
        var stat = GetStat(type);
        if (stat != null)
        {
            stat.BaseValue += amount;

            if (type == StatType.MaxHP) CurrHp += amount;
            if (type == StatType.MaxPP) CurrPp += amount;
        }
    }

    #endregion

    #region Status Effect Management API

    /// <summary>
    /// Applies a status effect template to this fighter. Handles stacking and duration refreshing.
    /// </summary>
    public bool ApplyStatusEffect(StatusEffectSO effectSO, FighterStats caster = null, int overrideDuration = -2)
    {
        if (effectSO == null || IsDead) return false;

        ActiveStatusEffect existingEffect = GetActiveStatusEffect(effectSO);

        if (existingEffect != null)
        {
            if (effectSO.IsStackable)
            {
                bool stacked = existingEffect.AddStack();
                ApplyStatusStatModifiers(existingEffect); // Re-apply modifiers with new stack count if necessary
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

        OnStatusEffectAdded?.Invoke(newEffect);
        Debug.Log($"[FighterStats] {fighterName} acquired status effect: '{effectSO.EffectName}'.");
        return true;
    }

    /// <summary>
    /// Removes a specific active status effect instance from this fighter.
    /// </summary>
    public bool RemoveStatusEffect(StatusEffectSO effectSO)
    {
        if (effectSO == null) return false;

        ActiveStatusEffect target = GetActiveStatusEffect(effectSO);
        if (target != null)
        {
            RemoveStatusStatModifiers(target);
            activeStatusEffects.Remove(target);
            OnStatusEffectRemoved?.Invoke(target);
            Debug.Log($"[FighterStats] {fighterName} cured/lost status effect: '{effectSO.EffectName}'.");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes all active status effects belonging to a specific category (e.g. Ailments, Buffs, Debuffs).
    /// </summary>
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

    /// <summary>
    /// Clears status effects that are marked as combat-only (!PersistsAfterBattle) at the end of combat.
    /// </summary>
    public void ClearCombatOnlyStatusEffects()
    {
        List<ActiveStatusEffect> toRemove = activeStatusEffects.FindAll(e => e.Data != null && !e.Data.PersistsAfterBattle);

        foreach (var effect in toRemove)
        {
            RemoveStatusStatModifiers(effect);
            activeStatusEffects.Remove(effect);
            OnStatusEffectRemoved?.Invoke(effect);
        }

        Debug.Log($"[FighterStats] Cleared {toRemove.Count} combat-only status effects on {fighterName}.");
    }

    /// <summary>
    /// Clears all active status effects unconditionally.
    /// </summary>
    public void ClearAllStatusEffects()
    {
        foreach (var effect in activeStatusEffects)
        {
            RemoveStatusStatModifiers(effect);
        }

        activeStatusEffects.Clear();
        OnStatusEffectsCleared?.Invoke();
    }

    /// <summary>
    /// Checks if a specific status effect is currently active on this fighter.
    /// </summary>
    public bool HasStatusEffect(StatusEffectSO effectSO)
    {
        return GetActiveStatusEffect(effectSO) != null;
    }

    /// <summary>
    /// Gets the active status effect instance associated with a ScriptableObject template if present.
    /// </summary>
    public ActiveStatusEffect GetActiveStatusEffect(StatusEffectSO effectSO)
    {

        return activeStatusEffects.Find(e => e.Data == effectSO || (e.Data != null && e.Data.EffectSlug == effectSO.EffectSlug));
    }

    #endregion

    #region Turn & Overworld Step Execution

    /// <summary>
    /// Executes turn start ticks for all active status effects: DoT damage, resource regeneration, auto-cure checks, and duration countdowns.
    /// Called at the beginning of this fighter's turn in combat.
    /// </summary>
    public void ProcessTurnStartStatusEffects()
    {
        if (IsDead) return;

        List<ActiveStatusEffect> expired = new List<ActiveStatusEffect>();

        for (int i = 0; i < activeStatusEffects.Count; i++)
        {
            ActiveStatusEffect effect = activeStatusEffects[i];
            if (effect.Data == null) continue;

            // 1. Process Resource Regens (HP/PP HoT)
            foreach (var regen in effect.Data.ResourceRegens)
            {
                ApplyResourceRegen(regen, effect.CurrentStacks);
            }

            // 2. Process Damage Over Time (DoT)
            foreach (var dot in effect.Data.DamagesOverTime)
            {
                ApplyDamageOverTime(dot, effect.CurrentStacks);
            }

            // 3. Tick duration / Auto-cure check
            bool isExpired = effect.TickTurn();
            if (isExpired)
            {
                expired.Add(effect);
            }
        }

        // Clean up expired effects
        foreach (var exp in expired)
        {
            RemoveStatusEffect(exp.Data);
        }
    }

    /// <summary>
    /// Processes overworld step ticks for persisting status effects (e.g. Poison taking damage every X steps).
    /// Called by Overworld Player Controller upon taking a step.
    /// </summary>
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

    /// <summary>
    /// Returns true if the fighter is currently restricted from taking turn actions due to crowd control (Stun, Sleep, Freeze).
    /// </summary>
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

    /// <summary>
    /// Returns the specific crowd control type currently restricting this fighter.
    /// </summary>
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

    /// <summary>
    /// Calculates cumulative percentage of damage mitigation provided by active status shields (e.g. Granite).
    /// </summary>
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

        // Remove old modifiers from this source first to prevent stacking duplicates on duration refresh
        RemoveModifiersFromSource(activeEffect);

        foreach (var bonus in activeEffect.Data.StatModifiers)
        {
            StatModifierType modType = (bonus.valueType == StatusValueType.Percentage)
                ? StatModifierType.PercentBase
                : StatModifierType.Flat;

            float calculatedValue = bonus.value * activeEffect.CurrentStacks;

            StatModifier modifier = new StatModifier(
                value: calculatedValue,
                type: modType,
                source: activeEffect,
                isTemporary: true
            );

            AddStatModifier(bonus.statType, modifier);
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
            amount = (regen.valueType == StatusValueType.Percentage)
                ? Mathf.RoundToInt((MaxHp * (regen.amountPerTurn / 100f)) * stacks)
                : Mathf.RoundToInt(regen.amountPerTurn * stacks);

            if (amount > 0) RestoreHP(amount);
            else if (amount < 0) TakeDamage(Mathf.Abs(amount));
        }
        else if (regen.targetResource == ResourceTarget.PP)
        {
            amount = (regen.valueType == StatusValueType.Percentage)
                ? Mathf.RoundToInt((MaxPp * (regen.amountPerTurn / 100f)) * stacks)
                : Mathf.RoundToInt(regen.amountPerTurn * stacks);

            if (amount > 0) RestorePP(amount);
            else if (amount < 0) ConsumePP(Mathf.Abs(amount));
        }
    }

    private void ApplyDamageOverTime(StatusDamageOverTime dot, int stacks)
    {
        if (dot == null) return;

        int damage = (dot.damageType == StatusValueType.Percentage)
            ? Mathf.RoundToInt((MaxHp * (dot.damagePerTurn / 100f)) * stacks)
            : Mathf.RoundToInt(dot.damagePerTurn * stacks);

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
        stat?.AddModifier(modifier);

        if (type == StatType.MaxHP) OnHpChanged?.Invoke(CurrHp, MaxHp);
        if (type == StatType.MaxPP) OnPpChanged?.Invoke(CurrPp, MaxPp);
    }

    public void RemoveModifiersFromSource(object source)
    {
        foreach (var stat in statsMap.Values)
        {
            stat.RemoveAllModifiersFromSource(source);
        }

        OnHpChanged?.Invoke(CurrHp, MaxHp);
        OnPpChanged?.Invoke(CurrPp, MaxPp);
    }

    public void TickTurnModifiers()
    {
        foreach (var stat in statsMap.Values)
        {
            stat.TickTurnModifiers();
        }
    }

    public void RemoveAllTemporaryModifiers()
    {
        foreach (var stat in statsMap.Values)
        {
            stat.RemoveAllTemporaryModifiers();
        }
    }

    #endregion
}