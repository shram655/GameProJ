using UnityEngine;
using Photon.Pun;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("═══ НАСТРОЙКИ ДВИЖЕНИЯ ═══")]
    public float speed = 7f;
    public float sprintSpeed = 15f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;

    [Header("═══ CHARACTER CONTROLLER ═══")]
    public float controllerRadius = 0.35f;
    public float skinWidth = 0.01f;
    public float stepOffset = 0.3f;
    public float slopeLimit = 45f;

    [Header("═══ 🆕 ПРИЖАТИЕ К ЗЕМЛЕ ═══")]
    public bool snapToGroundOnStart = true;
    public float liftHeight = 5f;
    public float maxGroundSearchHeight = 50f;
    public LayerMask groundMask = ~0;
    public int snapAttempts = 3;
    public float snapDelay = 0.1f;

    private CharacterController controller;
    private PlayerController playerController;
    private PlayerInventory playerInventory;
    private CubeWorldCharacter cubeCharacter;
    private float verticalVelocity = 0f;

    // 🆕 Реальные границы модели (относительно transform)
    private float modelMinY = 0f;   // низ (подошвы)
    private float modelMaxY = 1.45f; // верх (голова)

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerController = GetComponent<PlayerController>();
        playerInventory = GetComponent<PlayerInventory>();
        cubeCharacter = GetComponent<CubeWorldCharacter>();
    }

    void Start()
    {
        StartCoroutine(InitializeAndSnap());
    }

    IEnumerator InitializeAndSnap()
    {
        // Ждём 2 кадра чтобы модель создалась
        yield return null;
        yield return null;

        // 1. Вычисляем реальные границы модели
        CalculateModelBounds();

        // 2. Настраиваем капсулу ТОЧНО под модель
        SetupControllerFromModel();

        // 3. Отключаем контроллер перед прижатием
        if (controller != null) controller.enabled = false;

        // 4. Прижимаем к земле
        if (snapToGroundOnStart)
        {
            for (int i = 0; i < snapAttempts; i++)
            {
                SnapToGroundReliable();
                if (!IsStuckInGround()) { Debug.Log($"✅ Прижато с попытки {i + 1}"); break; }
                yield return new WaitForSeconds(snapDelay);
            }
        }

        // 5. Включаем контроллер
        if (controller != null) controller.enabled = true;

        // 6. Контрольная проверка
        yield return new WaitForSeconds(0.5f);
        if (IsStuckInGround()) EmergencyUnstuck();
    }

    /// <summary>
    /// 🆕 Вычисляем реальные низ и верх модели по мешам
    /// </summary>
    void CalculateModelBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            modelMinY = -0.06f;
            modelMaxY = 1.45f;
            return;
        }

        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (var r in renderers)
        {
            // Игнорируем оружие и превью — только тело
            string n = r.gameObject.name;
            if (n.Contains("Weapon") || n.Contains("EditorPreview")) continue;

            minY = Mathf.Min(minY, r.bounds.min.y);
            maxY = Mathf.Max(maxY, r.bounds.max.y);
        }

        modelMinY = minY - transform.position.y; // низ относительно корня
        modelMaxY = maxY - transform.position.y; // верх относительно корня

        Debug.Log($"✅ Границы модели: низ={modelMinY:F3}, верх={modelMaxY:F3}, высота={modelMaxY - modelMinY:F3}");
    }

    /// <summary>
    /// 🆕 Капсула ТОЧНО повторяет модель: низ капсулы = подошвы
    /// </summary>
    void SetupControllerFromModel()
    {
        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
            if (controller == null) return;
        }

        float height = Mathf.Max(0.5f, modelMaxY - modelMinY);
        float centerY = (modelMinY + modelMaxY) / 2f;

        controller.height = height;
        controller.center = new Vector3(0f, centerY, 0f);
        controller.radius = controllerRadius;
        controller.skinWidth = skinWidth;
        controller.stepOffset = stepOffset;
        controller.slopeLimit = slopeLimit;

        Debug.Log($"✅ Капсула: height={height:F3}, center.y={centerY:F3} (низ капсулы = подошвы)");
    }

    bool IsStuckInGround()
    {
        if (controller == null) return false;
        Vector3 feetPos = transform.position + Vector3.up * modelMinY;
        Vector3 checkPos = feetPos - Vector3.up * 0.1f;
        Collider[] cols = Physics.OverlapSphere(checkPos, 0.05f, groundMask, QueryTriggerInteraction.Ignore);
        foreach (var col in cols)
        {
            if (col == controller) continue;
            if (col.transform.root == transform) continue;
            return true;
        }
        return false;
    }

    void EmergencyUnstuck()
    {
        if (controller != null) controller.enabled = false;
        transform.position += Vector3.up * 3f;
        SnapToGroundReliable();
        SetupControllerFromModel();
        if (controller != null) controller.enabled = true;
        Debug.Log("🆘 Экстренный подъём выполнен");
    }

    void SnapToGroundReliable()
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        bool wasEnabled = false;
        if (controller != null) { wasEnabled = controller.enabled; controller.enabled = false; }

        Vector3 originalPos = transform.position;
        transform.position = originalPos + Vector3.up * liftHeight;

        Vector3 rayStart = transform.position + Vector3.up * maxGroundSearchHeight;
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, maxGroundSearchHeight * 2f, groundMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RaycastHit? groundHit = null;
        foreach (var hit in hits)
        {
            if (hit.collider == controller) continue;
            if (hit.transform.root == transform) continue;
            if (hit.collider.isTrigger) continue;
            groundHit = hit;
            break;
        }

        if (groundHit.HasValue)
        {
            // 🆕 Опускаем так, чтобы ПОДОШВЫ (modelMinY) касались земли
            float newY = groundHit.Value.point.y - modelMinY;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            Debug.Log($"✅ Ноги на земле: Y={transform.position.y:F2} (подошвы на {groundHit.Value.point.y:F2})");
        }
        else
        {
            transform.position = originalPos;
            Debug.LogWarning("⚠️ Земля не найдена");
        }

        if (controller != null) controller.enabled = wasEnabled;
    }

    void Update()
    {
        if (playerController != null && playerController.view != null && !playerController.view.IsMine) return;
        if (playerController != null && playerController.isPlayerDead) return;

        bool inputBlocked = false;
        if (playerInventory != null && playerInventory.IsInventoryOpen) inputBlocked = true;
        if (ChatManager.IsChatOpen) inputBlocked = true;

        Move(inputBlocked);
    }

    private void Move(bool inputBlocked)
    {
        if (controller == null) return;

        Vector3 horizontalMove = Vector3.zero;
        if (!inputBlocked)
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            horizontalMove = new Vector3(h, 0f, v);
            horizontalMove = Vector3.ClampMagnitude(horizontalMove, 1f);
            horizontalMove = transform.TransformDirection(horizontalMove);
            horizontalMove *= Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : speed;
        }

        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -2f;
            if (!inputBlocked && Input.GetKeyDown(KeyCode.Space)) verticalVelocity = jumpSpeed;
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        controller.Move((horizontalMove + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    void OnDrawGizmos()
    {
        // Красным — где подошвы
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * modelMinY, 0.1f);
        // Голубым — центр капсулы
        if (controller != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position + controller.center, 0.05f);
        }
    }

    public void RefreshCharacterSetup()
    {
        if (controller != null) controller.enabled = false;
        CalculateModelBounds();
        SetupControllerFromModel();
        SnapToGroundReliable();
        if (controller != null) controller.enabled = true;
    }
}