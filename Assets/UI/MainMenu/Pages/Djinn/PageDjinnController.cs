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

#region Drag & Drop Data Structures (Vista B)

public class DjinnDragContext
{
    public HeroStats SourceHero { get; private set; }
    public DjinnInstance SourceDjinn { get; private set; }
    public VisualElement SourceCardUI { get; private set; }

    public DjinnDragContext(HeroStats sourceHero, DjinnInstance sourceDjinn, VisualElement sourceCardUI)
    {
        SourceHero = sourceHero;
        SourceDjinn = sourceDjinn;
        SourceCardUI = sourceCardUI;
    }
}

public struct DjinnDropTargetInfo
{
    public HeroStats TargetHero;
    public DjinnInstance TargetDjinn;
    public VisualElement SlotUI;

    public DjinnDropTargetInfo(HeroStats hero, DjinnInstance djinn, VisualElement slotUI)
    {
        TargetHero = hero;
        TargetDjinn = djinn;
        SlotUI = slotUI;
    }
}

#endregion

public class PageDjinnController
{
    #region Events

    public event Action OnDjinnStateChanged;
    public event Action<bool> OnToggleTopHeroesBar;
    public event Action<DjinnViewMode> OnRequestViewChange;

    #endregion

    #region Private Fields - Containers & Views

    private VisualElement rootViewA;
    private VisualElement rootViewB;

    private VisualElement pageDjinnContainer;
    private VisualElement sectionVistaB;
    private VisualElement modalOverlayCompare;

    private VisualElement partyColumnsContainer;
    private Button btnSwitchToVistaA;

    private DjinnViewMode currentViewMode = DjinnViewMode.Individual;

    #endregion

    #region Private Fields - Left Panel Elements

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

    #region Private Fields - Center Panel Elements

    private Label labelGridHeader;
    private VisualElement djinnSlotsGrid;
    private VisualElement djinn3dRenderTarget;
    private Button btnSwitchToVistaB;

    #endregion

    #region Private Fields - Right Panel Elements

    private VisualElement djinnVideoPreview;
    private VisualElement djinnElementalIcon;
    private Label labelDjinnName;
    private Label labelDjinnType;
    private Label labelDjinnStatus;
    private Label labelDjinnRecovery;
    private Label labelDjinnDesc;

    private Label labelSetBonusMaxHP;
    private Label labelSetBonusMaxPP;
    private Label labelSetBonusAtq;
    private Label labelSetBonusDef;
    private Label labelSetBonusAgi;
    private Label labelSetBonusSue;

    private VisualElement boxUnleashStatusEffectInfo;
    private Label labelUnleashName;
    private Label labelUnleashDesc;
    private Label labelUnleashDamage;
    private Label labelUnleashEffect;

    private Sprite iconTargetSingle;
    private Sprite iconTargetGroup;

    private Label labelLeaderPassiveTitle;
    private Label labelLeaderPassiveDesc;

    #endregion

    #region Private Fields - Data & Selection State

    private List<HeroStats> partyHeroes = new List<HeroStats>();
    private HeroStats currentHero;
    private HeroDjinnController currentHeroDjinnController;

    private VisualTreeAsset psynergyCardAsset;
    private VisualTreeAsset djinnSlotCardAAsset;
    private VisualTreeAsset djinnSlotCardBAsset;

    private DjinnInstance selectedDjinnForDetails;

    private DjinnInstance swapSourceDjinn;
    private HeroStats swapSourceHero;
    private DjinnInstance swapTargetDjinn;
    private HeroStats swapTargetHero;

    #endregion

    #region Private Fields - Drag & Drop System (Vista B - Pattern Items)

    private DjinnDragContext currentDragContext;
    private VisualElement ghostElement;
    private bool isDragging = false;
    private Vector2 pointerStartPosition;
    private int activePointerId = -1;
    private const float DRAG_THRESHOLD = 5f;

    private List<(VisualElement element, HeroStats ownerHero, DjinnInstance djinnInstance)> dragTargetSlots
        = new List<(VisualElement, HeroStats, DjinnInstance)>();

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

    #region Helpers for Top Root Level

    private VisualElement GetTopRootElement()
    {
        VisualElement root = rootViewB ?? rootViewA;
        return root?.panel?.visualTree ?? root;
    }

    #endregion

    #region UI Binding

    private void BindUIElements()
    {
        if (rootViewA != null)
        {
            pageDjinnContainer = rootViewA.Q<VisualElement>("page_djinn_container") ?? rootViewA;

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

            labelGridHeader = rootViewA.Q<Label>("label_grid_header");
            djinnSlotsGrid = rootViewA.Q<VisualElement>("djinn_slots_grid");
            djinn3dRenderTarget = rootViewA.Q<VisualElement>("djinn_3d_render_target");
            btnSwitchToVistaB = rootViewA.Q<Button>("btn_switch_vista_b");

            djinnVideoPreview = rootViewA.Q<VisualElement>("djinn_video_preview");
            djinnElementalIcon = rootViewA.Q<VisualElement>("detail_djinn_icon");
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

            boxUnleashStatusEffectInfo = rootViewA.Q<VisualElement>("box_unleash_status_effect_info");
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

        if (rootViewB != null)
        {
            sectionVistaB = rootViewB.Q<VisualElement>("page_djinn_global_container")
                         ?? rootViewB.Q<VisualElement>("djinn_global_section")
                         ?? rootViewB;

            partyColumnsContainer = rootViewB.Q<VisualElement>("party_columns_container");
            modalOverlayCompare = rootViewB.Q<VisualElement>("compare_modal_overlay");
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

    public void SetTemplates(VisualTreeAsset psynergyCard, VisualTreeAsset djinnSlotCardA, VisualTreeAsset djinnSlotCardB)
    {
        psynergyCardAsset = psynergyCard;
        djinnSlotCardAAsset = djinnSlotCardA;
        djinnSlotCardBAsset = djinnSlotCardB;
    }

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

        UpdateElementalPips(currentHeroDjinnController, labelPipVenus, labelPipMars, labelPipJupiter, labelPipMercury);

        if (labelStatAtq != null) labelStatAtq.text = Mathf.RoundToInt(currentHero.Attack).ToString();
        if (labelStatDef != null) labelStatDef.text = Mathf.RoundToInt(currentHero.Defense).ToString();
        if (labelStatAgi != null) labelStatAgi.text = Mathf.RoundToInt(currentHero.Agility).ToString();
        if (labelStatSue != null) labelStatSue.text = Mathf.RoundToInt(currentHero.Luck).ToString();
        if (labelStatMaxHP != null) labelStatMaxHP.text = Mathf.RoundToInt(currentHero.MaxHp).ToString();
        if (labelStatMaxPP != null) labelStatMaxPP.text = Mathf.RoundToInt(currentHero.MaxPp).ToString();

        HideStatDeltas();

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

        if (selectedDjinnForDetails != null)
        {
            SelectDjinnInVistaA(selectedDjinnForDetails);
        }
        else
        {
            ClearRightDetailPanel();
        }

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

        if (djinnElementalIcon != null)
        {
            djinnElementalIcon.RemoveFromClassList("icon-venus");
            djinnElementalIcon.RemoveFromClassList("icon-mars");
            djinnElementalIcon.RemoveFromClassList("icon-jupiter");
            djinnElementalIcon.RemoveFromClassList("icon-mercury");

            djinnElementalIcon.AddToClassList($"icon-{data.Element.ToString().ToLower()}");
            djinnElementalIcon.style.display = DisplayStyle.Flex;
        }
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

            if (boxUnleashStatusEffectInfo != null)
            {
                boxUnleashStatusEffectInfo.Clear();
                Label emptyBadge = new Label("Sin Efecto");
                emptyBadge.AddToClassList("unleash-badge");
                emptyBadge.AddToClassList("badge-target");
                boxUnleashStatusEffectInfo.Add(emptyBadge);
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
        if (boxUnleashStatusEffectInfo == null) return;

        boxUnleashStatusEffectInfo.Clear();

        string targetText = GetTargetBadgeText(unleash.TargetsAlly, unleash.IsAreaOfEffect);
        Sprite targetIcon = GetTargetIcon(unleash.TargetsAlly, unleash.IsAreaOfEffect);

        VisualElement targetBadge = CreateBadgeContainer("badge-target", targetIcon, targetText);
        boxUnleashStatusEffectInfo.Add(targetBadge);

        if (unleash.DamageMultiplier > 1.0f)
        {
            VisualElement damageBadge = CreateBadgeContainer("badge-damage", null, $"Daño: x{unleash.DamageMultiplier:F2}");
            boxUnleashStatusEffectInfo.Add(damageBadge);
        }
        else if (unleash.FlatBonusDamage > 0)
        {
            VisualElement damageBadge = CreateBadgeContainer("badge-damage", null, $"Daño: +{unleash.FlatBonusDamage}");
            boxUnleashStatusEffectInfo.Add(damageBadge);
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

                boxUnleashStatusEffectInfo.Add(effectBadge);
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
        return isAreaOfEffect ? iconTargetGroup : iconTargetSingle;
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

        if (djinnElementalIcon != null) djinnElementalIcon.style.display = DisplayStyle.None;
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

    private VisualElement CreatePsynergyCard(PsynergySO psynergy, PsynergyCardState cardState, int requiredLevel = 0)
    {
        if (psynergyCardAsset == null)
        {
            Debug.LogError("[PageDjinn] psynergyCardTemplate is null. Cannot instantiate card.");
            return new VisualElement();
        }

        TemplateContainer cardInstance = psynergyCardAsset.Instantiate();
        VisualElement card = cardInstance.Q<VisualElement>("psynergy_card") ?? cardInstance;

        card.AddToClassList(GetElementCssClass(psynergy.Element));

        VisualElement cardIcon = card.Q<VisualElement>("psynergy_card_icon");
        if (cardIcon != null && psynergy.Icon != null)
        {
            cardIcon.style.backgroundImage = new StyleBackground(psynergy.Icon);
        }

        VisualElement elementPipIcon = card.Q<VisualElement>("element_pip_icon");
        if (elementPipIcon != null)
        {
            elementPipIcon.AddToClassList(GetElementPipCssClass(psynergy.Element));
        }

        Label nameLabel = card.Q<Label>("label_card_name") ?? card.Q<Label>("label_psynergy_name");
        if (nameLabel != null)
        {
            nameLabel.text = psynergy.PsynergyName;
        }

        VisualElement badgesRow = card.Q<VisualElement>("card_badges_row");
        if (badgesRow != null)
        {
            badgesRow.Clear();

            if (psynergy.WorldData.CanBeUsedInWorld)
            {
                Label worldBadge = new Label("Mundo");
                worldBadge.AddToClassList("psynergy-badge");
                worldBadge.AddToClassList("badge-world");
                badgesRow.Add(worldBadge);
            }

            if (psynergy.MenuData.CanBeUsedInMenu)
            {
                Label menuBadge = new Label("Menú");
                menuBadge.AddToClassList("psynergy-badge");
                menuBadge.AddToClassList("badge-menu");
                badgesRow.Add(menuBadge);
            }

            if (psynergy.IsBattlePsynergy)
            {
                Label battleBadge = new Label("Combate");
                battleBadge.AddToClassList("psynergy-badge");
                battleBadge.AddToClassList("badge-battle");
                badgesRow.Add(battleBadge);
            }
        }

        Label ppLabel = card.Q<Label>("label_card_pp") ?? card.Q<Label>("label_psynergy_pp");
        if (ppLabel != null)
        {
            ppLabel.text = $"{psynergy.PpCost} PP";
        }

        VisualElement lockOverlay = card.Q<VisualElement>("mini_psy_lock_overlay");
        Label statusLabel = card.Q<Label>("label_psy_status");

        if (lockOverlay != null) lockOverlay.style.display = DisplayStyle.None;
        if (statusLabel != null) statusLabel.style.display = DisplayStyle.None;

        switch (cardState)
        {
            case PsynergyCardState.Locked:
                card.AddToClassList("psy-card-locked");
                if (lockOverlay != null)
                {
                    lockOverlay.style.display = DisplayStyle.Flex;

                    Label levelLabel = lockOverlay.Q<Label>("label_lock_level");
                    if (levelLabel != null)
                    {
                        levelLabel.text = $"NIVEL {requiredLevel}";
                    }
                }
                break;

            case PsynergyCardState.Gained:
                card.AddToClassList("psy-card-new");
                if (statusLabel != null)
                {
                    statusLabel.text = "+ GANADA";
                    statusLabel.RemoveFromClassList("badge-lost");
                    statusLabel.RemoveFromClassList("text-lost");
                    statusLabel.AddToClassList("badge-gained");
                    statusLabel.style.display = DisplayStyle.Flex;
                }
                break;

            case PsynergyCardState.Lost:
                card.AddToClassList("psy-card-lost");
                if (statusLabel != null)
                {
                    statusLabel.text = "- PERDIDA";
                    statusLabel.RemoveFromClassList("badge-gained");
                    statusLabel.AddToClassList("badge-lost");
                    statusLabel.AddToClassList("text-lost");
                    statusLabel.style.display = DisplayStyle.Flex;
                }
                break;

            case PsynergyCardState.Normal:
            default:
                break;
        }

        return card;
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
        switch (element)
        {
            case ElementalType.Venus: return "element-venus";
            case ElementalType.Mars: return "element-mars";
            case ElementalType.Jupiter: return "element-jupiter";
            case ElementalType.Mercury: return "element-mercury";
            default: return "element-venus";
        }
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
                var card = CreatePsynergyCard(psynergy, PsynergyCardState.Normal);
                if (card != null) scrollClassPsynergies.Add(card);
            }
        }

        List<ClassPsynergyEntry> futurePsynergies = currentHeroDjinnController.CurrentClass.GetFuturePsynergies(currentHero.Level);
        foreach (var entry in futurePsynergies)
        {
            var card = CreatePsynergyCard(entry.psynergy, PsynergyCardState.Locked, entry.requiredLevel);
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
                var card = CreatePsynergyCard(psy, PsynergyCardState.Gained);
                if (card != null) container.Add(card);
            }
        }

        if (lost != null)
        {
            foreach (var psy in lost)
            {
                var card = CreatePsynergyCard(psy, PsynergyCardState.Lost);
                if (card != null) container.Add(card);
            }
        }

        if (kept != null)
        {
            foreach (var psy in kept)
            {
                var card = CreatePsynergyCard(psy, PsynergyCardState.Normal);
                if (card != null) container.Add(card);
            }
        }
    }

    #endregion

    #region Vista B & Party Rendering

    private void RefreshVistaB()
    {
        if (partyColumnsContainer == null || partyHeroes == null || djinnSlotCardBAsset == null) return;

        dragTargetSlots.Clear();
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

            // 1. Header
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

            // 2. Djinn Slots
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

                // Force pickingMode = Position for raycast drop detection
                cardRoot.pickingMode = PickingMode.Position;

                // Cache reference for Drag & Drop hit testing
                dragTargetSlots.Add((cardRoot, hero, djinn));

                VisualElement cardBody = cardRoot.Q<VisualElement>("djinn_card_body");
                VisualElement iconElem = cardRoot.Q<VisualElement>("djinn_card_icon");
                Label cardNameLabel = cardRoot.Q<Label>("label_djinn_card_name");
                Button btnLeaderTag = cardRoot.Q<Button>("btn_leader_tag");
                Label unleashValLabel = cardRoot.Q<Label>("label_unleash_short_desc");
                Button statusBtn = cardRoot.Q<Button>("btn_djinn_status");
                Label emptyLabel = cardRoot.Q<Label>("label_djinn_empty");
                VisualElement elementPip = cardRoot.Q<VisualElement>("djinn_element_pip");

                if (cardBody != null) cardBody.pickingMode = PickingMode.Ignore;
                if (iconElem != null) iconElem.pickingMode = PickingMode.Ignore;
                if (cardNameLabel != null) cardNameLabel.pickingMode = PickingMode.Ignore;
                if (unleashValLabel != null) unleashValLabel.pickingMode = PickingMode.Ignore;
                if (emptyLabel != null) emptyLabel.pickingMode = PickingMode.Ignore;
                if (elementPip != null) elementPip.pickingMode = PickingMode.Ignore;

                cardRoot.RemoveFromClassList("element-venus");
                cardRoot.RemoveFromClassList("element-mars");
                cardRoot.RemoveFromClassList("element-jupiter");
                cardRoot.RemoveFromClassList("element-mercury");
                cardRoot.RemoveFromClassList("djinn-card-disabled");
                cardRoot.RemoveFromClassList("djinn-slot-drop-valid");
                cardRoot.RemoveFromClassList("djinn-slot-drop-invalid");

                if (djinn != null && djinn.DjinnData != null)
                {
                    DjinnSO data = djinn.DjinnData;

                    if (cardBody != null) cardBody.style.display = DisplayStyle.Flex;
                    if (emptyLabel != null) emptyLabel.style.display = DisplayStyle.None;

                    cardRoot.AddToClassList($"element-{data.Element.ToString().ToLower()}");

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
                            btnLeaderTag.text = "★ LÍDER";
                        }
                    }

                    if (unleashValLabel != null)
                    {
                        unleashValLabel.text = (data.UnleashEffect != null) ? data.UnleashEffect.Description : "Sin efecto";
                    }

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

                    RegisterPointerCallbacks(cardRoot, hero, djinn);
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
        OnRequestViewChange?.Invoke(newMode);
        RefreshAllData();
    }

    #endregion

    #region Pointer & Drag Events (Vista B - Pattern Items)

    private void RegisterPointerCallbacks(VisualElement slotUI, HeroStats hero, DjinnInstance djinn)
    {
        slotUI.RegisterCallback<PointerDownEvent>(evt => OnSlotPointerDown(evt, hero, djinn, slotUI));
        slotUI.RegisterCallback<PointerMoveEvent>(OnSlotPointerMove);
        slotUI.RegisterCallback<PointerUpEvent>(OnSlotPointerUp);
        slotUI.RegisterCallback<PointerCaptureOutEvent>(OnSlotPointerCancel);
    }

    private void OnSlotPointerDown(PointerDownEvent evt, HeroStats hero, DjinnInstance djinn, VisualElement slotUI)
    {
        if (evt.button != 0 || djinn == null || hero == null) return;

        VisualElement topRoot = GetTopRootElement();
        if (topRoot == null) return;

        // Guardamos la posición inicial siempre proyectada en el espacio local del topRoot
        pointerStartPosition = topRoot.WorldToLocal(evt.position);
        activePointerId = evt.pointerId;

        currentDragContext = new DjinnDragContext(hero, djinn, slotUI);
        isDragging = false;
    }

    private void OnSlotPointerMove(PointerMoveEvent evt)
    {
        if (currentDragContext == null || evt.pointerId != activePointerId) return;

        VisualElement topRoot = GetTopRootElement();
        if (topRoot == null) return;

        // Convertimos la posición global del puntero al espacio Panel estandarizado
        Vector2 currentPanelPos = topRoot.WorldToLocal(evt.position);

        if (!isDragging)
        {
            float distance = Vector2.Distance(pointerStartPosition, currentPanelPos);
            if (distance >= DRAG_THRESHOLD)
            {
                isDragging = true;

                if (currentDragContext.SourceCardUI != null)
                {
                    currentDragContext.SourceCardUI.AddToClassList("djinn-card-disabled");
                }

                swapSourceHero = currentDragContext.SourceHero;
                swapSourceDjinn = currentDragContext.SourceDjinn;

                HighlightDropTargets();
                CreateGhostElement(currentDragContext.SourceDjinn.DjinnData?.Icon, currentPanelPos);
                currentDragContext.SourceCardUI.CapturePointer(activePointerId);
            }
        }
        else
        {
            UpdateGhostPosition(currentPanelPos);
        }
    }

    private void OnSlotPointerUp(PointerUpEvent evt)
    {
        if (currentDragContext == null || evt.pointerId != activePointerId) return;

        VisualElement topRoot = GetTopRootElement();
        Vector2 panelPointerPos = topRoot != null ? topRoot.WorldToLocal(evt.position) : evt.position;

        if (currentDragContext.SourceCardUI != null && currentDragContext.SourceCardUI.HasPointerCapture(activePointerId))
        {
            currentDragContext.SourceCardUI.ReleasePointer(activePointerId);
        }

        if (isDragging)
        {
            ProcessDrop(panelPointerPos);
            RemoveGhostElement();
        }
        else
        {
            currentDragContext = null;
            activePointerId = -1;
        }
    }

    private void OnSlotPointerCancel(PointerCaptureOutEvent evt)
    {
        if (isDragging)
        {
            RemoveGhostElement();
        }
    }

    #endregion

    #region Drop Processing & Transactions

    private void ProcessDrop(Vector2 panelPointerPosition)
    {
        if (currentDragContext == null) return;

        if (TryGetDropTarget(panelPointerPosition, out DjinnDropTargetInfo targetInfo))
        {
            Debug.Log($"<color=cyan>✨ [TINKLE TINKLE] ✨ Slot Detectado:</color> Heroe = {targetInfo.TargetHero?.Name}, Djinn = {targetInfo.TargetDjinn?.DjinnData?.DjinnName ?? "Vacío"}");

            bool validDrop = CanDropOnTarget(currentDragContext.SourceHero, targetInfo.TargetHero, targetInfo.TargetDjinn);

            if (validDrop)
            {
                swapSourceHero = currentDragContext.SourceHero;
                swapSourceDjinn = currentDragContext.SourceDjinn;
                swapTargetHero = targetInfo.TargetHero;
                swapTargetDjinn = targetInfo.TargetDjinn;

                OpenSwapConfirmationModal();
            }
            else
            {
                Debug.LogWarning("⚠️ [PageDjinn] Drop rechazado: No cumple las reglas de intercambio/mínimos.");
            }
        }
        else
        {
            Debug.LogWarning("❌ [PageDjinn] Drop fallido: No se detectó ninguna casilla válida bajo el cursor.");
        }
    }

    private bool TryGetDropTarget(Vector2 panelPointerPosition, out DjinnDropTargetInfo targetInfo)
    {
        targetInfo = new DjinnDropTargetInfo(null, null, null);
        VisualElement topRoot = GetTopRootElement();

        foreach (var target in dragTargetSlots)
        {
            if (target.element != null)
            {
                // Convertimos el rectángulo worldBound de la tarjeta objetivo al espacio Panel
                Rect cardPanelBounds = topRoot != null
                    ? topRoot.WorldToLocal(target.element.worldBound)
                    : target.element.worldBound;

                if (cardPanelBounds.Contains(panelPointerPosition))
                {
                    Debug.Log($"<color=green>🎯 [DROP DETECTADO CON ÉXITO]:</color> Target Hero = {target.ownerHero.Name}, Djinn = {target.djinnInstance?.DjinnData?.DjinnName ?? "Vacío"}");

                    targetInfo = new DjinnDropTargetInfo(target.ownerHero, target.djinnInstance, target.element);
                    return true;
                }
            }
        }

        Debug.LogWarning($"<color=red>❌ [DROP FALLIDO]:</color> El puntero en espacio Panel {panelPointerPosition} no colisionó con ninguna casilla. (Tarjetas evaluadas: {dragTargetSlots.Count})");
        return false;
    }

    private bool IsElementOrChildOf(VisualElement child, VisualElement parent)
    {
        VisualElement current = child;
        while (current != null)
        {
            if (current == parent) return true;
            current = current.parent;
        }
        return false;
    }

    private bool CanDropOnTarget(HeroStats sourceHero, HeroStats targetHero, DjinnInstance targetDjinn)
    {
        if (sourceHero == null || targetHero == null || sourceHero == targetHero) return false;

        if (targetDjinn != null)
        {
            return true;
        }
        else
        {
            int minPartyCount = GetPartyMinDjinnCount();
            int sourceCount = GetHeroDjinnCount(sourceHero);
            return sourceCount > minPartyCount;
        }
    }

    private void HighlightDropTargets()
    {
        int minPartyCount = GetPartyMinDjinnCount();
        int sourceHeroCount = GetHeroDjinnCount(swapSourceHero);

        foreach (var target in dragTargetSlots)
        {
            if (currentDragContext != null && target.element == currentDragContext.SourceCardUI) continue;

            bool isValid = false;

            if (target.djinnInstance != null)
            {
                isValid = (target.ownerHero != swapSourceHero);
            }
            else
            {
                isValid = (target.ownerHero != swapSourceHero) && (sourceHeroCount > minPartyCount);
            }

            if (isValid)
            {
                target.element.AddToClassList("djinn-slot-drop-valid");
            }
            else
            {
                target.element.AddToClassList("djinn-slot-drop-invalid");
            }
        }
    }

    private void ClearDropHighlights()
    {
        foreach (var target in dragTargetSlots)
        {
            if (target.element != null)
            {
                target.element.RemoveFromClassList("djinn-slot-drop-valid");
                target.element.RemoveFromClassList("djinn-slot-drop-invalid");
            }
        }
    }

    private int GetHeroDjinnCount(HeroStats hero)
    {
        if (hero == null) return 0;
        HeroDjinnController ctrl = hero.GetComponent<HeroDjinnController>();
        return (ctrl != null && ctrl.AssignedDjinns != null) ? ctrl.AssignedDjinns.Count : 0;
    }

    private int GetPartyMinDjinnCount()
    {
        if (partyHeroes == null || partyHeroes.Count == 0) return 0;
        int min = int.MaxValue;
        foreach (var h in partyHeroes)
        {
            int c = GetHeroDjinnCount(h);
            if (c < min) min = c;
        }
        return min;
    }

    #endregion

    #region Ghost Element Management

    private void CreateGhostElement(Sprite icon, Vector2 initialPanelPosition)
    {
        DestroyGhostVisual();

        float width = 200f;
        float height = 48f;

        if (currentDragContext?.SourceCardUI != null && currentDragContext.SourceCardUI.resolvedStyle.width > 0)
        {
            width = currentDragContext.SourceCardUI.resolvedStyle.width;
            height = currentDragContext.SourceCardUI.resolvedStyle.height;
        }

        ghostElement = djinnSlotCardBAsset.Instantiate();
        ghostElement.name = "djinn_drag_ghost_element";
        ghostElement.AddToClassList("djinn-drag-ghost");
        ghostElement.pickingMode = PickingMode.Ignore;
        ghostElement.Query<VisualElement>().ForEach(elem => elem.pickingMode = PickingMode.Ignore);

        ghostElement.style.position = Position.Absolute;
        ghostElement.style.width = width;
        ghostElement.style.height = height;

        VisualElement ghostRoot = ghostElement.Q<VisualElement>("djinn_slot_card_b_root");
        if (ghostRoot != null)
        {
            ghostRoot.pickingMode = PickingMode.Ignore;
            if (currentDragContext?.SourceDjinn?.DjinnData != null)
            {
                ghostRoot.AddToClassList($"element-{currentDragContext.SourceDjinn.DjinnData.Element.ToString().ToLower()}");
                VisualElement iconElem = ghostRoot.Q<VisualElement>("djinn_card_icon");
                Label cardNameLabel = ghostRoot.Q<Label>("label_djinn_card_name");

                if (iconElem != null)
                {
                    iconElem.pickingMode = PickingMode.Ignore;
                    if (icon != null) iconElem.style.backgroundImage = new StyleBackground(icon);
                }
                if (cardNameLabel != null)
                {
                    cardNameLabel.pickingMode = PickingMode.Ignore;
                    cardNameLabel.text = currentDragContext.SourceDjinn.DjinnData.DjinnName;
                }
            }
        }

        VisualElement topRoot = GetTopRootElement();
        if (topRoot != null)
        {
            topRoot.Add(ghostElement);

            /*Debug.Log($"<color=orange>📐 [GHOST INIT DIAGNOSTIC]:</color>\n" +
                      $"• TopRoot Name: '{topRoot.name}'\n" +
                      $"• TopRoot WorldBounds: {topRoot.worldBound}\n" +
                      $"• Initial PanelPos: {initialPanelPosition}");*/

            UpdateGhostPosition(initialPanelPosition);
        }
    }

    private void UpdateGhostPosition(Vector2 panelPosition)
    {
        if (ghostElement == null) return;

        float width = ghostElement.resolvedStyle.width > 0 ? ghostElement.resolvedStyle.width : 200f;
        float height = ghostElement.resolvedStyle.height > 0 ? ghostElement.resolvedStyle.height : 48f;

        // Fijamos estilos base limpios sin desfasar el Layout nativo
        ghostElement.style.left = 0;
        ghostElement.style.top = 0;

        // Posicionamiento absoluto sobre la matriz con transform.position
        Vector3 targetPos = new Vector3(panelPosition.x - (width / 2f), panelPosition.y - (height / 2f), 0f);
        ghostElement.transform.position = targetPos;

        /* DIAGNÓSTICO EN TIEMPO REAL:
        Debug.Log($"<color=yellow>🧪 [GHOST LIVE TRACKING]:</color> " +
                  $"PanelPointer={panelPosition} | GhostTransform={targetPos}");*/
    }

    private void DestroyGhostVisual()
    {
        if (ghostElement != null)
        {
            ghostElement.RemoveFromHierarchy();
            ghostElement = null;
        }
    }

    private void RemoveGhostElement()
    {
        ClearDropHighlights();
        DestroyGhostVisual();

        if (currentDragContext?.SourceCardUI != null)
        {
            currentDragContext.SourceCardUI.RemoveFromClassList("djinn-card-disabled");
        }

        isDragging = false;
        currentDragContext = null;
        activePointerId = -1;
    }

    #endregion

    #region Modal Confirmation & Data Exchange Logic

    private void OpenSwapConfirmationModal()
    {
        Debug.Log("ENTER OpenSwapConfirmationModal");
        if (modalOverlayCompare == null)
        {
            ConfirmDjinnExchange();
            return;
        }

        modalOverlayCompare.style.display = DisplayStyle.Flex;

        Button btnConfirm = modalOverlayCompare.Q<Button>("btn_modal_confirm");
        Button btnCancel = modalOverlayCompare.Q<Button>("btn_modal_cancel");

        if (btnConfirm != null)
        {
            btnConfirm.clicked -= OnModalConfirmClicked;
            btnConfirm.clicked += OnModalConfirmClicked;
        }

        if (btnCancel != null)
        {
            btnCancel.clicked -= OnModalCancelClicked;
            btnCancel.clicked += OnModalCancelClicked;
        }

        PopulateModalComparisonData();
    }

    private void OnModalConfirmClicked()
    {
        ConfirmDjinnExchange();
        CloseSwapConfirmationModal();
    }

    private void OnModalCancelClicked()
    {
        CloseSwapConfirmationModal();
    }

    private void CloseSwapConfirmationModal()
    {
        if (modalOverlayCompare != null)
        {
            modalOverlayCompare.style.display = DisplayStyle.None;
        }

        swapSourceDjinn = null;
        swapSourceHero = null;
        swapTargetDjinn = null;
        swapTargetHero = null;
    }

    private void PopulateModalComparisonData()
    {
        // TODO: Proyecciones en modal
    }

    private void ConfirmDjinnExchange()
    {
        Debug.Log("Djinn Exchange Confirmed");
        // TODO: Intercambio definitivo en HeroDjinnController

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
        // TODO: Actualizar santuario 3D
    }

    #endregion
}