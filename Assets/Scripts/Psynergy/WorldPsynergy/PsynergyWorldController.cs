using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages assigned Psynergy shortcuts for each hero and handles execution of world Psynergies.
/// Validates PP costs, manages state transitions to Interacting during casts, and triggers visual execution events.
/// Reacts to requests via GameEvents.Psynergy, GameEvents.Party, and handles persistence via GameEvents.SaveLoad.
/// </summary>
public class PsynergyWorldController : MonoBehaviour
{
    [Header("Overworld Player Reference")]
    [Tooltip("Reference to the active physical Player root object in the world.")]
    [SerializeField] private GameObject playerWorldObject;

    [Header("Visual View Manager Reference")]
    [Tooltip("Reference to the manager orchestrating field Psynergy visual views (Hand 3D, Area VFX, etc.).")]
    [SerializeField] private WorldPsynergyViewManager viewManager;

    #region Private Fields

    // Stores up to 4 shortcut slots per hero (0: Up, 1: Down, 2: Left, 3: Right)
    private readonly Dictionary<GameObject, PsynergySO[]> heroShortcuts = new Dictionary<GameObject, PsynergySO[]>();

    // Local state caches
    private IReadOnlyList<GameObject> _cachedActiveParty;
    private GameObject _currentActiveHero;

    #endregion

    #region Unity Lifecycle & Initialization

    private void Awake()
    {
        if (viewManager == null)
            viewManager = GetComponent<WorldPsynergyViewManager>() ?? GetComponentInChildren<WorldPsynergyViewManager>();
    }

    private void OnEnable()
    {
        // Psynergy & Party Domain Subscriptions
        GameEvents.Psynergy.OnAssignShortcutRequested += HandleAssignShortcutRequested;
        GameEvents.Psynergy.OnGetShortcutsRequested += HandleGetShortcutsRequested;
        GameEvents.Psynergy.OnUseShortcutRequested += HandleUseShortcutRequested;
        GameEvents.Psynergy.OnPsynergyLost += HandleHeroLostPsynergy;
        GameEvents.Party.OnPartyChanged += HandlePartyChanged;
        GameEvents.Party.OnActiveHeroChanged += HandleActiveHeroChanged;

        // Save & Load Event Subscriptions
        GameEvents.SaveLoad.OnCollectSaveData += HandleCollectSaveData;
        GameEvents.SaveLoad.OnRestoreSaveData += HandleRestoreSaveData;
    }

    private void OnDisable()
    {
        // Psynergy & Party Domain Unsubscriptions
        GameEvents.Psynergy.OnAssignShortcutRequested -= HandleAssignShortcutRequested;
        GameEvents.Psynergy.OnGetShortcutsRequested -= HandleGetShortcutsRequested;
        GameEvents.Psynergy.OnUseShortcutRequested -= HandleUseShortcutRequested;
        GameEvents.Psynergy.OnPsynergyLost -= HandleHeroLostPsynergy;
        GameEvents.Party.OnPartyChanged -= HandlePartyChanged;
        GameEvents.Party.OnActiveHeroChanged -= HandleActiveHeroChanged;

        // Save & Load Event Unsubscriptions
        GameEvents.SaveLoad.OnCollectSaveData -= HandleCollectSaveData;
        GameEvents.SaveLoad.OnRestoreSaveData -= HandleRestoreSaveData;
    }

    #endregion

    #region Event Handlers & Request Processors

    private void HandlePartyChanged(IReadOnlyList<GameObject> activeParty)
    {
        _cachedActiveParty = activeParty;

        if (_cachedActiveParty == null || _cachedActiveParty.Count == 0) return;

        foreach (GameObject hero in _cachedActiveParty)
        {
            if (hero == null) continue;

            if (!heroShortcuts.ContainsKey(hero))
            {
                heroShortcuts.Add(hero, new PsynergySO[4]);
            }
        }
    }

    private void HandleActiveHeroChanged(GameObject activeHero)
    {
        _currentActiveHero = activeHero;

        if (_currentActiveHero != null)
        {
            NotifyShortcutsChanged(_currentActiveHero);
        }
    }

    private void HandleAssignShortcutRequested(GameObject hero, PsynergySO psynergy, int slotIndex)
    {
        SetShortcut(hero, psynergy, slotIndex);
    }

    private void HandleGetShortcutsRequested(GameObject hero, Action<PsynergySO[]> onResult)
    {
        PsynergySO[] shortcuts = GetShortcuts(hero);
        onResult?.Invoke(shortcuts);
    }

    private void HandleUseShortcutRequested(int slotIndex, IPsynergyTargetable target)
    {
        UsePsynergy(slotIndex, target);
    }

    private void HandleHeroLostPsynergy(GameObject hero, PsynergySO lostPsynergy)
    {
        if (hero == null || lostPsynergy == null) return;

        if (!lostPsynergy.WorldData.CanBeUsedInWorld)
        {
            return;
        }

        PsynergySO[] shortcuts = GetShortcuts(hero);
        bool modified = false;

        for (int i = 0; i < shortcuts.Length; i++)
        {
            if (shortcuts[i] == lostPsynergy)
            {
                shortcuts[i] = null;
                modified = true;
                Debug.Log($"[PsynergyWorldController] Cleared shortcut slot {i} for {hero.name} due to lost access to '{lostPsynergy.PsynergyName}'.");
            }
        }

        if (modified)
        {
            NotifyShortcutsChanged(hero);
        }
    }

    #endregion

    #region Save & Load Event Handlers

    private void HandleCollectSaveData(GameData saveData)
    {
        if (saveData == null) return;

        saveData.heroesShortcuts.Clear();

        foreach (var kvp in heroShortcuts)
        {
            GameObject heroObj = kvp.Key;
            PsynergySO[] shortcuts = kvp.Value;

            if (heroObj == null || shortcuts == null) continue;

            HeroShortcutData shortcutData = new HeroShortcutData
            {
                heroName = heroObj.name
            };

            for (int i = 0; i < Mathf.Min(4, shortcuts.Length); i++)
            {
                shortcutData.shortcutSlugs[i] = shortcuts[i] != null ? shortcuts[i].PsynergySlug : string.Empty;
            }

            saveData.heroesShortcuts.Add(shortcutData);
        }
    }

    private void HandleRestoreSaveData(GameData saveData)
    {
        if (saveData == null || saveData.heroesShortcuts == null) return;

        heroShortcuts.Clear();

        if (_cachedActiveParty != null)
        {
            for (int i = 0; i < _cachedActiveParty.Count; i++)
            {
                GameObject hero = _cachedActiveParty[i];
                if (hero != null && !heroShortcuts.ContainsKey(hero))
                {
                    heroShortcuts.Add(hero, new PsynergySO[4]);
                }
            }
        }

        for (int i = 0; i < saveData.heroesShortcuts.Count; i++)
        {
            HeroShortcutData shortcutData = saveData.heroesShortcuts[i];
            if (shortcutData == null || string.IsNullOrEmpty(shortcutData.heroName)) continue;

            GameObject matchedHero = null;
            if (_cachedActiveParty != null)
            {
                for (int h = 0; h < _cachedActiveParty.Count; h++)
                {
                    if (_cachedActiveParty[h] != null && _cachedActiveParty[h].name.Equals(shortcutData.heroName))
                    {
                        matchedHero = _cachedActiveParty[h];
                        break;
                    }
                }
            }

            if (matchedHero == null) continue;

            PsynergySO[] restoredShortcuts = GetShortcuts(matchedHero);

            for (int slot = 0; slot < Mathf.Min(4, shortcutData.shortcutSlugs.Length); slot++)
            {
                string slug = shortcutData.shortcutSlugs[slot];
                if (string.IsNullOrEmpty(slug))
                {
                    restoredShortcuts[slot] = null;
                    continue;
                }

                PsynergySO psynergyAsset = null;
                GameEvents.Database.RequestPsynergyBySlug(slug, p => psynergyAsset = p);

                if (psynergyAsset != null)
                {
                    restoredShortcuts[slot] = psynergyAsset;
                }
                else
                {
                    Debug.LogWarning($"[PsynergyWorldController] Shortcut psynergy slug '{slug}' not found in database for '{matchedHero.name}'.");
                }
            }

            NotifyShortcutsChanged(matchedHero);
        }

        if (_currentActiveHero != null)
        {
            NotifyShortcutsChanged(_currentActiveHero);
        }

        Debug.Log("<color=cyan>[PsynergyWorldController] Psynergy shortcuts successfully restored from save data.</color>");
    }

    #endregion

    #region Shortcut Data Access & Internal Mutators

    private PsynergySO[] GetShortcuts(GameObject hero)
    {
        if (hero == null) return new PsynergySO[4];

        if (heroShortcuts.TryGetValue(hero, out PsynergySO[] shortcuts))
        {
            return shortcuts;
        }

        PsynergySO[] newShortcuts = new PsynergySO[4];
        heroShortcuts.Add(hero, newShortcuts);
        return newShortcuts;
    }

    private void SetShortcut(GameObject hero, PsynergySO psynergy, int slotIndex)
    {
        if (hero == null || slotIndex < 0 || slotIndex >= 4) return;

        PsynergySO[] shortcuts = GetShortcuts(hero);
        shortcuts[slotIndex] = psynergy;

        NotifyShortcutsChanged(hero);
    }

    #endregion

    #region Gameplay Execution

    /// <summary>
    /// Executes the Psynergy assigned to the active hero's shortcut slot.
    /// Validates PP requirements, deducts cost, freezes game world state, and delegates visual/logical sequence.
    /// </summary>
    private void UsePsynergy(int slotIndex, IPsynergyTargetable target)
    {
        if (_currentActiveHero == null) return;
        if (slotIndex < 0 || slotIndex >= 4) return;

        PsynergySO[] shortcuts = GetShortcuts(_currentActiveHero);
        PsynergySO usedPsynergy = shortcuts[slotIndex];

        if (usedPsynergy == null) return;

        // 1. PP Cost Validation
        if (_currentActiveHero.TryGetComponent<HeroStats>(out var heroStats))
        {
            if (heroStats.CurrPp < usedPsynergy.PpCost)
            {
                Debug.LogWarning($"[PsynergyWorldController] Not enough PP to cast '{usedPsynergy.PsynergyName}'. Required: {usedPsynergy.PpCost}, Current: {heroStats.CurrPp}");
                GameEvents.Audio.RequestPlaySFX(SFXType.UI_Error);
                return;
            }

            // Deduct PP cost
            heroStats.ConsumePP(usedPsynergy.PpCost);
        }

        // 2. Lock game state to Interacting during the cast sequence
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.Interacting);

        // Broadcast player hero animation event
        GameEvents.Audio.RequestPlaySFX(SFXType.World_PsynergyCast);
        GameEvents.Psynergy.TriggerWorldPsynergyCast();

        Transform casterTransform = playerWorldObject != null ? playerWorldObject.transform : transform;

        // 3. Delegate execution sequence to View Manager
        bool isCompatibleTarget = target != null && target.IsCompatibleWith(usedPsynergy);

        if (viewManager != null)
        {
            viewManager.PlayPsynergyVisuals(
                usedPsynergy,
                casterTransform,
                isCompatibleTarget ? target : null,
                onImpactOrGrasp: () =>
                {
                    // Phase 1: Keyframe moment (fist closes or push impacts)
                    if (isCompatibleTarget)
                    {
                        Debug.Log($"[PsynergyWorldController] Executing '{usedPsynergy.PsynergySlug}' impact logic on target object.");
                        target.ExecutePsynergy(usedPsynergy, playerWorldObject);
                    }
                    else
                    {
                        Debug.Log($"[PsynergyWorldController] Executing '{usedPsynergy.PsynergySlug}' impact into empty space.");
                        GameEvents.Audio.RequestPlaySFX(SFXType.UI_Error);
                    }
                },
                onFinished: () =>
                {
                    // Phase 2: Visual presentation fully completed (e.g., hand returned to caster)
                    if (isCompatibleTarget)
                    {
                        target.OnPsynergySequenceFinished(usedPsynergy, playerWorldObject);
                    }

                    // Restore free world exploration state
                    GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
                }
            );
        }
        else
        {
            // Fallback direct execution if ViewManager is missing
            if (isCompatibleTarget)
            {
                target.ExecutePsynergy(usedPsynergy, playerWorldObject);
                target.OnPsynergySequenceFinished(usedPsynergy, playerWorldObject);
            }
            GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
        }
    }

    #endregion

    #region Helper Methods

    private void NotifyShortcutsChanged(GameObject hero)
    {
        if (heroShortcuts.TryGetValue(hero, out PsynergySO[] shortcuts))
        {
            GameEvents.Psynergy.TriggerShortcutsChanged(hero, shortcuts);
        }
    }

    #endregion
}