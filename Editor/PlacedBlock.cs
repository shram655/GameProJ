using UnityEngine;

public class PlacedBlock : MonoBehaviour
{
    [Tooltip("ID блока в инвентаре (соответствует индексу в массиве blockPrefabs + 1)")]
    public int blockId;
}