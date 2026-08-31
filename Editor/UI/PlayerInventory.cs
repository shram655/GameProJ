using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerInventory : MonoBehaviour
{
    [Header("Настройки слотов")]
    public GameObject[] blockPrefabs;
    public Sprite[] blockIcons;
    public GameObject[] cellPanels;

    [Header("🎯 ПАТРОНЫ: два типа")]
    [Tooltip("Патроны 7.62 (АК-47)")]
    public int ammoItemId = -2;
    [Tooltip("Патроны 5.45 (АК-74)")]
    public int ammoItemId2 = -3;
    [Tooltip("Иконка 7.62")]
    public Sprite ammoIcon;
    [Tooltip("Иконка 5.45")]
    public Sprite ammoIcon2;
    public int maxAmmoStack = 30;

    [Header("Иконки оружий")]
    public Sprite[] weaponIcons;
    public Sprite defaultWeaponIcon;

    [Header("Иконки холодного оружия")]
    public Sprite[] meleeIcons;
    public Sprite defaultMeleeIcon;

    [Header("Данные инвентаря")]
    public int[] inventory = new int[20];
    public int[] inventoryCounts = new int[20];
    public int selectedSlot = 15;

    [Header("UI Ссылки")]
    public InventoryUI inventoryUI;

    private Color[] originalSlotColors = new Color[5];
    private PlayerController playerController;

    public bool IsInventoryOpen
    {
        get
        {
            if (inventoryUI == null) return false;
            if (inventoryUI.inventoryPanel != null) return inventoryUI.inventoryPanel.activeSelf;
            return inventoryUI.IsOpen();
        }
    }

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        for (int i = 0; i < 20; i++) { inventory[i] = 0; inventoryCounts[i] = 0; }
        selectedSlot = 15;
        for (int i = 0; i < 5; i++)
            if (cellPanels.Length > i && cellPanels[i] != null)
            {
                Image p = cellPanels[i].GetComponent<Image>();
                if (p != null) originalSlotColors[i] = p.color;
            }
    }

    void Start()
    {
        if (inventoryUI == null) inventoryUI = FindLocalInventoryUI();
        SyncIconArrayWithInventory();
        CopyIconSettingsFromInventory();
        UpdateHotbarUI();
    }

    InventoryUI FindLocalInventoryUI()
    {
        foreach (var ui in FindObjectsOfType<InventoryUI>())
        {
            if (ui == null) continue;
            PlayerController pc = ui.GetComponentInParent<PlayerController>();
            if (pc == null) return ui;
            if (pc.view != null && pc.view.IsMine) return ui;
        }
        return FindObjectOfType<InventoryUI>();
    }

    void Update()
    {
        if (playerController != null && playerController.view != null && !playerController.view.IsMine) return;
        if (playerController != null && playerController.isPlayerDead) return;
        if (ChatManager.IsChatOpen) return;

        UpdateHotbarUI();

        if (IsInventoryOpen) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

        HandleSlotSwitching();
    }

    private void HandleSlotSwitching()
    {
        if (IsInventoryOpen) return;
        bool switched = false;
        if (Input.GetAxis("Mouse ScrollWheel") > 0f) { selectedSlot = (selectedSlot + 1) % 5 + 15; switched = true; }
        else if (Input.GetAxis("Mouse ScrollWheel") < 0f) { selectedSlot = (selectedSlot - 1 + 5) % 5 + 15; switched = true; }
        for (int i = 1; i <= 5; i++)
            if (Input.GetKeyDown(KeyCode.Alpha0 + i)) { selectedSlot = i - 1 + 15; switched = true; }
        if (switched) { UpdateHotbarUI(); CheckSelectedSlot(); }
    }

    private void SyncIconArrayWithInventory()
    {
        if (inventoryUI == null) return;
        if (inventoryUI.itemIcons != null && inventoryUI.itemIcons.Length > 0) blockIcons = inventoryUI.itemIcons;
    }

    private void CopyIconSettingsFromInventory()
    {
        if (inventoryUI == null) return;
        if (inventoryUI.inventorySlots == null || inventoryUI.inventorySlots.Length == 0) return;
        GameObject refSlot = inventoryUI.inventorySlots[0];
        if (refSlot == null) return;
        Transform refIconT = refSlot.transform.Find("Icon");
        if (refIconT == null) return;
        Image refIcon = refIconT.GetComponent<Image>();
        if (refIcon == null) return;
        RectTransform refRect = refIcon.GetComponent<RectTransform>();
        for (int i = 0; i < cellPanels.Length; i++)
        {
            if (cellPanels[i] == null) continue;
            Transform hotT = cellPanels[i].transform.Find("Icon");
            if (hotT == null) continue;
            Image hot = hotT.GetComponent<Image>();
            if (hot == null) continue;
            hot.color = refIcon.color; hot.type = refIcon.type; hot.preserveAspect = refIcon.preserveAspect; hot.raycastTarget = refIcon.raycastTarget;
            RectTransform hr = hot.GetComponent<RectTransform>();
            if (hr != null && refRect != null)
            {
                hr.anchorMin = refRect.anchorMin; hr.anchorMax = refRect.anchorMax;
                hr.offsetMin = refRect.offsetMin; hr.offsetMax = refRect.offsetMax; hr.pivot = refRect.pivot;
            }
        }
    }

    public const int MELEE_BASE = 200;

    // ═════════════════════════════════════════════════════
    // 🆕 ТИПЫ ПРЕДМЕТОВ
    // ═════════════════════════════════════════════════════
    public bool IsAmmo(int id) => id == ammoItemId || id == ammoItemId2;
    public bool IsGun(int itemId) => itemId < -2 && itemId > -MELEE_BASE && !IsAmmo(itemId);
    public bool IsMelee(int itemId) => itemId < -MELEE_BASE;
    public bool IsWeapon(int itemId) => IsGun(itemId);

    public Sprite GetAmmoIcon(int id) => id == ammoItemId2 ? ammoIcon2 : ammoIcon;
    // 🆕 Какое оружие какие патроны использует
    public int GetAmmoIdForWeapon(int weaponId) => weaponId == 2 ? ammoItemId2 : ammoItemId;

    public Sprite GetMeleeIcon(int meleeId)
    {
        int idx = meleeId - 1;
        if (meleeIcons != null && idx >= 0 && idx < meleeIcons.Length && meleeIcons[idx] != null) return meleeIcons[idx];
        return defaultMeleeIcon;
    }
    public int GetMeleeIdFromItemId(int itemId) => -(itemId + MELEE_BASE);

    public Sprite GetWeaponIcon(int weaponId)
    {
        int idx = weaponId - 1;
        if (weaponIcons != null && idx >= 0 && idx < weaponIcons.Length && weaponIcons[idx] != null) return weaponIcons[idx];
        return defaultWeaponIcon;
    }
    public int GetWeaponIdFromItemId(int itemId) => -(itemId + 100);

    // ═════════════════════════════════════════════════════
    // 🆕 ПАТРОНЫ ПО ТИПАМ
    // ═════════════════════════════════════════════════════
    public int GetTotalAmmoOfType(int ammoId)
    {
        int total = 0;
        for (int i = 0; i < 15; i++) if (inventory[i] == ammoId) total += inventoryCounts[i];
        return total;
    }
    public int GetTotalAmmo() => GetTotalAmmoOfType(ammoItemId);

    public bool ConsumeAmmoOfType(int ammoId, int amount)
    {
        if (GetTotalAmmoOfType(ammoId) < amount) return false;
        int remaining = amount;
        for (int i = 14; i >= 0; i--)
        {
            if (inventory[i] == ammoId && remaining > 0)
            {
                int take = Mathf.Min(inventoryCounts[i], remaining);
                inventoryCounts[i] -= take; remaining -= take;
                if (inventoryCounts[i] <= 0) { inventory[i] = 0; inventoryCounts[i] = 0; }
            }
        }
        UpdateHotbarUI(); if (inventoryUI != null) inventoryUI.UpdateAllSlots();
        return true;
    }
    public bool ConsumeAmmo(int a) => ConsumeAmmoOfType(ammoItemId, a);

    public void AddAmmoOfType(int ammoId, int amount)
    {
        int remaining = amount;
        for (int i = 0; i < 15; i++)
        {
            if (inventory[i] == ammoId && inventoryCounts[i] < maxAmmoStack)
            {
                int add = Mathf.Min(maxAmmoStack - inventoryCounts[i], remaining);
                inventoryCounts[i] += add; remaining -= add;
                if (remaining <= 0) break;
            }
        }
        while (remaining > 0)
        {
            int empty = -1;
            for (int i = 0; i < 15; i++) if (inventory[i] == 0) { empty = i; break; }
            if (empty == -1) break;
            int add = Mathf.Min(maxAmmoStack, remaining);
            inventory[empty] = ammoId; inventoryCounts[empty] = add; remaining -= add;
        }
        UpdateHotbarUI(); if (inventoryUI != null) inventoryUI.UpdateAllSlots();
    }
    public void AddAmmo(int a) => AddAmmoOfType(ammoItemId, a);

    // ═════════════════════════════════════════════════════
    public void AddMeleeToInventory(int meleeId)
    {
        int invId = -(MELEE_BASE + meleeId);
        for (int i = 0; i < 20; i++)
            if (inventory[i] == invId) { inventoryCounts[i]++; UpdateHotbarUI(); if (inventoryUI != null) inventoryUI.UpdateAllSlots(); return; }
        for (int i = 0; i < 20; i++)
            if (inventory[i] == 0) { inventory[i] = invId; inventoryCounts[i] = 1; UpdateHotbarUI(); if (inventoryUI != null) inventoryUI.UpdateAllSlots(); return; }
    }

    public void UpdateHotbarUI()
    {
        for (int i = 0; i < 5; i++)
        {
            int slotIndex = i + 15;
            if (cellPanels.Length > i && cellPanels[i] != null)
            {
                Image panelImg = cellPanels[i].GetComponent<Image>();
                if (panelImg != null)
                    panelImg.color = (slotIndex == selectedSlot) ? new Color(1f, 1f, 1f, 0.5f) : originalSlotColors[i];

                Transform iconT = cellPanels[i].transform.Find("Icon");
                if (iconT != null)
                {
                    Image iconImg = iconT.GetComponent<Image>();
                    if (iconImg != null)
                    {
                        int blockId = inventory[slotIndex];
                        if (IsAmmo(blockId)) { iconImg.sprite = GetAmmoIcon(blockId); iconImg.gameObject.SetActive(true); }
                        else if (blockId > 0)
                        {
                            int idx = blockId - 1;
                            if (blockIcons != null && idx >= 0 && idx < blockIcons.Length && blockIcons[idx] != null) { iconImg.sprite = blockIcons[idx]; iconImg.gameObject.SetActive(true); }
                            else iconImg.gameObject.SetActive(false);
                        }
                        else if (IsGun(blockId)) { Sprite ic = GetWeaponIcon(GetWeaponIdFromItemId(blockId)); if (ic != null) { iconImg.sprite = ic; iconImg.gameObject.SetActive(true); } else iconImg.gameObject.SetActive(false); }
                        else if (IsMelee(blockId)) { Sprite ic = GetMeleeIcon(GetMeleeIdFromItemId(blockId)); if (ic != null) { iconImg.sprite = ic; iconImg.gameObject.SetActive(true); } else iconImg.gameObject.SetActive(false); }
                        else iconImg.gameObject.SetActive(false);
                    }
                }

                Transform countT = cellPanels[i].transform.Find("CountText");
                if (countT != null)
                {
                    TextMeshProUGUI tmp = countT.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        int count = inventoryCounts[slotIndex];
                        int blockId = inventory[slotIndex];
                        if (IsAmmo(blockId) || IsGun(blockId) || IsMelee(blockId)) tmp.text = count > 0 ? count.ToString() : "";
                        else tmp.text = count > 1 ? count.ToString() : "";
                    }
                }
            }
        }
    }

    public void AddToInventory(int blockId)
    {
        if (blockId <= 0) return;
        for (int i = 0; i < 15; i++) if (inventory[i] == blockId) { inventoryCounts[i]++; UpdateHotbarUI(); if (inventoryUI != null) inventoryUI.UpdateAllSlots(); return; }
        for (int i = 0; i < 15; i++) if (inventory[i] == 0) { inventory[i] = blockId; inventoryCounts[i] = 1; UpdateHotbarUI(); if (inventoryUI != null) inventoryUI.UpdateAllSlots(); return; }
        for (int i = 15; i < 20; i++) if (inventory[i] == blockId) { inventoryCounts[i]++; UpdateHotbarUI(); if (inventoryUI != null) inventoryUI.UpdateAllSlots(); return; }
        for (int i = 15; i < 20; i++) if (inventory[i] == 0) { inventory[i] = blockId; inventoryCounts[i] = 1; UpdateHotbarUI(); if (inventoryUI != null) inventoryUI.UpdateAllSlots(); return; }
    }

    public void ClearInventory()
    {
        if (playerController != null && playerController.weaponManager != null) playerController.weaponManager.UnequipCurrentWeapon();
        for (int i = 0; i < 20; i++) { inventory[i] = 0; inventoryCounts[i] = 0; }
        UpdateHotbarUI(); if (inventoryUI != null) inventoryUI.UpdateAllSlots();
    }

    public void CheckSelectedSlot()
    {
        if (playerController == null || playerController.weaponManager == null) return;
        int itemId = inventory[selectedSlot];
        if (IsGun(itemId)) playerController.weaponManager.EquipWeaponFromSlot(selectedSlot);
        else if (IsMelee(itemId)) playerController.weaponManager.EquipMeleeFromSlot(selectedSlot);
        else if (playerController.weaponManager.HasWeaponEquipped) playerController.weaponManager.UnequipCurrentWeapon();
    }

    public bool IsFood(int blockId) => blockId == 10;
    public float GetFoodRestoreAmount(int blockId) { switch (blockId) { case 10: return 25f; default: return 0f; } }

    public void ConsumeItemFromInventory(int slotIndex)
    {
        int blockId = inventory[slotIndex];
        if (!IsFood(blockId)) return;
        float restore = GetFoodRestoreAmount(blockId);
        PlayerHunger hunger = GetComponent<PlayerHunger>();
        if (hunger != null) hunger.ConsumeFoodItem(blockId, restore);
        inventoryCounts[slotIndex]--;
        if (inventoryCounts[slotIndex] <= 0) inventory[slotIndex] = 0;
        UpdateHotbarUI(); if (inventoryUI != null) inventoryUI.UpdateAllSlots();
    }
}