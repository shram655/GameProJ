using UnityEngine;

[System.Serializable]
public class LootEntry
{
    public string itemName;
    public float dropChance;
    public bool isWeapon;
    public bool isAmmo;
    public bool isMelee;
    public int blockId;
    public int minCount = 1;
    public int maxCount = 1;

    // Оружие
    public int weaponId = 1;
    public string weaponName;
    public int weaponDamage;
    public float weaponFireRate;
    public float weaponRange;
    public float weaponSpread;
    public int weaponMaxAmmo;
    public float weaponReloadTime;
    public float weaponRecoilAmount;
    public float weaponRecoilRecovery;
    public GameObject weaponPrefab;

    // 🆕 Тип патронов для лута (0 = 7.62, -3 = 5.45)
    public int ammoItemId = 0;

    // Холодное
    public int meleeId = 1;
    public string meleeName = "Топор";
}