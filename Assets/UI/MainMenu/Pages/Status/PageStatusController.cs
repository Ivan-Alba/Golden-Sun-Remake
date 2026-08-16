using UnityEngine;
using UnityEngine.UIElements;

public class PageStatusController
{
    private VisualElement rootContainer;
    private VisualElement[] heroCards = new VisualElement[4];
    private bool[] isFlipped = new bool[4];
    private int focusedCardIndex = 0;

    public PageStatusController(VisualElement root)
    {
        rootContainer = root;
        BindCards();
        RegisterCallbacks();
        UpdateHighlight();
    }

    private void BindCards()
    {
        for (int i = 0; i < 4; i++)
        {
            heroCards[i] = rootContainer.Q<VisualElement>($"hero_card_{i}");
            isFlipped[i] = false;
        }
    }

    private void RegisterCallbacks()
    {
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            if (heroCards[i] != null)
            {
                heroCards[i].RegisterCallback<ClickEvent>(e =>
                {
                    SetFocusedCard(index);
                    ToggleCardFlip(index);
                });
            }
        }
    }

    public void HandleConfirm()
    {
        ToggleCardFlip(focusedCardIndex);
    }

    #region Data Population

    /// <summary>
    /// Populates party status cards with actual data from active hero party members.
    /// </summary>
    /// <param name="partyMembers">List of party member HeroStats.</param>
    public void PopulateData(System.Collections.Generic.List<HeroStats> partyMembers)
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

        // --- FRONT FACE ---
        var labelName = card.Q<Label>("label_hero_name");
        if (labelName != null) labelName.text = stats.Name;

        var labelClass = card.Q<Label>("label_hero_class");
        //if (labelClass != null && stats.CurrentClass != null) labelClass.text = stats.CurrentClass.ClassName;

        var labelLevel = card.Q<Label>("label_hero_level");
        if (labelLevel != null) labelLevel.text = $"Lv. {stats.Level}";

        // HP / PP text and progress bars
        var labelHp = card.Q<Label>("label_hp_text");
        if (labelHp != null) labelHp.text = $"PV {stats.CurrHp} / {stats.MaxHp}";

        var hpBar = card.Q<ProgressBar>("bar_hp");
        if (hpBar != null && stats.MaxHp > 0) hpBar.value = ((float)stats.CurrHp / stats.MaxHp) * 100f;

        var labelPp = card.Q<Label>("label_pp_text");
        if (labelPp != null) labelPp.text = $"PP {stats.CurrPp} / {stats.MaxPp}";

        var ppBar = card.Q<ProgressBar>("bar_pp");
        if (ppBar != null && stats.MaxPp > 0) ppBar.value = ((float)stats.CurrPp / stats.MaxPp) * 100f;

        // --- BACK FACE ---
        // Base Attributes
        SetStatRowValue(card, "stat_row_0", stats.Attack.ToString());
        SetStatRowValue(card, "stat_row_1", stats.Defense.ToString());
        SetStatRowValue(card, "stat_row_2", stats.Agility.ToString());
        SetStatRowValue(card, "stat_row_3", stats.Luck.ToString());

        // Experience
        var labelExp = card.Q<Label>("label_exp_text");
        if (labelExp != null) labelExp.text = $"EXP {stats.CurrentXp} / {stats.NextLevelXp}";

        var expBar = card.Q<ProgressBar>("bar_exp");
        if (expBar != null && stats.NextLevelXp > 0) expBar.value = ((float)stats.CurrentXp / stats.NextLevelXp) * 100f;

        // Elemental Stats (Venus, Mars, Jupiter, Mercury)
        SetElementRowValue(card, "element_row_0", stats.PowerVenus, stats.ResistVenus);
        SetElementRowValue(card, "element_row_1", stats.PowerMars, stats.ResistMars);
        SetElementRowValue(card, "element_row_2", stats.PowerJupiter, stats.ResistJupiter);
        SetElementRowValue(card, "element_row_3", stats.PowerMercury, stats.ResistMercury);
    }

    private void SetStatRowValue(VisualElement card, string rowName, string value)
    {
        var row = card.Q<VisualElement>(rowName);
        if (row != null)
        {
            var valLabel = row.Q<Label>("label_stat_value");
            if (valLabel != null) valLabel.text = value;
        }
    }

    private void SetElementRowValue(VisualElement card, string rowName, int power, int resist)
    {
        var row = card.Q<VisualElement>(rowName);
        if (row != null)
        {
            var pwrLabel = row.Q<Label>("elem_power");
            var resLabel = row.Q<Label>("elem_resistance");
            if (pwrLabel != null) pwrLabel.text = $"{power}";
            if (resLabel != null) resLabel.text = $"{resist}";
        }
    }

    #endregion

    public void NavigateHero(int direction)
    {
        int totalCards = heroCards.Length;
        int nextIndex = (focusedCardIndex + direction + totalCards) % totalCards;
        SetFocusedCard(nextIndex);
    }

    public void ToggleCardFlip(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= heroCards.Length || heroCards[cardIndex] == null) return;

        VisualElement cardFront = heroCards[cardIndex].Q<VisualElement>("card_front");
        VisualElement cardBack = heroCards[cardIndex].Q<VisualElement>("card_back");

        if (cardFront == null || cardBack == null) return;

        isFlipped[cardIndex] = !isFlipped[cardIndex];

        cardFront.style.display = isFlipped[cardIndex] ? DisplayStyle.None : DisplayStyle.Flex;
        cardBack.style.display = isFlipped[cardIndex] ? DisplayStyle.Flex : DisplayStyle.None;

        Debug.Log($"[PageStatus] Card {cardIndex} flipped. Back visible: {isFlipped[cardIndex]}");
    }

    public void SetFocusedCard(int index)
    {
        focusedCardIndex = Mathf.Clamp(index, 0, 3);
        UpdateHighlight();
    }

    private void UpdateHighlight()
    {
        for (int i = 0; i < heroCards.Length; i++)
        {
            if (heroCards[i] == null) continue;

            if (i == focusedCardIndex)
            {
                heroCards[i].style.borderLeftColor = new StyleColor(new Color(0.85f, 0.65f, 0.13f));
                heroCards[i].style.borderLeftWidth = 2;
                heroCards[i].style.borderRightColor = new StyleColor(new Color(0.85f, 0.65f, 0.13f));
                heroCards[i].style.borderRightWidth = 2;
            }
            else
            {
                heroCards[i].style.borderLeftWidth = 0;
                heroCards[i].style.borderRightWidth = 0;
            }
        }
    }
}