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

    [Tooltip("Direct reference to the world AnimatorController asset.")]
    public RuntimeAnimatorController animatorController;

    [Tooltip("Direct reference to the world Humanoid Avatar asset.")]
    public Avatar avatar;

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
/// Manages player model swapping, mesh visibility, animator controller assignments,
/// interaction animations, active footstep audio profile synchronization, 3D exploration weapon visuals,
/// and dialogue focus provider updates.
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
    [SerializeField] private GameObject EyePoint;
    [SerializeField] private FootstepAudioEmitter footstepEmitter;
    [SerializeField] private DialogueFocusProvider focusProvider;

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
        InitializeLookupDictionary();
    }

    private void OnEnable()
    {
        GameEvents.Party.OnActiveHeroChanged += HandleActiveHeroChanged;
        GameEvents.Player.OnSetPlayerVisibilityRequested += SetModelVisibility;
        GameEvents.Player.OnPlayInteractionAnimation += HandlePlayInteractionAnimation;

        // Listen to equipment changes to refresh 3D weapon models in real-time
        GameEvents.Hero.OnEquipmentChanged += HandleHeroEquipmentChanged;
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDisable()
    {
        GameEvents.Party.OnActiveHeroChanged -= HandleActiveHeroChanged;
        GameEvents.Player.OnSetPlayerVisibilityRequested -= SetModelVisibility;
        GameEvents.Player.OnPlayInteractionAnimation -= HandlePlayInteractionAnimation;

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

    /// <summary>
    /// Overload that receives the Hero GameObject directly containing the HeroEquipment component.
    /// </summary>
    public void SwapToHero(GameObject heroObject)
    {
        if (heroObject == null) return;

        string heroId = heroObject.name;

        // Cache or update the reference to the Hero GameObject for future equipment events
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

    /// <summary>
    /// Swaps active model mesh, updates AnimatorController + Avatar, synchronizes footstep audio profile, 
    /// attaches the active weapon to the correct socket using the provided heroObject, and updates DialogueFocusProvider offsets.
    /// </summary>
    public void SwapToHero(string heroId, GameObject heroObject = null)
    {
        if (string.IsNullOrEmpty(heroId)) return;

        if (!visualLookup.TryGetValue(heroId, out HeroVisualData visualData))
        {
            Debug.LogWarning($"[PlayerVisualManager] No visual configuration found for hero ID: '{heroId}'.", this);
            return;
        }

        currentActiveHeroId = heroId;

        // If heroObject wasn't passed directly, attempt to fetch it from cached dictionary
        if (heroObject == null)
        {
            activeHeroGameObjects.TryGetValue(heroId, out heroObject);
        }

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

        // 5. Update DialogueFocusProvider parameters dynamically based on current hero anatomy
        if (focusProvider != null)
        {
            focusProvider.UpdateProfile(visualData.eyeHeightOffset, visualData.framingCenterOffset, visualData.targetRadius);
        }

        if (EyePoint != null)
        {
            EyePoint.transform.localPosition = new Vector3(0f, visualData.eyeHeightOffset, EyePoint.transform.localPosition.z);
        }

        // 6. Update 3D Weapon Model for exploration passing the heroObject with HeroEquipment
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

        // Only refresh visuals if the equipment event belongs to the currently active hero
        if (heroObject.name.Equals(currentActiveHeroId, StringComparison.OrdinalIgnoreCase))
        {
            if (visualLookup.TryGetValue(currentActiveHeroId, out HeroVisualData visualData))
            {
                UpdateExplorationWeaponVisuals(visualData, heroObject);
            }
        }
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

    #region Exploration Weapon Spawning & Attachment Logic

    /// <summary>
    /// Fetches the equipped weapon from the provided hero's HeroEquipment component, 
    /// determines target socket (Back or Sheath), and instantiates the 3D model.
    /// </summary>
    private void UpdateExplorationWeaponVisuals(HeroVisualData visualData, GameObject heroObject)
    {
        ClearExplorationWeaponVisuals();

        if (visualData == null || heroObject == null) return;

        // Fetch HeroEquipment component directly from the passed Hero GameObject
        HeroEquipment heroEquipment = heroObject.GetComponent<HeroEquipment>();
        if (heroEquipment == null) return;

        WeaponSO equippedWeapon = heroEquipment.GetEquippedWeapon();

        if (equippedWeapon == null || equippedWeapon.WeaponModelPrefab == null)
        {
            // If no weapon equipped, disable sheath mesh if present
            if (visualData.sheathMeshObject != null)
            {
                visualData.sheathMeshObject.SetActive(false);
            }
            return;
        }

        // Check if weapon subtype is a sword (Longsword or LightBlade)
        bool isSwordSubtype = equippedWeapon.EquipmentSubtype == EquipmentSubtype.Longsword ||
                             equippedWeapon.EquipmentSubtype == EquipmentSubtype.LightBlade;

        Transform targetSocket = null;

        // Case A: Sword equipped AND hero has an assigned sheath mesh
        if (isSwordSubtype && visualData.sheathMeshObject != null)
        {
            visualData.sheathMeshObject.SetActive(true);
            targetSocket = visualData.weaponSheathSocket;
        }
        // Case B: Axes, Maces, Staffs, OR swords on heroes without sheath reference
        else
        {
            if (visualData.sheathMeshObject != null)
            {
                visualData.sheathMeshObject.SetActive(false);
            }

            targetSocket = visualData.weaponBackSocket;
        }

        // Instantiate model under target socket if valid
        if (targetSocket != null)
        {
            currentExplorationWeaponInstance = Instantiate(equippedWeapon.WeaponModelPrefab, targetSocket);
            currentExplorationWeaponInstance.transform.localPosition = Vector3.zero;
            currentExplorationWeaponInstance.transform.localRotation = Quaternion.identity;
            currentExplorationWeaponInstance.transform.localScale = Vector3.one;
        }
    }

    /// <summary>
    /// Safely destroys the active 3D weapon model instance in exploration.
    /// </summary>
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