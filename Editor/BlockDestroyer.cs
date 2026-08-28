using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

public class BlockDestroyer : MonoBehaviourPun
{
    private BlockLootTable lootTable;
    private BlockInfo blockInfo;
    private WorldManager worldManager;
    private bool isDestroyed = false;

    // СТАТИЧЕСКАЯ ЗАЩИТА: одна позиция — один лут в течение 2 секунд
    private static Dictionary<string, float> recentDestroys = new Dictionary<string, float>();

    void Start()
    {
        lootTable = GetComponent<BlockLootTable>();
        blockInfo = GetComponent<BlockInfo>();
        worldManager = FindObjectOfType<WorldManager>();

        Debug.Log($"✅ BlockDestroyer Start на {gameObject.name}");
        Debug.Log($"  lootTable: {(lootTable != null ? "OK" : "NULL")}");
        if (lootTable != null) Debug.Log($"  lootTable.lootTable.Count: {lootTable.lootTable.Count}");
    }

    public void RequestDestroy(PlayerInventory playerInventory)
    {
        if (playerInventory == null)
        {
            Debug.LogError("❌ BD: playerInventory = null!");
            return;
        }

        PlayerController pc = playerInventory.GetComponent<PlayerController>();
        if (pc == null)
        {
            Debug.LogError("❌ BD: PlayerController не найден!");
            return;
        }

        if (pc.view == null || !pc.view.IsMine)
        {
            Debug.LogWarning("⚠️ BD: вызван НЕ локальным игроком — выход!");
            return;
        }

        if (isDestroyed)
        {
            Debug.LogWarning("⚠️ BD: блок уже разрушен!");
            return;
        }

        // АНТИ-ДУБЛЬ по позиции
        string posKey = transform.position.ToString("F2");
        float lastTime;
        if (recentDestroys.TryGetValue(posKey, out lastTime) && Time.time - lastTime < 2f)
        {
            Debug.LogWarning("⚠️ BD: анти-дубль — эта позиция уже обработана!");
            return;
        }
        recentDestroys[posKey] = Time.time;

        isDestroyed = true;

        Debug.Log($"🔨 BD START: {pc.nick.text} ломает {gameObject.name}");

        // ═══ ШАГ 1: ЛУТ ТОЛЬКО ЛОКАЛЬНО ═══
        try
        {
            if (lootTable != null && lootTable.lootTable != null && lootTable.lootTable.Count > 0)
            {
                lootTable.GenerateAndAddLoot(pc);
            }
            else if (blockInfo != null)
            {
                playerInventory.AddToInventory(blockInfo.blockId);
                Debug.Log($"🎒 ЛУТ ВЫДАН: Блок #{blockInfo.blockId} x1 (игрок {pc.nick.text})");
            }
            else
            {
                Debug.LogError("❌ BD: lootTable пуст!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ BD: ошибка лута: {e.Message}");
        }

        // ═══ ШАГ 2: WorldManager — респавн + RPC другим ═══
        Vector3 pos = transform.position;
        if (worldManager == null) worldManager = FindObjectOfType<WorldManager>();
        if (worldManager != null)
        {
            worldManager.NotifyLootBlockDestroyed(pos);
        }

        // ═══ ШАГ 3: уничтожить локально ═══
        Destroy(gameObject);
        Debug.Log("🔨 BD END");
    }
}