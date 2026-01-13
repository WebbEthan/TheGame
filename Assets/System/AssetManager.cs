using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class AssetManager
{
    private struct EffectTypeData
    {
        public Type type;
        public FieldInfo[] fields; // Store field info for quick access
    }

    private static Dictionary<string, EffectTypeData> scriptableEffects = new Dictionary<string, EffectTypeData>();

    public static void CacheEffectData()
    {

        if (Application.isPlaying) ThreadManager.MainLog.LogItem("Starting Effect Cache Process");
        var types = Assembly.GetAssembly(typeof(Effect)).GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(Effect)));

        foreach (var type in types)
        {
            if (!scriptableEffects.ContainsKey(type.Name))
            {
                var allFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(f => f.GetCustomAttribute<NonSerializedAttribute>() == null)
                    .ToArray();

                scriptableEffects.Add(type.Name, new EffectTypeData
                {
                    type = type,
                    fields = allFields
                });

                // Logging the discovery of the effect type and its parameter count
                if (Application.isPlaying) ThreadManager.MainLog.LogItem($"Cached Effect Type: {type.Name} with {allFields.Length} parameters.");
            }
        }
        if (Application.isPlaying) ThreadManager.MainLog.LogItem("Effect Cache Initialization Complete.");
    }

    public static Effect GenerateEffect(string effectName, string[] parameterValues)
    {
        CacheEffectData();

        if (scriptableEffects.TryGetValue(effectName, out EffectTypeData data))
        {
            // Create the unique instance in memory
            Effect instance = (Effect)ScriptableObject.CreateInstance(data.type);

            ThreadManager.ObjectLog.LogItem($"Instantiated new {effectName} object.");

            // Deserialize values into fields of this specific instance
            for (int i = 0; i < data.fields.Length && i < parameterValues.Length; i++)
            {
                try
                {
                    object convertedValue = Convert.ChangeType(parameterValues[i], data.fields[i].FieldType);

                    // The FieldInfo 'data.fields[i]' is the tool; 'instance' is the specific target.
                    data.fields[i].SetValue(instance, convertedValue);

                    ThreadManager.ObjectLog.LogItem($"Set {data.fields[i].Name} to {convertedValue} on {effectName} instance.");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Failed to parse {parameterValues[i]} for {data.fields[i].Name}: {e.Message}");
                }
            }
            return instance;
        }

        UnityEngine.Debug.LogError($"Effect {effectName} not found in cache.");
        return null;
    }
#if UNITY_EDITOR
    public static string[] GetEffectNames() { CacheEffectData(); return scriptableEffects.Keys.ToArray(); }

    public static FieldInfo[] GetFieldsForType(string typeName)
    {
        CacheEffectData(); // Ensure we are cached before looking up
        return scriptableEffects.TryGetValue(typeName, out var data) ? data.fields : null;
    }

    // Add this method to resolve the compiler error
    public static Type GetEffectType(string typeName)
    {
        CacheEffectData();
        return scriptableEffects.TryGetValue(typeName, out var data) ? data.type : null;
    }
#endif
}
public static class ObjectPool
{ 

}
