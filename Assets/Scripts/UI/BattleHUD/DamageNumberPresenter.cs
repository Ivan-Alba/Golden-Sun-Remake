using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Presenter responsible for projecting 3D combat target locations onto the UI Toolkit panel
/// and displaying dynamic floating damage/healing text overlays.
/// </summary>
public class DamageNumbersPresenter : MonoBehaviour
{
    private UIDocument uiDocument;
    private VisualElement rootContainer;
    private Camera mainCamera;

    private readonly Vector3 _headOffset = new Vector3(0f, 1.8f, 0f);

    public enum DamageTextType
    {
        NormalDamage,
        CriticalDamage,
        Healing,
        Miss,
        StatusEffect
    }

    private void OnEnable()
    {
        GameEvents.Battle.OnDamageNumberRequested += SpawnFloatingNumber;
    }

    private void OnDisable()
    {
        GameEvents.Battle.OnDamageNumberRequested -= SpawnFloatingNumber;
    }

    public void Initialize(UIDocument document, Camera battleCamera)
    {
        uiDocument = document;
        mainCamera = battleCamera != null ? battleCamera : Camera.main;

        if (uiDocument != null)
        {
            rootContainer = uiDocument.rootVisualElement;
        }
    }

    /// <summary>
    /// Spawns a floating damage or healing label mapped to a 3D target GameObject.
    /// </summary>
    public void SpawnFloatingNumber(GameObject target, string text, DamageTextType textType)
    {
        if (target == null || rootContainer == null || mainCamera == null || rootContainer.panel == null) return;

        // 1. Calculate 3D position with vertical offset and transform to UI Toolkit Panel Space
        Vector3 worldPos = target.transform.position + _headOffset;

        // Check if target is behind camera frustum
        Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPos);
        if (screenPoint.z < 0) return;

        Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(rootContainer.panel, worldPos, mainCamera);

        // 2. Create Dynamic Label
        Label damageLabel = new Label(text);
        damageLabel.style.position = Position.Absolute;
        damageLabel.style.left = panelPos.x;
        damageLabel.style.top = panelPos.y;

        // Alignment offset (-50% X, -100% Y) to center horizontally above target point
        damageLabel.style.translate = new StyleTranslate(new Translate(new Length(-50, LengthUnit.Percent), new Length(-100, LengthUnit.Percent)));

        damageLabel.style.fontSize = 40;
        damageLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        // 3. Apply Style depending on damage type
        switch (textType)
        {
            case DamageTextType.NormalDamage:
                damageLabel.style.color = Color.white;
                break;
            case DamageTextType.CriticalDamage:
                damageLabel.style.color = new Color(1f, 0.85f, 0f); // Bright Gold
                damageLabel.style.fontSize = 50;
                break;
            case DamageTextType.Healing:
                damageLabel.style.color = new Color(0.2f, 1f, 0.3f); // Bright Green
                break;
            case DamageTextType.Miss:
                damageLabel.style.color = new Color(0.7f, 0.7f, 0.7f); // Gray
                break;
            case DamageTextType.StatusEffect:
                damageLabel.style.color = new Color(0.4f, 0.8f, 1f); // Light Cyan
                break;
        }

        rootContainer.Add(damageLabel);

        // 4. Animate Floating & Fading
        StartCoroutine(AnimateAndDestroyLabel(damageLabel, panelPos));
    }

    private IEnumerator AnimateAndDestroyLabel(Label label, Vector2 initialPos)
    {
        float duration = 0.8f;
        float elapsedTime = 0f;
        float floatDistance = 45f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;

            label.style.top = initialPos.y - (progress * floatDistance);

            if (progress > 0.5f)
            {
                float alpha = Mathf.Lerp(1f, 0f, (progress - 0.5f) * 2f);
                label.style.opacity = alpha;
            }

            yield return null;
        }

        rootContainer.Remove(label);
    }
}