using UnityEngine;
using Photon.Pun;

public class PickupWeapon : MonoBehaviourPun
{
    [Header("Настройки оружия")]
    public string weaponName = "Оружие";
    public int weaponId = 1;          // 🆕 ID типа оружия
    public int damage = 15;
    public float fireRate = 0.08f;
    public float range = 100f;
    public float spread = 0.015f;
    public int maxAmmo = 30;
    public float reloadTime = 2f;
    public float recoilAmount = 0.8f;
    public float recoilRecovery = 5f;

    public int currentMagazine = 30;  // 🆕 патронов в магазине на земле

    [Header("Эффекты")]
    public ParticleSystem muzzleFlash;
    public GameObject impactEffect;
    public AudioClip shootSound;
    public AudioClip reloadSound;
    public AudioClip emptySound;

    [Header("Визуал")]
    public GameObject weaponModel;

    private bool isPickedUp = false;

    void Start()
    {
        if (photonView != null && !photonView.IsMine) { }
    }

    public void PickUp(GameObject player)
    {
        if (isPickedUp) return;
        isPickedUp = true;

        PlayerWeaponManager weaponManager = player.GetComponent<PlayerWeaponManager>();
        if (weaponManager != null)
        {
            WeaponData data = new WeaponData
            {
                weaponId = weaponId,
                weaponName = weaponName,
                damage = damage,
                fireRate = fireRate,
                range = range,
                spread = spread,
                maxAmmo = maxAmmo,
                reloadTime = reloadTime,
                recoilAmount = recoilAmount,
                recoilRecovery = recoilRecovery,
                muzzleFlash = muzzleFlash,
                impactEffect = impactEffect,
                shootSound = shootSound,
                reloadSound = reloadSound,
                emptySound = emptySound
            };

            // Регистрируем тип оружия
            weaponManager.RegisterWeaponData(data);

            // 🆕 Добавляем в инвентарь как новое оружие
            PlayerInventory inv = player.GetComponent<PlayerInventory>();
            if (inv != null)
            {
                int freeSlot = -1;
                for (int i = 0; i < 15; i++) { if (inv.inventory[i] == 0) { freeSlot = i; break; } }
                if (freeSlot == -1) { for (int i = 15; i < 20; i++) { if (inv.inventory[i] == 0) { freeSlot = i; break; } } }

                if (freeSlot >= 0)
                {
                    int invId = -(100 + weaponId);
                    inv.inventory[freeSlot] = invId;
                    inv.inventoryCounts[freeSlot] = currentMagazine;
                    inv.UpdateHotbarUI();
                    if (inv.inventoryUI != null) inv.inventoryUI.UpdateAllSlots();
                }
                else
                {
                    Debug.LogWarning("⚠️ Нет места для оружия!");
                }
            }
        }

        if (photonView != null && photonView.IsMine) PhotonNetwork.Destroy(gameObject);
        else Destroy(gameObject);
    }

    public static GameObject DropWeapon(
        Vector3 position, Quaternion rotation,
        string weaponName, int damage, float fireRate, float range, float spread,
        int maxAmmo, float reloadTime, float recoilAmount, float recoilRecovery,
        ParticleSystem muzzleFlash, GameObject impactEffect,
        AudioClip shootSound, AudioClip reloadSound, AudioClip emptySound,
        GameObject weaponModelPrefab,
        int weaponId = 1, int magAmmo = 30)
    {
        GameObject droppedWeapon = new GameObject("PickupWeapon_" + weaponName);
        droppedWeapon.transform.position = position;
        droppedWeapon.transform.rotation = rotation;

        Rigidbody rb = droppedWeapon.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.mass = 1f;
        rb.drag = 0.5f;
        rb.angularDrag = 0.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.isKinematic = true;

        BoxCollider collider = droppedWeapon.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(1.5f, 1f, 1.5f);

        PhotonView photonView = droppedWeapon.AddComponent<PhotonView>();

        PickupWeapon pickup = droppedWeapon.AddComponent<PickupWeapon>();
        WeaponDropper dropper = droppedWeapon.AddComponent<WeaponDropper>();

        pickup.weaponId = weaponId;
        pickup.weaponName = weaponName;
        pickup.damage = damage;
        pickup.fireRate = fireRate;
        pickup.range = range;
        pickup.spread = spread;
        pickup.maxAmmo = maxAmmo;
        pickup.reloadTime = reloadTime;
        pickup.recoilAmount = recoilAmount;
        pickup.recoilRecovery = recoilRecovery;
        pickup.muzzleFlash = muzzleFlash;
        pickup.impactEffect = impactEffect;
        pickup.shootSound = shootSound;
        pickup.reloadSound = reloadSound;
        pickup.emptySound = emptySound;
        pickup.currentMagazine = magAmmo;

        GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        model.transform.SetParent(droppedWeapon.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = new Vector3(0.5f, 0.5f, 1.5f);
        Destroy(model.GetComponent<Collider>());
        pickup.weaponModel = model;

        dropper.StartDrop(rb);

        Debug.Log($"🗑️ Оружие #{weaponId} ({weaponName}) выброшено, магазин: {magAmmo}");
        return droppedWeapon;
    }
}