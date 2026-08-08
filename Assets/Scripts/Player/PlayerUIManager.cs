using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUIManager : MonoBehaviour
{
    public static PlayerUIManager Instance { get; private set; }

    [Header("Stamina UI Component")]
    [SerializeField] private GameObject staminaBarObject;
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private Image staminaFillImage;
    [SerializeField] private Color normalStaminaColor = new Color(0.94f, 0.71f, 0f);
    [SerializeField] private Color lowStaminaColor = Color.red;

    [Header("Interaction Icon Component")]
    [SerializeField] private GameObject interactionIcon;

    [Header("Party Member Subcomponents")]
    [SerializeField] private List<PartyMemberUI> partySlots;

    [Header("Other UI Panels")]
    [SerializeField] private GameObject minimapPanel;
    [SerializeField] private GameObject aimUI;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    #region Stamina Methods

    public void UpdateStamina(float currentValue, float maxValue)
    {
        if (staminaSlider == null) return;

        staminaSlider.maxValue = maxValue;
        staminaSlider.value = currentValue;

        if (staminaFillImage != null)
        {
            staminaFillImage.color = (currentValue < 30f) ? lowStaminaColor : normalStaminaColor;
        }

        bool isFull = currentValue >= maxValue;
        SetStaminaBarVisible(!isFull);
    }

    public void SetStaminaBarVisible(bool visible)
    {
        if (staminaBarObject != null)
        {
            staminaBarObject.SetActive(visible);
        }
    }

    #endregion

    #region Interaction Icon Methods

    public void SetInteractionIconVisible(bool visible)
    {
        if (interactionIcon != null)
        {
            interactionIcon.SetActive(visible);
        }
    }

    #endregion

    #region Party Info Methods

    public void RefreshPartyInfo()
    {
        if (GameManager.Instance == null) return;

        List<GameObject> party = GameManager.Instance.GetParty();

        for (int i = 0; i < partySlots.Count; i++)
        {
            if (i < party.Count && party[i] != null)
            {
                FighterStats stats = party[i].GetComponent<FighterStats>();
                partySlots[i].UpdateData(stats, party[i].name);
            }
            else
            {
                partySlots[i].gameObject.SetActive(false);
            }
        }
    }

    #endregion

    #region Map & Aim UI Methods

    public void SetMinimapVisible(bool visible)
    {
        if (minimapPanel != null)
        {
            minimapPanel.SetActive(visible);
        }
    }

    public void SetAimUIVisible(bool visible)
    {
        if (aimUI != null)
        {
            aimUI.SetActive(visible);
        }
    }

    #endregion
}