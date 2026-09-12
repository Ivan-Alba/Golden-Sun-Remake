using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Lightweight presenter for the battle command cross UI Toolkit overlay.
/// Handles mouse interactions, smooth CSS transition toggles, disabled states,
/// and reactive feedback badges.
/// </summary>
public class CommandCrossPresenter
{
    public event Action<BattleCommandType> OnCommandConfirmed;

    private readonly VisualElement _rootContainer;
    private readonly VisualElement _slotAttack;
    private readonly VisualElement _slotPsynergy;
    private readonly VisualElement _slotItem;
    private readonly VisualElement _slotDefend;
    private readonly VisualElement _slotDjinn;
    private readonly VisualElement _slotSummon;
    private readonly Label _labelActiveCommand;

    private const string HIDDEN_ROOT_CLASS = "command-cross-root--hidden";
    private const string HIDDEN_BADGE_CLASS = "cmd-selected-badge--hidden";
    private const string DISABLED_SLOT_CLASS = "cmd-slot--disabled";

    public CommandCrossPresenter(VisualElement rootOverlayElement)
    {
        _rootContainer = rootOverlayElement ?? throw new ArgumentNullException(nameof(rootOverlayElement));

        _slotAttack = _rootContainer.Q<VisualElement>("slot_attack");
        _slotPsynergy = _rootContainer.Q<VisualElement>("slot_psynergy");
        _slotItem = _rootContainer.Q<VisualElement>("slot_item");
        _slotDefend = _rootContainer.Q<VisualElement>("slot_defend");
        _slotDjinn = _rootContainer.Q<VisualElement>("slot_djinn");
        _slotSummon = _rootContainer.Q<VisualElement>("slot_summon");
        _labelActiveCommand = _rootContainer.Q<Label>("label_active_command");

        RegisterSlotEvents(_slotAttack, BattleCommandType.Attack, "ATTACK");
        RegisterSlotEvents(_slotPsynergy, BattleCommandType.Psynergy, "PSYNERGY");
        RegisterSlotEvents(_slotItem, BattleCommandType.Item, "ITEM");
        RegisterSlotEvents(_slotDefend, BattleCommandType.Defend, "DEFEND");
        RegisterSlotEvents(_slotDjinn, BattleCommandType.Djinn, "DJINN");
        RegisterSlotEvents(_slotSummon, BattleCommandType.Summon, "SUMMON");
    }

    /// <summary>
    /// Displays the command cross overlay with a smooth scale/fade-in CSS transition.
    /// Configures special wings visibility (Djinn/Summon) and disabled state for slots (Psynergy/Item).
    /// </summary>
    public void Show(
        bool hasPsynergyAvailable = true,
        bool hasItemsAvailable = true,
        bool hasDjinnsUnlocked = true,
        bool hasSummonsUnlocked = true)
    {
        // Djinns and Summons: Completely hidden if not unlocked
        if (_slotDjinn != null)
            _slotDjinn.style.display = hasDjinnsUnlocked ? DisplayStyle.Flex : DisplayStyle.None;

        if (_slotSummon != null)
            _slotSummon.style.display = hasSummonsUnlocked ? DisplayStyle.Flex : DisplayStyle.None;

        // Psynergy and Items: Visual disabled style if unavailable
        SetSlotEnabledState(_slotPsynergy, hasPsynergyAvailable);
        SetSlotEnabledState(_slotItem, hasItemsAvailable);

        // Ensure container display is active before removing hidden transition class
        _rootContainer.style.display = DisplayStyle.Flex;
        _rootContainer.RemoveFromClassList(HIDDEN_ROOT_CLASS);

        ClearCommandLabel();
    }

    /// <summary>
    /// Hides the command cross overlay applying a smooth scale/fade-out transition.
    /// </summary>
    public void Hide()
    {
        _rootContainer.AddToClassList(HIDDEN_ROOT_CLASS);
        ClearCommandLabel();
    }

    private void SetSlotEnabledState(VisualElement slot, bool isEnabled)
    {
        if (slot == null) return;

        slot.SetEnabled(isEnabled);

        if (isEnabled)
        {
            slot.RemoveFromClassList(DISABLED_SLOT_CLASS);
        }
        else
        {
            slot.AddToClassList(DISABLED_SLOT_CLASS);
        }
    }

    private void RegisterSlotEvents(VisualElement slot, BattleCommandType commandType, string labelText)
    {
        if (slot == null) return;

        slot.RegisterCallback<MouseEnterEvent>(evt =>
        {
            if (slot.enabledSelf) SetCommandLabelText(labelText);
        });

        slot.RegisterCallback<MouseLeaveEvent>(evt => ClearCommandLabel());

        slot.RegisterCallback<ClickEvent>(evt =>
        {
            if (!slot.enabledSelf) return;

            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Confirm);
            OnCommandConfirmed?.Invoke(commandType);
        });
    }

    private void SetCommandLabelText(string text)
    {
        if (_labelActiveCommand == null) return;

        _labelActiveCommand.text = text;
        _labelActiveCommand.RemoveFromClassList(HIDDEN_BADGE_CLASS);
    }

    private void ClearCommandLabel()
    {
        if (_labelActiveCommand == null) return;

        _labelActiveCommand.text = string.Empty;
        _labelActiveCommand.AddToClassList(HIDDEN_BADGE_CLASS);
    }
}