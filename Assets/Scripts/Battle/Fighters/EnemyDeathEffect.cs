using System.Collections;
using UnityEngine;

/// <summary>
/// Dedicated visual component responsible exclusively for non-hero combatants.
/// Executes the FFVII/Golden Sun style death dissolve effect (flash red, URP transparency transition, fade out, and destruction).
/// </summary>
public class EnemyDeathEffect : MonoBehaviour
{
    [Header("Dissolve Timing Settings")]
    [Tooltip("Total duration of the red flash and alpha fade out process.")]
    [SerializeField] private float defaultDuration = 1.2f;

    [Tooltip("Percentage of total duration allocated to the initial red flash (0.3 = 30%).")]
    [Range(0.1f, 0.5f)]
    [SerializeField] private float flashRatio = 0.3f;

    /// <summary>
    /// Public API to trigger the death dissolve sequence.
    /// </summary>
    public void PlayDeathEffect(float? overrideDuration = null)
    {
        float duration = overrideDuration ?? defaultDuration;
        StartCoroutine(FFVIIDeathEffectCoroutine(duration));
    }

    private IEnumerator FFVIIDeathEffectCoroutine(float duration)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Destroy(gameObject);
            yield break;
        }

        Material[] materials = new Material[renderers.Length];
        Color[] originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            materials[i] = renderers[i].material;

            if (materials[i].HasProperty("_BaseColor"))
            {
                originalColors[i] = materials[i].GetColor("_BaseColor");
            }
            else if (materials[i].HasProperty("_Color"))
            {
                originalColors[i] = materials[i].GetColor("_Color");
            }
            else
            {
                originalColors[i] = Color.white;
            }
        }

        float elapsedTime = 0f;
        float flashDuration = duration * flashRatio;
        float fadeDuration = duration * (1f - flashRatio);

        // Phase 1: Lerp color to target red flash
        while (elapsedTime < flashDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / flashDuration;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;

                Color targetRed = Color.Lerp(originalColors[i], new Color(1f, 0.1f, 0.1f, originalColors[i].a), t);
                SetMaterialColor(materials[i], targetRed);
            }

            yield return null;
        }

        // Phase 2: Enable URP Transparency properties
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == null) continue;
            EnableURPTransparency(materials[i]);
        }

        // Phase 3: Fade Alpha to 0
        elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;

                Color currentColor = new Color(1f, 0.1f, 0.1f, alpha);
                SetMaterialColor(materials[i], currentColor);
            }

            yield return null;
        }

        gameObject.SetActive(false);
        Destroy(gameObject, 0.2f);
    }

    private void EnableURPTransparency(Material mat)
    {
        if (mat == null) return;

        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f);
        }

        mat.SetFloat("_Blend", 0f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void SetMaterialColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }
    }
}