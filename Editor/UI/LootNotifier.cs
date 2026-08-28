using UnityEngine;
using System.Collections.Generic;

// Уведомления о луте.
// Можно повесить на любой объект в сцене и настроить через Inspector.
// Если не повесить — создастся сам со стандартными настройками.
public class LootNotifier : MonoBehaviour
{
    private static LootNotifier instance;

    public enum ПозицияНаЭкране
    {
        СверхуЦентр,
        СверхуСлева,
        СверхуСправа,
        СнизуЦентр
    }

    [Header("═══ ОБЩИЕ НАСТРОЙКИ ═══")]
    [Tooltip("Сколько секунд показывается одно уведомление")]
    public float displayTime = 3f;

    [Tooltip("Пауза между уведомлениями в очереди (сек)")]
    public float gapBetween = 0.2f;

    [Tooltip("Где на экране показывать уведомление")]
    public ПозицияНаЭкране position = ПозицияНаЭкране.СверхуЦентр;

    [Tooltip("Отступ от верхнего/нижнего края экрана (пиксели)")]
    public float edgeOffset = 100f;

    [Tooltip("Отступ от левого/правого края экрана (пиксели)")]
    public float sideOffset = 20f;

    [Header("═══ РАЗМЕР ПЛАШКИ ═══")]
    [Tooltip("Ширина плашки уведомления (пиксели)")]
    public float width = 460f;

    [Tooltip("Высота плашки уведомления (пиксели)")]
    public float height = 60f;

    [Header("═══ ЦВЕТА ПЛАШКИ ═══")]
    [Tooltip("Цвет фона плашки")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);

    [Tooltip("Показывать рамку вокруг плашки")]
    public bool showBorder = true;

    [Tooltip("Цвет рамки")]
    public Color borderColor = new Color(1f, 0.84f, 0f, 0.9f);

    [Tooltip("Толщина рамки (пиксели)")]
    [Range(1f, 10f)]
    public float borderWidth = 2f;

    [Header("═══ ТЕКСТ ═══")]
    [Tooltip("Цвет текста уведомления")]
    public Color textColor = Color.white;

    [Tooltip("Размер шрифта")]
    [Range(10, 48)]
    public int fontSize = 24;

    [Tooltip("Жирный текст")]
    public bool boldText = true;

    [Tooltip("Отступ текста от левого края плашки (пиксели)")]
    public float textLeftPadding = 60f;

    [Header("═══ ИКОНКА ═══")]
    [Tooltip("Показывать иконку предмета")]
    public bool showIcon = true;

    [Tooltip("Размер иконки (пиксели)")]
    [Range(16f, 128f)]
    public float iconSize = 44f;

    [Tooltip("Отступ иконки от края плашки (пиксели)")]
    public float iconPadding = 8f;

    // ═════════════════════════════════════════════════════
    // ВНУТРЕННИЕ ПЕРЕМЕННЫЕ (не трогать)
    // ═════════════════════════════════════════════════════
    private readonly Queue<NotificationData> queue = new Queue<NotificationData>();
    private string currentText = "";
    private Texture2D currentIcon = null;
    private bool showing = false;
    private float hideAt = -1f;
    private float nextAt = -1f;

    public struct NotificationData
    {
        public Sprite icon;
        public string text;
    }

    void Awake()
    {
        // Если скрипт повешен в сцене — он становится основным
        if (instance == null) instance = this;
    }

    // ═════════════════════════════════════════════════════
    // СТАТИЧЕСКИЙ ВЫЗОВ (из BlockLootTable)
    // ═════════════════════════════════════════════════════
    public static void Show(Sprite icon, string itemName, int count)
    {
        if (BlockLootTable.IsSyncing) return;

        if (instance == null)
        {
            // Не повешен в сцене — создаём сам
            GameObject go = new GameObject("LootNotifier");
            instance = go.AddComponent<LootNotifier>();
        }

        instance.queue.Enqueue(new NotificationData
        {
            icon = icon,
            text = $"+{count} {itemName}"
        });
    }

    // ═════════════════════════════════════════════════════
    // ОЧЕРЕДЬ (без корутин — не может зависнуть)
    // ═════════════════════════════════════════════════════
    void Update()
    {
        if (showing)
        {
            if (Time.time >= hideAt)
            {
                showing = false;
                nextAt = Time.time + gapBetween;
            }
        }

        if (!showing && queue.Count > 0 && Time.time >= nextAt)
        {
            ShowNext();
        }
    }

    void ShowNext()
    {
        NotificationData data = queue.Dequeue();

        currentText = data.text;
        currentIcon = data.icon != null ? data.icon.texture : null;
        showing = true;
        hideAt = Time.time + displayTime;

        // 🆕 Служебная строка убрана — это не ошибка, а просто показ уведомления
    }

    // ═════════════════════════════════════════════════════
    // ПОЗИЦИЯ ПЛАШКИ ПО НАСТРОЙКАМ
    // ═════════════════════════════════════════════════════
    Rect GetRect()
    {
        float x, y;

        switch (position)
        {
            case ПозицияНаЭкране.СверхуСлева:
                x = sideOffset;
                y = edgeOffset;
                break;
            case ПозицияНаЭкране.СверхуСправа:
                x = Screen.width - width - sideOffset;
                y = edgeOffset;
                break;
            case ПозицияНаЭкране.СнизуЦентр:
                x = (Screen.width - width) / 2f;
                y = Screen.height - height - edgeOffset;
                break;
            default: // СверхуЦентр
                x = (Screen.width - width) / 2f;
                y = edgeOffset;
                break;
        }

        return new Rect(x, y, width, height);
    }

    // ═════════════════════════════════════════════════════
    // РЕНДЕР — ВСЕ НАСТРОЙКИ ИЗ INSPECTOR ПРИМЕНЯЮТСЯ СРАЗУ
    // ═════════════════════════════════════════════════════
    void OnGUI()
    {
        if (!showing) return;

        Rect rect = GetRect();

        // Фон плашки
        GUI.color = backgroundColor;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        // Рамка
        if (showBorder)
        {
            GUI.color = borderColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, borderWidth), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - borderWidth, rect.width, borderWidth), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, borderWidth, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - borderWidth, rect.y, borderWidth, rect.height), Texture2D.whiteTexture);
        }

        GUI.color = Color.white;

        // Иконка предмета
        if (showIcon && currentIcon != null)
        {
            GUI.DrawTexture(
                new Rect(rect.x + iconPadding, rect.y + (rect.height - iconSize) / 2f, iconSize, iconSize),
                currentIcon);
        }

        // Текст
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = fontSize;
        style.fontStyle = boldText ? FontStyle.Bold : FontStyle.Normal;
        style.normal.textColor = textColor;
        style.alignment = TextAnchor.MiddleLeft;

        GUI.Label(new Rect(rect.x + textLeftPadding, rect.y, rect.width - textLeftPadding - 10, rect.height),
            currentText, style);
    }
}