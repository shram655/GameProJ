using UnityEngine;
using UnityEngine.UI;

public class CharacterPreviewUI : MonoBehaviour
{
    [Header("UI")]
    public RawImage previewImage;

    [Header("Модель")]
    public GameObject characterModelPrefab; // необязательно: если пусто — возьмёт твоего игрока
    public Vector3 previewPosition = new Vector3(0, -100, 0); // спрятано под картой
    public float startRotationY = 180f; // 180 = лицом к камере

    [Header("Камера")]
    public bool autoFrame = true; // автоцентровка персонажа
    public Vector3 cameraOffset = new Vector3(0, 1.6f, -2.5f); // ручной режим
    public Vector3 lookAtOffset = new Vector3(0, 1.0f, 0); // ручной режим
    public float viewMargin = 1.2f; // отступ (больше = дальше камера)
    public int rtWidth = 512; // базовая ширина RenderTexture

    [Header("Анимация")]
    public bool rotateModel = true;
    public float rotateSpeed = 30f;

    private GameObject modelInstance;
    private Camera previewCam;
    private Light previewLight;
    private RenderTexture rt;
    private float lastAspect = 0f;
    private bool initialized = false;

    void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;

        if (previewImage == null)
        {
            Debug.LogError("❌ CharacterPreviewUI: не назначен RawImage!");
            return;
        }

        // ── Источник модели: префаб или локальный игрок ──
        GameObject source = characterModelPrefab;
        bool cloneWholePlayer = false;
        if (source == null)
        {
            source = FindLocalPlayer();
            cloneWholePlayer = true;
        }

        if (source == null)
        {
            Debug.LogWarning("⚠️ CharacterPreviewUI: не найдена модель персонажа!");
            return;
        }

        // ── Создаём копию под картой, повёрнутую ЛИЦОМ к камере ──
        modelInstance = Instantiate(source, previewPosition, Quaternion.Euler(0f, startRotationY, 0f));
        if (cloneWholePlayer)
        {
            StripClone(modelInstance);

            // 🆕 Возвращаем руки/все Vis_ части на клоне для превью
            // (у локального игрока они скрыты для FPS-камеры)
            foreach (Transform t in modelInstance.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Vis_")) t.gameObject.SetActive(true);
            }
        }

        // ── Камера превью ──
        GameObject camObj = new GameObject("PreviewCamera");
        previewCam = camObj.AddComponent<Camera>();
        previewCam.clearFlags = CameraClearFlags.SolidColor;
        previewCam.backgroundColor = new Color(0, 0, 0, 0); // прозрачный фон
        previewCam.nearClipPlane = 0.1f;

        // RenderTexture с пропорцией как у RawImage — без сплющивания
        RebuildRT();

        // ── Автокадр: персонаж ровно по центру ──
        if (autoFrame)
        {
            Bounds b = GetModelBounds(modelInstance);
            Vector3 center = b.center;
            float h = Mathf.Max(b.size.y, 0.5f);
            float dist = h * viewMargin + 0.5f;
            previewCam.transform.position = center + new Vector3(0, h * 0.05f, -dist);
            previewCam.transform.LookAt(center);
            previewCam.farClipPlane = dist + 10f;
        }
        else
        {
            previewCam.transform.position = previewPosition + cameraOffset;
            previewCam.transform.LookAt(previewPosition + lookAtOffset);
            previewCam.farClipPlane = 15f;
        }

        // ── Свет для превью ──
        GameObject lightObj = new GameObject("PreviewLight");
        lightObj.transform.position = previewCam.transform.position + new Vector3(1.5f, 1f, 0.5f);
        previewLight = lightObj.AddComponent<Light>();
        previewLight.type = LightType.Point;
        previewLight.range = 12f;
        previewLight.intensity = 1.2f;

        previewImage.texture = rt;

        Debug.Log("✅ CharacterPreviewUI: превью создано");
    }

    // ══════════════════════════════════════════════════════
    // RenderTexture с той же пропорцией, что и RawImage
    // ══════════════════════════════════════════════════════
    void RebuildRT()
    {
        RectTransform rect = previewImage.rectTransform;
        float w = Mathf.Max(1f, rect.rect.width);
        float h = Mathf.Max(1f, rect.rect.height);
        lastAspect = h / w;

        int rw = rtWidth;
        int rh = Mathf.Max(64, Mathf.RoundToInt(rtWidth * lastAspect));

        RenderTexture newRt = new RenderTexture(rw, rh, 24, RenderTextureFormat.ARGB32);
        newRt.name = "CharacterPreviewRT";

        if (previewCam != null) previewCam.targetTexture = newRt;
        if (rt != null) rt.Release();
        rt = newRt;

        if (previewImage != null) previewImage.texture = rt;
    }

    Bounds GetModelBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0)
        {
            return new Bounds(go.transform.position + Vector3.up, Vector3.one * 2f);
        }

        Bounds b = rs[0].bounds;
        foreach (var r in rs)
        {
            b.Encapsulate(r.bounds);
        }
        return b;
    }

    // ══════════════════════════════════════════════════════
    // Компоненты НЕ уничтожаются, а ОТКЛЮЧАЮТСЯ.
    // Для превью нужны только рендереры — остальное глушим.
    // ══════════════════════════════════════════════════════
    void StripClone(GameObject clone)
    {
        // 1) Отключаем ВСЕ скрипты
        foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true))
        {
            mb.enabled = false;
        }

        // 2) Отключаем коллайдеры
        foreach (var col in clone.GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false;
        }

        // 3) Глушим звук, камеру и AudioListener клона
        foreach (var asrc in clone.GetComponentsInChildren<AudioSource>(true)) asrc.enabled = false;
        foreach (var cam in clone.GetComponentsInChildren<Camera>(true)) cam.enabled = false;
        foreach (var al in clone.GetComponentsInChildren<AudioListener>(true)) al.enabled = false;

        // 4) Отключаем Canvas игрока
        foreach (var cv in clone.GetComponentsInChildren<Canvas>(true)) cv.enabled = false;

        // 5) Rigidbody уничтожаем безопасно
        foreach (var rb in clone.GetComponentsInChildren<Rigidbody>(true))
        {
            Destroy(rb);
        }
    }

    GameObject FindLocalPlayer()
    {
        PlayerController[] pcs = FindObjectsOfType<PlayerController>();
        foreach (var pc in pcs)
        {
            if (pc.view != null && pc.view.IsMine)
            {
                return pc.gameObject;
            }
        }
        return null;
    }

    void Update()
    {
        if (modelInstance != null && rotateModel)
        {
            modelInstance.transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
        }

        // Если RawImage поменял размер — пересоздаём RT под новую пропорцию
        if (previewImage != null && previewCam != null)
        {
            RectTransform rect = previewImage.rectTransform;
            float aspect = Mathf.Max(1f, rect.rect.height) / Mathf.Max(1f, rect.rect.width);
            if (Mathf.Abs(aspect - lastAspect) > 0.02f)
            {
                RebuildRT();
            }
        }
    }

    void OnDestroy()
    {
        if (modelInstance != null) Destroy(modelInstance);
        if (previewCam != null) Destroy(previewCam.gameObject);
        if (previewLight != null) Destroy(previewLight.gameObject);
        if (rt != null) rt.Release();
    }
}