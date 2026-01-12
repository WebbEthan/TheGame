using UnityEngine;
using System.IO;
public class Preferences
{

}
public class SavedData
{

}
public static class SaveSystems
{
    public static SavedData StoredData;
    public static void SaveLocalData()
    {
        SaveJSONObject(StoredData, Application.persistentDataPath + "/Settings.json");
    }
    public static void LoadSavedData()
    {
        StoredData = GetJSONObject<SavedData>(Application.persistentDataPath + "/Settings.json");

        // Ensure _settings is never null to avoid NullReferenceExceptions
        if (StoredData == null) StoredData = new SavedData();
    }
    private static Preferences _settings;
    public static Preferences Settings
    {
        get
        {
            if (_settings == null) LoadSettings();
            return _settings;
        }
        set
        {
            _settings = value;
            SaveSettings();
        }
    }

    public static void SaveSettings()
    {
        SaveJSONObject(_settings, Application.persistentDataPath + "/Settings.json");
    }

    public static void LoadSettings()
    {
        _settings = GetJSONObject<Preferences>(Application.persistentDataPath + "/Settings.json");

        // Ensure _settings is never null to avoid NullReferenceExceptions
        if (_settings == null) _settings = new Preferences();
    }

    private static void SaveJSONObject<T>(T data, string filepath)
    {
        try
        {
            // Get the directory path from the full filepath
            string directory = Path.GetDirectoryName(filepath);

            // If the folder doesn't exist, create it
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(filepath, json);

            Debug.Log($"Settings saved successfully to: {filepath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save settings: {e.Message}");
        }
    }

    private static T GetJSONObject<T>(string filepath)
    {
        if (!File.Exists(filepath))
        {
            return default;
        }

        string json = File.ReadAllText(filepath);
        return JsonUtility.FromJson<T>(json);
    }
}
