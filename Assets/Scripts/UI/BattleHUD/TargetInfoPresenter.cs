using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Controls the UI data and states of a floating target indicator in Battle.
/// Manages the nameplate text, selection visual state (primary/secondary), and 3D positioning.
/// </summary>
public class TargetInfoPresenter
{
    private const string HIDDEN_CLASS = "hidden";
    private const string SECONDARY_TARGET_CLASS = "secondary-target";

    public VisualElement RootElement { get; private set; }
    public GameObject BoundFighter { get; private set; }

    private Label _labelName;
    private Label _labelLevel;
    private ProgressBar _hpBar;
    private VisualElement _selectionPointer;

    public TargetInfoPresenter(VisualElement root, GameObject fighter)
    {
        RootElement = root;
        BoundFighter = fighter;

        if (RootElement == null)
        {
            Debug.LogError($"[{nameof(TargetInfoPresenter)}] RootElement is null.");
            return;
        }

        // Query elements
        _labelName = RootElement.Q<Label>("entity_name");
        _labelLevel = RootElement.Q<Label>("entity_level");
        _hpBar = RootElement.Q<ProgressBar>("hp_bar");
        _selectionPointer = RootElement.Q<VisualElement>("selection_pointer");

        // Initialize state
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
    /// Sets whether this target is the primary focus or a secondary target (in AoE spread).
    /// </summary>
    public void SetPrimaryTarget(bool isPrimary)
    {
        if (RootElement == null) return;

        if (isPrimary)
        {
            RootElement.RemoveFromClassList(SECONDARY_TARGET_CLASS);
        }
        else
        {
            RootElement.AddToClassList(SECONDARY_TARGET_CLASS);
        }
    }

    /// <summary>
    /// Projects the 3D position of the bound fighter onto the 2D UI coordinates.
    /// </summary>
    public void UpdateScreenPosition(Camera battleCamera, Vector3 verticalOffset)
    {
        if (RootElement == null || BoundFighter == null || battleCamera == null) return;
        if (RootElement.ClassListContains(HIDDEN_CLASS)) return; // Don't calculate if hidden

        Vector3 worldPos = BoundFighter.transform.position + verticalOffset;
        Vector3 screenPos = battleCamera.WorldToScreenPoint(worldPos);

        // Hide if behind the camera clipping plane
        if (screenPos.z < 0)
        {
            RootElement.style.display = DisplayStyle.None;
            return;
        }

        RootElement.style.display = DisplayStyle.Flex; // Restore if it was behind camera

        // Convert from Screen space (bottom-left) to UI Toolkit space (top-left)
        float uiX = screenPos.x;
        float uiY = Screen.height - screenPos.y;

        RootElement.style.left = uiX;
        RootElement.style.top = uiY;
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