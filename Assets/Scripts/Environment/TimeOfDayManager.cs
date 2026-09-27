using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages directional light, skybox, fog, and environment settings.
/// Fully driven by global in-game time events from GameManager (No Update loop).
/// </summary>
public class TimeOfDayManager : MonoBehaviour
{
    #region Serialized Fields

    [Header("System Control")]
    [Tooltip("Toggle to enable or disable the day/night cycle lighting in this scene.")]
    [SerializeField] private bool isCycleActive = true;

    [Header("Scene References")]
    [SerializeField] private Light mainDirectionalLight;

    [Header("Phases Configuration")]
    [Tooltip("List of ordered lighting phases matching 24h ranges.")]
    [SerializeField] private List<LightingPhaseSO> phases = new List<LightingPhaseSO>();

    #endregion

    #region Private Fields & Runtime State

    private int currentPhaseIndex = -1;

    #endregion

    #region Events

    public static event Action<LightingPhaseSO, int> OnPhaseChanged;

    #endregion

    #region Properties

    public bool IsCycleActive { get => isCycleActive; set => isCycleActive = value; }
    public LightingPhaseSO CurrentPhase => (phases != null && currentPhaseIndex >= 0 && currentPhaseIndex < phases.Count) ? phases[currentPhaseIndex] : null;

    /// <summary>
    /// Lazy accessor for the main directional light to guarantee a valid reference even if missing at Awake.
    /// </summary>
    private Light MainDirectionalLight
    {
        get
        {
            if (mainDirectionalLight == null)
            {
                mainDirectionalLight = RenderSettings.sun;
                if (mainDirectionalLight == null)
                {
                    var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                    foreach (var l in lights)
                    {
                        if (l.type == LightType.Directional)
                        {
                            mainDirectionalLight = l;
                            break;
                        }
                    }
                }
            }
            return mainDirectionalLight;
        }
    }

    #endregion

    #region Unity Lifecycle & Event Subscriptions

    private void Awake()
    {
        // Force initial lazy resolution during Awake
        _ = MainDirectionalLight;
    }

    private void OnEnable()
    {
        GameEvents.Environment.OnTimeUpdated += HandleTimeUpdated;
    }

    private void OnDisable()
    {
        GameEvents.Environment.OnTimeUpdated -= HandleTimeUpdated;
    }

    #endregion

    #region Event Handlers & Lighting Calculation

    private void HandleTimeUpdated(float totalHours, int hourInt, int minuteInt)
    {
        if (!isCycleActive || phases == null || phases.Count == 0) return;

        // 1. Find phase containing totalHours
        int evaluatedIndex = -1;
        LightingPhaseSO activePhase = null;

        for (int i = 0; i < phases.Count; i++)
        {
            if (phases[i] != null && phases[i].ContainsTime(totalHours))
            {
                evaluatedIndex = i;
                activePhase = phases[i];
                break;
            }
        }

        if (activePhase == null) return;

        // 2. Trigger Phase Change Event if entering new phase
        if (evaluatedIndex != currentPhaseIndex)
        {
            currentPhaseIndex = evaluatedIndex;
            ApplyPhaseEnvironment(activePhase);

            GameEvents.Environment.TriggerPhaseChangeTriggered(activePhase);
            OnPhaseChanged?.Invoke(activePhase, currentPhaseIndex);
        }

        // 3. Interpolate Directional Light Rotation & Intensity continuously
        float t = activePhase.GetNormalizedProgress(totalHours);

        Light dirLight = MainDirectionalLight;
        if (dirLight != null)
        {
            Vector3 interpolatedRotation = Vector3.Lerp(activePhase.startRotation, activePhase.endRotation, t);
            dirLight.transform.rotation = Quaternion.Euler(interpolatedRotation);

            dirLight.intensity = Mathf.Lerp(activePhase.startLightIntensity, activePhase.endLightIntensity, t);
        }
    }

    public void ApplyPhaseEnvironment(LightingPhaseSO phase)
    {
        if (phase == null) return;

        Light dirLight = MainDirectionalLight;
        if (dirLight != null)
        {
            dirLight.color = phase.lightColor;
        }

        if (phase.skyboxMaterial != null)
        {
            RenderSettings.skybox = phase.skyboxMaterial;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = phase.environmentIntensity;

        DynamicGI.UpdateEnvironment();

        RenderSettings.fog = phase.enableFog;
        RenderSettings.fogColor = phase.fogColor;
        RenderSettings.fogMode = phase.fogMode;
        RenderSettings.fogDensity = phase.fogDensity;
    }

    #endregion
}