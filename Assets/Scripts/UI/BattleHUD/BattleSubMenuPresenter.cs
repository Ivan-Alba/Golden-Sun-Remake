using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Presenter responsible for populating and managing the floating Battle SubMenu
/// (Psynergies, Items, Djinns, and Summons), including sound effects and local input navigation.
/// </summary>
public class BattleSubMenuPresenter
{
    #region UI Elements References

    private readonly VisualElement _rootElement;
    private readonly Label _labelSubMenuTitle;
    private readonly ScrollView _scrollView;
    private readonly Label _labelInfoDescription;

    // Injected Visual Templates
    private readonly VisualTreeAsset _psynergyCardAsset;
    private readonly VisualTreeAsset _itemCardAsset;
    private readonly VisualTreeAsset _djinnCardAsset;

    // Last focused card reference & data to handle confirm input safely
    private VisualElement _lastFocusedCard;
    private object _lastFocusedPayload;
    private bool _lastFocusedCanAfford = true;

    // Track active Command and Battle State
    private BattleCommandType _currentCommandType = BattleCommandType.Psynergy;
    private BattleState _currentBattleState = BattleState.None;

    #endregion

    #region Events

    /// <summary>
    /// Triggered when an action (Psynergy/Item/Djinn/Summon) is chosen from the SubMenu
    /// to proceed to target selection.
    /// </summary>
    public event Action<BattleCommandType, object> OnActionSelected;

    /// <summary>
    /// Triggered when the user cancels/backs out of the SubMenu.
    /// </summary>
    public event Action OnSubMenuCancelled;

    #endregion

    public BattleSubMenuPresenter(
        VisualElement rootElement,
        VisualTreeAsset psynergyCardAsset,
        VisualTreeAsset itemCardAsset,
        VisualTreeAsset djinnCardAsset)
    {
        _rootElement = rootElement ?? throw new ArgumentNullException(nameof(rootElement));
        _psynergyCardAsset = psynergyCardAsset;
        _itemCardAsset = itemCardAsset;
        _djinnCardAsset = djinnCardAsset;

        _labelSubMenuTitle = _rootElement.Q<Label>("label_submenu_title");
        _scrollView = _rootElement.Q<ScrollView>("submenu_scroll_view");
        _labelInfoDescription = _rootElement.Q<Label>("label_info_description");

        // Disable Focus and Interaction on ScrollView and internal Scrollers
        if (_scrollView != null)
        {
            _scrollView.focusable = false;
            _scrollView.pickingMode = PickingMode.Ignore;

            if (_scrollView.verticalScroller != null)
            {
                _scrollView.verticalScroller.focusable = false;
                _scrollView.verticalScroller.pickingMode = PickingMode.Ignore;

                if (_scrollView.verticalScroller.slider != null)
                {
                    _scrollView.verticalScroller.slider.focusable = false;
                    _scrollView.verticalScroller.slider.pickingMode = PickingMode.Ignore;
                }
            }
        }

        // Subscribe to global GameEvents
        SubscribeToEvents();
    }

    #region Event Subscriptions & Input Dispatching

    public void SubscribeToEvents()
    {
        UnsubscribeFromEvents();

        GameEvents.Input.OnBattleConfirmPressed += HandleConfirmInput;
        GameEvents.Input.OnBattleCancelPressed += HandleCancelInput;
    }

    public void UnsubscribeFromEvents()
    {
        GameEvents.Input.OnBattleConfirmPressed -= HandleConfirmInput;
        GameEvents.Input.OnBattleCancelPressed -= HandleCancelInput;
    }

    /// <summary>
    /// Hooked directly to BattleManager.OnBattleStateChanged via BattleHUDController subscription.
    /// </summary>
    public void HandleBattleStateChanged(BattleState newState)
    {
        _currentBattleState = newState;

        if (newState != BattleState.SubMenuSelection)
        {
            ClearFocusAndHide();
        }
    }

    /// <summary>
    /// Handles confirm input (Space, Gamepad A, etc.) when the SubMenu is active.
    /// </summary>
    private void HandleConfirmInput(InputEventArgs args)
    {
        if (args.Handled || _currentBattleState != BattleState.SubMenuSelection) return;
        if (_lastFocusedPayload == null || !_lastFocusedCanAfford) return;

        // Consume the input event to prevent bleeding into target selection
        args.Use();

        ConfirmCurrentSelection();
    }

    /// <summary>
    /// Handles cancel input (Escape, Gamepad B, etc.) when the SubMenu is active.
    /// </summary>
    private void HandleCancelInput(InputEventArgs args)
    {
        if (args.Handled || _currentBattleState != BattleState.SubMenuSelection) return;

        // Consume the input event to prevent bleeding into upper controllers
        args.Use();

        GameEvents.Audio.RequestPlaySFX(SFXType.UI_Cancel);
        OnSubMenuCancelled?.Invoke();
    }

    private void ConfirmCurrentSelection()
    {
        if (_lastFocusedPayload != null && _lastFocusedCanAfford)
        {
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Confirm);
            OnActionSelected?.Invoke(_currentCommandType, _lastFocusedPayload);
        }
    }

    #endregion

    #region Visibility & Focus Management

    public void ClearFocusAndHide()
    {
        _lastFocusedCard = null;
        _lastFocusedPayload = null;
        _lastFocusedCanAfford = false;
        _rootElement.focusController?.focusedElement?.Blur();
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        if (!visible && _rootElement != null)
        {
            _lastFocusedCard = null;
            _lastFocusedPayload = null;
            _lastFocusedCanAfford = false;
            _rootElement.focusController?.focusedElement?.Blur();
        }

        _rootElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    #endregion

    #region Populate Psynergies

    public void ShowPsynergies(IReadOnlyList<PsynergySO> psynergies, int currentPP)
    {
        _currentCommandType = BattleCommandType.Psynergy;
        SetVisible(true);
        if (_labelSubMenuTitle != null) _labelSubMenuTitle.text = "PSYNERGY";
        _scrollView.Clear();
        _lastFocusedCard = null;
        _lastFocusedPayload = null;

        if (psynergies == null || psynergies.Count == 0)
        {
            UpdateInfoDescription("No hay Psinergías disponibles.");
            return;
        }

        UpdateInfoDescription("Selecciona una Psinergía...");
        List<VisualElement> cardsList = new List<VisualElement>();

        foreach (PsynergySO psy in psynergies)
        {
            if (psy == null || !psy.IsBattlePsynergy) continue;

            TemplateContainer instance = _psynergyCardAsset != null
                ? _psynergyCardAsset.Instantiate()
                : new TemplateContainer();

            VisualElement cardRoot = instance.Q<VisualElement>("psynergy_card") ?? instance;

            cardRoot.focusable = true;
            cardRoot.pickingMode = PickingMode.Ignore;
            cardRoot.AddToClassList("interactive");

            cardRoot.AddToClassList(GetElementCssClass(psy.Element));
            VisualElement elementPipIcon = cardRoot.Q<VisualElement>("element_pip_icon");
            if (elementPipIcon != null)
            {
                elementPipIcon.AddToClassList(GetElementPipCssClass(psy.Element));
            }

            Label labelName = cardRoot.Q<Label>("label_card_name");
            Label labelPP = cardRoot.Q<Label>("label_card_pp");
            VisualElement iconElement = cardRoot.Q<VisualElement>("psynergy_card_icon");
            VisualElement badgesRow = cardRoot.Q<VisualElement>("card_badges_row");

            if (labelName != null) labelName.text = psy.PsynergyName;
            if (labelPP != null) labelPP.text = $"{psy.PpCost} PP";
            if (badgesRow != null) badgesRow.style.display = DisplayStyle.None;

            if (iconElement != null && psy.Icon != null)
            {
                iconElement.style.backgroundImage = Background.FromSprite(psy.Icon);
            }

            bool canAfford = currentPP >= psy.PpCost;
            cardRoot.SetEnabled(canAfford);

            cardRoot.RegisterCallback<FocusEvent>(evt =>
            {
                _lastFocusedCard = cardRoot;
                _lastFocusedPayload = psy;
                _lastFocusedCanAfford = canAfford;

                UpdateInfoDescription(psy.Description);
                cardRoot.schedule.Execute(() => _scrollView.ScrollTo(cardRoot));
            });

            cardRoot.RegisterCallback<FocusOutEvent>(evt => RestoreFocusIfLost());
            cardRoot.RegisterCallback<MouseEnterEvent>(evt => UpdateInfoDescription(psy.Description));
            cardRoot.RegisterCallback<ClickEvent>(evt =>
            {
                if (canAfford) ConfirmCurrentSelection();
            });
            cardRoot.RegisterCallback<NavigationSubmitEvent>(evt =>
            {
                if (canAfford) ConfirmCurrentSelection();
            });

            _scrollView.Add(instance);

            if (canAfford)
            {
                cardsList.Add(cardRoot);
            }
        }

        SetupExplicitVerticalNavigation(cardsList);
        FocusFirstElement(cardsList.Count > 0 ? cardsList[0] : null);
    }

    private string GetElementPipCssClass(ElementalType element)
    {
        return element switch
        {
            ElementalType.Venus => "icon-venus",
            ElementalType.Mars => "icon-mars",
            ElementalType.Jupiter => "icon-jupiter",
            ElementalType.Mercury => "icon-mercury",
            _ => "icon-none"
        };
    }

    private string GetElementCssClass(ElementalType element)
    {
        return element switch
        {
            ElementalType.Venus => "element-venus",
            ElementalType.Mars => "element-mars",
            ElementalType.Jupiter => "element-jupiter",
            ElementalType.Mercury => "element-mercury",
            _ => "element-venus"
        };
    }

    #endregion

    #region Populate Items

    public void ShowItems(IReadOnlyList<InventorySlot> slots)
    {
        _currentCommandType = BattleCommandType.Item;
        SetVisible(true);
        if (_labelSubMenuTitle != null) _labelSubMenuTitle.text = "ITEMS";
        _scrollView.Clear();
        _lastFocusedCard = null;
        _lastFocusedPayload = null;

        if (slots == null || slots.Count == 0)
        {
            UpdateInfoDescription("No hay objetos en el inventario.");
            return;
        }

        UpdateInfoDescription("Selecciona un objeto...");
        List<VisualElement> cardsList = new List<VisualElement>();

        foreach (InventorySlot slot in slots)
        {
            if (slot == null || slot.Item == null || slot.Quantity <= 0) continue;

            ItemSO item = slot.Item;
            bool isBattleUsable = item.ItemType == ItemType.Consumable;
            if (!isBattleUsable) continue;

            TemplateContainer instance = _itemCardAsset != null
                ? _itemCardAsset.Instantiate()
                : new TemplateContainer();

            VisualElement cardRoot = instance.Q<VisualElement>("item_card") ?? instance;

            cardRoot.focusable = true;
            cardRoot.pickingMode = PickingMode.Ignore;
            cardRoot.AddToClassList("interactive");

            VisualElement stripe = cardRoot.Q<VisualElement>("card_item_stripe");
            Label labelName = cardRoot.Q<Label>("label_item_name");
            Label labelSubtype = cardRoot.Q<Label>("label_item_subtype");
            Label labelEffect = cardRoot.Q<Label>("label_item_effect");

            Label labelQuantity = cardRoot.Q<Label>("label_quantity");
            VisualElement itemIcon = cardRoot.Q<VisualElement>("item_icon");
            VisualElement quantityBox = cardRoot.Q<VisualElement>("quantity_box");

            if (labelName != null) labelName.text = item.ItemName;
            if (labelSubtype != null) labelSubtype.text = item.ItemType.ToString();
            if (labelEffect != null) labelEffect.text = item.ItemDescription;

            if (labelQuantity != null) labelQuantity.text = slot.Quantity.ToString();
            if (quantityBox != null) quantityBox.style.display = DisplayStyle.Flex;
            if (itemIcon != null && item.Icon != null)
            {
                itemIcon.style.backgroundImage = Background.FromSprite(item.Icon);
            }

            if (stripe != null)
            {
                stripe.RemoveFromClassList("stripe-consumable");
                stripe.RemoveFromClassList("stripe-equipment");
                stripe.AddToClassList(item.ItemType == ItemType.Consumable ? "stripe-consumable" : "stripe-equipment");
            }

            cardRoot.RegisterCallback<FocusEvent>(evt =>
            {
                _lastFocusedCard = cardRoot;
                _lastFocusedPayload = item;
                _lastFocusedCanAfford = true;

                UpdateInfoDescription(item.ItemDescription);
                cardRoot.schedule.Execute(() => _scrollView.ScrollTo(cardRoot));
            });

            cardRoot.RegisterCallback<FocusOutEvent>(evt => RestoreFocusIfLost());
            cardRoot.RegisterCallback<MouseEnterEvent>(evt => UpdateInfoDescription(item.ItemDescription));
            cardRoot.RegisterCallback<ClickEvent>(evt => ConfirmCurrentSelection());
            cardRoot.RegisterCallback<NavigationSubmitEvent>(evt => ConfirmCurrentSelection());

            _scrollView.Add(instance);
            cardsList.Add(cardRoot);
        }

        SetupExplicitVerticalNavigation(cardsList);
        FocusFirstElement(cardsList.Count > 0 ? cardsList[0] : null);
    }

    #endregion

    #region Populate Djinns

    public void ShowDjinns(IReadOnlyList<DjinnInstance> djinns)
    {
        _currentCommandType = BattleCommandType.Djinn;
        SetVisible(true);
        if (_labelSubMenuTitle != null) _labelSubMenuTitle.text = "DJINNS";
        _scrollView.Clear();
        _lastFocusedCard = null;
        _lastFocusedPayload = null;

        if (djinns == null || djinns.Count == 0)
        {
            UpdateInfoDescription("No hay Djinns asignados.");
            return;
        }

        UpdateInfoDescription("Selecciona un Djinn...");
        List<VisualElement> cardsList = new List<VisualElement>();

        foreach (DjinnInstance djinn in djinns)
        {
            if (djinn == null || djinn.DjinnData == null) continue;

            DjinnSO data = djinn.DjinnData;

            TemplateContainer instance = _djinnCardAsset != null
                ? _djinnCardAsset.Instantiate()
                : new TemplateContainer();

            VisualElement cardRoot = instance.Q<VisualElement>("djinn_card") ?? instance;

            cardRoot.focusable = true;
            cardRoot.pickingMode = PickingMode.Ignore;
            cardRoot.AddToClassList("interactive");

            Label labelName = cardRoot.Q<Label>("label_djinn_name");
            Label labelDescription = cardRoot.Q<Label>("label_unleash_description");
            Label labelState = cardRoot.Q<Label>("label_djinn_state");
            VisualElement iconElement = cardRoot.Q<VisualElement>("djinn_icon");

            if (labelName != null) labelName.text = data.DjinnName;
            if (labelDescription != null && data.UnleashEffect != null)
            {
                labelDescription.text = data.UnleashEffect.Description;
            }

            if (labelState != null)
            {
                labelState.text = djinn.CurrentState.ToString().ToUpper();
            }

            if (iconElement != null && data.Icon != null)
            {
                iconElement.style.backgroundImage = Background.FromSprite(data.Icon);
            }

            string infoText = data.UnleashEffect != null ? data.UnleashEffect.Description : data.DjinnName;
            cardRoot.RegisterCallback<FocusEvent>(evt =>
            {
                _lastFocusedCard = cardRoot;
                _lastFocusedPayload = djinn;
                _lastFocusedCanAfford = true;

                UpdateInfoDescription(infoText);
                cardRoot.schedule.Execute(() => _scrollView.ScrollTo(cardRoot));
            });

            cardRoot.RegisterCallback<FocusOutEvent>(evt => RestoreFocusIfLost());
            cardRoot.RegisterCallback<MouseEnterEvent>(evt => UpdateInfoDescription(infoText));
            cardRoot.RegisterCallback<ClickEvent>(evt => ConfirmCurrentSelection());
            cardRoot.RegisterCallback<NavigationSubmitEvent>(evt => ConfirmCurrentSelection());

            _scrollView.Add(instance);
            cardsList.Add(cardRoot);
        }

        SetupExplicitVerticalNavigation(cardsList);
        FocusFirstElement(cardsList.Count > 0 ? cardsList[0] : null);
    }

    #endregion

    #region Helpers & Explicit Navigation Logic

    private void RestoreFocusIfLost()
    {
        _rootElement.schedule.Execute(() =>
        {
            if (_rootElement.style.display == DisplayStyle.Flex &&
                _rootElement.focusController?.focusedElement == null &&
                _lastFocusedCard != null)
            {
                _lastFocusedCard.Focus();
            }
        });
    }

    private void SetupExplicitVerticalNavigation(IReadOnlyList<VisualElement> cards)
    {
        if (cards == null || cards.Count <= 1) return;

        int count = cards.Count;

        for (int i = 0; i < count; i++)
        {
            int prevIndex = (i - 1 + count) % count;
            int nextIndex = (i + 1) % count;

            VisualElement current = cards[i];
            VisualElement prevTarget = cards[prevIndex];
            VisualElement nextTarget = cards[nextIndex];

            current.RegisterCallback<NavigationMoveEvent>(evt =>
            {
                if (evt.direction == NavigationMoveEvent.Direction.Up)
                {
                    evt.StopPropagation();
                    GameEvents.Audio.RequestPlaySFX(SFXType.UI_Move);
                    prevTarget.Focus();
                }
                else if (evt.direction == NavigationMoveEvent.Direction.Down)
                {
                    evt.StopPropagation();
                    GameEvents.Audio.RequestPlaySFX(SFXType.UI_Move);
                    nextTarget.Focus();
                }
            });
        }
    }

    private void FocusFirstElement(VisualElement targetElement)
    {
        if (targetElement == null) return;

        targetElement.schedule.Execute(() =>
        {
            targetElement.Focus();
        });
    }

    private void UpdateInfoDescription(string description)
    {
        if (_labelInfoDescription != null)
        {
            _labelInfoDescription.text = string.IsNullOrEmpty(description)
                ? "Selecciona una opción..."
                : description;
        }
    }

    #endregion
}