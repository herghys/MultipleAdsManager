using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;

namespace Herghys.MultipleAdsManager.Editor
{
    public static class MultipleAdsUpdateManager
    {
        public const string PackageName = "com.herghys.multipleadsmanager";
        public const string RepositoryGitUrl = "https://github.com/herghys/MultipleAdsManager.git";
        public const string RepositoryWebUrl = "https://github.com/herghys/MultipleAdsManager";
        public const string RemotePackageJsonUrl = "https://raw.githubusercontent.com/herghys/MultipleAdsManager/main/package.json";
        public const string RemoteChangelogUrl = "https://raw.githubusercontent.com/herghys/MultipleAdsManager/main/CHANGELOG.md";

        private const string PrefKeyLatestVersion = "MultipleAds_LatestRemoteVersion";
        private const string PrefKeyLastCheckTime = "MultipleAds_LastUpdateCheckTime";
        private const string PrefKeyDismissedVersion = "MultipleAds_DismissedUpdateVersion";
        public const string PrefKeyAutoCheck = "MultipleAds_AutoCheckUpdates";

        private static string cachedCurrentVersion = null;
        private static bool isChecking = false;
        private static AddRequest activeAddRequest = null;

        public static string CurrentVersion
        {
            get
            {
                if (string.IsNullOrEmpty(cachedCurrentVersion))
                {
                    cachedCurrentVersion = ReadPackageVersionFromDisk();
                }
                return cachedCurrentVersion;
            }
        }

        public static string LatestVersion
        {
            get => EditorPrefs.GetString(PrefKeyLatestVersion, "");
            private set => EditorPrefs.SetString(PrefKeyLatestVersion, value ?? "");
        }

        public static bool IsChecking => isChecking;

        public static bool IsUpdateAvailable
        {
            get
            {
                string latest = LatestVersion;
                if (string.IsNullOrEmpty(latest)) return false;

                string dismissed = EditorPrefs.GetString(PrefKeyDismissedVersion, "");
                if (string.Equals(dismissed, latest, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return CompareVersions(latest, CurrentVersion) > 0;
            }
        }

        public static bool AutoCheckEnabled
        {
            get => EditorPrefs.GetBool(PrefKeyAutoCheck, true);
            set => EditorPrefs.SetBool(PrefKeyAutoCheck, value);
        }

        public static event Action OnUpdateStateChanged;

        public static string ReadPackageVersionFromDisk()
        {
            try
            {
                string[] possiblePaths = new[]
                {
                    "Packages/com.herghys.multipleadsmanager/package.json",
                    Path.Combine(Application.dataPath, "Plugins/Herghys/MultipleAds/package.json"),
                    Path.Combine(Application.dataPath, "MultipleAds/package.json")
                };

                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        string json = File.ReadAllText(path);
                        if (MultipleAdsPackageManager.MiniJson.Parse(json) is Dictionary<string, object> dict &&
                            dict.TryGetValue("version", out var verObj) && verObj != null)
                        {
                            return verObj.ToString().Trim();
                        }

                        var match = Regex.Match(json, @"""version""\s*:\s*""([^""]+)""");
                        if (match.Success)
                        {
                            return match.Groups[1].Value.Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MultipleAdsUpdateManager] Failed to read package.json version: {ex.Message}");
            }

            return "1.0.4"; // Safe fallback
        }

        public static int CompareVersions(string vA, string vB)
        {
            if (string.IsNullOrEmpty(vA) && string.IsNullOrEmpty(vB)) return 0;
            if (string.IsNullOrEmpty(vA)) return -1;
            if (string.IsNullOrEmpty(vB)) return 1;

            vA = vA.TrimStart('v', 'V');
            vB = vB.TrimStart('v', 'V');

            string[] partsA = vA.Split(new[] { '.', '-', '+' }, StringSplitOptions.RemoveEmptyEntries);
            string[] partsB = vB.Split(new[] { '.', '-', '+' }, StringSplitOptions.RemoveEmptyEntries);

            int maxLen = Math.Max(partsA.Length, partsB.Length);
            for (int i = 0; i < maxLen; i++)
            {
                int numA = 0;
                int numB = 0;

                if (i < partsA.Length) int.TryParse(partsA[i], out numA);
                if (i < partsB.Length) int.TryParse(partsB[i], out numB);

                if (numA != numB)
                {
                    return numA.CompareTo(numB);
                }
            }

            return 0;
        }

        public static void CheckForUpdates(bool silent = false, Action<bool, string> onComplete = null)
        {
            if (isChecking)
            {
                return;
            }

            // In silent auto-check mode, respect auto-check setting and throttle to once per 10 minutes
            if (silent)
            {
                if (!AutoCheckEnabled)
                {
                    onComplete?.Invoke(false, "Auto-check disabled");
                    return;
                }

                string lastCheckStr = EditorPrefs.GetString(PrefKeyLastCheckTime, "");
                if (DateTime.TryParse(lastCheckStr, out DateTime lastCheck))
                {
                    if ((DateTime.UtcNow - lastCheck).TotalMinutes < 10)
                    {
                        onComplete?.Invoke(IsUpdateAvailable, LatestVersion);
                        return;
                    }
                }
            }

            isChecking = true;
            EditorPrefs.SetString(PrefKeyLastCheckTime, DateTime.UtcNow.ToString("O"));

            var req = UnityWebRequest.Get(RemotePackageJsonUrl);
            req.timeout = 10;
            var asyncOp = req.SendWebRequest();

            asyncOp.completed += op =>
            {
                isChecking = false;

                try
                {
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        if (!silent)
                        {
                            EditorUtility.DisplayDialog(
                                "Check for Updates",
                                $"Failed to check for updates.\n\nError: {req.error}\n\nPlease check your internet connection or visit the GitHub repository.",
                                "OK"
                            );
                        }
                        onComplete?.Invoke(false, req.error);
                        return;
                    }

                    string jsonText = req.downloadHandler.text;
                    string remoteVer = null;

                    if (MultipleAdsPackageManager.MiniJson.Parse(jsonText) is Dictionary<string, object> dict &&
                        dict.TryGetValue("version", out var verObj) && verObj != null)
                    {
                        remoteVer = verObj.ToString().Trim();
                    }
                    else
                    {
                        var match = Regex.Match(jsonText, @"""version""\s*:\s*""([^""]+)""");
                        if (match.Success)
                        {
                            remoteVer = match.Groups[1].Value.Trim();
                        }
                    }

                    if (string.IsNullOrEmpty(remoteVer))
                    {
                        if (!silent)
                        {
                            EditorUtility.DisplayDialog("Check for Updates", "Unable to parse version from remote repository.", "OK");
                        }
                        onComplete?.Invoke(false, "Parse error");
                        return;
                    }

                    LatestVersion = remoteVer;
                    string localVer = CurrentVersion;
                    bool updateAvail = CompareVersions(remoteVer, localVer) > 0;

                    OnUpdateStateChanged?.Invoke();
                    NotifyOpenWindowsToRepaint();

                    if (updateAvail)
                    {
                        if (!silent)
                        {
                            int choice = EditorUtility.DisplayDialogComplex(
                                "MultipleAds Update Available",
                                $"A new version of Multiple Ads Manager is available!\n\n" +
                                $"• Installed: v{localVer}\n" +
                                $"• Latest:    v{remoteVer}\n\n" +
                                $"Updating will refresh the package and automatically patch all installed module scripts to match the latest interfaces.",
                                "Update Package",
                                "View on GitHub",
                                "Later"
                            );

                            if (choice == 0)
                            {
                                UpdatePackage();
                            }
                            else if (choice == 1)
                            {
                                Application.OpenURL($"{RepositoryWebUrl}/releases");
                            }
                        }
                    }
                    else
                    {
                        if (!silent)
                        {
                            EditorUtility.DisplayDialog(
                                "Check for Updates",
                                $"Multiple Ads Manager is up to date!\n\nInstalled version: v{localVer}",
                                "OK"
                            );
                        }
                    }

                    onComplete?.Invoke(updateAvail, remoteVer);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MultipleAdsUpdateManager] Error processing update check: {ex.Message}");
                    onComplete?.Invoke(false, ex.Message);
                }
                finally
                {
                    req.Dispose();
                }
            };
        }

        public static void DismissUpdateBanner()
        {
            if (!string.IsNullOrEmpty(LatestVersion))
            {
                EditorPrefs.SetString(PrefKeyDismissedVersion, LatestVersion);
            }
            OnUpdateStateChanged?.Invoke();
            NotifyOpenWindowsToRepaint();
        }

        public static void OpenChangelog()
        {
            Application.OpenURL($"{RepositoryWebUrl}/blob/main/CHANGELOG.md");
        }

        public static void UpdatePackage()
        {
            // 1. Check if git lock exists in Packages/packages-lock.json and unlock it
            UnlockGitPackageInLockFile();

            // 2. If it's a git package or embedded, initiate update
            bool isGitOrManifest = MultipleAdsPackageManager.IsPackageInManifest(PackageName);

            Debug.Log($"[MultipleAdsUpdateManager] Updating '{PackageName}' to latest from {RepositoryGitUrl}...");
            EditorUtility.DisplayProgressBar("Updating Multiple Ads Manager", "Requesting package update via Unity Package Manager...", 0.3f);

            try
            {
                activeAddRequest = Client.Add(RepositoryGitUrl);
                EditorApplication.update += MonitorAddRequest;
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[MultipleAdsUpdateManager] Failed to start package update: {ex.Message}");
                bool openWeb = EditorUtility.DisplayDialog(
                    "Update Failed",
                    $"Failed to start package update via Unity Package Manager: {ex.Message}\n\nWould you like to open the GitHub repository to view download options?",
                    "Open GitHub",
                    "Cancel"
                );
                if (openWeb)
                {
                    Application.OpenURL(RepositoryWebUrl);
                }
            }
        }

        private static void MonitorAddRequest()
        {
            if (activeAddRequest == null)
            {
                EditorApplication.update -= MonitorAddRequest;
                EditorUtility.ClearProgressBar();
                return;
            }

            if (activeAddRequest.IsCompleted)
            {
                EditorApplication.update -= MonitorAddRequest;
                EditorUtility.ClearProgressBar();

                if (activeAddRequest.Status == StatusCode.Success)
                {
                    Debug.Log($"[MultipleAdsUpdateManager] Successfully updated package '{PackageName}'.");

                    // Immediately trigger module patching
                    MultipleAdsModuleManager.CheckAndPatchInstalledModules(force: true, verbose: true);

                    EditorUtility.DisplayDialog(
                        "Update Completed",
                        $"Multiple Ads Manager has been updated.\n\nInstalled module scripts in Assets/MultipleAds/Modules/ are being verified and synchronized.",
                        "OK"
                    );

                    cachedCurrentVersion = null;
                    EditorPrefs.DeleteKey(PrefKeyDismissedVersion);
                    Client.Resolve();
                    AssetDatabase.Refresh();
                }
                else
                {
                    string errorMsg = activeAddRequest.Error != null ? activeAddRequest.Error.message : "Unknown error";
                    Debug.LogError($"[MultipleAdsUpdateManager] Package update failed: {errorMsg}");

                    int choice = EditorUtility.DisplayDialogComplex(
                        "Package Update",
                        $"Unity Package Manager update returned: {errorMsg}\n\nIf this project uses a local clone or embedded package in 'Packages/{PackageName}', you can pull the latest changes using Git.",
                        "Open GitHub",
                        "Patch Installed Modules Now",
                        "Cancel"
                    );

                    if (choice == 0)
                    {
                        Application.OpenURL(RepositoryWebUrl);
                    }
                    else if (choice == 1)
                    {
                        MultipleAdsModuleManager.CheckAndPatchInstalledModules(force: true, verbose: true);
                    }
                }

                activeAddRequest = null;
            }
        }

        private static void UnlockGitPackageInLockFile()
        {
            try
            {
                string lockPath = MultipleAdsPackageManager.PackagesLockPath;
                if (!File.Exists(lockPath)) return;

                string text = File.ReadAllText(lockPath);
                if (MultipleAdsPackageManager.MiniJson.Parse(text) is Dictionary<string, object> root &&
                    root.TryGetValue("dependencies", out var depsObj) &&
                    depsObj is Dictionary<string, object> deps)
                {
                    if (deps.ContainsKey(PackageName))
                    {
                        deps.Remove(PackageName);
                        string serialized = MultipleAdsPackageManager.MiniJson.Serialize(root);
                        File.WriteAllText(lockPath, serialized);
                        Debug.Log($"[MultipleAdsUpdateManager] Cleared '{PackageName}' from packages-lock.json to force pull latest Git commit.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MultipleAdsUpdateManager] Could not edit packages-lock.json: {ex.Message}");
            }
        }

        public static void NotifyOpenWindowsToRepaint()
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            if (windows != null)
            {
                foreach (var win in windows)
                {
                    if (win is MultipleAdsEditorWindowBase || win is MultipleAdsSetupWindow)
                    {
                        win.Repaint();
                    }
                }
            }
        }
    }
}
