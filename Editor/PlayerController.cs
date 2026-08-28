using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using System.Collections;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerBuilding))]
[RequireComponent(typeof(PlayerWeaponManager))]
public class PlayerController : MonoBehaviourPunCallbacks
{
    [Header("Ссылки на компоненты")]
    public PhotonView view;
    public Camera playerCamera;
    public TextMeshPro nick;
    public GameObject Torch;

    [Header("═══ СИНХРОНИЗАЦИЯ ПОЗИЦИИ ═══")]
    [Tooltip("Как часто владелец шлёт позицию (сек). 0.1 = 10 раз/сек — плавно")]
    [Range(0.05f, 1f)]
    public float sendInterval = 0.1f;

    [Tooltip("Плавность движения клона (больше = быстрее догоняет)")]
    [Range(2f, 30f)]
    public float smoothSpeed = 12f;

    [Tooltip("Если рассинхрон больше этого (м) — мгновенный снап (телепорты, респавн)")]
    [Range(0.5f, 10f)]
    public float teleportThreshold = 3f;

    [HideInInspector] public PlayerMovement movement;
    [HideInInspector] public PlayerInventory inventory;
    [HideInInspector] public PlayerBuilding building;
    [HideInInspector] public PlayerWeaponManager weaponManager;
    [HideInInspector] public PlayerHealth health;
    [HideInInspector] public PlayerHunger hunger;

    [HideInInspector] public bool isPlayerDead = false;

    // 🆕 Цель для плавной интерполяции клона
    private Vector3 targetPos;
    private Quaternion targetRot = Quaternion.identity;
    private bool hasTarget = false;

    void Awake()
    {
        view = GetComponent<PhotonView>();
        movement = GetComponent<PlayerMovement>();
        inventory = GetComponent<PlayerInventory>();
        building = GetComponent<PlayerBuilding>();
        weaponManager = GetComponent<PlayerWeaponManager>();
        health = GetComponent<PlayerHealth>();
        hunger = GetComponent<PlayerHunger>();
    }

    void Start()
    {
        string nickname = PhotonNetwork.NickName;
        if (string.IsNullOrEmpty(nickname)) nickname = "Player";
        if (nick != null) nick.text = nickname;

        if (!view.IsMine)
        {
            enabled = false;
            movement.enabled = false;
            inventory.enabled = false;
            building.enabled = false;
            weaponManager.enabled = false;
            if (playerCamera != null) playerCamera.gameObject.SetActive(false);

            // Скрываем ВЕСЬ UI чужого игрока
            foreach (Canvas c in GetComponentsInChildren<Canvas>(true))
            {
                c.gameObject.SetActive(false);
            }

            // 🆕 Отключаем PhotonTransformView клона — позицией управляет
            // наша плавная интерполяция (иначе они конфликтуют = рывки)
            PhotonTransformView ptv = GetComponent<PhotonTransformView>();
            if (ptv != null) ptv.enabled = false;

            // Запрашиваем актуальную позицию у владельца
            StartCoroutine(RequestInitialSync());

            Debug.Log($"👥 Чужой игрок {nickname}: UI отключён, интерполяция включена");
            return;
        }

        // Владелец шлёт позицию 10 раз/сек
        StartCoroutine(PeriodicTransformSync());

        Debug.Log($"✅ Локальный игрок создан: {nickname}");
    }

    // ═════════════════════════════════════════════════════
    // ВЛАДЕЛЕЦ: отправка позиции
    // ═════════════════════════════════════════════════════
    IEnumerator PeriodicTransformSync()
    {
        WaitForSeconds wait = new WaitForSeconds(sendInterval);
        while (true)
        {
            if (view != null && view.IsMine && PhotonNetwork.IsConnected)
            {
                view.RPC("RPC_SyncTransform", RpcTarget.Others,
                    transform.position, transform.eulerAngles);
            }
            yield return wait;
        }
    }

    // Клону: запрос позиции при спавне
    IEnumerator RequestInitialSync()
    {
        for (int i = 0; i < 5; i++)
        {
            if (this == null) yield break;
            if (view != null && view.Owner != null)
            {
                view.RPC("RPC_RequestSync", view.Owner);
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    // Владельцу: отдать позицию тому, кто попросил
    [PunRPC]
    void RPC_RequestSync(PhotonMessageInfo info)
    {
        if (view == null || !view.IsMine) return;

        view.RPC("RPC_SyncTransform", info.Sender,
            transform.position, transform.eulerAngles);
    }

    // ═════════════════════════════════════════════════════
    // КЛОН: получение цели
    // ═════════════════════════════════════════════════════
    [PunRPC]
    void RPC_SyncTransform(Vector3 pos, Vector3 rot)
    {
        if (view == null || view.IsMine) return;

        targetRot = Quaternion.Euler(rot);

        // Телепорт/респавн/первая синхронизация — снап мгновенно
        if (!hasTarget || Vector3.Distance(transform.position, pos) > teleportThreshold)
        {
            transform.position = pos;
            transform.rotation = targetRot;
        }

        targetPos = pos;
        hasTarget = true;
    }

    // 🆕 КЛОН: плавное скольжение к цели КАЖДЫЙ КАДР
    void LateUpdate()
    {
        if (view == null || view.IsMine || !hasTarget) return;

        float t = Time.deltaTime * smoothSpeed;
        transform.position = Vector3.Lerp(transform.position, targetPos, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
    }

    void Update()
    {
        if (!view.IsMine || isPlayerDead) return;

        // БЛОКИРОВКА ВСЕХ КЛАВИШ ВО ВРЕМЯ ЧАТА
        if (ChatManager.IsChatOpen) return;

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (Torch != null) Torch.SetActive(!Torch.activeSelf);
        }

        if (Input.GetKeyUp(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.Confined;
            PhotonNetwork.Disconnect();
            PhotonNetwork.LoadLevel("LobbyScene");
        }
    }

    public void SetDead(bool dead)
    {
        isPlayerDead = dead;
        if (dead)
        {
            if (weaponManager != null) weaponManager.UnequipCurrentWeapon();
            if (inventory != null) inventory.ClearInventory();
        }
    }

    // ═════════════════════════════════════════════════════
    // МЕТОДЫ ДЛЯ МИНИКАРТЫ
    // ═════════════════════════════════════════════════════
    public Vector3 GetPosition()
    {
        return transform.position;
    }

    public float GetRotationY()
    {
        return transform.eulerAngles.y;
    }

    public bool IsLocalPlayer()
    {
        return view != null && view.IsMine;
    }
}