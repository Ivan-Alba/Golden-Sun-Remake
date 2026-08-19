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
                // Activate card if hero exists in party
                heroCards[i].style.display = DisplayStyle.Flex;
                FillCardData(i, partyMembers[i]);
            }
            else
            {
                // Hide slot if no hero present
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
            expBar.value = ((float)stats.CurrentXp / stats.NextLevelXp) * 100f;
        }

        if (expLabel != null)
        {
            expLabel.text = $"EXP {stats.CurrentXp}/{stats.NextLevelXp}";
        }

        // --- 2. HEALTH & PSYNERGY BARS ---
        var labelHp = card.Q<Label>($"label_hp_text_{index}") ?? card.Q<Label>("label_hp_text");
        if (labelHp != null) labelHp.text = $"PV {stats.CurrHp} / {stats.MaxHp}";

        var hpBar = card.Q<ProgressBar>($"bar_hp_{index}") ?? card.Q<ProgressBar>("bar_hp");
        if (hpBar != null && stats.MaxHp > 0) hpBar.value = ((float)stats.CurrHp / stats.MaxHp) * 100f;

        var labelPp = card.Q<Label>($"label_pp_text_{index}") ?? card.Q<Label>("label_pp_text");
        if (labelPp != null) labelPp.text = $"PP {stats.CurrPp} / {stats.MaxPp}";

        var ppBar = card.Q<ProgressBar>($"bar_pp_{index}") ?? card.Q<ProgressBar>("bar_pp");
        if (ppBar != null && stats.MaxPp > 0) ppBar.value = ((float)stats.CurrPp / stats.MaxPp) * 100f;

        // --- 3. MAIN STATS GRID (2x2) ---
        SetStatGridValue(card, $"label_stat_value_0_{index}", "label_stat_value_0", stats.Attack.ToString());
        SetStatGridValue(card, $"label_stat_value_1_{index}", "label_stat_value_1", stats.Defense.ToString());
        SetStatGridValue(card, $"label_stat_value_2_{index}", "label_stat_value_2", stats.Agility.ToString());
        SetStatGridValue(card, $"label_stat_value_3_{index}", "label_stat_value_3", stats.Luck.ToString());

        // --- 4. ELEMENTAL AFFINITY TABLE ---
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
            foreach (var djinn in djinnController.AssignedDjinns)
            {
                if (djinn != null && djinn.DjinnData != null)
                {
                    bool isSet = djinn.CurrentState == DjinnState.Set;

                    switch (djinn.DjinnData.Element)
                    {
                        case ElementalType.Venus:
                            venusTotal++;
                            if (isSet) venusSet++;
                            break;
                        case ElementalType.Mars:
                            marsTotal++;
                            if (isSet) marsSet++;
                            break;
                        case ElementalType.Jupiter:
                            jupiterTotal++;
                            if (isSet) jupiterSet++;
                            break;
                        case ElementalType.Mercury:
                            mercuryTotal++;
                            if (isSet) mercurySet++;
                            break;
                    }
                }
            }
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
            if (abilityLabel != null) abilityLabel.text = data.Description;

            // Apply elemental border and background tint directly to the inner Djinn body card
            UpdateDjinnElementStyles(djinnBody, djinnIcon, data.Element);

            // Populate passive effect details
            StatusEffectSO passiveEffect = data.LeaderPassiveEffect;
            if (passiveEffect != null)
            {
                if (passiveDivider != null) passiveDivider.style.display = DisplayStyle.Flex;
                if (passiveTitleLabel != null)
                {
                    passiveTitleLabel.style.display = DisplayStyle.Flex;
                    passiveTitleLabel.text = $"PASIVA: {passiveEffect.EffectName}";
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

            // Apply Status Badge & Outer Card Opacity by State
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
            // Discreet style for Unassigned Leader State
            if (nameLabel != null) nameLabel.text = "Sin Asignar";
            if (abilityLabel != null) abilityLabel.text = "No hay un Djinn Principal seleccionado.";
            if (passiveDivider != null) passiveDivider.style.display = DisplayStyle.None;
            if (passiveTitleLabel != null) passiveTitleLabel.style.display = DisplayStyle.None;
            if (passiveDescLabel != null) passiveDescLabel.style.display = DisplayStyle.None;
            if (badgeStatus != null) badgeStatus.style.display = DisplayStyle.None;

            ResetDjinnElementStyles(djinnBody, djinnIcon);
            if (djinnBody != null)
            {
                djinnBody.AddToClassList("djinn-none-border");
                djinnBody.AddToClassList("djinn-none-bg");
            }
        }
    }

    private void UpdateDjinnElementStyles(VisualElement djinnBody, VisualElement icon, ElementalType element)
    {
        if (djinnBody == null) return;

        ResetDjinnElementStyles(djinnBody, icon);

        string elemLower = element.ToString().ToLower();
        djinnBody.AddToClassList($"djinn-{elemLower}-border");
        djinnBody.AddToClassList($"djinn-{elemLower}-bg");
    }

    private void ResetDjinnElementStyles(VisualElement djinnBody, VisualElement icon)
    {
        if (djinnBody != null)
        {
            djinnBody.RemoveFromClassList("djinn-venus-border");
            djinnBody.RemoveFromClassList("djinn-mars-border");
            djinnBody.RemoveFromClassList("djinn-jupiter-border");
            djinnBody.RemoveFromClassList("djinn-mercury-border");
            djinnBody.RemoveFromClassList("djinn-none-border");

            djinnBody.RemoveFromClassList("djinn-venus-bg");
            djinnBody.RemoveFromClassList("djinn-mars-bg");
            djinnBody.RemoveFromClassList("djinn-jupiter-bg");
            djinnBody.RemoveFromClassList("djinn-mercury-bg");
            djinnBody.RemoveFromClassList("djinn-none-bg");
        }

        if (icon != null)
        {
            icon.RemoveFromClassList("djinn-venus-bg");
            icon.RemoveFromClassList("djinn-mars-bg");
            icon.RemoveFromClassList("djinn-jupiter-bg");
            icon.RemoveFromClassList("djinn-mercury-bg");
            icon.RemoveFromClassList("djinn-none-bg");
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