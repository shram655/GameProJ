using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CubeWorldCharacter))]
[CanEditMultipleObjects]
public class CubeWorldCharacterEditor : Editor
{
    private bool showVisualEditor = true;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        CubeWorldCharacter c = (CubeWorldCharacter)target;

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("🎯 WeaponAnchor (позиция оружия в кисти)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Оружие от 1-го лица настраивается через WeaponSlot (в префабе игрока, внутри камеры).\n" +
            "Оружие от 3-го лица — через WeaponAnchor ниже.",
            MessageType.Info);

        showVisualEditor = EditorGUILayout.Foldout(showVisualEditor, "⚙️ Настройка WeaponAnchor");
        if (showVisualEditor)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponLocalPosition"), new GUIContent("Позиция"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponLocalRotation"), new GUIContent("Вращение"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponLocalScale"), new GUIContent("Масштаб"));
            EditorGUI.indentLevel--;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 Сброс")) { c.ResetWeaponAnchorToDefault(); EditorUtility.SetDirty(c); }
            if (GUILayout.Button("💾 Запомнить") && c.WeaponAnchor != null)
            {
                serializedObject.FindProperty("weaponLocalPosition").vector3Value = c.WeaponAnchor.localPosition;
                serializedObject.FindProperty("weaponLocalRotation").vector3Value = c.WeaponAnchor.localEulerAngles;
                serializedObject.FindProperty("weaponLocalScale").vector3Value = c.WeaponAnchor.localScale;
                serializedObject.ApplyModifiedProperties();
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Остальные настройки:", EditorStyles.boldLabel);
        DrawPropertiesExcluding(serializedObject, "m_Script",
            "weaponLocalPosition", "weaponLocalRotation", "weaponLocalScale");

        if (serializedObject.ApplyModifiedProperties())
        {
            c.ApplyWeaponAnchorSettings();
            EditorUtility.SetDirty(c);
        }
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
    static void DrawWeaponAnchorGizmo(CubeWorldCharacter c, GizmoType gt)
    {
        if (c.WeaponAnchor == null) return;
        Handles.color = Color.yellow;
        Handles.DrawWireDisc(c.WeaponAnchor.position, c.WeaponAnchor.forward, 0.08f);
        Handles.color = Color.red;
        Handles.ArrowHandleCap(0, c.WeaponAnchor.position, c.WeaponAnchor.rotation, 0.4f, EventType.Repaint);
        GUIStyle s = new GUIStyle { normal = { textColor = Color.yellow }, fontSize = 14, fontStyle = FontStyle.Bold };
        Handles.Label(c.WeaponAnchor.position + Vector3.up * 0.15f, "WeaponAnchor", s);
    }
}