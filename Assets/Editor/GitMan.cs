#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Diagnostics;
using System.IO;
using Debug = UnityEngine.Debug;

public static class GitMan
{
    [InitializeOnLoadMethod]
    private static void StartRemoteWatcher()
    {
        _lastRemoteCheckTime = EditorApplication.timeSinceStartup;

        EditorApplication.update += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (_autoSyncInProgress) return;

            double now = EditorApplication.timeSinceStartup;
            if (now - _lastRemoteCheckTime < REMOTE_CHECK_INTERVAL_MINUTES * 60)
                return;

            _lastRemoteCheckTime = now;
            CheckRemoteAndPull();
        };
    }



    private static void CheckRemoteAndPull()
    {
        if (!EnsureRepoURL()) return;

        RunGit("fetch");

        string branch = RunGit("rev-parse --abbrev-ref HEAD").Trim();
        string count = RunGit($"rev-list HEAD..origin/{branch} --count").Trim();

        if (int.TryParse(count, out int commits) && commits > 0)
        {
            Debug.Log($"GitMan: {commits} new remote commits found");
            AutoPullMerge();
        }
    }
    private static void AutoPullMerge()
    {
        if (_autoSyncInProgress) return;
        _autoSyncInProgress = true;

        RunGit("pull --rebase");

        // Auto stage merged scenes
        RunGit("add *.unity");
        RunGit("add *.meta");

        _autoSyncInProgress = false;
    }
    private static void AutoPullMergePush()
    {
        if (!EnsureLogin()) return;

        _autoSyncInProgress = true;

        RunGit("fetch");

        string branch = RunGit("rev-parse --abbrev-ref HEAD").Trim();
        string count = RunGit($"rev-list HEAD..origin/{branch} --count").Trim();

        if (int.TryParse(count, out int commits) && commits > 0)
        {
            RunGit("pull --rebase");
        }

        // Stage only scene-related files
        RunGit("add *.unity");
        RunGit("add *.meta");

        string status = RunGit("status --porcelain");
        if (!string.IsNullOrEmpty(status))
        {
            string msg = $"Auto-merge scene {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            RunGit($"commit -m \"{msg}\"");
            RunGit("push");

            Debug.Log("GitMan: Scene merged & pushed");
        }

        _autoSyncInProgress = false;
    }

    public static void OnSceneSaved()
    {
        if (_autoSyncInProgress) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        Debug.Log("GitMan: Scene saved — syncing...");

        EditorApplication.delayCall += () =>
        {
            AutoPullMergePush();
        };
    }





    private const double REMOTE_CHECK_INTERVAL_MINUTES = 3.0;
    private static double _lastRemoteCheckTime;
    private static bool _autoSyncInProgress;
    class GitSceneSaveHook : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (string p in paths)
            {
                if (p.EndsWith(".unity"))
                {
                    GitMan.OnSceneSaved();
                    break;
                }
            }
            return paths;
        }
    }




    // ==============================
    // CONSTANTS / STATE
    // ==============================
    private const string RepoUrlKey = "GitMan_RepoURL";

    private static string gitUser;
    private static string gitToken;

    private static string ProjectRoot =>
        Directory.GetParent(Application.dataPath).FullName;

    private static string RepoURL
    {
        get => EditorPrefs.GetString(RepoUrlKey, string.Empty);
        set => EditorPrefs.SetString(RepoUrlKey, value);
    }

    // ==============================
    // GIT PROCESS
    // ==============================
    private static string RunGit(string args)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = ProjectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrEmpty(gitUser))
        {
            psi.Environment["GIT_USERNAME"] = gitUser;
            psi.Environment["GIT_PASSWORD"] = gitToken;
            psi.Environment["GIT_ASKPASS"] = "echo";
        }

        using (Process p = Process.Start(psi))
        {
            string output = p.StandardOutput.ReadToEnd();
            string error = p.StandardError.ReadToEnd();
            p.WaitForExit();

            if (!string.IsNullOrEmpty(error))
                Debug.LogError(error);

            return output;
        }
    }

    // ==============================
    // REPO URL HANDLING
    // ==============================
    private static bool EnsureRepoURL()
    {
        if (!string.IsNullOrEmpty(RepoURL))
            return true;

        EditorUtility.DisplayDialog(
            "Git Repository URL Not Set",
            "Please set the repository URL first.",
            "OK");

        ShowRepoUrlDialog();
        return false;
    }

    private static void ConfigureRemote(string url)
    {
        string remotes = RunGit("remote");

        if (remotes.Contains("origin"))
            RunGit($"remote set-url origin {url}");
        else
            RunGit($"remote add origin {url}");

        Debug.Log("GitMan: origin configured");
    }

    // ==============================
    // LOGIN HANDLING
    // ==============================
    private static bool EnsureLogin()
    {
        if (!string.IsNullOrEmpty(gitUser) &&
            !string.IsNullOrEmpty(gitToken))
            return true;

        GitLoginWindow.ShowWindow();
        return false;
    }

    // ==============================
    // GIT COMMANDS
    // ==============================
    public static void Commit()
    {
        if (!EnsureRepoURL()) return;

        string msg = $"Unity Commit {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        RunGit("add -A");
        RunGit($"commit -m \"{msg}\"");

        Debug.Log("Git: Commit complete");
    }

    public static void CommitAndPush()
    {
        if (!EnsureRepoURL()) return;
        if (!EnsureLogin()) return;

        Commit();
        RunGit("push");
        Debug.Log("Git: Push complete");
    }

    public static void Pull()
    {
        if (!EnsureRepoURL()) return;
        if (!EnsureLogin()) return;

        RunGit("pull --rebase");
        Debug.Log("Git: Pull complete");
    }

    public static void Restore()
    {
        if (!EnsureRepoURL()) return;

        if (!EditorUtility.DisplayDialog(
            "WARNING",
            "This will discard ALL local changes.\nThis cannot be undone.",
            "Restore",
            "Cancel")) return;

        RunGit("reset --hard");
        RunGit("clean -fd");
        Debug.Log("Git: Repository restored");
    }

    // ==============================
    // MENU ITEMS
    // ==============================
    [MenuItem("Git/Set Repository URL")]
    private static void MenuSetRepoURL() => ShowRepoUrlDialog();

    [MenuItem("Git/Login")]
    private static void MenuLogin() => GitLoginWindow.ShowWindow();

    [MenuItem("Git/Commit")]
    private static void MenuCommit() => Commit();

    [MenuItem("Git/Commit & Push")]
    private static void MenuCommitPush() => CommitAndPush();

    [MenuItem("Git/Pull")]
    private static void MenuPull() => Pull();

    [MenuItem("Git/Restore (Hard Reset)")]
    private static void MenuRestore() => Restore();

    // ==============================
    // REPO URL WINDOW
    // ==============================
    private static void ShowRepoUrlDialog()
    {
        GitRepoUrlWindow.ShowWindow();
    }

    private class GitRepoUrlWindow : EditorWindow
    {
        private string repoUrl;

        public static void ShowWindow()
        {
            var win = GetWindow<GitRepoUrlWindow>("Git Repository URL");
            win.minSize = new Vector2(420, 90);
        }

        private void OnEnable()
        {
            repoUrl = RepoURL;
        }

        private void OnGUI()
        {
            GUILayout.Label("Remote Repository URL", EditorStyles.boldLabel);
            repoUrl = EditorGUILayout.TextField("Origin URL", repoUrl);

            GUILayout.Space(10);

            if (GUILayout.Button("Save"))
            {
                RepoURL = repoUrl;
                ConfigureRemote(repoUrl);
                Close();
            }
        }
    }

    // ==============================
    // LOGIN WINDOW
    // ==============================
    private class GitLoginWindow : EditorWindow
    {
        private string username;
        private string token;

        public static void ShowWindow()
        {
            var win = GetWindow<GitLoginWindow>("Git Login");
            win.minSize = new Vector2(320, 120);
        }

        private void OnGUI()
        {
            GUILayout.Label("Git Credentials (Session Only)", EditorStyles.boldLabel);

            username = EditorGUILayout.TextField("Username", username);
            token = EditorGUILayout.PasswordField("Token / Password", token);

            GUILayout.Space(10);

            if (GUILayout.Button("Login"))
            {
                gitUser = username;
                gitToken = token;
                Debug.Log("GitMan: Credentials set for this session.");
                Close();
            }
        }
    }
}
#endif
