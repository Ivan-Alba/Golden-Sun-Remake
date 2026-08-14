using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class TurnController : MonoBehaviour
{
    [Header("Turn Controller Settings & Prefabs")]
    [SerializeField] private GameObject turnCameraPrefab;
    [SerializeField] private BattleCameraController battleCameraController;
    [SerializeField] private int maxTurnBoxes = 6;
    [SerializeField] private float agilityScaleFactor = 100f; // Scale constant K for CTB formula

    // Variable with the gameObject of current fighter
    private FighterController currentFighter;

    // Active fighters participating in battle
    private List<GameObject> activeFighters = new List<GameObject>();

    // CAMERA HERO
    private Camera turnCamera;
    private GameObject turnCameraMask;

    private BattleUIController battleUIController;

    #region Properties

    public int MaxTurnBoxes {get => maxTurnBoxes; set => maxTurnBoxes = value; }
    public FighterController CurrentFighter {get => currentFighter; set => currentFighter = value; }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        battleUIController = GetComponent<BattleUIController>();
    }

    #endregion

    #region Initialization & Turn Management

    /// <summary>
    /// Initializes the turn system at the start of a battle.
    /// Sets initial cooldowns based on agility for all fighters.
    /// </summary>
    public List<KeyValuePair<GameObject, int>> InitializeTurnSystem(List<GameObject> fighters)
    {
        activeFighters = new List<GameObject>(fighters);

        // 1. Calculate initial turn CDs for all fighters based on their agility
        foreach (GameObject fighterObj in activeFighters)
        {
            if (fighterObj == null) continue;

            FighterController controller = fighterObj.GetComponent<FighterController>();
            FighterStats stats = fighterObj.GetComponent<FighterStats>();

            if (controller != null && stats != null)
            {
                // Initial delay inverse to agility (faster fighters get lower initial CD)
                int agility = Mathf.Max(1, stats.Agility);
                controller.NextTurnCD = Mathf.Max(1, Mathf.RoundToInt(agilityScaleFactor / agility));
            }
        }

        // 2. Normalize CDs so the fastest fighter starts at 0
        NormalizeCooldowns();

        // 3. Set current active fighter
        UpdateCurrentFighter();

        // 4. Generate UI turn projection and update UI panel
        List<KeyValuePair<GameObject, int>> turnProjection = BuildTurnProjection();
        if (battleUIController != null)
        {
            battleUIController.SetTurnsPanel(turnProjection);
        }

        // 5. Trigger Hero Turn UI/Camera if a Hero acts first
        if (currentFighter != null && currentFighter.CompareTag("Hero"))
        {
            SetHeroTurn();
        }

        return turnProjection;
    }

    /// <summary>
    /// Advances the battle turn by applying action delay to the current fighter,
    /// normalizing cooldowns, and updating the UI projection.
    /// </summary>
    /// <param name="fighterObj">The fighter that just performed an action.</param>
    /// <param name="baseActionCost">Base delay cost of the performed action (e.g., 50 for Basic Attack, 30 for Guard).</param>
    public void AdvanceTurn(GameObject fighterObj, int baseActionCost)
    {
        if (fighterObj == null) return;

        FighterController controller = fighterObj.GetComponent<FighterController>();
        if (controller != null)
        {
            // 1. Calculate CTB action delay using inverse agility formula
            int delay = CalculateDelay(fighterObj, baseActionCost);
            controller.NextTurnCD += delay;
        }

        // 2. Remove dead fighters from active list
        activeFighters.RemoveAll(f => f == null || f.GetComponent<FighterStats>().CurrHp <= 0);

        if (activeFighters.Count == 0) return;

        // 3. Normalize cooldowns (subtract minimum CD from everyone)
        NormalizeCooldowns();

        // 4. Update current active fighter
        UpdateCurrentFighter();

        // 5. Rebuild UI projection list and refresh UI
        List<KeyValuePair<GameObject, int>> turnProjection = BuildTurnProjection();
        if (battleUIController != null)
        {
            battleUIController.SetTurnsPanel(turnProjection);
        }

        // 6. Handle camera and UI if the new current fighter is a hero
        if (currentFighter != null && currentFighter.CompareTag("Hero"))
        {
            SetHeroTurn();
        }
    }

    #endregion

    #region Formula & Helper Methods

    /// <summary>
    /// Calculates action delay using Inverse Proportional CTB Formula (Option A).
    /// Delay = Max(1, Round((BaseCost * K) / Agility))
    /// </summary>
    public int CalculateDelay(GameObject fighterObj, int baseActionCost)
    {
        if (fighterObj == null) return baseActionCost;

        FighterStats stats = fighterObj.GetComponent<FighterStats>();
        int agility = (stats != null) ? Mathf.Max(1, stats.Agility) : 1;

        int finalDelay = Mathf.RoundToInt((baseActionCost * agilityScaleFactor) / agility);
        return Mathf.Max(1, finalDelay);
    }

    /// <summary>
    /// Subtracts the minimum CD among active fighters from everyone.
    /// Keeps turn values small and normalized near zero.
    /// </summary>
    private void NormalizeCooldowns()
    {
        if (activeFighters.Count == 0) return;

        int minCD = activeFighters
            .Select(f => f.GetComponent<FighterController>().NextTurnCD)
            .Min();

        foreach (GameObject fighterObj in activeFighters)
        {
            if (fighterObj != null)
            {
                fighterObj.GetComponent<FighterController>().NextTurnCD -= minCD;
            }
        }
    }

    /// <summary>
    /// Sets CurrentFighter to the active fighter with the lowest NextTurnCD.
    /// </summary>
    private void UpdateCurrentFighter()
    {
        if (activeFighters.Count == 0)
        {
            currentFighter = null;
            return;
        }

        GameObject leadFighter = activeFighters
            .OrderBy(f => f.GetComponent<FighterController>().NextTurnCD)
            .FirstOrDefault();

        if (leadFighter != null)
        {
            currentFighter = leadFighter.GetComponent<FighterController>();
        }
    }

    /// <summary>
    /// Simulates future turns up to maxTurnBoxes without altering real fighter state.
    /// Used for drawing the UI Timeline.
    /// </summary>
    public List<KeyValuePair<GameObject, int>> BuildTurnProjection()
    {
        List<KeyValuePair<GameObject, int>> projection = new List<KeyValuePair<GameObject, int>>();
        if (activeFighters.Count == 0) return projection;

        // Create temporary virtual CD state for simulation
        Dictionary<GameObject, int> simulatedCDs = new Dictionary<GameObject, int>();
        foreach (GameObject f in activeFighters)
        {
            if (f != null)
            {
                simulatedCDs[f] = f.GetComponent<FighterController>().NextTurnCD;
            }
        }

        // Standard default action cost assumed for timeline preview (e.g., 50)
        int defaultCost = 50;

        while (projection.Count < maxTurnBoxes && simulatedCDs.Count > 0)
        {
            // Find fighter with minimum simulated CD
            KeyValuePair<GameObject, int> nextUnit = simulatedCDs.OrderBy(kvp => kvp.Value).First();
            projection.Add(new KeyValuePair<GameObject, int>(nextUnit.Key, nextUnit.Value));

            // Simulate next turn delay for this unit
            int estimatedDelay = CalculateDelay(nextUnit.Key, defaultCost);
            simulatedCDs[nextUnit.Key] += estimatedDelay;
        }

        return projection;
    }

    #endregion

    #region Hero Turn UI & Camera Handling

    private void SetHeroTurn()
    {
        if (currentFighter == null) return;

        Transform cameraContainer = currentFighter.transform.Find("ContainerTurnCamera");
        if (cameraContainer != null && turnCameraPrefab != null)
        {
            GameObject camInst = Instantiate(turnCameraPrefab, cameraContainer);
            turnCamera = camInst.GetComponent<Camera>();
            turnCamera.gameObject.SetActive(true);

            turnCameraMask = turnCamera.transform.GetComponentInChildren<Image>()?.gameObject;
            if (turnCameraMask != null)
            {
                Animator maskAnim = turnCameraMask.GetComponent<Animator>();
                maskAnim?.SetTrigger("enter");

                Transform turnImg = turnCameraMask.transform.Find("TurnImage");
                turnImg?.GetComponent<Animator>()?.SetTrigger("enter");
            }
        }

        if (battleUIController != null)
        {
            battleUIController.ShowOrdersPanel(true);
            battleUIController.ShowTurnsPanel(true);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SFXType.UI_Confirm);
        }
    }

    public void HideTurnCamera()
    {
        if (turnCameraMask != null)
        {
            turnCameraMask.GetComponent<Animator>()?.SetTrigger("exit");
            turnCameraMask.transform.Find("TurnImage")?.GetComponent<Animator>()?.SetTrigger("exit");
        }

        if (turnCamera != null)
        {
            Destroy(turnCamera.gameObject, 1f);
        }
    }

    #endregion
}