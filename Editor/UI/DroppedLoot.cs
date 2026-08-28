using UnityEngine;
using Photon.Pun;

public class DroppedLoot : MonoBehaviourPun
{
    [Header("═══ ВИЗУАЛ КУБИКА ═══")]
    public float cubeSize = 0.25f;
    public float bobHeight = 0.02f;
    public float spinSpeed = 40f;

    [Header("═══ ИКОНКА НАД ЛУТОМ ═══")]
    public bool showIcon = true;
    public float iconSize = 0.35f;
    public float iconHeight = 0.5f;

    [Header("═══ ДАННЫЕ ЛУТА ═══")]
    public int itemId;
    public int count = 1;
    [HideInInspector] public bool picked = false;

    private GameObject cube;
    private GameObject iconObj;
    private Renderer iconRenderer;
    private Material iconMat;
    private float iconRetry = 0f;
    private Camera cachedCam; // 🆕 камера локального игрока

    void Start()
    {
        SnapToGround();
        BuildVisual();
        BuildIcon();
    }

    [PunRPC]
    public void SetData(int id, int c) { ApplyData(id, c); }

    public void ApplyData(int id, int c)
    {
        itemId = id;
        count = c;
        SnapToGround();
        BuildVisual();
        BuildIcon();
    }

    void SnapToGround()
    {
        Vector3 origin = transform.position + Vector3.up * 1f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 3f))
            transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
    }

    void BuildVisual()
    {
        if (cube == null)
        {
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "LootCube";
            cube.transform.SetParent(transform);
        }
        cube.transform.localScale = Vector3.one * cubeSize;
        var r = cube.GetComponent<Renderer>();
        if (r != null) r.material.color = GetColor();
    }

    Sprite GetIcon()
    {
        if (InventoryUI.Instance != null)
            return InventoryUI.Instance.GetIconForItem(itemId);
        return null;
    }

    void BuildIcon()
    {
        if (!showIcon) { if (iconObj != null) iconObj.SetActive(false); return; }

        Sprite sp = GetIcon();
        if (sp == null)
        {
            if (iconObj != null) iconObj.SetActive(false);
            iconRetry = Time.time + 0.5f;
            return;
        }

        if (iconObj == null)
        {
            iconObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            iconObj.name = "LootIcon";
            iconObj.transform.SetParent(transform);
            Destroy(iconObj.GetComponent<Collider>());
            iconRenderer = iconObj.GetComponent<Renderer>();
            iconMat = new Material(Shader.Find("Sprites/Default"));
            iconRenderer.material = iconMat;
            iconRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        iconMat.mainTexture = sp.texture;
        iconObj.SetActive(true);
        iconObj.transform.localScale = Vector3.one * iconSize;
    }

    // 🆕 Надёжно получаем камеру ЛОКАЛЬНОГО игрока (не Camera.main)
    Camera GetCam()
    {
        if (cachedCam != null) return cachedCam;
        var pc = FindLocalPlayer();
        if (pc != null && pc.playerCamera != null) cachedCam = pc.playerCamera;
        else cachedCam = Camera.main;
        return cachedCam;
    }

    Color GetColor()
    {
        var inv = FindLocalInv();
        if (inv == null) return Color.gray;
        if (inv.IsGun(itemId)) return new Color(0.2f, 0.2f, 0.25f);
        if (inv.IsMelee(itemId)) return new Color(0.5f, 0.3f, 0.1f);
        if (inv.IsAmmo(itemId)) return Color.yellow;
        if (inv.IsFood(itemId)) return Color.red;
        return Color.gray;
    }

    PlayerInventory FindLocalInv()
    {
        foreach (var i in FindObjectsOfType<PlayerInventory>())
        {
            var pc = i.GetComponent<PlayerController>();
            if (pc != null && pc.view != null && pc.view.IsMine) return i;
        }
        return null;
    }

    PlayerController FindLocalPlayer()
    {
        foreach (var p in FindObjectsOfType<PlayerController>())
            if (p.view != null && p.view.IsMine) return p;
        return null;
    }

    void Update()
    {
        if (cube != null)
        {
            cube.transform.localPosition = new Vector3(0, cubeSize * 0.5f + Mathf.Sin(Time.time * 2f) * bobHeight, 0);
            cube.transform.Rotate(Vector3.up, Time.deltaTime * spinSpeed);
        }

        if (showIcon && (iconObj == null || !iconObj.activeSelf) && Time.time >= iconRetry)
            BuildIcon();
    }

    void LateUpdate()
    {
        // 🆕 Иконка висит над кубиком и ВСЕГДА повёрнута лицом к камере игрока
        if (iconObj != null && iconObj.activeSelf)
        {
            iconObj.transform.localPosition = new Vector3(0, iconHeight, 0);

            Camera cam = GetCam();
            if (cam != null)
            {
                Vector3 dir = cam.transform.position - iconObj.transform.position;
                if (dir.sqrMagnitude > 0.001f)
                    iconObj.transform.rotation = Quaternion.LookRotation(dir);
            }
        }
    }

    public void TryPickup(PlayerController pc)
    {
        if (picked) return;
        if (photonView != null && PhotonNetwork.IsConnected)
        {
            int me = PhotonNetwork.LocalPlayer.ActorNumber;
            photonView.RPC("RPC_Pickup", RpcTarget.AllBuffered, me);
        }
        else
        {
            AddToMyInventory();
            Destroy(gameObject);
        }
    }

    [PunRPC]
    public void RPC_Pickup(int pickerActor)
    {
        if (picked) return;
        picked = true;

        if (PhotonNetwork.LocalPlayer.ActorNumber == pickerActor)
            AddToMyInventory();

        if (photonView.IsMine)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject, 0.3f);
    }

    void AddToMyInventory()
    {
        var pc = FindLocalPlayer();
        if (pc == null) return;
        var inv = pc.inventory;
        var wm = pc.weaponManager;
        if (inv == null) return;

        if (inv.IsGun(itemId))
        {
            int wid = -(itemId + 100);
            if (wm != null) wm.AddWeaponToInventory(wid, wm.GetWeaponData(wid));
            for (int i = 0; i < 20; i++)
                if (inv.inventory[i] == itemId) { inv.inventoryCounts[i] = count; break; }
        }
        else if (inv.IsMelee(itemId))
        {
            inv.AddMeleeToInventory(inv.GetMeleeIdFromItemId(itemId));
        }
        else if (inv.IsAmmo(itemId))
        {
            inv.AddAmmoOfType(itemId, count);
        }
        else
        {
            for (int i = 0; i < count; i++) inv.AddToInventory(itemId);
        }

        inv.UpdateHotbarUI();
        if (inv.inventoryUI != null) inv.inventoryUI.UpdateAllSlots();
    }
}