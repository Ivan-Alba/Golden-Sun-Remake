using UnityEngine;

/// <summary>
/// Represents a live, dynamic instance of a status effect applied to a fighter in runtime.
/// Manages turn countdowns, overworld step tracking, stack counts, caster tracking, and expiration logic.
/// </summary>
public class ActiveStatusEffect
{
    #region Properties

    /// <summary>
    /// The static template defining the rules and mechanics of this status effect.
    /// </summary>
    public StatusEffectSO Data { get; private set; }

    /// <summary>
    /// The fighter who originally cast or applied this effect (useful for scaling damage or kill attribution).
    /// </summary>
    public FighterStats Caster { get; private set; }

    /// <summary>
    /// Turns remaining before this effect naturally expires. (-1 for infinite/permanent until cured).
    /// </summary>
    public int RemainingTurns { get; private set; }

    /// <summary>
    /// Current number of stacks applied to the fighter if the effect is stackable.
    /// </summary>
    public int CurrentStacks { get; private set; }

    /// <summary>
    /// Counter tracking overworld steps taken since last tick.
    /// </summary>
    public int StepCounter { get; private set; }

    /// <summary>
    /// Returns true if the duration has reached 0 and the effect should be removed.
    /// Permanent effects (-1 turns) never expire naturally.
    /// </summary>
    public bool IsExpired => RemainingTurns == 0;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new active status instance targeting a fighter.
    /// </summary>
    /// <param name="data">The ScriptableObject asset defining the effect.</param>
    /// <param name="caster">The fighter who inflicted the status.</param>
    /// <param name="overrideDuration">Optional custom turn duration; if -2, uses base duration from SO.</param>
    public ActiveStatusEffect(StatusEffectSO data, FighterStats caster, int overrideDuration = -2)
    {
        Data = data;
        Caster = caster;
        CurrentStacks = 1;
        StepCounter = 0;

        if (overrideDuration != -2)
        {
            RemainingTurns = overrideDuration;
        }
        else
        {
            RemainingTurns = data != null ? data.BaseDurationTurns : 1;
        }
    }

    #endregion

    #region Lifecycle & Turn Processing

    /// <summary>
    /// Decrements the remaining turns countdown at turn execution.
    /// Checks for natural auto-cure chance if configured.
    /// </summary>
    /// <returns>True if the status expired or auto-cured during this tick.</returns>
    public bool TickTurn()
    {
        // Infinite/Permanent effects do not count down turns naturally
        if (RemainingTurns == -1)
        {
            return CheckAutoCure();
        }

        if (RemainingTurns > 0)
        {
            RemainingTurns--;
        }

        if (RemainingTurns == 0)
        {
            return true; // Expired
        }

        return CheckAutoCure();
    }

    /// <summary>
    /// Increments overworld step counter when walking on the map.
    /// </summary>
    /// <returns>True if an overworld tick (damage/regen) should trigger on this step.</returns>
    public bool OnOverworldStep()
    {
        if (Data == null || !Data.TicksInOverworld || Data.StepsPerOverworldTick <= 0)
        {
            return false;
        }

        StepCounter++;

        if (StepCounter >= Data.StepsPerOverworldTick)
        {
            StepCounter = 0;
            return true; // Trigger overworld tick!
        }

        return false;
    }

    /// <summary>
    /// Refreshes the turn duration back to default or maximum value (when re-applied).
    /// </summary>
    public void RefreshDuration(int customDuration = -2)
    {
        int targetDuration = (customDuration != -2) ? customDuration : Data.BaseDurationTurns;

        // If current duration is lower, reset to max. Do not reduce duration if re-applied with shorter duration.
        if (RemainingTurns != -1 && (targetDuration == -1 || targetDuration > RemainingTurns))
        {
            RemainingTurns = targetDuration;
        }
    }

    /// <summary>
    /// Adds a stack to this effect if supported by its template up to MaxStacks, and refreshes duration.
    /// </summary>
    public bool AddStack()
    {
        if (Data == null || !Data.IsStackable) return false;

        if (CurrentStacks < Data.MaxStacks)
        {
            CurrentStacks++;
            RefreshDuration();
            return true;
        }

        // Already at max stacks, just refresh duration
        RefreshDuration();
        return false;
    }

    /// <summary>
    /// Checks if a random percentage roll cures this effect at the start of a turn.
    /// </summary>
    private bool CheckAutoCure()
    {
        if (Data == null || Data.AutoCureChancePerTurn <= 0f) return false;

        float roll = Random.Range(0f, 100f);
        if (roll <= Data.AutoCureChancePerTurn)
        {
            Debug.Log($"[ActiveStatusEffect] {Data.EffectName} auto-cured naturally via percentage roll ({roll:F1}% <= {Data.AutoCureChancePerTurn}%).");
            RemainingTurns = 0;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Forces immediate expiration (e.g. when taking physical damage while Sleeping).
    /// </summary>
    public void ForceExpire()
    {
        RemainingTurns = 0;
    }

    #endregion
}