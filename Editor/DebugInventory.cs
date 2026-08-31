using UnityEngine;

public class DebugInventory : MonoBehaviour
{
    public GameObject panelToTest; // Перетащите сюда InventoryPanel
    
    void Start()
    {
        Debug.Log("=================================");
        Debug.Log("DebugInventory Start()");
        Debug.Log("panelToTest: " + (panelToTest != null ? "OK" : "NULL!"));
        Debug.Log("=================================");
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            Debug.Log("✅ TAB НАЖАТ!");
            if (panelToTest != null)
            {
                panelToTest.SetActive(!panelToTest.activeSelf);
                Debug.Log("Панель теперь: " + panelToTest.activeSelf);
            }
        }
        
        if (Input.GetKeyDown(KeyCode.I))
        {
            Debug.Log("✅ I НАЖАТА!");
            if (panelToTest != null)
            {
                panelToTest.SetActive(!panelToTest.activeSelf);
                Debug.Log("Панель теперь: " + panelToTest.activeSelf);
            }
        }
    }
}