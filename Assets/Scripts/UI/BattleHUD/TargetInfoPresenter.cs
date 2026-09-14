using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Controls the UI data and states of a floating target indicator in Battle.
/// Manages the nameplate text and level display, and projects its 3D position onto the UI panel.
/// </summary>
public class TargetInfoPresenter
{
    private const string HIDDEN_CLASS = "hidden";

    public VisualElement RootElement { get; private set; }
    public GameObject BoundFighter { get; private set; }

    private Label _labelName;
    private Label _labelLevel;

    public TargetInfoPresenter(VisualElement root, GameObject fighter)
    {
        RootElement = root;
        BoundFighter = fighter;

        if (RootElement == null)
        {
            Debug.LogError($"[{nameof(TargetInfoPresenter)}] RootElement is null.");
            return;
        }

        // Query name and level labels
        _labelName = RootElement.Q<Label>("entity_name");
        _labelLevel = RootElement.Q<Label>("entity_level");

        // Initialize state as hidden
        SetVisible(false);
    }

    /// <summary>
    /// Updates the text and data displayed on the nameplate.
    /// </summary>
    public void UpdateData(string name, int level = -1)
    {
        if (_labelName != null)
        {
            _labelName.text = name;
        }

        if (_labelLevel != null)
        {
            if (level > 0)
            {
                _labelLevel.text = $"Lv. {level}";
                _labelLevel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _labelLevel.style.display = DisplayStyle.None;
            }
        }
    }

    /// <summary>
    /// Projects the 3D position of the bound fighter onto UI Toolkit panel coordinates.
    /// Uses FighterBattleAnchors if available on the bound fighter, falling back to verticalOffset.
    /// Centering is handled seamlessly via CSS translate(-50%, -100%).
    /// </summary>
    public void UpdatePanelPosition(IPanel panel, Camera battleCamera, Vector3 fallbackVerticalOffset)
    {
        if (RootElement == null || BoundFighter == null || battleCamera == null || panel == null) return;
        if (RootElement.ClassListContains(HIDDEN_CLASS)) return;

        // Determine 3D world position via FighterBattleAnchors or fallback offset
        Vector3 worldPos = BoundFighter.TryGetComponent<FighterBattleAnchors>(out var anchors)
            ? anchors.GetTargetInfoWorldPosition()
            : BoundFighter.transform.position + fallbackVerticalOffset;

        // Hide if behind the camera clipping plane
        Vector3 screenPoint = battleCamera.WorldToScreenPoint(worldPos);
        if (screenPoint.z < 0)
        {
            RootElement.style.visibility = Visibility.Hidden;
            return;
        }

        RootElement.style.visibility = Visibility.Visible;

        // Transform 3D world space position directly to UI Toolkit panel space coordinates
        Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(panel, worldPos, battleCamera);

        RootElement.style.position = Position.Absolute;

        // Direct assignment: CSS translate(-50%, -100%) aligns the horizontal center and bottom anchor perfectly
        RootElement.style.left = panelPos.x;
        RootElement.style.top = panelPos.y;
    }

    /// <summary>
    /// Toggles the opacity/scale hidden class for smooth fading.
    /// </summary>
    public void SetVisible(bool isVisible)
    {
        if (RootElement == null) return;

        if (isVisible)
        {
            RootElement.RemoveFromClassList(HIDDEN_CLASS);
        }
        else
        {
            RootElement.AddToClassList(HIDDEN_CLASS);
        }
    }
}