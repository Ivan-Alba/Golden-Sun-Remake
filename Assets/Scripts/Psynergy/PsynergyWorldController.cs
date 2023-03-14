using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PsynergyWorldController : MonoBehaviour
{
    public static PsynergyWorldController Instance { get; set; }

    public Dictionary<GameObject, Psynergy[]> heroShortcuts;

    //UI
    GameObject psynergyPanel;
    Transform psynergyChoosePanel;
    int chooseIndex;

    public Dictionary<GameObject, Psynergy[]> HeroShortcuts { get => heroShortcuts; set => heroShortcuts = value; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            psynergyPanel = GameObject.Find("PsynergyPanel");
            psynergyChoosePanel = psynergyPanel.transform.Find("PsynergyChoosePanel");
            heroShortcuts = new Dictionary<GameObject, Psynergy[]>();
        }
    }

    private void Start()
    {
        Initialize(GameManager.instance.ActiveHero);
    }

    public void Initialize(GameObject hero)
    {
        Psynergy[] newPsynergyLayer = new Psynergy[4];
        heroShortcuts.Add(GameManager.instance.ActiveHero, newPsynergyLayer);
        ResfreshShortcutsUI(GameManager.instance.ActiveHero);
    }

    public void setShortcut(GameObject hero, Psynergy psynergy, int position)
    {
        if (HeroShortcuts.ContainsKey(hero))
        {
            //Recuperamos psinergías ya asignadas
            Psynergy[] currentShortcuts = HeroShortcuts[hero];

            for (int i = 0; i < 4; i++)
            {
                if (i == position)
                {
                    //Cambiamos psinergía posición
                    currentShortcuts[position] = psynergy;
                }
            }

            //Introducimos el array en el dictionary de heroes con la psinergía cambiada
            heroShortcuts.Add(hero, currentShortcuts);
        }
        else
        {
            //Primera psinergía
            Psynergy[] newShortcuts = new Psynergy[4];
            for (int i = 0; i < 4; i++)
            {
                if (i == position)
                {
                    newShortcuts.SetValue(psynergy, position);
                }
                else
                {
                    newShortcuts.SetValue(null, i);
                }
            }

            //Añadimos array de psinergías al dictionary del heroe
            heroShortcuts.Add(hero, newShortcuts);
        }

        ResfreshShortcutsUI(hero);
    }

    public void ChoosePanelMoveUp()
    {

    }

    public void ShowPsynergyChoosePanel()
    {

        List<Psynergy> allPsynergies = GameManager.instance.ActiveHero.GetComponent<FighterStats>().Psynergies;
        List<Psynergy> worldPsynergies = new List<Psynergy>();

        for (int i = 0; i < allPsynergies.Count; i++)
        {
            if (allPsynergies[i].IsWorldPsynergy)
            {
                worldPsynergies.Add(allPsynergies[i]);
            }
        }

        if (worldPsynergies.Count >= 3)
        {
            for (int i = 0; i < 3; i++)
            {
                if (i == 2)
                {
                    psynergyChoosePanel.Find("Mask").Find($"Icon{i}").GetComponent<Image>().sprite = Resources.Load($"Images/Psynergy/{worldPsynergies[worldPsynergies.Count - 1].SlugName}") as Sprite;
                }
                else
                {
                    psynergyChoosePanel.Find("Mask").Find($"Icon{i}").GetComponent<Image>().sprite = Resources.Load($"Images/Psynergy/{worldPsynergies[i].SlugName}") as Sprite;
                }
                psynergyChoosePanel.Find("Mask").Find($"Icon{i}").gameObject.SetActive(true);
            }

        }
        else if (worldPsynergies.Count == 2)
        {
            for (int i = 0; i < 3; i++)
            {
                if (i != 2)
                {
                    psynergyChoosePanel.Find("Mask").Find($"Icon{i}").GetComponent<Image>().sprite = Resources.Load($"Images/Psynergy/{worldPsynergies[i].SlugName}") as Sprite;
                    psynergyChoosePanel.Find("Mask").Find($"Icon{i}").gameObject.SetActive(true);
                }

            }
        } else if (worldPsynergies.Count == 1)
        {
            psynergyChoosePanel.Find("Mask").Find("Icon0").GetComponent<Image>().sprite = Resources.Load($"Images/Psynergy/{worldPsynergies[0].SlugName}") as Sprite;
            psynergyChoosePanel.Find("Mask").Find("Icon0").gameObject.SetActive(true);
        }

        psynergyChoosePanel.gameObject.SetActive(true);
    }

    public void ResfreshShortcutsUI(GameObject hero)
    {


        for (int i = 0; i < 4; i++)
        {
            GameObject iconObject = psynergyPanel.transform.Find($"Icon{i}").gameObject;

            if (heroShortcuts[hero] != null)
            {
                Psynergy currentPsynergy = (Psynergy)HeroShortcuts[hero].GetValue(i);

                if (currentPsynergy != null)
                {
                    iconObject.GetComponent<Image>().sprite = Resources.Load($"Images/Psynergy/{currentPsynergy.SlugName}") as Sprite;
                    iconObject.SetActive(true);
                }
                else
                {
                    iconObject.SetActive(false);
                }
            }
            else
            {
                iconObject.SetActive(false);
            }
        }

    }
}
