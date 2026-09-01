using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using System.Collections.Generic;

public class MinimapManager : MonoBehaviour
{
    [Header("═══════ UI ССЫЛКИ (перетащи из Hierarchy) ═══════")]
    [Tooltip("ПАНЕЛЬ миникарты (MinimapPanel, НЕ сам Canvas!)")]
    public RectTransform minimapPanel;

    [Tooltip("Фон миникарты")]
    public Image backgroundImage;

    [Tooltip("Маркер локального игрока (ты)")]
    public RectTransform localMarker;

    [Tooltip("Стрелка направления локального игрока")]
    public RectTransform directionArrow;

    [Tooltip("Контейнер для маркеров других игроков")]
    public RectTransform otherPlayersContainer;

    [Header("═══════ НАСТРОЙКИ ОТОБРАЖЕНИЯ ═══════")]
    [Tooltip("Радиус видимости на карте (мировые единицы)")]
    [Range(10f, 300f)]
    public float viewRadius = 60f;

    [Tooltip("Частота обновления маркеров других игроков (секунды)")]
    [Range(0.05f, 1f)]
    public float updateInterval = 0.1f;

    [Header("═══════ ПРЕФАБЫ ═══════")]
    [Tooltip("Префаб маркера другого игрока (можно оставить пустым)")]
    public GameObject otherPlayerMarkerPrefab;

    [Header("═══════ ЦВЕТА ═══════")]
    public Color otherPlayerAliveColor = new Color(1f, 0.2f, 0.2f, 1f);
    public Color otherPlayerDeadColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);

    [Header("═══════ ПОВЕДЕНИЕ ═══════")]
    public bool hideDuringChat = true;
    public bool hideDuringInventory = true;

    // Внутренние переменные
    private PlayerController localPlayer;
    private Dictionary<int, RectTransform> otherMarkers = new Dictionary<int, RectTransform>();
    private Dictionary<int, Image> otherMarkerImages = new Dictionary<int, Image>();
    private float nextUpdateTime = 0f;
    private float nextSearchTime = 0f;
    private bool initialized = false;

    void Start()
    {
        if (minimapPanel == null || backgroundImage == null || localMarker == null)
        {
            Debug.LogError("❌ Миникарта: заполни все ссылки UI в Inspector! (MinimapPanel, Background, LocalMarker)");
            enabled = false;
            return;
        }

        FindLocalPlayer();
        initialized = true;
        Debug.Log("✅ Миникарта готова! Ищу локального игрока...");
    }

    void Update()
    {
        if (!initialized) return;

        // Повторный поиск игрока (Photon создаёт его не сразу)
        if (localPlayer == null)
        {
            if (Time.time >= nextSearchTime)
            {
                nextSearchTime = Time.time + 0.5f;
                FindLocalPlayer();
            }
            return;
        }

        // Проверка что игрок жив
        if (localPlayer.Equals(null))
        {
            localPlayer = null;
            return;
        }

        // Скрытие миникарты
        bool shouldHide = false;
        if (hideDuringChat && ChatManager.IsChatOpen) shouldHide = true;
        if (hideDuringInventory)
        {
            PlayerInventory inv = localPlayer.inventory;
            if (inv != null && inv.IsInventoryOpen) shouldHide = true;
        }

        if (minimapPanel != null)
        {
            minimapPanel.gameObject.SetActive(!shouldHide);
        }

        if (shouldHide) return;

        // Обновление направления локального игрока
        UpdateLocalPlayerDirection();

        // Обновление других игроков
        if (Time.time >= nextUpdateTime)
        {
            nextUpdateTime = Time.time + updateInterval;
            UpdateOtherPlayers();
        }
    }

    void FindLocalPlayer()
    {
        PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
        foreach (var pc in allPlayers)
        {
            if (pc != null && pc.IsLocalPlayer())
            {
                localPlayer = pc;
                Debug.Log("✅ Локальный игрок найден — миникарта активна!");
                return;
            }
        }
    }

    void UpdateLocalPlayerDirection()
    {
        if (localPlayer == null || directionArrow == null) return;
        float yRot = localPlayer.GetRotationY();
        directionArrow.localRotation = Quaternion.Euler(0, 0, -yRot);
    }

    void UpdateOtherPlayers()
    {
        if (localPlayer == null || otherPlayersContainer == null) return;

        PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
        HashSet<int> visibleActorNumbers = new HashSet<int>();

        Vector3 localPos = localPlayer.transform.position;
        RectTransform bgRect = backgroundImage.rectTransform;
        float halfWidth = bgRect.rect.width * 0.5f;
        float halfHeight = bgRect.rect.height * 0.5f;

        foreach (var pc in allPlayers)
        {
            if (pc == null || pc.IsLocalPlayer() || pc.view == null) continue;

            int actorId = pc.view.OwnerActorNr;
            visibleActorNumbers.Add(actorId);

            Vector3 relative = pc.GetPosition() - localPos;
            float mapX = (relative.x / viewRadius) * halfWidth;
            float mapY = (relative.z / viewRadius) * halfHeight;

            bool insideView = Mathf.Abs(mapX) <= halfWidth && Mathf.Abs(mapY) <= halfHeight;

            if (!otherMarkers.ContainsKey(actorId))
            {
                CreateOtherPlayerMarker(actorId);
            }

            RectTransform markerRect = otherMarkers[actorId];
            Image markerImage = otherMarkerImages[actorId];

            if (markerRect == null) continue;

            if (insideView)
            {
                markerRect.gameObject.SetActive(true);
                markerRect.anchoredPosition = new Vector2(mapX, mapY);
                markerImage.color = pc.isPlayerDead ? otherPlayerDeadColor : otherPlayerAliveColor;

                float otherY = pc.GetRotationY();
                markerRect.localRotation = Quaternion.Euler(0, 0, -otherY);
            }
            else
            {
                markerRect.gameObject.SetActive(false);
            }
        }

        // Удаление маркеров ушедших игроков
        List<int> toRemove = new List<int>();
        foreach (var kvp in otherMarkers)
        {
            if (!visibleActorNumbers.Contains(kvp.Key))
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (int id in toRemove)
        {
            otherMarkers.Remove(id);
            otherMarkerImages.Remove(id);
        }
    }

    void CreateOtherPlayerMarker(int actorId)
    {
        if (otherPlayersContainer == null) return;

        GameObject markerObj;
        if (otherPlayerMarkerPrefab != null)
        {
            markerObj = Instantiate(otherPlayerMarkerPrefab, otherPlayersContainer);
        }
        else
        {
            // Создаём простой маркер если префаба нет
            markerObj = new GameObject($"OtherPlayer_{actorId}");
            markerObj.transform.SetParent(otherPlayersContainer, false);
            RectTransform rect = markerObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(8, 8);
            Image img = markerObj.AddComponent<Image>();
            img.color = otherPlayerAliveColor;
        }

        RectTransform markerRect = markerObj.GetComponent<RectTransform>();
        Image markerImage = markerObj.GetComponent<Image>();

        if (markerRect != null && markerImage != null)
        {
            otherMarkers[actorId] = markerRect;
            otherMarkerImages[actorId] = markerImage;
        }
    }
}