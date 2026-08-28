using UnityEngine;
using Photon.Pun;

public class PlayerBuilding : MonoBehaviourPun
{
    [Header("Настройки строительства")]
    public float buildRange = 10f;

    [Header("UI и Звуки")]
    public AudioSource audioSource;
    public AudioClip buildSound;
    public AudioClip destroySound;

    private Camera cam;
    private PlayerController playerController;
    private PlayerInventory playerInventory;
    private WorldManager worldManager;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        playerInventory = GetComponent<PlayerInventory>();
    }

    void Start()
    {
        if (playerController != null && playerController.view.IsMine)
        {
            cam = playerController.playerCamera;
        }

        worldManager = FindObjectOfType<WorldManager>();

        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        if (!photonView.IsMine) return;
        if (playerController.isPlayerDead) return;
        if (playerInventory.IsInventoryOpen) return;
        if (cam == null) return;

        if (Input.GetKeyUp(KeyCode.Mouse0)) TryPlaceBlock();
        if (Input.GetKeyUp(KeyCode.Mouse1)) TryDestroyBlock();
    }

    bool HasGunInHands()
    {
        if (playerInventory != null)
        {
            return playerInventory.IsGun(playerInventory.inventory[playerInventory.selectedSlot]);
        }
        return playerController != null
            && playerController.weaponManager != null
            && playerController.weaponManager.HasWeaponEquipped;
    }

    private void TryPlaceBlock()
    {
        if (HasGunInHands()) return;

        int currentBlockId = playerInventory.inventory[playerInventory.selectedSlot];
        if (currentBlockId <= 0) return;

        Ray spawnRay = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(spawnRay, out RaycastHit hit, buildRange))
        {
            Vector3 spawnPosition = hit.point;
            bool placed = false;

            if (hit.transform.tag == "floor")
            {
                spawnPosition = hit.point;
                placed = true;
            }
            else if (hit.transform.gameObject.name.Contains("Trigger"))
            {
                string n = hit.transform.gameObject.name;

                if (n.Contains("-Y")) { spawnPosition = hit.transform.position + new Vector3(0, -0.459f, 0); placed = true; }
                else if (n.Contains("Y")) { spawnPosition = hit.transform.position + new Vector3(0, 0.459f, 0); placed = true; }
                else if (n.Contains("-X")) { spawnPosition = hit.transform.position + new Vector3(-0.459f, 0, 0); placed = true; }
                else if (n.Contains("X")) { spawnPosition = hit.transform.position + new Vector3(0.459f, 0, 0); placed = true; }
                else if (n.Contains("-Z")) { spawnPosition = hit.transform.position + new Vector3(0, 0, -0.459f); placed = true; }
                else if (n.Contains("Z")) { spawnPosition = hit.transform.position + new Vector3(0, 0, 0.459f); placed = true; }
            }

            if (placed && worldManager != null)
            {
                worldManager.PlaceBlock(currentBlockId, spawnPosition);

                if (PhotonNetwork.IsConnected)
                {
                    photonView.RPC("RPC_BlockPlaced", RpcTarget.All, currentBlockId,
                        Mathf.RoundToInt(spawnPosition.x * 100),
                        Mathf.RoundToInt(spawnPosition.y * 100),
                        Mathf.RoundToInt(spawnPosition.z * 100));
                }

                if (audioSource != null && buildSound != null) audioSource.PlayOneShot(buildSound);

                playerInventory.inventoryCounts[playerInventory.selectedSlot]--;
                if (playerInventory.inventoryCounts[playerInventory.selectedSlot] <= 0)
                    playerInventory.inventory[playerInventory.selectedSlot] = 0;

                playerInventory.UpdateHotbarUI();
                if (playerInventory.inventoryUI != null) playerInventory.inventoryUI.UpdateAllSlots();
            }
        }
    }

    private void TryDestroyBlock()
    {
        if (HasGunInHands()) return;

        Ray spawnRay = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(spawnRay, out RaycastHit hit, buildRange))
        {
            GameObject blockRoot = hit.transform.gameObject;
            while (blockRoot.transform.parent != null && blockRoot.transform.parent.name != "WorldBlocks")
            {
                blockRoot = blockRoot.transform.parent.gameObject;
            }

            BlockDestroyer blockDestroyer = blockRoot.GetComponent<BlockDestroyer>();
            if (blockDestroyer != null)
            {
                blockDestroyer.RequestDestroy(playerInventory);
                PlaySound(destroySound);
                return;
            }

            PlacedBlock placedBlock = blockRoot.GetComponent<PlacedBlock>();
            if (placedBlock != null)
            {
                playerInventory.AddToInventory(placedBlock.blockId);

                if (worldManager != null)
                {
                    worldManager.DestroyBlock(blockRoot.transform.position, PhotonNetwork.NickName);
                }

                if (PhotonNetwork.IsConnected)
                {
                    photonView.RPC("RPC_BlockDestroyed", RpcTarget.All,
                        Mathf.RoundToInt(blockRoot.transform.position.x * 100),
                        Mathf.RoundToInt(blockRoot.transform.position.y * 100),
                        Mathf.RoundToInt(blockRoot.transform.position.z * 100));
                }

                Destroy(blockRoot);
                PlaySound(destroySound);
                return;
            }
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    // ═════════════════════════════════════════════════════
    // СЕТЕВЫЕ RPC
    // ═════════════════════════════════════════════════════

    // 🆕 ТОЛЬКО разрушение блока на других клиентах. БЕЗ ЛУТА!
    [PunRPC]
    void RPC_DestroyLootBlock(int x, int y, int z)
    {
        Vector3 position = new Vector3(x / 100f, y / 100f, z / 100f);
        Debug.Log($"📡 RPC_DestroyLootBlock получен: {position}");

        BlockDestroyer target = null;
        float bestDist = 0.6f;

        BlockDestroyer[] allDestroyers = FindObjectsOfType<BlockDestroyer>();
        foreach (var bd in allDestroyers)
        {
            if (bd == null) continue;
            float d = Vector3.Distance(bd.transform.position, position);
            if (d < bestDist)
            {
                bestDist = d;
                target = bd;
            }
        }

        if (worldManager == null) worldManager = FindObjectOfType<WorldManager>();

        if (target != null)
        {
            if (worldManager != null) worldManager.DestroyBlock(position, "");
            Destroy(target.gameObject);
            Debug.Log($"💥 RPC: блок уничтожен на этом клиенте");
        }
        else
        {
            if (worldManager != null) worldManager.HandleBlockDestroyed(position);
        }
    }

    [PunRPC]
    void RPC_BlockDestroyed(int x, int y, int z)
    {
        Vector3 position = new Vector3(x / 100f, y / 100f, z / 100f);
        if (worldManager == null) worldManager = FindObjectOfType<WorldManager>();
        if (worldManager != null) worldManager.HandleBlockDestroyed(position);
    }

    [PunRPC]
    void RPC_BlockPlaced(int blockId, int x, int y, int z)
    {
        Vector3 position = new Vector3(x / 100f, y / 100f, z / 100f);
        if (worldManager == null) worldManager = FindObjectOfType<WorldManager>();
        if (worldManager != null) worldManager.HandleBlockPlaced(blockId, position);
    }
}