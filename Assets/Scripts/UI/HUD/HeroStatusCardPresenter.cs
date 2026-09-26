using UnityEngine;
using UnityEngine.UIElements;
using static GameEvents;

public class HeroStatusCardPresenter
{
    #region Public Properties

    public VisualElement RootElement => _rootElement;
    public GameObject BoundHero { get; private set; }

    #endregion

    #region UI Elements References

    private readonly VisualElement _rootElement;

    private readonly VisualElement _portraitImg;
    private readonly Label _nameLabel;
    private readonly Label _levelLabel;
    private readonly VisualElement _heroKeyBadge;
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
    public HeroStatusCardPresenter(VisualElement root, GameObject hero = null)
    {
        _rootElement = root;
        BoundHero = hero;

        if (_rootElement == null)
        {
            Debug.LogError($"[{nameof(HeroStatusCardPresenter)}] Root VisualElement is null!");
            return;
        }

        // Query UXML Elements
        _portraitImg = _rootElement.Q<VisualElement>("hero_portrait_img");
        _nameLabel = _rootElement.Q<Label>("label_hero_name");
        _levelLabel = _rootElement.Q<Label>("label_hero_level");
        _heroKeyBadge = _rootElement.Q<VisualElement>("hero_key_badge");
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
    public void UpdateData(HeroStats stats, string heroName, int keyNumber = 1, bool isActiveHero = false, bool inBattle = false)
    {
        if (stats == null || _rootElement == null) return;

        bool isDead = stats.IsDead || stats.CurrHp <= 0;

        // 1. Update Name & Level
        if (_nameLabel != null) _nameLabel.text = stats.Name;
        if (_levelLabel != null) _levelLabel.text = $"Nv. {stats.Level}";

        // 2. Update Key Badge Number
        if (inBattle)
        {
            if (_heroKeyBadge != null) _heroKeyBadge.style.display = DisplayStyle.None;
        }
        else
        {
            if (_heroKeyBadge != null) _heroKeyBadge.style.display = DisplayStyle.Flex;
            if (_keyNumberLabel != null) _keyNumberLabel.text = keyNumber.ToString();
        }

        // 3. Update Fill Bars & Colors
        float hpRatio = stats.MaxHp > 0 ? Mathf.Clamp01((float)stats.CurrHp / stats.MaxHp) : 0f;
        float ppRatio = stats.MaxPp > 0 ? Mathf.Clamp01((float)stats.CurrPp / stats.MaxPp) : 0f;

        if (_hpFillBar != null)
        {
            _hpFillBar.style.width = Length.Percent(hpRatio * 100f);

            // Clean previous state classes
            _hpFillBar.RemoveFromClassList("bar-warning");
            _hpFillBar.RemoveFromClassList("bar-danger");

            if (isDead)
            {
                _hpFillBar.AddToClassList("bar-danger");
            }
            else if (hpRatio < 0.3f)
            {
                _hpFillBar.AddToClassList("bar-warning");
            }
        }

        if (_ppFillBar != null)
        {
            _ppFillBar.style.width = Length.Percent(ppRatio * 100f);
        }

        // 4. Update Formatted Numerical Values with Rich Text formatting
        if (_hpValLabel != null)
        {
            string currentHpColor;
            if (stats.CurrHp == 0)
            {
                currentHpColor = "#E74C3C"; // Red
            }
            else if (hpRatio < 0.3f)
            {
                currentHpColor = "#F1C40F"; // Yellow
            }
            else
            {
                currentHpColor = "#FFFFFF"; // White
            }

            _hpValLabel.text = $"<color={currentHpColor}>{stats.CurrHp}</color>/{stats.MaxHp}";
        }

        if (_ppValLabel != null)
        {
            string currentPpColor = (stats.CurrPp == 0) ? "#E74C3C" : "#FFFFFF";
            _ppValLabel.text = $"<color={currentPpColor}>{stats.CurrPp}</color>/{stats.MaxPp}";
        }

        // 5. Update Avatar Texture from Resources
        if (_portraitImg != null && stats.TurnBarImage != null)
        {
            _portraitImg.style.backgroundImage = Background.FromSprite(stats.TurnBarImage);
        }

        if (_elementGem != null)
        {
            GameEvents.Database.RequestElementIcon(stats.BaseElement, (elemSprite) =>
            {
                _elementGem.style.backgroundImage = Background.FromSprite(elemSprite);
            });
        }

        // 6. Toggle Active Hero & Death Visual States
        SetCardActive(isActiveHero);
        SetCardDeadState(isDead);

        // Ensure Card is visible
        SetVisible(true);
    }

    /// <summary>
    /// Applies or removes the grayscaled/dimmed dead state styling.
    /// </summary>
    public void SetCardDeadState(bool isDead)
    {
        if (_rootElement == null) return;

        if (isDead)
        {
            _rootElement.AddToClassList("hero-dead");
        }
        else
        {
            _rootElement.RemoveFromClassList("hero-dead");
        }
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