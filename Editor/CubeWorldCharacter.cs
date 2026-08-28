using UnityEngine;
using Photon.Pun;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class CubeWorldCharacter : MonoBehaviourPun
{
    [Header("ЦВЕТА")]
    public Color headColor = new Color(1.0f, 0.9f, 0.7f);
    public Color hairColor = new Color(0.9f, 0.7f, 0.3f);
    public Color bodyColor = new Color(0.3f, 0.6f, 0.9f);
    public Color legColor = new Color(0.8f, 0.3f, 0.2f);
    public Color bootColor = new Color(0.4f, 0.3f, 0.2f);

    [Header("РАЗМЕРЫ")]
    public float headScale = 0.6f;
    public float bodyScale = 0.35f;
    public float armScale = 0.13f;
    public float legScale = 0.15f;
    public float armLength = 0.30f;
    public float legLength = 0.40f;

    [Header("ПОЛОЖЕНИЕ РУК")]
    [Range(0f, 90f)] public float armRaiseForward = 90f;
    [Range(0f, 120f)] public float armElbowStraight = 0f;
    [Range(-30f, 30f)] public float armCenterRoll = -5f;
    [Range(0f, 90f)] public float leftArmRaise = 0f;

    [Header("ПРАВАЯ РУКА")]
    public Vector3 offsetShoulderR = Vector3.zero;
    public Vector3 offsetUpperArmR = Vector3.zero;
    public Vector3 offsetForearmR = Vector3.zero;
    public Vector3 offsetHandR = Vector3.zero;

    [Header("ЛЕВАЯ РУКА")]
    public Vector3 offsetShoulderL = Vector3.zero;
    public Vector3 offsetUpperArmL = Vector3.zero;
    public Vector3 offsetForearmL = Vector3.zero;
    public Vector3 offsetHandL = Vector3.zero;

    [Header("ПРИЦЕЛИВАНИЕ")]
    public bool weaponFollowsCamera = true;
    public float aimSmoothSpeed = 12f;
    [Range(10f, 80f)] public float aimPitchLimit = 60f;

    [Header("ПЛАВНОСТЬ")]
    [Range(2f, 30f)] public float poseSmoothSpeed = 12f;

    [Header("ХВАТ")]
    [Range(0.5f, 1f)] public float gripHandShrink = 0.8f;

    [Header("WeaponAnchor")]
    public Vector3 weaponLocalPosition = new Vector3(0f, 0f, 0.05f);
    public Vector3 weaponLocalRotation = Vector3.zero;
    public Vector3 weaponLocalScale = Vector3.one;

    [Header("ПРЕВЬЮ В РЕДАКТОРЕ")]
    [Tooltip("ВКЛ = руки в редакторе стоят как в игре с оружием")]
    public bool previewWeaponPose = true;

    [Header("ВИД ОТ 1-ГО ЛИЦА")]
    public bool hideLocalArms = true;

    [Header("ХОДЬБА")]
    public float walkSpeed = 8f;
    public float walkLegSwing = 45f;
    public float walkArmSwing = 35f;

    [Header("СЦЕНА")]
    public bool showGizmoArrow = true;
    public Color gizmoColor = Color.red;

    public Transform WeaponAnchor { get; private set; }
    private bool hasWeaponFlag = false;
    public void SetHasWeapon(bool v) { hasWeaponFlag = v; if (!v) currentPitch = 0f; }
    bool HasWeapon { get { if (hasWeaponFlag) return true; if (WeaponAnchor != null) foreach (Transform c in WeaponAnchor) if (c.gameObject.activeSelf) return true; return false; } }

    private Transform root, hips, spine, chest, neck, head;
    private Transform shoulderR, upperArmR, forearmR, handR;
    private Transform shoulderL, upperArmL, forearmL, handL;
    private Transform thighR, shinR, footR;
    private Transform thighL, shinL, footL;
    private Vector3 baseShoulderR, baseUpperArmR, baseForearmR, baseHandR;
    private Vector3 baseShoulderL, baseUpperArmL, baseForearmL, baseHandL;
    private bool basePositionsSaved = false;
    private Transform handRVis;
    private Vector3 handRBaseScale = Vector3.one;
    private Camera cachedCam;
    private float currentPitch = 0f;
    private float smoothT = 1f;
    private static Shader cachedShader;
    private Vector3 lastPos;
    private float walkTime = 0f;

    void ApplyRot(Transform b, Quaternion t) { if (b == null) return; if (Application.isPlaying) b.localRotation = Quaternion.Slerp(b.localRotation, t, smoothT); else b.localRotation = t; }
    void ApplyPos(Transform b, Vector3 t) { if (b == null) return; if (Application.isPlaying) b.localPosition = Vector3.Lerp(b.localPosition, t, smoothT); else b.localPosition = t; }

    static Shader GetCharShader()
    {
        if (cachedShader != null) return cachedShader;
        foreach (string n in new[] { "Unlit/Color", "Universal Render Pipeline/Unlit", "Custom/VoxelCharacter", "HDRP/Unlit", "Diffuse", "Standard" }) { Shader s = Shader.Find(n); if (s != null) { cachedShader = s; return s; } }
        cachedShader = Shader.Find("Sprites/Default"); return cachedShader;
    }
    static void SetMatColor(Material m, Color c) { if (m == null) return; m.color = c; if (m.HasProperty("_Color")) m.SetColor("_Color", c); if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); }

#if UNITY_EDITOR
    Material GetOrCreatePartMaterial(string n, Color c)
    {
        string dir = "Assets/CharacterMats";
        if (!AssetDatabase.IsValidFolder(dir)) { System.IO.Directory.CreateDirectory(dir); AssetDatabase.Refresh(); }
        string p = dir + "/" + n + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(GetCharShader()); m.name = n; SetMatColor(m, c); AssetDatabase.CreateAsset(m, p); EditorUtility.SetDirty(m); }
        return m;
    }
#endif

    void OnEnable() { if (!Application.isPlaying) { if (transform.Find("CubeWorldRoot") == null) CreateCharacter(); BindBones(); ApplyEditorPose(); } }
    void OnValidate() { if (!Application.isPlaying && transform.Find("CubeWorldRoot") != null) { BindBones(); ApplyEditorPose(); } }
    void ApplyEditorPose() { if (previewWeaponPose) SetArmPoseWeapon(0f); else SetArmPoseIdle(); }

    void SetArmPoseWeapon(float p) { ApplyRot(upperArmR, Quaternion.Euler(-armRaiseForward + p, 0, armCenterRoll)); ApplyRot(forearmR, Quaternion.Euler(-armElbowStraight, 0, 0)); ApplyRot(upperArmL, Quaternion.Euler(-leftArmRaise + p * 0.5f, 0, -5f)); ApplyRot(forearmL, Quaternion.Euler(-10f, 0, 0)); if (basePositionsSaved) { ApplyPos(shoulderR, baseShoulderR + offsetShoulderR); ApplyPos(upperArmR, baseUpperArmR + offsetUpperArmR); ApplyPos(forearmR, baseForearmR + offsetForearmR); ApplyPos(handR, baseHandR + offsetHandR); ApplyPos(shoulderL, baseShoulderL + offsetShoulderL); ApplyPos(upperArmL, baseUpperArmL + offsetUpperArmL); ApplyPos(forearmL, baseForearmL + offsetForearmL); ApplyPos(handL, baseHandL + offsetHandL); } }
    void SetArmPoseIdle() { ApplyRot(upperArmR, Quaternion.Euler(0, 0, 5f)); ApplyRot(upperArmL, Quaternion.Euler(0, 0, -5f)); ApplyRot(forearmR, Quaternion.Euler(-10f, 0, 0)); ApplyRot(forearmL, Quaternion.Euler(-10f, 0, 0)); if (basePositionsSaved) { ApplyPos(shoulderR, baseShoulderR); ApplyPos(upperArmR, baseUpperArmR); ApplyPos(forearmR, baseForearmR); ApplyPos(handR, baseHandR); ApplyPos(shoulderL, baseShoulderL); ApplyPos(upperArmL, baseUpperArmL); ApplyPos(forearmL, baseForearmL); ApplyPos(handL, baseHandL); } }
    void SetLegsNeutral() { ApplyRot(thighR, Quaternion.identity); ApplyRot(thighL, Quaternion.identity); ApplyRot(shinR, Quaternion.identity); ApplyRot(shinL, Quaternion.identity); ApplyRot(footR, Quaternion.identity); ApplyRot(footL, Quaternion.identity); }

    public void ApplyWeaponAnchorSettings() { if (WeaponAnchor == null) return; WeaponAnchor.localPosition = weaponLocalPosition; WeaponAnchor.localRotation = Quaternion.Euler(weaponLocalRotation); WeaponAnchor.localScale = weaponLocalScale; }
    [ContextMenu("Сброс WeaponAnchor")] public void ResetWeaponAnchorToDefault() { weaponLocalPosition = new Vector3(0f, 0f, 0.05f); weaponLocalRotation = Vector3.zero; weaponLocalScale = Vector3.one; ApplyWeaponAnchorSettings(); }
    [ContextMenu("Пересобрать")] public void RebuildCharacter() { if (Application.isPlaying) return; Transform o = transform.Find("CubeWorldRoot"); if (o) DestroyImmediate(o.gameObject); basePositionsSaved = false; CreateCharacter(); BindBones(); }

    void OnDrawGizmos() { if (!showGizmoArrow || WeaponAnchor == null) return; Gizmos.color = gizmoColor; Gizmos.DrawRay(WeaponAnchor.position, WeaponAnchor.forward * 0.5f); Gizmos.DrawWireSphere(WeaponAnchor.position, 0.05f); }

    void Start()
    {
        if (transform.Find("CubeWorldRoot") == null) CreateCharacter();
        BindBones();
        PlayerController pc = GetComponent<PlayerController>();
        cachedCam = (pc != null && pc.playerCamera != null) ? pc.playerCamera : Camera.main;
        if (hideLocalArms && (photonView == null || photonView.IsMine)) HideLocalArmVisuals();
        lastPos = transform.position; currentPitch = 0f;
        IdlePoseInstant();
    }

    void IdlePoseInstant() { if (upperArmR) upperArmR.localRotation = Quaternion.Euler(0, 0, 5f); if (upperArmL) upperArmL.localRotation = Quaternion.Euler(0, 0, -5f); if (forearmR) forearmR.localRotation = Quaternion.Euler(-10f, 0, 0); if (forearmL) forearmL.localRotation = Quaternion.Euler(-10f, 0, 0); }
    float GetCameraPitch() { if (cachedCam == null) return 0f; Quaternion r = Quaternion.Inverse(transform.rotation) * cachedCam.transform.rotation; float p = r.eulerAngles.x; if (p > 180f) p -= 360f; return Mathf.Clamp(p, -aimPitchLimit, aimPitchLimit); }

    void BindBones()
    {
        root = transform.Find("CubeWorldRoot"); if (root == null) return;
        hips = root.Find("Hips"); if (hips == null) return; spine = hips.Find("Spine"); chest = spine != null ? spine.Find("Chest") : null; neck = chest != null ? chest.Find("Neck") : null; head = neck != null ? neck.Find("Head") : null;
        shoulderR = chest != null ? chest.Find("ShoulderR") : null; upperArmR = shoulderR != null ? shoulderR.Find("UpperArmR") : null; forearmR = upperArmR != null ? upperArmR.Find("ForearmR") : null; handR = forearmR != null ? forearmR.Find("HandR") : null;
        shoulderL = chest != null ? chest.Find("ShoulderL") : null; upperArmL = shoulderL != null ? shoulderL.Find("UpperArmL") : null; forearmL = upperArmL != null ? upperArmL.Find("ForearmL") : null; handL = forearmL != null ? forearmL.Find("HandL") : null;
        thighR = hips.Find("ThighR"); shinR = thighR != null ? thighR.Find("ShinR") : null; footR = shinR != null ? shinR.Find("FootR") : null;
        thighL = hips.Find("ThighL"); shinL = thighL != null ? thighL.Find("ShinL") : null; footL = shinL != null ? shinL.Find("FootL") : null;
        handRVis = handR != null ? handR.Find("Vis_ArmR_Hand") : null; if (handRVis != null) handRBaseScale = handRVis.localScale;
        WeaponAnchor = handR != null ? handR.Find("WeaponAnchor") : null;
        if (!basePositionsSaved && shoulderR != null && shoulderL != null && upperArmR != null && upperArmL != null)
        {
            Vector3 sr = shoulderR.localPosition; Vector3 sl = shoulderL.localPosition;
            if (Mathf.Abs(sr.x) > 0.01f || Mathf.Abs(sl.x) > 0.01f)
            {
                baseShoulderR = sr; baseUpperArmR = upperArmR.localPosition;
                baseForearmR = forearmR != null ? forearmR.localPosition : Vector3.zero;
                baseHandR = handR != null ? handR.localPosition : Vector3.zero;
                baseShoulderL = sl; baseUpperArmL = upperArmL.localPosition;
                baseForearmL = forearmL != null ? forearmL.localPosition : Vector3.zero;
                baseHandL = handL != null ? handL.localPosition : Vector3.zero;
                basePositionsSaved = true;
            }
        }
    }

    Transform FindDeep(Transform p, string n) { if (p == null) return null; if (p.name == n) return p; foreach (Transform c in p) { Transform f = FindDeep(c, n); if (f != null) return f; } return null; }

    void CreateCharacter()
    {
        root = CreateBone("CubeWorldRoot", transform, Vector3.zero);
        hips = CreateBone("Hips", root, new Vector3(0, legLength, 0)); spine = CreateBone("Spine", hips, new Vector3(0, bodyScale * 0.4f, 0)); chest = CreateBone("Chest", spine, new Vector3(0, bodyScale * 0.3f, 0)); neck = CreateBone("Neck", chest, new Vector3(0, bodyScale * 0.3f, 0)); head = CreateBone("Head", neck, new Vector3(0, headScale * 0.5f, 0));
        shoulderR = CreateBone("ShoulderR", chest, new Vector3(bodyScale * 0.5f, bodyScale * 0.2f, 0)); shoulderL = CreateBone("ShoulderL", chest, new Vector3(-bodyScale * 0.5f, bodyScale * 0.2f, 0));
        upperArmR = CreateBone("UpperArmR", shoulderR, Vector3.zero); forearmR = CreateBone("ForearmR", upperArmR, new Vector3(0, -armLength * 0.5f, 0)); handR = CreateBone("HandR", forearmR, new Vector3(0, -armLength * 0.5f, 0));
        upperArmL = CreateBone("UpperArmL", shoulderL, Vector3.zero); forearmL = CreateBone("ForearmL", upperArmL, new Vector3(0, -armLength * 0.5f, 0)); handL = CreateBone("HandL", forearmL, new Vector3(0, -armLength * 0.5f, 0));
        thighR = CreateBone("ThighR", hips, new Vector3(bodyScale * 0.3f, 0, 0)); shinR = CreateBone("ShinR", thighR, new Vector3(0, -legLength * 0.5f, 0)); footR = CreateBone("FootR", shinR, new Vector3(0, -legLength * 0.5f, 0));
        thighL = CreateBone("ThighL", hips, new Vector3(-bodyScale * 0.3f, 0, 0)); shinL = CreateBone("ShinL", thighL, new Vector3(0, -legLength * 0.5f, 0)); footL = CreateBone("FootL", shinL, new Vector3(0, -legLength * 0.5f, 0));
        CreateCube("Vis_Head", new Vector3(headScale, headScale, headScale), headColor, head, Vector3.zero);
        CreateCube("Vis_Hair", new Vector3(headScale * 1.05f, headScale * 0.25f, headScale * 1.05f), hairColor, head, new Vector3(0, headScale * 0.45f, 0));
        CreateCube("Vis_Body", new Vector3(bodyScale, bodyScale, bodyScale * 0.7f), bodyColor, chest, Vector3.zero);
        CreateCube("Vis_ArmR_Upper", new Vector3(armScale, armLength * 0.5f, armScale), bodyColor, upperArmR, new Vector3(0, -armLength * 0.25f, 0));
        CreateCube("Vis_ArmR_Fore", new Vector3(armScale * 0.9f, armLength * 0.5f, armScale * 0.9f), headColor, forearmR, new Vector3(0, -armLength * 0.25f, 0));
        CreateCube("Vis_ArmR_Hand", new Vector3(armScale * 0.85f, armScale * 0.85f, armScale * 0.85f), headColor, handR, Vector3.zero);
        CreateCube("Vis_ArmL_Upper", new Vector3(armScale, armLength * 0.5f, armScale), bodyColor, upperArmL, new Vector3(0, -armLength * 0.25f, 0));
        CreateCube("Vis_ArmL_Fore", new Vector3(armScale * 0.9f, armLength * 0.5f, armScale * 0.9f), headColor, forearmL, new Vector3(0, -armLength * 0.25f, 0));
        CreateCube("Vis_ArmL_Hand", new Vector3(armScale * 0.85f, armScale * 0.85f, armScale * 0.85f), headColor, handL, Vector3.zero);
        CreateCube("Vis_LegR_Upper", new Vector3(legScale, legLength * 0.5f, legScale), legColor, thighR, new Vector3(0, -legLength * 0.25f, 0));
        CreateCube("Vis_LegR_Shin", new Vector3(legScale * 0.9f, legLength * 0.5f, legScale * 0.9f), legColor, shinR, new Vector3(0, -legLength * 0.25f, 0));
        CreateCube("Vis_LegR_Foot", new Vector3(legScale * 1.2f, legScale * 0.4f, legScale * 1.3f), bootColor, footR, new Vector3(0, -legScale * 0.2f, legScale * 0.15f));
        CreateCube("Vis_LegL_Upper", new Vector3(legScale, legLength * 0.5f, legScale), legColor, thighL, new Vector3(0, -legLength * 0.25f, 0));
        CreateCube("Vis_LegL_Shin", new Vector3(legScale * 0.9f, legLength * 0.5f, legScale * 0.9f), legColor, shinL, new Vector3(0, -legLength * 0.25f, 0));
        CreateCube("Vis_LegL_Foot", new Vector3(legScale * 1.2f, legScale * 0.4f, legScale * 1.3f), bootColor, footL, new Vector3(0, -legScale * 0.2f, legScale * 0.15f));
        GameObject a = new GameObject("WeaponAnchor"); a.transform.SetParent(handR); a.transform.localPosition = weaponLocalPosition; a.transform.localRotation = Quaternion.Euler(weaponLocalRotation); a.transform.localScale = weaponLocalScale; WeaponAnchor = a.transform;
        handRVis = handR.Find("Vis_ArmR_Hand"); if (handRVis != null) handRBaseScale = handRVis.localScale;
        basePositionsSaved = false;
    }

    Transform CreateBone(string n, Transform p, Vector3 pos) { GameObject b = new GameObject(n); b.transform.SetParent(p); b.transform.localPosition = pos; b.transform.localRotation = Quaternion.identity; return b.transform; }

    Transform CreateCube(string n, Vector3 s, Color c, Transform p, Vector3 lp)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = n; cube.transform.SetParent(p); cube.transform.localPosition = lp; cube.transform.localRotation = Quaternion.identity; cube.transform.localScale = s; Destroy(cube.GetComponent<Collider>());
        Renderer r = cube.GetComponent<Renderer>();
#if UNITY_EDITOR
        if (!Application.isPlaying) r.sharedMaterial = GetOrCreatePartMaterial(n, c); else
#endif
        { Material m = new Material(GetCharShader()); m.name = "VoxelCharMat_" + n; SetMatColor(m, c); r.sharedMaterial = m; }
        return cube.transform;
    }

    void HideLocalArmVisuals()
    {
        foreach (string n in new[] { "Vis_ArmR_Upper", "Vis_ArmR_Fore", "Vis_ArmR_Hand", "Vis_ArmL_Upper", "Vis_ArmL_Fore", "Vis_ArmL_Hand" })
        {
            Transform t = FindDeep(root, n);
            if (t == null) continue;
            int l = LayerMask.NameToLayer("LocalArms");
            if (l >= 0) SetLayerRecursive(t.gameObject, l);
            else t.gameObject.SetActive(false);
        }
        int layer = LayerMask.NameToLayer("LocalArms");
        if (layer >= 0 && cachedCam != null) cachedCam.cullingMask &= ~(1 << layer);
    }
    void SetLayerRecursive(GameObject go, int l) { go.layer = l; foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, l); }

    void Update()
    {
        if (!Application.isPlaying) return;
        smoothT = 1f - Mathf.Exp(-poseSmoothSpeed * Time.deltaTime);
        float tp = (weaponFollowsCamera && HasWeapon) ? GetCameraPitch() : 0f;
        currentPitch = Mathf.Lerp(currentPitch, tp, Time.deltaTime * aimSmoothSpeed);
        if (handRVis != null) { Vector3 t = HasWeapon ? handRBaseScale * gripHandShrink : handRBaseScale; handRVis.localScale = Vector3.Lerp(handRVis.localScale, t, Time.deltaTime * 10f); }
        float speed = (transform.position - lastPos).magnitude / Mathf.Max(Time.deltaTime, 1e-4f); lastPos = transform.position;
        bool moving = speed > 0.5f;
        if (moving) { walkTime += Time.deltaTime * walkSpeed; AnimateWalk(); }
        else { walkTime = 0f; SetLegsNeutral(); if (HasWeapon) WeaponPose(); else IdlePose(); }
    }
    void IdlePose() { SetArmPoseIdle(); }
    void AnimateWalk()
    {
        float s = Mathf.Sin(walkTime) * walkLegSwing;
        ApplyRot(thighR, Quaternion.Euler(s, 0, 0)); ApplyRot(thighL, Quaternion.Euler(-s, 0, 0));
        ApplyRot(shinR, Quaternion.Euler(Mathf.Max(0, -s) * 0.5f, 0, 0)); ApplyRot(shinL, Quaternion.Euler(Mathf.Max(0, s) * 0.5f, 0, 0));
        ApplyRot(footR, Quaternion.Euler(-Mathf.Max(0, -s) * 0.25f, 0, 0)); ApplyRot(footL, Quaternion.Euler(-Mathf.Max(0, s) * 0.25f, 0, 0));
        if (HasWeapon) WeaponPose();
        else { float a = Mathf.Sin(walkTime) * walkArmSwing; ApplyRot(upperArmR, Quaternion.Euler(-a, 0, 0)); ApplyRot(upperArmL, Quaternion.Euler(a, 0, 0)); ApplyRot(forearmR, Quaternion.Euler(-10f, 0, 0)); ApplyRot(forearmL, Quaternion.Euler(-10f, 0, 0)); }
    }
    void WeaponPose() { SetArmPoseWeapon(currentPitch); }
}