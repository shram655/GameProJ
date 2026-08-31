using UnityEngine;
using Photon.Pun;
using System.Collections;

public class Gun : MonoBehaviourPun
{
    [Header("Основные")]
    public string weaponName = "Оружие";
    public float damage = 25f;
    public float fireRate = 0.1f;
    public float range = 100f;
    public float spread = 0.02f;

    [Header("Патроны")]
    public int maxAmmo = 30;
    public int currentAmmo;
    public float reloadTime = 2f;
    private bool isReloading = false;
    public int weaponId = -1;
    public int slotIndex = -1;

    [Header("Отдача")]
    public float recoilAmount = 0.5f;
    public float recoilRecovery = 5f;

    [Header("Эффекты")]
    public ParticleSystem muzzleFlash;
    public GameObject impactEffect;
    public AudioSource gunAudio;
    public AudioClip shootSound;
    public AudioClip reloadSound;
    public AudioClip emptySound;

    [Header("Ссылки")]
    public Camera fpsCam;
    public Transform barrelEnd;
    public PlayerInventory playerInventory;

    [Header("Viewmodel")]
    public bool attachToCamera = true;
    public Vector3 viewmodelPosition = new Vector3(0.25f, -0.25f, 0.5f);
    public Vector3 viewmodelRotation = Vector3.zero;

    [Header("ADS")]
    public bool useADS = true;
    public Transform adsTarget;
    [Range(0.2f, 3f)] public float adsDistance = 0.6f;
    public Vector3 adsRotation = Vector3.zero;
    public Vector3 adsPosition = new Vector3(0f, -0.22f, 0.45f);
    [Range(20f, 90f)] public float adsFov = 40f;
    [Range(0.05f, 1f)] public float adsSpreadMultiplier = 0.3f;
    [Range(0.1f, 1f)] public float adsSensMultiplier = 0.6f;
    [Range(2f, 40f)] public float adsSmooth = 30f;
    [Range(5f, 60f)] public float adsSnapSpeed = 35f;

    [Header("Доставание")]
    public bool useDrawAnimation = true;
    public Vector3 drawStartPosition = new Vector3(0.25f, -0.6f, 0.3f);
    public Vector3 drawStartRotation = new Vector3(30f, 0f, 0f);
    [Range(2f, 20f)] public float drawSpeed = 10f;

    [Header("Бег")]
    public bool useSprintCarry = true;
    [Range(-90f, 90f)] public float sprintBarrelAngle = 35f;
    public Vector3 sprintCarryPosition = new Vector3(0.22f, -0.3f, 0.45f);
    [Range(2f, 20f)] public float sprintCarrySmooth = 8f;
    [Range(10f, 60f)] public float fireSnapSpeed = 30f;

    [Header("Дыхание")]
    public bool useBreathing = true;
    [Range(0.5f, 6f)] public float breathSpeed = 2.2f;
    [Range(0f, 0.05f)] public float breathAmplitude = 0.008f;
    [Range(0f, 0.05f)] public float breathSwayX = 0.004f;
    [Range(0f, 3f)] public float breathTiltAngle = 0.6f;
    [Range(2f, 20f)] public float breathIntensitySmooth = 8f;

    [Header("FOV")]
    public float sprintLerpSpeed = 12f;
    public float snapSpeed = 30f;
    public bool useFovKick = true;
    public float sprintFovAdd = 8f;

    private float nextTimeToFire = 0f;
    private float currentRecoil = 0f;
    private bool isEquipped = false;
    private bool ammoInitialized = false;
    private float normalFov = 60f;
    private bool fovCaptured = false;
    private float adsProgress = 0f;
    private bool lastAimHeld = false;
    private Vector3 currentPosition;
    private Quaternion currentRotation = Quaternion.identity;
    private bool viewmodelInitialized = false;
    private bool isDrawing = false;
    private float breathTime = 0f;
    private float breathIntensity = 1f;
    private mouseLook cachedMouseLook;
    private float baseSens = 100f;
    private CrosshairController cachedCrosshair;
    private CubeWorldCharacter ownerCharacter;

    bool IsLocal() => photonView == null || photonView.IsMine;

    // 🆕 Какие патроны нужны ЭТОМУ оружию
    int GetRequiredAmmoId()
    {
        return playerInventory != null ? playerInventory.GetAmmoIdForWeapon(weaponId) : -2;
    }

    void Awake()
    {
        if (IsLocal()) gameObject.SetActive(false);
        if (adsTarget == null) adsTarget = transform.Find("ADSTarget");
    }

    [ContextMenu("🎯 Создать ADSTarget")]
    public void CreateAdsTarget()
    {
        if (adsTarget == null) adsTarget = transform.Find("ADSTarget");
        if (adsTarget == null)
        {
            GameObject go = new GameObject("ADSTarget");
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(0f, 0.1f, 0.5f);
            go.transform.localRotation = Quaternion.identity;
            adsTarget = go.transform;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (adsTarget == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(adsTarget.position, 0.02f);
        Gizmos.DrawLine(adsTarget.position, adsTarget.position + transform.forward * 0.5f);
    }

    void Start()
    {
        if (gunAudio == null) { gunAudio = GetComponent<AudioSource>(); if (gunAudio == null) gunAudio = gameObject.AddComponent<AudioSource>(); }
        isEquipped = false;
        if (IsLocal())
        {
            ownerCharacter = GetComponentInParent<CubeWorldCharacter>();
            if (ownerCharacter == null) { PlayerController pc = GetComponentInParent<PlayerController>(); if (pc != null) ownerCharacter = pc.GetComponent<CubeWorldCharacter>(); }
            StartCoroutine(SelfEquipFallback());
        }
        else StartCoroutine(AttachToOwner());
    }

    IEnumerator SelfEquipFallback()
    {
        float w = 0f;
        while (!isEquipped && w < 2f) { if (fpsCam != null) { yield return null; if (!isEquipped) Equip(); yield break; } w += Time.deltaTime; yield return null; }
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
                        transform.SetParent(cw.WeaponAnchor);
                        transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity; transform.localScale = Vector3.one;
                    }
                    yield break;
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    void OnDestroy() { RestoreAimSideEffects(); ownerCharacter = null; }

    void CacheReferences()
    {
        if (fpsCam != null && cachedMouseLook == null) { cachedMouseLook = fpsCam.GetComponent<mouseLook>(); if (cachedMouseLook != null) baseSens = cachedMouseLook.mouseSensitiviti; }
        if (cachedCrosshair == null) cachedCrosshair = FindObjectOfType<CrosshairController>();
    }

    void RestoreAimSideEffects()
    {
        if (cachedMouseLook != null) cachedMouseLook.mouseSensitiviti = baseSens;
        if (cachedCrosshair != null) cachedCrosshair.SetCrosshairActive(true);
        adsProgress = 0f; lastAimHeld = false;
    }

    bool IsSelf(Transform t) { if (ownerCharacter == null) return false; return t == ownerCharacter.transform || t.IsChildOf(ownerCharacter.transform); }

    void Update()
    {
        if (photonView != null && !photonView.IsMine) return;
        if (!isEquipped || fpsCam == null) return;
        if (playerInventory != null && playerInventory.IsInventoryOpen) return;
        if (fireRate <= 0f) fireRate = 0.1f;

        currentRecoil = Mathf.Lerp(currentRecoil, 0f, Time.deltaTime * recoilRecovery);
        fpsCam.transform.localRotation *= Quaternion.Euler(-currentRecoil, 0, 0);

        if (Input.GetMouseButton(0) && !isReloading) Shoot();
        // 🆕 Перезарядка берёт ТОЛЬКО свои патроны
        if (Input.GetKeyDown(KeyCode.R) && !isReloading && currentAmmo < maxAmmo
            && playerInventory != null && playerInventory.GetTotalAmmoOfType(GetRequiredAmmoId()) > 0)
            StartCoroutine(Reload());
    }

    void LateUpdate()
    {
        if (photonView != null && !photonView.IsMine) return;
        if (!isEquipped || fpsCam == null) return;

        bool inventoryOpen = playerInventory != null && playerInventory.IsInventoryOpen;
        bool firing = Input.GetMouseButton(0) && !inventoryOpen;
        bool aimHeld = useADS && Input.GetMouseButton(1) && !inventoryOpen && !isReloading;
        bool sprinting = IsSprinting() && !firing && !aimHeld;

        float adsTargetProg = aimHeld ? 1f : 0f;
        adsProgress = Mathf.Lerp(adsProgress, adsTargetProg, Time.deltaTime * adsSmooth);
        if (Mathf.Abs(adsProgress - adsTargetProg) < 0.01f) adsProgress = adsTargetProg;

        if (cachedMouseLook != null) cachedMouseLook.mouseSensitiviti = Mathf.Lerp(baseSens, baseSens * adsSensMultiplier, adsProgress);

        if (aimHeld != lastAimHeld) { lastAimHeld = aimHeld; if (cachedCrosshair != null) cachedCrosshair.SetCrosshairActive(!aimHeld); }

        if (useFovKick || useADS)
        {
            if (!fovCaptured) { normalFov = fpsCam.fieldOfView; fovCaptured = true; }
            float baseFov = normalFov + (sprinting ? sprintFovAdd : 0f);
            float targetFov = Mathf.Lerp(baseFov, adsFov, useADS ? adsProgress : 0f);
            float t = Time.deltaTime * (firing ? snapSpeed : sprintLerpSpeed);
            fpsCam.fieldOfView = Mathf.Lerp(fpsCam.fieldOfView, targetFov, t);
        }

        if (attachToCamera)
        {
            if (!viewmodelInitialized) { currentPosition = viewmodelPosition; currentRotation = Quaternion.Euler(viewmodelRotation); viewmodelInitialized = true; }
            Vector3 targetPos; Quaternion targetRot;
            if (aimHeld)
            {
                if (adsTarget != null) { Vector3 a = transform.InverseTransformPoint(adsTarget.position); targetRot = Quaternion.Euler(adsRotation); targetPos = new Vector3(0f, 0f, adsDistance) - (targetRot * a); }
                else { targetPos = adsPosition; targetRot = Quaternion.Euler(adsRotation); }
            }
            else if (sprinting && useSprintCarry) { targetPos = sprintCarryPosition; targetRot = Quaternion.Euler(0f, -sprintBarrelAngle, 0f); }
            else { targetPos = viewmodelPosition; targetRot = Quaternion.Euler(viewmodelRotation); }

            float speed;
            if (firing) speed = fireSnapSpeed;
            else if (aimHeld || adsProgress > 0.01f) speed = adsSnapSpeed;
            else if (isDrawing) speed = drawSpeed;
            else speed = sprintCarrySmooth;

            float t = Time.deltaTime * speed;
            currentPosition = Vector3.Lerp(currentPosition, targetPos, t);
            currentRotation = Quaternion.Slerp(currentRotation, targetRot, t);
            if (isDrawing && !firing && Vector3.Distance(currentPosition, targetPos) < 0.01f) isDrawing = false;

            Vector3 breathOffset = Vector3.zero; Quaternion breathRot = Quaternion.identity;
            if (useBreathing)
            {
                breathTime += Time.deltaTime;
                float targetIntensity = firing ? 0.25f : (aimHeld ? 0.15f : (sprinting ? 0.5f : 1f));
                breathIntensity = Mathf.Lerp(breathIntensity, targetIntensity, Time.deltaTime * breathIntensitySmooth);
                float upDown = Mathf.Sin(breathTime * breathSpeed) * breathAmplitude;
                float sideWay = Mathf.Sin(breathTime * breathSpeed * 0.5f) * breathSwayX;
                breathOffset = new Vector3(sideWay, upDown, 0f) * breathIntensity;
                breathRot = Quaternion.Euler(Mathf.Sin(breathTime * breathSpeed) * breathTiltAngle * 0.5f * breathIntensity, 0f, Mathf.Sin(breathTime * breathSpeed * 0.5f) * breathTiltAngle * breathIntensity);
            }
            transform.localPosition = currentPosition + breathOffset;
            transform.localRotation = currentRotation * breathRot;
        }
    }

    bool IsSprinting()
    {
        if (isReloading) return false;
        if (playerInventory != null && playerInventory.IsInventoryOpen) return false;
        float m = Mathf.Abs(Input.GetAxis("Horizontal")) + Mathf.Abs(Input.GetAxis("Vertical"));
        return Input.GetKey(KeyCode.LeftShift) && m > 0.1f;
    }

    public void Equip()
    {
        if (photonView != null && !photonView.IsMine) return;
        bool first = !isEquipped;
        isEquipped = true; gameObject.SetActive(true);
        if (first && !ammoInitialized) { ammoInitialized = true; LoadFromInventory(); }
        if (ownerCharacter == null) { ownerCharacter = GetComponentInParent<CubeWorldCharacter>(); if (ownerCharacter == null) { PlayerController pc = GetComponentInParent<PlayerController>(); if (pc != null) ownerCharacter = pc.GetComponent<CubeWorldCharacter>(); } }
        if (ownerCharacter != null) ownerCharacter.SetHasWeapon(true);
        CacheReferences();
        if (first)
        {
            if (useDrawAnimation) { currentPosition = drawStartPosition; currentRotation = Quaternion.Euler(viewmodelRotation + drawStartRotation); isDrawing = true; }
            else { currentPosition = viewmodelPosition; currentRotation = Quaternion.Euler(viewmodelRotation); isDrawing = false; }
            viewmodelInitialized = true;
        }
        if (attachToCamera && fpsCam != null)
        {
            if (transform.parent != fpsCam.transform) { transform.SetParent(fpsCam.transform); transform.localPosition = currentPosition; transform.localRotation = currentRotation; transform.localScale = Vector3.one; }
            PhotonTransformView ptv = GetComponent<PhotonTransformView>(); if (ptv != null) ptv.enabled = false;
        }
    }

    public void Unequip()
    {
        if (photonView != null && !photonView.IsMine) return;
        isEquipped = false; isReloading = false; isDrawing = false; gameObject.SetActive(false);
        RestoreAimSideEffects(); SaveToInventory();
        if (fpsCam != null && useFovKick) fpsCam.fieldOfView = normalFov;
    }

    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }

    void LoadFromInventory()
    {
        if (playerInventory == null) return;
        if (slotIndex < 0 || slotIndex >= playerInventory.inventory.Length) return;
        currentAmmo = playerInventory.inventoryCounts[slotIndex];
        playerInventory.inventoryCounts[slotIndex] = 0;
        playerInventory.UpdateHotbarUI();
    }

    void SaveToInventory()
    {
        if (playerInventory == null) return;
        if (slotIndex < 0 || slotIndex >= playerInventory.inventory.Length) return;
        playerInventory.inventoryCounts[slotIndex] = currentAmmo;
        currentAmmo = 0;
        playerInventory.UpdateHotbarUI();
    }

    public void SaveAmmoToInventory()
    {
        if (playerInventory == null) return;
        if (slotIndex < 0 || slotIndex >= playerInventory.inventory.Length) return;
        playerInventory.inventoryCounts[slotIndex] = currentAmmo;
        playerInventory.UpdateHotbarUI();
    }

    public void LoadAmmoFromInventory()
    {
        if (playerInventory == null) return;
        if (slotIndex < 0 || slotIndex >= playerInventory.inventory.Length) return;
        currentAmmo = playerInventory.inventoryCounts[slotIndex];
        playerInventory.inventoryCounts[slotIndex] = 0;
        playerInventory.UpdateHotbarUI();
    }

    void Shoot()
    {
        if (Time.time < nextTimeToFire) return;
        nextTimeToFire = Time.time + fireRate;
        if (currentAmmo <= 0) { if (gunAudio != null && emptySound != null) gunAudio.PlayOneShot(emptySound); return; }
        currentAmmo--;
        float curSpread = Mathf.Lerp(spread, spread * adsSpreadMultiplier, adsProgress);
        Vector3 dir = fpsCam.transform.forward;
        dir.x += Random.Range(-curSpread, curSpread); dir.y += Random.Range(-curSpread, curSpread); dir.z += Random.Range(-curSpread, curSpread);
        dir.Normalize();
        RaycastHit[] hits = Physics.RaycastAll(fpsCam.transform.position, dir, range);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (IsSelf(hit.transform)) continue;
            PlayerHealth target = hit.transform.GetComponent<PlayerHealth>();
            if (target == null) target = hit.transform.GetComponentInParent<PlayerHealth>();
            if (target != null) { if (photonView == null || photonView.IsMine) { if (target.photonView != null && target.photonView.IsMine) continue; } target.photonView.RPC("RPC_TakeDamage", target.photonView.Owner, damage); }
            if (impactEffect != null) { GameObject g = Instantiate(impactEffect, hit.point, Quaternion.LookRotation(hit.normal)); Destroy(g, 2f); }
            break;
        }
        if (muzzleFlash != null) muzzleFlash.Play();
        if (gunAudio != null && shootSound != null) gunAudio.PlayOneShot(shootSound);
        currentRecoil += recoilAmount;
    }

    IEnumerator Reload()
    {
        if (isReloading) yield break;
        isReloading = true;
        if (gunAudio != null && reloadSound != null) gunAudio.PlayOneShot(reloadSound);
        yield return new WaitForSeconds(reloadTime);
        if (playerInventory != null)
        {
            // 🆕 Берём ТОЛЬКО свой тип патронов
            int ammoId = GetRequiredAmmoId();
            int needed = maxAmmo - currentAmmo;
            int take = Mathf.Min(needed, playerInventory.GetTotalAmmoOfType(ammoId));
            if (take > 0) { playerInventory.ConsumeAmmoOfType(ammoId, take); currentAmmo += take; }
        }
        isReloading = false;
    }

    void OnGUI()
    {
        if (photonView != null && !photonView.IsMine) return;
        if (!isEquipped) return;
        GUIStyle s = new GUIStyle(); s.fontSize = 16; s.normal.textColor = Color.white;
        int reserve = playerInventory != null ? playerInventory.GetTotalAmmoOfType(GetRequiredAmmoId()) : 0;
        GUI.Label(new Rect(10, 10, 400, 25), "Оружие: " + weaponName, s);
        GUI.Label(new Rect(10, 35, 400, 25), $"Магазин: {currentAmmo}/{maxAmmo}  (запас: {reserve})", s);
        if (isReloading) GUI.Label(new Rect(10, 60, 400, 25), "ПЕРЕЗАРЯДКА...", s);
    }
}