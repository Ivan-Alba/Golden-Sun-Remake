using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public enum DjinnViewMode
{
    Individual, // Vista A
    Global      // Vista B
}

public enum PsynergyCardState
{
    Normal,     // Learned / Active
    Gained,     // Projection (+ GANADA)
    Lost,       // Projection (- PERDIDA)
    Locked      // Placeholder locked with level requirement
}

public class PageDjinnController
{
    #region Events

    /// <summary>
    /// Event triggered whenever a Djinn's state changes or an exchange is completed.
    /// Used by MainMenuController to refresh top header hero cards, stats, or class titles.
    /// </summary>
    public event Action OnDjinnStateChanged;

    /// <summary>
    /// Event triggered when switching between Vista A and Vista B to toggle the top heroes bar visibility in MainMenuController.
    /// </summary>
    public event Action<bool> OnToggleTopHeroesBar;

    /// <summary>
    /// Event triggered when user clicks to toggle between Vista A and Vista B.
    /// Delegated to MainMenuController to switch visual containers visibility.
    /// </summary>
    public event Action<DjinnViewMode> OnRequestViewChange;

    #endregion

    #region Private Fields - Containers & Views

    private VisualElement rootViewA;
    private VisualElement rootViewB;

    // View Containers
    private VisualElement pageDjinnContainer;
    private VisualElement sectionVistaB;
    private VisualElement modalOverlayCompare;

    // Vista B Elements
    private VisualElement partyColumnsContainer;
    private Button btnSwitchToVistaA;

    // Current View State
    private DjinnViewMode currentViewMode = DjinnViewMode.Individual;

    #endregion

    #region Private Fields - Left Panel Elements (Hero Summary & Stats)

    private VisualElement heroAvatarIcon;
    private Label labelHeroName;
    private Label labelHeroLevel;
    private Label labelHeroClassCurrent;
    private Label labelHeroClassNext;
    private Label labelClassArrow;

    private Label labelPipVenus;
    private Label labelPipMars;
    private Label labelPipJupiter;
    private Label labelPipMercury;

    private Label labelStatMaxHP;
    private Label labelStatMaxPP;
    private Label labelStatAtq;
    private Label labelStatDef;
    private Label labelStatAgi;
    private Label labelStatSue;

    private Label labelDeltaAtq;
    private Label labelDeltaDef;
    private Label labelDeltaAgi;
    private Label labelDeltaSue;

    private ScrollView scrollClassPsynergies;

    #endregion

    #region Private Fields - Center Panel Elements (Djinn Grid)

    private Label labelGridHeader;
    private VisualElement djinnSlotsGrid;
    private VisualElement djinn3dRenderTarget;
    private Button btnSwitchToVistaB;

    #endregion

    #region Private Fields - Right Panel Elements (Djinn Info)

    private VisualElement djinnVideoPreview;
    private Label labelDjinnName;
    private Label labelDjinnType;
    private Label labelDjinnStatus;
    private Label labelDjinnRecovery;
    private Label labelDjinnDesc;

    // Set Bonus Labels
    private Label labelSetBonusMaxHP;
    private Label labelSetBonusMaxPP;
    private Label labelSetBonusAtq;
    private Label labelSetBonusDef;
    private Label labelSetBonusAgi;
    private Label labelSetBonusSue;

    // Unleash Labels
    private VisualElement boxUnleashInfo;
    private Label labelUnleashName;
    private Label labelUnleashDesc;
    private Label labelUnleashDamage;
    private Label labelUnleashEffect;

    // Unleash Status Effect Target icons
    private Sprite iconTargetSingle;
    private Sprite iconTargetGroup;

    // Leader Passive Labels
    private Label labelLeaderPassiveTitle;
    private Label labelLeaderPassiveDesc;

    #endregion

    #region Private Fields - Data & Selection State

    private List<HeroStats> partyHeroes = new List<HeroStats>();
    private HeroStats currentHero;
    private HeroDjinnController currentHeroDjinnController;

    // Templates UXML
    private VisualTreeAsset psynergyCardAsset;
    private VisualTreeAsset djinnSlotCardAAsset;
    private VisualTreeAsset djinnSlotCardBAsset;

    // Djinn Selected for Detail Display in Vista A
    private DjinnInstance selectedDjinnForDetails;

    // Djinn Swap Context (for Vista B & Modal)
    private DjinnInstance swapSourceDjinn;
    private HeroStats swapSourceHero;
    private DjinnInstance swapTargetDjinn;
    private HeroStats swapTargetHero;

    #endregion

    #region Constructor

    public PageDjinnController(VisualElement viewA, VisualElement viewB)
    {
        rootViewA = viewA;
        rootViewB = viewB;

        BindUIElements();
        BindEvents();
    }

    #endregion

    #region UI Binding

    private void BindUIElements()
    {
        // 1. Root Containers & Vista A Binding
        if (rootViewA != null)
        {
            pageDjinnContainer = rootViewA.Q<VisualElement>("page_djinn_container") ?? rootViewA;
            modalOverlayCompare = rootViewA.Q<VisualElement>("compare_modal_overlay");

            // Left Panel
            heroAvatarIcon = rootViewA.Q<VisualElement>("hero_avatar_icon");
            labelHeroName = rootViewA.Q<Label>("label_hero_name");
            labelHeroLevel = rootViewA.Q<Label>("label_hero_level");
            labelHeroClassCurrent = rootViewA.Q<Label>("label_hero_class_current");
            labelHeroClassNext = rootViewA.Q<Label>("label_hero_class_next");
            labelClassArrow = rootViewA.Q<Label>("label_class_arrow");

            labelPipVenus = rootViewA.Q<Label>("label_pip_venus");
            labelPipMars = rootViewA.Q<Label>("label_pip_mars");
            labelPipJupiter = rootViewA.Q<Label>("label_pip_jupiter");
            labelPipMercury = rootViewA.Q<Label>("label_pip_mercury");

            labelStatMaxHP = rootViewA.Q<Label>("label_stat_max_hp");
            labelStatMaxPP = rootViewA.Q<Label>("label_stat_max_pp");
            labelStatAtq = rootViewA.Q<Label>("label_stat_atq");
            labelStatDef = rootViewA.Q<Label>("label_stat_def");
            labelStatAgi = rootViewA.Q<Label>("label_stat_agi");
            labelStatSue = rootViewA.Q<Label>("label_stat_sue");

            labelDeltaAtq = rootViewA.Q<Label>("label_delta_atq");
            labelDeltaDef = rootViewA.Q<Label>("label_delta_def");
            labelDeltaAgi = rootViewA.Q<Label>("label_delta_agi");
            labelDeltaSue = rootViewA.Q<Label>("label_delta_sue");

            scrollClassPsynergies = rootViewA.Q<ScrollView>("scroll_class_psynergies");

            // Center Panel
            labelGridHeader = rootViewA.Q<Label>("label_grid_header");
            djinnSlotsGrid = rootViewA.Q<VisualElement>("djinn_slots_grid");
            djinn3dRenderTarget = rootViewA.Q<VisualElement>("djinn_3d_render_target");
            btnSwitchToVistaB = rootViewA.Q<Button>("btn_switch_vista_b");

            // Right Panel
            djinnVideoPreview = rootViewA.Q<VisualElement>("djinn_video_preview");
            labelDjinnName = rootViewA.Q<Label>("label_djinn_name");
            labelDjinnType = rootViewA.Q<Label>("label_djinn_type");
            labelDjinnStatus = rootViewA.Q<Label>("label_djinn_status");
            labelDjinnRecovery = rootViewA.Q<Label>("label_djinn_recovery");
            labelDjinnDesc = rootViewA.Q<Label>("label_djinn_desc");

            labelSetBonusMaxHP = rootViewA.Q<Label>("label_set_bonus_max_hp");
            labelSetBonusMaxPP = rootViewA.Q<Label>("label_set_bonus_max_pp");
            labelSetBonusAtq = rootViewA.Q<Label>("label_set_bonus_atq");
            labelSetBonusDef = rootViewA.Q<Label>("label_set_bonus_def");
            labelSetBonusAgi = rootViewA.Q<Label>("label_set_bonus_agi");
            labelSetBonusSue = rootViewA.Q<Label>("label_set_bonus_sue");

            boxUnleashInfo = rootViewA.Q<VisualElement>("box_unleash_info");
            labelUnleashName = rootViewA.Q<Label>("label_unleash_name");
            labelUnleashDesc = rootViewA.Q<Label>("label_unleash_desc");
            labelUnleashDamage = rootViewA.Q<Label>("label_unleash_damage");
            labelUnleashEffect = rootViewA.Q<Label>("label_unleash_effect");

            labelLeaderPassiveTitle = rootViewA.Q<Label>("label_leader_passive_title");
            labelLeaderPassiveDesc = rootViewA.Q<Label>("label_leader_passive_desc");

            if (modalOverlayCompare != null)
            {
                modalOverlayCompare.style.display = DisplayStyle.None;
            }
        }

        // 2. Vista B Binding
        if (rootViewB != null)
        {
            sectionVistaB = rootViewB.Q<VisualElement>("page_djinn_global_container")
                         ?? rootViewB.Q<VisualElement>("djinn_global_section")
                         ?? rootViewB;

            partyColumnsContainer = rootViewB.Q<VisualElement>("party_columns_container");
            btnSwitchToVistaA = rootViewB.Q<Button>("btn_switch_vista_a");
        }
    }

    private void BindEvents()
    {
        if (btnSwitchToVistaB != null)
        {
            btnSwitchToVistaB.clicked += () => SwitchToView(DjinnViewMode.Global);
        }

        if (btnSwitchToVistaA != null)
        {
            btnSwitchToVistaA.clicked += () => SwitchToView(DjinnViewMode.Individual);
        }
    }

    #endregion

    #region Data Initialization & Layout Setup

    public void SetParty(List<HeroStats> party)
    {
        partyHeroes = party ?? new List<HeroStats>();
    }

    /// <summary>
    /// Assigns the UXML cards templates used for dynamic instantiation.
    /// </summary>
    public void SetTemplates(VisualTreeAsset miniPsynergyCard, VisualTreeAsset djinnSlotCardA, VisualTreeAsset djinnSlotCardB)
    {
        psynergyCardAsset = miniPsynergyCard;
        djinnSlotCardAAsset = djinnSlotCardA;
        djinnSlotCardBAsset = djinnSlotCardB;
    }

    /// <summary>
    /// Assigns the target icons used for dynamic unleash badge instantiation.
    /// </summary>
    public void SetTargetIcons(Sprite singleTarget, Sprite groupTarget)
    {
        iconTargetSingle = singleTarget;
        iconTargetGroup = groupTarget;
    }

    public void PopulateData(HeroStats hero)
    {
        if (currentHeroDjinnController != null)
        {
            currentHeroDjinnController.OnDjinnSetupUpdated -= RefreshAllData;
        }

        currentHero = hero;
        selectedDjinnForDetails = null;
        currentHeroDjinnController = (hero != null) ? hero.GetComponent<HeroDjinnController>() : null;

        if (currentHeroDjinnController != null)
        {
            currentHeroDjinnController.OnDjinnSetupUpdated += RefreshAllData;
        }

        // English comment: Ensure view mode is reset to default when populating data
        currentViewMode = DjinnViewMode.Individual;
        RefreshAllData();
    }

    public void RefreshAllData()
    {
        if (currentHero == null) return;

        if (currentViewMode == DjinnViewMode.Individual)
        {
            RefreshVistaA();
        }
        else
        {
            RefreshVistaB();
        }

        OnDjinnStateChanged?.Invoke();
    }

    #endregion

    #region Vista A - Logic & Rendering

    private void RefreshVistaA()
    {
        if (currentHero == null || currentHeroDjinnController == null) return;

        // 1. Update Hero Class
        if (heroAvatarIcon != null) heroAvatarIcon.style.backgroundImage = new StyleBackground(currentHero.TurnBarImage);
        if (labelHeroName != null) labelHeroName.text = currentHero.Name;
        if (labelHeroLevel != null) labelHeroLevel.text = $"Nv. {currentHero.Level}";

        CharacterClassSO currentClass = currentHeroDjinnController.CurrentClass;
        if (labelHeroClassCurrent != null)
        {
            labelHeroClassCurrent.text = (currentClass != null) ? currentClass.ClassName : "Sin Clase";
        }

        if (labelHeroClassNext != null) labelHeroClassNext.style.display = DisplayStyle.None;
        if (labelClassArrow != null) labelClassArrow.style.display = DisplayStyle.None;

        // 2. Update Elemental Pips
        UpdateElementalPips(currentHeroDjinnController, labelPipVenus, labelPipMars, labelPipJupiter, labelPipMercury);

        // 3. Update Base Stats
        if (labelStatAtq != null) labelStatAtq.text = Mathf.RoundToInt(currentHero.Attack).ToString();
        if (labelStatDef != null) labelStatDef.text = Mathf.RoundToInt(currentHero.Defense).ToString();
        if (labelStatAgi != null) labelStatAgi.text = Mathf.RoundToInt(currentHero.Agility).ToString();
        if (labelStatSue != null) labelStatSue.text = Mathf.RoundToInt(currentHero.Luck).ToString();
        if (labelStatMaxHP != null) labelStatMaxHP.text = Mathf.RoundToInt(currentHero.MaxHp).ToString();
        if (labelStatMaxPP != null) labelStatMaxPP.text = Mathf.RoundToInt(currentHero.MaxPp).ToString();

        HideStatDeltas();

        // 4. Update Grid Title and Slots
        IReadOnlyList<DjinnInstance> assignedDjinns = currentHeroDjinnController.AssignedDjinns;
        int count = (assignedDjinns != null) ? assignedDjinns.Count : 0;

        if (labelGridHeader != null)
        {
            labelGridHeader.text = $"DJINNS EQUIPADOS ({count}/8)";
        }

        if (assignedDjinns != null && assignedDjinns.Count > 0)
        {
            if (selectedDjinnForDetails == null || !assignedDjinns.Contains(selectedDjinnForDetails))
            {
                selectedDjinnForDetails = assignedDjinns[0];
            }
        }
        else
        {
            selectedDjinnForDetails = null;
        }

        RenderVistaASlots(assignedDjinns);

        // 5. Render details of selected Djinn
        if (selectedDjinnForDetails != null)
        {
            SelectDjinnInVistaA(selectedDjinnForDetails);
        }
        else
        {
            ClearRightDetailPanel();
        }

        // 6. Refresh Psynergies List
        RefreshClassPsynergies();

        UpdateSanctuaryUI();
    }

    private void UpdateElementalPips(HeroDjinnController djinnCtrl, Label pVenus, Label pMars, Label pJupiter, Label pMercury)
    {
        if (djinnCtrl == null) return;

        int vCount = 0, mCount = 0, jCount = 0, meCount = 0;
        int totalVCount = 0, totalMCount = 0, totalJCount = 0, totalMeCount = 0;

        foreach (var djinn in djinnCtrl.AssignedDjinns)
        {
            if (djinn != null && djinn.CurrentState == DjinnState.Set && djinn.DjinnData != null)
            {
                switch (djinn.DjinnData.Element)
                {
                    case ElementalType.Venus: vCount++; break;
                    case ElementalType.Mars: mCount++; break;
                    case ElementalType.Jupiter: jCount++; break;
                    case ElementalType.Mercury: meCount++; break;
                }
            }
            else if (djinn != null && djinn.DjinnData != null)
            {
                switch (djinn.DjinnData.Element)
                {
                    case ElementalType.Venus: totalVCount++; break;
                    case ElementalType.Mars: totalMCount++; break;
                    case ElementalType.Jupiter: totalJCount++; break;
                    case ElementalType.Mercury: totalMeCount++; break;
                }
            }
        }

        if (pVenus != null) pVenus.text = $"{vCount}/{totalVCount + vCount}";
        if (pMars != null) pMars.text = $"{mCount}/{totalMCount + mCount}";
        if (pJupiter != null) pJupiter.text = $"{jCount}/{totalJCount + jCount}";
        if (pMercury != null) pMercury.text = $"{meCount}/{totalMeCount + meCount}";
    }

    private void HideStatDeltas()
    {
        if (labelDeltaAtq != null) labelDeltaAtq.style.display = DisplayStyle.None;
        if (labelDeltaDef != null) labelDeltaDef.style.display = DisplayStyle.None;
        if (labelDeltaAgi != null) labelDeltaAgi.style.display = DisplayStyle.None;
        if (labelDeltaSue != null) labelDeltaSue.style.display = DisplayStyle.None;
    }

    private void RenderVistaASlots(IReadOnlyList<DjinnInstance> djinns)
    {
        if (djinnSlotsGrid == null || djinnSlotCardAAsset == null) return;

        djinnSlotsGrid.Clear();

        for (int i = 0; i < 8; i++)
        {
            TemplateContainer slotInstance = djinnSlotCardAAsset.Instantiate();
            slotInstance.AddToClassList("djinn-slot-card");

            VisualElement cardRoot = slotInstance.Q<VisualElement>("djinn_slot_card_root");
            if (cardRoot == null) continue;

            DjinnInstance djinnInstance = (djinns != null && i < djinns.Count) ? djinns[i] : null;

            VisualElement cardHeader = cardRoot.Q<VisualElement>("djinn_card_header");
            Button btnLeaderTag = cardRoot.Q<Button>("btn_leader_tag");
            VisualElement cardBody = cardRoot.Q<VisualElement>("djinn_card_body");
            VisualElement iconElem = cardRoot.Q<VisualElement>("djinn_card_icon");
            Label nameLabel = cardRoot.Q<Label>("label_djinn_card_name");
            Button statusBtn = cardRoot.Q<Button>("btn_djinn_status");
            Label emptyLabel = cardRoot.Q<Label>("label_djinn_empty");
            VisualElement elementPip = cardRoot.Q<VisualElement>("djinn_element_pip");

            cardRoot.RemoveFromClassList("djinn-card-empty");
            cardRoot.RemoveFromClassList("djinn-card-selected");
            cardRoot.RemoveFromClassList("djinn-card-leader");
            cardRoot.RemoveFromClassList("djinn-card-venus");
            cardRoot.RemoveFromClassList("djinn-card-mars");
            cardRoot.RemoveFromClassList("djinn-card-jupiter");
            cardRoot.RemoveFromClassList("djinn-card-mercury");

            if (djinnInstance != null && djinnInstance.DjinnData != null)
            {
                DjinnSO data = djinnInstance.DjinnData;

                if (cardHeader != null) cardHeader.style.display = DisplayStyle.Flex;
                if (cardBody != null) cardBody.style.display = DisplayStyle.Flex;
                if (emptyLabel != null) emptyLabel.style.display = DisplayStyle.None;

                cardRoot.AddToClassList($"djinn-card-{data.Element.ToString().ToLower()}");

                if (elementPip != null)
                {
                    elementPip.RemoveFromClassList("icon-venus");
                    elementPip.RemoveFromClassList("icon-mars");
                    elementPip.RemoveFromClassList("icon-jupiter");
                    elementPip.RemoveFromClassList("icon-mercury");
                    elementPip.AddToClassList($"icon-{data.Element.ToString().ToLower()}");
                    elementPip.style.display = DisplayStyle.Flex;
                }

                bool isLeader = (currentHeroDjinnController != null && currentHeroDjinnController.LeaderDjinn == djinnInstance);
                if (isLeader)
                {
                    cardRoot.AddToClassList("djinn-card-leader");
                }

                if (selectedDjinnForDetails == djinnInstance)
                {
                    cardRoot.AddToClassList("djinn-card-selected");
                }

                if (btnLeaderTag != null)
                {
                    btnLeaderTag.RemoveFromClassList("leader-active");
                    btnLeaderTag.RemoveFromClassList("leader-disabled");

                    if (isLeader)
                    {
                        btnLeaderTag.AddToClassList("leader-active");
                    }
                    else
                    {
                        btnLeaderTag.AddToClassList("leader-disabled");
                    }

                    btnLeaderTag.RegisterCallback<ClickEvent>(evt =>
                    {
                        evt.StopPropagation();
                        if (currentHeroDjinnController != null)
                        {
                            currentHeroDjinnController.SetLeaderDjinn(djinnInstance);
                            RefreshAllData();
                        }
                    });
                }

                if (iconElem != null && data.Icon != null)
                {
                    iconElem.style.backgroundImage = new StyleBackground(data.Icon);
                }

                if (nameLabel != null) nameLabel.text = data.DjinnName;

                if (statusBtn != null)
                {
                    statusBtn.text = GetStatusButtonText(djinnInstance.CurrentState);

                    statusBtn.RemoveFromClassList("status-set");
                    statusBtn.RemoveFromClassList("status-standby");
                    statusBtn.RemoveFromClassList("status-recovery");

                    switch (djinnInstance.CurrentState)
                    {
                        case DjinnState.Set: statusBtn.AddToClassList("status-set"); break;
                        case DjinnState.Standby: statusBtn.AddToClassList("status-standby"); break;
                        case DjinnState.Recovery: statusBtn.AddToClassList("status-recovery"); break;
                    }

                    statusBtn.RegisterCallback<ClickEvent>(evt =>
                    {
                        evt.StopPropagation();
                        ToggleDjinnStatus(djinnInstance);
                    });
                }

                slotInstance.RegisterCallback<ClickEvent>(evt =>
                {
                    selectedDjinnForDetails = djinnInstance;
                    RefreshVistaA();
                });
            }
            else
            {
                cardRoot.AddToClassList("djinn-card-empty");

                if (cardHeader != null) cardHeader.style.display = DisplayStyle.None;
                if (cardBody != null) cardBody.style.display = DisplayStyle.None;

                if (emptyLabel != null)
                {
                    emptyLabel.text = "Vacío";
                    emptyLabel.style.display = DisplayStyle.Flex;
                }
            }

            djinnSlotsGrid.Add(slotInstance);
        }
    }

    private void SelectDjinnInVistaA(DjinnInstance djinnInstance)
    {
        if (djinnInstance == null || djinnInstance.DjinnData == null) return;

        selectedDjinnForDetails = djinnInstance;
        DjinnSO data = djinnInstance.DjinnData;

        if (labelDjinnName != null) labelDjinnName.text = data.DjinnName;
        if (labelDjinnType != null) labelDjinnType.text = $"Djinn de {GetElementDisplayName(data.Element)} - {data.Element}";
        if (labelDjinnStatus != null) labelDjinnStatus.text = $"Estado: {GetStatusButtonText(djinnInstance.CurrentState)}";
        if (labelDjinnRecovery != null) labelDjinnRecovery.text = djinnInstance.CurrentState == DjinnState.Recovery ? $"Recuperación: {djinnInstance.CurrentRecoveryTurns} Turnos" : "";
        if (labelDjinnDesc != null) labelDjinnDesc.text = data.Description;

        UpdateSetBonusGrid(data);

        if (data.UnleashEffect != null)
        {
            DjinnUnleashSO unleash = data.UnleashEffect;

            if (labelUnleashName != null) labelUnleashName.text = unleash.UnleashName;
            if (labelUnleashDesc != null) labelUnleashDesc.text = unleash.Description;

            RenderUnleashBadges(unleash);
        }
        else
        {
            if (labelUnleashName != null) labelUnleashName.text = "Ninguno";
            if (labelUnleashDesc != null) labelUnleashDesc.text = "Sin efecto de desatado.";

            if (boxUnleashInfo != null)
            {
                boxUnleashInfo.Clear();
                Label emptyBadge = new Label("Sin Efecto");
                emptyBadge.AddToClassList("unleash-badge");
                emptyBadge.AddToClassList("badge-target");
                boxUnleashInfo.Add(emptyBadge);
            }
        }

        if (labelLeaderPassiveTitle != null)
        {
            labelLeaderPassiveTitle.text = (data.LeaderPassiveEffect != null) ? data.LeaderPassiveEffect.EffectName : "Sin Pasiva";
        }

        if (labelLeaderPassiveDesc != null)
        {
            labelLeaderPassiveDesc.text = (data.LeaderPassiveEffect != null && !string.IsNullOrEmpty(data.LeaderPassiveEffect.Description))
                ? data.LeaderPassiveEffect.Description
                : "Este Djinn no posee una pasiva de líder.";
        }

        RenderDjinnVideo(djinnInstance);
    }

    private void RenderUnleashBadges(DjinnUnleashSO unleash)
    {
        if (boxUnleashInfo == null) return;

        boxUnleashInfo.Clear();

        string targetText = GetTargetBadgeText(unleash.TargetsAlly, unleash.IsAreaOfEffect);
        Sprite targetIcon = GetTargetIcon(unleash.TargetsAlly, unleash.IsAreaOfEffect);

        VisualElement targetBadge = CreateBadgeContainer("badge-target", targetIcon, targetText);
        boxUnleashInfo.Add(targetBadge);

        if (unleash.DamageMultiplier > 1.0f)
        {
            VisualElement damageBadge = CreateBadgeContainer("badge-damage", null, $"Daño: x{unleash.DamageMultiplier:F2}");
            boxUnleashInfo.Add(damageBadge);
        }
        else if (unleash.FlatBonusDamage > 0)
        {
            VisualElement damageBadge = CreateBadgeContainer("badge-damage", null, $"Daño: +{unleash.FlatBonusDamage}");
            boxUnleashInfo.Add(damageBadge);
        }

        if (unleash.StatusEffects != null && unleash.StatusEffects.Count > 0)
        {
            foreach (StatusEffectSO status in unleash.StatusEffects)
            {
                if (status == null) continue;

                string chancePrefix = (unleash.StatusChance < 1.0f) ? $" {(int)(unleash.StatusChance * 100)}%" : "";
                string categoryPrefix = "";
                string categoryClass = "badge-target";
                Sprite effectIcon = null;

                switch (status.Category)
                {
                    case StatusEffectCategory.Buff:
                        categoryPrefix = "↑ ";
                        categoryClass = "badge-buff";
                        effectIcon = null;
                        break;

                    case StatusEffectCategory.Debuff:
                        categoryPrefix = "↓ ";
                        categoryClass = "badge-debuff";
                        effectIcon = null;
                        break;

                    case StatusEffectCategory.Ailment:
                        categoryClass = "badge-ailment";
                        effectIcon = status.Icon;
                        break;
                }

                string fullText = $"{categoryPrefix}{status.EffectName}{chancePrefix}";
                VisualElement effectBadge = CreateBadgeContainer(categoryClass, effectIcon, fullText);

                boxUnleashInfo.Add(effectBadge);
            }
        }
    }

    private VisualElement CreateBadgeContainer(string categoryClass, Sprite iconSprite, string text)
    {
        VisualElement badgeContainer = new VisualElement();
        badgeContainer.AddToClassList("unleash-badge");
        badgeContainer.AddToClassList(categoryClass);

        VisualElement iconElem = new VisualElement();
        iconElem.AddToClassList("badge-icon");

        if (iconSprite != null)
        {
            iconElem.style.backgroundImage = new StyleBackground(iconSprite);
            iconElem.style.display = DisplayStyle.Flex;
        }
        else
        {
            iconElem.style.display = DisplayStyle.None;
        }

        Label textLabel = new Label(text);

        badgeContainer.Add(iconElem);
        badgeContainer.Add(textLabel);

        return badgeContainer;
    }

    private Sprite GetTargetIcon(bool targetsAlly, bool isAreaOfEffect)
    {
        if (targetsAlly)
        {
            return isAreaOfEffect ? iconTargetGroup : iconTargetSingle;
        }
        else
        {
            return isAreaOfEffect ? iconTargetGroup : iconTargetSingle;
        }
    }

    private string GetTargetBadgeText(bool targetsAlly, bool isAreaOfEffect)
    {
        if (targetsAlly)
        {
            return isAreaOfEffect ? "Todos los Aliados" : "1 Aliado";
        }
        else
        {
            return isAreaOfEffect ? "Todos los Enemigos" : "1 Enemigo";
        }
    }

    private void UpdateSetBonusGrid(DjinnSO data)
    {
        float maxHp = 0, maxPp = 0, atk = 0, def = 0, agi = 0, sue = 0;

        if (data.SetStatModifiers != null)
        {
            foreach (var mod in data.SetStatModifiers)
            {
                switch (mod.statType)
                {
                    case StatType.MaxHP: maxHp += mod.value; break;
                    case StatType.MaxPP: maxPp += mod.value; break;
                    case StatType.Attack: atk += mod.value; break;
                    case StatType.Defense: def += mod.value; break;
                    case StatType.Agility: agi += mod.value; break;
                    case StatType.Luck: sue += mod.value; break;
                }
            }
        }

        if (labelSetBonusMaxHP != null) labelSetBonusMaxHP.text = formatBonus(maxHp, labelSetBonusMaxHP);
        if (labelSetBonusMaxPP != null) labelSetBonusMaxPP.text = formatBonus(maxPp, labelSetBonusMaxPP);
        if (labelSetBonusAtq != null) labelSetBonusAtq.text = formatBonus(atk, labelSetBonusAtq);
        if (labelSetBonusDef != null) labelSetBonusDef.text = formatBonus(def, labelSetBonusDef);
        if (labelSetBonusAgi != null) labelSetBonusAgi.text = formatBonus(agi, labelSetBonusAgi);
        if (labelSetBonusSue != null) labelSetBonusSue.text = formatBonus(sue, labelSetBonusSue);
    }

    private string formatBonus(float val, Label setBonusLabel)
    {
        if (val > 0)
        {
            setBonusLabel.style.color = new StyleColor(Color.green);
            return $"+{Mathf.RoundToInt(val)}";
        }
        else
        {
            setBonusLabel.style.color = new StyleColor(Color.white);
            return "0";
        }
    }

    private void ToggleDjinnStatus(DjinnInstance djinnInstance)
    {
        if (djinnInstance == null) return;

        if (djinnInstance.CurrentState == DjinnState.Set)
        {
            djinnInstance.PutOnStandby();
        }
        else if (djinnInstance.CurrentState == DjinnState.Standby)
        {
            djinnInstance.SetReady();
        }

        RefreshAllData();
    }

    private void ClearRightDetailPanel()
    {
        selectedDjinnForDetails = null;

        if (labelDjinnName != null) labelDjinnName.text = "---";
        if (labelDjinnType != null) labelDjinnType.text = "---";
        if (labelDjinnStatus != null) labelDjinnStatus.text = "Estado: ---";
        if (labelDjinnRecovery != null) labelDjinnRecovery.text = "Recuperación: ---";
        if (labelDjinnDesc != null) labelDjinnDesc.text = "Selecciona un Djinn para ver sus detalles.";

        if (labelSetBonusMaxHP != null) labelSetBonusMaxHP.text = "+0";
        if (labelSetBonusMaxPP != null) labelSetBonusMaxPP.text = "+0";
        if (labelSetBonusAtq != null) labelSetBonusAtq.text = "+0";
        if (labelSetBonusDef != null) labelSetBonusDef.text = "+0";
        if (labelSetBonusAgi != null) labelSetBonusAgi.text = "+0";
        if (labelSetBonusSue != null) labelSetBonusSue.text = "+0";

        if (labelUnleashName != null) labelUnleashName.text = "---";
        if (labelUnleashDesc != null) labelUnleashDesc.text = "---";
        if (labelUnleashDamage != null) labelUnleashDamage.text = "Daño: ---";
        if (labelUnleashEffect != null) labelUnleashEffect.text = "Efecto: ---";

        if (labelLeaderPassiveTitle != null) labelLeaderPassiveTitle.text = "Sin Pasiva";
        if (labelLeaderPassiveDesc != null) labelLeaderPassiveDesc.text = "---";
    }

    #endregion

    #region Modular Psynergy Card Instantiation

    private TemplateContainer CreatePsynergyCardInstance(PsynergySO psynergy, PsynergyCardState state, int requiredLevel = 0)
    {
        if (psynergyCardAsset == null) return null;

        TemplateContainer cardInstance = psynergyCardAsset.Instantiate();
        VisualElement cardRoot = cardInstance.Q<VisualElement>("mini_psy_card_root");
        if (cardRoot == null) return cardInstance;

        cardRoot.RemoveFromClassList("element-venus");
        cardRoot.RemoveFromClassList("element-mars");
        cardRoot.RemoveFromClassList("element-jupiter");
        cardRoot.RemoveFromClassList("element-mercury");

        if (psynergy != null && state != PsynergyCardState.Locked)
        {
            cardRoot.AddToClassList($"element-{psynergy.Element.ToString().ToLower()}");

            VisualElement elementPip = cardRoot.Q<VisualElement>("mini_psy_element_pip");
            if (elementPip != null)
            {
                elementPip.RemoveFromClassList("icon-venus");
                elementPip.RemoveFromClassList("icon-mars");
                elementPip.RemoveFromClassList("icon-jupiter");
                elementPip.RemoveFromClassList("icon-mercury");
                elementPip.AddToClassList($"icon-{psynergy.Element.ToString().ToLower()}");
            }

            VisualElement iconElem = cardRoot.Q<VisualElement>("mini_psy_icon");
            if (iconElem != null && psynergy.Icon != null)
            {
                iconElem.style.backgroundImage = new StyleBackground(psynergy.Icon);
            }

            Label nameLabel = cardRoot.Q<Label>("mini_psy_name");
            if (nameLabel != null) nameLabel.text = psynergy.PsynergyName;

            Label ppLabel = cardRoot.Q<Label>("mini_psy_pp");
            if (ppLabel != null) ppLabel.text = $"{psynergy.PpCost} PP";

            Label badgeMenu = cardRoot.Q<Label>("badge_menu");
            if (badgeMenu != null)
            {
                badgeMenu.style.display = psynergy.MenuData.CanBeUsedInMenu ? DisplayStyle.Flex : DisplayStyle.None;
            }

            Label badgeBattle = cardRoot.Q<Label>("badge_battle");
            if (badgeBattle != null)
            {
                badgeBattle.style.display = psynergy.IsBattlePsynergy ? DisplayStyle.Flex : DisplayStyle.None;
            }

            Label badgeWorld = cardRoot.Q<Label>("badge_world");
            if (badgeWorld != null)
            {
                badgeWorld.style.display = psynergy.WorldData.CanBeUsedInWorld ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        VisualElement statusBox = cardRoot.Q<VisualElement>("mini_psy_status_box");
        Label labelStatus = cardRoot.Q<Label>("label_psy_status");

        switch (state)
        {
            case PsynergyCardState.Normal:
                if (statusBox != null) statusBox.style.display = DisplayStyle.None;
                break;

            case PsynergyCardState.Gained:
                if (statusBox != null) statusBox.style.display = DisplayStyle.Flex;
                if (labelStatus != null)
                {
                    labelStatus.text = "+ GANADA";
                    labelStatus.RemoveFromClassList("badge-lost");
                    labelStatus.AddToClassList("badge-gained");
                    labelStatus.style.display = DisplayStyle.Flex;
                }
                break;

            case PsynergyCardState.Lost:
                if (statusBox != null) statusBox.style.display = DisplayStyle.Flex;
                if (labelStatus != null)
                {
                    labelStatus.text = "- PERDIDA";
                    labelStatus.RemoveFromClassList("badge-gained");
                    labelStatus.AddToClassList("badge-lost");
                    labelStatus.style.display = DisplayStyle.Flex;
                }
                break;

            case PsynergyCardState.Locked:
                VisualElement lockOverlay = cardRoot.Q<VisualElement>("mini_psy_lock_overlay");
                Label lockLevelLabel = cardRoot.Q<Label>("label_lock_level");
                if (lockOverlay != null) lockOverlay.style.display = DisplayStyle.Flex;
                if (lockLevelLabel != null) lockLevelLabel.text = $"Nv. {requiredLevel}";
                break;
        }

        return cardInstance;
    }

    private void RefreshClassPsynergies()
    {
        if (scrollClassPsynergies == null) return;

        scrollClassPsynergies.Clear();

        if (currentHero == null || psynergyCardAsset == null) return;

        IReadOnlyList<PsynergySO> heroPsynergies = currentHero.Psynergies;
        if (heroPsynergies != null)
        {
            foreach (PsynergySO psynergy in heroPsynergies)
            {
                if (psynergy == null) continue;
                TemplateContainer card = CreatePsynergyCardInstance(psynergy, PsynergyCardState.Normal);
                if (card != null) scrollClassPsynergies.Add(card);
            }
        }

        List<ClassPsynergyEntry> futurePsynergies = currentHeroDjinnController.CurrentClass.GetFuturePsynergies(currentHero.Level);
        foreach (var entry in futurePsynergies)
        {
            TemplateContainer card = CreatePsynergyCardInstance(entry.psynergy, PsynergyCardState.Locked, entry.requiredLevel);
            if (card != null) scrollClassPsynergies.Add(card);
        }
    }

    private void RefreshPsynergiesComparison(ScrollView container, List<PsynergySO> gained, List<PsynergySO> lost, List<PsynergySO> kept)
    {
        if (container == null) return;
        container.Clear();

        if (gained != null)
        {
            foreach (var psy in gained)
            {
                var card = CreatePsynergyCardInstance(psy, PsynergyCardState.Gained);
                if (card != null) container.Add(card);
            }
        }

        if (lost != null)
        {
            foreach (var psy in lost)
            {
                var card = CreatePsynergyCardInstance(psy, PsynergyCardState.Lost);
                if (card != null) container.Add(card);
            }
        }

        if (kept != null)
        {
            foreach (var psy in kept)
            {
                var card = CreatePsynergyCardInstance(psy, PsynergyCardState.Normal);
                if (card != null) container.Add(card);
            }
        }
    }

    #endregion

    #region Vista B & Party Rendering

    private void RefreshVistaB()
    {
        if (partyColumnsContainer == null || partyHeroes == null || djinnSlotCardBAsset == null) return;

        List<VisualElement> columns = partyColumnsContainer.Query<VisualElement>(className: "hero-djinn-column").ToList();

        for (int i = 0; i < columns.Count; i++)
        {
            VisualElement columnElem = columns[i];
            HeroStats hero = (i < partyHeroes.Count) ? partyHeroes[i] : null;

            if (hero == null)
            {
                columnElem.style.display = DisplayStyle.None;
                continue;
            }

            columnElem.style.display = DisplayStyle.Flex;

            // 1. Populate Header Data
            VisualElement avatarIcon = columnElem.Q<VisualElement>("hero_avatar_icon");
            Label nameLabel = columnElem.Q<Label>("label_hero_name");
            Label levelLabel = columnElem.Q<Label>("label_hero_level");
            Label classCurrentLabel = columnElem.Q<Label>("label_hero_class_current");

            Label pipVenus = columnElem.Q<Label>("label_pip_venus");
            Label pipMars = columnElem.Q<Label>("label_pip_mars");
            Label pipJupiter = columnElem.Q<Label>("label_pip_jupiter");
            Label pipMercury = columnElem.Q<Label>("label_pip_mercury");

            if (avatarIcon != null) avatarIcon.style.backgroundImage = new StyleBackground(hero.TurnBarImage);
            if (nameLabel != null) nameLabel.text = hero.Name;
            if (levelLabel != null) levelLabel.text = $"Nv. {hero.Level}";

            HeroDjinnController djinnCtrl = hero.GetComponent<HeroDjinnController>();
            if (classCurrentLabel != null && djinnCtrl != null && djinnCtrl.CurrentClass != null)
            {
                classCurrentLabel.text = djinnCtrl.CurrentClass.ClassName;
            }

            UpdateElementalPips(djinnCtrl, pipVenus, pipMars, pipJupiter, pipMercury);

            // 2. Populate Djinn Slot Cards
            VisualElement slotsContainer = columnElem.Q<VisualElement>(className: "column-slots-container");
            if (slotsContainer == null) continue;

            slotsContainer.Clear();

            IReadOnlyList<DjinnInstance> assignedDjinns = (djinnCtrl != null) ? djinnCtrl.AssignedDjinns : null;

            for (int slotIndex = 0; slotIndex < 8; slotIndex++)
            {
                TemplateContainer slotInstance = djinnSlotCardBAsset.Instantiate();
                VisualElement cardRoot = slotInstance.Q<VisualElement>("djinn_slot_card_b_root");
                if (cardRoot == null) continue;

                DjinnInstance djinn = (assignedDjinns != null && slotIndex < assignedDjinns.Count) ? assignedDjinns[slotIndex] : null;

                VisualElement cardBody = cardRoot.Q<VisualElement>("djinn_card_body");
                VisualElement iconElem = cardRoot.Q<VisualElement>("djinn_card_icon");
                Label cardNameLabel = cardRoot.Q<Label>("label_djinn_card_name");
                Button btnLeaderTag = cardRoot.Q<Button>("btn_leader_tag");
                Label unleashValLabel = cardRoot.Q<Label>("label_unleash_short_desc");
                Button statusBtn = cardRoot.Q<Button>("btn_djinn_status");
                Label emptyLabel = cardRoot.Q<Label>("label_djinn_empty");
                VisualElement elementPip = cardRoot.Q<VisualElement>("djinn_element_pip");

                cardRoot.RemoveFromClassList("djinn-card-venus");
                cardRoot.RemoveFromClassList("djinn-card-mars");
                cardRoot.RemoveFromClassList("djinn-card-jupiter");
                cardRoot.RemoveFromClassList("djinn-card-mercury");

                if (djinn != null && djinn.DjinnData != null)
                {
                    DjinnSO data = djinn.DjinnData;

                    if (cardBody != null) cardBody.style.display = DisplayStyle.Flex;
                    if (emptyLabel != null) emptyLabel.style.display = DisplayStyle.None;

                    cardRoot.AddToClassList($"djinn-card-{data.Element.ToString().ToLower()}");

                    if (elementPip != null)
                    {
                        elementPip.RemoveFromClassList("icon-venus");
                        elementPip.RemoveFromClassList("icon-mars");
                        elementPip.RemoveFromClassList("icon-jupiter");
                        elementPip.RemoveFromClassList("icon-mercury");
                        elementPip.AddToClassList($"icon-{data.Element.ToString().ToLower()}");
                    }

                    if (iconElem != null && data.Icon != null)
                    {
                        iconElem.style.backgroundImage = new StyleBackground(data.Icon);
                    }

                    if (cardNameLabel != null) cardNameLabel.text = data.DjinnName;

                    // Leader Tag Status
                    bool isLeader = (djinnCtrl != null && djinnCtrl.LeaderDjinn == djinn);
                    if (btnLeaderTag != null)
                    {
                        btnLeaderTag.RemoveFromClassList("leader-active");
                        btnLeaderTag.RemoveFromClassList("leader-inactive");

                        if (isLeader)
                        {
                            btnLeaderTag.AddToClassList("leader-active");
                            btnLeaderTag.text = "★ LÍDER";
                        }
                        else
                        {
                            btnLeaderTag.AddToClassList("leader-inactive");
                            btnLeaderTag.text = "LÍDER";
                        }
                    }

                    // Unleash short description
                    if (unleashValLabel != null)
                    {
                        unleashValLabel.text = (data.UnleashEffect != null) ? data.UnleashEffect.Description : "Sin efecto";
                    }

                    // Status Button
                    if (statusBtn != null)
                    {
                        statusBtn.text = GetStatusButtonText(djinn.CurrentState);
                        statusBtn.RemoveFromClassList("status-set");
                        statusBtn.RemoveFromClassList("status-standby");
                        statusBtn.RemoveFromClassList("status-recovery");

                        switch (djinn.CurrentState)
                        {
                            case DjinnState.Set: statusBtn.AddToClassList("status-set"); break;
                            case DjinnState.Standby: statusBtn.AddToClassList("status-standby"); break;
                            case DjinnState.Recovery: statusBtn.AddToClassList("status-recovery"); break;
                        }

                        statusBtn.RegisterCallback<ClickEvent>(evt =>
                        {
                            evt.StopPropagation();
                            ToggleDjinnStatus(djinn);
                        });
                    }
                }
                else
                {
                    if (cardBody != null) cardBody.style.display = DisplayStyle.None;
                    if (emptyLabel != null)
                    {
                        emptyLabel.text = "Vacío";
                        emptyLabel.style.display = DisplayStyle.Flex;
                    }
                }

                slotsContainer.Add(slotInstance);
            }
        }
    }

    private void SwitchToView(DjinnViewMode newMode)
    {
        currentViewMode = newMode;

        // English comment: Notify MainMenuController to update root containers visibility
        OnRequestViewChange?.Invoke(newMode);

        RefreshAllData();
    }

    #endregion

    #region Helper Methods

    private string GetStatusButtonText(DjinnState state)
    {
        switch (state)
        {
            case DjinnState.Set: return "PREPARADO";
            case DjinnState.Standby: return "EN ESPERA";
            case DjinnState.Recovery: return "RECUPERANDO";
            default: return "---";
        }
    }

    private string GetElementDisplayName(ElementalType element)
    {
        switch (element)
        {
            case ElementalType.Venus: return "Tierra";
            case ElementalType.Mars: return "Fuego";
            case ElementalType.Jupiter: return "Viento";
            case ElementalType.Mercury: return "Agua";
            default: return "---";
        }
    }

    private void RenderDjinnVideo(DjinnInstance djinnInstance)
    {
        // TODO: Cargar RenderTarget/VideoPlayer
    }

    private void UpdateSanctuaryUI()
    {
        // TODO: Actualizar estado 3D del santuario
    }

    #endregion
}