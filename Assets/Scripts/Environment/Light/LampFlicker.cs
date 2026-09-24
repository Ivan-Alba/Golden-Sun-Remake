using UnityEngine;

/// <summary>
/// Controls flickering effect for small lamps, lanterns, and candles.
/// Optimized for performance (zero allocation) and natural visual dynamic.
/// </summary>
[RequireComponent(typeof(Light))]
public class LampFlicker : MonoBehaviour
{
    #region Serialized Fields

    [Header("Intensity Variation")]
    [Tooltip("Base intensity for the lamp light.")]
    [SerializeField] private float baseIntensity = 1.2f;

    [Tooltip("Maximum intensity fluctuation around base value.")]
    [SerializeField] private float intensityFlickerRange = 0.25f;

    [Header("Flicker Behavior")]
    [Tooltip("Speed of the flickering effect.")]
    [SerializeField] private float flickerSpeed = 15.0f;

    [Tooltip("Adds occasional sharper dips in intensity (simulates a draft or sputtering wick).")]
    [SerializeField] private bool enableSputterEffect = true;

    #endregion

    #region Private Fields

    private Light targetLight;
    private float noiseSeed;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        targetLight = GetComponent<Light>();

        // Unique seed so multiple lamps on screen flicker asynchronously
        noiseSeed = Random.Range(0f, 1000f);

        // Auto-assign current light intensity as base if not manually configured
        if (baseIntensity <= 0f)
        {
            baseIntensity = targetLight.intensity;
        }
    }

    private void Update()
    {
        if (targetLight == null) return;

        // Calculate continuous smooth Perlin Noise
        float timeValue = (Time.time * flickerSpeed) + noiseSeed;
        float noise = Mathf.PerlinNoise(timeValue, 0f);

        // Map noise (0 to 1) to (-intensityFlickerRange to +intensityFlickerRange)
        float calculatedIntensity = baseIntensity + ((noise - 0.5f) * 2f * intensityFlickerRange);

        // Optional sputtering effect for lamps
        if (enableSputterEffect)
        {
            float sputterNoise = Mathf.PerlinNoise(0f, timeValue * 0.5f);
            if (sputterNoise > 0.8f)
            {
                calculatedIntensity -= intensityFlickerRange * 0.8f;
            }
        }

        targetLight.intensity = Mathf.Max(0f, calculatedIntensity);
    }

    #endregion
}