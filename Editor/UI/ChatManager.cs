using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Collections.Generic;

public class ChatManager : MonoBehaviour, IOnEventCallback
{
    private const byte CHAT_EVENT_CODE = 100;

    public static bool IsChatOpen { get; private set; }
    private static bool justClosed = false;

    [Header("═══════ РАЗМЕРЫ ПАНЕЛИ ЛОГА ═══════")]
    public float logWidth = 720f;
    public float logHeight = 320f;
    public float logOffsetX = 10f;
    public float logOffsetY = 60f;

    [Header("═══════ РАЗМЕРЫ ПАНЕЛИ ВВОДА ═══════")]
    public float inputWidth = 720f;
    public float inputHeight = 44f;
    public float inputOffsetX = 10f;
    public float inputOffsetY = 10f;

    [Header("═══════ ЦВЕТА ПАНЕЛЕЙ ═══════")]
    public Color logPanelColor = new Color(0f, 0f, 0f, 0.35f);
    public Color inputPanelColor = new Color(0.05f, 0.05f, 0.05f, 0.75f);

    [Header("═══════ ВИЗУАЛЬНОЕ СОСТОЯНИЕ ВВОДА ═══════")]
    [Tooltip("Цвет панели ввода, когда чат ЗАКРЫТ")]
    public Color inputClosedColor = new Color(0.05f, 0.05f, 0.05f, 0.55f);
    [Tooltip("Цвет панели ввода, когда вы ПИШЕТЕ (Enter нажат)")]
    public Color inputOpenColor = new Color(0.10f, 0.25f, 0.50f, 0.85f);

    [Header("═══════ ЦВЕТА ТЕКСТА ═══════")]
    public Color textColor = Color.white;
    public Color inputTextColor = Color.white;
    public Color placeholderColor = new Color(1f, 1f, 1f, 0.4f);

    [Header("═══════ ШРИФТ И ТЕКСТ ═══════")]
    public TMP_FontAsset chatFont;
    [Range(10, 40)] public int logFontSize = 18;
    [Range(10, 40)] public int inputFontSize = 20;
    [Range(10, 40)] public int placeholderFontSize = 18;
    public string placeholderText = "Сообщение... (Enter — отправить, Esc — закрыть)";
    public FontStyles fontStyle = FontStyles.Normal;

    [Header("═══════ ЦВЕТА НИКОВ ═══════")]
    public Color[] nickColors = new Color[]
    {
        new Color(0.31f, 0.76f, 0.97f),
        new Color(0.51f, 0.78f, 0.52f),
        new Color(1.00f, 0.72f, 0.30f),
        new Color(0.94f, 0.38f, 0.57f),
        new Color(0.73f, 0.41f, 0.78f),
        new Color(1.00f, 0.95f, 0.46f),
        new Color(0.30f, 0.71f, 0.67f)
    };

    [Header("═══════ НАСТРОЙКИ ═══════")]
    [Range(3, 50)] public int maxMessages = 10;
    [Range(20, 500)] public int maxLength = 100;
    public bool blockMovement = true;
    public bool blockMouse = true;

    private Canvas chatCanvas;
    private GameObject logPanel;
    private TextMeshProUGUI logText;
    private GameObject inputPanel;
    private TMP_InputField inputField;
    private Image logPanelImage;
    private Image inputPanelImage;
    private CanvasGroup inputCanvasGroup;

    private PlayerMovement playerMovement;
    private mouseLook mouseLookComp;
    private List<string> messages = new List<string>();
    private bool initialized = false;

    void OnEnable() { PhotonNetwork.AddCallbackTarget(this); }
    void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); }

    void Start()
    {
        CreateUI();
        InitializeMovementRefs();
        initialized = true;

        if (inputPanel != null)
        {
            inputPanel.SetActive(true);
            SetInputInteractable(false);
            if (inputPanelImage != null) inputPanelImage.color = inputClosedColor;
        }

        if (logPanelImage != null) logPanelImage.enabled = true;

        Debug.Log("✅ ЧАТ ГОТОВ! Enter — открыть, Esc — закрыть");
    }

    void SetInputInteractable(bool on)
    {
        if (inputCanvasGroup != null)
        {
            inputCanvasGroup.blocksRaycasts = on;
            inputCanvasGroup.interactable = on;
        }
        if (inputField != null) inputField.interactable = on;
    }

    void CreateUI()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        GameObject canvasObj = new GameObject("ChatCanvas");
        chatCanvas = canvasObj.AddComponent<Canvas>();
        chatCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        chatCanvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // ═══ ПАНЕЛЬ ЛОГА ═══
        logPanel = new GameObject("LogPanel");
        logPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform logRect = logPanel.AddComponent<RectTransform>();
        logRect.anchorMin = new Vector2(0, 0);
        logRect.anchorMax = new Vector2(0, 0);
        logRect.pivot = new Vector2(0, 0);
        logRect.anchoredPosition = new Vector2(logOffsetX, logOffsetY);
        logRect.sizeDelta = new Vector2(logWidth, logHeight);
        logPanelImage = logPanel.AddComponent<Image>();
        logPanelImage.color = logPanelColor;
        logPanelImage.raycastTarget = false;

        GameObject logTextObj = new GameObject("LogText");
        logTextObj.transform.SetParent(logPanel.transform, false);
        RectTransform logTextRect = logTextObj.AddComponent<RectTransform>();
        logTextRect.anchorMin = Vector2.zero;
        logTextRect.anchorMax = Vector2.one;
        logTextRect.offsetMin = new Vector2(10, 8);
        logTextRect.offsetMax = new Vector2(-10, -8);
        logText = logTextObj.AddComponent<TextMeshProUGUI>();
        logText.fontSize = logFontSize;
        logText.fontStyle = fontStyle;
        logText.color = textColor;
        logText.alignment = TextAlignmentOptions.BottomLeft;
        logText.raycastTarget = false;
        logText.enableWordWrapping = true;
        if (chatFont != null) logText.font = chatFont;

        // ═══ ПАНЕЛЬ ВВОДА ═══
        inputPanel = new GameObject("InputPanel");
        inputPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform inputRect = inputPanel.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0, 0);
        inputRect.anchorMax = new Vector2(0, 0);
        inputRect.pivot = new Vector2(0, 0);
        inputRect.anchoredPosition = new Vector2(inputOffsetX, inputOffsetY);
        inputRect.sizeDelta = new Vector2(inputWidth, inputHeight);
        inputPanelImage = inputPanel.AddComponent<Image>();
        inputPanelImage.color = inputClosedColor;
        inputCanvasGroup = inputPanel.AddComponent<CanvasGroup>();

        // ═══ ПОЛЕ ВВОДА ═══
        GameObject fieldObj = new GameObject("InputField");
        fieldObj.transform.SetParent(inputPanel.transform, false);
        RectTransform fieldRect = fieldObj.AddComponent<RectTransform>();
        fieldRect.anchorMin = Vector2.zero;
        fieldRect.anchorMax = Vector2.one;
        fieldRect.offsetMin = new Vector2(10, 4);
        fieldRect.offsetMax = new Vector2(-10, -4);
        fieldObj.AddComponent<RectMask2D>();

        inputField = fieldObj.AddComponent<TMP_InputField>();
        inputField.characterLimit = maxLength;
        inputField.lineType = TMP_InputField.LineType.SingleLine;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(fieldObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.fontSize = inputFontSize;
        tmpText.color = inputTextColor;
        tmpText.fontStyle = fontStyle;
        tmpText.raycastTarget = false;
        tmpText.enableWordWrapping = false;
        tmpText.overflowMode = TextOverflowModes.Overflow;
        if (chatFont != null) tmpText.font = chatFont;

        GameObject phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(fieldObj.transform, false);
        RectTransform phRect = phObj.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = Vector2.zero;
        phRect.offsetMax = Vector2.zero;
        TextMeshProUGUI phText = phObj.AddComponent<TextMeshProUGUI>();
        phText.text = placeholderText;
        phText.fontSize = placeholderFontSize;
        phText.color = placeholderColor;
        phText.raycastTarget = false;
        if (chatFont != null) phText.font = chatFont;

        inputField.textComponent = tmpText;
        inputField.placeholder = phText;
        inputField.onEndEdit.AddListener(OnEndEdit);
    }

    void InitializeMovementRefs()
    {
        PlayerController[] pcs = FindObjectsOfType<PlayerController>();
        foreach (var pc in pcs)
        {
            if (pc.view != null && pc.view.IsMine)
            {
                playerMovement = pc.movement;
                if (playerMovement == null) playerMovement = pc.GetComponent<PlayerMovement>();
                mouseLookComp = pc.GetComponentInChildren<mouseLook>(true);
                break;
            }
        }
        if (mouseLookComp == null) mouseLookComp = FindObjectOfType<mouseLook>();
    }

    void Update()
    {
        if (!initialized) return;

        if (!IsChatOpen && !justClosed && inputField != null && !inputField.isFocused
            && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            OpenChat();
        }
        else if (IsChatOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseChat();
        }
    }

    void LateUpdate()
    {
        if (IsChatOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void ResetJustClosed() { justClosed = false; }

    public void OpenChat()
    {
        IsChatOpen = true;
        justClosed = false;

        if (blockMovement && playerMovement != null) playerMovement.enabled = false;
        if (blockMouse && mouseLookComp != null) mouseLookComp.enabled = false;

        if (inputPanel != null) inputPanel.SetActive(true);
        if (inputPanelImage != null) inputPanelImage.color = inputOpenColor;
        SetInputInteractable(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (inputField != null)
        {
            inputField.text = "";
            inputField.ActivateInputField();
            inputField.Select();
        }
    }

    public void CloseChat()
    {
        IsChatOpen = false;
        justClosed = true;
        Invoke(nameof(ResetJustClosed), 0.1f);

        if (blockMovement && playerMovement != null) playerMovement.enabled = true;
        if (blockMouse && mouseLookComp != null) mouseLookComp.enabled = true;

        if (inputPanelImage != null) inputPanelImage.color = inputClosedColor;
        if (inputField != null)
        {
            inputField.text = "";
            inputField.DeactivateInputField();
        }
        SetInputInteractable(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnEndEdit(string text)
    {
        if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter))
            SendChatMessage(text);
        CloseChat();
    }

    public void SendChatMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        text = text.Trim();
        if (text.Length > maxLength) text = text.Substring(0, maxLength);

        string nick = PhotonNetwork.NickName;
        if (string.IsNullOrEmpty(nick)) nick = "Player";

        AddMessage(nick, text);

        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            PhotonNetwork.RaiseEvent(CHAT_EVENT_CODE,
                new string[] { nick, text },
                RaiseEventOptions.Default,
                SendOptions.SendReliable);
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != CHAT_EVENT_CODE) return;
        try
        {
            object raw = photonEvent.CustomData;
            if (raw == null) return;
            if (raw is string[] sArr && sArr.Length >= 2) { AddMessage(sArr[0], sArr[1]); return; }
            if (raw is object[] oArr && oArr.Length >= 2) { AddMessage(oArr[0] as string ?? "Player", oArr[1] as string ?? ""); return; }
            if (raw is string single)
            {
                int idx = single.IndexOf(": ");
                if (idx > 0) AddMessage(single.Substring(0, idx), single.Substring(idx + 2));
                else AddMessage("Player", single);
            }
        }
        catch { }
    }

    string GetNickColorHex(string nick)
    {
        if (nickColors == null || nickColors.Length == 0) return "#FFFFFF";
        int index = Mathf.Abs(nick.GetHashCode()) % nickColors.Length;
        return ColorUtility.ToHtmlStringRGB(nickColors[index]);
    }

    public void AddMessage(string nick, string text)
    {
        if (string.IsNullOrEmpty(nick)) nick = "Player";
        if (text == null) text = "";

        string color = GetNickColorHex(nick);
        messages.Add($"<color=#{color}><b>{nick}</b></color>: {text}");
        if (messages.Count > maxMessages) messages.RemoveAt(0);

        if (logText != null) logText.text = string.Join("\n", messages);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying || !initialized) return;
        if (logPanelImage != null) logPanelImage.color = logPanelColor;
        if (inputPanelImage != null && !IsChatOpen) inputPanelImage.color = inputClosedColor;
    }
#endif
}