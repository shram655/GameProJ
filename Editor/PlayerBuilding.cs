using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

public class PlayerBuilding : MonoBehaviourPun
{
    [Header("═══ НАСТРОЙКИ ═══")]
    [Tooltip("Дальность УСТАНОВКИ блока (ПКМ)")]
    public float buildRange = 6f;

    [Header("═══ 🪓 ЛОМАНИЕ (удержание ЛКМ) ═══")]
    [Tooltip("Дальность РАЗРУШЕНИЯ (как у топора)")]
    public float destroyRange = 3f;
    [Tooltip("Время полного разрушения блока РУКОЙ (сек)")]
    public float breakTime = 1.0f;
    [Tooltip("Сила тряски блока")]
    public float shakeAmount = 0.05f;
    [Tooltip("Интервал спавна осколков (сек)")]
    public float debrisInterval = 0.15f;
    [Tooltip("Количество линий трещин")]
    public int crackCount = 6;
    [Tooltip("Толщина линии трещины")]
    public float crackWidth = 0.05f;

    [Header("═══ ⬜ ПОДСВЕТКА БЛОКА ═══")]
    [Tooltip("Цвет обводки")]
    public Color highlightColor = Color.white;
    [Tooltip("Небольшой отступ обводки")]
    public float highlightPadding = 0.02f;

    [Header("═══ ЗВУКИ ═══")]
    public AudioSource audioSource;
    public AudioClip buildSound;
    public AudioClip destroySound;
    public AudioClip crackSound;

    // 🆕 Состояние для топора: MeleeWeapon устанавливает это при экипировке
    [HideInInspector] public bool isToolEquipped = false;
    [HideInInspector] public float toolBreakTimeMultiplier = 1f; // 1 = рука, 0.25 = топор

    private Camera cam;
    private PlayerController playerController;
    private PlayerInventory playerInventory;
    private WorldManager worldManager;

    // ═══ Состояние разрушения ═══
    private GameObject currentTarget;
    private Vector3 breakOrigLocal;
    private float breakProgress;
    private float debrisTimer;
    private List<LineRenderer> crackLines = new List<LineRenderer>();
    private int revealedCracks = 0;

    // ═══ Подсветка ═══
    private GameObject highlightObj;

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        playerInventory = GetComponent<PlayerInventory>();
        worldManager = WorldManager.Instance != null ? WorldManager.Instance : FindObjectOfType<WorldManager>();

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        BuildHighlight();
    }

    void Update()
    {
        if (photonView == null || !photonView.IsMine) return;

        if (playerController == null || playerController.isPlayerDead) { HideHighlight(); return; }
        if (playerInventory == null || playerInventory.IsInventoryOpen) { HideHighlight(); CancelBreak(); return; }
        if (ChatManager.IsChatOpen) { HideHighlight(); CancelBreak(); return; }

        if (cam == null) cam = playerController.playerCamera != null ? playerController.playerCamera : Camera.main;
        if (cam == null) return;
        if (worldManager == null) worldManager = WorldManager.Instance;

        // 🆕 Проверка оружия: пистолет блокирует, топор — РАЗРЕШАЕТ разрушение
        bool hasGun = playerController.weaponManager != null && playerController.weaponManager.HasGunEquipped;

        if (hasGun) { HideHighlight(); CancelBreak(); return; }
        UpdateHighlight();

        // 🆕 Удержание ЛКМ: работает и для руки, и для топора
        if (Input.GetMouseButton(0)) UpdateBreaking();
        else CancelBreak();

        if (Input.GetMouseButtonDown(1)) TryPlaceBlock();
    }

    // ═════════════════════════════════════════════════════
    // ⬜ БЕЛАЯ ОБВОДКА (работает для руки И топора)
    // ═════════════════════════════════════════════════════
    void BuildHighlight()
    {
        highlightObj = new GameObject("BlockHighlight");

        var mf = highlightObj.AddComponent<MeshFilter>();
        var mr = highlightObj.AddComponent<MeshRenderer>();

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = highlightColor;
        mat.renderQueue = 5000;
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var m = new Mesh();
        m.vertices = new Vector3[]
        {
            new Vector3(-0.5f,-0.5f,-0.5f), new Vector3(0.5f,-0.5f,-0.5f),
            new Vector3(0.5f,-0.5f, 0.5f), new Vector3(-0.5f,-0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f,-0.5f), new Vector3(0.5f, 0.5f,-0.5f),
            new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
        };
        m.SetIndices(new int[]
        {
            0,1, 1,2, 2,3, 3,0,
            4,5, 5,6, 6,7, 7,4,
            0,4, 1,5, 2,6, 3,7
        }, MeshTopology.Lines, 0);
        mf.sharedMesh = m;

        highlightObj.SetActive(false);
    }

    void UpdateHighlight()
    {
        if (cam == null) { HideHighlight(); return; }

        float range = destroyRange;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            GameObject root = GetBlockRoot(hit.collider);
            if (root != null && TryGetColliderBounds(root, out Bounds b))
            {
                b.Expand(highlightPadding);
                highlightObj.transform.position = b.center;
                highlightObj.transform.localScale = b.size;
                highlightObj.SetActive(true);
                return;
            }
        }
        HideHighlight();
    }

    void HideHighlight()
    {
        if (highlightObj != null) highlightObj.SetActive(false);
    }

    bool TryGetColliderBounds(GameObject root, out Bounds b)
    {
        b = default;
        bool first = true;
        foreach (var c in root.GetComponentsInChildren<Collider>())
        {
            if (c == null || c.isTrigger) continue;
            if (first) { b = c.bounds; first = false; }
            else b.Encapsulate(c.bounds);
        }
        return !first;
    }

    // ═════════════════════════════════════════════════════
    // 🪓 ПРОЦЕСС РАЗРУШЕНИЯ (рука И топор используют одно)
    // ═════════════════════════════════════════════════════
    void UpdateBreaking()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, destroyRange))
        {
            CancelBreak();
            return;
        }

        GameObject root = GetBlockRoot(hit.collider);
        if (root == null)
        {
            CancelBreak();
            return;
        }

        if (currentTarget != root)
        {
            ResetShake();
            currentTarget = root;
            breakOrigLocal = root.transform.localPosition;
            breakProgress = 0f;
            revealedCracks = 0;
            ShowCracks(root, hit.point, hit.normal);
        }

        // 🆕 Учитываем множитель топора: если isToolEquipped, breakTime умножается
        float effectiveBreakTime = breakTime * toolBreakTimeMultiplier;
        breakProgress += Time.deltaTime / effectiveBreakTime;

        float amp = shakeAmount * breakProgress;
        root.transform.localPosition = breakOrigLocal + new Vector3(
            Random.Range(-amp, amp), Random.Range(-amp, amp), Random.Range(-amp, amp));

        int reveal = Mathf.CeilToInt(breakProgress * crackCount);
        if (reveal > revealedCracks)
        {
            for (int i = revealedCracks; i < reveal && i < crackLines.Count; i++)
                if (crackLines[i] != null) crackLines[i].gameObject.SetActive(true);
            revealedCracks = reveal;
            if (crackSound != null && audioSource != null) audioSource.PlayOneShot(crackSound);
        }

        debrisTimer += Time.deltaTime;
        if (debrisTimer >= debrisInterval)
        {
            debrisTimer = 0f;
            SpawnDebris(hit.point);
        }

        if (breakProgress >= 1f)
        {
            GameObject toDestroy = currentTarget;
            CancelBreak();
            PerformDestroy(toDestroy);
        }
    }

    GameObject GetBlockRoot(Collider col)
    {
        BlockDestroyer bd = col.GetComponentInParent<BlockDestroyer>();
        if (bd != null) return bd.gameObject;
        PlacedBlock pb = col.GetComponentInParent<PlacedBlock>();
        if (pb != null) return pb.gameObject;
        return null;
    }

    void CancelBreak()
    {
        ResetShake();
        currentTarget = null;
        breakProgress = 0f;
        revealedCracks = 0;
        HideCracks();
    }

    void ResetShake()
    {
        if (currentTarget != null) currentTarget.transform.localPosition = breakOrigLocal;
    }

    void DisableColliders(GameObject root)
    {
        if (root == null) return;
        foreach (var c in root.GetComponentsInChildren<Collider>())
            if (c != null) c.enabled = false;
    }

    void PerformDestroy(GameObject root)
    {
        if (root == null) return;

        DisableColliders(root);

        BlockDestroyer bd = root.GetComponentInParent<BlockDestroyer>();
        if (bd != null)
        {
            bd.RequestDestroy(playerInventory);
            PlaySound(destroySound);
            return;
        }

        PlacedBlock pb = root.GetComponentInParent<PlacedBlock>();
        if (pb == null) return;

        Vector3 pos = pb.transform.position;
        if (pb.blockId > 0) playerInventory.AddToInventory(pb.blockId);
        if (worldManager != null) worldManager.DestroyBlock(pos, PhotonNetwork.NickName);
        Destroy(pb.gameObject);

        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC("RPC_BlockDestroyed", RpcTarget.Others,
                Mathf.RoundToInt(pos.x * 100), Mathf.RoundToInt(pos.y * 100), Mathf.RoundToInt(pos.z * 100));
        }

        PlaySound(destroySound);
    }

    // ═════════════════════════════════════════════════════
    // 🕸️ 3D-ТРЕЩИНЫ
    // ═════════════════════════════════════════════════════
    void ShowCracks(GameObject root, Vector3 point, Vector3 normal)
    {
        HideCracks();

        Vector3 t1 = Vector3.Cross(normal, Vector3.up);
        if (t1.sqrMagnitude < 0.01f) t1 = Vector3.Cross(normal, Vector3.right);
        t1.Normalize();
        Vector3 t2 = Vector3.Cross(normal, t1).normalized;

        for (int i = 0; i < crackCount; i++)
        {
            var go = new GameObject("Crack_" + i);
            var lr = go.AddComponent<LineRenderer>();
            lr.transform.SetParent(root.transform);
            lr.useWorldSpace = false;
            lr.startWidth = crackWidth;
            lr.endWidth = crackWidth;
            lr.startColor = Color.black;
            lr.endColor = Color.black;

            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.renderQueue = 5000;
            lr.material = mat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            float a = Random.Range(-0.2f, 0.2f), b = Random.Range(-0.2f, 0.2f);
            float ang = Random.Range(0f, Mathf.PI * 2);
            int steps = Random.Range(4, 8);
            var pts = new List<Vector3>();
            for (int s = 0; s < steps; s++)
            {
                Vector3 world = point + t1 * a + t2 * b + normal * 0.05f;
                pts.Add(root.transform.InverseTransformPoint(world));
                ang += Random.Range(-0.9f, 0.9f);
                float step = Random.Range(0.08f, 0.16f);
                a = Mathf.Clamp(a + Mathf.Cos(ang) * step, -0.5f, 0.5f);
                b = Mathf.Clamp(b + Mathf.Sin(ang) * step, -0.5f, 0.5f);
            }
            lr.positionCount = pts.Count;
            lr.SetPositions(pts.ToArray());
            lr.gameObject.SetActive(false);
            crackLines.Add(lr);
        }

        if (crackLines.Count > 0) { crackLines[0].gameObject.SetActive(true); revealedCracks = 1; }
    }

    void HideCracks()
    {
        for (int i = 0; i < crackLines.Count; i++)
            if (crackLines[i] != null) Destroy(crackLines[i].gameObject);
        crackLines.Clear();
    }

    void SpawnDebris(Vector3 point)
    {
        for (int i = 0; i < 3; i++)
        {
            var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
            d.name = "Debris";
            d.transform.position = point + Random.insideUnitSphere * 0.2f;
            d.transform.localScale = Vector3.one * 0.07f;

            Collider col = d.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rb = d.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.velocity = Random.onUnitSphere * 2f + Vector3.up * 1.5f;
            Destroy(d, 0.8f);
        }
    }

    // ═════════════════════════════════════════════════════
    // 🧱 ПКМ: УСТАНОВКА БЛОКА
    // ═════════════════════════════════════════════════════
    void TryPlaceBlock()
    {
        int blockId = playerInventory.inventory[playerInventory.selectedSlot];
        if (blockId <= 0) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, buildRange)) return;

        Vector3 raw = hit.point + hit.normal * 0.5f;
        Vector3 pos = new Vector3(Mathf.Round(raw.x), Mathf.Round(raw.y), Mathf.Round(raw.z));

        if (Vector3.Distance(pos, transform.position) < 1.2f) return;

        if (worldManager != null) worldManager.PlaceBlock(blockId, pos);

        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC("RPC_BlockPlaced", RpcTarget.Others,
                blockId,
                Mathf.RoundToInt(pos.x * 100), Mathf.RoundToInt(pos.y * 100), Mathf.RoundToInt(pos.z * 100));
        }

        playerInventory.inventoryCounts[playerInventory.selectedSlot]--;
        if (playerInventory.inventoryCounts[playerInventory.selectedSlot] <= 0)
        {
            playerInventory.inventory[playerInventory.selectedSlot] = 0;
            playerInventory.inventoryCounts[playerInventory.selectedSlot] = 0;
        }
        playerInventory.UpdateHotbarUI();
        if (playerInventory.inventoryUI != null) playerInventory.inventoryUI.UpdateAllSlots();

        PlaySound(buildSound);
    }

    // ═════════════════════════════════════════════════════
    // 🌐 СЕТЬ
    // ═════════════════════════════════════════════════════
    [PunRPC]
    void RPC_BlockPlaced(int blockId, int x, int y, int z)
    {
        Vector3 pos = new Vector3(x / 100f, y / 100f, z / 100f);
        if (worldManager == null) worldManager = WorldManager.Instance;
        if (worldManager != null) worldManager.HandleBlockPlaced(blockId, pos);
    }

    [PunRPC]
    void RPC_BlockDestroyed(int x, int y, int z)
    {
        Vector3 pos = new Vector3(x / 100f, y / 100f, z / 100f);
        if (worldManager == null) worldManager = WorldManager.Instance;
        if (worldManager != null) worldManager.HandleBlockDestroyed(pos);

        PlacedBlock pb = FindBlockNear<PlacedBlock>(pos, 0.4f);
        if (pb != null) Destroy(pb.gameObject);
    }

    T FindBlockNear<T>(Vector3 pos, float radius) where T : Component
    {
        T best = null;
        float bestDist = radius;
        foreach (var c in FindObjectsOfType<T>())
        {
            if (c == null) continue;
            float d = Vector3.Distance(c.transform.position, pos);
            if (d < bestDist) { bestDist = d; best = c; }
        }
        return best;
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    void OnDestroy()
    {
        HideCracks();
        if (highlightObj != null) Destroy(highlightObj);
    }
}