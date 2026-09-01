using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

public class DroppedLootInteractor : MonoBehaviour
{
    [Header("═══ НАСТРОЙКИ ПОДБОРА ═══")]
    [Tooltip("Дистанция, с которой можно подобрать лут")]
    public float range = 3f;
    [Tooltip("Клавиша подбора")]
    public KeyCode pickupKey = KeyCode.F;

    [Header("═══ НАДПИСЬ (редактируй в Inspector) ═══")]
    [Tooltip("Размер плашки")]
    public Vector2 labelSize = new Vector2(260f, 50f);
    [Tooltip("Размер шрифта")]
    public int labelFontSize = 16;
    [Tooltip("Позиция на экране (0,-120 = ниже центра)")]
    public Vector2 labelPosition = new Vector2(0f, -120f);
    [Tooltip("Цвет фона плашки")]
    public Color labelBackground = new Color(0f, 0f, 0f, 0.6f);

    private PlayerController pc;
    private Camera cam;
    private GameObject panel;
    private TextMeshProUGUI text;
    private DroppedLoot aimed;

    void Start() { BuildUI(); }

    void BuildUI()
    {
        GameObject canvasObj = new GameObject("LootLabelCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 550;
        canvasObj.AddComponent<CanvasScaler>();

        panel = new GameObject("LootLabel");
        panel.transform.SetParent(canvasObj.transform, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = labelPosition;      // 🆕 из Inspector
        rt.sizeDelta = labelSize;                  // 🆕 из Inspector
        Image img = panel.AddComponent<Image>();
        img.color = labelBackground;               // 🆕 из Inspector
        img.raycastTarget = false;

        GameObject t = new GameObject("Text");
        t.transform.SetParent(panel.transform, false);
        RectTransform trt = t.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        text = t.AddComponent<TextMeshProUGUI>();
        text.fontSize = labelFontSize;             // 🆕 из Inspector
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        panel.SetActive(false);
    }

    void Update()
    {
        pc = FindLocalPlayer();
        if (pc == null) { Hide(); return; }
        cam = pc.playerCamera;
        if (cam == null) { Hide(); return; }

        if (ChatManager.IsChatOpen || (pc.inventory != null && pc.inventory.IsInventoryOpen)) { Hide(); return; }

        aimed = RaycastLoot();
        if (aimed != null && !aimed.picked)
        {
            string name = InventoryUI.Instance != null ? InventoryUI.Instance.GetItemName(aimed.itemId) : "Лут";
            text.text = $"{name} ({aimed.count})\n[{pickupKey}] Подобрать";
            panel.SetActive(true);

            if (Input.GetKeyDown(pickupKey))
                aimed.TryPickup(pc);
        }
        else Hide();
    }

    DroppedLoot RaycastLoot()
    {
        Ray r = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit h;
        if (Physics.Raycast(r, out h, range))
            return h.collider.GetComponentInParent<DroppedLoot>();
        return null;
    }

    PlayerController FindLocalPlayer()
    {
        foreach (var p in FindObjectsOfType<PlayerController>())
            if (p.view != null && p.view.IsMine) return p;
        return null;
    }

    void Hide() { if (panel != null) panel.SetActive(false); }
}