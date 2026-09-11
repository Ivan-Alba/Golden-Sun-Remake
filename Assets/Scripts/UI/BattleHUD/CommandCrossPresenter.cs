using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.BattleHUD.CommandCross
{
    /// <summary>
    /// Lightweight presenter for the battle command cross UI Toolkit overlay.
    /// Handles mouse interactions, smooth CSS transition toggles, and reactive feedback badges.
    /// </summary>
    public class CommandCrossPresenter
    {
        public event Action<BattleCommandType> OnCommandConfirmed;

        private readonly VisualElement _rootContainer;
        private readonly VisualElement _slotDjinn;
        private readonly VisualElement _slotSummon;
        private readonly Label _labelActiveCommand;

        private const string HIDDEN_ROOT_CLASS = "command-cross-root--hidden";
        private const string HIDDEN_BADGE_CLASS = "cmd-selected-badge--hidden";

        public CommandCrossPresenter(VisualElement rootOverlayElement)
        {
            _rootContainer = rootOverlayElement ?? throw new ArgumentNullException(nameof(rootOverlayElement));

            _slotDjinn = _rootContainer.Q<VisualElement>("slot_djinn");
            _slotSummon = _rootContainer.Q<VisualElement>("slot_summon");
            _labelActiveCommand = _rootContainer.Q<Label>("label_active_command");

            RegisterSlotEvents("slot_attack", BattleCommandType.Attack, "ATTACK");
            RegisterSlotEvents("slot_psynergy", BattleCommandType.Psynergy, "PSYNERGY");
            RegisterSlotEvents("slot_item", BattleCommandType.Item, "ITEM");
            RegisterSlotEvents("slot_defend", BattleCommandType.Defend, "DEFEND");
            RegisterSlotEvents("slot_djinn", BattleCommandType.Djinn, "DJINN");
            RegisterSlotEvents("slot_summon", BattleCommandType.Summon, "SUMMON");
        }

        /// <summary>
        /// Displays the command cross overlay with a smooth scale/fade-in CSS transition
        /// and configures special wing availability (Djinn/Summon).
        /// </summary>
        public void Show(bool hasDjinnsUnlocked = true, bool hasSummonsUnlocked = true)
        {
            if (_slotDjinn != null)
                _slotDjinn.style.display = hasDjinnsUnlocked ? DisplayStyle.Flex : DisplayStyle.None;

            if (_slotSummon != null)
                _slotSummon.style.display = hasSummonsUnlocked ? DisplayStyle.Flex : DisplayStyle.None;

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

        private void RegisterSlotEvents(string slotName, BattleCommandType commandType, string labelText)
        {
            VisualElement slot = _rootContainer.Q<VisualElement>(slotName);
            if (slot == null) return;

            slot.RegisterCallback<MouseEnterEvent>(evt => SetCommandLabelText(labelText));
            slot.RegisterCallback<MouseLeaveEvent>(evt => ClearCommandLabel());

            slot.RegisterCallback<ClickEvent>(evt =>
            {
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
}