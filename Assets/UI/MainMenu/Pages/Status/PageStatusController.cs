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
        if (expBar != null && stats.NextLevelXp > 0)
        {
            expBar.value = ((float)stats.CurrentXp / stats.NextLevelXp) * 100f;
        }

        var labelClass = card.Q<Label>($"label_hero_class_{index}") ?? card.Q<Label>("label_hero_class");
        // if (labelClass != null && stats.CurrentClass != null) labelClass.text = stats.CurrentClass.ClassName;

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