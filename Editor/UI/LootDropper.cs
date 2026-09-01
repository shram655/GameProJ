using UnityEngine;
using Photon.Pun;

public static class LootDropper
{
    // Выкидывание из инвентаря (перетаскивание за рамку)
    public static void DropFromInventory(PlayerInventory inv, int slot)
    {
        if (inv == null) return;

        var pwm = inv.GetComponent<PlayerWeaponManager>();
        if (pwm != null) pwm.ReleaseEquippedIfSlotInvolved(slot, slot);

        int id = inv.inventory[slot];
        int count = inv.inventoryCounts[slot];
        if (id == 0) return;

        inv.inventory[slot] = 0;
        inv.inventoryCounts[slot] = 0;
        inv.UpdateHotbarUI();
        if (inv.inventoryUI != null) inv.inventoryUI.UpdateAllSlots();

        Spawn(id, count, inv.transform);
    }

    // Спавн перед игроком
    public static void Spawn(int itemId, int count, Transform near)
    {
        Vector3 pos = near.position + near.forward * 1.2f + Vector3.up * 0.3f;
        SpawnAt(itemId, count, pos);
    }

    // 🆕 Спавн в ТОЧКУ (для лута при смерти)
    public static void SpawnAt(int itemId, int count, Vector3 worldPos)
    {
        GameObject go = null;
        if (PhotonNetwork.IsConnected)
            go = PhotonNetwork.Instantiate("DroppedLoot", worldPos, Quaternion.identity);

        if (go == null)
        {
            go = new GameObject("DroppedLoot");
            go.transform.position = worldPos;
            go.AddComponent<PhotonView>();
            go.AddComponent<DroppedLoot>();
        }

        var dl = go.GetComponent<DroppedLoot>();
        if (dl != null)
        {
            if (dl.photonView != null && PhotonNetwork.IsConnected)
                dl.photonView.RPC("SetData", RpcTarget.AllBuffered, itemId, count);
            else
                dl.ApplyData(itemId, count);
        }
    }
}