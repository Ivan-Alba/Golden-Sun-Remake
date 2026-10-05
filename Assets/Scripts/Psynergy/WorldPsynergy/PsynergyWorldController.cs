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

    #region Private Fields

    // Stores up to 4 shortcut slots per hero (0: Up, 1: Down, 2: Left, 3: Right)
    private readonly Dictionary<GameObject, PsynergySO[]> heroShortcuts = new Dictionary<GameObject, PsynergySO[]>();

    // Local state caches
    private IReadOnlyList<GameObject> _cachedActiveParty;
    private GameObject _currentActiveHero;

    #endregion

    #region Unity Lifecycle & Initialization

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

    /// <summary>
    /// Handles automated removal of assigned shortcuts when a hero loses access to a Psynergy.
    /// Filters early if the lost psynergy is not usable in the world.
    /// </summary>
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

        // Re-initialize containers for cached active party
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

            // Find matching hero GameObject reference
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

    /// <summary>
    /// Gets the current array of 4 Psynergy shortcuts for the given hero.
    /// </summary>
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

    /// <summary>
    /// Assigns a Psynergy to a specific shortcut slot (0..3) for a hero.
    /// Pass null as psynergy to clear the slot.
    /// </summary>
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
    /// Validates PP requirements, deducts cost, freezes game world state, and broadcasts execution events.
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

        // 2. Lock game state to Interacting to prevent world interference during cast
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.Interacting);

        // 3. Process Execution & Broadcast Events
        if (target != null && target.IsCompatibleWith(usedPsynergy))
        {
            Debug.Log($"[PsynergyWorldController] Executing '{usedPsynergy.PsynergySlug}' on target object.");

            // Broadcast hero cast event for animations/VFX
            GameEvents.Psynergy.TriggerWorldPsynergyCast();

            // Execute target logic (target responsible for unlocking GameState upon sequence completion)
            target.ExecutePsynergy(usedPsynergy, playerWorldObject);
        }
        else
        {
            Debug.Log($"[PsynergyWorldController] Casting '{usedPsynergy.PsynergySlug}' into empty space (No compatible target).");

            // Broadcast hero cast event for animations/VFX
            GameEvents.Psynergy.TriggerWorldPsynergyCast();

            // Targetless cast: trigger timer to automatically restore WorldExploration state
            StartCoroutine(Routine_HandleTargetlessCastRecovery(1.4f));
        }
    }

    private IEnumerator Routine_HandleTargetlessCastRecovery(float delay)
    {
        yield return new WaitForSeconds(delay);
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
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