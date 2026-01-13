using UnityEngine;

public class GameManager : MonoBehaviour
{
    private void Awake()
    {
        // Start Threads and logs
        ThreadManager.StartThreadManager();
        // Deserialize Assets and Cache Reflection Based Info
        AssetManager.CacheEffectData();
        // Load Preferences
        SaveSystems.LoadSettings();
    }

    private void Update()
    {
        ThreadManager.MainThreadDispatchUpdate();
    }
}
