using UnityEngine;

public class GameManager : MonoBehaviour
{
    private void Awake()
    {
        ThreadManager.StartThreadManager();
        SaveSystems.LoadSettings();
    }
}
