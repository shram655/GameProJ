#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public static class FixRaycastTargets
{
    [MenuItem("Tools/Исправить Raycast Target в инвентаре")]
    public static void Fix()
    {
        int fixedCount = 0;

        // Находим все слоты инвентаря и хотбара
        string[] slotNames = { "Slot_", "Cell_" };

        foreach (string prefix in slotNames)
        {
            for (int i = 0; i < 20; i++)
            {
                string name = prefix + i;
                GameObject slot = GameObject.Find(name);
                if (slot == null) continue;

                // На самом слоте — включаем Raycast Target
                Image slotImage = slot.GetComponent<Image>();
                if (slotImage != null)
                {
                    slotImage.raycastTarget = true;
                    fixedCount++;
                }

                // На Icon — выключаем
                Transform icon = slot.transform.Find("Icon");
                if (icon != null)
                {
                    Image iconImg = icon.GetComponent<Image>();
                    if (iconImg != null)
                    {
                        iconImg.raycastTarget = false;
                        fixedCount++;
                    }
                }

                // На CountText — выключаем
                Transform countText = slot.transform.Find("CountText");
                if (countText != null)
                {
                    TextMeshProUGUI tmp = countText.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.raycastTarget = false;
                        fixedCount++;
                    }
                    Text txt = countText.GetComponent<Text>();
                    if (txt != null)
                    {
                        txt.raycastTarget = false;
                        fixedCount++;
                    }
                }
            }
        }

        if (Selection.activeObject != null)
        {
            EditorUtility.SetDirty(Selection.activeObject);
        }
        Debug.Log($"✅ Исправлено Raycast Target на {fixedCount} компонентах!");
        Debug.Log("⚠️ Не забудь сохранить сцену (Ctrl+S)!");
    }
}
#endif