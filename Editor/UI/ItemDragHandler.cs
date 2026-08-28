using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class ItemDragHandler : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    private int slotIndex = -1;
    private static readonly List<ItemDragHandler> allSlots = new List<ItemDragHandler>();

    public static ItemDragHandler DragSource;
    public static int DragItemId;

    private static GameObject dragCanvasObj;
    private static Image dragIconImage;

    public void SetSlotIndex(int index)
    {
        slotIndex = index;
        if (!allSlots.Contains(this)) allSlots.Add(this);
    }

    void OnDestroy()
    {
        allSlots.Remove(this);
        if (DragSource == this) DragSource = null;
    }

    InventoryUI UI => InventoryUI.Instance;

    public void OnPointerEnter(PointerEventData eventData) { if (UI != null) UI.OnSlotHover(slotIndex, true); }
    public void OnPointerExit(PointerEventData eventData) { if (UI != null) UI.OnSlotHover(slotIndex, false); }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (UI == null || UI.playerInventory == null) return;
        if (ChatManager.IsChatOpen) return;
        if (slotIndex < 0 || slotIndex >= 20) return;

        int id = UI.playerInventory.inventory[slotIndex];
        int count = UI.playerInventory.inventoryCounts[slotIndex];
        if (id == 0) return;

        bool weaponOrMelee = UI.playerInventory.IsGun(id) || UI.playerInventory.IsMelee(id);
        if (!weaponOrMelee && count <= 0) return;

        UI.OnSlotHover(slotIndex, false);
        DragSource = this;
        DragItemId = id;
        ShowDragIcon(eventData.position, id);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (DragSource == null) return;
        MoveDragIcon(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (DragSource != null)
        {
            ItemDragHandler target = FindSlotAt(eventData.position);

            if (target != null && UI != null && UI.playerInventory != null)
            {
                UI.MoveItemBetweenSlots(DragSource.slotIndex, target.slotIndex);
            }
            // 🆕 Если бросили ЗА РАМКУ инвентаря — выкинуть лут на землю
            else if (UI != null && UI.playerInventory != null && IsOutsideInventory(eventData.position))
            {
                LootDropper.DropFromInventory(UI.playerInventory, DragSource.slotIndex);
            }
        }

        HideDragIcon();
        DragSource = null;
    }

    bool IsOutsideInventory(Vector2 pos)
    {
        if (UI == null || UI.inventoryPanel == null) return true;
        RectTransform rt = UI.inventoryPanel.GetComponent<RectTransform>();
        if (rt == null) return true;
        return !RectTransformUtility.RectangleContainsScreenPoint(rt, pos, null);
    }

    static ItemDragHandler FindSlotAt(Vector2 screenPos)
    {
        for (int i = allSlots.Count - 1; i >= 0; i--)
        {
            ItemDragHandler s = allSlots[i];
            if (s == null) { allSlots.RemoveAt(i); continue; }
            if (!s.gameObject.activeInHierarchy) continue;
            RectTransform rt = s.transform as RectTransform;
            if (rt == null) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null)) return s;
        }
        return null;
    }

    static void EnsureDragCanvas()
    {
        if (dragCanvasObj != null) return;
        dragCanvasObj = new GameObject("DragIconCanvas");
        Canvas canvas = dragCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        dragCanvasObj.AddComponent<CanvasScaler>();
        dragCanvasObj.AddComponent<GraphicRaycaster>();

        GameObject iconObj = new GameObject("DragIcon");
        iconObj.transform.SetParent(dragCanvasObj.transform, false);
        RectTransform rt = iconObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(50, 50);
        dragIconImage = iconObj.AddComponent<Image>();
        dragIconImage.raycastTarget = false;
        iconObj.SetActive(false);
    }

    static void ShowDragIcon(Vector2 screenPos, int itemId)
    {
        EnsureDragCanvas();
        if (dragIconImage == null) return;
        Sprite icon = InventoryUI.Instance != null ? InventoryUI.Instance.GetIconForItem(itemId) : null;
        dragIconImage.sprite = icon;
        dragIconImage.enabled = icon != null;
        dragIconImage.gameObject.SetActive(true);
        MoveDragIcon(screenPos);
    }

    static void MoveDragIcon(Vector2 screenPos)
    {
        if (dragIconImage == null) return;
        dragIconImage.rectTransform.position = screenPos;
    }

    static void HideDragIcon()
    {
        if (dragIconImage != null) dragIconImage.gameObject.SetActive(false);
    }
}