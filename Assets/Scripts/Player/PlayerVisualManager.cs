using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Container for inspector-assigned visual, audio, dialogue framing, and 3D weapon sockets 
/// references of a playable hero in the world map.
/// </summary>
[Serializable]
public class HeroVisualData
{
    [Tooltip("Unique Identifier matching the hero name or ID (e.g. 'Isaac', 'Garet').")]
    public string heroId;

    [Tooltip("Reference to the world model GameObject inside Player/Model hierarchy.")]
    public GameObject worldModelObject;

    [Tooltip("Humanoid Avatar asset corresponding to this specific hero model skeleton.")]
    public Avatar avatar;

    [Tooltip("Optional Animator Override Controller asset for hero-specific animation variants.")]
    public AnimatorOverrideController overrideController;

    [Tooltip("Footstep audio profile assigned to this specific hero.")]
    public HeroFootstepProfileSO footstepProfile;

    [Header("3D Exploration Weapon Sockets")]
    [Tooltip("Transform socket located on the hero's back for Holstered weapons (Axes, Maces, Staffs, or sheathless swords).")]
    public Transform weaponBackSocket;

    [Tooltip("Optional Transform socket located at the hip for sword sheaths/scabbards.")]
    public Transform weaponSheathSocket;

    [Tooltip("Optional Sheath/Scabbard mesh GameObject that can be toggled on/off (e.g., Isaac/Garet).")]
    public GameObject sheathMeshObject;

    [Header("Dialogue Framing Configuration")]
    [Tooltip("Eye height offset specific to this hero's anatomy for camera focus.")]
    public float eyeHeightOffset = 1.4f;

    [Tooltip("Framing center offset specific to this hero for group bounds.")]
    public float framingCenterOffset = 1.0f;

    [Tooltip("Framing radius specific to this hero for Cinemachine target group scaling.")]
    public float targetRadius = 0.8f;
}

/// <summary>
/// Manages hero model swapping, mesh visibility, 3D exploration weapon visuals, 
/// active footstep audio profile synchronization, and dialogue focus provider updates.
/// Delegates animation execution strictly to PlayerAnimationController.
/// </summary>
public class PlayerVisualManager : MonoBehaviour
{
    #region Serialized Fields

    [Header("Model Container & Components")]
    [SerializeField] private GameObject modelContainer;
    [SerializeField] private GameObject eyePoint;
    [SerializeField] private FootstepAudioEmitter footstepEmitter;
    [SerializeField] private DialogueFocusProvider focusProvider;
    [SerializeField] private PlayerAnimationController animationController;

    [Header("Hero Visual Configurations")]
    [SerializeField] private List<HeroVisualData> heroVisuals = new List<HeroVisualData>();

    #endregion

    #region Private Fields

    private readonly Dictionary<string, HeroVisualData> visualLookup = new Dictionary<string, HeroVisualData>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GameObject> activeHeroGameObjects = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

    private string currentActiveHeroId;

    // Runtime reference to the currently instantiated weapon mesh in exploration
    private GameObject currentExplorationWeaponInstance = null;

    #endregion

    #region Unity Lifecycle & Events

    private void Awake()
    {
        if (animationController == null)
        {
            animationController = GetComponent<PlayerAnimationController>();
        }

        InitializeLookupDictionary();
    }

    private void OnEnable()
    {
        GameEvents.Party.OnActiveHeroChanged += HandleActiveHeroChanged;
        GameEvents.Player.OnSetPlayerVisibilityRequested += SetModelVisibility;

        // Listen to equipment changes to refresh 3D weapon models in real-time
        GameEvents.Hero.OnEquipmentChanged += HandleHeroEquipmentChanged;
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDisable()
    {
        GameEvents.Party.OnActiveHeroChanged -= HandleActiveHeroChanged;
        GameEvents.Player.OnSetPlayerVisibilityRequested -= SetModelVisibility;

        GameEvents.Hero.OnEquipmentChanged -= HandleHeroEquipmentChanged;
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
    }

    #endregion

    #region GameState Handling

    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
        switch (newState)
        {
            case GameManager.GameState.TitleScreen:
            case GameManager.GameState.InBattle:
            case GameManager.GameState.Loading:
                SetModelVisibility(false);
                break;

            case GameManager.GameState.WorldExploration:
            case GameManager.GameState.Cutscene:
                SetModelVisibility(true);
                break;
        }
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

    public void SwapToHero(GameObject heroObject)
    {
        if (heroObject == null) return;

        string heroId = heroObject.name;

        if (!activeHeroGameObjects.ContainsKey(heroId))
        {
            activeHeroGameObjects.Add(heroId, heroObject);
        }
        else
        {
            activeHeroGameObjects[heroId] = heroObject;
        }

        SwapToHero(heroId, heroObject);
    }

    public void SwapToHero(string heroId, GameObject heroObject = null)
    {
        if (string.IsNullOrEmpty(heroId)) return;

        if (!visualLookup.TryGetValue(heroId, out HeroVisualData visualData))
        {
            Debug.LogWarning($"[PlayerVisualManager] No visual configuration found for hero ID: '{heroId}'.", this);
            return;
        }

        currentActiveHeroId = heroId;

        if (heroObject == null)
        {
            activeHeroGameObjects.TryGetValue(heroId, out heroObject);
        }

        // 1. Ensure Model container is active
        if (modelContainer != null && !modelContainer.activeSelf)
        {
            modelContainer.SetActive(true);
        }

        // 2. Toggle active world model mesh
        foreach (var kvp in visualLookup)
        {
            if (kvp.Value.worldModelObject != null)
            {
                bool isTarget = string.Equals(kvp.Key, heroId, StringComparison.OrdinalIgnoreCase);
                kvp.Value.worldModelObject.SetActive(isTarget);
            }
        }

        // 3. Delegate Avatar and Animator Override assignment to PlayerAnimationController
        if (animationController != null)
        {
            if (visualData.avatar != null)
            {
                animationController.SetAvatar(visualData.avatar);
            }

            if (visualData.overrideController != null)
            {
                animationController.ApplyAnimatorOverride(visualData.overrideController);
            }
        }

        // 4. Synchronize footstep audio profile
        if (footstepEmitter != null)
        {
            footstepEmitter.SetActiveProfile(visualData.footstepProfile);
        }

        // 5. Update DialogueFocusProvider parameters
        if (focusProvider != null)
        {
            focusProvider.UpdateProfile(visualData.eyeHeightOffset, visualData.framingCenterOffset, visualData.targetRadius);
        }

        if (eyePoint != null)
        {
            eyePoint.transform.localPosition = new Vector3(0f, visualData.eyeHeightOffset, eyePoint.transform.localPosition.z);
        }

        // 6. Update 3D Weapon Model
        UpdateExplorationWeaponVisuals(visualData, heroObject);
    }

    private void HandleActiveHeroChanged(GameObject activeHero)
    {
        if (activeHero == null) return;
        SwapToHero(activeHero);
    }

    private void HandleHeroEquipmentChanged(GameObject heroObject)
    {
        if (heroObject == null || string.IsNullOrEmpty(currentActiveHeroId)) return;

        if (heroObject.name.Equals(currentActiveHeroId, StringComparison.OrdinalIgnoreCase))
        {
            if (visualLookup.TryGetValue(currentActiveHeroId, out HeroVisualData visualData))
            {
                UpdateExplorationWeaponVisuals(visualData, heroObject);
            }
        }
    }

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

    #endregion

    #region Exploration Weapon Spawning & Attachment Logic

    private void UpdateExplorationWeaponVisuals(HeroVisualData visualData, GameObject heroObject)
    {
        ClearExplorationWeaponVisuals();

        if (visualData == null || heroObject == null) return;

        HeroEquipment heroEquipment = heroObject.GetComponent<HeroEquipment>();
        if (heroEquipment == null) return;

        WeaponSO equippedWeapon = heroEquipment.GetEquippedWeapon();

        if (equippedWeapon == null || equippedWeapon.WeaponModelPrefab == null)
        {
            if (visualData.sheathMeshObject != null)
            {
                visualData.sheathMeshObject.SetActive(false);
            }
            return;
        }

        bool isSwordSubtype = equippedWeapon.EquipmentSubtype == EquipmentSubtype.Longsword ||
                             equippedWeapon.EquipmentSubtype == EquipmentSubtype.LightBlade;

        Transform targetSocket = null;

        if (isSwordSubtype && visualData.sheathMeshObject != null)
        {
            visualData.sheathMeshObject.SetActive(true);
            targetSocket = visualData.weaponSheathSocket;
        }
        else
        {
            if (visualData.sheathMeshObject != null)
            {
                visualData.sheathMeshObject.SetActive(false);
            }

            targetSocket = visualData.weaponBackSocket;
        }

        if (targetSocket != null)
        {
            currentExplorationWeaponInstance = Instantiate(equippedWeapon.WeaponModelPrefab, targetSocket);
            currentExplorationWeaponInstance.transform.localPosition = Vector3.zero;
            currentExplorationWeaponInstance.transform.localRotation = Quaternion.identity;
            currentExplorationWeaponInstance.transform.localScale = Vector3.one;
        }
    }

    private void ClearExplorationWeaponVisuals()
    {
        if (currentExplorationWeaponInstance != null)
        {
            Destroy(currentExplorationWeaponInstance);
            currentExplorationWeaponInstance = null;
        }
    }

    #endregion
}