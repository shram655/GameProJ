using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;

public class PlayerHunger : MonoBehaviourPun
{
    [Header("Настройки голода")]
    public float maxHunger = 100f;
    private float currentHunger;
    private float displayHunger;

    [Header("Убывание и урон")]
    public float hungerDecayRate = 1f;
    public float starvationDamage = 5f;
    public float starvationTickRate = 2f;

    [Header("UI")]
    public Slider hungerBar;
    public Image hungerBarFill;
    public TextMeshProUGUI hungerText;

    [Header("Ссылки")]
    public PlayerHealth playerHealth;

    private float starvationTimer = 0f;
    private PlayerController playerController;

    // 🆕 Надёжная проверка: локальный ли это игрок
    bool IsLocalPlayer()
    {
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (playerController != null && playerController.view != null)
            return playerController.view.IsMine;
        return photonView == null || photonView.IsMine;
    }

    void Start()
    {
        currentHunger = maxHunger;
        displayHunger = maxHunger;

        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        if (hungerBar != null)
        {
            hungerBar.maxValue = maxHunger;
            hungerBar.value = maxHunger;
        }
        if (hungerText != null) hungerText.text = Mathf.Ceil(currentHunger).ToString();
        UpdateHungerUI();
    }

    void Update()
    {
        // 🆕 Работает ТОЛЬКО у локального игрока (надёжная проверка)
        if (!IsLocalPlayer()) return;

        // БЛОКИРОВКА ВВОДА ВО ВРЕМЯ ЧАТА (голод продолжает убывать)
        bool blockInput = ChatManager.IsChatOpen;

        // 1. Убывание голода (работает всегда)
        if (currentHunger > 0)
        {
            currentHunger -= hungerDecayRate * Time.deltaTime;
            currentHunger = Mathf.Max(currentHunger, 0f);
            starvationTimer = 0f;
        }
        else
        {
            starvationTimer += Time.deltaTime;
            if (starvationTimer >= starvationTickRate)
            {
                starvationTimer = 0f;
                if (playerHealth != null && !playerHealth.IsDead())
                {
                    playerHealth.TakeDamage(starvationDamage);
                    Debug.Log("💀 Вы получаете урон от голода! (-" + starvationDamage + " HP)");
                }
            }
        }

        // 2. Плавная анимация полоски (работает всегда)
        if (hungerBar != null && displayHunger != currentHunger)
        {
            displayHunger = Mathf.Lerp(displayHunger, currentHunger, Time.deltaTime * 5f);
            hungerBar.value = displayHunger;
        }

        // 3. Обновляем текст (работает всегда)
        if (hungerText != null) hungerText.text = Mathf.Ceil(currentHunger).ToString();

        // 4. Обновляем цвет (работает всегда)
        UpdateHungerUI();

        // 5. КНОПКА O — поесть (ТОЛЬКО когда чат закрыт)
        if (!blockInput && Input.GetKeyDown(KeyCode.O))
        {
            ConsumeFood(30f);
        }
    }

    public void ConsumeFood(float amount)
    {
        if (!IsLocalPlayer()) return;
        currentHunger += amount;
        currentHunger = Mathf.Min(currentHunger, maxHunger);
        Debug.Log("🍔 Вы поели! Восстановлено " + amount + ". Текущий голод: " + currentHunger);
    }

    public void ConsumeFoodItem(int blockId, float hungerRestore)
    {
        if (!IsLocalPlayer()) return;
        ConsumeFood(hungerRestore);
        Debug.Log("🍎 Съеден предмет ID " + blockId + ", восстановлено " + hungerRestore + " голода");
    }

    void UpdateHungerUI()
    {
        if (hungerBarFill != null)
        {
            if (currentHunger > maxHunger * 0.5f)
            {
                hungerBarFill.color = new Color(1f, 0.55f, 0f);
                if (hungerText != null) hungerText.color = Color.white;
            }
            else if (currentHunger > maxHunger * 0.2f)
            {
                hungerBarFill.color = Color.yellow;
                if (hungerText != null) hungerText.color = Color.yellow;
            }
            else
            {
                hungerBarFill.color = Color.red;
                if (hungerText != null) hungerText.color = Color.red;
            }
        }
    }

    public void FullRestore()
    {
        if (!IsLocalPlayer()) return;
        currentHunger = maxHunger;
        displayHunger = maxHunger;
        starvationTimer = 0f;
        if (hungerBar != null) hungerBar.value = maxHunger;
        if (hungerText != null) hungerText.text = maxHunger.ToString();
        UpdateHungerUI();
        Debug.Log("🍖 Голод полностью восстановлен при респавне!");
    }

    public float GetCurrentHunger() { return currentHunger; }
}