using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Container holding details about a learned psynergy and the source class context.
/// </summary>
public struct LearnedPsynergyEntry
{
    public PsynergySO Psynergy;
    public string SourceClassName;
}

/// <summary>
/// Data DTO representing a hero level-up event, stat gains, and unlocked psynergies.
/// </summary>
public struct HeroLevelUpData
{
    public GameObject HeroObject;
    public string HeroName;
    public int OldLevel;
    public int NewLevel;
    public int HpGain;
    public int PpGain;
    public int AtkGain;
    public int DefGain;
    public int AgiGain;
    public int LuckGain;
    public List<LearnedPsynergyEntry> LearnedPsynergies;
}

/// <summary>
/// Presenter responsible for orchestrating the sequential Dragon Quest / Golden Sun style victory results screen,
/// including experience text notifications, level-up highlights, stat gain cards,
/// class psynergy unlocks, and item drop notifications.
/// </summary>
public class ResultsScreenPresenter
{
    #region UI References & Elements

    private readonly VisualElement _resultsLayerRoot;
    private readonly VisualElement _dialogContainer;
    private readonly Label _dialogLabel;
    private readonly VisualElement _promptIndicator;

    private readonly VisualElement _levelUpCardContainer;
    private readonly Label _levelUpTitleLabel;
    private readonly Label _statHpLabel;
    private readonly Label _statPpLabel;
    private readonly Label _statAtkLabel;
    private readonly Label _statDefLabel;
    private readonly Label _statAgiLabel;

    #endregion

    #region Private State

    private bool _isWaitingForInput;
    private bool _inputTriggered;

    #endregion

    public bool IsFinished { get; private set; }

    public ResultsScreenPresenter(VisualElement resultsLayerRoot)
    {
        _resultsLayerRoot = resultsLayerRoot;

        if (_resultsLayerRoot == null)
        {
            Debug.LogError($"[{nameof(ResultsScreenPresenter)}] CRITICAL: Results layer root visual element is null!");
            return;
        }

        // Dialog Container Elements
        _dialogContainer = _resultsLayerRoot.Q<VisualElement>("results_dialog_container");
        _dialogLabel = _resultsLayerRoot.Q<Label>("label_results_dialog_text");
        _promptIndicator = _resultsLayerRoot.Q<VisualElement>("results_prompt_indicator");

        // Level Up Card Elements
        _levelUpCardContainer = _resultsLayerRoot.Q<VisualElement>("level_up_overlay_container");
        _levelUpTitleLabel = _resultsLayerRoot.Q<Label>("label_levelup_title");
        _statHpLabel = _resultsLayerRoot.Q<Label>("label_stat_hp");
        _statPpLabel = _resultsLayerRoot.Q<Label>("label_stat_pp");
        _statAtkLabel = _resultsLayerRoot.Q<Label>("label_stat_atk");
        _statDefLabel = _resultsLayerRoot.Q<Label>("label_stat_def");
        _statAgiLabel = _resultsLayerRoot.Q<Label>("label_stat_agi");

        HideAll();
    }

    #region Public API & Sequence Execution

    /// <summary>
    /// Starts the sequential victory flow asynchronously.
    /// </summary>
    public IEnumerator PlayResultsSequence(
        IReadOnlyList<GameObject> partyHeroes,
        BattleRewardSummary rewardSummary,
        BattleCameraController cameraController = null)
    {
        IsFinished = false;
        HideAll();

        if (_resultsLayerRoot != null)
        {
            _resultsLayerRoot.RemoveFromClassList("hud-layer--hidden");
        }

        // 1. STEP 1: EXPERIENCE GAINED NOTIFICATION
        yield return ShowDialogStep($"¡El grupo obtiene {rewardSummary.TotalXpEarned} puntos de experiencia!");

        // Apply XP to hero stats & track level-up events with stat delta calculations
        List<HeroLevelUpData> levelUps = ProcessHeroExperience(partyHeroes, rewardSummary.TotalXpEarned);

        // 2. STEP 2: LEVEL UP SEQUENCES (IF ANY HERO LEVELED UP)
        if (levelUps.Count > 0)
        {
            foreach (HeroLevelUpData levelUp in levelUps)
            {
                // TODO: Camera hook for closeup blend
                // if (cameraController != null) cameraController.FocusHeroLevelUp(levelUp.HeroObject);

                // Play SFX & Trigger Level Up Animation Trigger on Hero
                GameEvents.Audio.RequestPlaySFX(SFXType.UI_Confirm);
                if (levelUp.HeroObject.TryGetComponent<Animator>(out var anim))
                {
                    anim.SetTrigger("levelUp");
                }

                ShowLevelUpCard(levelUp);
                yield return ShowDialogStep($"¡{levelUp.HeroName.ToUpper()} ha subido al Nivel {levelUp.NewLevel}!");
                HideLevelUpCard();

                // Display unlocked Psynergies sequence (Golden Sun original format)
                if (levelUp.LearnedPsynergies != null && levelUp.LearnedPsynergies.Count > 0)
                {
                    foreach (LearnedPsynergyEntry entry in levelUp.LearnedPsynergies)
                    {
                        if (entry.Psynergy == null) continue;

                        string psynergyName = string.IsNullOrEmpty(entry.Psynergy.PsynergyName)
                            ? entry.Psynergy.name
                            : entry.Psynergy.PsynergyName;

                        string message = string.IsNullOrEmpty(entry.SourceClassName)
                            ? $"¡{levelUp.HeroName.ToUpper()} ha aprendido {psynergyName.ToUpper()}!"
                            : $"¡{levelUp.HeroName.ToUpper()} aprendió {psynergyName.ToUpper()} del {entry.SourceClassName.ToUpper()}!";

                        yield return ShowDialogStep(message);
                    }
                }
            }

            // TODO: Camera hook to return to wide victory shot
            // if (cameraController != null) cameraController.SetInitialIdle();
        }

        // 3. STEP 3: GOLD OBTAINED NOTIFICATION
        if (rewardSummary.TotalGoldEarned > 0)
        {
            yield return ShowDialogStep($"¡Obtienes {rewardSummary.TotalGoldEarned} monedas de Oro!");
        }

        // 4. STEP 4: ITEM DROPS NOTIFICATIONS
        if (rewardSummary.ItemsObtained != null && rewardSummary.ItemsObtained.Count > 0)
        {
            foreach (ObtainedItemEntry drop in rewardSummary.ItemsObtained)
            {
                if (drop.Item == null) continue;

                string itemName = !string.IsNullOrEmpty(drop.Item.ItemName) ? drop.Item.ItemName : drop.Item.name;
                string enemyName = !string.IsNullOrEmpty(drop.DroppedByEnemyName) ? drop.DroppedByEnemyName.ToUpper() : "EL ENEMIGO";
                string heroName = !string.IsNullOrEmpty(drop.RecipientHeroName) ? drop.RecipientHeroName.ToUpper() : "";

                if (drop.AddedFully)
                {
                    string heroText = !string.IsNullOrEmpty(heroName)
                        ? $" ¡{heroName} lo guarda en su bolsa!"
                        : "";

                    yield return ShowDialogStep($"¡{enemyName} dejó caer {itemName.ToUpper()}!{heroText}");
                }
                else if (drop.AddedPartialOrFull)
                {
                    int storedAmount = drop.QuantityDropped - drop.QuantityOverflow;
                    string heroText = !string.IsNullOrEmpty(heroName) ? $" ¡{heroName} guardó {storedAmount}" : $" Se guardaron {storedAmount}";
                    yield return ShowDialogStep($"¡{enemyName} dejó caer {itemName.ToUpper()}!{heroText}, pero las mochilas se llenaron!");
                }
                else
                {
                    yield return ShowDialogStep($"¡{enemyName} dejó caer {itemName.ToUpper()}, pero las mochilas están llenas!");
                }
            }
        }

        HideAll();
        IsFinished = true;
    }

    /// <summary>
    /// Call this method when the user presses the Confirm/Submit input button.
    /// </summary>
    public void HandleConfirmInput()
    {
        if (_isWaitingForInput)
        {
            _inputTriggered = true;
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Confirm);
        }
    }

    #endregion

    #region Internal Step Helpers & Experience Calculation

    private IEnumerator ShowDialogStep(string text)
    {
        if (_dialogContainer != null) _dialogContainer.style.display = DisplayStyle.Flex;
        if (_dialogLabel != null) _dialogLabel.text = text;

        _isWaitingForInput = true;
        _inputTriggered = false;

        if (_promptIndicator != null) _promptIndicator.style.display = DisplayStyle.Flex;

        while (!_inputTriggered)
        {
            yield return null;
        }

        _isWaitingForInput = false;
        if (_promptIndicator != null) _promptIndicator.style.display = DisplayStyle.None;
    }

    private List<HeroLevelUpData> ProcessHeroExperience(IReadOnlyList<GameObject> partyHeroes, int xpEarned)
    {
        List<HeroLevelUpData> levelUps = new List<HeroLevelUpData>();
        if (partyHeroes == null) return levelUps;

        foreach (GameObject heroObj in partyHeroes)
        {
            if (heroObj == null) continue;

            HeroStats heroStats = heroObj.GetComponent<HeroStats>();
            if (heroStats != null)
            {
                int oldLevel = heroStats.Level;

                // Grant experience to hero stats
                heroStats.AddExperience(xpEarned);

                if (heroStats.Level > oldLevel)
                {
                    HeroBaseProgressionSO progression = heroStats.BaseProgression;
                    LevelStatGrowth statGrowth = (progression != null)
                        ? progression.GetStatGrowthDelta(oldLevel, heroStats.Level)
                        : new LevelStatGrowth();

                    List<LearnedPsynergyEntry> learnedPsynergies = new List<LearnedPsynergyEntry>();

                    // 1. Evaluate Innate Psynergies from Base Progression
                    if (progression != null)
                    {
                        var innateUnlocked = progression.GetPsynergiesUnlockedBetweenLevels(oldLevel, heroStats.Level);
                        foreach (var psy in innateUnlocked)
                        {
                            learnedPsynergies.Add(new LearnedPsynergyEntry
                            {
                                Psynergy = psy,
                                SourceClassName = string.Empty
                            });
                        }
                    }

                    // 2. Evaluate Class Psynergies from Current Hero Class
                    CharacterClassSO currentClass = heroStats.CurrentClass;
                    if (currentClass != null && currentClass.GrantedPsynergies != null)
                    {
                        foreach (var classEntry in currentClass.GrantedPsynergies)
                        {
                            if (classEntry.psynergy != null &&
                                classEntry.requiredLevel > oldLevel &&
                                classEntry.requiredLevel <= heroStats.Level)
                            {
                                learnedPsynergies.Add(new LearnedPsynergyEntry
                                {
                                    Psynergy = classEntry.psynergy,
                                    SourceClassName = currentClass.ClassName
                                });
                            }
                        }
                    }

                    levelUps.Add(new HeroLevelUpData
                    {
                        HeroObject = heroObj,
                        HeroName = heroObj.name,
                        OldLevel = oldLevel,
                        NewLevel = heroStats.Level,
                        HpGain = statGrowth.hpGain,
                        PpGain = statGrowth.ppGain,
                        AtkGain = statGrowth.attackGain,
                        DefGain = statGrowth.defenseGain,
                        AgiGain = statGrowth.agilityGain,
                        LuckGain = statGrowth.luckGain,
                        LearnedPsynergies = learnedPsynergies
                    });
                }
            }
        }

        return levelUps;
    }

    private void ShowLevelUpCard(HeroLevelUpData data)
    {
        if (_levelUpCardContainer != null) _levelUpCardContainer.style.display = DisplayStyle.Flex;

        if (_levelUpTitleLabel != null) _levelUpTitleLabel.text = $"¡{data.HeroName.ToUpper()} SUBE AL NIVEL {data.NewLevel}!";
        if (_statHpLabel != null) _statHpLabel.text = $"+{data.HpGain}";
        if (_statPpLabel != null) _statPpLabel.text = $"+{data.PpGain}";
        if (_statAtkLabel != null) _statAtkLabel.text = $"+{data.AtkGain}";
        if (_statDefLabel != null) _statDefLabel.text = $"+{data.DefGain}";
        if (_statAgiLabel != null) _statAgiLabel.text = $"+{data.AgiGain}";
    }

    private void HideLevelUpCard()
    {
        if (_levelUpCardContainer != null) _levelUpCardContainer.style.display = DisplayStyle.None;
    }

    public void HideAll()
    {
        if (_dialogContainer != null) _dialogContainer.style.display = DisplayStyle.None;
        if (_levelUpCardContainer != null) _levelUpCardContainer.style.display = DisplayStyle.None;
        if (_resultsLayerRoot != null) _resultsLayerRoot.AddToClassList("hud-layer--hidden");
    }

    #endregion
}