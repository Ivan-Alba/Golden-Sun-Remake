using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the execution interface for all round-based combat actions.
/// Supports both Golden Sun Global Rounds and FFX CTB execution modes.
/// </summary>
public interface ICombatCommand
{
    FighterController Caster { get; }
    BattleActionRequest Request { get; }
    int RoundPriority { get; }
    bool IsDefendingFallback { get; }

    /// <summary>
    /// Evaluates if the command can still execute its main logic or if targets are no longer valid.
    /// </summary>
    bool ValidateTargets();

    /// <summary>
    /// Converts the current action to a Defensive stance if all intended targets were defeated prior to execution.
    /// Activates spatial defense visuals/mitigation immediately while maintaining original agility for turn notifications.
    /// </summary>
    void ConvertToDefenseFallback();
}

/// <summary>
/// Concrete command wrapper encapsulating fighter requests, calculated speed priority, and target validation.
/// </summary>
public class CombatCommand : ICombatCommand
{
    public FighterController Caster { get; private set; }
    public BattleActionRequest Request { get; private set; }
    public int RoundPriority { get; private set; }
    public bool IsDefendingFallback { get; private set; }

    public bool IsExecuted { get; set; } = false;

    public CombatCommand(FighterController caster, BattleActionRequest request, int baseAgility)
    {
        Caster = caster;
        Request = request;
        IsDefendingFallback = request.CommandType == BattleCommandType.Defend;

        // Golden Sun Round Priority formula: Base Agility + random RNG variance (0-10%)
        int rngOffset = Random.Range(0, Mathf.Max(1, Mathf.RoundToInt(baseAgility * 0.1f)));

        // Strictly use Agility priority so turn notifications trigger in correct chronological order
        RoundPriority = baseAgility + rngOffset;

        // NOTE: Defense visualization/mitigation for explicit 'Defend' commands is deferred to ActivatePreDeclaredDefenses() 
        // when the combat round actually starts executing.
    }

    public bool ValidateTargets()
    {
        // 1. Si el ejecutante murió antes de que llegara su turno, el comando es inválido
        if (Caster == null || Caster.GetComponent<FighterStats>().IsDead)
        {
            return false;
        }

        // 2. Acciones de Defensa o auto-beneficio siempre se ejecutan
        if (Request.CommandType == BattleCommandType.Defend || Request.PrimaryTarget == Caster.gameObject)
        {
            return true;
        }

        // 3. Identificamos el tipo de targeting de la acción
        ActionTargetType targetType = ActionTargetType.SingleEnemy;
        if (Request.ActionPayload is PsynergySO psynergy)
        {
            targetType = psynergy.TargetType;
        }
        else if (Request.ActionPayload is DjinnInstance djinn && djinn.DjinnData?.UnleashEffect != null)
        {
            targetType = djinn.DjinnData.UnleashEffect.TargetType;
        }

        // CASO 1: Acciones de Área o Múltiples (MultiEnemyArea, AllEnemies, AllAllies)
        // Se ejecuta SI Y SOLO SI al menos un objetivo dentro del área afectada continúa con vida.
        if (targetType == ActionTargetType.MultiEnemyArea ||
            targetType == ActionTargetType.AllEnemies ||
            targetType == ActionTargetType.AllAllies)
        {
            if (Request.AffectedTargets != null && Request.AffectedTargets.Count > 0)
            {
                for (int i = 0; i < Request.AffectedTargets.Count; i++)
                {
                    GameObject target = Request.AffectedTargets[i];
                    if (target != null && target.TryGetComponent<FighterStats>(out var stats) && !stats.IsDead)
                    {
                        return true; // ¡Al menos un objetivo del área sigue vivo! Se ejecuta la Psinergia.
                    }
                }
            }

            // Si el objetivo central Y todos los laterales del área murieron -> Fallback a Defensa
            return false;
        }

        // CASO 2: Acciones Unitarias (SingleEnemy, SingleAlly)
        // Si el objetivo único individual ha muerto, se activa el fallback a Defensa.
        if (Request.PrimaryTarget != null && Request.PrimaryTarget.TryGetComponent<FighterStats>(out var targetStats))
        {
            return !targetStats.IsDead;
        }

        return false;
    }

    public void ConvertToDefenseFallback()
    {
        IsDefendingFallback = true;

        // Replace original request with a Defend command targeting self
        Request = new BattleActionRequest
        {
            Caster = Caster,
            CommandType = BattleCommandType.Defend,
            ActionPayload = null,
            PrimaryTarget = Caster.gameObject,
            AffectedTargets = new List<GameObject> { Caster.gameObject }
        };

        // Immediately trigger defense stance and damage mitigation on the 3D model when target is lost mid-round
        if (Caster != null)
        {
            Caster.SetUpDefense();
        }

        Debug.Log($"[CombatCommand] Target for {Caster.name} was defeated! Defensive stance activated immediately, banner deferred to turn order.");
    }
}