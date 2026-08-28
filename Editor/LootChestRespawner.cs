using UnityEngine;

public class LootChestRespawner : MonoBehaviour
{
    [Header("Настройки респавна сундука")]
    [Tooltip("Через сколько секунд сундук появится снова на том же месте")]
    public float respawnTime = 60f;

    [Tooltip("ВКЛ = респавн активен")]
    public bool enableRespawn = true;

    // 🆕 Когда блок появился в мире (респавн у всех клиентов) —
    // регистрируем его как живой и очищаем "обработанное разрушение"
    void Start()
    {
        if (WorldManager.Instance != null)
        {
            WorldManager.Instance.RegisterAliveBlock(gameObject, transform.position);
        }
    }
}