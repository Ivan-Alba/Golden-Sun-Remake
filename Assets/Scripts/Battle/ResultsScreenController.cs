using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResultsScreenController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI xpText;
    [SerializeField] private TextMeshProUGUI phText;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private GameObject itemsPanel;
    [SerializeField] private TextMeshProUGUI confirmText;

    private TextMeshProUGUI heroName, heroLevel, heroNextLevel, abilityLearnedName;
    private Slider xpBar;
    private List<GameObject> heroList = new List<GameObject>();
    private bool waitingConfirmation;
    private bool resultsFinished;

    public bool ResultsFinished { get => resultsFinished; set => resultsFinished = value; }

    private void Awake()
    {
        if (confirmText != null)
        {
            confirmText.SetText("Pulsa ESPACIO para continuar");
        }
    }

    /// <summary>
    /// Called directly by BattleManager when Confirm key is pressed during ResultsScreen state.
    /// </summary>
    public void HandleConfirmInput()
    {
        if (waitingConfirmation)
        {
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Confirm);

            waitingConfirmation = false;
            ResultsFinished = true;
        }
    }

    private void SetInitConfig()
    {
        // SET INIT CONFIGURATIONS
        gameObject.GetComponent<CanvasGroup>().alpha = 0;
        gameObject.SetActive(true);
        if (confirmText != null) confirmText.gameObject.SetActive(false);

        if (xpText != null) xpText.SetText("0");
        if (phText != null) phText.SetText("0");
        if (goldText != null) goldText.SetText("0");

        // SET INIT HERO INFO
        for (int i = 0; i < heroList.Count; i++)
        {
            if (!heroList[i].TryGetComponent<HeroStats>(out var heroStats)) continue;

            Transform heroPanel = transform.Find("HeroPanel").Find($"Hero{i}");
            if (heroPanel == null) continue;

            heroPanel.gameObject.SetActive(true);
            heroPanel.Find("Mask").Find("Avatar").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/MainMenu/{heroList[i].name}_avatar");

            heroName = heroPanel.Find("NameText").GetComponent<TextMeshProUGUI>();
            heroLevel = heroPanel.Find("LevelText").GetComponent<TextMeshProUGUI>();
            heroNextLevel = heroPanel.Find("NextLevelText").GetComponent<TextMeshProUGUI>();

            heroName.SetText(heroStats.Name);
            heroLevel.SetText($"NV: {heroStats.Level}");
            heroNextLevel.SetText((heroStats.NextLevelXp - heroStats.CurrentXp).ToString());

            xpBar = heroPanel.Find("XpBar").GetComponent<Slider>();
            xpBar.value = heroStats.CurrentXp;
            xpBar.maxValue = heroStats.NextLevelXp;
        }
    }

    public IEnumerator ShowResults(List<GameObject> heroList, int xpEarned, int phEarned, int goldEarned, Dictionary<ItemSO, int> totalItemsObtained)
    {
        this.heroList = heroList;
        SetInitConfig();

        // CROSSFADE
        CanvasGroup canvasGroup = gameObject.GetComponent<CanvasGroup>();
        while (canvasGroup.alpha < 1)
        {
            canvasGroup.alpha += 0.2f;
            yield return new WaitForSeconds(0.01f);
        }
        yield return new WaitForSeconds(1f);

        // ------------------ ITEMS ------------------
        int initY = 130;
        int offsetY = 60;
        int iterat = 0;
        foreach (KeyValuePair<ItemSO, int> entry in totalItemsObtained)
        {
            Debug.Log($"Enemigo deja caer: {entry.Key.ItemName}");
            GameObject newItem = Instantiate(Resources.Load("Prefabs/UIComponents/ResultScreenItem"), itemsPanel.transform) as GameObject;
            newItem.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, 290);
            newItem.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 0, initY + (offsetY * iterat));
            newItem.transform.Find("Name").GetComponent<TextMeshProUGUI>().text = entry.Key.ItemName;
            newItem.transform.Find("ElementIcon").gameObject.SetActive(false);
            newItem.transform.Find("Icon").GetComponent<Image>().sprite = entry.Key.Icon;
            newItem.transform.Find("Number").GetComponent<TextMeshProUGUI>().text = entry.Value.ToString();

            iterat++;
        }

        GrantLootToParty(totalItemsObtained);

        // -- ANIMATE NUMBERS AND BAR CONFIGURATION --
        int previousValue = 0;
        int stepAmount;
        int countFPS = 30;
        float duration = 0.4f;
        WaitForSeconds Wait = new WaitForSeconds(duration / countFPS);

        // ------------------- XP --------------------
        GameEvents.Audio.RequestPlaySFX(SFXType.UI_Count);

        stepAmount = Mathf.CeilToInt((float)(xpEarned - previousValue) / (countFPS * duration));

        // APPLY EXPERIENCE TO HERO DATA MODEL FIRST
        for (int i = 0; i < heroList.Count; i++)
        {
            if (!heroList[i].TryGetComponent<HeroStats>(out var heroStats)) continue;

            if (heroStats.CurrHp == 0) // IF HERO IS DEAD DOESN'T WIN XP
            {
                transform.Find("HeroPanel").Find($"Hero{i}").GetComponent<CanvasGroup>().alpha = 0.5f;
            }
            else
            {
                // Add experience through method and receive any newly learned abilities
                List<PsynergySO> learnedAbilities = heroStats.AddExperience(xpEarned);

                foreach (PsynergySO learnedPsynergy in learnedAbilities)
                {
                    StartCoroutine(AbilityLearned(i, learnedPsynergy.PsynergyName));
                }
            }
        }

        // ANIMATE XP VALUE DISPLAY AND BARS TO MATCH NEW STATS
        while (previousValue < xpEarned)
        {
            previousValue += stepAmount;

            if (previousValue > xpEarned)
            {
                previousValue = xpEarned;
            }

            for (int i = 0; i < heroList.Count; i++)
            {
                if (!heroList[i].TryGetComponent<HeroStats>(out var heroStats) || heroStats.CurrHp == 0) continue;

                Transform heroPanel = transform.Find("HeroPanel").Find($"Hero{i}");
                xpBar = heroPanel.Find("XpBar").GetComponent<Slider>();
                heroNextLevel = heroPanel.Find("NextLevelText").GetComponent<TextMeshProUGUI>();
                heroLevel = heroPanel.Find("LevelText").GetComponent<TextMeshProUGUI>();

                // Refresh UI Elements according to the updated HeroStats
                xpBar.maxValue = heroStats.NextLevelXp;
                xpBar.value = heroStats.CurrentXp;

                heroLevel.SetText($"NV: {heroStats.Level}");
                heroNextLevel.SetText((heroStats.NextLevelXp - heroStats.CurrentXp).ToString());
            }

            if (xpText != null) xpText.SetText(previousValue.ToString());
            yield return Wait;
        }

        // ----------------- PH ----------------------
        previousValue = 0;
        stepAmount = Mathf.CeilToInt((float)(phEarned - previousValue) / (countFPS * duration));

        while (previousValue < phEarned)
        {
            previousValue += stepAmount;
            if (previousValue > phEarned)
            {
                previousValue = phEarned;
            }
            if (phText != null) phText.SetText(previousValue.ToString());

            yield return Wait;
        }

        // ----------------- GOLD ----------------------
        previousValue = 0;
        stepAmount = Mathf.CeilToInt((float)(goldEarned - previousValue) / (countFPS * duration));

        while (previousValue < goldEarned)
        {
            previousValue += stepAmount;
            if (previousValue > goldEarned)
            {
                previousValue = goldEarned;
            }
            if (goldText != null) goldText.SetText(previousValue.ToString());

            yield return Wait;
        }

        // ADD GOLD TO GAME TOTAL
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TotalGold += goldEarned;
        }

        // SHOW EXIT MESSAGE
        waitingConfirmation = true;
        if (confirmText != null) confirmText.gameObject.SetActive(true);
    }

    private void GrantLootToParty(Dictionary<ItemSO, int> totalItemsObtained)
    {
        foreach (var kvp in totalItemsObtained)
        {
            ItemSO item = kvp.Key;
            int quantity = kvp.Value;

            // Automatically routes KeyItems to GlobalBag or regular items to Hero Backpacks
            int remaining = -1;
            GameEvents.Inventory.RequestAddItem(item, quantity, (result) =>
            {
                remaining = result;
            });
        }

        totalItemsObtained.Clear();
    }

    private IEnumerator AbilityLearned(int index, string psynergySlugName)
    {
        Transform abilityLearnedPanel = transform.Find("HeroPanel").Find($"Hero{index}").Find("LearnAbility");
        if (abilityLearnedPanel == null) yield break;

        abilityLearnedName = abilityLearnedPanel.Find("AbilityName").GetComponent<TextMeshProUGUI>();

        PsynergySO psy = PsynergyDatabase.Instance?.GetPsynergy(psynergySlugName);
        if (psy != null)
        {
            abilityLearnedPanel.GetComponent<Image>().sprite = psy.Icon;
            abilityLearnedName.text = psy.name;
            abilityLearnedPanel.gameObject.SetActive(true);
        }
        yield return null;
    }

    public void Reset()
    {
        resultsFinished = false;
        gameObject.SetActive(false);
        for (int i = 0; i < heroList.Count; i++)
        {
            Transform heroPanel = transform.Find("HeroPanel").Find($"Hero{i}");
            if (heroPanel == null) continue;

            heroPanel.gameObject.SetActive(false);
            heroPanel.GetComponent<CanvasGroup>().alpha = 1;
            heroPanel.Find("LevelText").GetComponent<TextMeshProUGUI>().color = Color.white;
            heroPanel.Find("LearnAbility").gameObject.SetActive(false);
        }

        if (itemsPanel != null)
        {
            for (int i = 0; i < itemsPanel.transform.childCount; i++)
            {
                if (itemsPanel.transform.GetChild(i).gameObject.name != "Items")
                {
                    Destroy(itemsPanel.transform.GetChild(i).gameObject);
                }
            }
        }
    }
}