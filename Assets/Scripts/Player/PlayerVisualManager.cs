using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Strongly-typed interaction animation requests for the player character.
/// </summary>
public enum PlayerInteractionAnimType
{
    PickChest,
    PickFloor,
    InteractNPC,
    TouchObject,
    EnemyHitReaction
}

/// <summary>
/// Container for inspector-assigned visual and audio references of a playable hero in the world map.
/// </summary>
[Serializable]
public class HeroVisualData
{
    [Tooltip("Unique Identifier matching the hero name or ID (e.g. 'Isaac', 'Garet').")]
    public string heroId;

    [Tooltip("Reference to the world model GameObject inside Player/Model hierarchy.")]
    public GameObject worldModelObject;

    [Tooltip("Direct reference to the world AnimatorController asset.")]
    public RuntimeAnimatorController animatorController;

    [Tooltip("Direct reference to the world Humanoid Avatar asset.")]
    public Avatar avatar;

    [Tooltip("Footstep audio profile assigned to this specific hero.")]
    public HeroFootstepProfileSO footstepProfile;
}

/// <summary>
/// Manages player model swapping, mesh visibility, animator controller assignments,
/// interaction animations, and active footstep audio profile synchronization.
/// </summary>
public class PlayerVisualManager : MonoBehaviour
{
    #region Animator Hashes (Cached for performance)

    private static readonly int PickChestHash = Animator.StringToHash("pickChest");
    private static readonly int PickFloorHash = Animator.StringToHash("pickFloor");
    private static readonly int InteractNPCHash = Animator.StringToHash("interactNPC");
    private static readonly int TouchObjectHash = Animator.StringToHash("touchObject");
    private static readonly int EnemyHitReactionHash = Animator.StringToHash("attacked");

    #endregion

    #region Serialized Fields

    [Header("Model Container & Components")]
    [SerializeField] private GameObject modelContainer;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private FootstepAudioEmitter footstepEmitter;

    [Header("Hero Visual Configurations")]
    [SerializeField] private List<HeroVisualData> heroVisuals = new List<HeroVisualData>();

    #endregion

    #region Private Fields

    private readonly Dictionary<string, HeroVisualData> visualLookup = new Dictionary<string, HeroVisualData>(StringComparer.OrdinalIgnoreCase);
    private string currentActiveHeroId;

    #endregion

    #region Unity Lifecycle & Events

    private void Awake()
    {
        if (playerAnimator == null)
        {
            playerAnimator = GetComponent<Animator>();
            if (playerAnimator == null)
            {
                playerAnimator = GetComponentInParent<Animator>();
            }
        }

        if (footstepEmitter == null)
        {
            footstepEmitter = GetComponent<FootstepAudioEmitter>();
            if (footstepEmitter == null)
            {
                footstepEmitter = GetComponentInParent<FootstepAudioEmitter>();
            }
        }

        InitializeLookupDictionary();
    }

    private void OnEnable()
    {
        GameEvents.Party.OnActiveHeroChanged += HandleActiveHeroChanged;
        GameEvents.Player.OnSetPlayerVisibilityRequested += SetModelVisibility;
        GameEvents.Player.OnPlayInteractionAnimation += HandlePlayInteractionAnimation;
    }

    private void OnDisable()
    {
        GameEvents.Party.OnActiveHeroChanged -= HandleActiveHeroChanged;
        GameEvents.Player.OnSetPlayerVisibilityRequested -= SetModelVisibility;
        GameEvents.Player.OnPlayInteractionAnimation -= HandlePlayInteractionAnimation;
    }

    #endregion

    #region Initialization & Data Setup

    private void InitializeLookupDictionary()
    {
        visualLookup.Clear();

        for (int i = 0; i < heroVisuals.Count; i++)
        {
            var visualData = heroVisuals[i];
            if (visualData == null || string.IsNullOrEmpty(visualData.heroId))
            {
                Debug.LogWarning("[PlayerVisualManager] Found an unassigned or empty HeroVisualData element.", this);
                continue;
            }

            if (!visualLookup.ContainsKey(visualData.heroId))
            {
                visualLookup.Add(visualData.heroId, visualData);
            }
            else
            {
                Debug.LogWarning($"[PlayerVisualManager] Duplicate hero ID '{visualData.heroId}' found in configurations.", this);
            }
        }
    }

    #endregion

    #region Visual & Audio Swap Logic

    /// <summary>
    /// Swaps active model mesh, updates AnimatorController + Avatar, and synchronizes footstep audio profile.
    /// </summary>
    public void SwapToHero(string heroId)
    {
        if (string.IsNullOrEmpty(heroId)) return;

        if (!visualLookup.TryGetValue(heroId, out HeroVisualData visualData))
        {
            Debug.LogWarning($"[PlayerVisualManager] No visual configuration found for hero ID: '{heroId}'.", this);
            return;
        }

        currentActiveHeroId = heroId;

        // 1. Ensure Model container is active
        if (modelContainer != null && !modelContainer.activeSelf)
        {
            modelContainer.SetActive(true);
        }

        // 2. Hide all other models and show only target model
        foreach (var kvp in visualLookup)
        {
            if (kvp.Value.worldModelObject != null)
            {
                bool isTarget = string.Equals(kvp.Key, heroId, StringComparison.OrdinalIgnoreCase);
                kvp.Value.worldModelObject.SetActive(isTarget);
            }
        }

        // 3. Update Animator Controller & Avatar directly
        if (playerAnimator != null)
        {
            if (visualData.animatorController != null)
            {
                playerAnimator.runtimeAnimatorController = visualData.animatorController;
            }

            if (visualData.avatar != null)
            {
                playerAnimator.avatar = visualData.avatar;
            }
        }

        // 4. Synchronize footstep audio profile
        if (footstepEmitter != null)
        {
            footstepEmitter.SetActiveProfile(visualData.footstepProfile);
        }
    }

    private void HandleActiveHeroChanged(GameObject activeHero)
    {
        if (activeHero == null) return;
        SwapToHero(activeHero.name);
    }

    /// <summary>
    /// Toggles main Model container visibility and ensures active hero model state.
    /// </summary>
    public void SetModelVisibility(bool visible)
    {
        if (modelContainer != null)
        {
            modelContainer.SetActive(visible);
        }

        if (visible && !string.IsNullOrEmpty(currentActiveHeroId))
        {
            SwapToHero(currentActiveHeroId);
        }
    }

    /// <summary>
    /// Handles requests to play interaction animations directly on the active Animator.
    /// </summary>
    private void HandlePlayInteractionAnimation(PlayerInteractionAnimType animType)
    {
        if (playerAnimator == null) return;

        switch (animType)
        {
            case PlayerInteractionAnimType.PickChest:
                playerAnimator.SetTrigger(PickChestHash);
                break;
            case PlayerInteractionAnimType.PickFloor:
                playerAnimator.SetTrigger(PickFloorHash);
                break;
            case PlayerInteractionAnimType.InteractNPC:
                playerAnimator.SetTrigger(InteractNPCHash);
                break;
            case PlayerInteractionAnimType.TouchObject:
                playerAnimator.SetTrigger(TouchObjectHash);
                break;
            case PlayerInteractionAnimType.EnemyHitReaction:
                playerAnimator.SetTrigger(EnemyHitReactionHash);
                break;
        }
    }

    #endregion
}