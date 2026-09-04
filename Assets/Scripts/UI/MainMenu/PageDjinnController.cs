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

    private VisualElement djinnSummarySection;
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

    private Label labelPowVenus;
    private Label labelResVenus;
    private Label labelPowMars;
    private Label labelResMars;
    private Label labelPowJupiter;
    private Label labelResJupiter;
    private Label labelPowMercury;
    private Label labelResMercury;

    private ScrollView scrollClassPsynergies;

    #endregion

    #region Private Fields - Center Panel Elements

    private Label labelGridHeader;
    private VisualElement djinnSlotsGrid;
    private VisualElement djinn3dRenderTarget;
    private Button btnSwitchToVistaB;

    #endregion

    #region Private Fields - Right Panel Elements

    private VisualElement djinnInfoPanel;

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

    private Label labelSetPowVenus;
    private Label labelSetResVenus;
    private Label labelSetPowMars;
    private Label labelSetResMars;
    private Label labelSetPowJupiter;
    private Label labelSetResJupiter;
    private Label labelSetPowMercury;
    private Label labelSetResMercury;

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

    #region Private Structs - Modal Panel Binding Structures

    private struct ModalHeroPanelUI
    {
        public VisualElement PanelRoot;
        public VisualElement AvatarIcon;
        public Label LabelName;
        public Label LabelLevel;
        public Label LabelClassCurrent;
        public Label LabelClassNext;
        public Label LabelClassArrow;

        public Label LabelPipVenus;
        public Label LabelPipMars;
        public Label LabelPipJupiter;
        public Label LabelPipMercury;

        public Label LabelStatMaxHP;
        public Label LabelStatMaxPP;
        public Label LabelStatAtq;
        public Label LabelStatDef;
        public Label LabelStatAgi;
        public Label LabelStatSue;

        public Label labelPowVenusB;
        public Label labelResVenusB;
        public Label labelPowMarsB;
        public Label labelResMarsB;
        public Label labelPowJupiterB;
        public Label labelResJupiterB;
        public Label labelPowMercuryB;
        public Label labelResMercuryB;

        public Label labelDeltaPowVenusB;
        public Label labelDeltaResVenusB;
        public Label labelDeltaPowMarsB;
        public Label labelDeltaResMarsB;
        public Label labelDeltaPowJupiterB;
        public Label labelDeltaResJupiterB;
        public Label labelDeltaPowMercuryB;
        public Label labelDeltaResMercuryB;

        public Label LabelDeltaMaxHP;
        public Label LabelDeltaMaxPP;
        public Label LabelDeltaAtq;
        public Label LabelDeltaDef;
        public Label LabelDeltaAgi;
        public Label LabelDeltaSue;

        public VisualElement PsynergyTabs;
        public Button BtnFilterAll;
        public Button BtnFilterGained;
        public Button BtnFilterLost;
        public ScrollView ScrollPsynergies;
    }

    #endregion

    #region Private Fields - Modal Overlay Elements

    private VisualElement modalHeroLeftRoot;
    private VisualElement modalHeroRightRoot;

    private ModalHeroPanelUI modalLeftUI;
    private ModalHeroPanelUI modalRightUI;

    // Center Action Box Elements
    private VisualElement compareCenterActionBox;
    private Label labelCompareTitle;
    private VisualElement compareExchangeVisual;
    private VisualElement pillSourceDjinn;
    private Label labelSourceDjinnName;
    private Label labelExchangeArrow;
    private VisualElement pillTargetDjinn;
    private Label labelTargetDjinnName;

    private Button btnModalConfirm;
    private Button btnModalCancel;

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
            djinnSummarySection = rootViewA.Q<VisualElement>("djinn_summary_section");
            djinnInfoPanel = rootViewA.Q<VisualElement>("djinn_info_panel");

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

            labelPowVenus = djinnSummarySection.Q<Label>("elem_power_venus");
            labelResVenus = djinnSummarySection.Q<Label>("elem_resistance_venus");
            labelPowMars = djinnSummarySection.Q<Label>("elem_power_mars");
            labelResMars = djinnSummarySection.Q<Label>("elem_resistance_mars");
            labelPowJupiter = djinnSummarySection.Q<Label>("elem_power_jupiter");
            labelResJupiter = djinnSummarySection.Q<Label>("elem_resistance_jupiter");
            labelPowMercury = djinnSummarySection.Q<Label>("elem_power_mercury");
            labelResMercury = djinnSummarySection.Q<Label>("elem_resistance_mercury");

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

            labelSetBonusMaxHP = djinnInfoPanel.Q<Label>("label_stat_max_hp");
            labelSetBonusMaxPP = djinnInfoPanel.Q<Label>("label_stat_max_pp");
            labelSetBonusAtq = djinnInfoPanel.Q<Label>("label_stat_atq");
            labelSetBonusDef = djinnInfoPanel.Q<Label>("label_stat_def");
            labelSetBonusAgi = djinnInfoPanel.Q<Label>("label_stat_agi");
            labelSetBonusSue = djinnInfoPanel.Q<Label>("label_stat_sue");

            labelSetPowVenus = djinnInfoPanel.Q<Label>("elem_power_venus");
            labelSetResVenus = djinnInfoPanel.Q<Label>("elem_resistance_venus");
            labelSetPowMars = djinnInfoPanel.Q<Label>("elem_power_mars");
            labelSetResMars = djinnInfoPanel.Q<Label>("elem_resistance_mars");
            labelSetPowJupiter = djinnInfoPanel.Q<Label>("elem_power_jupiter");
            labelSetResJupiter = djinnInfoPanel.Q<Label>("elem_resistance_jupiter");
            labelSetPowMercury = djinnInfoPanel.Q<Label>("elem_power_mercury");
            labelSetResMercury = djinnInfoPanel.Q<Label>("elem_resistance_mercury");

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

            if (modalOverlayCompare != null)
            {
                modalHeroLeftRoot = modalOverlayCompare.Q<VisualElement>("modal_hero_left");
                if (modalHeroLeftRoot != null)
                {
                    modalLeftUI = BindModalHeroPanel(modalHeroLeftRoot, "_left");
                }

                modalHeroRightRoot = modalOverlayCompare.Q<VisualElement>("modal_hero_right");
                if (modalHeroRightRoot != null)
                {
                    modalRightUI = BindModalHeroPanel(modalHeroRightRoot, "_right");
                }

                compareCenterActionBox = modalOverlayCompare.Q<VisualElement>(className: "compare-center-action-box");
                labelCompareTitle = modalOverlayCompare.Q<Label>(className: "compare-action-title");
                compareExchangeVisual = modalOverlayCompare.Q<VisualElement>(className: "compare-exchange-visual");

                List<VisualElement> pills = modalOverlayCompare.Query<VisualElement>(className: "compare-djinn-pill").ToList();
                if (pills.Count >= 1)
                {
                    pillSourceDjinn = pills[0];
                    labelSourceDjinnName = pillSourceDjinn.Q<Label>(className: "compare-djinn-pill-name");
                }
                if (pills.Count >= 2)
                {
                    pillTargetDjinn = pills[1];
                    labelTargetDjinnName = pillTargetDjinn.Q<Label>(className: "compare-djinn-pill-name");
                }

                labelExchangeArrow = modalOverlayCompare.Q<Label>(className: "compare-exchange-arrow");

                btnModalConfirm = modalOverlayCompare.Q<Button>("btn_modal_confirm");
                btnModalCancel = modalOverlayCompare.Q<Button>("btn_modal_cancel");
            }
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

    private ModalHeroPanelUI BindModalHeroPanel(VisualElement panelRoot, string suffix)
    {
        ModalHeroPanelUI ui = new ModalHeroPanelUI();
        ui.PanelRoot = panelRoot;

        ui.AvatarIcon = panelRoot.Q<VisualElement>("hero_avatar_icon");
        ui.LabelName = panelRoot.Q<Label>("label_hero_name");
        ui.LabelLevel = panelRoot.Q<Label>("label_hero_level");
        ui.LabelClassCurrent = panelRoot.Q<Label>("label_hero_class_current");
        ui.LabelClassNext = panelRoot.Q<Label>("label_hero_class_next");
        ui.LabelClassArrow = panelRoot.Q<Label>("label_class_arrow");

        ui.LabelPipVenus = panelRoot.Q<Label>("label_pip_venus");
        ui.LabelPipMars = panelRoot.Q<Label>("label_pip_mars");
        ui.LabelPipJupiter = panelRoot.Q<Label>("label_pip_jupiter");
        ui.LabelPipMercury = panelRoot.Q<Label>("label_pip_mercury");

        ui.LabelStatMaxHP = panelRoot.Q<Label>("label_stat_max_hp");
        ui.LabelStatMaxPP = panelRoot.Q<Label>("label_stat_max_pp");
        ui.LabelStatAtq = panelRoot.Q<Label>("label_stat_atq");
        ui.LabelStatDef = panelRoot.Q<Label>("label_stat_def");
        ui.LabelStatAgi = panelRoot.Q<Label>("label_stat_agi");
        ui.LabelStatSue = panelRoot.Q<Label>("label_stat_sue");

        ui.labelPowVenusB = panelRoot.Q<Label>("elem_power_venus");
        ui.labelResVenusB = panelRoot.Q<Label>("elem_resistance_venus");
        ui.labelPowMarsB = panelRoot.Q<Label>("elem_power_mars");
        ui.labelResMarsB = panelRoot.Q<Label>("elem_resistance_mars");
        ui.labelPowJupiterB = panelRoot.Q<Label>("elem_power_jupiter");
        ui.labelResJupiterB = panelRoot.Q<Label>("elem_resistance_jupiter");
        ui.labelPowMercuryB = panelRoot.Q<Label>("elem_power_mercury");
        ui.labelResMercuryB = panelRoot.Q<Label>("elem_resistance_mercury");

        ui.labelDeltaPowVenusB = panelRoot.Q<Label>("elem_power_delta_venus");
        ui.labelDeltaResVenusB = panelRoot.Q<Label>("elem_resistance_delta_venus");
        ui.labelDeltaPowMarsB = panelRoot.Q<Label>("elem_power_delta_mars");
        ui.labelDeltaResMarsB = panelRoot.Q<Label>("elem_resistance_delta_mars");
        ui.labelDeltaPowJupiterB = panelRoot.Q<Label>("elem_power_delta_jupiter");
        ui.labelDeltaResJupiterB = panelRoot.Q<Label>("elem_resistance_delta_jupiter");
        ui.labelDeltaPowMercuryB = panelRoot.Q<Label>("elem_power_delta_mercury");
        ui.labelDeltaResMercuryB = panelRoot.Q<Label>("elem_resistance_delta_mercury");

        ui.LabelDeltaMaxHP = panelRoot.Q<Label>("label_delta_max_hp");
        ui.LabelDeltaMaxPP = panelRoot.Q<Label>("label_delta_max_pp");
        ui.LabelDeltaAtq = panelRoot.Q<Label>("label_delta_atq");
        ui.LabelDeltaDef = panelRoot.Q<Label>("label_delta_def");
        ui.LabelDeltaAgi = panelRoot.Q<Label>("label_delta_agi");
        ui.LabelDeltaSue = panelRoot.Q<Label>("label_delta_sue");

        ui.PsynergyTabs = panelRoot.Q<VisualElement>($"psynergy_compare_tabs{suffix}");
        ui.BtnFilterAll = panelRoot.Q<Button>($"btn_filter_all{suffix}");
        ui.BtnFilterGained = panelRoot.Q<Button>($"btn_filter_gained{suffix}");
        ui.BtnFilterLost = panelRoot.Q<Button>($"btn_filter_lost{suffix}");
        ui.ScrollPsynergies = panelRoot.Q<ScrollView>($"scroll_class_psynergies{suffix}");

        return ui;
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

        // Directly query live calculated core stats from HeroStats overrides.
        if (labelStatAtq != null) labelStatAtq.text = currentHero.Attack.ToString();
        if (labelStatDef != null) labelStatDef.text = currentHero.Defense.ToString();
        if (labelStatAgi != null) labelStatAgi.text = currentHero.Agility.ToString();
        if (labelStatSue != null) labelStatSue.text = currentHero.Luck.ToString();
        if (labelStatMaxHP != null) labelStatMaxHP.text = currentHero.MaxHp.ToString();
        if (labelStatMaxPP != null) labelStatMaxPP.text = currentHero.MaxPp.ToString();

        // Elemental affinity
        if (labelPowVenus != null) labelPowVenus.text = currentHero.PowerVenus.ToString();
        if (labelResVenus != null) labelResVenus.text = currentHero.ResistVenus.ToString();
        if (labelPowMars != null) labelPowMars.text = currentHero.PowerMars.ToString();
        if (labelResMars != null) labelResMars.text = currentHero.ResistMars.ToString();
        if (labelPowJupiter != null) labelPowJupiter.text = currentHero.PowerJupiter.ToString();
        if (labelResJupiter != null) labelResJupiter.text = currentHero.ResistJupiter.ToString();
        if (labelPowMercury != null) labelPowMercury.text = currentHero.PowerMercury.ToString();
        if (labelResMercury != null) labelResMercury.text = currentHero.ResistMercury.ToString();

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

    private void UpdateElementalPips(
        HeroDjinnController djinnCtrl,
        Label pVenus, Label pMars, Label pJupiter, Label pMercury,
        int dSetVenus = 0, int dSetMars = 0, int dSetJup = 0, int dSetMerc = 0,
        int dTotalVenus = 0, int dTotalMars = 0, int dTotalJup = 0, int dTotalMerc = 0)
    {
        if (djinnCtrl == null) return;

        SetPipLabelText(pVenus, djinnCtrl, ElementalType.Venus, dSetVenus, dTotalVenus);
        SetPipLabelText(pMars, djinnCtrl, ElementalType.Mars, dSetMars, dTotalMars);
        SetPipLabelText(pJupiter, djinnCtrl, ElementalType.Jupiter, dSetJup, dTotalJup);
        SetPipLabelText(pMercury, djinnCtrl, ElementalType.Mercury, dSetMerc, dTotalMerc);
    }

    private void SetPipLabelText(Label label, HeroDjinnController djinnCtrl, ElementalType element, int deltaSet, int deltaTotal)
    {
        if (label == null) return;

        int currentSet = djinnCtrl.GetSetCountByElement(element);
        int currentTotal = djinnCtrl.GetTotalCountByElement(element);

        int finalSet = Mathf.Max(0, currentSet + deltaSet);
        int finalTotal = Mathf.Max(0, currentTotal + deltaTotal);

        label.text = $"{finalSet}/{finalTotal}";
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

        // Checks if Leader Djinn defines at least one valid passive status effect application.
        bool hasLeaderPassive = false;
        if (data.LeaderPassiveEffects != null && data.LeaderPassiveEffects.Count > 0)
        {
            for (int i = 0; i < data.LeaderPassiveEffects.Count; i++)
            {
                if (data.LeaderPassiveEffects[i].StatusEffect != null)
                {
                    hasLeaderPassive = true;
                    break;
                }
            }
        }

        if (labelLeaderPassiveTitle != null)
        {
            labelLeaderPassiveTitle.text = hasLeaderPassive
                ? data.LeaderPassiveName
                : "Sin Pasiva";
        }

        if (labelLeaderPassiveDesc != null)
        {
            labelLeaderPassiveDesc.text = (hasLeaderPassive && !string.IsNullOrEmpty(data.LeaderPassiveDescription))
                ? data.LeaderPassiveDescription
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

        if (unleash.SecondaryEffects != null && unleash.SecondaryEffects.Count > 0)
        {
            for (int i = 0; i < unleash.SecondaryEffects.Count; i++)
            {
                StatusEffectApplication app = unleash.SecondaryEffects[i];
                StatusEffectSO status = app.StatusEffect;

                if (status == null) continue;

                string chancePrefix = (app.ApplicationChance < 1.0f) ? $" ({(int)(app.ApplicationChance * 100)}%)" : "";
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
        float maxHp = 0, maxPp = 0, atk = 0, def = 0, agi = 0, sue = 0,
            powVenus = 0, resVenus = 0, powMars = 0, resMars = 0,
            powJupiter = 0, resJupiter = 0, powMercury = 0, resMercury = 0;

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
                    case StatType.PowerVenus: powVenus += mod.value; break;
                    case StatType.ResistVenus: resVenus += mod.value; break;
                    case StatType.PowerMars: powMars += mod.value; break;
                    case StatType.ResistMars: resMars += mod.value; break;
                    case StatType.PowerJupiter: powJupiter += mod.value; break;
                    case StatType.ResistJupiter: resJupiter += mod.value; break;
                    case StatType.PowerMercury: powMercury += mod.value; break;
                    case StatType.ResistMercury: resMercury += mod.value; break;
                }
            }
        }

        if (labelSetBonusMaxHP != null) labelSetBonusMaxHP.text = formatBonus(maxHp, labelSetBonusMaxHP);
        if (labelSetBonusMaxPP != null) labelSetBonusMaxPP.text = formatBonus(maxPp, labelSetBonusMaxPP);
        if (labelSetBonusAtq != null) labelSetBonusAtq.text = formatBonus(atk, labelSetBonusAtq);
        if (labelSetBonusDef != null) labelSetBonusDef.text = formatBonus(def, labelSetBonusDef);
        if (labelSetBonusAgi != null) labelSetBonusAgi.text = formatBonus(agi, labelSetBonusAgi);
        if (labelSetBonusSue != null) labelSetBonusSue.text = formatBonus(sue, labelSetBonusSue);

        if (labelSetPowVenus != null) labelSetPowVenus.text = formatBonus(powVenus, labelSetPowVenus);
        if (labelSetResVenus != null) labelSetResVenus.text = formatBonus(resVenus, labelSetResVenus);
        if (labelSetPowMars != null) labelSetPowMars.text = formatBonus(powMars, labelSetPowMars);
        if (labelSetResMars != null) labelSetResMars.text = formatBonus(resMars, labelSetResMars);
        if (labelSetPowJupiter != null) labelSetPowJupiter.text = formatBonus(powJupiter, labelSetPowJupiter);
        if (labelSetResJupiter != null) labelSetResJupiter.text = formatBonus(resJupiter, labelSetResJupiter);
        if (labelSetPowMercury != null) labelSetPowMercury.text = formatBonus(powMercury, labelSetPowMercury);
        if (labelSetResMercury != null) labelSetResMercury.text = formatBonus(resMercury, labelSetResMercury);
    }

    private string formatBonus(float val, Label setBonusLabel)
    {
        if (val > 0)
        {
            setBonusLabel.style.color = new StyleColor(Color.green);
            return $"+{Mathf.FloorToInt(val)}";
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
                    statusLabel.text = "GANADA";
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
                    statusLabel.text = "PERDIDA";
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

                cardRoot.pickingMode = PickingMode.Position;
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
            bool validDrop = CanDropOnTarget(currentDragContext.SourceHero, targetInfo.TargetHero, targetInfo.TargetDjinn);

            if (validDrop)
            {
                swapSourceHero = currentDragContext.SourceHero;
                swapSourceDjinn = currentDragContext.SourceDjinn;
                swapTargetHero = targetInfo.TargetHero;
                swapTargetDjinn = targetInfo.TargetDjinn;

                OpenSwapConfirmationModal();
            }
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
                Rect cardPanelBounds = topRoot != null
                    ? topRoot.WorldToLocal(target.element.worldBound)
                    : target.element.worldBound;

                if (cardPanelBounds.Contains(panelPointerPosition))
                {
                    targetInfo = new DjinnDropTargetInfo(target.ownerHero, target.djinnInstance, target.element);
                    return true;
                }
            }
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
            UpdateGhostPosition(initialPanelPosition);
        }
    }

    private void UpdateGhostPosition(Vector2 panelPosition)
    {
        if (ghostElement == null) return;

        float width = ghostElement.resolvedStyle.width > 0 ? ghostElement.resolvedStyle.width : 200f;
        float height = ghostElement.resolvedStyle.height > 0 ? ghostElement.resolvedStyle.height : 48f;

        ghostElement.style.left = 0;
        ghostElement.style.top = 0;

        Vector3 targetPos = new Vector3(panelPosition.x - (width / 2f), panelPosition.y - (height / 2f), 0f);

        // Unity 6 UI Toolkit Standard
        ghostElement.style.translate = new Translate(targetPos.x, targetPos.y, 0f);
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

    #region Modal Confirmation

    private void OpenSwapConfirmationModal()
    {
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

    private void ConfirmDjinnExchange()
    {
        if (swapSourceHero == null || swapTargetHero == null || swapSourceDjinn == null)
        {
            return;
        }

        HeroDjinnController sourceCtrl = swapSourceHero.GetComponent<HeroDjinnController>();
        HeroDjinnController targetCtrl = swapTargetHero.GetComponent<HeroDjinnController>();

        if (sourceCtrl == null || targetCtrl == null) return;

        bool isSwap = (swapTargetDjinn != null);

        DjinnSO sourceData = swapSourceDjinn.DjinnData;
        DjinnState sourceState = swapSourceDjinn.CurrentState;

        DjinnSO targetData = isSwap ? swapTargetDjinn.DjinnData : null;
        DjinnState targetState = isSwap ? swapTargetDjinn.CurrentState : DjinnState.Set;

        if (sourceCtrl.UnassignDjinn(swapSourceDjinn))
        {
            if (isSwap)
            {
                targetCtrl.UnassignDjinn(swapTargetDjinn);
            }

            targetCtrl.AssignDjinn(sourceData, sourceState);

            if (isSwap && targetData != null)
            {
                sourceCtrl.AssignDjinn(targetData, targetState);
            }
        }

        RefreshAllData();
    }

    #endregion

    #region Modal Comparison Data Population

    private void PopulateModalComparisonData()
    {
        if (swapSourceHero == null || swapTargetHero == null) return;

        bool isSwap = (swapTargetDjinn != null);

        // 1. CENTER PANEL
        if (labelCompareTitle != null)
        {
            labelCompareTitle.text = isSwap ? "INTERCAMBIO" : "TRANSFERENCIA";
        }

        if (pillSourceDjinn != null && swapSourceDjinn?.DjinnData != null)
        {
            pillSourceDjinn.style.display = DisplayStyle.Flex;
            pillSourceDjinn.RemoveFromClassList("element-venus");
            pillSourceDjinn.RemoveFromClassList("element-mars");
            pillSourceDjinn.RemoveFromClassList("element-jupiter");
            pillSourceDjinn.RemoveFromClassList("element-mercury");
            pillSourceDjinn.AddToClassList($"element-{swapSourceDjinn.DjinnData.Element.ToString().ToLower()}");

            if (labelSourceDjinnName != null)
            {
                labelSourceDjinnName.text = swapSourceDjinn.DjinnData.DjinnName;
            }
        }

        if (pillTargetDjinn != null)
        {
            if (isSwap && swapTargetDjinn?.DjinnData != null)
            {
                pillTargetDjinn.style.display = DisplayStyle.Flex;
                pillTargetDjinn.RemoveFromClassList("element-venus");
                pillTargetDjinn.RemoveFromClassList("element-mars");
                pillTargetDjinn.RemoveFromClassList("element-jupiter");
                pillTargetDjinn.RemoveFromClassList("element-mercury");
                pillTargetDjinn.AddToClassList($"element-{swapTargetDjinn.DjinnData.Element.ToString().ToLower()}");

                if (labelTargetDjinnName != null)
                {
                    labelTargetDjinnName.text = swapTargetDjinn.DjinnData.DjinnName;
                }
            }
            else
            {
                pillTargetDjinn.style.display = DisplayStyle.None;
            }
        }

        if (labelExchangeArrow != null)
        {
            labelExchangeArrow.text = isSwap ? "⇄" : "➔";
        }

        // 2. BASE DATA (SOURCE AND TARGET HEROES)
        PopulateModalHeroBaseData(swapSourceHero, modalLeftUI);
        PopulateModalHeroBaseData(swapTargetHero, modalRightUI);

        // 3. CALCULATE AND PROJECT DELTAS AND PSYNERGIES
        ApplyHeroProjection(swapSourceHero, swapSourceDjinn, swapTargetDjinn, modalLeftUI);
        ApplyHeroProjection(swapTargetHero, swapTargetDjinn, swapSourceDjinn, modalRightUI);
    }

    private void PopulateModalHeroBaseData(HeroStats hero, ModalHeroPanelUI ui)
    {
        if (hero == null || ui.PanelRoot == null) return;

        if (ui.AvatarIcon != null) ui.AvatarIcon.style.backgroundImage = new StyleBackground(hero.TurnBarImage);
        if (ui.LabelName != null) ui.LabelName.text = hero.Name;
        if (ui.LabelLevel != null) ui.LabelLevel.text = $"Nv. {hero.Level}";

        HeroDjinnController djinnCtrl = hero.GetComponent<HeroDjinnController>();
        if (ui.LabelClassCurrent != null && djinnCtrl != null && djinnCtrl.CurrentClass != null)
        {
            ui.LabelClassCurrent.text = djinnCtrl.CurrentClass.ClassName;
        }

        if (ui.LabelClassNext != null) ui.LabelClassNext.style.display = DisplayStyle.None;
        if (ui.LabelClassArrow != null) ui.LabelClassArrow.style.display = DisplayStyle.None;

        UpdateElementalPips(djinnCtrl, ui.LabelPipVenus, ui.LabelPipMars, ui.LabelPipJupiter, ui.LabelPipMercury);

        // English comment: Query live calculated core stats from HeroStats overrides.
        if (ui.LabelStatMaxHP != null) ui.LabelStatMaxHP.text = hero.MaxHp.ToString();
        if (ui.LabelStatMaxPP != null) ui.LabelStatMaxPP.text = hero.MaxPp.ToString();
        if (ui.LabelStatAtq != null) ui.LabelStatAtq.text = hero.Attack.ToString();
        if (ui.LabelStatDef != null) ui.LabelStatDef.text = hero.Defense.ToString();
        if (ui.LabelStatAgi != null) ui.LabelStatAgi.text = hero.Agility.ToString();
        if (ui.LabelStatSue != null) ui.LabelStatSue.text = hero.Luck.ToString();

        if (ui.labelPowVenusB != null) ui.labelPowVenusB.text = hero.PowerVenus.ToString();
        if (ui.labelResVenusB != null) ui.labelResVenusB.text = hero.ResistVenus.ToString();
        if (ui.labelPowMarsB != null) ui.labelPowMarsB.text = hero.PowerMars.ToString();
        if (ui.labelResMarsB != null) ui.labelResMarsB.text = hero.ResistMars.ToString();
        if (ui.labelPowJupiterB != null) ui.labelPowJupiterB.text = hero.PowerJupiter.ToString();
        if (ui.labelResJupiterB != null) ui.labelResJupiterB.text = hero.ResistJupiter.ToString();
        if (ui.labelPowMercuryB != null) ui.labelPowMercuryB.text = hero.PowerMercury.ToString();
        if (ui.labelResMercuryB != null) ui.labelResMercuryB.text = hero.ResistMercury.ToString();

        HideModalDeltas(ui);
    }

    private void HideModalDeltas(ModalHeroPanelUI ui)
    {
        if (ui.LabelDeltaMaxHP != null) ui.LabelDeltaMaxHP.style.display = DisplayStyle.None;
        if (ui.LabelDeltaMaxPP != null) ui.LabelDeltaMaxPP.style.display = DisplayStyle.None;
        if (ui.LabelDeltaAtq != null) ui.LabelDeltaAtq.style.display = DisplayStyle.None;
        if (ui.LabelDeltaDef != null) ui.LabelDeltaDef.style.display = DisplayStyle.None;
        if (ui.LabelDeltaAgi != null) ui.LabelDeltaAgi.style.display = DisplayStyle.None;
        if (ui.LabelDeltaSue != null) ui.LabelDeltaSue.style.display = DisplayStyle.None;

        //Elemental affinity grid
        if (ui.labelDeltaPowVenusB != null) ui.labelDeltaPowVenusB.style.display = DisplayStyle.None;
        if (ui.labelDeltaResVenusB != null) ui.labelDeltaResVenusB.style.display = DisplayStyle.None;
        if (ui.labelDeltaPowMarsB != null) ui.labelDeltaPowMarsB.style.display = DisplayStyle.None;
        if (ui.labelDeltaResMarsB != null) ui.labelDeltaResMarsB.style.display = DisplayStyle.None;
        if (ui.labelDeltaPowJupiterB != null) ui.labelDeltaPowJupiterB.style.display = DisplayStyle.None;
        if (ui.labelDeltaResJupiterB != null) ui.labelDeltaResJupiterB.style.display = DisplayStyle.None;
        if (ui.labelDeltaPowMercuryB != null) ui.labelDeltaPowMercuryB.style.display = DisplayStyle.None;
        if (ui.labelDeltaResMercuryB != null) ui.labelDeltaResMercuryB.style.display = DisplayStyle.None;
    }

    private void ApplyHeroProjection(HeroStats hero, DjinnInstance djinnToRemove, DjinnInstance djinnToAdd, ModalHeroPanelUI ui)
    {
        if (hero == null || ui.PanelRoot == null) return;

        HeroDjinnController djinnCtrl = hero.GetComponent<HeroDjinnController>();
        if (djinnCtrl == null) return;

        bool removeIsSet = (djinnToRemove != null && djinnToRemove.CurrentState == DjinnState.Set);
        bool addIsSet = (djinnToAdd != null && djinnToAdd.CurrentState == DjinnState.Set);

        // 1. Project new class
        CharacterClassSO currentClassSO = djinnCtrl.CurrentClass;
        CharacterClassSO projectedClassSO = GetProjectedClass(djinnCtrl, djinnToRemove, removeIsSet, djinnToAdd, addIsSet);

        if (projectedClassSO != null && projectedClassSO != currentClassSO)
        {
            if (ui.LabelClassNext != null)
            {
                ui.LabelClassNext.text = projectedClassSO.ClassName;
                ui.LabelClassNext.style.display = DisplayStyle.Flex;
            }
            if (ui.LabelClassArrow != null)
            {
                ui.LabelClassArrow.style.display = DisplayStyle.Flex;
            }
        }
        else
        {
            if (ui.LabelClassNext != null) ui.LabelClassNext.style.display = DisplayStyle.None;
            if (ui.LabelClassArrow != null) ui.LabelClassArrow.style.display = DisplayStyle.None;
        }

        // 2. Project Elemental Pips
        int dSetVenus = (addIsSet && djinnToAdd.DjinnData.Element == ElementalType.Venus ? 1 : 0) - (removeIsSet && djinnToRemove.DjinnData.Element == ElementalType.Venus ? 1 : 0);
        int dSetMars = (addIsSet && djinnToAdd.DjinnData.Element == ElementalType.Mars ? 1 : 0) - (removeIsSet && djinnToRemove.DjinnData.Element == ElementalType.Mars ? 1 : 0);
        int dSetJup = (addIsSet && djinnToAdd.DjinnData.Element == ElementalType.Jupiter ? 1 : 0) - (removeIsSet && djinnToRemove.DjinnData.Element == ElementalType.Jupiter ? 1 : 0);
        int dSetMerc = (addIsSet && djinnToAdd.DjinnData.Element == ElementalType.Mercury ? 1 : 0) - (removeIsSet && djinnToRemove.DjinnData.Element == ElementalType.Mercury ? 1 : 0);

        int dTotalVenus = (djinnToAdd?.DjinnData.Element == ElementalType.Venus ? 1 : 0) - (djinnToRemove?.DjinnData.Element == ElementalType.Venus ? 1 : 0);
        int dTotalMars = (djinnToAdd?.DjinnData.Element == ElementalType.Mars ? 1 : 0) - (djinnToRemove?.DjinnData.Element == ElementalType.Mars ? 1 : 0);
        int dTotalJup = (djinnToAdd?.DjinnData.Element == ElementalType.Jupiter ? 1 : 0) - (djinnToRemove?.DjinnData.Element == ElementalType.Jupiter ? 1 : 0);
        int dTotalMerc = (djinnToAdd?.DjinnData.Element == ElementalType.Mercury ? 1 : 0) - (djinnToRemove?.DjinnData.Element == ElementalType.Mercury ? 1 : 0);

        UpdateElementalPips(
            djinnCtrl,
            ui.LabelPipVenus, ui.LabelPipMars, ui.LabelPipJupiter, ui.LabelPipMercury,
            dSetVenus, dSetMars, dSetJup, dSetMerc,
            dTotalVenus, dTotalMars, dTotalJup, dTotalMerc
        );

        // 3. Project Statistical Attributes via StatCalculator API
        StatBlock projectedStats = CalculateProjectedStatsViaCalculator(hero, djinnCtrl, projectedClassSO, djinnToRemove, djinnToAdd);

        ApplyStatProjection(ui.LabelStatMaxHP, ui.LabelDeltaMaxHP, hero.MaxHp, projectedStats.MaxHP);
        ApplyStatProjection(ui.LabelStatMaxPP, ui.LabelDeltaMaxPP, hero.MaxPp, projectedStats.MaxPP);
        ApplyStatProjection(ui.LabelStatAtq, ui.LabelDeltaAtq, hero.Attack, projectedStats.Attack);
        ApplyStatProjection(ui.LabelStatDef, ui.LabelDeltaDef, hero.Defense, projectedStats.Defense);
        ApplyStatProjection(ui.LabelStatAgi, ui.LabelDeltaAgi, hero.Agility, projectedStats.Agility);
        ApplyStatProjection(ui.LabelStatSue, ui.LabelDeltaSue, hero.Luck, projectedStats.Luck);
        // 3.1 Elemental power/resistance
        ApplyStatProjection(ui.labelPowVenusB, ui.labelDeltaPowVenusB, hero.PowerVenus, projectedStats.PowerVenus);
        ApplyStatProjection(ui.labelResVenusB, ui.labelDeltaResVenusB, hero.ResistVenus, projectedStats.ResistVenus);
        ApplyStatProjection(ui.labelPowMarsB, ui.labelDeltaPowMarsB, hero.PowerMars, projectedStats.PowerMars);
        ApplyStatProjection(ui.labelResMarsB, ui.labelDeltaResMarsB, hero.ResistMars, projectedStats.ResistMars);
        ApplyStatProjection(ui.labelPowJupiterB, ui.labelDeltaPowJupiterB, hero.PowerJupiter, projectedStats.PowerJupiter);
        ApplyStatProjection(ui.labelResJupiterB, ui.labelDeltaResJupiterB, hero.ResistJupiter, projectedStats.ResistJupiter);
        ApplyStatProjection(ui.labelPowMercuryB, ui.labelDeltaPowMercuryB, hero.PowerMercury, projectedStats.PowerMercury);
        ApplyStatProjection(ui.labelResMercuryB, ui.labelDeltaResMercuryB, hero.ResistMercury, projectedStats.ResistMercury);

        // 4. Project Psynergies
        List<PsynergySO> currentPsynergies = hero.Psynergies != null ? new List<PsynergySO>(hero.Psynergies) : new List<PsynergySO>();
        List<PsynergySO> projectedPsynergies = GetProjectedConsolidatedPsynergies(hero, projectedClassSO);

        List<PsynergySO> gained = new List<PsynergySO>();
        List<PsynergySO> lost = new List<PsynergySO>();
        List<PsynergySO> kept = new List<PsynergySO>();

        foreach (var psy in currentPsynergies)
        {
            if (projectedPsynergies.Contains(psy))
            {
                kept.Add(psy);
            }
            else
            {
                lost.Add(psy);
            }
        }

        foreach (var psy in projectedPsynergies)
        {
            if (!currentPsynergies.Contains(psy))
            {
                gained.Add(psy);
            }
        }

        RefreshPsynergiesComparison(ui.ScrollPsynergies, gained, lost, kept);
    }

    private CharacterClassSO GetProjectedClass(HeroDjinnController djinnCtrl, DjinnInstance removeDjinn, bool removeIsSet, DjinnInstance addDjinn, bool addIsSet)
    {
        if (djinnCtrl == null) return null;

        int venusSet = djinnCtrl.GetSetCountByElement(ElementalType.Venus);
        int marsSet = djinnCtrl.GetSetCountByElement(ElementalType.Mars);
        int jupiterSet = djinnCtrl.GetSetCountByElement(ElementalType.Jupiter);
        int mercurySet = djinnCtrl.GetSetCountByElement(ElementalType.Mercury);

        if (removeIsSet && removeDjinn?.DjinnData != null)
        {
            switch (removeDjinn.DjinnData.Element)
            {
                case ElementalType.Venus: venusSet--; break;
                case ElementalType.Mars: marsSet--; break;
                case ElementalType.Jupiter: jupiterSet--; break;
                case ElementalType.Mercury: mercurySet--; break;
            }
        }

        if (addIsSet && addDjinn?.DjinnData != null)
        {
            switch (addDjinn.DjinnData.Element)
            {
                case ElementalType.Venus: venusSet++; break;
                case ElementalType.Mars: marsSet++; break;
                case ElementalType.Jupiter: jupiterSet++; break;
                case ElementalType.Mercury: mercurySet++; break;
            }
        }

        HeroStats hero = djinnCtrl.GetComponent<HeroStats>();
        ElementalType baseElement = hero != null ? hero.BaseElement : ElementalType.Venus;

        ClassDatabaseSO targetDatabase = djinnCtrl.ClassDatabase;

        return targetDatabase != null
            ? targetDatabase.EvaluateClass(baseElement, Mathf.Max(0, venusSet), Mathf.Max(0, marsSet), Mathf.Max(0, jupiterSet), Mathf.Max(0, mercurySet))
            : djinnCtrl.CurrentClass;
    }

    /// <summary>
    /// Builds a StatEvaluationContext with base stats, class multipliers, and simulated Djinn/Equipment/Leader modifiers,
    /// then passes it to StatCalculator.Evaluate to produce a projected StatBlock.
    /// </summary>
    private StatBlock CalculateProjectedStatsViaCalculator(
        HeroStats hero,
        HeroDjinnController djinnCtrl,
        CharacterClassSO projectedClass,
        DjinnInstance djinnToRemove,
        DjinnInstance djinnToAdd)
    {
        StatEvaluationContext context = new StatEvaluationContext();

        if (hero == null) return StatCalculator.Evaluate(context);

        // 1. Assign Base Stats (pure character base stats without modifiers across all Core & Elemental stats)
        foreach (StatType statType in Enum.GetValues(typeof(StatType)))
        {
            context.SetBaseStat(statType, hero.GetStat(statType)?.BaseValue ?? 0);
        }

        // 2. Assign Class Multipliers from projected class across all stats
        if (projectedClass != null)
        {
            foreach (StatType statType in Enum.GetValues(typeof(StatType)))
            {
                context.SetClassMultiplier(statType, projectedClass.GetStatMultiplier(statType));
            }
        }

        // 3. Build simulated list of assigned Djinns after transaction
        List<DjinnInstance> simulatedDjinns = new List<DjinnInstance>();
        if (djinnCtrl != null && djinnCtrl.AssignedDjinns != null)
        {
            foreach (var inst in djinnCtrl.AssignedDjinns)
            {
                if (inst != djinnToRemove)
                {
                    simulatedDjinns.Add(inst);
                }
            }
        }

        if (djinnToAdd != null)
        {
            simulatedDjinns.Add(djinnToAdd);
        }

        // 4. Collect Modifiers from Djinns in Set state
        foreach (var djinn in simulatedDjinns)
        {
            if (djinn != null && djinn.CurrentState == DjinnState.Set && djinn.DjinnData != null && djinn.DjinnData.SetStatModifiers != null)
            {
                foreach (var mod in djinn.DjinnData.SetStatModifiers)
                {
                    if (mod == null) continue;
                    context.AddModifier(mod.statType, new StatModifier(mod.value, mod.modifierType, djinn));
                }
            }
        }

        // 5. Collect Modifiers from Leader Passive Effects
        IReadOnlyList<StatusEffectApplication> simulatedLeaderEffects = SimulateLeaderPassiveApplications(djinnCtrl, djinnToRemove, simulatedDjinns);
        if (simulatedLeaderEffects != null)
        {
            foreach (var app in simulatedLeaderEffects)
            {
                if (app.StatusEffect == null || app.StatusEffect.StatModifiers == null) continue;

                foreach (var bonus in app.StatusEffect.StatModifiers)
                {
                    if (bonus == null) continue;
                    context.AddModifier(bonus.statType, new StatModifier(bonus.value, bonus.modifierType));
                }
            }
        }

        // 6. Collect Equipment Modifiers
        HeroEquipment equipment = hero.GetComponent<HeroEquipment>();
        if (equipment != null && equipment.EquippedItems != null)
        {
            foreach (var item in equipment.EquippedItems.Values)
            {
                if (item != null && item.StatModifiers != null)
                {
                    foreach (var mod in item.StatModifiers)
                    {
                        if (mod == null) continue;
                        context.AddModifier(mod.statType, new StatModifier(mod.value, mod.modifierType, item));
                    }
                }
            }
        }

        // Delegate to StatCalculator engine
        return StatCalculator.Evaluate(context);
    }

    /// <summary>
    /// Evaluates which Leader Djinn passive applications will be active in the projected state.
    /// </summary>
    private IReadOnlyList<StatusEffectApplication> SimulateLeaderPassiveApplications(
        HeroDjinnController djinnCtrl,
        DjinnInstance djinnToRemove,
        List<DjinnInstance> simulatedDjinns)
    {
        DjinnInstance currentLeader = djinnCtrl != null ? djinnCtrl.LeaderDjinn : null;
        DjinnSO projectedLeaderData = null;
        DjinnState projectedLeaderState = DjinnState.Set;

        if (currentLeader != null && currentLeader != djinnToRemove)
        {
            projectedLeaderData = currentLeader.DjinnData;
            projectedLeaderState = currentLeader.CurrentState;
        }
        else if (simulatedDjinns.Count > 0)
        {
            projectedLeaderData = simulatedDjinns[0].DjinnData;
            projectedLeaderState = simulatedDjinns[0].CurrentState;
        }

        if (projectedLeaderData != null && projectedLeaderState == DjinnState.Set && projectedLeaderData.LeaderPassiveEffects != null)
        {
            return projectedLeaderData.LeaderPassiveEffects;
        }

        return null;
    }

    private List<PsynergySO> GetProjectedConsolidatedPsynergies(HeroStats hero, CharacterClassSO projectedClass)
    {
        HashSet<PsynergySO> consolidatedSet = new HashSet<PsynergySO>();

        if (hero != null)
        {
            List<PsynergySO> innateAndStory = hero.GetInnateAndStoryPsynergies();
            foreach (var p in innateAndStory)
            {
                if (p != null) consolidatedSet.Add(p);
            }

            HeroEquipment equipment = hero.GetComponent<HeroEquipment>();
            if (equipment != null)
            {
                List<PsynergySO> relicPsynergies = equipment.GetEquippedRelicPsynergies();
                foreach (var p in relicPsynergies)
                {
                    if (p != null) consolidatedSet.Add(p);
                }
            }

            if (projectedClass != null && projectedClass.GrantedPsynergies != null)
            {
                IReadOnlyList<ClassPsynergyEntry> entries = projectedClass.GrantedPsynergies;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].psynergy != null && hero.Level >= entries[i].requiredLevel)
                    {
                        consolidatedSet.Add(entries[i].psynergy);
                    }
                }
            }
        }

        return new List<PsynergySO>(consolidatedSet);
    }

    private void ApplyStatProjection(Label labelStat, Label labelDelta, float currentVal, float projectedVal)
    {
        int currentInt = Mathf.FloorToInt(currentVal);
        int projectedInt = Mathf.FloorToInt(projectedVal);
        int delta = projectedInt - currentInt;

        if (labelStat != null)
        {
            labelStat.text = projectedInt.ToString();
        }

        if (labelDelta == null) return;

        if (delta != 0)
        {
            labelDelta.style.display = DisplayStyle.Flex;
            labelDelta.text = delta > 0 ? $"({delta:+0})" : $"({delta})";

            labelDelta.RemoveFromClassList("delta-positive");
            labelDelta.RemoveFromClassList("delta-negative");
            labelDelta.AddToClassList(delta > 0 ? "delta-positive" : "delta-negative");
        }
        else
        {
            labelDelta.style.display = DisplayStyle.None;
        }
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
        // TODO: Load RenderTarget/VideoPlayer
    }

    private void UpdateSanctuaryUI()
    {
        // TODO: Update 3D sanctuary
    }

    #endregion
}