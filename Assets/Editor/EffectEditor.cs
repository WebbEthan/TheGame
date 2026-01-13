using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

[CustomPropertyDrawer(typeof(EffectDesign))]
public class EffectDesignDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty scriptProp = property.FindPropertyRelative("EffectScript");
        SerializedProperty valuesProp = property.FindPropertyRelative("ParameterValues");

        string[] names = AssetManager.GetEffectNames();
        int currentIndex = Array.IndexOf(names, scriptProp.stringValue);
        if (currentIndex < 0) currentIndex = 0;

        // 1. Draw Type Dropdown
        Rect rect = new Rect(position.x, position.y, position.width, 18);
        int newIndex = EditorGUI.Popup(rect, "Effect Type", currentIndex, names);

        if (newIndex != currentIndex || string.IsNullOrEmpty(scriptProp.stringValue))
        {
            scriptProp.stringValue = names[newIndex];
            valuesProp.ClearArray();
            property.serializedObject.ApplyModifiedProperties();
        }

        // 2. Determine if the current class has [ShowDuration]
        Type currentType = AssetManager.GetEffectType(scriptProp.stringValue);
        bool hasShowDuration = currentType?.GetCustomAttribute<ShowDurationAttribute>() != null;

        // 3. Get cached fields
        FieldInfo[] fields = AssetManager.GetFieldsForType(scriptProp.stringValue);

        if (fields != null)
        {
            // Sync array size
            if (valuesProp.arraySize != fields.Length)
                valuesProp.arraySize = fields.Length;

            for (int i = 0; i < fields.Length; i++)
            {
                // EDITOR-ONLY CHECK: Skip Duration if attribute is missing
                if (fields[i].Name == "Duration" && !hasShowDuration)
                {
                    // Optionally force the serialized string to "0" here so it's saved correctly
                    valuesProp.GetArrayElementAtIndex(i).stringValue = "0";
                    continue;
                }

                rect.y += 20;
                SerializedProperty element = valuesProp.GetArrayElementAtIndex(i);

                // Draw type-specific field (Int, Float, etc.)
                DrawTypeSpecificField(rect, fields[i], element);
            }
        }

        EditorGUI.EndProperty();
    }

    private void DrawTypeSpecificField(Rect rect, FieldInfo field, SerializedProperty element)
    {
        if (field.FieldType == typeof(float))
        {
            float.TryParse(element.stringValue, out float val);
            element.stringValue = EditorGUI.FloatField(rect, field.Name, val).ToString();
        }
        else if (field.FieldType == typeof(int))
        {
            int.TryParse(element.stringValue, out int val);
            element.stringValue = EditorGUI.IntField(rect, field.Name, val).ToString();
        }
        else
        {
            element.stringValue = EditorGUI.TextField(rect, field.Name, element.stringValue);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        string scriptName = property.FindPropertyRelative("EffectScript").stringValue;
        Type type = AssetManager.GetEffectType(scriptName);
        FieldInfo[] fields = AssetManager.GetFieldsForType(scriptName);

        if (fields == null) return 20f;

        bool hasShowDuration = type?.GetCustomAttribute<ShowDurationAttribute>() != null;
        int visibleCount = 0;

        foreach (var f in fields)
        {
            if (f.Name == "Duration" && !hasShowDuration) continue;
            visibleCount++;
        }

        return 20f + (visibleCount * 20f) + 5f;
    }
}