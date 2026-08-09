using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PsynergyWorldController : MonoBehaviour
{
    public static PsynergyWorldController Instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private GameObject psynergyPanel;
    [SerializeField] private Transform psynergyChoosePanel;

    private Dictionary<GameObject, Psynergy[]> heroShortcuts = new Dictionary<GameObject, Psynergy[]>();
    private int chooseIndex;

    public Dictionary<GameObject, Psynergy[]> HeroShortcuts { get => heroShortcuts; set => heroShortcuts = value; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Lazy UI cache if not assigned via Inspector
        if (psynergyPanel == null)
        {
            psynergyPanel = GameObject.Find("PsynergyPanel");
        }

        if (psynergyPanel != null && psynergyChoosePanel == null)
        {
            psynergyChoosePanel = psynergyPanel.transform.Find("PsynergyChoosePanel");
        }
    }

    /// <summary>
    /// Clears all existing shortcut data and resets UI elements.
    /// </summary>
    public void ClearShortcuts()
    {
        if (heroShortcuts != null)
        {
            heroShortcuts.Clear();
        }

        if (psynergyChoosePanel != null)
        {
            psynergyChoosePanel.gameObject.SetActive(false);
        }

        chooseIndex = 0;

        // Hide all shortcut icons if panel is assigned
        if (psynergyPanel != null)
        {
            for (int i = 0; i < 4; i++)
            {
                Transform iconTrans = psynergyPanel.transform.Find($"Icon{i}");
                if (iconTrans != null)
                {
                    iconTrans.gameObject.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// Called explicitly from GameManager when starting a new game or loading a save file.
    /// </summary>
    public void Initialize()
    {
        ClearShortcuts();

        if (GameManager.Instance == null) return;

        List<GameObject> party = GameManager.Instance.GetParty();
        if (party == null || party.Count == 0) return;

        foreach (GameObject hero in party)
        {
            if (hero != null && !heroShortcuts.ContainsKey(hero))
            {
                heroShortcuts.Add(hero, new Psynergy[4]);
            }
        }

        if (GameManager.Instance.ActiveHero != null)
        {
            ResfreshShortcutsUI(GameManager.Instance.ActiveHero);
        }
    }

    public void SetShortcut(GameObject hero, Psynergy psynergy, int position)
    {
        if (hero == null || position < 0 || position >= 4) return;

        if (!HeroShortcuts.ContainsKey(hero))
        {
            HeroShortcuts.Add(hero, new Psynergy[4]);
        }

        HeroShortcuts[hero][position] = psynergy;
        ResfreshShortcutsUI(hero);
    }

    public void UsePsynergy(int position, GameObject target)
    {
        if (GameManager.Instance == null || GameManager.Instance.ActiveHero == null) return;

        GameObject activeHero = GameManager.Instance.ActiveHero;

        if (!HeroShortcuts.ContainsKey(activeHero)) return;

        Psynergy usedPsynergy = HeroShortcuts[activeHero][position];
        if (usedPsynergy != null)
        {
            if (target != null)
            {
                Debug.Log($"Used psynergy: {usedPsynergy.SlugName} on {target.name}");

                if (target.CompareTag($"{usedPsynergy.SlugName}Reactive"))
                {
                    PsynergyReactive reactiveScript = target.GetComponent<PsynergyReactive>();
                    if (reactiveScript != null)
                    {
                        reactiveScript.React();
                    }
                }
            }
            else
            {
                // Auto-target logic can be handled here if needed
            }
        }
    }

    public void ChoosePanelMoveUp()
    {
        chooseIndex--;
        RefreshPsynergyChoosePanel();
    }

    public void ChoosePanelMoveDown()
    {
        chooseIndex++;
        RefreshPsynergyChoosePanel();
    }

    public void RefreshPsynergyChoosePanel()
    {
        if (GameManager.Instance == null || GameManager.Instance.ActiveHero == null || psynergyChoosePanel == null) return;

        FighterStats stats = GameManager.Instance.ActiveHero.GetComponent<FighterStats>();
        if (stats == null || stats.Psynergies == null) return;

        List<Psynergy> allPsynergies = stats.Psynergies;
        List<Psynergy> worldPsynergies = new List<Psynergy>();

        for (int i = 0; i < allPsynergies.Count; i++)
        {
            if (allPsynergies[i] != null && allPsynergies[i].IsWorldPsynergy)
            {
                worldPsynergies.Add(allPsynergies[i]);
            }
        }

        if (worldPsynergies.Count == 0) return;

        if (chooseIndex < 0)
        {
            chooseIndex = worldPsynergies.Count - 1;
        }
        else if (chooseIndex >= worldPsynergies.Count)
        {
            chooseIndex = 0;
        }

        Transform maskTrans = psynergyChoosePanel.Find("Mask");
        if (maskTrans == null) return;

        if (worldPsynergies.Count >= 3)
        {
            for (int i = 0; i < 3; i++)
            {
                Transform iconTrans = maskTrans.Find($"Icon{i}");
                if (iconTrans == null) continue;

                Image img = iconTrans.GetComponent<Image>();
                if (img != null)
                {
                    if (i == 2)
                    {
                        int targetIdx = (chooseIndex - 1 < 0) ? worldPsynergies.Count - 1 : chooseIndex - 1;
                        img.sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[targetIdx].SlugName}");
                    }
                    else if (i == 1)
                    {
                        int targetIdx = (chooseIndex + 1 == worldPsynergies.Count) ? 0 : chooseIndex + 1;
                        img.sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[targetIdx].SlugName}");
                    }
                    else
                    {
                        img.sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[chooseIndex].SlugName}");
                    }
                }
                iconTrans.gameObject.SetActive(true);
            }
        }
        else if (worldPsynergies.Count == 2)
        {
            for (int i = 0; i < 2; i++)
            {
                Transform iconTrans = maskTrans.Find($"Icon{i}");
                if (iconTrans == null) continue;

                Image img = iconTrans.GetComponent<Image>();
                if (img != null)
                {
                    int targetIdx = (i == 0) ? chooseIndex : ((chooseIndex + 1 >= worldPsynergies.Count) ? 0 : chooseIndex + 1);
                    img.sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[targetIdx].SlugName}");
                }
                iconTrans.gameObject.SetActive(true);
            }
        }
        else if (worldPsynergies.Count == 1)
        {
            Transform iconTrans = maskTrans.Find("Icon0");
            if (iconTrans != null)
            {
                Image img = iconTrans.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[0].SlugName}");
                }
                iconTrans.gameObject.SetActive(true);
            }
        }
    }

    public void ShowPsynergyChoosePanel(bool active, int position)
    {
        if (GameManager.Instance == null || GameManager.Instance.ActiveHero == null || psynergyChoosePanel == null) return;

        FighterStats stats = GameManager.Instance.ActiveHero.GetComponent<FighterStats>();
        if (stats == null || stats.Psynergies == null) return;

        List<Psynergy> allPsynergies = stats.Psynergies;
        List<Psynergy> worldPsynergies = new List<Psynergy>();

        for (int i = 0; i < allPsynergies.Count; i++)
        {
            if (allPsynergies[i] != null && allPsynergies[i].IsWorldPsynergy)
            {
                worldPsynergies.Add(allPsynergies[i]);
            }
        }

        if (active)
        {
            Transform maskTrans = psynergyChoosePanel.Find("Mask");
            if (maskTrans != null)
            {
                if (worldPsynergies.Count >= 3)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Transform iconTrans = maskTrans.Find($"Icon{i}");
                        if (iconTrans == null) continue;

                        Image img = iconTrans.GetComponent<Image>();
                        if (img != null)
                        {
                            int targetIdx = (i == 2) ? worldPsynergies.Count - 1 : i;
                            img.sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[targetIdx].SlugName}");
                        }
                        iconTrans.gameObject.SetActive(true);
                    }
                }
                else if (worldPsynergies.Count == 2)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        Transform iconTrans = maskTrans.Find($"Icon{i}");
                        if (iconTrans == null) continue;

                        Image img = iconTrans.GetComponent<Image>();
                        if (img != null)
                        {
                            img.sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[i].SlugName}");
                        }
                        iconTrans.gameObject.SetActive(true);
                    }
                }
                else if (worldPsynergies.Count == 1)
                {
                    Transform iconTrans = maskTrans.Find("Icon0");
                    if (iconTrans != null)
                    {
                        Image img = iconTrans.GetComponent<Image>();
                        if (img != null)
                        {
                            img.sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[0].SlugName}");
                        }
                        iconTrans.gameObject.SetActive(true);
                    }
                }
            }

            psynergyChoosePanel.gameObject.SetActive(true);
        }
        else
        {
            if (worldPsynergies.Count > 0 && chooseIndex < worldPsynergies.Count)
            {
                SetShortcut(GameManager.Instance.ActiveHero, worldPsynergies[chooseIndex], position);
            }

            psynergyChoosePanel.gameObject.SetActive(false);
            chooseIndex = 0;
        }
    }

    public void ResfreshShortcutsUI(GameObject hero)
    {
        if (hero == null || psynergyPanel == null) return;

        if (!heroShortcuts.ContainsKey(hero) || heroShortcuts[hero] == null)
        {
            // Hide all icon objects if no shortcuts registered for this hero
            for (int i = 0; i < 4; i++)
            {
                Transform iconTrans = psynergyPanel.transform.Find($"Icon{i}");
                if (iconTrans != null) iconTrans.gameObject.SetActive(false);
            }
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            Transform iconTrans = psynergyPanel.transform.Find($"Icon{i}");
            if (iconTrans == null) continue;

            Psynergy currentPsynergy = HeroShortcuts[hero][i];

            if (currentPsynergy != null)
            {
                Image img = iconTrans.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = Resources.Load<Sprite>($"Images/Psynergy/{currentPsynergy.SlugName}");
                }
                iconTrans.gameObject.SetActive(true);
            }
            else
            {
                iconTrans.gameObject.SetActive(false);
            }
        }
    }
}