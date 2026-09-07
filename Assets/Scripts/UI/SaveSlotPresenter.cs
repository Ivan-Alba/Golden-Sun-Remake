using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Presenter for individual save/load slot elements inside UI Toolkit menus.
/// Binds SaveHeaderData DTOs to UXML VisualElement instances, handling Base64 thumbnail decoding,
/// party roster previews, read-only slot states, new save overlay layer, and focus/hover callbacks.
/// </summary>
public class SaveSlotPresenter
{
    #region UXML Element Names & Classes

    private const string ELEM_SLOT_CONTAINER = "slot-container";
    private const string ELEM_SLOT_NUMBER = "slot-number-label";
    private const string ELEM_CHAPTER_TITLE = "chapter-title-label";
    private const string ELEM_LOCATION_NAME = "location-name-label";

    // Split Date/Time Labels for Column 4 Alignment
    private const string ELEM_DATE_LABEL = "date-label";
    private const string ELEM_TIME_LABEL = "time-label";

    private const string ELEM_PLAYTIME = "playtime-label";
    private const string ELEM_GOLD = "gold-label";
    private const string ELEM_THUMBNAIL = "thumbnail-image";

    private const string ELEM_PARTY_CONTAINER = "party-preview-container";
    private const string ELEM_DJINN_CONTAINER = "djinn-preview-container";

    private const string ELEM_VENUS_COUNT = "venus-count-label";
    private const string ELEM_MARS_COUNT = "mars-count-label";
    private const string ELEM_JUPITER_COUNT = "jupiter-count-label";
    private const string ELEM_MERCURY_COUNT = "mercury-count-label";

    private const string CLASS_EMPTY_SLOT = "save-slot--empty";
    private const string CLASS_FILLED_SLOT = "save-slot--filled";
    private const string CLASS_READONLY_SLOT = "save-slot--readonly";
    private const string CLASS_NEW_SLOT = "save-slot--new";

    #endregion

    #region Private Fields

    private readonly VisualElement rootElement;
    private readonly Label slotNumberLabel;
    private readonly Label chapterTitleLabel;
    private readonly Label locationNameLabel;

    // References to split date/time labels
    private readonly Label dateLabel;
    private readonly Label timeLabel;

    private readonly Label playTimeLabel;
    private readonly Label goldLabel;
    private readonly VisualElement thumbnailElement;

    private readonly VisualElement partyContainer;
    private readonly VisualElement djinnContainer;

    private readonly Label venusCountLabel;
    private readonly Label marsCountLabel;
    private readonly Label jupiterCountLabel;
    private readonly Label mercuryCountLabel;

    private int currentSlotIndex;
    private Action<int> onSlotSelectedCallback;
    private Action<int> onSlotHoveredCallback;

    private bool isReadOnly = false;

    #endregion

    #region Public Properties

    public VisualElement RootElement => rootElement;
    public int SlotIndex => currentSlotIndex;
    public bool IsReadOnly => isReadOnly;

    #endregion

    #region Constructor

    public SaveSlotPresenter(VisualElement visualElement)
    {
        rootElement = visualElement?.Q<VisualElement>(ELEM_SLOT_CONTAINER) ?? visualElement ?? throw new ArgumentNullException(nameof(visualElement));

        // Query Child UI Toolkit Elements
        slotNumberLabel = rootElement.Q<Label>(ELEM_SLOT_NUMBER);
        chapterTitleLabel = rootElement.Q<Label>(ELEM_CHAPTER_TITLE);
        locationNameLabel = rootElement.Q<Label>(ELEM_LOCATION_NAME);

        // Query new split date and time labels
        dateLabel = rootElement.Q<Label>(ELEM_DATE_LABEL);
        timeLabel = rootElement.Q<Label>(ELEM_TIME_LABEL);

        playTimeLabel = rootElement.Q<Label>(ELEM_PLAYTIME);
        goldLabel = rootElement.Q<Label>(ELEM_GOLD);
        thumbnailElement = rootElement.Q<VisualElement>(ELEM_THUMBNAIL);

        partyContainer = rootElement.Q<VisualElement>(ELEM_PARTY_CONTAINER);
        djinnContainer = rootElement.Q<VisualElement>(ELEM_DJINN_CONTAINER);

        venusCountLabel = rootElement.Q<Label>(ELEM_VENUS_COUNT);
        marsCountLabel = rootElement.Q<Label>(ELEM_MARS_COUNT);
        jupiterCountLabel = rootElement.Q<Label>(ELEM_JUPITER_COUNT);
        mercuryCountLabel = rootElement.Q<Label>(ELEM_MERCURY_COUNT);

        // Register Interaction Callbacks
        rootElement.RegisterCallback<ClickEvent>(OnClick);
        rootElement.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
        rootElement.RegisterCallback<FocusEvent>(OnFocus);
    }

    #endregion

    #region Binding & Data Setup

    /// <summary>
    /// Binds header data to UI Toolkit elements or formats the slot as empty.
    /// </summary>
    public void SetupSlot(int slotIndex, SaveHeaderData headerData, bool isDjinnSystemUnlocked, Action<int> onSelected, Action<int> onHovered = null)
    {
        currentSlotIndex = slotIndex;
        onSlotSelectedCallback = onSelected;
        onSlotHoveredCallback = onHovered;

        SetNewSaveState(false);

        if (slotNumberLabel != null)
        {
            slotNumberLabel.text = slotIndex == 0 ? "AUTOGUARDADO" : $"RANURA {slotIndex}";
        }

        if (headerData == null || string.IsNullOrEmpty(headerData.saveTimestamp))
        {
            SetupEmptySlot();
            return;
        }

        SetupFilledSlot(headerData, isDjinnSystemUnlocked);
    }

    /// <summary>
    /// Enables or disables the 'New Save' overlay layer.
    /// </summary>
    public void SetNewSaveState(bool isNewSave)
    {
        if (isNewSave)
        {
            rootElement.AddToClassList(CLASS_NEW_SLOT);
        }
        else
        {
            rootElement.RemoveFromClassList(CLASS_NEW_SLOT);
        }
    }

    /// <summary>
    /// Toggles the read-only visual state (e.g., disabling AutoSave selection during Save mode).
    /// </summary>
    public void SetReadOnlyState(bool readOnly)
    {
        isReadOnly = readOnly;

        if (isReadOnly)
        {
            rootElement.AddToClassList(CLASS_READONLY_SLOT);
            rootElement.focusable = false;
        }
        else
        {
            rootElement.RemoveFromClassList(CLASS_READONLY_SLOT);
            rootElement.focusable = true;
        }
    }

    private void SetupEmptySlot()
    {
        rootElement.RemoveFromClassList(CLASS_FILLED_SLOT);
        rootElement.AddToClassList(CLASS_EMPTY_SLOT);

        if (chapterTitleLabel != null) chapterTitleLabel.text = "--- RANURA VACÍA ---";
        if (locationNameLabel != null) locationNameLabel.text = string.Empty;
        if (dateLabel != null) dateLabel.text = string.Empty;
        if (timeLabel != null) timeLabel.text = string.Empty;
        if (playTimeLabel != null) playTimeLabel.text = string.Empty;
        if (goldLabel != null) goldLabel.text = string.Empty;

        if (thumbnailElement != null)
        {
            thumbnailElement.style.backgroundImage = null;
        }

        ClearPartyContainer();

        if (djinnContainer != null)
        {
            djinnContainer.style.display = DisplayStyle.None;
        }
    }

    private void SetupFilledSlot(SaveHeaderData headerData, bool isDjinnSystemUnlocked)
    {
        rootElement.RemoveFromClassList(CLASS_EMPTY_SLOT);
        rootElement.AddToClassList(CLASS_FILLED_SLOT);

        // 1. Text & Location Setup
        if (locationNameLabel != null) locationNameLabel.text = headerData.locationName;

        // Split "yyyy-MM-dd HH:mm:ss" string into separate Date and Time labels
        if (!string.IsNullOrEmpty(headerData.saveTimestamp))
        {
            string[] timestampParts = headerData.saveTimestamp.Split(' ');
            if (dateLabel != null) dateLabel.text = timestampParts.Length > 0 ? timestampParts[0] : string.Empty;
            if (timeLabel != null) timeLabel.text = timestampParts.Length > 1 ? timestampParts[1] : string.Empty;
        }

        // 2. Dynamic Chapter Title Lookup via FlagSO
        ResolveChapterTitle(headerData.lastChronicleFlagSlug);

        // 3. Formatting Time & Gold
        TimeSpan timeSpan = TimeSpan.FromSeconds(headerData.totalTime);
        if (playTimeLabel != null) playTimeLabel.text = $"{((int)timeSpan.TotalHours):D2}h {timeSpan.Minutes:D2}m";
        if (goldLabel != null) goldLabel.text = $"{headerData.totalGold} G";

        // 4. Decode Base64 Screenshot Thumbnail
        if (thumbnailElement != null && !string.IsNullOrEmpty(headerData.saveThumbnailBase64))
        {
            Sprite thumbnailSprite = SaveScreenCapture.Base64ToSprite(headerData.saveThumbnailBase64);
            if (thumbnailSprite != null)
            {
                thumbnailElement.style.backgroundImage = new StyleBackground(thumbnailSprite);
            }
            else
            {
                thumbnailElement.style.backgroundImage = null;
            }
        }

        // 5. Render Active Party Preview using vertical hero item cards
        RenderPartyPreview(headerData.partyPreview);

        // 6. Render Djinn Metrics if unlocked
        if (djinnContainer != null)
        {
            djinnContainer.style.display = isDjinnSystemUnlocked ? DisplayStyle.Flex : DisplayStyle.None;

            if (isDjinnSystemUnlocked && headerData.djinnCountByElement != null && headerData.djinnCountByElement.Length >= 4)
            {
                if (venusCountLabel != null) venusCountLabel.text = headerData.djinnCountByElement[0].ToString();
                if (marsCountLabel != null) marsCountLabel.text = headerData.djinnCountByElement[1].ToString();
                if (jupiterCountLabel != null) jupiterCountLabel.text = headerData.djinnCountByElement[2].ToString();
                if (mercuryCountLabel != null) mercuryCountLabel.text = headerData.djinnCountByElement[3].ToString();
            }
        }
    }

    private void ResolveChapterTitle(string flagSlug)
    {
        if (chapterTitleLabel == null) return;

        if (string.IsNullOrEmpty(flagSlug))
        {
            chapterTitleLabel.text = "Prólogo";
            return;
        }

        // Query FlagSO asynchronously via database event channel
        GameEvents.Database.RequestFlagBySlug(flagSlug, flagAsset =>
        {
            if (flagAsset != null && flagAsset.ChapterTitle != null && !flagAsset.ChapterTitle.IsEmpty)
            {
                chapterTitleLabel.text = flagAsset.ChapterTitle.GetLocalizedString();
            }
            else if (flagAsset != null && flagAsset.MilestoneTitle != null && !flagAsset.MilestoneTitle.IsEmpty)
            {
                chapterTitleLabel.text = flagAsset.MilestoneTitle.GetLocalizedString();
            }
            else
            {
                chapterTitleLabel.text = "Prólogo";
            }
        });
    }

    private void RenderPartyPreview(List<HeroPreviewData> party)
    {
        ClearPartyContainer();

        if (party == null || partyContainer == null) return;

        for (int i = 0; i < party.Count; i++)
        {
            HeroPreviewData hero = party[i];

            // Contenedor vertical de héroe
            VisualElement heroBox = new VisualElement();
            heroBox.AddToClassList("hero-preview-vertical-item");

            // Recuadro para la foto de perfil/retrato
            VisualElement avatarPortrait = new VisualElement();
            avatarPortrait.AddToClassList("hero-avatar-portrait");

            // Solicitamos el retrato al GameManager usando el nombre guardado en la partida
            if (!string.IsNullOrEmpty(hero.heroName))
            {
                GameEvents.Hero.RequestHeroPortrait(hero.heroName, portraitSprite =>
                {
                    if (portraitSprite != null)
                    {
                        avatarPortrait.style.backgroundImage = new StyleBackground(portraitSprite);
                    }
                });
            }

            Label nameLabel = new Label(hero.heroName);
            nameLabel.AddToClassList("hero-preview-name");

            Label levelLabel = new Label($"Nv. {hero.level}");
            levelLabel.AddToClassList("hero-preview-level");

            heroBox.Add(avatarPortrait);
            heroBox.Add(nameLabel);
            heroBox.Add(levelLabel);

            partyContainer.Add(heroBox);
        }
    }

    private void ClearPartyContainer()
    {
        if (partyContainer != null)
        {
            partyContainer.Clear();
        }
    }

    #endregion

    #region Event Handlers

    private void OnClick(ClickEvent evt)
    {
        if (isReadOnly) return;

        GameEvents.Audio.RequestPlaySFX(SFXType.UI_Confirm);
        onSlotSelectedCallback?.Invoke(currentSlotIndex);
    }

    private void OnPointerEnter(PointerEnterEvent evt)
    {
        onSlotHoveredCallback?.Invoke(currentSlotIndex);
    }

    private void OnFocus(FocusEvent evt)
    {
        onSlotHoveredCallback?.Invoke(currentSlotIndex);
    }

    #endregion
}