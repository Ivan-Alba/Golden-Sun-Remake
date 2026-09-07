using UnityEngine;
using UnityEngine.UIElements;

public class HeroStatusCardPresenter
{
    #region UI Elements References

    private readonly VisualElement _rootElement;

    private readonly VisualElement _portraitImg;
    private readonly Label _nameLabel;
    private readonly Label _levelLabel;
    private readonly Label _keyNumberLabel;

    private readonly Label _hpValLabel;
    private readonly Label _ppValLabel;
    private readonly VisualElement _hpFillBar;
    private readonly VisualElement _ppFillBar;

    private readonly VisualElement _elementGem;

    #endregion

    /// <summary>
    /// Constructor that queries and binds all UXML elements inside the HeroStatusCard template.
    /// </summary>
    public HeroStatusCardPresenter(VisualElement root)
    {
        _rootElement = root;

        if (_rootElement == null)
        {
            Debug.LogError($"[{nameof(HeroStatusCardPresenter)}] Root VisualElement is null!");
            return;
        }

        // Query UXML Elements
        _portraitImg = _rootElement.Q<VisualElement>("hero_portrait_img");
        _nameLabel = _rootElement.Q<Label>("label_hero_name");
        _levelLabel = _rootElement.Q<Label>("label_hero_level");
        _keyNumberLabel = _rootElement.Q<Label>("label_key_number");

        _hpValLabel = _rootElement.Q<Label>("label_hp_val");
        _ppValLabel = _rootElement.Q<Label>("label_pp_val");
        _hpFillBar = _rootElement.Q<VisualElement>("bar_hp_fill");
        _ppFillBar = _rootElement.Q<VisualElement>("bar_pp_fill");

        _elementGem = _rootElement.Q<VisualElement>("hero_element_gem");
    }

    /// <summary>
    /// Updates all stats, labels, bars, and artwork for this hero card.
    /// </summary>
    public void UpdateData(HeroStats stats, string heroName, int keyNumber = 1, bool isActiveHero = false)
    {
        if (stats == null || _rootElement == null) return;

        // 1. Update Name & Level
        if (_nameLabel != null) _nameLabel.text = stats.Name;
        if (_levelLabel != null) _levelLabel.text = $"Nv. {stats.Level}";

        // 2. Update Key Badge Number
        if (_keyNumberLabel != null) _keyNumberLabel.text = keyNumber.ToString();

        // 3. Update Numerical Values
        if (_hpValLabel != null) _hpValLabel.text = $"{stats.CurrHp}/{stats.MaxHp}";
        if (_ppValLabel != null) _ppValLabel.text = $"{stats.CurrPp}/{stats.MaxPp}";

        // 4. Update Fill Bars (Width percentage calculation)
        if (_hpFillBar != null)
        {
            float hpPercent = stats.MaxHp > 0 ? Mathf.Clamp01((float)stats.CurrHp / stats.MaxHp) * 100f : 0f;
            _hpFillBar.style.width = Length.Percent(hpPercent);
        }

        if (_ppFillBar != null)
        {
            float ppPercent = stats.MaxPp > 0 ? Mathf.Clamp01((float)stats.CurrPp / stats.MaxPp) * 100f : 0f;
            _ppFillBar.style.width = Length.Percent(ppPercent);
        }

        // 5. Update Avatar Texture from Resources
        if (_portraitImg != null)
        {
            if (stats.TurnBarImage != null)
            {
                _portraitImg.style.backgroundImage = Background.FromSprite(stats.TurnBarImage);
            }
        }

        // 6. Toggle Active Hero Highlight Style
        SetCardActive(isActiveHero);

        // Ensure Card is visible
        SetVisible(true);
    }

    /// <summary>
    /// Highlights or dims the card based on whether this hero is currently controlled.
    /// </summary>
    public void SetCardActive(bool isActive)
    {
        if (_rootElement == null) return;

        if (isActive)
        {
            _rootElement.AddToClassList("active-hero");
        }
        else
        {
            _rootElement.RemoveFromClassList("active-hero");
        }
    }

    /// <summary>
    /// Shows or hides the entire status card.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (_rootElement != null)
        {
            _rootElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}