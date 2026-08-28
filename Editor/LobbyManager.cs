using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("UI Элементы")]
    public GameObject lobbyPanel;
    public TMP_InputField nicknameInput;
    public Button connectButton;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI playerNameText;

    [Header("Настройки")]
    public string roomName = "GameRoom";
    public int maxPlayers = 10;
    public bool autoCreateRoom = true;
    
    [Header("Сцены")]
    public string gameSceneName = "GameScene";
    public string lobbySceneName = "LobbyScene";

    private string playerNickname = "";
    private bool isConnecting = false;

    void Start()
    {
        if (PlayerPrefs.HasKey("PlayerNickname"))
        {
            nicknameInput.text = PlayerPrefs.GetString("PlayerNickname");
        }

        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(true);
        }

        UpdateStatus("Введите ник и нажмите 'Подключиться'");
        
        PhotonNetwork.AutomaticallySyncScene = false;
    }

    public void OnConnectButtonClicked()
    {
        if (isConnecting) return;

        playerNickname = nicknameInput.text.Trim();
        if (string.IsNullOrEmpty(playerNickname))
        {
            UpdateStatus("⚠️ Введите ник!");
            return;
        }

        PlayerPrefs.SetString("PlayerNickname", playerNickname);
        PlayerPrefs.Save();

        isConnecting = true;
        UpdateStatus("🔄 Подключение к серверу...");
        connectButton.interactable = false;

        PhotonNetwork.NickName = playerNickname;
        PhotonNetwork.ConnectUsingSettings();
    }

    void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log("📢 " + message);
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("✅ Подключено к мастер-серверу");
        UpdateStatus("✅ Подключено! Поиск комнаты...");

        PhotonNetwork.JoinRoom(roomName);
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("📋 Вошли в лобби");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning("⚠️ Комната не найдена: " + message);
        
        if (autoCreateRoom)
        {
            UpdateStatus("🏗️ Создаём новую комнату...");
            
            RoomOptions roomOptions = new RoomOptions
            {
                MaxPlayers = (byte)maxPlayers,
                IsVisible = true,
                IsOpen = true,
                EmptyRoomTtl = 0,
                PlayerTtl = 0
            };
            
            PhotonNetwork.CreateRoom(roomName, roomOptions);
        }
        else
        {
            UpdateStatus("❌ Комната не найдена!");
            isConnecting = false;
            connectButton.interactable = true;
        }
    }

    public override void OnCreatedRoom()
    {
        Debug.Log("🎉 Комната создана: " + roomName);
        UpdateStatus("🎉 Комната создана! Загружаем игру...");
        
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(gameSceneName);
        }
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("✅ Вошли в комнату: " + roomName);
        Debug.Log("👥 Игроков в комнате: " + PhotonNetwork.CurrentRoom.PlayerCount);
        
        UpdateStatus("✅ Вы в игре! Ник: " + playerNickname);
        
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log("👥 Не мастер-клиент, загружаем сцену...");
            PhotonNetwork.LoadLevel(gameSceneName);
        }
        else
        {
            Debug.Log("👑 Я мастер-клиент, сцена уже загружается");
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log(" Новый игрок присоединился: " + newPlayer.NickName);
        UpdateStatus(" Игрок присоединился: " + newPlayer.NickName);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log("👋 Игрок вышел: " + otherPlayer.NickName);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning("❌ Отключено: " + cause);
        UpdateStatus("❌ Отключено: " + cause);
        
        isConnecting = false;
        connectButton.interactable = true;
    }

    public void ReturnToLobby()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.LeaveRoom();
        }
    }

    public override void OnLeftRoom()
    {
        Debug.Log("🚪 Покинули комнату");
        PhotonNetwork.LoadLevel(lobbySceneName);
    }
}