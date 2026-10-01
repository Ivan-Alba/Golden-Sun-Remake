using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages standard equipment slots and dedicated Psynergy Relic slots for a hero.
/// Applies stat modifiers to FighterStats, notifies system events upon equipment changes,
/// spawns 3D weapon models in hand sockets, toggles sheath visibility based on weapon subtype,
/// and handles self-contained save data serialization via GameEvents.SaveLoad.
/// </summary>
public class HeroEquipment : MonoBehaviour
{
    #region Serialized Fields

    [Header("Relic Slot Configuration")]
    [SerializeField] private int maxRelicSlots = 4;

    [Header("Starting Equipment Config")]
    [Tooltip("List of default equipment assigned on a new game (Weapons, Armors, Helmets...). Slot type is auto-detected from each EquipableSO.")]
    [SerializeField] private List<EquipableSO> startingEquipment = new List<EquipableSO>();

    [Tooltip("List of default Psynergy Relics assigned on a new game (up to MaxRelicSlots).")]
    [SerializeField] private List<PsynergyRelicSO> startingRelics = new List<PsynergyRelicSO>();

    [Header("3D Model Sockets (Combat)")]
    [Tooltip("Transform socket in the hero's hand bone where the weapon prefab will be instantiated.")]
    [SerializeField] private Transform weaponHandSocket = null;

    [Tooltip("Optional Transform or Mesh GameObject representing the sword sheath/scabbard (e.g., Isaac/Garet).")]
    [SerializeField] private GameObject sheathSocket = null;

    [Header("Component References")]
    [SerializeField] private FighterStats fighterStats;
    [SerializeField] private HeroInventory heroInventory;

    #endregion

    #region Private Fields

    // Standard Equipment Slots Map
    private readonly Dictionary<EquipmentSlot, EquipableSO> equippedItems = new Dictionary<EquipmentSlot, EquipableSO>();

    // Dedicated Psynergy Relic Slots Array
    private PsynergyRelicSO[] equippedRelics;

    // Runtime reference to the currently instantiated 3D weapon model
    private GameObject currentWeaponInstance = null;

    #endregion

    #region Properties

    public int MaxRelicSlots => maxRelicSlots;
    public IReadOnlyDictionary<EquipmentSlot, EquipableSO> EquippedItems => equippedItems;
    public IReadOnlyList<PsynergyRelicSO> EquippedRelics => equippedRelics;

    #endregion

    #region Unity Lifecycle & Event Subscriptions

    private void Awake()
    {
        if (fighterStats == null) fighterStats = GetComponent<FighterStats>();
        if (heroInventory == null) heroInventory = GetComponent<HeroInventory>();

        InitializeEquipmentSlots();
    }

    private void OnEnable()
    {
        // Save & Load Event Subscriptions
        GameEvents.SaveLoad.OnCollectSaveData += HandleCollectSaveData;
        GameEvents.SaveLoad.OnRestoreSaveData += HandleRestoreSaveData;
    }

    private void OnDisable()
    {
        // Save & Load Event Unsubscriptions
        GameEvents.SaveLoad.OnCollectSaveData -= HandleCollectSaveData;
        GameEvents.SaveLoad.OnRestoreSaveData -= HandleRestoreSaveData;
    }

    #endregion

    #region Initialization

    private void InitializeEquipmentSlots()
    {
        equippedItems.Clear();

        // Initialize standard slots
        equippedItems.Add(EquipmentSlot.Weapon, null);
        equippedItems.Add(EquipmentSlot.Shield, null);
        equippedItems.Add(EquipmentSlot.Helmet, null);
        equippedItems.Add(EquipmentSlot.Armor, null);
        equippedItems.Add(EquipmentSlot.Hands, null);
        equippedItems.Add(EquipmentSlot.Boots, null);
        equippedItems.Add(EquipmentSlot.Accessory, null);

        // Initialize dedicated relic slots
        equippedRelics = new PsynergyRelicSO[maxRelicSlots];
    }

    #endregion

    #region Save & Load Event Handlers

    private void HandleCollectSaveData(GameData saveData)
    {
        if (saveData == null) return;

        HeroEquipmentData equipmentData = new HeroEquipmentData
        {
            heroName = gameObject.name
        };

        // 1. Serialize standard equipment slots
        foreach (var kvp in equippedItems)
        {
            if (kvp.Value != null)
            {
                equipmentData.standardEquipment.Add(new EquipmentSlotData
                {
                    slotType = kvp.Key,
                    itemSlug = kvp.Value.ItemSlug
                });
            }
        }

        // 2. Serialize equipped relics
        if (equippedRelics != null)
        {
            for (int i = 0; i < equippedRelics.Length; i++)
            {
                if (equippedRelics[i] != null)
                {
                    equipmentData.relicSlugs.Add(equippedRelics[i].ItemSlug);
                }
                else
                {
                    equipmentData.relicSlugs.Add(string.Empty);
                }
            }
        }

        saveData.heroesEquipment.Add(equipmentData);
        //Debug.Log($"<color=green>[HeroEquipment] Collected equipment save data for hero '{gameObject.name}'.</color>");
    }

    private void HandleRestoreSaveData(GameData saveData)
    {
        if (saveData == null || saveData.heroesEquipment == null) return;

        // Find this specific hero's equipment DTO by GameObject name
        HeroEquipmentData equipmentData = saveData.heroesEquipment.Find(e => e != null && e.heroName.Equals(gameObject.name));
        if (equipmentData == null) return;

        // 1. Clear current equipped items and stat modifiers silently
        ClearAllEquipmentSilently();

        // 2. Restore standard equipment slots
        for (int i = 0; i < equipmentData.standardEquipment.Count; i++)
        {
            EquipmentSlotData slotData = equipmentData.standardEquipment[i];
            if (slotData == null || string.IsNullOrEmpty(slotData.itemSlug)) continue;

            ItemSO itemAsset = null;
            GameEvents.Database.RequestItemBySlug(slotData.itemSlug, item => itemAsset = item);

            if (itemAsset is EquipableSO equipable)
            {
                LoadEquippedItem(equipable, notifyEvent: false);
            }
            else
            {
                //Debug.LogWarning($"[HeroEquipment] Equipable slug '{slotData.itemSlug}' not found in database for hero '{gameObject.name}'.");
            }
        }

        // 3. Restore equipped relics
        for (int i = 0; i < equipmentData.relicSlugs.Count; i++)
        {
            string relicSlug = equipmentData.relicSlugs[i];
            if (string.IsNullOrEmpty(relicSlug)) continue;

            ItemSO itemAsset = null;
            GameEvents.Database.RequestItemBySlug(relicSlug, item => itemAsset = item);

            if (itemAsset is PsynergyRelicSO relic)
            {
                LoadEquippedRelic(relic, i, notifyEvent: false);
            }
            else
            {
                Debug.LogWarning($"[HeroEquipment] Relic slug '{relicSlug}' not found in database for hero '{gameObject.name}'.");
            }
        }

        // 4. Update 3D visual mesh models after restoration
        UpdateWeaponVisuals();

        // 5. Notify all system listeners that this hero's equipment has finished restoring
        GameEvents.Hero.TriggerEquipmentChanged(gameObject);
        Debug.Log($"<color=cyan>[HeroEquipment] Equipment successfully restored from save data for hero '{gameObject.name}'.</color>");
    }

    private void ClearAllEquipmentSilently()
    {
        // Remove standard equipment modifiers
        List<EquipmentSlot> keys = new List<EquipmentSlot>(equippedItems.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            EquipmentSlot slotKey = keys[i];
            if (equippedItems[slotKey] != null)
            {
                RemoveItemModifiers(equippedItems[slotKey]);
                equippedItems[slotKey] = null;
            }
        }

        // Remove relic modifiers
        if (equippedRelics != null)
        {
            for (int i = 0; i < equippedRelics.Length; i++)
            {
                if (equippedRelics[i] != null)
                {
                    RemoveItemModifiers(equippedRelics[i]);
                    equippedRelics[i] = null;
                }
            }
        }

        // Clear 3D model instance
        ClearWeaponVisuals();
    }

    #endregion

    #region Equip Operations

    public bool EquipFromBackpack(int backpackSlotIndex, int targetRelicIndex = 0)
    {
        if (heroInventory == null) return false;

        InventorySlot slot = heroInventory.GetSlot(backpackSlotIndex);
        if (slot == null || slot.IsEmpty) return false;

        ItemSO itemToEquip = slot.Item;

        // 1. Handle Psynergy Relics FIRST
        if (itemToEquip is PsynergyRelicSO relic)
        {
            return EquipRelicItem(relic, backpackSlotIndex, targetRelicIndex);
        }
        // 2. Handle Standard Equipment SECOND
        else if (itemToEquip is EquipableSO equipable)
        {
            return EquipStandardItem(equipable, backpackSlotIndex);
        }

        Debug.LogWarning($"[HeroEquipment] Item '{itemToEquip.ItemName}' is neither an Equipable nor a PsynergyRelic.");
        return false;
    }

    private bool EquipStandardItem(EquipableSO newEquipable, int backpackSlotIndex)
    {
        EquipmentSlot slotType = newEquipable.EquipmentSlot;

        if (slotType == EquipmentSlot.None || slotType == EquipmentSlot.PsynergyRelic)
        {
            Debug.LogWarning($"[HeroEquipment] Invalid slot type '{slotType}' for standard equipable.");
            return false;
        }

        EquipableSO currentEquipped = equippedItems[slotType];

        // 1. Remove stat bonuses of previously equipped item
        if (currentEquipped != null)
        {
            RemoveItemModifiers(currentEquipped);
        }

        // 2. Apply stat bonuses of the new item
        if (fighterStats != null)
        {
            ApplyItemModifiers(newEquipable);
        }

        // 3. Update equipped slot map
        equippedItems[slotType] = newEquipable;

        // 4. Update backpack slot
        if (currentEquipped != null)
        {
            heroInventory.GetSlot(backpackSlotIndex).SetSlot(currentEquipped, 1, false);
        }
        else
        {
            heroInventory.RemoveFromSlot(backpackSlotIndex, 1);
        }

        // 5. Update 3D Visual Mesh Model if equipping a weapon
        if (slotType == EquipmentSlot.Weapon)
        {
            UpdateWeaponVisuals();
        }

        GameEvents.Hero.TriggerEquipmentChanged(gameObject);
        Debug.Log($"<color=green>[Equipment] Equipped {newEquipable.ItemName} on {gameObject.name}</color>");
        return true;
    }

    private bool EquipRelicItem(PsynergyRelicSO newRelic, int backpackSlotIndex, int relicSlotIndex)
    {
        if (relicSlotIndex < 0 || relicSlotIndex >= maxRelicSlots)
        {
            relicSlotIndex = 0;
        }

        PsynergyRelicSO currentRelic = equippedRelics[relicSlotIndex];

        // 1. Remove modifiers of previously equipped relic
        if (currentRelic != null)
        {
            RemoveItemModifiers(currentRelic);
        }

        // 2. Apply modifiers of new relic
        ApplyItemModifiers(newRelic);

        // 3. Assign new relic to dedicated array
        equippedRelics[relicSlotIndex] = newRelic;

        // 4. Update backpack slot
        if (currentRelic != null)
        {
            heroInventory.GetSlot(backpackSlotIndex).SetSlot(currentRelic, 1, false);
        }
        else
        {
            heroInventory.RemoveFromSlot(backpackSlotIndex, 1);
        }

        GameEvents.Hero.TriggerEquipmentChanged(gameObject);
        Debug.Log($"<color=cyan>[Equipment] Equipped Relic {newRelic.ItemName} in slot {relicSlotIndex} on {gameObject.name}</color>");
        return true;
    }

    #endregion

    #region Unequip Operations

    public bool UnequipStandardSlot(EquipmentSlot slotType)
    {
        if (!equippedItems.ContainsKey(slotType) || equippedItems[slotType] == null) return false;

        EquipableSO itemToUnequip = equippedItems[slotType];

        if (!heroInventory.CanAddItem(itemToUnequip, 1))
        {
            Debug.LogWarning("[HeroEquipment] Cannot unequip. Backpack is full!");
            return false;
        }

        RemoveItemModifiers(itemToUnequip);
        equippedItems[slotType] = null;
        heroInventory.AddItem(itemToUnequip, 1);

        // Update 3D Visual Mesh Model if unequipping weapon
        if (slotType == EquipmentSlot.Weapon)
        {
            UpdateWeaponVisuals();
        }

        GameEvents.Hero.TriggerEquipmentChanged(gameObject);
        Debug.Log($"<color=yellow>[Equipment] Unequipped {itemToUnequip.ItemName} from {gameObject.name}</color>");
        return true;
    }

    public bool UnequipRelicSlot(int relicSlotIndex)
    {
        if (relicSlotIndex < 0 || relicSlotIndex >= maxRelicSlots) return false;

        PsynergyRelicSO relicToUnequip = equippedRelics[relicSlotIndex];
        if (relicToUnequip == null) return false;

        if (!heroInventory.CanAddItem(relicToUnequip, 1))
        {
            Debug.LogWarning("[HeroEquipment] Cannot unequip relic. Backpack is full!");
            return false;
        }

        RemoveItemModifiers(relicToUnequip);
        equippedRelics[relicSlotIndex] = null;
        heroInventory.AddItem(relicToUnequip, 1);

        GameEvents.Hero.TriggerEquipmentChanged(gameObject);
        Debug.Log($"<color=yellow>[Equipment] Unequipped Relic {relicToUnequip.ItemName} from {gameObject.name}</color>");
        return true;
    }

    #endregion

    #region 3D Model Spawning & Visual Logic

    /// <summary>
    /// Destroys the current weapon mesh instance and spawns the newly equipped weapon prefab.
    /// Manages sheath socket visibility depending on weapon subtype (e.g., Swords vs Axes/Staffs).
    /// </summary>
    private void UpdateWeaponVisuals()
    {
        ClearWeaponVisuals();

        WeaponSO equippedWeapon = GetEquippedWeapon();

        if (equippedWeapon == null || equippedWeapon.WeaponModelPrefab == null)
        {
            // If no weapon or model assigned, hide sheath
            if (sheathSocket != null) sheathSocket.SetActive(false);
            return;
        }

        // 1. Instantiate new 3D model under hand socket
        if (weaponHandSocket != null)
        {
            currentWeaponInstance = Instantiate(equippedWeapon.WeaponModelPrefab, weaponHandSocket);
            currentWeaponInstance.transform.localPosition = Vector3.zero;
            currentWeaponInstance.transform.localRotation = Quaternion.identity;
            currentWeaponInstance.transform.localScale = Vector3.one;
        }
        else
        {
            Debug.LogWarning($"[HeroEquipment] WeaponHandSocket is not assigned on '{gameObject.name}'. Cannot spawn weapon mesh.");
        }

        // 2. Toggle sheath visibility based on weapon subtype
        if (sheathSocket != null)
        {
            bool isSword = equippedWeapon.EquipmentSubtype == EquipmentSubtype.Longsword ||
                           equippedWeapon.EquipmentSubtype == EquipmentSubtype.LightBlade;

            sheathSocket.SetActive(isSword);
        }
    }

    /// <summary>
    /// Safely destroys the instantiated 3D weapon model gameobject.
    /// </summary>
    private void ClearWeaponVisuals()
    {
        if (currentWeaponInstance != null)
        {
            Destroy(currentWeaponInstance);
            currentWeaponInstance = null;
        }
    }

    #endregion

    #region Modifier Helpers

    private void ApplyItemModifiers(ItemSO item)
    {
        if (fighterStats == null || item == null) return;

        if (item is EquipableSO equipable)
        {
            var modifiers = equipable.GetStatModifiers();
            int count = modifiers.Count;
            for (int i = 0; i < count; i++)
            {
                fighterStats.AddStatModifier(modifiers[i].statType, modifiers[i].modifier);
            }
        }
    }

    private void RemoveItemModifiers(ItemSO item)
    {
        if (fighterStats == null || item == null) return;

        fighterStats.RemoveModifiersFromSource(item);
    }

    #endregion

    #region Public Queries & Load Helpers

    /// <summary>
    /// Equips default starting items defined in the Inspector.
    /// Used when starting a new game or recruiting a new party member.
    /// </summary>
    public void InitializeStartingEquipment()
    {
        // 1. Clear current slots to avoid residual items
        ClearAllEquipmentSilently();

        // 2. Equip standard equipment (Slot auto-detected via EquipableSO.EquipmentSlot)
        for (int i = 0; i < startingEquipment.Count; i++)
        {
            if (startingEquipment[i] != null)
            {
                LoadEquippedItem(startingEquipment[i], notifyEvent: false);
            }
        }

        // 3. Equip starting relics in sequence (up to maxRelicSlots)
        for (int i = 0; i < startingRelics.Count; i++)
        {
            if (startingRelics[i] != null && i < maxRelicSlots)
            {
                LoadEquippedRelic(startingRelics[i], i, notifyEvent: false);
            }
        }

        // 4. Update 3D visual mesh and trigger event once
        UpdateWeaponVisuals();
        GameEvents.Hero.TriggerEquipmentChanged(gameObject);
    }

    public EquipableSO GetEquippedItem(EquipmentSlot slotType)
    {
        if (equippedItems.TryGetValue(slotType, out var item))
        {
            return item;
        }
        return null;
    }

    public WeaponSO GetEquippedWeapon()
    {
        if (equippedItems.TryGetValue(EquipmentSlot.Weapon, out var item))
        {
            return item as WeaponSO;
        }
        return null;
    }

    public PsynergyRelicSO GetEquippedRelic(int relicSlotIndex)
    {
        if (relicSlotIndex >= 0 && relicSlotIndex < maxRelicSlots)
        {
            return equippedRelics[relicSlotIndex];
        }
        return null;
    }

    public List<PsynergySO> GetEquippedRelicPsynergies()
    {
        List<PsynergySO> relicPsynergies = new List<PsynergySO>();
        if (equippedRelics == null) return relicPsynergies;

        for (int i = 0; i < equippedRelics.Length; i++)
        {
            if (equippedRelics[i] != null && equippedRelics[i].GrantedPsynergy != null)
            {
                relicPsynergies.Add(equippedRelics[i].GrantedPsynergy);
            }
        }
        return relicPsynergies;
    }

    public void LoadEquippedItem(EquipableSO item, bool notifyEvent = false)
    {
        if (item == null) return;

        EquipmentSlot slotType = item.EquipmentSlot;
        if (equippedItems.ContainsKey(slotType))
        {
            if (equippedItems[slotType] != null)
            {
                RemoveItemModifiers(equippedItems[slotType]);
            }

            equippedItems[slotType] = item;
            ApplyItemModifiers(item);

            if (slotType == EquipmentSlot.Weapon)
            {
                UpdateWeaponVisuals();
            }

            if (notifyEvent)
            {
                GameEvents.Hero.TriggerEquipmentChanged(gameObject);
            }
        }
    }

    public void LoadEquippedRelic(PsynergyRelicSO relic, int slotIndex, bool notifyEvent = false)
    {
        if (relic == null || slotIndex < 0 || slotIndex >= maxRelicSlots) return;

        if (equippedRelics[slotIndex] != null)
        {
            RemoveItemModifiers(equippedRelics[slotIndex]);
        }

        equippedRelics[slotIndex] = relic;
        ApplyItemModifiers(relic);

        if (notifyEvent)
        {
            GameEvents.Hero.TriggerEquipmentChanged(gameObject);
        }
    }

    #endregion
}