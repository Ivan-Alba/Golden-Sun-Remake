using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PageStatusController
{
    private VisualElement rootContainer;
    private VisualElement[] heroCards = new VisualElement[4];

    public PageStatusController(VisualElement root)
    {
        rootContainer = root;
        BindCards();
    }

    private void BindCards()
    {
        for (int i = 0; i < 4; i++)
        {
            heroCards[i] = rootContainer.Q<VisualElement>($"hero_card_{i}");
        }
    }

    #region Data Population

    /// <summary>
    /// Populates party status cards with actual data from active hero party members.
    /// Uses live calculated properties provided by HeroStats via StatCalculator.
    /// </summary>
    /// <param name="partyMembers">List of party member HeroStats.</param>
    public void PopulateData(List<HeroStats> partyMembers)
    {
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

    private void FillCardData(int index, HeroStats stats)
    {
        VisualElement card = heroCards[index];
        if (card == null || stats == null) return;

        HeroDjinnController djinnController = stats.GetComponent<HeroDjinnController>();

        // --- 1. HEADER SECTION ---
        var portrait = card.Q<VisualElement>($"hero_portrait_{index}") ?? card.Q<VisualElement>("hero_portrait");
        if (portrait != null && stats.TurnBarImage != null)
        {
            portrait.style.backgroundImage = new StyleBackground(stats.TurnBarImage);
        }

        var labelName = card.Q<Label>($"label_hero_name_{index}") ?? card.Q<Label>("label_hero_name");
        if (labelName != null) labelName.text = stats.Name;

        var labelLevel = card.Q<Label>($"label_hero_level_{index}") ?? card.Q<Label>("label_hero_level");
        if (labelLevel != null) labelLevel.text = $"Nv. {stats.Level}";

        var expBar = card.Q<ProgressBar>($"bar_exp_{index}") ?? card.Q<ProgressBar>("bar_exp");
        var expLabel = card.Q<Label>($"label_exp_text_{index}") ?? card.Q<Label>("label_exp_text");

        if (expBar != null && stats.NextLevelXp > 0)
        {
            expBar.value = Mathf.Clamp01((float)stats.CurrentXp / stats.NextLevelXp) * 100f;
        }

        if (expLabel != null)
        {
            expLabel.text = $"EXP {stats.CurrentXp}/{stats.NextLevelXp}";
        }

        // --- 2. HEALTH & PSYNERGY BARS ---
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

        // --- 3. MAIN STATS GRID (2x2) ---
        // English comment: Reads live calculated core stats from HeroStats overrides.
        SetStatGridValue(card, $"label_stat_value_0_{index}", "label_stat_value_0", stats.Attack.ToString());
        SetStatGridValue(card, $"label_stat_value_1_{index}", "label_stat_value_1", stats.Defense.ToString());
        SetStatGridValue(card, $"label_stat_value_2_{index}", "label_stat_value_2", stats.Agility.ToString());
        SetStatGridValue(card, $"label_stat_value_3_{index}", "label_stat_value_3", stats.Luck.ToString());

        // --- 4. ELEMENTAL AFFINITY TABLE ---
        // English comment: Reads live calculated elemental powers and resistances from HeroStats overrides.
        SetElementRowValue(card, index, 0, stats.PowerVenus, stats.ResistVenus);
        SetElementRowValue(card, index, 1, stats.PowerMars, stats.ResistMars);
        SetElementRowValue(card, index, 2, stats.PowerJupiter, stats.ResistJupiter);
        SetElementRowValue(card, index, 3, stats.PowerMercury, stats.ResistMercury);

        // --- 5. UNIFIED CLASS & DJINN SECTION ---
        var labelClass = card.Q<Label>($"label_hero_class_{index}") ?? card.Q<Label>("label_hero_class");
        if (labelClass != null)
        {
            labelClass.text = (djinnController != null && djinnController.CurrentClass != null)
                ? djinnController.CurrentClass.ClassName
                : "Sin Clase";
        }

        PopulateElementPips(card, index, djinnController);
        PopulateMainDjinnSection(card, index, djinnController);
    }

    private void PopulateElementPips(VisualElement card, int cardIndex, HeroDjinnController djinnController)
    {
        VisualElement pipsRow = card.Q<VisualElement>($"equipped_pips_row_{cardIndex}") ?? card.Q<VisualElement>("equipped_pips_row");
        if (pipsRow == null) return;

        int venusSet = 0, venusTotal = 0;
        int marsSet = 0, marsTotal = 0;
        int jupiterSet = 0, jupiterTotal = 0;
        int mercurySet = 0, mercuryTotal = 0;

        if (djinnController != null)
        {
            // English comment: Query counts directly using HeroDjinnController API helpers.
            venusSet = djinnController.GetSetCountByElement(ElementalType.Venus);
            venusTotal = djinnController.GetTotalCountByElement(ElementalType.Venus);

            marsSet = djinnController.GetSetCountByElement(ElementalType.Mars);
            marsTotal = djinnController.GetTotalCountByElement(ElementalType.Mars);

            jupiterSet = djinnController.GetSetCountByElement(ElementalType.Jupiter);
            jupiterTotal = djinnController.GetTotalCountByElement(ElementalType.Jupiter);

            mercurySet = djinnController.GetSetCountByElement(ElementalType.Mercury);
            mercuryTotal = djinnController.GetTotalCountByElement(ElementalType.Mercury);
        }

        var pipLabels = pipsRow.Query<Label>(className: "pip-count").ToList();
        if (pipLabels.Count >= 4)
        {
            pipLabels[0].text = $"{venusSet}/{venusTotal}";
            pipLabels[1].text = $"{marsSet}/{marsTotal}";
            pipLabels[2].text = $"{jupiterSet}/{jupiterTotal}";
            pipLabels[3].text = $"{mercurySet}/{mercuryTotal}";
        }
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

            StatusEffectSO passiveEffect = data.LeaderPassiveEffect;
            if (passiveEffect != null)
            {
                if (passiveDivider != null) passiveDivider.style.display = DisplayStyle.Flex;
                if (passiveTitleLabel != null)
                {
                    passiveTitleLabel.style.display = DisplayStyle.Flex;
                    passiveTitleLabel.text = passiveEffect.EffectName;
                }
                if (passiveDescLabel != null)
                {
                    passiveDescLabel.style.display = DisplayStyle.Flex;
                    passiveDescLabel.text = passiveEffect.Description;
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
                badgeStatus.RemoveFromClassList("badge-active");
                badgeStatus.RemoveFromClassList("badge-standby");
                badgeStatus.RemoveFromClassList("badge-recovery");
                badgeStatus.style.display = DisplayStyle.Flex;

                switch (leader.CurrentState)
                {
                    case DjinnState.Set:
                        badgeStatus.AddToClassList("badge-active");
                        if (badgeLabel != null) badgeLabel.text = "PASIVA ACTIVA";
                        break;

                    case DjinnState.Standby:
                        badgeStatus.AddToClassList("badge-standby");
                        if (badgeLabel != null) badgeLabel.text = "EN ESPERA";
                        if (djinnCard != null) djinnCard.AddToClassList("card-djinn-standby");
                        break;

                    case DjinnState.Recovery:
                        badgeStatus.AddToClassList("badge-recovery");
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

    private void SetStatGridValue(VisualElement card, string primaryLabelName, string fallbackLabelName, string value)
    {
        var valLabel = card.Q<Label>(primaryLabelName) ?? card.Q<Label>(fallbackLabelName);
        if (valLabel != null)
        {
            valLabel.text = value;
        }
    }

    private void SetElementRowValue(VisualElement card, int cardIndex, int rowIndex, int power, int resist)
    {
        var pwrLabel = card.Q<Label>($"elem_power_{rowIndex}_{cardIndex}") ?? card.Q<Label>($"elem_power_{rowIndex}");
        var resLabel = card.Q<Label>($"elem_resistance_{rowIndex}_{cardIndex}") ?? card.Q<Label>($"elem_resistance_{rowIndex}");

        if (pwrLabel != null) pwrLabel.text = power.ToString();
        if (resLabel != null) resLabel.text = resist.ToString();
    }

    #endregion
}