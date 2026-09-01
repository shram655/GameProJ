using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Globalization;
using System.Collections;

public class WorldManager : MonoBehaviourPunCallbacks
{
    [Header("Настройки")]
    public GameObject[] blockPrefabs;

    [Header("Ссылки")]
    public Transform worldParent;

    private Dictionary<string, GameObject> activeBlocks = new Dictionary<string, GameObject>();
    private HashSet<string> processedDestroyKeys = new HashSet<string>();
    private bool isWorldSynced = false;

    private const int POSITION_MULTIPLIER = 100;

    public static WorldManager Instance;

    private List<PendingRespawn> pendingRespawns = new List<PendingRespawn>();

    [System.Serializable]
    public class PendingRespawn
    {
        public int blockId;
        public Vector3 position;
        public float timeRemaining;
        public string key;
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (worldParent == null)
        {
            GameObject worldObj = new GameObject("WorldBlocks");
            worldParent = worldObj.transform;
        }

        if (photonView == null)
        {
            Debug.LogError("❌ WorldManager не имеет PhotonView!");
            return;
        }

        Debug.Log($"✅ WorldManager Start. IsMasterClient: {PhotonNetwork.IsMasterClient}");

        if (PhotonNetwork.IsMasterClient)
        {
            ScanExistingBlocks();
            DeduplicateActiveBlocks();
            isWorldSynced = true;
            Debug.Log($"✅ Мастер. Активных блоков: {activeBlocks.Count}");
        }
        else
        {
            Debug.Log("🔄 Не мастер, запрашиваем синхронизацию...");
            Invoke(nameof(RequestWorldSync), 2.0f);
        }

        StartCoroutine(UpdateRespawns());
    }

    // ═════════════════════════════════════════════════════
    // Разрушение сундука: локальный учёт + RPC ДРУГИМ
    // ═════════════════════════════════════════════════════
    public void NotifyLootBlockDestroyed(Vector3 position)
    {
        HandleBlockDestroyed(position);

        if (PhotonNetwork.IsConnected && photonView != null)
        {
            photonView.RPC("RPC_RemoteLootBlockDestroyed", RpcTarget.Others,
                Mathf.RoundToInt(position.x * 100),
                Mathf.RoundToInt(position.y * 100),
                Mathf.RoundToInt(position.z * 100));
        }
    }

    [PunRPC]
    void RPC_RemoteLootBlockDestroyed(int x, int y, int z)
    {
        Vector3 position = new Vector3(x / 100f, y / 100f, z / 100f);
        Debug.Log($"📡 RPC_RemoteLootBlockDestroyed: {position}");

        HandleBlockDestroyed(position);

        BlockDestroyer target = null;
        float bestDist = 0.6f;
        foreach (var bd in FindObjectsOfType<BlockDestroyer>())
        {
            if (bd == null) continue;
            float d = Vector3.Distance(bd.transform.position, position);
            if (d < bestDist) { bestDist = d; target = bd; }
        }

        if (target != null)
        {
            Destroy(target.gameObject);
            Debug.Log("💥 RPC: блок уничтожен на этом клиенте");
        }
    }

    public void RegisterAliveBlock(GameObject block, Vector3 position)
    {
        if (block == null) return;

        string key = GetBlockKey(position.x, position.y, position.z);

        if (processedDestroyKeys.Remove(key))
        {
            Debug.Log($"♻️ Ключ {key} убран из processedDestroyKeys — блок снова живой");
        }

        if (!activeBlocks.ContainsKey(key))
        {
            activeBlocks.Add(key, block);
        }
    }

    // ═════════════════════════════════════════════════════
    // РЕСПАВН: очередь теперь есть у ВСЕХ клиентов
    // ═════════════════════════════════════════════════════
    public void ScheduleBlockRespawn(int blockId, Vector3 position, float delay)
    {
        if (blockId < 1 || blockId > blockPrefabs.Length)
        {
            Debug.LogWarning($"⚠️ ScheduleBlockRespawn: некорректный blockId={blockId}");
            return;
        }

        string key = GetBlockKey(position.x, position.y, position.z);

        if (PhotonNetwork.IsMasterClient)
        {
            AddPendingRespawn(blockId, position, delay, key);

            // 🆕 Рассылаем очередь всем, чтобы новый хост не потерял респавн
            if (PhotonNetwork.IsConnected && photonView != null)
            {
                photonView.RPC("RPC_AddPendingRespawn", RpcTarget.Others, blockId,
                    Mathf.RoundToInt(position.x * 100),
                    Mathf.RoundToInt(position.y * 100),
                    Mathf.RoundToInt(position.z * 100),
                    delay);
            }

            Debug.Log($"⏳ Мастер: сундук #{blockId} заспавнится через {delay}с на {position}");
        }
        else
        {
            photonView.RPC("RPC_ScheduleRespawn", RpcTarget.MasterClient, blockId,
                Mathf.RoundToInt(position.x * 100),
                Mathf.RoundToInt(position.y * 100),
                Mathf.RoundToInt(position.z * 100),
                delay);
        }
    }

    void AddPendingRespawn(int blockId, Vector3 position, float delay, string key)
    {
        foreach (var p in pendingRespawns)
        {
            if (p.key == key)
            {
                Debug.Log($"⚠️ Респавн уже запланирован для {key}, пропускаем");
                return;
            }
        }

        pendingRespawns.Add(new PendingRespawn
        {
            blockId = blockId,
            position = position,
            timeRemaining = delay,
            key = key
        });
    }

    [PunRPC]
    void RPC_AddPendingRespawn(int blockId, int x, int y, int z, float delay)
    {
        Vector3 pos = new Vector3(x / 100f, y / 100f, z / 100f);
        string key = GetBlockKey(pos.x, pos.y, pos.z);
        AddPendingRespawn(blockId, pos, delay, key);
    }

    [PunRPC]
    void RPC_ScheduleRespawn(int blockId, int x, int y, int z, float delay, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        Vector3 pos = new Vector3(x / 100f, y / 100f, z / 100f);
        ScheduleBlockRespawn(blockId, pos, delay);
        Debug.Log($"✅ Мастер получил запрос респавна от {info.Sender.NickName}");
    }

    IEnumerator UpdateRespawns()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            // Тикает ТОЛЬКО мастер (у него актуальная очередь)
            if (!PhotonNetwork.IsMasterClient) continue;

            List<PendingRespawn> toRemove = new List<PendingRespawn>();

            foreach (var r in pendingRespawns)
            {
                r.timeRemaining -= 1f;

                if (r.timeRemaining <= 0f)
                {
                    if (!activeBlocks.ContainsKey(r.key) && !HasBlockNear(r.position))
                    {
                        SpawnBlockNetworked(r.blockId, r.position);
                        Debug.Log($"✅ Респавн: блок #{r.blockId} появился на {r.position}");
                    }
                    else
                    {
                        Debug.Log($"⚠️ Респавн пропущен — блок уже есть на {r.position}");
                    }
                    toRemove.Add(r);
                }
            }

            foreach (var r in toRemove) pendingRespawns.Remove(r);
        }
    }

    // ═════════════════════════════════════════════════════
    // 🆕 СПАВН БЛОКА: обычный Instantiate на КАЖДОМ клиенте.
    // Такие блоки НИЧЬИ — они НЕ исчезают при выходе игрока!
    // ═════════════════════════════════════════════════════
    void SpawnBlockNetworked(int blockId, Vector3 position)
    {
        if (blockId < 1 || blockId > blockPrefabs.Length) return;

        // Локально у мастера
        SpawnBlockLocal(blockId, position);

        // И у всех остальных
        if (PhotonNetwork.IsConnected && photonView != null)
        {
            photonView.RPC("RPC_SpawnBlock", RpcTarget.Others, blockId,
                Mathf.RoundToInt(position.x * 100),
                Mathf.RoundToInt(position.y * 100),
                Mathf.RoundToInt(position.z * 100));
        }
    }

    [PunRPC]
    void RPC_SpawnBlock(int blockId, int x, int y, int z)
    {
        Vector3 pos = new Vector3(x / 100f, y / 100f, z / 100f);
        SpawnBlockLocal(blockId, pos);
        Debug.Log($"✅ RPC_SpawnBlock: блок #{blockId} появился на {pos}");
    }

    void SpawnBlockLocal(int blockId, Vector3 position)
    {
        if (blockId < 1 || blockId > blockPrefabs.Length) return;

        GameObject prefab = blockPrefabs[blockId - 1];
        if (prefab == null)
        {
            Debug.LogError($"❌ Префаб с blockId={blockId} не найден в blockPrefabs!");
            return;
        }

        // Защита от дублей
        if (HasBlockNear(position)) return;

        GameObject blockObj = Instantiate(prefab, position, Quaternion.identity);
        blockObj.transform.SetParent(worldParent);

        RegisterAliveBlock(blockObj, position);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log($"👑 Смена мастера! Новый мастер: {newMasterClient.NickName}");
        if (PhotonNetwork.IsMasterClient)
        {
            DeduplicateActiveBlocks();
            isWorldSynced = true;
            // 🆕 Очередь респавнов уже есть у этого клиента (рассылали всем) —
            // таймеры продолжат тикать автоматически
            Debug.Log($"✅ Я теперь мастер. Активных блоков: {activeBlocks.Count}, респавнов в очереди: {pendingRespawns.Count}");
        }
    }

    void ScanExistingBlocks()
    {
        activeBlocks.Clear();
        PlacedBlock[] placedBlocks = FindObjectsOfType<PlacedBlock>();
        BlockDestroyer[] lootBlocks = FindObjectsOfType<BlockDestroyer>();

        foreach (PlacedBlock pb in placedBlocks)
        {
            if (pb == null) continue;
            string key = GetBlockKey(pb.transform.position.x, pb.transform.position.y, pb.transform.position.z);
            if (!activeBlocks.ContainsKey(key)) activeBlocks.Add(key, pb.gameObject);
        }

        foreach (BlockDestroyer bd in lootBlocks)
        {
            if (bd == null) continue;
            string key = GetBlockKey(bd.transform.position.x, bd.transform.position.y, bd.transform.position.z);
            if (!activeBlocks.ContainsKey(key)) activeBlocks.Add(key, bd.gameObject);
        }

        Debug.Log($"📊 Найдено блоков: {activeBlocks.Count}");
    }

    void DeduplicateActiveBlocks()
    {
        List<string> keys = new List<string>(activeBlocks.Keys);
        List<string> toRemove = new List<string>();

        for (int i = 0; i < keys.Count; i++)
        {
            if (toRemove.Contains(keys[i])) continue;
            if (!activeBlocks.ContainsKey(keys[i]) || activeBlocks[keys[i]] == null) continue;

            Vector3 posI = activeBlocks[keys[i]].transform.position;

            for (int j = i + 1; j < keys.Count; j++)
            {
                if (toRemove.Contains(keys[j])) continue;
                if (!activeBlocks.ContainsKey(keys[j]) || activeBlocks[keys[j]] == null) continue;

                Vector3 posJ = activeBlocks[keys[j]].transform.position;
                if (Vector3.Distance(posI, posJ) < 0.5f)
                {
                    toRemove.Add(keys[j]);
                }
            }
        }

        foreach (string key in toRemove)
        {
            if (activeBlocks.ContainsKey(key))
            {
                if (activeBlocks[key] != null) Destroy(activeBlocks[key]);
                activeBlocks.Remove(key);
            }
        }

        if (toRemove.Count > 0)
            Debug.Log($"🧹 Удалено дубликатов блоков: {toRemove.Count}");
    }

    bool HasBlockNear(Vector3 position)
    {
        foreach (var kvp in activeBlocks)
        {
            if (kvp.Value == null) continue;
            if (Vector3.Distance(kvp.Value.transform.position, position) < 0.5f) return true;
        }
        return false;
    }

    void RequestWorldSync()
    {
        if (PhotonNetwork.IsMasterClient || isWorldSynced) return;
        if (photonView == null) return;
        photonView.RPC("RequestFullWorldSync", PhotonNetwork.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);
        Invoke(nameof(ForceSync), 5.0f);
    }

    void ForceSync()
    {
        if (!isWorldSynced) isWorldSynced = true;
    }

    [PunRPC]
    void RequestFullWorldSync(int requesterActorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        Player targetPlayer = PhotonNetwork.CurrentRoom.GetPlayer(requesterActorNumber);
        if (targetPlayer == null) return;
        SendWorldSync(targetPlayer);
    }

    string SerializeEntry(LootEntry e)
    {
        return string.Join("|",
            e.itemName ?? "",
            e.dropChance.ToString(CultureInfo.InvariantCulture),
            e.isWeapon ? "1" : "0",
            e.isAmmo ? "1" : "0",
            e.isMelee ? "1" : "0",
            e.blockId.ToString(),
            e.minCount.ToString(),
            e.maxCount.ToString(),
            e.weaponId.ToString(),
            e.weaponName ?? "",
            e.weaponDamage.ToString(),
            e.weaponFireRate.ToString(CultureInfo.InvariantCulture),
            e.weaponRange.ToString(CultureInfo.InvariantCulture),
            e.weaponSpread.ToString(CultureInfo.InvariantCulture),
            e.weaponMaxAmmo.ToString(),
            e.weaponReloadTime.ToString(CultureInfo.InvariantCulture),
            e.weaponRecoilAmount.ToString(CultureInfo.InvariantCulture),
            e.weaponRecoilRecovery.ToString(CultureInfo.InvariantCulture),
            e.meleeId.ToString(),
            e.meleeName ?? "");
    }

    LootEntry DeserializeEntry(string s)
    {
        string[] p = s.Split('|');
        LootEntry e = new LootEntry();
        try
        {
            e.itemName = p[0];
            e.dropChance = float.Parse(p[1], CultureInfo.InvariantCulture);
            e.isWeapon = p[2] == "1";
            e.isAmmo = p[3] == "1";
            e.isMelee = p[4] == "1";
            e.blockId = int.Parse(p[5]);
            e.minCount = int.Parse(p[6]);
            e.maxCount = int.Parse(p[7]);
            e.weaponId = int.Parse(p[8]);
            e.weaponName = p[9];
            e.weaponDamage = int.Parse(p[10]);
            e.weaponFireRate = float.Parse(p[11], CultureInfo.InvariantCulture);
            e.weaponRange = float.Parse(p[12], CultureInfo.InvariantCulture);
            e.weaponSpread = float.Parse(p[13], CultureInfo.InvariantCulture);
            e.weaponMaxAmmo = int.Parse(p[14]);
            e.weaponReloadTime = float.Parse(p[15], CultureInfo.InvariantCulture);
            e.weaponRecoilAmount = float.Parse(p[16], CultureInfo.InvariantCulture);
            e.weaponRecoilRecovery = float.Parse(p[17], CultureInfo.InvariantCulture);
            e.meleeId = int.Parse(p[18]);
            e.meleeName = p[19];
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"⚠️ Ошибка десериализации лута: {ex.Message}");
        }
        return e;
    }

    void SendWorldSync(Player targetPlayer)
    {
        List<int> posX = new List<int>();
        List<int> posY = new List<int>();
        List<int> posZ = new List<int>();
        List<int> blockIds = new List<int>();
        List<bool> isLoots = new List<bool>();
        List<int> lootCounts = new List<int>();
        List<string> lootEntries = new List<string>();

        foreach (var kvp in activeBlocks)
        {
            if (kvp.Value == null) continue;
            Vector3 pos = kvp.Value.transform.position;
            int blockId = 1;
            PlacedBlock pb = kvp.Value.GetComponent<PlacedBlock>();
            if (pb != null) blockId = pb.blockId;

            bool isLoot = kvp.Value.GetComponent<BlockDestroyer>() != null;

            posX.Add(Mathf.RoundToInt(pos.x * POSITION_MULTIPLIER));
            posY.Add(Mathf.RoundToInt(pos.y * POSITION_MULTIPLIER));
            posZ.Add(Mathf.RoundToInt(pos.z * POSITION_MULTIPLIER));
            blockIds.Add(blockId);
            isLoots.Add(isLoot);

            if (isLoot)
            {
                BlockLootTable lt = kvp.Value.GetComponent<BlockLootTable>();
                if (lt != null && lt.lootTable != null && lt.lootTable.Count > 0)
                {
                    lootCounts.Add(lt.lootTable.Count);
                    foreach (var e in lt.lootTable)
                    {
                        lootEntries.Add(SerializeEntry(e));
                    }
                }
                else lootCounts.Add(0);
            }
            else lootCounts.Add(0);
        }

        Debug.Log($"📤 Отправляю {blockIds.Count} живых блоков игроку {targetPlayer.NickName}");

        photonView.RPC("RPC_FullWorldSync", targetPlayer,
            posX.ToArray(), posY.ToArray(), posZ.ToArray(),
            blockIds.ToArray(), isLoots.ToArray(),
            lootCounts.ToArray(), lootEntries.ToArray());
    }

    [PunRPC]
    void RPC_FullWorldSync(int[] posX, int[] posY, int[] posZ, int[] blockIds, bool[] isLoots,
        int[] lootCounts, string[] lootEntries)
    {
        Debug.Log($"🌍 Получена синхронизация: {blockIds.Length} блоков");

        BlockLootTable.IsSyncing = true;

        ClearAllBlocks();

        int lootIndex = 0;
        for (int i = 0; i < blockIds.Length; i++)
        {
            Vector3 position = new Vector3(
                posX[i] / (float)POSITION_MULTIPLIER,
                posY[i] / (float)POSITION_MULTIPLIER,
                posZ[i] / (float)POSITION_MULTIPLIER);

            string key = GetBlockKey(position.x, position.y, position.z);

            if (processedDestroyKeys.Contains(key))
            {
                lootIndex += lootCounts[i];
                continue;
            }

            CreateBlockFromSync(blockIds[i], position, isLoots[i], lootCounts[i], ref lootIndex, lootEntries);
        }

        isWorldSynced = true;
        Debug.Log($"✅ Мир синхронизирован. Активных: {activeBlocks.Count}");

        BlockLootTable.IsSyncing = false;
    }

    void ClearAllBlocks()
    {
        PlacedBlock[] allPlaced = FindObjectsOfType<PlacedBlock>();
        foreach (PlacedBlock pb in allPlaced)
            if (pb != null && pb.gameObject != null) Destroy(pb.gameObject);

        BlockDestroyer[] allLoot = FindObjectsOfType<BlockDestroyer>();
        foreach (BlockDestroyer bd in allLoot)
            if (bd != null && bd.gameObject != null) Destroy(bd.gameObject);

        activeBlocks.Clear();
    }

    void CreateBlockFromSync(int blockId, Vector3 position, bool isLoot, int lootCount, ref int lootIndex,
        string[] lootEntries)
    {
        if (blockId < 1 || blockId > blockPrefabs.Length)
        {
            lootIndex += lootCount;
            return;
        }

        if (HasBlockNear(position))
        {
            lootIndex += lootCount;
            return;
        }

        GameObject blockObj = Instantiate(blockPrefabs[blockId - 1], position, Quaternion.identity, worldParent);
        blockObj.name = "Block_" + position.x + "_" + position.y + "_" + position.z;

        string key = GetBlockKey(position.x, position.y, position.z);

        PlacedBlock pb = blockObj.GetComponent<PlacedBlock>();
        if (pb == null) pb = blockObj.AddComponent<PlacedBlock>();
        pb.blockId = blockId;

        if (isLoot)
        {
            BlockLootTable lootTable = blockObj.GetComponent<BlockLootTable>();
            if (lootTable == null) lootTable = blockObj.AddComponent<BlockLootTable>();

            if (lootTable.lootTable == null || lootTable.lootTable.Count == 0)
            {
                lootTable.lootTable = new List<LootEntry>();
                for (int j = 0; j < lootCount; j++)
                {
                    if (lootIndex < lootEntries.Length)
                    {
                        lootTable.lootTable.Add(DeserializeEntry(lootEntries[lootIndex]));
                    }
                    lootIndex++;
                }
            }
            else
            {
                lootIndex += lootCount;
            }

            BlockInfo bi = blockObj.GetComponent<BlockInfo>();
            if (bi == null) bi = blockObj.AddComponent<BlockInfo>();
            bi.blockId = blockId;

            if (blockObj.GetComponent<BlockDestroyer>() == null)
                blockObj.AddComponent<BlockDestroyer>();

            if (blockObj.GetComponent<LootChestRespawner>() == null)
            {
                LootChestRespawner resp = blockObj.AddComponent<LootChestRespawner>();
                resp.respawnTime = 60f;
                resp.enableRespawn = true;
            }
        }
        else
        {
            lootIndex += lootCount;
        }

        PhotonView pv = blockObj.GetComponent<PhotonView>();
        if (pv != null && !isLoot) Destroy(pv);

        if (!activeBlocks.ContainsKey(key)) activeBlocks.Add(key, blockObj);
    }

    public void HandleBlockDestroyed(Vector3 position)
    {
        string key = GetBlockKey(position.x, position.y, position.z);
        if (processedDestroyKeys.Contains(key))
        {
            Debug.Log($"⚠️ HandleBlockDestroyed: ключ {key} уже обработан, пропускаем");
            return;
        }
        processedDestroyKeys.Add(key);

        Debug.Log($"🔍 HandleBlockDestroyed: позиция={position}, ключ={key}");

        GameObject blockToRemove = null;
        int blockId = -1;
        float respawnTime = 0f;
        bool enableRespawn = false;

        if (activeBlocks.ContainsKey(key))
        {
            blockToRemove = activeBlocks[key];

            if (blockToRemove != null)
            {
                LootChestRespawner respawner = blockToRemove.GetComponent<LootChestRespawner>();
                BlockInfo bi = blockToRemove.GetComponent<BlockInfo>();

                if (respawner != null && respawner.enableRespawn && bi != null)
                {
                    blockId = bi.blockId;
                    respawnTime = respawner.respawnTime;
                    enableRespawn = true;
                    Debug.Log($"🎯 Сундук будет зареспавнен через {respawnTime}с (blockId={blockId})");
                }
            }

            activeBlocks.Remove(key);
            Debug.Log($"✅ Блок удалён из activeBlocks: {key}");
        }
        else
        {
            foreach (var kvp in activeBlocks)
            {
                if (kvp.Value == null) continue;
                if (Vector3.Distance(kvp.Value.transform.position, position) < 0.1f)
                {
                    blockToRemove = kvp.Value;

                    LootChestRespawner respawner = blockToRemove.GetComponent<LootChestRespawner>();
                    BlockInfo bi = blockToRemove.GetComponent<BlockInfo>();

                    if (respawner != null && respawner.enableRespawn && bi != null)
                    {
                        blockId = bi.blockId;
                        respawnTime = respawner.respawnTime;
                        enableRespawn = true;
                    }

                    activeBlocks.Remove(kvp.Key);
                    break;
                }
            }
        }

        if (enableRespawn && blockId > 0)
        {
            ScheduleBlockRespawn(blockId, position, respawnTime);
        }
    }

    public void HandleBlockPlaced(int blockId, Vector3 position)
    {
        string key = GetBlockKey(position.x, position.y, position.z);
        if (activeBlocks.ContainsKey(key)) return;

        if (HasBlockNear(position))
        {
            Debug.Log($"⚠️ ДЮП ЗАБЛОКИРОВАН: рядом уже есть блок, позиция {position}");
            return;
        }

        if (blockId < 1 || blockId > blockPrefabs.Length) return;

        processedDestroyKeys.Remove(key);

        GameObject blockObj = Instantiate(blockPrefabs[blockId - 1], position, Quaternion.identity, worldParent);
        blockObj.name = "Block_" + position.x + "_" + position.y + "_" + position.z;

        PlacedBlock pb = blockObj.AddComponent<PlacedBlock>();
        pb.blockId = blockId;

        PhotonView pv = blockObj.GetComponent<PhotonView>();
        if (pv != null) Destroy(pv);

        activeBlocks.Add(key, blockObj);
        Debug.Log($"✅ Блок установлен: {key}");
    }

    public void PlaceBlock(int blockId, Vector3 position)
    {
        string key = GetBlockKey(position.x, position.y, position.z);
        if (activeBlocks.ContainsKey(key) || HasBlockNear(position))
        {
            Debug.Log($"⚠️ Блок уже существует рядом с {position}");
            return;
        }
        HandleBlockPlaced(blockId, position);
    }

    public void DestroyBlock(Vector3 position, string playerNickname = "")
    {
        HandleBlockDestroyed(position);
    }

    string GetBlockKey(float x, float y, float z)
    {
        return x.ToString("F2", CultureInfo.InvariantCulture) + "|" +
               y.ToString("F2", CultureInfo.InvariantCulture) + "|" +
               z.ToString("F2", CultureInfo.InvariantCulture);
    }

    void OnGUI()
    {
        GUILayout.Label("Активных блоков: " + activeBlocks.Count);
        GUILayout.Label("Мир синхронизирован: " + isWorldSynced);
    }
}