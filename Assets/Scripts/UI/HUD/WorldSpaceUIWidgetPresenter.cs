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

    // Sub-template elements for interaction prompts
    private readonly VisualElement promptWrapper;
    private readonly Label keyLabel;
    private readonly Label actionLabel;

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

        // Interaction Prompt Sub-Template selectors
        promptWrapper = rootElement.Q<VisualElement>("interaction-prompt-wrapper");

        // If promptWrapper is null (e.g., GenericInteractionPrompt root), search directly in rootElement
        VisualElement searchContainer = promptWrapper ?? rootElement;
        keyLabel = searchContainer.Q<Label>("label_prompt_key");
        actionLabel = searchContainer.Q<Label>("label_action_text");
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

        // Hide prompt by default on setup
        SetInteractionPrompt(false);
    }

    public void UpdateThreat(float threatPercentage, EnemyAlertState alertState)
    {
        float calculatedValue = threatPercentage * 100f;

        // 1. Show/Hide threat bar based on value
        if (threatBar != null)
        {
            threatBar.value = calculatedValue;
            bool showBar = calculatedValue > 0f;
            threatBar.style.display = showBar ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // 2. Manage alert icon and color modifier CSS classes
        if (alertIcon != null)
        {
            bool isAlerted = alertState == EnemyAlertState.Suspicious || alertState == EnemyAlertState.Chasing;
            alertIcon.style.display = isAlerted ? DisplayStyle.Flex : DisplayStyle.None;

            if (isAlerted)
            {
                alertIcon.RemoveFromClassList("alert-suspicious");
                alertIcon.RemoveFromClassList("alert-chasing");

                if (alertState == EnemyAlertState.Suspicious)
                {
                    alertIcon.AddToClassList("alert-suspicious");
                }
                else if (alertState == EnemyAlertState.Chasing)
                {
                    alertIcon.AddToClassList("alert-chasing");
                }
            }
        }
    }

    /// <summary>
    /// Displays or hides the embedded interaction prompt block and updates key/action labels.
    /// </summary>
    public void SetInteractionPrompt(bool visible, string keyPrompt = "", string actionText = "")
    {
        if (promptWrapper != null)
        {
            promptWrapper.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (visible)
        {
            if (keyLabel != null) keyLabel.text = keyPrompt;
            if (actionLabel != null) actionLabel.text = actionText;
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