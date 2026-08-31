using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

// 🆕 Редактируемое название предмета (itemId -> своё имя)
[System.Serializable]
public class ItemNameEntry
{
    public int itemId;
    public string displayName;
}

public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance;

    [Header("Настройки")]
    public GameObject inventoryPanel;
    public GameObject[] inventorySlots;

    [Header("Иконки предметов")]
    public Sprite[] itemIcons;
    public Sprite ammoIcon;
    public int ammoItemId = -2;

    [Header("🆕 СВОИ НАЗВАНИЯ ПРЕДМЕТОВ")]
    public List<ItemNameEntry> itemNames = new List<ItemNameEntry>();

    [Header("НАЗВАНИЯ БЛОКОВ (запасной вариант)")]
    public string[] blockNames;

    [Header("🆕 ТУЛТИП (ФИКСИРОВАННЫЙ размер для всего лута)")]
    [Tooltip("Постоянный размер плашки (не меняется!)")]
    public Vector2 tooltipSize = new Vector2(150f, 26f);
    [Tooltip("Размер шрифта")]
    public int tooltipFontSize = 13;
    [Tooltip("Смещение от курсора")]
    public Vector2 tooltipOffset = new Vector2(12f, 16f);

    public PlayerInventory playerInventory;

    private bool isOpen = false;
    private bool isRemoteUI = false;

    // Тултип
    private RectTransform tooltipRect;
    private Image tooltipImage;
    private TextMeshProUGUI tooltipText;
    private GameObject tooltipPanel;
    private bool tooltipVisible = false;

    void Start()
    {
        PlayerController parentPC = GetComponentInParent<PlayerController>();
        if (parentPC != null && parentPC.view != null && !parentPC.view.IsMine)
        {
            isRemoteUI = true;
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
            enabled = false;
            return;
        }

        playerInventory = FindLocalPlayerInventory();
        if (playerInventory != null && Instance == null) Instance = this;
        if (playerInventory != null)
        {
            if (ammoIcon == null) ammoIcon = playerInventory.ammoIcon;
            ammoItemId = playerInventory.ammoItemId;
        }
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        SetupDragAndDrop();
        CreateTooltip();
    }

    PlayerInventory FindLocalPlayerInventory()
    {
        if (playerInventory != null)
        {
            PlayerController self = playerInventory.GetComponent<PlayerController>();
            if (self != null && self.view != null && self.view.IsMine) return playerInventory;
        }
        foreach (var inv in FindObjectsOfType<PlayerInventory>())
        {
            if (inv == null) continue;
            PlayerController pc = inv.GetComponent<PlayerController>();
            if (pc != null && pc.view != null && pc.view.IsMine) return inv;
        }
        return FindObjectOfType<PlayerInventory>();
    }

    void SetupDragAndDrop()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
        foreach (Canvas c in FindObjectsOfType<Canvas>())
            if (c.GetComponent<GraphicRaycaster>() == null) c.gameObject.AddComponent<GraphicRaycaster>();

        if (inventorySlots != null)
            for (int i = 0; i < inventorySlots.Length; i++)
            {
                if (inventorySlots[i] == null) continue;
                EnsureSlotDraggable(inventorySlots[i], i);
            }

        for (int i = 0; i < 5; i++)
        {
            GameObject cell = GameObject.Find("Cell_" + i);
            if (cell != null) EnsureSlotDraggable(cell, i + 15);
        }
    }

    void EnsureSlotDraggable(GameObject slot, int index)
    {
        Image img = slot.GetComponent<Image>();
        if (img == null) { img = slot.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0); }
        img.raycastTarget = true;
        ItemDragHandler handler = slot.GetComponent<ItemDragHandler>();
        if (handler == null) handler = slot.AddComponent<ItemDragHandler>();
        handler.SetSlotIndex(index);
    }

    // ═════════════════════════════════════════════════════
    // 🆕 ТУЛТИП ФИКСИРОВАННОГО РАЗМЕРА
    // ═════════════════════════════════════════════════════
    void CreateTooltip()
    {
        GameObject canvasObj = new GameObject("TooltipCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        tooltipPanel = new GameObject("TooltipPanel");
        tooltipPanel.transform.SetParent(canvasObj.transform, false);
        tooltipRect = tooltipPanel.AddComponent<RectTransform>();
        tooltipRect.sizeDelta = tooltipSize;   // 🆕 ОДИН размер навсегда
        tooltipImage = tooltipPanel.AddComponent<Image>();
        tooltipImage.color = new Color(0f, 0f, 0f, 0.85f);
        tooltipImage.raycastTarget = false;

        GameObject textObj = new GameObject("TooltipText");
        textObj.transform.SetParent(tooltipPanel.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4, 2);
        textRect.offsetMax = new Vector2(-4, -2);
        tooltipText = textObj.AddComponent<TextMeshProUGUI>();
        tooltipText.fontSize = tooltipFontSize;
        tooltipText.color = Color.white;
        tooltipText.alignment = TextAlignmentOptions.Center;
        tooltipText.raycastTarget = false;
        tooltipText.enableWordWrapping = false;
        tooltipText.overflowMode = TextOverflowModes.Ellipsis; // длинный текст обрезается, не растягивает

        tooltipPanel.SetActive(false);
    }

    // ═════════════════════════════════════════════════════
    // ПОКАЗАТЬ / СКРЫТЬ
    // ═════════════════════════════════════════════════════
    public void OnSlotHover(int slotIndex, bool enter)
    {
        if (tooltipPanel == null || playerInventory == null) return;

        if (!enter || slotIndex < 0 || slotIndex >= 20)
        {
            HideTooltip();
            return;
        }

        int id = playerInventory.inventory[slotIndex];
        if (id == 0) { HideTooltip(); return; }

        string name = GetItemName(id);
        int count = playerInventory.inventoryCounts[slotIndex];

        string label = name;
        if (count > 0) label += " — " + count;

        tooltipText.text = label;
        tooltipVisible = true;
        tooltipPanel.SetActive(true);
    }

    public void HideTooltip()
    {
        tooltipVisible = false;
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    // 🆕 НАЗВАНИЕ ПРЕДМЕТА
    public string GetItemName(int id)
    {
        if (id == 0 || playerInventory == null) return "";

        foreach (var e in itemNames)
            if (e.itemId == id && !string.IsNullOrEmpty(e.displayName))
                return e.displayName;

        if (playerInventory.IsAmmo(id))
            return id == playerInventory.ammoItemId2 ? "Патроны 5.45" : "Патроны 7.62";

        if (playerInventory.IsGun(id))
        {
            int wid = playerInventory.GetWeaponIdFromItemId(id);
            PlayerWeaponManager wm = playerInventory.GetComponent<PlayerWeaponManager>();
            if (wm != null)
            {
                WeaponData wd = wm.GetWeaponData(wid);
                if (wd != null && !string.IsNullOrEmpty(wd.weaponName)) return wd.weaponName;
            }
            return "Оружие #" + wid;
        }

        if (playerInventory.IsMelee(id)) return "Топор";

        if (id > 0)
        {
            if (blockNames != null && id - 1 < blockNames.Length && !string.IsNullOrEmpty(blockNames[id - 1]))
                return blockNames[id - 1];
            return "Блок #" + id;
        }

        return "";
    }

    void Update()
    {
        if (isRemoteUI) return;
        if (ChatManager.IsChatOpen) return;

        // Тултип следует за курсором, размер НЕ меняется
        if (tooltipVisible && tooltipRect != null)
        {
            Vector3 pos = Input.mousePosition + (Vector3)tooltipOffset;
            tooltipRect.position = pos;
        }

        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I))
            ToggleInventory();
    }

    public void ToggleInventory()
    {
        if (isRemoteUI) return;
        isOpen = !isOpen;
        if (inventoryPanel != null) inventoryPanel.SetActive(isOpen);

        if (!isOpen) HideTooltip();

        if (isOpen) { UpdateAllSlots(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }

    public bool IsOpen() { return isOpen; }

    public void MoveItemBetweenSlots(int from, int to)
    {
        if (playerInventory == null) return;
        if (from == to) return;
        if (from < 0 || from >= 20 || to < 0 || to >= 20) return;

        if (playerInventory.inventory[from] == 0) return;

        PlayerWeaponManager pwm = playerInventory.GetComponent<PlayerWeaponManager>();
        if (pwm != null) pwm.ReleaseEquippedIfSlotInvolved(from, to);

        int fromId = playerInventory.inventory[from];
        int fromCount = playerInventory.inventoryCounts[from];
        int toId = playerInventory.inventory[to];
        int toCount = playerInventory.inventoryCounts[to];

        if (fromId == 0) return;

        bool fromIsWeapon = playerInventory.IsGun(fromId) || playerInventory.IsMelee(fromId);
        if (!fromIsWeapon && fromCount <= 0) return;

        bool canStack = false;
        int maxStack = 1;
        if (toId == fromId && fromId > 0)
        {
            if (playerInventory.IsAmmo(fromId)) { canStack = true; maxStack = playerInventory.maxAmmoStack; }
            else if (!playerInventory.IsGun(fromId) && !playerInventory.IsMelee(fromId)) { canStack = true; maxStack = 99; }
        }

        if (canStack && toCount < maxStack)
        {
            int move = Mathf.Min(fromCount, maxStack - toCount);
            playerInventory.inventoryCounts[to] += move;
            playerInventory.inventoryCounts[from] -= move;
            if (playerInventory.inventoryCounts[from] <= 0) { playerInventory.inventory[from] = 0; playerInventory.inventoryCounts[from] = 0; }
        }
        else
        {
            playerInventory.inventory[to] = fromId;
            playerInventory.inventoryCounts[to] = fromCount;
            playerInventory.inventory[from] = toId;
            playerInventory.inventoryCounts[from] = toCount;
        }

        playerInventory.UpdateHotbarUI();
        UpdateAllSlots();
        playerInventory.CheckSelectedSlot();
    }

    public Sprite GetIconForItem(int blockId)
    {
        if (playerInventory != null && playerInventory.IsAmmo(blockId))
            return playerInventory.GetAmmoIcon(blockId);

        if (blockId > 0)
        {
            int idx = blockId - 1;
            if (itemIcons != null && idx >= 0 && idx < itemIcons.Length) return itemIcons[idx];
            return null;
        }

        if (playerInventory != null && playerInventory.IsGun(blockId))
            return playerInventory.GetWeaponIcon(playerInventory.GetWeaponIdFromItemId(blockId));

        if (playerInventory != null && playerInventory.IsMelee(blockId))
            return playerInventory.GetMeleeIcon(playerInventory.GetMeleeIdFromItemId(blockId));

        return null;
    }

    public void UpdateAllSlots()
    {
        if (playerInventory == null) return;
        for (int i = 0; i < 15 && i < inventorySlots.Length; i++)
            UpdateSlot(i, playerInventory.inventory[i], playerInventory.inventoryCounts[i]);
    }

    void UpdateSlot(int slotIndex, int blockId, int count)
    {
        if (slotIndex >= inventorySlots.Length) return;
        GameObject slot = inventorySlots[slotIndex];
        if (slot == null) return;

        Transform iconTransform = slot.transform.Find("Icon");
        if (iconTransform == null) return;
        Image iconImg = iconTransform.GetComponent<Image>();
        if (iconImg == null) return;

        Sprite icon = GetIconForItem(blockId);
        if (icon != null) { iconImg.sprite = icon; iconImg.gameObject.SetActive(true); }
        else iconImg.gameObject.SetActive(false);

        Transform countText = slot.transform.Find("CountText");
        if (countText != null)
        {
            TextMeshProUGUI txt = countText.GetComponent<TextMeshProUGUI>();
            if (txt != null)
            {
                if ((playerInventory != null && playerInventory.IsAmmo(blockId)) ||
                    (playerInventory != null && playerInventory.IsGun(blockId)) ||
                    (playerInventory != null && playerInventory.IsMelee(blockId)))
                    txt.text = count > 0 ? count.ToString() : "";
                else
                    txt.text = count > 1 ? count.ToString() : "";
            }
        }
    }

    public void SwapItems(int slot1, int slot2) { MoveItemBetweenSlots(slot1, slot2); }

    public void OnInventorySlotClick(int slotIndex)
    {
        if (isRemoteUI) return;
        if (playerInventory == null) return;
        if (slotIndex < 0 || slotIndex >= 15) return;

        int blockId = playerInventory.inventory[slotIndex];
        int count = playerInventory.inventoryCounts[slotIndex];
        if (blockId == 0) return;

        bool isWeapon = playerInventory.IsGun(blockId) || playerInventory.IsMelee(blockId);
        if (!isWeapon && count <= 0) return;

        if (playerInventory.IsFood(blockId)) { playerInventory.ConsumeItemFromInventory(slotIndex); UpdateAllSlots(); return; }

        MoveItemBetweenSlots(slotIndex, playerInventory.selectedSlot);
    }
}