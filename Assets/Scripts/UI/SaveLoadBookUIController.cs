using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Controller governing the ancient Save/Load Book UI using UI Toolkit.
/// Renders a two-page symmetrical book:
/// - Left Page: Displays current playthrough chronicle, total gold, Djinn summary, and playtime (Save Mode), or preview of the highlighted slot (Load Mode).
/// - Right Page: Pinboard for the fixed AutoSave slot and scrollable list of active user save slots.
/// Reacts to decoupled events via GameEvents.
/// </summary>
public class SaveLoadBookUIController : MonoBehaviour
{
    #region Serialized Fields

    [Header("UI Toolkit Document")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Configuration")]
    [SerializeField] private int totalSaveSlots = 5;
    [SerializeField] private VisualTreeAsset slotTemplateUXML;

    #endregion

    #region UXML Element Names

    private const string ELEM_BOOK_ROOT = "save-book-root";
    private const string ELEM_BOOK_TITLE = "book-mode-title-label";

    // Left Page Elements (Chronicle & Journal)
    private const string ELEM_CHAPTER_TITLE = "chronicle-chapter-title";
    private const string ELEM_MILESTONE_TITLE = "chronicle-milestone-title";
    private const string ELEM_ILLUSTRATION = "chronicle-illustration-image";
    private const string ELEM_SUMMARY_TEXT = "chronicle-summary-text";

    private const string ELEM_LEFT_GOLD_COUNT = "chronicle-gold-count";
    private const string ELEM_LEFT_PLAYTIME_COUNT = "chronicle-playtime-count";

    private const string ELEM_LEFT_DJINN_CONTAINER = "global-djinn-container";
    private const string ELEM_LEFT_VENUS_COUNT = "global-venus-count";
    private const string ELEM_LEFT_MARS_COUNT = "global-mars-count";
    private const string ELEM_LEFT_JUPITER_COUNT = "global-jupiter-count";
    private const string ELEM_LEFT_MERCURY_COUNT = "global-mercury-count";

    // Right Page Elements (Slots Container & AutoSave Pinboard)
    private const string ELEM_AUTOSAVE_CONTAINER = "autosave-slot-container";
    private const string ELEM_SLOTS_CONTAINER = "slots-scroll-container";

    #endregion

    #region Private Fields

    private VisualElement rootElement;
    private VisualElement bookRootContainer;
    private Label bookModeTitleLabel;

    // Left Page UXML References
    private Label chapterTitleLabel;
    private Label milestoneTitleLabel;
    private VisualElement illustrationImage;
    private Label summaryTextLabel;

    private Label leftGoldCountLabel;
    private Label leftPlaytimeCountLabel;

    private VisualElement leftDjinnContainer;
    private Label leftVenusCountLabel;
    private Label leftMarsCountLabel;
    private Label leftJupiterCountLabel;
    private Label leftMercuryCountLabel;

    // Right Page UXML References
    private VisualElement autosaveSlotContainer;
    private VisualElement slotsScrollContainer;

    // Active Presenters Cache
    private SaveSlotPresenter autosavePresenter;
    private readonly List<SaveSlotPresenter> activeSlotPresenters = new List<SaveSlotPresenter>();

    private GameEvents.UI.SaveBookMode currentMode = GameEvents.UI.SaveBookMode.Save;
    private bool isOpen = false;

    // Playtime Live Tracking Fields (for active Save Mode live ticking)
    private float cachedLivePlayTime = 0f;
    private int lastDisplayedSecond = -1;

    /// <summary>
    /// Event triggered when the Save/Load book is closed.
    /// </summary>
    public static event Action OnBookClosed;

    #endregion

    #region Helper Structs

    private struct SlotHeaderWrapper
    {
        public int SlotIndex;
        public SaveHeaderData Header;
        public DateTime ParsedTimestamp;
    }

    #endregion

    #region Unity Lifecycle & Event Subscriptions

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        BindUXMLElements();
        HideBookImmediately();
    }

    private void OnEnable()
    {
        GameEvents.UI.OnOpenSaveLoadBookRequested += HandleOpenRequested;

        GameEvents.Input.OnMenuCancelPressed += HandleCancelPressed;

        GameEvents.SaveLoad.OnSaveCompleted += HandleSaveCompleted;
        GameEvents.SaveLoad.OnLoadCompleted += HandleLoadCompleted;
    }

    private void OnDisable()
    {
        GameEvents.UI.OnOpenSaveLoadBookRequested -= HandleOpenRequested;

        GameEvents.Input.OnMenuCancelPressed -= HandleCancelPressed;

        GameEvents.SaveLoad.OnSaveCompleted -= HandleSaveCompleted;
        GameEvents.SaveLoad.OnLoadCompleted -= HandleLoadCompleted;
    }

    private void Update()
    {
        // Si el libro está abierto en modo Save, mantenemos el tiempo de juego corriendo en vivo en la página izquierda
        if (!isOpen || currentMode != GameEvents.UI.SaveBookMode.Save) return;

        cachedLivePlayTime += Time.unscaledDeltaTime;
        int currentSecond = Mathf.FloorToInt(cachedLivePlayTime);

        if (currentSecond != lastDisplayedSecond)
        {
            lastDisplayedSecond = currentSecond;
            UpdateLivePlaytimeText();
        }
    }

    #endregion

    #region UI Toolkit Binding & Initialization

    private void BindUXMLElements()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        rootElement = uiDocument.rootVisualElement;

        bookRootContainer = rootElement.Q<VisualElement>(ELEM_BOOK_ROOT);
        bookModeTitleLabel = rootElement.Q<Label>(ELEM_BOOK_TITLE);

        // Left Page
        chapterTitleLabel = rootElement.Q<Label>(ELEM_CHAPTER_TITLE);
        milestoneTitleLabel = rootElement.Q<Label>(ELEM_MILESTONE_TITLE);
        illustrationImage = rootElement.Q<VisualElement>(ELEM_ILLUSTRATION);
        summaryTextLabel = rootElement.Q<Label>(ELEM_SUMMARY_TEXT);

        leftGoldCountLabel = rootElement.Q<Label>(ELEM_LEFT_GOLD_COUNT);
        leftPlaytimeCountLabel = rootElement.Q<Label>(ELEM_LEFT_PLAYTIME_COUNT);

        leftDjinnContainer = rootElement.Q<VisualElement>(ELEM_LEFT_DJINN_CONTAINER);
        leftVenusCountLabel = rootElement.Q<Label>(ELEM_LEFT_VENUS_COUNT);
        leftMarsCountLabel = rootElement.Q<Label>(ELEM_LEFT_MARS_COUNT);
        leftJupiterCountLabel = rootElement.Q<Label>(ELEM_LEFT_JUPITER_COUNT);
        leftMercuryCountLabel = rootElement.Q<Label>(ELEM_LEFT_MERCURY_COUNT);

        // Right Page
        autosaveSlotContainer = rootElement.Q<VisualElement>(ELEM_AUTOSAVE_CONTAINER);
        slotsScrollContainer = rootElement.Q<VisualElement>(ELEM_SLOTS_CONTAINER);
    }

    #endregion

    #region Event Handlers & Open/Close Flow

    private void HandleOpenRequested(GameEvents.UI.SaveBookMode mode)
    {
        if (mode == GameEvents.UI.SaveBookMode.Load && !HasAnySaveFile())
        {
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Error);
            GameEvents.UI.TriggerShowNotification(NotificationType.Warning, "No existen datos de guardado para cargar.");
            return;
        }

        currentMode = mode;
        OpenBook();
    }

    private void HandleCancelPressed()
    {
        if (isOpen)
        {
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Cancel);
            CloseBook();
        }
    }

    private void OpenBook()
    {
        if (bookRootContainer == null) BindUXMLElements();

        isOpen = true;

        if (bookRootContainer != null)
        {
            bookRootContainer.style.display = DisplayStyle.Flex;
        }

        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.InMenu);

        if (bookModeTitleLabel != null)
        {
            bookModeTitleLabel.text = currentMode == GameEvents.UI.SaveBookMode.Save
                ? "REGISTRO DE AVENTURA (GUARDAR)"
                : "MEMORIAS DE WEYARD (CARGAR)";
        }

        // 1. In Save mode, query live game state via events and render current game state on the left page ONCE
        if (currentMode == GameEvents.UI.SaveBookMode.Save)
        {
            RenderLiveChroniclePage();
        }

        // 2. Render save slots on the right page
        RefreshRightSlotsPage();
    }

    private void CloseBook()
    {
        if (!isOpen) return;

        isOpen = false;

        if (bookRootContainer != null)
        {
            bookRootContainer.style.display = DisplayStyle.None;
        }

        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);

        OnBookClosed?.Invoke();
    }

    private void HideBookImmediately()
    {
        isOpen = false;

        if (bookRootContainer != null)
        {
            bookRootContainer.style.display = DisplayStyle.None;
        }
    }

    private bool HasAnySaveFile()
    {
        for (int i = 0; i <= totalSaveSlots; i++)
        {
            if (SaveManager.HasSaveFile(i)) return true;
        }
        return false;
    }

    private int FindFirstEmptySlot()
    {
        for (int i = 1; i <= totalSaveSlots; i++)
        {
            if (!SaveManager.HasSaveFile(i)) return i;
        }
        return -1;
    }

    #endregion

    #region Left Page Rendering (Chronicle & Journal)

    private void RenderLeftChroniclePage(SaveHeaderData headerData)
    {
        string lastFlagSlug = headerData != null ? headerData.lastChronicleFlagSlug : string.Empty;

        if (!string.IsNullOrEmpty(lastFlagSlug))
        {
            GameEvents.Database.RequestFlagBySlug(lastFlagSlug, flagAsset =>
            {
                if (flagAsset != null)
                {
                    if (chapterTitleLabel != null)
                        chapterTitleLabel.text = (flagAsset.ChapterTitle != null && !flagAsset.ChapterTitle.IsEmpty)
                            ? flagAsset.ChapterTitle.GetLocalizedString()
                            : string.Empty;

                    if (milestoneTitleLabel != null)
                        milestoneTitleLabel.text = (flagAsset.MilestoneTitle != null && !flagAsset.MilestoneTitle.IsEmpty)
                            ? flagAsset.MilestoneTitle.GetLocalizedString()
                            : string.Empty;

                    if (summaryTextLabel != null)
                        summaryTextLabel.text = (flagAsset.ChronicleSummaryText != null && !flagAsset.ChronicleSummaryText.IsEmpty)
                            ? flagAsset.ChronicleSummaryText.GetLocalizedString()
                            : string.Empty;

                    if (illustrationImage != null)
                    {
                        illustrationImage.style.backgroundImage = flagAsset.ChronicleIllustration != null
                            ? new StyleBackground(flagAsset.ChronicleIllustration)
                            : null;
                    }
                }
            });
        }
        else
        {
            ClearChronicleVisuals();
        }

        // Render total gold metric
        if (leftGoldCountLabel != null)
        {
            leftGoldCountLabel.text = headerData != null ? $"{headerData.totalGold} G" : "0 G";
        }

        // Render playtime metric using hh:mm:ss format matching MainMenu standards
        if (leftPlaytimeCountLabel != null && headerData != null)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(headerData.totalTime);
            leftPlaytimeCountLabel.text = $"{((int)timeSpan.TotalHours):D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }
        else if (leftPlaytimeCountLabel != null)
        {
            leftPlaytimeCountLabel.text = "00:00:00";
        }

        // Render Djinn counters directly from headerData and manage visibility
        int[] djinnCounts = (headerData != null && headerData.djinnCountByElement != null) ? headerData.djinnCountByElement : new int[4];
        int totalDjinns = djinnCounts[0] + djinnCounts[1] + djinnCounts[2] + djinnCounts[3];

        if (leftDjinnContainer != null)
        {
            leftDjinnContainer.style.display = totalDjinns > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (leftVenusCountLabel != null) leftVenusCountLabel.text = djinnCounts[0].ToString();
        if (leftMarsCountLabel != null) leftMarsCountLabel.text = djinnCounts[1].ToString();
        if (leftJupiterCountLabel != null) leftJupiterCountLabel.text = djinnCounts[2].ToString();
        if (leftMercuryCountLabel != null) leftMercuryCountLabel.text = djinnCounts[3].ToString();
    }

    /// <summary>
    /// Renders the left chronicle page in real-time using decoupled query events for active live game data.
    /// </summary>
    private void RenderLiveChroniclePage()
    {
        // 1. Request last active chronicle flag slug from WorldStateController
        GameEvents.World.RequestLastChronicleFlagSlug(flagSlug =>
        {
            if (!string.IsNullOrEmpty(flagSlug))
            {
                GameEvents.Database.RequestFlagBySlug(flagSlug, flagAsset =>
                {
                    if (flagAsset != null)
                    {
                        if (chapterTitleLabel != null)
                            chapterTitleLabel.text = (flagAsset.ChapterTitle != null && !flagAsset.ChapterTitle.IsEmpty)
                                ? flagAsset.ChapterTitle.GetLocalizedString()
                                : string.Empty;

                        if (milestoneTitleLabel != null)
                            milestoneTitleLabel.text = (flagAsset.MilestoneTitle != null && !flagAsset.MilestoneTitle.IsEmpty)
                                ? flagAsset.MilestoneTitle.GetLocalizedString()
                                : string.Empty;

                        if (summaryTextLabel != null)
                            summaryTextLabel.text = (flagAsset.ChronicleSummaryText != null && !flagAsset.ChronicleSummaryText.IsEmpty)
                                ? flagAsset.ChronicleSummaryText.GetLocalizedString()
                                : string.Empty;

                        if (illustrationImage != null)
                        {
                            illustrationImage.style.backgroundImage = flagAsset.ChronicleIllustration != null
                                ? new StyleBackground(flagAsset.ChronicleIllustration)
                                : null;
                        }
                    }
                });
            }
            else
            {
                ClearChronicleVisuals();
            }
        });

        // 2. Request live total gold
        GameEvents.Party.RequestGold(gold =>
        {
            if (leftGoldCountLabel != null)
            {
                leftGoldCountLabel.text = $"{gold} G";
            }
        });

        // 3. Request live playtime and synchronize cache for live ticking
        GameEvents.GameState.RequestPlayTime(playTime =>
        {
            cachedLivePlayTime = playTime;
            lastDisplayedSecond = Mathf.FloorToInt(cachedLivePlayTime);
            UpdateLivePlaytimeText();
        });

        // 4. Request live elemental Djinn counts across active and reserve party
        GameEvents.Party.RequestDjinnCounts(djinnCounts =>
        {
            int[] counts = djinnCounts ?? new int[4];
            int totalDjinns = counts[0] + counts[1] + counts[2] + counts[3];

            if (leftDjinnContainer != null)
            {
                leftDjinnContainer.style.display = totalDjinns > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (leftVenusCountLabel != null) leftVenusCountLabel.text = counts[0].ToString();
            if (leftMarsCountLabel != null) leftMarsCountLabel.text = counts[1].ToString();
            if (leftJupiterCountLabel != null) leftJupiterCountLabel.text = counts[2].ToString();
            if (leftMercuryCountLabel != null) leftMercuryCountLabel.text = counts[3].ToString();
        });
    }

    private void ClearChronicleVisuals()
    {
        if (chapterTitleLabel != null) chapterTitleLabel.text = string.Empty;
        if (milestoneTitleLabel != null) milestoneTitleLabel.text = string.Empty;
        if (summaryTextLabel != null) summaryTextLabel.text = string.Empty;
        if (illustrationImage != null) illustrationImage.style.backgroundImage = null;
    }

    private void UpdateLivePlaytimeText()
    {
        if (leftPlaytimeCountLabel != null)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(cachedLivePlayTime);
            leftPlaytimeCountLabel.text = $"{((int)timeSpan.TotalHours):D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }
    }

    #endregion

    #region Right Page Rendering (Slots & AutoSave Pinboard)

    private void RefreshRightSlotsPage()
    {
        if (slotsScrollContainer == null || autosaveSlotContainer == null) return;

        autosaveSlotContainer.Clear();
        slotsScrollContainer.Clear();
        activeSlotPresenters.Clear();

        // Check live unlocked Djinns to configure the "NEW GAME" slot card correctly
        bool isDjinnUnlocked = false;
        int[] liveDjinnCounts = new int[4];

        GameEvents.Party.RequestDjinnCounts(counts =>
        {
            if (counts != null)
            {
                liveDjinnCounts = counts;
                isDjinnUnlocked = (counts[0] + counts[1] + counts[2] + counts[3]) > 0;
            }
        });

        // 1. Slot 0 (AUTOSAVE) Pinboard setup
        SaveHeaderData autosaveHeader = SaveManager.LoadHeader(0);
        VisualElement autosaveInstance = slotTemplateUXML != null ? slotTemplateUXML.Instantiate() : new VisualElement();

        autosavePresenter = new SaveSlotPresenter(autosaveInstance);

        bool autosaveDjinnUnlocked = isDjinnUnlocked;
        if (autosaveHeader != null && autosaveHeader.djinnCountByElement != null)
        {
            int autoTotal = autosaveHeader.djinnCountByElement[0] + autosaveHeader.djinnCountByElement[1] + autosaveHeader.djinnCountByElement[2] + autosaveHeader.djinnCountByElement[3];
            autosaveDjinnUnlocked = autoTotal > 0;
        }

        autosavePresenter.SetupSlot(0, autosaveHeader, autosaveDjinnUnlocked, OnSlotSelected, OnSlotHovered);

        if (currentMode == GameEvents.UI.SaveBookMode.Save)
        {
            autosavePresenter.SetReadOnlyState(true);
        }
        else
        {
            autosavePresenter.SetReadOnlyState(false);
        }

        autosaveSlotContainer.Add(autosaveInstance);

        // 2. Fetch all existing user save slots sorted descending by date
        List<SlotHeaderWrapper> sortedUserSlots = GetSortedUserSaveSlots();

        SaveSlotPresenter initialTargetPresenter = null;

        if (currentMode == GameEvents.UI.SaveBookMode.Save)
        {
            // Add "NUEVO REGISTRO" card pointing to the first free slot at the top
            int nextAvailableSlot = FindFirstEmptySlot();
            if (nextAvailableSlot > 0)
            {
                VisualElement newSlotInstance = slotTemplateUXML != null ? slotTemplateUXML.Instantiate() : new VisualElement();
                SaveSlotPresenter newSlotPresenter = new SaveSlotPresenter(newSlotInstance);

                newSlotPresenter.SetupSlot(nextAvailableSlot, null, isDjinnUnlocked, OnSlotSelected, OnSlotHovered);
                newSlotPresenter.SetNewSaveState(true);

                activeSlotPresenters.Add(newSlotPresenter);
                slotsScrollContainer.Add(newSlotInstance);

                initialTargetPresenter = newSlotPresenter;
            }

            // Render existing manual slots
            foreach (var slotWrapper in sortedUserSlots)
            {
                VisualElement slotInstance = slotTemplateUXML != null ? slotTemplateUXML.Instantiate() : new VisualElement();
                SaveSlotPresenter presenter = new SaveSlotPresenter(slotInstance);

                bool slotDjinnUnlocked = isDjinnUnlocked;
                if (slotWrapper.Header != null && slotWrapper.Header.djinnCountByElement != null)
                {
                    int wrapperTotal = slotWrapper.Header.djinnCountByElement[0] + slotWrapper.Header.djinnCountByElement[1] + slotWrapper.Header.djinnCountByElement[2] + slotWrapper.Header.djinnCountByElement[3];
                    slotDjinnUnlocked = wrapperTotal > 0;
                }

                presenter.SetupSlot(slotWrapper.SlotIndex, slotWrapper.Header, slotDjinnUnlocked, OnSlotSelected, OnSlotHovered);

                activeSlotPresenters.Add(presenter);
                slotsScrollContainer.Add(slotInstance);

                if (initialTargetPresenter == null)
                {
                    initialTargetPresenter = presenter;
                }
            }
        }
        else if (currentMode == GameEvents.UI.SaveBookMode.Load)
        {
            foreach (var slotWrapper in sortedUserSlots)
            {
                VisualElement slotInstance = slotTemplateUXML != null ? slotTemplateUXML.Instantiate() : new VisualElement();
                SaveSlotPresenter presenter = new SaveSlotPresenter(slotInstance);

                bool slotDjinnUnlocked = false;
                if (slotWrapper.Header != null && slotWrapper.Header.djinnCountByElement != null)
                {
                    int wrapperTotal = slotWrapper.Header.djinnCountByElement[0] + slotWrapper.Header.djinnCountByElement[1] + slotWrapper.Header.djinnCountByElement[2] + slotWrapper.Header.djinnCountByElement[3];
                    slotDjinnUnlocked = wrapperTotal > 0;
                }

                presenter.SetupSlot(slotWrapper.SlotIndex, slotWrapper.Header, slotDjinnUnlocked, OnSlotSelected, OnSlotHovered);

                activeSlotPresenters.Add(presenter);
                slotsScrollContainer.Add(slotInstance);

                if (initialTargetPresenter == null)
                {
                    initialTargetPresenter = presenter;
                }
            }

            if (initialTargetPresenter == null)
            {
                initialTargetPresenter = autosavePresenter;
            }
        }

        if (initialTargetPresenter != null)
        {
            initialTargetPresenter.RootElement.Focus();
            OnSlotHovered(initialTargetPresenter.SlotIndex);
        }
    }

    /// <summary>
    /// Collects existing user save headers (1..totalSaveSlots) and sorts them descending by save timestamp.
    /// </summary>
    private List<SlotHeaderWrapper> GetSortedUserSaveSlots()
    {
        List<SlotHeaderWrapper> list = new List<SlotHeaderWrapper>();

        for (int i = 1; i <= totalSaveSlots; i++)
        {
            if (!SaveManager.HasSaveFile(i)) continue;

            SaveHeaderData header = SaveManager.LoadHeader(i);
            DateTime parsedDate = DateTime.MinValue;

            if (header != null && !string.IsNullOrEmpty(header.saveTimestamp))
            {
                DateTime.TryParse(header.saveTimestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate);
            }

            list.Add(new SlotHeaderWrapper
            {
                SlotIndex = i,
                Header = header,
                ParsedTimestamp = parsedDate
            });
        }

        // Sort descending: newest save files first
        list.Sort((a, b) => b.ParsedTimestamp.CompareTo(a.ParsedTimestamp));

        return list;
    }

    private void OnSlotHovered(int slotIndex)
    {
        // En Modo Save, la pantalla izquierda es estática y NUNCA debe cambiar al hacer hover sobre las ranuras.
        if (currentMode == GameEvents.UI.SaveBookMode.Save) return;

        // Actualizar el estado visual persistente en las tarjetas de la derecha (Modo Load)
        if (autosavePresenter != null)
        {
            autosavePresenter.SetSelectedState(autosavePresenter.SlotIndex == slotIndex);
        }

        foreach (var presenter in activeSlotPresenters)
        {
            presenter.SetSelectedState(presenter.SlotIndex == slotIndex);
        }

        // El comportamiento de hover con previsualización queda reservado exclusivamente para el Modo Load.
        if (currentMode == GameEvents.UI.SaveBookMode.Load)
        {
            SaveHeaderData headerData = SaveManager.LoadHeader(slotIndex);
            RenderLeftChroniclePage(headerData);
        }
    }
    private void OnSlotSelected(int slotIndex)
    {
        if (currentMode == GameEvents.UI.SaveBookMode.Save)
        {
            if (slotIndex == 0) return;

            Debug.Log($"<color=yellow>[SaveLoadBookUIController] Capturing screenshot and saving to Slot {slotIndex}...</color>");

            SaveScreenCapture.CaptureScreenshotBase64(base64Image =>
            {
                Action<GameData> injectThumbnailHandler = null;
                injectThumbnailHandler = (dataToSave) =>
                {
                    if (dataToSave != null && dataToSave.header != null)
                    {
                        dataToSave.header.saveThumbnailBase64 = base64Image;
                    }
                    GameEvents.SaveLoad.OnCollectSaveData -= injectThumbnailHandler;
                };

                GameEvents.SaveLoad.OnCollectSaveData += injectThumbnailHandler;

                GameEvents.SaveLoad.RequestSaveGame(slotIndex);
            });
        }
        else if (currentMode == GameEvents.UI.SaveBookMode.Load)
        {
            if (SaveManager.HasSaveFile(slotIndex))
            {
                Debug.Log($"<color=cyan>[SaveLoadBookUIController] Requesting Load Game from Slot {slotIndex}</color>");
                GameEvents.SaveLoad.RequestLoadGame(slotIndex);
                CloseBook();
            }
            else
            {
                GameEvents.Audio.RequestPlaySFX(SFXType.UI_Error);
                GameEvents.UI.TriggerShowNotification(NotificationType.Warning, "La ranura seleccionada está vacía.");
            }
        }
    }

    private void HandleSaveCompleted(int slotIndex, bool success)
    {
        if (success)
        {
            GameEvents.UI.TriggerShowNotification(NotificationType.Info, $"Partida guardada correctamente en la Ranura {slotIndex}.");
            RefreshRightSlotsPage();
        }
        else
        {
            GameEvents.UI.TriggerShowNotification(NotificationType.Warning, "Error al intentar guardar la partida.");
        }
    }

    private void HandleLoadCompleted(int slotIndex, bool success)
    {
        if (!success)
        {
            GameEvents.UI.TriggerShowNotification(NotificationType.Warning, "Error al cargar el archivo de guardado.");
        }
    }

    #endregion
}