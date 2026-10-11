using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Presenter responsible for projecting 3D combat target locations onto the UI Toolkit panel
/// and displaying dynamic floating damage/healing text overlays.
/// Supports multi-line centered text formatting (e.g. CRITICAL!\n150).
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
        PPRecovery,
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

    public void Initialize(UIDocument document, Camera camera)
    {
        uiDocument = document;
        mainCamera = camera != null ? camera : Camera.main;

        if (uiDocument != null)
        {
            rootContainer = uiDocument.rootVisualElement;
        }
    }

    /// <summary>
    /// Spawns a floating damage or healing label mapped to a 3D target GameObject.
    /// Uses FighterBattleAnchors for precise positioning if available, falling back to standard offset.
    /// </summary>
    public void SpawnFloatingNumber(GameObject target, string text, DamageTextType textType)
    {
        if (target == null || rootContainer == null || mainCamera == null || rootContainer.panel == null) return;

        // 1. Calculate 3D world position using FighterBattleAnchors provider or fallback offset
        Vector3 worldPos = target.TryGetComponent<FighterBattleAnchors>(out var anchors)
            ? anchors.GetDamageWorldPosition()
            : target.transform.position + _headOffset;

        // Check if target is behind camera frustum
        Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPos);
        if (screenPoint.z < 0) return;

        Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(rootContainer.panel, worldPos, mainCamera);

        // Slight position jitter offset to prevent overlapping when multiple floating texts spawn simultaneously
        panelPos.x += UnityEngine.Random.Range(-12f, 12f);
        panelPos.y += UnityEngine.Random.Range(-8f, 8f);

        // 2. Create Dynamic Label
        Label damageLabel = new Label(text);
        damageLabel.style.position = Position.Absolute;
        damageLabel.style.left = panelPos.x;
        damageLabel.style.top = panelPos.y;

        // Alignment offset (-50% X, -100% Y) to center the label box horizontally above the target point
        damageLabel.style.translate = new StyleTranslate(new Translate(new Length(-50, LengthUnit.Percent), new Length(-100, LengthUnit.Percent)));

        // Multi-line internal text alignment centered
        damageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        damageLabel.style.fontSize = 60;
        damageLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        // --- ENHANCED READABILITY (Text Outline & Shadow) ---
        damageLabel.style.unityTextOutlineColor = Color.black;
        damageLabel.style.unityTextOutlineWidth = 2f;

        TextShadow textShadow = new TextShadow
        {
            offset = new Vector2(2f, 2f),
            blurRadius = 1f,
            color = new Color(0f, 0f, 0f, 0.85f)
        };
        damageLabel.style.textShadow = new StyleTextShadow(textShadow);

        // 3. Apply Style depending on damage type
        switch (textType)
        {
            case DamageTextType.NormalDamage:
                damageLabel.style.color = Color.white;
                break;
            case DamageTextType.CriticalDamage:
                damageLabel.style.color = new Color(1f, 0.85f, 0f); // Bright Gold
                damageLabel.style.fontSize = 70;
                damageLabel.style.unityTextOutlineWidth = 3f; // Heavy outline for crits
                break;
            case DamageTextType.Healing:
                damageLabel.style.color = new Color(0.2f, 1f, 0.3f); // Bright Green
                break;
            case DamageTextType.PPRecovery:
                damageLabel.style.color = new Color(0.2f, 0.6f, 1f); // Bright PP Blue
                break;
            case DamageTextType.Miss:
                damageLabel.style.color = new Color(0.85f, 0.85f, 0.85f); // Soft Gray
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