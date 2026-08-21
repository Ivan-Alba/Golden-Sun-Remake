using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
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

    #endregion

    #region Private Fields - Containers & Views

    private VisualElement rootElement;

    // View Containers
    private VisualElement pageDjinnContainer;
    private VisualElement sectionVistaB;
    private VisualElement modalOverlayCompare;

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

    // Djinn Selected for Detail Display in Vista A
    private DjinnInstance selectedDjinnForDetails;

    // Djinn Swap Context (for Vista B & Modal)
    private DjinnInstance swapSourceDjinn;
    private HeroStats swapSourceHero;
    private DjinnInstance swapTargetDjinn;
    private HeroStats swapTargetHero;

    #endregion

    #region Constructor

    public PageDjinnController(VisualElement root)
    {
        rootElement = root;
        BindUIElements();
        BindEvents();
    }

    #endregion

    #region UI Binding

    private void BindUIElements()
    {
        if (rootElement == null) return;

        // 1. Root Containers
        pageDjinnContainer = rootElement.Q<VisualElement>("page_djinn_container");
        sectionVistaB = rootElement.Q<VisualElement>("djinn_global_section");
        modalOverlayCompare = rootElement.Q<VisualElement>("compare_modal_overlay");

        // 2. Left Panel Elements
        heroAvatarIcon = rootElement.Q<VisualElement>("hero_avatar_icon");
        labelHeroName = rootElement.Q<Label>("label_hero_name");
        labelHeroLevel = rootElement.Q<Label>("label_hero_level");
        labelHeroClassCurrent = rootElement.Q<Label>("label_hero_class_current");
        labelHeroClassNext = rootElement.Q<Label>("label_hero_class_next");
        labelClassArrow = rootElement.Q<Label>("label_class_arrow");

        labelPipVenus = rootElement.Q<Label>("label_pip_venus");
        labelPipMars = rootElement.Q<Label>("label_pip_mars");
        labelPipJupiter = rootElement.Q<Label>("label_pip_jupiter");
        labelPipMercury = rootElement.Q<Label>("label_pip_mercury");

        labelStatMaxHP = rootElement.Q<Label>("label_stat_max_hp");
        labelStatMaxPP = rootElement.Q<Label>("label_stat_max_pp");
        labelStatAtq = rootElement.Q<Label>("label_stat_atq");
        labelStatDef = rootElement.Q<Label>("label_stat_def");
        labelStatAgi = rootElement.Q<Label>("label_stat_agi");
        labelStatSue = rootElement.Q<Label>("label_stat_sue");

        labelDeltaAtq = rootElement.Q<Label>("label_delta_atq");
        labelDeltaDef = rootElement.Q<Label>("label_delta_def");
        labelDeltaAgi = rootElement.Q<Label>("label_delta_agi");
        labelDeltaSue = rootElement.Q<Label>("label_delta_sue");

        scrollClassPsynergies = rootElement.Q<ScrollView>("scroll_class_psynergies");

        // 3. Center Panel Elements
        labelGridHeader = rootElement.Q<Label>("label_grid_header");
        djinnSlotsGrid = rootElement.Q<VisualElement>("djinn_slots_grid");
        djinn3dRenderTarget = rootElement.Q<VisualElement>("djinn_3d_render_target");
        btnSwitchToVistaB = rootElement.Q<Button>("btn_switch_vista_b");

        // 4. Right Panel Elements
        djinnVideoPreview = rootElement.Q<VisualElement>("djinn_video_preview");
        labelDjinnName = rootElement.Q<Label>("label_djinn_name");
        labelDjinnType = rootElement.Q<Label>("label_djinn_type");
        labelDjinnStatus = rootElement.Q<Label>("label_djinn_status");
        labelDjinnRecovery = rootElement.Q<Label>("label_djinn_recovery");
        labelDjinnDesc = rootElement.Q<Label>("label_djinn_desc");

        labelSetBonusMaxHP = rootElement.Q<Label>("label_set_bonus_max_hp");
        labelSetBonusMaxPP = rootElement.Q<Label>("label_set_bonus_max_pp");
        labelSetBonusAtq = rootElement.Q<Label>("label_set_bonus_atq");
        labelSetBonusDef = rootElement.Q<Label>("label_set_bonus_def");
        labelSetBonusAgi = rootElement.Q<Label>("label_set_bonus_agi");
        labelSetBonusSue = rootElement.Q<Label>("label_set_bonus_sue");

        boxUnleashInfo = rootElement.Q<VisualElement>("box_unleash_info");
        labelUnleashName = rootElement.Q<Label>("label_unleash_name");
        labelUnleashDesc = rootElement.Q<Label>("label_unleash_desc");
        labelUnleashDamage = rootElement.Q<Label>("label_unleash_damage");
        labelUnleashEffect = rootElement.Q<Label>("label_unleash_effect");

        labelLeaderPassiveTitle = rootElement.Q<Label>("label_leader_passive_title");
        labelLeaderPassiveDesc = rootElement.Q<Label>("label_leader_passive_desc");

        if (modalOverlayCompare != null)
        {
            modalOverlayCompare.style.display = DisplayStyle.None;
        }
    }

    private void BindEvents()
    {
        if (btnSwitchToVistaB != null)
        {
            btnSwitchToVistaB.clicked += () => SwitchToView(DjinnViewMode.Global);
        }
    }

    #endregion

    #region Data Initialization & Tab Switch

    public void SetParty(List<HeroStats> party)
    {
        partyHeroes = party ?? new List<HeroStats>();
    }

    /// <summary>
    /// Assigns the UXML templates used for dynamic instantiation in the page.
    /// </summary>
    public void SetTemplates(VisualTreeAsset miniPsynergyCard, VisualTreeAsset djinnSlotCardA)
    {
        psynergyCardAsset = miniPsynergyCard;
        djinnSlotCardAAsset = djinnSlotCardA;
    }

    /// <summary>
    /// Assigns the target icons used for dynamic unleash badge instantiation.
    /// </summary>
    public void SetTargetIcons(Sprite singleTarget, Sprite groupTarget)
    {
        // English comment: Inject target icons provided by MainMenuController
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
        selectedDjinnForDetails = null; // Reiniciar selección al cambiar de personaje
        currentHeroDjinnController = (hero != null) ? hero.GetComponent<HeroDjinnController>() : null;

        if (currentHeroDjinnController != null)
        {
            currentHeroDjinnController.OnDjinnSetupUpdated += RefreshAllData;
        }

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
        UpdateElementalPips();

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

        // Mantener la selección activa o seleccionar el primer Djinn disponible
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

        // 5. Renderizar detalles del Djinn Seleccionado
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

    private void UpdateElementalPips()
    {
        if (currentHeroDjinnController == null) return;

        int vCount = 0, mCount = 0, jCount = 0, meCount = 0;
        int totalVCount = 0, totalMCount = 0, totalJCount = 0, totalMeCount = 0;

        foreach (var djinn in currentHeroDjinnController.AssignedDjinns)
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

        if (labelPipVenus != null) labelPipVenus.text = $"{vCount}/{totalVCount + vCount}";
        if (labelPipMars != null) labelPipMars.text = $"{mCount}/{totalMCount + mCount}";
        if (labelPipJupiter != null) labelPipJupiter.text = $"{jCount}/{totalJCount + jCount}";
        if (labelPipMercury != null) labelPipMercury.text = $"{meCount}/{totalMeCount + meCount}";
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

            // 1. Limpiar clases previas en el contenedor raíz
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

                // Mostrar el cuerpo y ocultar el texto de vacio
                if (cardHeader != null) cardHeader.style.display = DisplayStyle.Flex;
                if (cardBody != null) cardBody.style.display = DisplayStyle.Flex;
                if (emptyLabel != null) emptyLabel.style.display = DisplayStyle.None;

                // 2. Asignar elemento
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

                // 3. Comprobar Líder
                bool isLeader = (currentHeroDjinnController != null && currentHeroDjinnController.LeaderDjinn == djinnInstance);
                if (isLeader)
                {
                    cardRoot.AddToClassList("djinn-card-leader");
                }

                // 4. Comprobar Selección (Prevalece sobre el borde elemental)
                if (selectedDjinnForDetails == djinnInstance)
                {
                    cardRoot.AddToClassList("djinn-card-selected");
                }

                // 5. Configurar el Chip/Botón de Líder
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

                    // Cambiar el líder al hacer clic directamente sobre el chip
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

                // 6. Configurar Botón de Estado (Set, Standby, Recovery)
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

                // 7. Click en la tarjeta completa para seleccionarla
                slotInstance.RegisterCallback<ClickEvent>(evt =>
                {
                    selectedDjinnForDetails = djinnInstance;
                    RefreshVistaA();
                });
            }
            else
            {
                // Estado Vacío
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

        // =========================================================================
        // EFECTOS DE LIBERACIÓN Y BADGES DINÁMICAS
        // =========================================================================
        if (data.UnleashEffect != null)
        {
            DjinnUnleashSO unleash = data.UnleashEffect;

            if (labelUnleashName != null) labelUnleashName.text = unleash.UnleashName;
            if (labelUnleashDesc != null) labelUnleashDesc.text = unleash.Description;

            // Renderizar Badges Dinámicas
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

        // =========================================================================
        // PASIVA DE LÍDER
        // =========================================================================
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

    /// <summary>  
    /// Instancia dinámicamente las badges de Target, Daño y Status Effects según las propiedades de DjinnUnleashSO.  
    /// </summary>  
    private void RenderUnleashBadges(DjinnUnleashSO unleash)
    {
        if (boxUnleashInfo == null) return;

        boxUnleashInfo.Clear();

        // 1. Badge de TARGET / ALCANCE
        string targetText = GetTargetBadgeText(unleash.TargetsAlly, unleash.IsAreaOfEffect);
        Sprite targetIcon = GetTargetIcon(unleash.TargetsAlly, unleash.IsAreaOfEffect);

        VisualElement targetBadge = CreateBadgeContainer("badge-target", targetIcon, targetText);
        boxUnleashInfo.Add(targetBadge);

        // 2. Badge de DAÑO / POTENCIA  
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

        // 3. Badges de STATUS EFFECTS (Buffs, Debuffs, Ailments)  
        if (unleash.StatusEffects != null && unleash.StatusEffects.Count > 0)
        {
            foreach (StatusEffectSO status in unleash.StatusEffects)
            {
                if (status == null) continue;

                // Prefijo de Probabilidad (ej. "30% " si la probabilidad no es del 100%) 
                string chancePrefix = (unleash.StatusChance < 1.0f) ? $" {(int)(unleash.StatusChance * 100)}%" : "";

                string categoryPrefix = "";
                string categoryClass = "badge-target";
                Sprite effectIcon = null;

                switch (status.Category)
                {
                    case StatusEffectCategory.Buff:
                        categoryPrefix = "↑ ";
                        categoryClass = "badge-buff";
                        effectIcon = null; // En Buffs no mostramos icono, usaremos la flecha en texto
                        break;

                    case StatusEffectCategory.Debuff:
                        categoryPrefix = "↓ ";
                        categoryClass = "badge-debuff";
                        effectIcon = null; // En Debuffs no mostramos icono, usaremos la flecha en texto
                        break;

                    case StatusEffectCategory.Ailment:
                        categoryClass = "badge-ailment";
                        effectIcon = status.Icon; // Muestra el icono definido en el StatusEffectSO
                        break;
                }

                string fullText = $"{categoryPrefix}{status.EffectName}{chancePrefix}";
                VisualElement effectBadge = CreateBadgeContainer(categoryClass, effectIcon, fullText);

                boxUnleashInfo.Add(effectBadge);
            }
        }
    }

    /// <summary>
    /// Crea el contenedor modular de una Badge con su caja de icono y su etiqueta de texto.
    /// </summary>
    private VisualElement CreateBadgeContainer(string categoryClass, Sprite iconSprite, string text)
    {
        // English comment: Create parent badge container with flex layout
        VisualElement badgeContainer = new VisualElement();
        badgeContainer.AddToClassList("unleash-badge");
        badgeContainer.AddToClassList(categoryClass);

        // English comment: Create icon element and apply sprite background if provided
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

        // English comment: Create label element for badge text
        Label textLabel = new Label(text);

        badgeContainer.Add(iconElem);
        badgeContainer.Add(textLabel);

        return badgeContainer;
    }

    /// <summary>
    /// Devuelve el Sprite correspondiente para la badge de objetivo según el ámbito de la habilidad.
    /// </summary>
    private Sprite GetTargetIcon(bool targetsAlly, bool isAreaOfEffect)
    {
        // English comment: Return the corresponding target icon based on alliance and AoE status
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

        //string formatBonus(float val) => (val >= 0) ? $"+{Mathf.RoundToInt(val)}" : $"{Mathf.RoundToInt(val)}";
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

    /// <summary>
    /// Creates and populates a single Psynergy card instance based on the provided state.
    /// </summary>
    private TemplateContainer CreatePsynergyCardInstance(PsynergySO psynergy, PsynergyCardState state, int requiredLevel = 0)
    {
        if (psynergyCardAsset == null) return null;

        TemplateContainer cardInstance = psynergyCardAsset.Instantiate();
        VisualElement cardRoot = cardInstance.Q<VisualElement>("mini_psy_card_root");
        if (cardRoot == null) return cardInstance;

        // 1. Limpiar temas elementales previos
        cardRoot.RemoveFromClassList("element-venus");
        cardRoot.RemoveFromClassList("element-mars");
        cardRoot.RemoveFromClassList("element-jupiter");
        cardRoot.RemoveFromClassList("element-mercury");

        if (psynergy != null && state != PsynergyCardState.Locked)
        {
            cardRoot.AddToClassList($"element-{psynergy.Element.ToString().ToLower()}");

            // Element Pip Icon
            VisualElement elementPip = cardRoot.Q<VisualElement>("mini_psy_element_pip");
            if (elementPip != null)
            {
                elementPip.RemoveFromClassList("icon-venus");
                elementPip.RemoveFromClassList("icon-mars");
                elementPip.RemoveFromClassList("icon-jupiter");
                elementPip.RemoveFromClassList("icon-mercury");
                elementPip.AddToClassList($"icon-{psynergy.Element.ToString().ToLower()}");
            }

            // Icono Principal
            VisualElement iconElem = cardRoot.Q<VisualElement>("mini_psy_icon");
            if (iconElem != null && psynergy.Icon != null)
            {
                iconElem.style.backgroundImage = new StyleBackground(psynergy.Icon);
            }

            // Nombre y Coste PP
            Label nameLabel = cardRoot.Q<Label>("mini_psy_name");
            if (nameLabel != null) nameLabel.text = psynergy.PsynergyName;

            Label ppLabel = cardRoot.Q<Label>("mini_psy_pp");
            if (ppLabel != null) ppLabel.text = $"{psynergy.PpCost} PP";

            // Badges
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

        // 2. Aplicar capa de estado / proyección / candado
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

        // 1. Instanciar Psinergías Aprendidas Actuales
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

        // 2. Instanciar Psinergías Bloqueadas de la clase actual
        List<ClassPsynergyEntry> futurePsynergies = currentHeroDjinnController.CurrentClass.GetFuturePsynergies(currentHero.Level);
        foreach (var entry in futurePsynergies)
        {
            TemplateContainer card = CreatePsynergyCardInstance(entry.psynergy, PsynergyCardState.Locked, entry.requiredLevel);
            if (card != null) scrollClassPsynergies.Add(card);
        }
    }

    /// <summary>
    /// Populates a given ScrollView with Psynergies categorized by state for Projection/Comparison views.
    /// </summary>
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

    #region Vista B & Modal Placeholder

    private void RefreshVistaB()
    {
        // TODO: Renderizar vista B
    }

    private void SwitchToView(DjinnViewMode newMode)
    {
        currentViewMode = newMode;

        if (pageDjinnContainer != null) pageDjinnContainer.style.display = (newMode == DjinnViewMode.Individual) ? DisplayStyle.Flex : DisplayStyle.None;
        if (sectionVistaB != null) sectionVistaB.style.display = (newMode == DjinnViewMode.Global) ? DisplayStyle.Flex : DisplayStyle.None;

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