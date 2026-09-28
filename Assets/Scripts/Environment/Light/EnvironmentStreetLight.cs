using System.Collections;
using UnityEngine;

/// <summary>
/// Controls street lamps and lanterns, progressively turning light intensity 
/// and material emission on/off based on the global in-game time.
/// </summary>
public class EnvironmentStreetLight : MonoBehaviour
{
    [Header("Lighting Components")]
    [SerializeField] private Light pointLight;
    [SerializeField] private MeshRenderer lampMeshRenderer;
    [SerializeField] private int materialIndex = 0;

    [Header("Intensity Settings")]
    [SerializeField] private float maxLightIntensity = 3.5f;
    [ColorUsage(true, true)]
    [SerializeField] private Color maxEmissionColor = new Color(1f, 0.6f, 0.2f) * 2f; // HDR emission

    [Header("Activation Time Ranges (24h Format)")]
    [Tooltip("Time window at dusk to fade in the light.")]
    [SerializeField] private float turnOnStartHour = 18.5f; // 18:30
    [SerializeField] private float turnOnEndHour = 19.5f;   // 19:30

    [Tooltip("Time window at dawn to fade out the light.")]
    [SerializeField] private float turnOffStartHour = 5.5f; // 05:30
    [SerializeField] private float turnOffEndHour = 6.5f;   // 06:30

    [Header("Flicker Effect")]
    [SerializeField] private bool enableFlicker = true;
    [SerializeField] private float flickerSpeed = 8.0f;
    [SerializeField] private float flickerAmount = 0.15f; // 15% intensity variation

    private Material instancedMaterial;
    private float currentProgress = 0f; // 0 = off, 1 = max light

    private void Awake()
    {
        if (pointLight == null) pointLight = GetComponentInChildren<Light>();

        if (lampMeshRenderer != null && materialIndex < lampMeshRenderer.materials.Length)
        {
            // Create a material instance so we don't modify the shared asset on disk
            instancedMaterial = lampMeshRenderer.materials[materialIndex];
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

    private void HandleTimeUpdated(float totalHours, int hourInt, int minuteInt)
    {
        // 1. Calculate turn-on / turn-off progress
        if (totalHours >= turnOnStartHour && totalHours <= turnOnEndHour)
        {
            // Dusk: Fading IN
            currentProgress = Mathf.InverseLerp(turnOnStartHour, turnOnEndHour, totalHours);
        }
        else if (totalHours >= turnOffStartHour && totalHours <= turnOffEndHour)
        {
            // Dawn: Fading OUT
            currentProgress = 1f - Mathf.InverseLerp(turnOffStartHour, turnOffEndHour, totalHours);
        }
        else if (totalHours > turnOnEndHour || totalHours < turnOffStartHour)
        {
            // Nighttime: Full ON
            currentProgress = 1f;
        }
        else
        {
            // Daytime: Full OFF
            currentProgress = 0f;
        }

        // 2. Apply to Light and Material
        ApplyLightState();
    }

    private void ApplyLightState()
    {
        bool shouldBeActive = currentProgress > 0.001f;

        if (pointLight != null)
        {
            pointLight.enabled = shouldBeActive;

            if (shouldBeActive)
            {
                // Optional flicker noise calculation for organic flame effect
                float flickerNoise = enableFlicker
                    ? (Mathf.PerlinNoise(Time.time * flickerSpeed, transform.position.x) - 0.5f) * 2f * flickerAmount
                    : 0f;

                float finalIntensity = (maxLightIntensity * currentProgress) * (1f + flickerNoise);
                pointLight.intensity = Mathf.Max(0f, finalIntensity);
            }
        }

        if (instancedMaterial != null)
        {
            if (shouldBeActive)
            {
                instancedMaterial.EnableKeyword("_EMISSION");
                Color targetEmission = Color.Lerp(Color.black, maxEmissionColor, currentProgress);
                instancedMaterial.SetColor("_EmissionColor", targetEmission);
            }
            else
            {
                instancedMaterial.SetColor("_EmissionColor", Color.black);
                instancedMaterial.DisableKeyword("_EMISSION");
            }
        }
    }
}