using UnityEngine;
using Photon.Pun;

public class PlayerSpawner : MonoBehaviourPunCallbacks
{
    [Header("Настройки")]
    public GameObject playerPrefab;
    public Transform[] spawnPoints;

    void Start()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("❌ Player Prefab не назначен!");
            return;
        }

        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int randomIndex = Random.Range(0, spawnPoints.Length);
            spawnPosition = spawnPoints[randomIndex].position;
            spawnRotation = spawnPoints[randomIndex].rotation;
        }

        Debug.Log("🎮 Спавним игрока на позиции: " + spawnPosition);

        PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, spawnRotation);
    }
}