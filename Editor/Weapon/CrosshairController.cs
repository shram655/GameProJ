using UnityEngine;
using UnityEngine.UI;

public class CrosshairController : MonoBehaviour
{
    [Header("Элементы прицела")]
    public RectTransform crosshairTop;
    public RectTransform crosshairBottom;
    public RectTransform crosshairLeft;
    public RectTransform crosshairRight;
    public RectTransform crosshairCenter;

    [Header("Настройки")]
    public float defaultGap = 6f; 
    public float spreadGap = 15f; 
    public float smoothSpeed = 10f; 

    [Header("Цвет прицела")]
    public Color normalColor = Color.white;
    public Color hitColor = Color.red;
    public Color lowAmmoColor = Color.yellow;

    private float currentGap;
    private float targetGap;
    private Image[] crosshairImages;

    void Start()
    {
        currentGap = defaultGap;
        targetGap = defaultGap;

        crosshairImages = new Image[]
        {
            crosshairTop != null ? crosshairTop.GetComponent<Image>() : null,
            crosshairBottom != null ? crosshairBottom.GetComponent<Image>() : null,
            crosshairLeft != null ? crosshairLeft.GetComponent<Image>() : null,
            crosshairRight != null ? crosshairRight.GetComponent<Image>() : null,
            crosshairCenter != null ? crosshairCenter.GetComponent<Image>() : null
        };

        SetCrosshairColor(normalColor);
    }

    void Update()
    {
        currentGap = Mathf.Lerp(currentGap, targetGap, Time.deltaTime * smoothSpeed);

        if (crosshairTop != null) crosshairTop.anchoredPosition = new Vector2(0, currentGap);
        if (crosshairBottom != null) crosshairBottom.anchoredPosition = new Vector2(0, -currentGap);
        if (crosshairLeft != null) crosshairLeft.anchoredPosition = new Vector2(-currentGap, 0);
        if (crosshairRight != null) crosshairRight.anchoredPosition = new Vector2(currentGap, 0);

        CheckMovement();
    }

    void CheckMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        bool isMoving = Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f;
        bool isSprinting = Input.GetKey(KeyCode.LeftShift);

        if (isSprinting) targetGap = spreadGap * 1.5f;
        else if (isMoving) targetGap = spreadGap;
        else targetGap = defaultGap;
    }

    public void OnShoot()
    {
        targetGap = spreadGap * 1.3f;
        Invoke("ResetGap", 0.1f);
    }

    public void OnHit()
    {
        SetCrosshairColor(hitColor);
        Invoke("ResetColor", 0.2f);
    }

    public void OnLowAmmo()
    {
        SetCrosshairColor(lowAmmoColor);
        Invoke("ResetColor", 1f);
    }

    void ResetGap() { targetGap = defaultGap; }
    void ResetColor() { SetCrosshairColor(normalColor); }

    void SetCrosshairColor(Color color)
    {
        foreach (Image img in crosshairImages)
        {
            if (img != null) img.color = color;
        }
    }

    // 🔧 ИСПРАВЛЕННЫЙ МЕТОД СКРЫТИЯ/ПОКАЗА
    public void SetCrosshairActive(bool active)
    {
        // ВМЕСТО gameObject.SetActive(active) мы отключаем только линии прицела.
        // Сам объект (контейнер) остаётся активным, чтобы Move_Player мог его найти!
        if (crosshairTop != null) crosshairTop.gameObject.SetActive(active);
        if (crosshairBottom != null) crosshairBottom.gameObject.SetActive(active);
        if (crosshairLeft != null) crosshairLeft.gameObject.SetActive(active);
        if (crosshairRight != null) crosshairRight.gameObject.SetActive(active);
        if (crosshairCenter != null) crosshairCenter.gameObject.SetActive(active);
    }
}