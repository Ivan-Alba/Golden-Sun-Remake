using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Controls the main menu Status sub-page, rendering a global summary view 
/// of all active party members' stats, levels, elemental pips, and main Djinn configuration.
/// Implements IMenuPageController for unified lifecycle integration.
/// </summary>
public class PageStatusController : IMenuPageController
{
    #region Private Fields

    private VisualElement rootContainer;
    private readonly VisualElement[] heroCards = new VisualElement[4];

    #endregion

    #region Interface Properties & Implementation

    /// <summary>
    /// Status page is purely informational and never enters targeting mode.
    /// </summary>
    public bool IsTargeting => false;

    /// <summary>
    /// Populates party status cards using the centralized active party GameObjects list.
    /// </summary>
    public void PopulateData(IReadOnlyList<GameObject> activeParty, int selectedHeroIndex)
    {
        List<HeroStats> partyMembers = ExtractHeroStatsList(activeParty);
        int partyCount = partyMembers != null ? partyMembers.Count : 0;

        for (int i = 0; i < 4; i++)
        {
            if (heroCards[i] == null) continue;

            if (i < partyCount)
            {
                heroCards[i].style.display = DisplayStyle.Flex;
                FillCardData(i, partyMembers[i]);
            }
            else
            {
                heroCards[i].style.display = DisplayStyle.None;
            }
        }
    }

    public void CancelTargeting()
    {
        // Status page does not support targeting operations, nothing to cancel.
    }

    public void Dispose()
    {
        // Clean up references when page is closed or disposed.
        rootContainer = null;
    }

    #endregion

    #region Constructor & Binding

    public PageStatusController(VisualElement root)
    {
        rootContainer = root;
        BindCards();
    }

    private void BindCards()
    {
        if (rootContainer == null) return;

        for (int i = 0; i < 4; i++)
        {
            heroCards[i] = rootContainer.Q<VisualElement>($"hero_card_{i}");
        }
    }

    private List<HeroStats> ExtractHeroStatsList(IReadOnlyList<GameObject> activeParty)
    {
        List<HeroStats> partyStats = new List<HeroStats>();
        if (activeParty != null)
        {
            foreach (GameObject memberObj in activeParty)
            {
                if (memberObj != null && memberObj.TryGetComponent<HeroStats>(out HeroStats heroStats))
                {
                    partyStats.Add(heroStats);
                }
            }
        }
        return partyStats;
    }

    #endregion

    #region Data Population & Card Rendering

    private void FillCardData(int index, HeroStats stats)
    {
        VisualElement card = heroCards[index];
        if (card == null || stats == null) return;

        HeroDjinnController djinnController = stats.GetComponent<HeroDjinnController>();

        // --- 1. HEADER SECTION (Hero Avatar, Name, Level, Exp, Class) ---
        var portrait = card.Q<VisualElement>("hero_avatar_icon");
        if (portrait != null && stats.TurnBarImage != null)
        {
            portrait.style.backgroundImage = new StyleBackground(stats.TurnBarImage);
        }

        var labelName = card.Q<Label>("label_hero_name");
        if (labelName != null) labelName.text = stats.Name;

        var labelLevel = card.Q<Label>("label_hero_level");
        if (labelLevel != null) labelLevel.text = $"Nv. {stats.Level}";

        var expBar = card.Q<ProgressBar>($"bar_exp");
        var expLabel = card.Q<Label>($"label_exp_text_{index}") ?? card.Q<Label>("label_exp_text");

        if (expBar != null && stats.NextLevelXp > 0)
        {
            expBar.value = Mathf.Clamp01((float)stats.CurrentXp / stats.NextLevelXp) * 100f;
        }

        if (expLabel != null)
        {
            expLabel.text = $"EXP {stats.CurrentXp}/{stats.NextLevelXp}";
        }

        var labelClass = card.Q<Label>("label_hero_class_current");
        if (labelClass != null)
        {
            labelClass.text = (djinnController != null && djinnController.CurrentClass != null)
                ? djinnController.CurrentClass.ClassName
                : "Sin Clase";
        }

        PopulateElementPips(card, djinnController);

        // --- 2. EXP & HEALTH BARS ---
        var labelHp = card.Q<Label>($"label_hp_text_{index}") ?? card.Q<Label>("label_hp_text");
        if (labelHp != null) labelHp.text = $"PV {stats.CurrHp} / {stats.MaxHp}";

        var hpBar = card.Q<ProgressBar>($"bar_hp_{index}") ?? card.Q<ProgressBar>("bar_hp");
        if (hpBar != null && stats.MaxHp > 0)
        {
            hpBar.value = Mathf.Clamp01((float)stats.CurrHp / stats.MaxHp) * 100f;
        }

        var labelPp = card.Q<Label>($"label_pp_text_{index}") ?? card.Q<Label>("label_pp_text");
        if (labelPp != null) labelPp.text = $"PP {stats.CurrPp} / {stats.MaxPp}";

        var ppBar = card.Q<ProgressBar>($"bar_pp_{index}") ?? card.Q<ProgressBar>("bar_pp");
        if (ppBar != null && stats.MaxPp > 0)
        {
            ppBar.value = Mathf.Clamp01((float)stats.CurrPp / stats.MaxPp) * 100f;
        }

        // --- 3. MAIN STATS GRID (2x2 Core Attributes) ---
        SetStatGridValue(card, stats);

        // --- 4. ELEMENTAL AFFINITY TABLE ---
        SetElementGridValue(card, stats);

        // --- 5. MAIN DJINN SECTION ---
        PopulateMainDjinnSection(card, index, djinnController);
    }

    private void PopulateElementPips(VisualElement card, HeroDjinnController djinnController)
    {
        var labelVenus = card.Q<Label>("label_pip_venus");
        var labelMars = card.Q<Label>("label_pip_mars");
        var labelJupiter = card.Q<Label>("label_pip_jupiter");
        var labelMercury = card.Q<Label>("label_pip_mercury");

        int venusSet = 0, venusTotal = 0;
        int marsSet = 0, marsTotal = 0;
        int jupiterSet = 0, jupiterTotal = 0;
        int mercurySet = 0, mercuryTotal = 0;

        if (djinnController != null)
        {
            venusSet = djinnController.GetSetCountByElement(ElementalType.Venus);
            venusTotal = djinnController.GetTotalCountByElement(ElementalType.Venus);

            marsSet = djinnController.GetSetCountByElement(ElementalType.Mars);
            marsTotal = djinnController.GetTotalCountByElement(ElementalType.Mars);

            jupiterSet = djinnController.GetSetCountByElement(ElementalType.Jupiter);
            jupiterTotal = djinnController.GetTotalCountByElement(ElementalType.Jupiter);

            mercurySet = djinnController.GetSetCountByElement(ElementalType.Mercury);
            mercuryTotal = djinnController.GetTotalCountByElement(ElementalType.Mercury);
        }

        if (labelVenus != null) labelVenus.text = $"{venusSet}/{venusTotal}";
        if (labelMars != null) labelMars.text = $"{marsSet}/{marsTotal}";
        if (labelJupiter != null) labelJupiter.text = $"{jupiterSet}/{jupiterTotal}";
        if (labelMercury != null) labelMercury.text = $"{mercurySet}/{mercuryTotal}";
    }

    private void PopulateMainDjinnSection(VisualElement card, int cardIndex, HeroDjinnController djinnController)
    {
        VisualElement djinnCard = card.Q<VisualElement>($"main_djinn_card_{cardIndex}") ?? card.Q<VisualElement>("main_djinn_card");
        VisualElement djinnBody = card.Q<VisualElement>($"main_djinn_body_{cardIndex}") ?? card.Q<VisualElement>("main_djinn_body");
        VisualElement djinnIcon = card.Q<VisualElement>($"main_djinn_icon_{cardIndex}") ?? card.Q<VisualElement>("main_djinn_icon");
        VisualElement djinnElementIcon = card.Q<VisualElement>($"main_djinn_element_icon_{cardIndex}");
        VisualElement badgeStatus = card.Q<VisualElement>($"badge_status_{cardIndex}") ?? card.Q<VisualElement>("badge_status");
        Label badgeLabel = card.Q<Label>($"label_badge_status_{cardIndex}") ?? card.Q<Label>("label_badge_status");

        Label nameLabel = card.Q<Label>($"label_main_djinn_name_{cardIndex}") ?? card.Q<Label>("label_main_djinn_name");
        Label abilityLabel = card.Q<Label>($"label_main_djinn_ability_{cardIndex}") ?? card.Q<Label>("label_main_djinn_ability");
        VisualElement passiveDivider = card.Q<VisualElement>($"passive_divider_{cardIndex}") ?? card.Q<VisualElement>("passive_divider") ?? card.Q<VisualElement>(className: "passive-divider");
        Label passiveTitleLabel = card.Q<Label>($"label_passive_title_{cardIndex}") ?? card.Q<Label>("label_passive_title");
        Label passiveDescLabel = card.Q<Label>($"label_passive_desc_{cardIndex}") ?? card.Q<Label>("label_passive_desc");

        DjinnInstance leader = djinnController != null ? djinnController.LeaderDjinn : null;

        if (leader != null && leader.DjinnData != null)
        {
            DjinnSO data = leader.DjinnData;

            if (nameLabel != null) nameLabel.text = data.DjinnName;
            if (abilityLabel != null) abilityLabel.text = data.UnleashEffect.Description;
            if (djinnIcon != null) djinnIcon.style.backgroundImage = new StyleBackground(data.Icon);

            UpdateDjinnElementStyles(djinnBody, djinnElementIcon, data.Element);

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

            if (hasLeaderPassive)
            {
                if (passiveDivider != null) passiveDivider.style.display = DisplayStyle.Flex;
                if (passiveTitleLabel != null)
                {
                    passiveTitleLabel.style.display = DisplayStyle.Flex;
                    passiveTitleLabel.text = data.LeaderPassiveName;
                }
                if (passiveDescLabel != null)
                {
                    passiveDescLabel.style.display = DisplayStyle.Flex;
                    passiveDescLabel.text = data.LeaderPassiveDescription;
                }
            }
            else
            {
                if (passiveDivider != null) passiveDivider.style.display = DisplayStyle.None;
                if (passiveTitleLabel != null) passiveTitleLabel.style.display = DisplayStyle.None;
                if (passiveDescLabel != null) passiveDescLabel.style.display = DisplayStyle.None;
            }

            if (djinnCard != null)
            {
                djinnCard.RemoveFromClassList("card-djinn-standby");
                djinnCard.RemoveFromClassList("card-djinn-recovery");
            }

            if (badgeStatus != null)
            {
                badgeStatus.RemoveFromClassList("djinn-badge-active");
                badgeStatus.RemoveFromClassList("djinn-badge-standby");
                badgeStatus.RemoveFromClassList("djinn-badge-recovery");
                badgeStatus.style.display = DisplayStyle.Flex;

                switch (leader.CurrentState)
                {
                    case DjinnState.Set:
                        badgeStatus.AddToClassList("djinn-badge-active");
                        if (badgeLabel != null) badgeLabel.text = "PASIVA ACTIVA";
                        break;

                    case DjinnState.Standby:
                        badgeStatus.AddToClassList("djinn-badge-standby");
                        if (badgeLabel != null) badgeLabel.text = "EN ESPERA";
                        if (djinnCard != null) djinnCard.AddToClassList("card-djinn-standby");
                        break;

                    case DjinnState.Recovery:
                        badgeStatus.AddToClassList("djinn-badge-recovery");
                        if (badgeLabel != null) badgeLabel.text = $"RECUP. ({leader.CurrentRecoveryTurns}T)";
                        if (djinnCard != null) djinnCard.AddToClassList("card-djinn-recovery");
                        break;
                }
            }
        }
        else
        {
            if (nameLabel != null) nameLabel.text = "Sin Asignar";
            if (abilityLabel != null) abilityLabel.text = "No hay un Djinn Principal seleccionado.";
            if (passiveDivider != null) passiveDivider.style.display = DisplayStyle.None;
            if (passiveTitleLabel != null) passiveTitleLabel.style.display = DisplayStyle.None;
            if (passiveDescLabel != null) passiveDescLabel.style.display = DisplayStyle.None;
            if (badgeStatus != null) badgeStatus.style.display = DisplayStyle.None;

            ResetDjinnElementStyles(djinnBody, djinnElementIcon);
            if (djinnBody != null)
            {
                djinnBody.AddToClassList("djinn-none-border");
                djinnBody.AddToClassList("djinn-none-bg");
            }
        }
    }

    private void UpdateDjinnElementStyles(VisualElement djinnBody, VisualElement elementIcon, ElementalType element)
    {
        if (djinnBody == null) return;

        ResetDjinnElementStyles(djinnBody, elementIcon);

        djinnBody.AddToClassList($"element-{element.ToString().ToLower()}");
        djinnBody.AddToClassList($"element-{element.ToString().ToLower()}-bg");

        if (elementIcon != null)
        {
            switch (element)
            {
                case ElementalType.Venus:
                    elementIcon.AddToClassList("icon-venus");
                    break;
                case ElementalType.Mars:
                    elementIcon.AddToClassList("icon-mars");
                    break;
                case ElementalType.Jupiter:
                    elementIcon.AddToClassList("icon-jupiter");
                    break;
                case ElementalType.Mercury:
                    elementIcon.AddToClassList("icon-mercury");
                    break;
            }
        }
    }

    private void ResetDjinnElementStyles(VisualElement djinnBody, VisualElement elementIcon)
    {
        if (djinnBody != null)
        {
            djinnBody.RemoveFromClassList("element-venus");
            djinnBody.RemoveFromClassList("element-venus-bg");
            djinnBody.RemoveFromClassList("element-mars");
            djinnBody.RemoveFromClassList("element-mars-bg");
            djinnBody.RemoveFromClassList("element-jupiter");
            djinnBody.RemoveFromClassList("element-jupiter-bg");
            djinnBody.RemoveFromClassList("element-mercury");
            djinnBody.RemoveFromClassList("element-mercury-bg");
            djinnBody.RemoveFromClassList("element-none");
        }

        if (elementIcon != null)
        {
            elementIcon.RemoveFromClassList("icon-venus");
            elementIcon.RemoveFromClassList("icon-mars");
            elementIcon.RemoveFromClassList("icon-jupiter");
            elementIcon.RemoveFromClassList("icon-mercury");
        }
    }

    private void SetStatGridValue(VisualElement card, HeroStats stats)
    {
        var maxHpLabel = card.Q<Label>("label_stat_max_hp");
        if (maxHpLabel != null) maxHpLabel.text = stats.MaxHp.ToString();
        var maxPpLabel = card.Q<Label>("label_stat_max_pp");
        if (maxPpLabel != null) maxPpLabel.text = stats.MaxPp.ToString();
        var attackLabel = card.Q<Label>("label_stat_atq");
        if (attackLabel != null) attackLabel.text = stats.Attack.ToString();
        var defenseLabel = card.Q<Label>("label_stat_def");
        if (defenseLabel != null) defenseLabel.text = stats.Defense.ToString();
        var agilityLabel = card.Q<Label>("label_stat_agi");
        if (agilityLabel != null) agilityLabel.text = stats.Agility.ToString();
        var luckLabel = card.Q<Label>("label_stat_sue");
        if (luckLabel != null) luckLabel.text = stats.Luck.ToString();
    }

    private void SetElementGridValue(VisualElement card, HeroStats stats)
    {
        var pwrVenus = card.Q<Label>($"elem_power_venus");
        var resVenus = card.Q<Label>($"elem_resistance_venus");

        var pwrMars = card.Q<Label>($"elem_power_mars");
        var resMars = card.Q<Label>($"elem_resistance_mars");

        var pwrJupiter = card.Q<Label>($"elem_power_jupiter");
        var resJupiter = card.Q<Label>($"elem_resistance_jupiter");

        var pwrMercury = card.Q<Label>($"elem_power_mercury");
        var resMercury = card.Q<Label>($"elem_resistance_mercury");

        if (pwrVenus != null) pwrVenus.text = stats.PowerVenus.ToString();
        if (resVenus != null) resVenus.text = stats.ResistVenus.ToString();

        if (pwrMars != null) pwrMars.text = stats.PowerMars.ToString();
        if (resMars != null) resMars.text = stats.ResistMars.ToString();

        if (pwrJupiter != null) pwrJupiter.text = stats.PowerJupiter.ToString();
        if (resJupiter != null) resJupiter.text = stats.ResistJupiter.ToString();

        if (pwrMercury != null) pwrMercury.text = stats.PowerMercury.ToString();
        if (resMercury != null) resMercury.text = stats.ResistMercury.ToString();
    }

    #endregion
}