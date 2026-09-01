using UnityEngine;
using Photon.Pun;
using System.Collections;

public class MeleeWeapon : MonoBehaviourPun
{
    [Header("Настройки")]
    public string meleeName = "Топор";
    public int meleeId = 1;
    public int slotIndex = -1;
    public float damage = 35f;
    public float range = 2.5f;
    public float attackRate = 0.6f;

    [Header("Анимация удара")]
    [Range(30f, 90f)] public float swingAngle = 60f;
    public float swingDuration = 0.3f;

    [Header("🆕 СКОРОСТЬ ЛОМАНИЯ БЛОКОВ")]
    [Tooltip("Множитель времени разрушения (0.25 = ломает в 4 раза быстрее руки)")]
    public float breakSpeedMultiplier = 0.25f;

    [Header("Ссылки")]
    public Camera fpsCam;
    public PlayerInventory playerInventory;
    public PlayerWeaponManager weaponManager;

    [Header("═══ 🎯 FPS: ТОНКАЯ ПОДГОНКА (относительно WeaponSlot) ═══")]
    public Vector3 fpsOffsetPosition = Vector3.zero;
    public Vector3 fpsOffsetRotation = Vector3.zero;

    [Header("═══ 🖐 3-е ЛИЦО: ПОЗИЦИЯ В КИСТИ (WeaponAnchor) ═══")]
    public Vector3 handPosition = Vector3.zero;
    public Vector3 handRotation = Vector3.zero;

    private float nextAttackTime = 0f;
    private float swingT = -1f;
    private bool isEquipped = false;
    private WorldManager worldManager;
    private CubeWorldCharacter ownerCharacter;
    private PlayerBuilding playerBuilding;

    bool IsLocal() => photonView == null || photonView.IsMine;

    void Awake()
    {
        if (IsLocal()) gameObject.SetActive(false);
    }

    void Start()
    {
        worldManager = FindObjectOfType<WorldManager>();
        playerBuilding = GetComponentInParent<PlayerBuilding>();

        if (IsLocal()) StartCoroutine(SelfEquipFallback());
        else StartCoroutine(AttachToOwner());
    }

    IEnumerator SelfEquipFallback()
    {
        float w = 0f;
        while (!isEquipped && w < 2f)
        {
            if (fpsCam != null) { yield return null; if (!isEquipped) Equip(); yield break; }
            w += Time.deltaTime; yield return null;
        }
    }

    IEnumerator AttachToOwner()
    {
        for (int i = 0; i < 60; i++)
        {
            if (this == null) yield break;
            foreach (var pc in FindObjectsOfType<PlayerController>())
            {
                if (pc.view != null && photonView != null && pc.view.OwnerActorNr == photonView.OwnerActorNr)
                {
                    CubeWorldCharacter cw = pc.GetComponent<CubeWorldCharacter>();
                    if (cw != null && cw.WeaponAnchor != null)
                    {
                        ownerCharacter = cw;
                        if (transform.parent != cw.WeaponAnchor)
                            transform.SetParent(cw.WeaponAnchor);
                        transform.localPosition = handPosition;
                        transform.localRotation = Quaternion.Euler(handRotation);
                        isEquipped = true;
                        gameObject.SetActive(true);
                    }
                    yield break;
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    void OnDestroy() { ownerCharacter = null; }

    bool IsSelf(Transform t)
    {
        if (ownerCharacter == null) return false;
        return t == ownerCharacter.transform || t.IsChildOf(ownerCharacter.transform);
    }

    void Update()
    {
        if (photonView != null && !photonView.IsMine) return;
        if (!isEquipped || fpsCam == null) return;
        if (playerInventory != null && playerInventory.IsInventoryOpen) return;

        // 🆕 Удар по ИГРОКАМ (ЛКМ одноразово)
        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackRate;
            SwingAtPlayers();
        }
        // 🆕 ЛОМАНИЕ БЛОКОВ теперь делает PlayerBuilding через удержание ЛКМ
    }

    // 🆕 Удар только по игрокам/мобам. Блоки НЕ трогаем — это делает PlayerBuilding
    void SwingAtPlayers()
    {
        swingT = 0f;
        if (PhotonNetwork.IsConnected && photonView != null)
            photonView.RPC("RPC_PlaySwingAnimation", RpcTarget.Others);

        RaycastHit[] hits = Physics.RaycastAll(fpsCam.transform.position, fpsCam.transform.forward, range);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (IsSelf(hit.transform)) continue;

            // Ищем игрока
            PlayerHealth t = hit.transform.GetComponent<PlayerHealth>();
            if (t == null) t = hit.transform.GetComponentInParent<PlayerHealth>();
            if (t != null)
            {
                if (!(photonView != null && photonView.IsMine && t.photonView != null && t.photonView.IsMine))
                    t.photonView.RPC("RPC_TakeDamage", t.photonView.Owner, damage);
                break;
            }
        }
    }

    [PunRPC] void RPC_PlaySwingAnimation() { swingT = 0f; }

    [PunRPC] void RPC_SetEquipped(bool v)
    {
        isEquipped = v;
        if (gameObject.activeSelf != v) gameObject.SetActive(v);
        if (!v) swingT = -1f;
    }

    void LateUpdate()
    {
        if (!isEquipped) return;

        Vector3 so = Vector3.zero; Quaternion sr = Quaternion.identity;
        if (swingT >= 0f)
        {
            swingT += Time.deltaTime;
            float p = swingT / swingDuration;
            if (p >= 1f) swingT = -1f;
            else
            {
                float c = Mathf.Sin(p * Mathf.PI);
                sr = Quaternion.Euler(c * swingAngle, 0f, -c * swingAngle * 0.15f);
                so = new Vector3(0f, -c * 0.08f, c * 0.15f);
            }
        }

        if (IsLocal())
        {
            transform.localPosition = fpsOffsetPosition + so;
            transform.localRotation = Quaternion.Euler(fpsOffsetRotation) * sr;
        }
        else
        {
            transform.localPosition = handPosition + so;
            transform.localRotation = Quaternion.Euler(handRotation) * sr;
        }
    }

    public void Equip()
    {
        if (photonView != null && !photonView.IsMine) return;

        bool first = !isEquipped;
        isEquipped = true;
        gameObject.SetActive(true);

        if (ownerCharacter == null)
        {
            ownerCharacter = GetComponentInParent<CubeWorldCharacter>();
            if (ownerCharacter == null)
            {
                PlayerController pc = GetComponentInParent<PlayerController>();
                if (pc != null) ownerCharacter = pc.GetComponent<CubeWorldCharacter>();
            }
        }

        if (ownerCharacter != null) ownerCharacter.SetHasWeapon(true);

        if (fpsCam != null)
        {
            Transform slot = fpsCam.transform.Find("WeaponSlot");
            if (slot == null) slot = fpsCam.transform;
            if (transform.parent != slot) transform.SetParent(slot);
            transform.localPosition = fpsOffsetPosition;
            transform.localRotation = Quaternion.Euler(fpsOffsetRotation);
        }

        // 🆕 Сообщаем PlayerBuilding что топор экипирован
        if (playerBuilding == null) playerBuilding = GetComponentInParent<PlayerBuilding>();
        if (playerBuilding != null)
        {
            playerBuilding.isToolEquipped = true;
            playerBuilding.toolBreakTimeMultiplier = breakSpeedMultiplier;
        }

        if (PhotonNetwork.IsConnected && photonView != null)
            photonView.RPC("RPC_SetEquipped", RpcTarget.Others, true);
    }

    public void Unequip()
    {
        if (photonView != null && !photonView.IsMine) return;

        isEquipped = false;
        swingT = -1f;
        gameObject.SetActive(false);

        // 🆕 Сбрасываем состояние в PlayerBuilding
        if (playerBuilding != null)
        {
            playerBuilding.isToolEquipped = false;
            playerBuilding.toolBreakTimeMultiplier = 1f;
        }

        if (PhotonNetwork.IsConnected && photonView != null)
            photonView.RPC("RPC_SetEquipped", RpcTarget.Others, false);
    }
}