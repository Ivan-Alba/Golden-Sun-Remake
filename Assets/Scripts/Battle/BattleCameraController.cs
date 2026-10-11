using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>
/// Controller responsible for 3D battle Cinemachine virtual camera presentations,
/// UI transition snapshot fades via screen capture, secondary turn UI camera attachments, 
/// and broadcasting active primary rendering camera events.
/// Reactive to BattleManager state transitions via Event-Driven Architecture.
/// Compatible with Unity 6 (Cinemachine v3 API).
/// </summary>
public class BattleCameraController : MonoBehaviour
{
    #region Serialized Fields & References

    [Header("UI Render Transition")]
    [Tooltip("Main overlay transition container (toggled during battle sequence).")]
    [SerializeField] private GameObject cameraRenderImage;
    [SerializeField] private RawImage snapshotRawImage;
    [SerializeField] private CanvasGroup transitionCanvasGroup;

    [Header("Transition Settings")]
    [Tooltip("Duration in seconds for the screen snapshot overlay to fade out.")]
    [SerializeField] private float fadeDuration = 0.8f;

    [Header("Cinemachine Cameras (Unity 6 / v3)")]
    [Tooltip("Static camera positioned in the arena acting as a zero-blend spatial anchor before activating intro orbit.")]
    [SerializeField] private CinemachineCamera vCamBattleAnchor;
    [SerializeField] private CinemachineCamera vCamIntroOrbit;
    [SerializeField] private CinemachineCamera vCamIdleMenuOrbit;
    [SerializeField] private CinemachineCamera vCamCommandSelection;
    [SerializeField] private CinemachineCamera vCamEnemyTargeting;
    [SerializeField] private CinemachineCamera vCamHeroTargeting;
    [SerializeField] private CinemachineCamera vCamExecutionBase;
    [SerializeField] private CinemachineCamera vCamActionMelee;
    [Tooltip("Inverted 3/4 action camera for enemy Psynergy actions aimed at heroes.")]
    [SerializeField] private CinemachineCamera vCamEnemyActionMelee;
    [SerializeField] private CinemachineCamera vCamCriticalImpact;
    [SerializeField] private CinemachineCamera vCamVictoryOrbit;

    [Header("Cinemachine Target Groups")]
    [SerializeField] private CinemachineTargetGroup globalTargetGroup;
    [SerializeField] private CinemachineTargetGroup heroTargetGroup;
    [SerializeField] private CinemachineTargetGroup enemyTargetGroup;
    [SerializeField] private CinemachineTargetGroup actionTargetGroup;

    [Header("Camera Prefabs")]
    [SerializeField] private GameObject turnCameraPrefab;

    #endregion

    #region Private State & Priority Constants

    private const int PRIORITY_OFF = 0;
    private const int PRIORITY_ACTIVE = 20;

    // Persistent Turn Camera Instance (RenderTexture / HUD Sub-camera)
    private GameObject turnCameraInstance;
    private Camera turnCameraComp;

    // Internal Reference to PERSISTENT_CORE MainCamera
    private Camera battleCameraComp;

    private GameObject _currentHeroTurn;
    private CinemachineBlendDefinition _previousBlendValue;

    private TargetSelectionController _targetSelectionController;

    /// <summary>
    /// Dynamic accessor for the CinemachineBrain attached to the active rendering camera.
    /// Prevents null reference issues caused by script execution order or scene loading.
    /// </summary>
    private CinemachineBrain Brain
    {
        get
        {
            if (battleCameraComp != null)
            {
                var brainComp = battleCameraComp.GetComponent<CinemachineBrain>();
                if (brainComp != null) return brainComp;
            }

            if (Camera.main != null)
            {
                return Camera.main.GetComponent<CinemachineBrain>();
            }

            return null;
        }
    }

    // Cached Overworld / Exploration Active Camera
    private Camera _cachedPreviousCamera;

    // Lifecycle & Snapshot State Flags
    private Texture2D _screenSnapshot;
    private bool _isBattleCameraActive = false;

    private bool transitionDone;
    /// <summary>
    /// Execution flag consumed by orchestrators to await sequence completions.
    /// </summary>
    public bool TransitionDone { get => transitionDone; set => transitionDone = value; }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Camera.main != null)
        {
            battleCameraComp = Camera.main;
        }
        else
        {
            GameObject mainCamObj = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCamObj != null)
            {
                battleCameraComp = mainCamObj.GetComponent<Camera>();
            }
        }

        // Load turn camera prefab if not assigned in Inspector
        if (turnCameraPrefab == null)
        {
            turnCameraPrefab = Resources.Load<GameObject>("Prefabs/TurnCamera");
        }

        // Lazy search for RenderImageCanvas child if not assigned
        if (cameraRenderImage == null)
        {
            GameObject canvasObj = GameObject.Find("RenderImageCanvas");
            if (canvasObj != null && canvasObj.transform.childCount > 0)
            {
                cameraRenderImage = canvasObj.transform.GetChild(0).gameObject;
            }
        }

        if (cameraRenderImage != null)
        {
            if (snapshotRawImage == null)
                snapshotRawImage = cameraRenderImage.GetComponent<RawImage>();

            if (transitionCanvasGroup == null)
                transitionCanvasGroup = cameraRenderImage.GetComponent<CanvasGroup>();

            cameraRenderImage.SetActive(false);
        }

        if (_targetSelectionController == null)
        {
            _targetSelectionController = GetComponent<TargetSelectionController>()
                                       ?? GetComponentInParent<TargetSelectionController>();
        }
    }

    private void OnEnable()
    {
        GameEvents.Camera.OnActiveCameraChanged += HandleActiveCameraChanged;
    }

    private void OnDisable()
    {
        GameEvents.Camera.OnActiveCameraChanged -= HandleActiveCameraChanged;
    }

    private void OnDestroy()
    {
        if (_screenSnapshot != null)
        {
            Destroy(_screenSnapshot);
            _screenSnapshot = null;
        }
    }

    #endregion

    #region Event Callbacks & Event-Driven Orchestration

    private void HandleActiveCameraChanged(Camera newCamera)
    {
        if (newCamera == null) return;

        if (battleCameraComp != null && newCamera == battleCameraComp) return;
        if (turnCameraComp != null && newCamera == turnCameraComp) return;

        _cachedPreviousCamera = newCamera;
    }

    public void HandleBattleStateChanged(BattleState newState)
    {
        switch (newState)
        {
            case BattleState.CommandSelection:
            case BattleState.SubMenuSelection:
                SetTurnCameraOnHero(_currentHeroTurn);
                break;

            case BattleState.TargetSelection:
                ResetHeroTargetGroupWeights();
                if (_targetSelectionController != null && _targetSelectionController.IsTargetingAllies)
                {
                    SetHeroTargetingCamera();
                }
                else
                {
                    SetEnemyTargetingCamera();
                }
                break;

            case BattleState.VictoryFanfare:
                Victory();
                break;

            case BattleState.WaitingTurnExecution:
                HideTurnCamera();
                ResetHeroTargetGroupWeights();
                break;
        }
    }

    #endregion

    #region Priority & Camera Switch Helpers

    private void SetActiveVirtualCamera(CinemachineCamera activeVCam)
    {
        if (vCamBattleAnchor != null) vCamBattleAnchor.Priority = PRIORITY_OFF;
        if (vCamIntroOrbit != null) vCamIntroOrbit.Priority = PRIORITY_OFF;
        if (vCamIdleMenuOrbit != null) vCamIdleMenuOrbit.Priority = PRIORITY_OFF;
        if (vCamCommandSelection != null) vCamCommandSelection.Priority = PRIORITY_OFF;
        if (vCamEnemyTargeting != null) vCamEnemyTargeting.Priority = PRIORITY_OFF;
        if (vCamHeroTargeting != null) vCamHeroTargeting.Priority = PRIORITY_OFF;
        if (vCamExecutionBase != null) vCamExecutionBase.Priority = PRIORITY_OFF;
        if (vCamActionMelee != null) vCamActionMelee.Priority = PRIORITY_OFF;
        if (vCamEnemyActionMelee != null) vCamEnemyActionMelee.Priority = PRIORITY_OFF;
        if (vCamCriticalImpact != null) vCamCriticalImpact.Priority = PRIORITY_OFF;
        if (vCamVictoryOrbit != null) vCamVictoryOrbit.Priority = PRIORITY_OFF;

        if (activeVCam != null)
        {
            activeVCam.Priority = PRIORITY_ACTIVE;
        }
    }

    #endregion

    #region Two-Phase Battle Transition Pipeline

    public IEnumerator PrepareSnapshotShield()
    {
        transitionDone = false;

        if (!_isBattleCameraActive)
        {
            Camera targetCam = _cachedPreviousCamera != null ? _cachedPreviousCamera : Camera.main;

            if (targetCam != null)
            {
                RenderTexture rt = RenderTexture.GetTemporary(Screen.width, Screen.height, 24);
                targetCam.targetTexture = rt;
                targetCam.Render();

                RenderTexture.active = rt;
                if (_screenSnapshot != null) Destroy(_screenSnapshot);
                _screenSnapshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                _screenSnapshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                _screenSnapshot.Apply();

                targetCam.targetTexture = null;
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
            }

            if (snapshotRawImage != null)
            {
                snapshotRawImage.texture = _screenSnapshot;
                snapshotRawImage.color = Color.white;
            }

            if (transitionCanvasGroup != null)
            {
                transitionCanvasGroup.alpha = 1f;
            }

            if (cameraRenderImage != null)
            {
                cameraRenderImage.SetActive(true);
            }

            yield return null;

            CinemachineBrain activeBrain = Brain;
            if (activeBrain != null)
            {
                _previousBlendValue = activeBrain.DefaultBlend;
                activeBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            }

            CinemachineCamera targetVCam = vCamBattleAnchor != null ? vCamBattleAnchor : vCamIntroOrbit;

            if (targetVCam != null)
            {
                if (targetVCam.TryGetComponent<CinemachineSplineDolly>(out var dolly))
                {
                    dolly.CameraPosition = 0f;
                }

                SetActiveVirtualCamera(targetVCam);
                targetVCam.UpdateCameraState(Vector3.up, Time.deltaTime);
            }

            _isBattleCameraActive = true;

            yield return null;
        }

        transitionDone = true;
    }

    public IEnumerator ExecuteFadeAndStartIntro()
    {
        transitionDone = false;

        IntroBattle();

        yield return null;

        if (transitionCanvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                transitionCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                yield return null;
            }
            transitionCanvasGroup.alpha = 0f;
        }

        if (cameraRenderImage != null)
        {
            cameraRenderImage.SetActive(false);
        }

        CinemachineBrain brainToRestore = Brain;
        if (brainToRestore != null)
        {
            brainToRestore.DefaultBlend = _previousBlendValue;
        }

        transitionDone = true;
    }

    #endregion

    #region Camera Presentation Triggers

    public void IntroBattle()
    {
        if (vCamIntroOrbit != null && vCamIntroOrbit.TryGetComponent<CinemachineSplineDolly>(out var splineDolly))
        {
            splineDolly.CameraPosition = 0f;
        }

        SetActiveVirtualCamera(vCamIntroOrbit);
    }

    public void SetIdleMenuCamera()
    {
        SetActiveVirtualCamera(vCamIdleMenuOrbit);
    }

    public void SetTurnCameraOnHero(GameObject activeHero)
    {
        _currentHeroTurn = activeHero;
        AttachHUDTurnCameraToHero(activeHero);
        UpdateHeroTargetGroupWeights(activeHero);
        SetActiveVirtualCamera(vCamCommandSelection);
    }

    public void SetEnemyTargetingCamera()
    {
        SetActiveVirtualCamera(vCamEnemyTargeting);
    }

    public void SetHeroTargetingCamera()
    {
        SetActiveVirtualCamera(vCamHeroTargeting);
    }

    public void SetExecutionBaseCamera()
    {
        ResetGlobalTargetGroupWeights();
        ResetActionTargetGroup();
        SetActiveVirtualCamera(vCamExecutionBase);
    }

    /// <summary>
    /// Updates weights in globalTargetGroup (Caster = 2.0, Targets = 1.5, Others = 1.0).
    /// Activates vCamEnemyActionMelee ONLY if Caster is an Enemy AND CommandType is Psynergy.
    /// Otherwise activates standard 3/4 action camera (vCamActionMelee).
    /// </summary>
    public void SetActionCamera(GameObject caster, List<GameObject> targets, BattleCommandType commandType = BattleCommandType.Attack)
    {
        UpdateGlobalTargetGroupWeights(caster, targets);

        bool isEnemy = IsEnemyCaster(caster);
        bool isPsynergy = (commandType == BattleCommandType.Psynergy);

        if (isEnemy && isPsynergy && vCamEnemyActionMelee != null)
        {
            SetActiveVirtualCamera(vCamEnemyActionMelee);
        }
        else
        {
            SetActiveVirtualCamera(vCamActionMelee);
        }
    }

    public void SetActionMeleeCamera(GameObject attacker, GameObject target)
    {
        List<GameObject> targetsList = target != null ? new List<GameObject> { target } : new List<GameObject>();
        SetActionCamera(attacker, targetsList, BattleCommandType.Attack);
    }

    /// <summary>
    /// Populates actionTargetGroup ONLY with Caster and Target (tight focus framing) and activates vCamCriticalImpact.
    /// </summary>
    public void SetCriticalImpactCamera(GameObject attacker, GameObject target)
    {
        SetupCriticalImpactTargetGroup(attacker, target);
        SetActiveVirtualCamera(vCamCriticalImpact);
    }

    public void Victory(GameObject lastHitHero = null)
    {
        if (vCamVictoryOrbit != null && vCamVictoryOrbit.TryGetComponent<CinemachineSplineDolly>(out var splineDolly))
        {
            splineDolly.CameraPosition = 0f;
        }

        SetActiveVirtualCamera(vCamVictoryOrbit);
    }

    #endregion

    #region Target Group Weight Management & Caster Inspector

    public void SetupTargetGroups(List<GameObject> heroes, List<GameObject> enemies)
    {
        var globalTargets = new List<CinemachineTargetGroup.Target>();

        if (heroTargetGroup != null && heroes != null)
        {
            var heroTargets = new List<CinemachineTargetGroup.Target>();
            foreach (var hero in heroes)
            {
                if (hero != null)
                {
                    var targetStruct = new CinemachineTargetGroup.Target
                    {
                        Object = hero.transform,
                        Weight = 1f,
                        Radius = 1f
                    };
                    heroTargets.Add(targetStruct);
                    globalTargets.Add(targetStruct);
                }
            }
            heroTargetGroup.Targets = heroTargets;
        }

        if (enemyTargetGroup != null && enemies != null)
        {
            var enemyTargets = new List<CinemachineTargetGroup.Target>();
            foreach (var enemy in enemies)
            {
                if (enemy != null)
                {
                    var targetStruct = new CinemachineTargetGroup.Target
                    {
                        Object = enemy.transform,
                        Weight = 1f,
                        Radius = 1f
                    };
                    enemyTargets.Add(targetStruct);
                    globalTargets.Add(targetStruct);
                }
            }
            enemyTargetGroup.Targets = enemyTargets;
        }

        if (globalTargetGroup != null)
        {
            globalTargetGroup.Targets = new List<CinemachineTargetGroup.Target>(globalTargets);
        }

        ResetActionTargetGroup();
    }

    private void UpdateHeroTargetGroupWeights(GameObject activeHero)
    {
        if (heroTargetGroup == null || heroTargetGroup.Targets == null) return;

        var targets = heroTargetGroup.Targets;
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            if (target.Object != null)
            {
                bool isActive = (activeHero != null && target.Object.gameObject == activeHero);
                target.Weight = isActive ? 1.8f : 1f;
                targets[i] = target;
            }
        }
    }

    public void ResetHeroTargetGroupWeights()
    {
        if (heroTargetGroup == null || heroTargetGroup.Targets == null) return;

        var targets = heroTargetGroup.Targets;
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            target.Weight = 1f;
            targets[i] = target;
        }
    }

    /// <summary>
    /// Modifies weights in globalTargetGroup: Caster = 2.0, Affected Targets = 1.5, Others = 1.0.
    /// </summary>
    private void UpdateGlobalTargetGroupWeights(GameObject caster, List<GameObject> targets)
    {
        if (globalTargetGroup == null || globalTargetGroup.Targets == null) return;

        var groupTargets = globalTargetGroup.Targets;
        for (int i = 0; i < groupTargets.Count; i++)
        {
            var targetStruct = groupTargets[i];
            if (targetStruct.Object == null) continue;

            GameObject obj = targetStruct.Object.gameObject;

            if (obj == caster)
            {
                targetStruct.Weight = 1.5f;
            }
            else if (targets != null && targets.Contains(obj))
            {
                targetStruct.Weight = 1.2f;
            }
            else
            {
                targetStruct.Weight = 1.0f;
            }

            groupTargets[i] = targetStruct;
        }
    }

    /// <summary>
    /// Resets all target weights in globalTargetGroup back to 1.0f.
    /// </summary>
    public void ResetGlobalTargetGroupWeights()
    {
        if (globalTargetGroup == null || globalTargetGroup.Targets == null) return;

        var groupTargets = globalTargetGroup.Targets;
        for (int i = 0; i < groupTargets.Count; i++)
        {
            var targetStruct = groupTargets[i];
            targetStruct.Weight = 1.0f;
            groupTargets[i] = targetStruct;
        }
    }

    /// <summary>
    /// Rebuilds actionTargetGroup exclusively for Critical Impact framing (Caster = 1.2, Target = 1.0).
    /// </summary>
    private void SetupCriticalImpactTargetGroup(GameObject caster, GameObject target)
    {
        if (actionTargetGroup == null) return;

        var criticalTargets = new List<CinemachineTargetGroup.Target>();

        if (caster != null)
        {
            criticalTargets.Add(new CinemachineTargetGroup.Target
            {
                Object = caster.transform,
                Weight = 1.2f,
                Radius = 1.0f
            });
        }

        if (target != null && target != caster)
        {
            criticalTargets.Add(new CinemachineTargetGroup.Target
            {
                Object = target.transform,
                Weight = 1.0f,
                Radius = 1.0f
            });
        }

        actionTargetGroup.Targets = criticalTargets;
    }

    /// <summary>
    /// Clears and resets actionTargetGroup targets.
    /// </summary>
    public void ResetActionTargetGroup()
    {
        if (actionTargetGroup != null)
        {
            actionTargetGroup.Targets = new List<CinemachineTargetGroup.Target>();
        }
    }

    /// <summary>
    /// Evaluates if a given caster is an Enemy by querying FighterStats or enemyTargetGroup.
    /// </summary>
    private bool IsEnemyCaster(GameObject caster)
    {
        if (caster == null) return false;

        if (caster.TryGetComponent<FighterStats>(out var stats))
        {
            return !stats.IsHero;
        }

        if (enemyTargetGroup != null && enemyTargetGroup.Targets != null)
        {
            foreach (var t in enemyTargetGroup.Targets)
            {
                if (t.Object != null && t.Object.gameObject == caster)
                {
                    return true;
                }
            }
        }

        return false;
    }

    #endregion

    #region Turn Camera Management (Sub-Camera HUD Output)

    private void AttachHUDTurnCameraToHero(GameObject hero)
    {
        if (hero == null) return;

        Transform container = hero.transform.Find("ContainerTurnCamera");
        if (container == null) return;

        if (turnCameraInstance == null && turnCameraPrefab != null)
        {
            turnCameraInstance = Instantiate(turnCameraPrefab);
        }

        if (turnCameraInstance != null)
        {
            turnCameraInstance.transform.SetParent(container, false);
            turnCameraInstance.transform.localPosition = Vector3.zero;
            turnCameraInstance.transform.localRotation = Quaternion.identity;

            turnCameraComp = turnCameraInstance.GetComponent<Camera>();
            if (turnCameraComp != null)
            {
                turnCameraComp.enabled = true;
            }

            turnCameraInstance.SetActive(true);
        }
    }

    public void HideTurnCamera()
    {
        if (turnCameraInstance == null) return;

        if (turnCameraComp != null)
        {
            turnCameraComp.enabled = false;
        }

        turnCameraInstance.SetActive(false);
    }

    #endregion

    #region Teardown & Reset

    public void SetOffBattleCamera()
    {
        SetActiveVirtualCamera(null);

        if (turnCameraInstance != null)
        {
            Destroy(turnCameraInstance);
            turnCameraInstance = null;
            turnCameraComp = null;
        }

        _isBattleCameraActive = false;

        if (_cachedPreviousCamera != null)
        {
            GameEvents.Camera.TriggerActiveCameraChanged(_cachedPreviousCamera);
        }
    }

    #endregion
}