using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering; // Required for LensFlareComponentSRP

/// <summary>
/// Manages directional light, skybox blending via Cubemaps, fog, and environment settings.
/// Fully driven by global in-game time events from GameManager (No Update loop).
/// Performs seamless continuous blending of colors, fog, and light intensities across phases.
/// </summary>
public class TimeOfDayManager : MonoBehaviour
{
    #region Serialized Fields

    [Header("System Control")]
    [Tooltip("Toggle to enable or disable the day/night cycle lighting in this scene.")]
    [SerializeField] private bool isCycleActive = true;

    [Header("Scene References")]
    [SerializeField] private Light mainDirectionalLight;
    [SerializeField] private LensFlareComponentSRP mainLensFlareComponent;

    [Header("Skybox Blending Material")]
    [Tooltip("Assign the material using SG_SkyboxBlend shader.")]
    [SerializeField] private Material skyboxBlendedMaterial;

    [Header("Phases Configuration")]
    [Tooltip("List of ordered lighting phases matching 24h ranges.")]
    [SerializeField] private List<LightingPhaseSO> phases = new List<LightingPhaseSO>();

    #endregion

    #region Shader Property IDs Cache

    private static readonly int CubemapCurrentID = Shader.PropertyToID("_Cubemap_Current");
    private static readonly int CubemapNextID = Shader.PropertyToID("_Cubemap_Next");
    private static readonly int BlendID = Shader.PropertyToID("_Blend");
    private static readonly int SunDirectionID = Shader.PropertyToID("_SunDirection");
    private static readonly int MoonDirectionID = Shader.PropertyToID("_MoonDirection");

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

    private LensFlareComponentSRP MainLensFlare
    {
        get
        {
            if (mainLensFlareComponent == null && MainDirectionalLight != null)
            {
                mainLensFlareComponent = MainDirectionalLight.GetComponent<LensFlareComponentSRP>();
            }
            return mainLensFlareComponent;
        }
    }

    #endregion

    #region Unity Lifecycle & Event Subscriptions

    private void Awake()
    {
        _ = MainDirectionalLight;
        _ = MainLensFlare;

        if (skyboxBlendedMaterial != null)
        {
            RenderSettings.skybox = skyboxBlendedMaterial;
        }
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

        // 3. Calculate phase progress (0.0 to 1.0) and evaluate Master Transition Curve
        float linearT = activePhase.GetNormalizedProgress(totalHours);
        float transitionedT = activePhase.EvaluateTransitionProgress(linearT);

        int nextPhaseIndex = (currentPhaseIndex + 1) % phases.Count;
        LightingPhaseSO nextPhase = phases[nextPhaseIndex];

        // 4. Directional Light Calculations
        Light dirLight = MainDirectionalLight;
        if (dirLight != null)
        {
            // Note: Rotation stays linear (linearT) so astro celestial movement remains constant and smooth
            Vector3 currentEuler = Vector3.Lerp(activePhase.startRotation, activePhase.endRotation, linearT);
            dirLight.transform.rotation = Quaternion.Euler(currentEuler);

            // Light Intensity & Light Color use the Master Transition Curve (transitionedT)
            dirLight.intensity = Mathf.Lerp(activePhase.startLightIntensity, activePhase.endLightIntensity, transitionedT);

            if (nextPhase != null)
            {
                dirLight.color = Color.Lerp(activePhase.lightColor, nextPhase.lightColor, transitionedT);
            }

            // Update Sun and Moon vectors in Skybox Material
            if (skyboxBlendedMaterial != null)
            {
                Vector3 sunDir = -dirLight.transform.forward;
                skyboxBlendedMaterial.SetVector(SunDirectionID, sunDir);
                skyboxBlendedMaterial.SetVector(MoonDirectionID, -sunDir);
            }

            // Update Sun Lens Flare intensity and active state based on horizon elevation
            UpdateSunLensFlareState(dirLight);
        }

        // 5. Seamless Fog Interpolation using Master Curve
        bool isFogCurrentlyActive = activePhase.enableFog || (nextPhase != null && nextPhase.enableFog);
        RenderSettings.fog = isFogCurrentlyActive;

        if (isFogCurrentlyActive)
        {
            Color targetFogColor = (nextPhase != null && nextPhase.enableFog) ? nextPhase.fogColor : activePhase.fogColor;
            float targetFogDensity = (nextPhase != null && nextPhase.enableFog) ? nextPhase.fogDensity : 0.0f;
            float startFogDensity = activePhase.enableFog ? activePhase.fogDensity : 0.0f;

            RenderSettings.fogColor = Color.Lerp(activePhase.fogColor, targetFogColor, transitionedT);
            RenderSettings.fogDensity = Mathf.Lerp(startFogDensity, targetFogDensity, transitionedT);
        }

        // 6. Ambient Light & Skybox Blend using Master Curve
        if (nextPhase != null)
        {
            RenderSettings.ambientIntensity = Mathf.Lerp(activePhase.environmentIntensity, nextPhase.environmentIntensity, transitionedT);
        }

        if (skyboxBlendedMaterial != null)
        {
            skyboxBlendedMaterial.SetFloat(BlendID, transitionedT);
        }

        DynamicGI.UpdateEnvironment();
    }

    /// <summary>
    /// Evaluates Sun elevation above the horizon plane to smoothly fade out and disable Lens Flare 
    /// at dusk, and reactivate/fade it in at dawn as it emerges from the opposite side.
    /// </summary>
    private void UpdateSunLensFlareState(Light dirLight)
    {
        LensFlareComponentSRP flareComp = MainLensFlare;
        if (flareComp == null) return;

        // Vector3.Dot between sun direction (-forward) and world UP vector.
        // > 0 = Above horizon (Daytime / Rising / Setting)
        // <= 0 = Below horizon (Nighttime / Underground)
        float sunElevation = Vector3.Dot(-dirLight.transform.forward, Vector3.up);

        if (sunElevation <= 0f)
        {
            flareComp.enabled = false;
            flareComp.intensity = 0f;
        }
        else
        {
            flareComp.enabled = true;
            // Smoothly scale intensity from 0.0 to 1.0 within a 0.1 elevation window above the horizon
            float horizonFade = Mathf.Clamp01(sunElevation / 0.1f);
            flareComp.intensity = horizonFade;
        }
    }

    public void ApplyPhaseEnvironment(LightingPhaseSO phase)
    {
        if (phase == null) return;

        if (skyboxBlendedMaterial != null && phases.Count > 0)
        {
            int nextPhaseIndex = (currentPhaseIndex + 1) % phases.Count;
            LightingPhaseSO nextPhase = phases[nextPhaseIndex];

            Texture currentTex = ExtractSkyboxTexture(phase);
            if (currentTex != null)
            {
                skyboxBlendedMaterial.SetTexture(CubemapCurrentID, currentTex);
            }

            if (nextPhase != null)
            {
                Texture nextTex = ExtractSkyboxTexture(nextPhase);
                if (nextTex != null)
                {
                    skyboxBlendedMaterial.SetTexture(CubemapNextID, nextTex);
                }
            }
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.fogMode = phase.fogMode;
    }

    private Texture ExtractSkyboxTexture(LightingPhaseSO phase)
    {
        if (phase == null) return null;

        if (phase.skyboxCubemap != null)
        {
            return phase.skyboxCubemap;
        }

        if (phase.skyboxMaterial != null)
        {
            if (phase.skyboxMaterial.HasProperty("_Tex"))
            {
                return phase.skyboxMaterial.GetTexture("_Tex");
            }
            if (phase.skyboxMaterial.HasProperty("_FrontTex"))
            {
                return phase.skyboxMaterial.GetTexture("_FrontTex");
            }
            return phase.skyboxMaterial.mainTexture;
        }

        return null;
    }

    #endregion
}