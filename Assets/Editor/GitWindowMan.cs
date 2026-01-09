using UnityEditor;
using UnityEngine;

public class GitWindowMan : EditorWindow
{
    private string commitMessage = "";
    private System.Action onCommitComplete;

    public static void ShowWindow(System.Action onComplete = null)
    {
        var win = GetWindow<GitWindowMan>("Git Commit");
        win.minSize = new Vector2(400, 100);
        win.commitMessage = "";
        win.onCommitComplete = onComplete;
    }

    private void OnGUI()
    {
        GUILayout.Label("Enter commit message", EditorStyles.boldLabel);
        commitMessage = EditorGUILayout.TextField("Message", commitMessage);

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Commit"))
        {
            GitMan.RunCommitWithMessage(commitMessage);
            Close();
            onCommitComplete?.Invoke(); // Notify callback
        }

        if (GUILayout.Button("Cancel"))
        {
            Close();
        }
        GUILayout.EndHorizontal();
    }
}
