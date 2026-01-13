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

        // 1. Draw the Type Selection Popup
        Rect rect = new Rect(position.x, position.y, position.width, 18);
        int newIndex = EditorGUI.Popup(rect, "Effect Type", currentIndex, names);

        if (newIndex != currentIndex || string.IsNullOrEmpty(scriptProp.stringValue))
        {
            scriptProp.stringValue = names[newIndex];
            valuesProp.ClearArray();
            property.serializedObject.ApplyModifiedProperties();
        }

        // 2. Draw Type-Specific Fields
        FieldInfo[] fields = AssetManager.GetFieldsForType(scriptProp.stringValue);
        if (fields != null)
        {
            if (valuesProp.arraySize != fields.Length)
                valuesProp.arraySize = fields.Length;

            for (int i = 0; i < fields.Length; i++)
            {
                rect.y += 20;
                SerializedProperty element = valuesProp.GetArrayElementAtIndex(i);
                Type fieldType = fields[i].FieldType;

                EditorGUI.BeginChangeCheck();

                // Logic for specific types
                if (fieldType == typeof(int))
                {
                    int val = int.TryParse(element.stringValue, out int res) ? res : 0;
                    element.stringValue = EditorGUI.IntField(rect, fields[i].Name, val).ToString();
                }
                else if (fieldType == typeof(float))
                {
                    float val = float.TryParse(element.stringValue, out float res) ? res : 0f;
                    element.stringValue = EditorGUI.FloatField(rect, fields[i].Name, val).ToString();
                }
                else if (fieldType == typeof(bool))
                {
                    bool val = bool.TryParse(element.stringValue, out bool res) && res;
                    element.stringValue = EditorGUI.Toggle(rect, fields[i].Name, val).ToString();
                }
                else
                {
                    // Default to text field for strings or unknown types
                    element.stringValue = EditorGUI.TextField(rect, fields[i].Name, element.stringValue);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        string scriptName = property.FindPropertyRelative("EffectScript").stringValue;
        var fields = AssetManager.GetFieldsForType(scriptName);
        int fieldCount = (fields != null) ? fields.Length : 0;
        return 18 + (fieldCount * 20) + 5;
    }
}