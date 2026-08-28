using UnityEngine;
using Photon.Pun;

public static class DeathLootDropper
{
    // 🆕 Выбросить ВЕСЬ инвентарь вокруг точки смерти
    public static void DropAll(PlayerController pc)
    {
        if (pc == null) return;
        var inv = pc.inventory;
        var wm = pc.weaponManager;
        if (inv == null) return;

        // Сначала снять оружие, чтобы патроны вернулись в слот
        if (wm != null) wm.UnequipCurrentWeapon();

        Vector3 center = pc.transform.position;
        int index = 0;

        for (int i = 0; i < 20; i++)
        {
            int id = inv.inventory[i];
            int count = inv.inventoryCounts[i];
            if (id == 0) continue;

            // 🆕 Каждый предмет — ОТДЕЛЬНЫЙ кубик рядом, не в одной точке
            Vector3 pos = ScatterPosition(center, index);
            LootDropper.SpawnAt(id, count, pos);

            inv.inventory[i] = 0;
            inv.inventoryCounts[i] = 0;
            index++;
        }

        inv.UpdateHotbarUI();
        if (inv.inventoryUI != null) inv.inventoryUI.UpdateAllSlots();

        if (index > 0)
            Debug.Log($"💀 Лут выпал: {index} предметов вокруг {center}");
    }

    // 🆕 Раскидывает по спирали (золотой угол), чтобы кубики лежали рядом, но не стыковались
    static Vector3 ScatterPosition(Vector3 center, int index)
    {
        float angle = index * 137.5f * Mathf.Deg2Rad;
        float radius = 0.5f + 0.3f * Mathf.Sqrt(index);
        Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        return center + dir * radius;
    }
}