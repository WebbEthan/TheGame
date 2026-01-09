#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class GitStatusWindow : EditorWindow
{
    private Vector2 scrollPos;
    private string commitMessage = "";
    private List<string> changedFiles = new List<string>();
    private bool stageAll = true;

    [MenuItem("Git/Status Window")]
    public static void ShowWindow()
    {
        GetWindow<GitStatusWindow>("Git Commit");
    }

    private void OnEnable()
    {
        RefreshStatus();
    }

    private void OnGUI()
    {
        GUILayout.Label("Git Status", EditorStyles.boldLabel);

        if (GUILayout.Button("Refresh"))
        {
            RefreshStatus();
        }

        GUILayout.Space(5);

        // Show list of changed/untracked files
        scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
        if (changedFiles.Count == 0)
        {
            GUILayout.Label("No changes detected.");
        }
        else
        {
            foreach (var file in changedFiles)
            {
                GUILayout.Label(file);
            }
        }
        GUILayout.EndScrollView();

        GUILayout.Space(10);

        // Commit message
        GUILayout.Label("Commit Message", EditorStyles.boldLabel);
        commitMessage = EditorGUILayout.TextField(commitMessage);

        GUILayout.Space(5);

        // Stage all checkbox
        stageAll = EditorGUILayout.Toggle("Stage all changes", stageAll);

        GUILayout.Space(10);

        // Commit buttons
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Commit"))
        {
            if (string.IsNullOrEmpty(commitMessage))
            {
                EditorUtility.DisplayDialog("GitMan", "Commit message cannot be empty!", "OK");
            }
            else
            {
                CommitChanges(false);
            }
        }

        if (GUILayout.Button("Commit & Push"))
        {
            if (string.IsNullOrEmpty(commitMessage))
            {
                EditorUtility.DisplayDialog("GitMan", "Commit message cannot be empty!", "OK");
            }
            else
            {
                CommitChanges(true);
            }
        }
        GUILayout.EndHorizontal();
    }

    private void RefreshStatus()
    {
        changedFiles.Clear();
        string status = GitMan.RunGit("status --porcelain");
        if (!string.IsNullOrEmpty(status))
        {
            var lines = status.Split('\n');
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    changedFiles.Add(line.Trim());
            }
        }
    }

    private void CommitChanges(bool push)
    {
        if (stageAll)
        {
            GitMan.RunGit("add -A");
        }
        else
        {
            foreach (var file in changedFiles)
            {
                string path = file.Substring(3); // status format: XY filename
                GitMan.RunGit($"add \"{path}\"");
            }
        }

        GitMan.RunGit($"commit -m \"{commitMessage}\"");

        if (push)
        {
            GitMan.RunGit("push");
            Debug.Log("GitMan: Push complete");
        }

        commitMessage = "";
        RefreshStatus();
    }
}
#endif
