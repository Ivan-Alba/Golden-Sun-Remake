using UnityEngine;

/// <summary>
/// Holds configuration data for a specific time-of-day lighting phase in Unity URP.
/// Defined by in-game hour ranges (0.00 to 24.00).
/// </summary>
[CreateAssetMenu(fileName = "NewLightingPhase", menuName = "GoldenSun/Environment/Lighting Phase")]
public class LightingPhaseSO : ScriptableObject
{
    [Header("Phase Information")]
    [Tooltip("Display name or identifier of the phase (e.g. Morning, Day, Evening, Night).")]
    public string phaseName = "Day";

    [Tooltip("Start hour for this phase in 24h format (e.g. 6.0 for 06:00 AM, 18.0 for 06:00 PM).")]
    [Range(0f, 24f)] public float startHour = 10f;

    [Tooltip("End hour for this phase in 24h format (e.g. 18.0 for 06:00 PM, 6.0 for 06:00 AM next day).")]
    [Range(0f, 24f)] public float endHour = 18f;

    [Header("Directional Light (Sun / Moon)")]
    [ColorUsage(false, true)]
    public Color lightColor = Color.white;

    [Tooltip("Starting and ending intensity for smooth fading between phases.")]
    public float startLightIntensity = 1.2f;
    public float endLightIntensity = 1.2f;

    [Tooltip("Rotation angles for the Directional Light (X = Pitch/Tilt, Y = Yaw/Direction).")]
    public Vector3 startRotation = new Vector3(50f, -30f, 0f);
    public Vector3 endRotation = new Vector3(50f, -30f, 0f);

    [Header("Skybox & Ambient")]
    public Material skyboxMaterial;

    [Tooltip("Environment lighting intensity derived from Skybox (e.g. 1.2 for Day, 0.4 for Night).")]
    public float environmentIntensity = 1.2f;

    [Header("Fog Configuration")]
    public bool enableFog = true;
    public Color fogColor = new Color(0.35f, 0.72f, 0.88f);
    public FogMode fogMode = FogMode.ExponentialSquared;
    public float fogDensity = 0.007f;

    #region Helper Methods

    /// <summary>
    /// Calculates total duration of this phase in in-game hours.
    /// Handles overnight wrap-around (e.g. 21.0 to 6.0 = 9 hours).
    /// </summary>
    public float GetDurationHours()
    {
        if (endHour >= startHour)
            return endHour - startHour;
        else
            return (24f - startHour) + endHour; // Wraps past midnight
    }

    /// <summary>
    /// Evaluates if a given in-game time (0-24) falls within this phase span.
    /// </summary>
    public bool ContainsTime(float currentHour)
    {
        if (startHour < endHour)
        {
            return currentHour >= startHour && currentHour < endHour;
        }
        else // Overnight phase (e.g. 21:00 to 06:00)
        {
            return currentHour >= startHour || currentHour < endHour;
        }
    }

    /// <summary>
    /// Returns normalized progress (0.0 to 1.0) of a specific time within this phase.
    /// </summary>
    public float GetNormalizedProgress(float currentHour)
    {
        float duration = GetDurationHours();
        if (duration <= 0f) return 0f;

        float elapsed;
        if (startHour <= endHour)
        {
            elapsed = currentHour - startHour;
        }
        else
        {
            elapsed = (currentHour >= startHour) ? (currentHour - startHour) : ((24f - startHour) + currentHour);
        }

        return Mathf.Clamp01(elapsed / duration);
    }

    #endregion
}