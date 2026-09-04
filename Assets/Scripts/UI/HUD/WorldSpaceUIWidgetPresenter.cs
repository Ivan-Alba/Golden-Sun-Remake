using UnityEngine.UIElements;

/// <summary>
/// Presenter wrapping a UI Toolkit VisualElement for Overworld World-Space UIs.
/// Controls UXML elements without MonoBehaviours or classical uGUI canvas components.
/// </summary>
public class WorldSpaceUIWidgetPresenter
{
    #region UXML Element Selectors

    private readonly VisualElement rootElement;
    private readonly Label nameLabel;
    private readonly Label subTitleLabel;
    private readonly Label levelLabel;
    private readonly ProgressBar threatBar;
    private readonly VisualElement alertIcon;

    #endregion

    public VisualElement Root => rootElement;

    public WorldSpaceUIWidgetPresenter(VisualElement instantiatedUxml)
    {
        rootElement = instantiatedUxml;

        // Query UXML nodes by name or class
        nameLabel = rootElement.Q<Label>("entity-name");
        subTitleLabel = rootElement.Q<Label>("entity-subtitle");
        levelLabel = rootElement.Q<Label>("entity-level");
        threatBar = rootElement.Q<ProgressBar>("threat-bar");
        alertIcon = rootElement.Q<VisualElement>("alert-icon");
    }

    public void Setup(string entityName, string subTitle, int level)
    {
        if (nameLabel != null) nameLabel.text = entityName;

        if (subTitleLabel != null)
        {
            subTitleLabel.text = subTitle;
            subTitleLabel.style.display = string.IsNullOrEmpty(subTitle) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (levelLabel != null)
        {
            levelLabel.text = $"Lv. {level}";
            levelLabel.style.display = level > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    public void UpdateThreat(float threatPercentage, EnemyAlertState alertState)
    {
        if (threatBar != null)
        {
            threatBar.value = threatPercentage * 100f; // UI Toolkit ProgressBars usually work 0-100
        }

        if (alertIcon != null)
        {
            bool isAlerted = alertState == EnemyAlertState.Suspicious || alertState == EnemyAlertState.Chasing;
            alertIcon.style.display = isAlerted ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    public void SetPosition(float screenX, float screenY)
    {
        // Position UI Toolkit element in Panel space
        rootElement.style.position = Position.Absolute;
        rootElement.style.left = screenX;
        rootElement.style.top = screenY;
    }
}