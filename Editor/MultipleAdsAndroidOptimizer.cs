using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    public class MultipleAdsAndroidOptimizer : IPreprocessBuildWithReport
    {
        public int callbackOrder => -50;

        private const string MainTemplatePath = "Assets/Plugins/Android/mainTemplate.gradle";
        private const string ProguardPath = "Assets/Plugins/Android/proguard-user.txt";
        private const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string LevelPlayEditorDir = "Assets/LevelPlay/Editor";

        public const string ProguardStartMarker = "# --- MultipleAds ProGuard Start ---";
        public const string ProguardEndMarker = "# --- MultipleAds ProGuard End ---";

        public const string ManifestStartMarker = "<!-- MultipleAds Manifest Merger Start -->";
        public const string ManifestEndMarker = "<!-- MultipleAds Manifest Merger End -->";

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android)
            {
                Debug.Log("[MultipleAdsAndroidOptimizer] Running automated pre-build Android size, ProGuard & Manifest appender...");
                OptimizeAll(showDialog: false);
            }
        }

        public static MultipleAdsConfig GetConfig()
        {
            return MultipleAdsConfigGenerator.GetConfig();
        }

        public static void OptimizeAll(bool showDialog = false)
        {
            var defineList = new HashSet<string>(MultipleAdsBuildTargetCompat.GetDefinesList(BuildTargetGroup.Android));

            bool hasGoogle = defineList.Contains("GOOGLE_ADS");
            bool hasLevelPlay = defineList.Contains("LEVELPLAY_ADS");
            bool hasAppLovin = defineList.Contains("APPLOVIN_ADS");
            bool hasVungle = defineList.Contains("VUNGLE_ADS");
            bool hasUnityAds = defineList.Contains("UNITY_ADS");

            MultipleAdsConfig config = GetConfig();
            bool targetChildren = config != null && config.TargetAdsForChildren;

            int disabledXmlCount = SyncAdapterXmlFiles(hasGoogle, hasLevelPlay, hasAppLovin, hasUnityAds);
            int strippedGradleDeps = CleanMainTemplateGradle(hasGoogle, hasLevelPlay, hasAppLovin, hasUnityAds, hasVungle);
            AppendProguardRules(defineList, config);
            AppendAndroidManifest(config);

            AssetDatabase.Refresh();

            string reportMsg = $"MultipleAds Android Optimization complete:\n" +
                               $"• Active Defines: {(hasGoogle ? "GoogleAds " : "")}{(hasLevelPlay ? "LevelPlay " : "")}{(hasAppLovin ? "AppLovin " : "")}{(hasVungle ? "Vungle " : "")}\n" +
                               $"• Inactive Adapter XMLs toggled: {disabledXmlCount}\n" +
                               $"• Inactive Gradle Dependencies stripped: {strippedGradleDeps}\n" +
                               $"• ProGuard / R8 Rules appended to {ProguardPath} using text template\n" +
                               $"• Manifest Merger rules appended to {ManifestPath} (TargetChildren: {targetChildren})";

            Debug.Log($"[MultipleAdsAndroidOptimizer] {reportMsg}");

            if (showDialog)
            {
                EditorUtility.DisplayDialog("MultipleAds Android Optimization", reportMsg, "OK");
            }
        }

        public static int SyncAdapterXmlFiles(bool hasGoogle, bool hasLevelPlay, bool hasAppLovin, bool hasUnityAds)
        {
            int modifiedCount = 0;

            if (!Directory.Exists(LevelPlayEditorDir)) return modifiedCount;

            modifiedCount += SetXmlFileState("ISAdMobAdapterDependencies.xml", hasLevelPlay && hasGoogle);
            modifiedCount += SetXmlFileState("ISAppLovinAdapterDependencies.xml", hasLevelPlay && hasAppLovin);
            modifiedCount += SetXmlFileState("ISUnityAdsAdapterDependencies.xml", hasLevelPlay && hasUnityAds);
            modifiedCount += SetXmlFileState("IronSourceSDKDependencies.xml", hasLevelPlay);

            return modifiedCount;
        }

        private static int SetXmlFileState(string baseXmlName, bool shouldBeEnabled)
        {
            string enabledPath = Path.Combine(LevelPlayEditorDir, baseXmlName).Replace('\\', '/');
            string disabledPath = enabledPath + ".disabled";

            if (shouldBeEnabled)
            {
                if (File.Exists(disabledPath) && !File.Exists(enabledPath))
                {
                    File.Move(disabledPath, enabledPath);
                    string metaDisabled = disabledPath + ".meta";
                    string metaEnabled = enabledPath + ".meta";
                    if (File.Exists(metaDisabled)) File.Move(metaDisabled, metaEnabled);
                    Debug.Log($"[MultipleAdsAndroidOptimizer] Enabled adapter XML: {baseXmlName}");
                    return 1;
                }
            }
            else
            {
                int removed = 0;
                if (File.Exists(enabledPath))
                {
                    File.Delete(enabledPath);
                    string meta = enabledPath + ".meta";
                    if (File.Exists(meta)) File.Delete(meta);
                    Debug.Log($"[MultipleAdsAndroidOptimizer] Completely removed disabled adapter XML: {baseXmlName}");
                    removed = 1;
                }
                if (File.Exists(disabledPath))
                {
                    File.Delete(disabledPath);
                    string metaDisabled = disabledPath + ".meta";
                    if (File.Exists(metaDisabled)) File.Delete(metaDisabled);
                    removed = 1;
                }
                return removed;
            }

            return 0;
        }

        public static int CleanMainTemplateGradle(bool hasGoogle, bool hasLevelPlay, bool hasAppLovin, bool hasUnityAds, bool hasVungle)
        {
            if (!File.Exists(MainTemplatePath)) return 0;

            string content = File.ReadAllText(MainTemplatePath);
            int strippedCount = 0;

            string startTag = "// Android Resolver Dependencies Start";
            string endTag = "// Android Resolver Dependencies End";

            int startIndex = content.IndexOf(startTag, StringComparison.Ordinal);
            int endIndex = content.IndexOf(endTag, StringComparison.Ordinal);

            if (startIndex >= 0 && endIndex > startIndex)
            {
                string header = content.Substring(0, startIndex + startTag.Length);
                string depBlock = content.Substring(startIndex + startTag.Length, endIndex - (startIndex + startTag.Length));
                string footer = content.Substring(endIndex);

                var lines = depBlock.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var keptLines = new List<string>();

                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed))
                    {
                        keptLines.Add(line);
                        continue;
                    }

                    bool shouldRemove = false;

                    // AppLovin adapter
                    if (!hasAppLovin && (trimmed.Contains("applovin-adapter") || trimmed.Contains("com.applovin:")))
                    {
                        shouldRemove = true;
                    }

                    // UnityAds adapter
                    if (!hasUnityAds && (trimmed.Contains("unityads-adapter") || trimmed.Contains("com.unity3d.ads:unity-ads")))
                    {
                        shouldRemove = true;
                    }

                    // AdMob adapter
                    if ((!hasGoogle || !hasLevelPlay) && trimmed.Contains("admob-adapter"))
                    {
                        shouldRemove = true;
                    }

                    // Google Mobile Ads standalone SDK
                    if (!hasGoogle && (trimmed.Contains("play-services-ads:") || trimmed.Contains("user-messaging-platform")))
                    {
                        shouldRemove = true;
                    }

                    // LevelPlay mediation SDK
                    if (!hasLevelPlay && (trimmed.Contains("mediation-sdk") || trimmed.Contains("play-services-ads-identifier")))
                    {
                        shouldRemove = true;
                    }

                    if (shouldRemove)
                    {
                        strippedCount++;
                        Debug.Log($"[MultipleAdsAndroidOptimizer] Stripped unused dependency from mainTemplate.gradle: {trimmed}");
                    }
                    else
                    {
                        keptLines.Add(line);
                    }
                }

                string newDepBlock = string.Join("\n", keptLines);
                content = header + newDepBlock + footer;
                File.WriteAllText(MainTemplatePath, content);
            }

            return strippedCount;
        }

        #region Appender - Separated ProGuard & Manifest Text Templates

        public static string LoadTemplateFile(string directory, string fileName, string fallback)
        {
            string primaryPath = Path.Combine(directory, fileName).Replace('\\', '/');
            if (File.Exists(primaryPath))
            {
                return File.ReadAllText(primaryPath);
            }

            string subDir = directory.Contains("Proguard") ? "Proguard" : (directory.Contains("Manifest") ? "Manifest" : "");
            if (!string.IsNullOrEmpty(subDir))
            {
                string resolved = MultipleAdsPaths.ResolveTemplatePath(subDir, fileName);
                if (File.Exists(resolved))
                {
                    return File.ReadAllText(resolved);
                }
            }

            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string[] guids = AssetDatabase.FindAssets($"{nameWithoutExt} t:TextAsset");
            if (guids != null && guids.Length > 0)
            {
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith(fileName, StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                    {
                        return File.ReadAllText(path);
                    }
                }
            }

            return fallback;
        }

        public static void AppendProguardRules(HashSet<string> defineList)
        {
            AppendProguardRules(defineList, GetConfig());
        }

        public static void AppendProguardRules(HashSet<string> defineList, MultipleAdsConfig config)
        {
            if (config == null) config = GetConfig();

            bool enableCore = config == null || config.EnableProguardCore;
            bool enableGoogle = (config == null || config.EnableProguardGoogleAds) && defineList.Contains("GOOGLE_ADS");
            bool enableLevelPlay = (config == null || config.EnableProguardLevelPlay) && defineList.Contains("LEVELPLAY_ADS");
            bool enableAppLovin = (config == null || config.EnableProguardAppLovin) && defineList.Contains("APPLOVIN_ADS");
            bool enableVungle = (config == null || config.EnableProguardVungle) && defineList.Contains("VUNGLE_ADS");

            var activeRules = new StringBuilder();

            if (enableCore)
            {
                string core = LoadTemplateFile(MultipleAdsPaths.ProguardTemplatesPath, "proguard-core.txt", GetFallbackCoreProguard());
                if (!string.IsNullOrWhiteSpace(core))
                {
                    activeRules.AppendLine(core.Trim());
                    activeRules.AppendLine();
                }
            }

            if (enableGoogle)
            {
                string gRules = LoadTemplateFile(MultipleAdsPaths.ProguardTemplatesPath, "proguard-googleads.txt", GetFallbackGoogleAdsProguard());
                if (!string.IsNullOrWhiteSpace(gRules))
                {
                    activeRules.AppendLine(gRules.Trim());
                    activeRules.AppendLine();
                }
            }

            if (enableLevelPlay)
            {
                string lpRules = LoadTemplateFile(MultipleAdsPaths.ProguardTemplatesPath, "proguard-levelplay.txt", GetFallbackLevelPlayProguard());
                if (!string.IsNullOrWhiteSpace(lpRules))
                {
                    activeRules.AppendLine(lpRules.Trim());
                    activeRules.AppendLine();
                }
            }

            if (enableAppLovin)
            {
                string alRules = LoadTemplateFile(MultipleAdsPaths.ProguardTemplatesPath, "proguard-applovin.txt", GetFallbackAppLovinProguard());
                if (!string.IsNullOrWhiteSpace(alRules))
                {
                    activeRules.AppendLine(alRules.Trim());
                    activeRules.AppendLine();
                }
            }

            if (enableVungle)
            {
                string vRules = LoadTemplateFile(MultipleAdsPaths.ProguardTemplatesPath, "proguard-vungle.txt", GetFallbackVungleProguard());
                if (!string.IsNullOrWhiteSpace(vRules))
                {
                    activeRules.AppendLine(vRules.Trim());
                    activeRules.AppendLine();
                }
            }

            var blockBuilder = new StringBuilder();
            blockBuilder.AppendLine(ProguardStartMarker);
            blockBuilder.AppendLine("# Automatically appended by MultipleAds before build. Do not edit between these markers.");
            if (activeRules.Length > 0)
            {
                blockBuilder.Append(activeRules.ToString().Trim());
                blockBuilder.AppendLine();
            }
            blockBuilder.AppendLine(ProguardEndMarker);
            string blockContent = blockBuilder.ToString();

            EnsureAndroidPluginsFolderExists();

            if (File.Exists(ProguardPath))
            {
                string existingContent = File.ReadAllText(ProguardPath);
                int startIndex = existingContent.IndexOf(ProguardStartMarker, StringComparison.Ordinal);
                int endIndex = existingContent.IndexOf(ProguardEndMarker, StringComparison.Ordinal);

                if (startIndex >= 0 && endIndex > startIndex)
                {
                    endIndex += ProguardEndMarker.Length;
                    string updated = existingContent.Substring(0, startIndex) + blockContent + existingContent.Substring(endIndex);
                    File.WriteAllText(ProguardPath, updated);
                    Debug.Log($"[MultipleAdsAndroidOptimizer] Updated MultipleAds section in existing {ProguardPath}.");
                }
                else
                {
                    string separator = existingContent.EndsWith("\n") || existingContent.EndsWith("\r") ? "\n" : "\n\n";
                    File.WriteAllText(ProguardPath, existingContent + separator + blockContent);
                    Debug.Log($"[MultipleAdsAndroidOptimizer] Appended MultipleAds rules to existing {ProguardPath}.");
                }
            }
            else
            {
                File.WriteAllText(ProguardPath, blockContent);
                Debug.Log($"[MultipleAdsAndroidOptimizer] Created {ProguardPath} with MultipleAds template rules.");
            }
        }

        public static void AppendAndroidManifest(bool targetAdsForChildren)
        {
            var config = GetConfig();
            if (config != null)
            {
                AppendAndroidManifest(config);
            }
            else
            {
                AppendAndroidManifestInternal(true, true, true, true, targetAdsForChildren);
            }
        }

        public static void AppendAndroidManifest(MultipleAdsConfig config = null)
        {
            if (config == null) config = GetConfig();

            bool stripFine = config == null || config.StripFineLocation;
            bool stripCoarse = config == null || config.StripCoarseLocation;
            bool stripPhone = config == null || config.StripPhoneState;
            bool stripStorage = config == null || config.StripExternalStorage;
            bool stripAdId = config != null && (config.StripAdvertisingId || config.TargetAdsForChildren);

            AppendAndroidManifestInternal(stripFine, stripCoarse, stripPhone, stripStorage, stripAdId);
        }

        private static void AppendAndroidManifestInternal(bool stripFine, bool stripCoarse, bool stripPhone, bool stripStorage, bool stripAdId)
        {
            var activeTags = new StringBuilder();

            if (stripFine)
            {
                string fineTag = LoadTemplateFile(MultipleAdsPaths.ManifestTemplatesPath, "manifest-strip-fine-location.txt", GetFallbackFineLocationManifest());
                activeTags.AppendLine(fineTag.Trim());
            }

            if (stripCoarse)
            {
                string coarseTag = LoadTemplateFile(MultipleAdsPaths.ManifestTemplatesPath, "manifest-strip-coarse-location.txt", GetFallbackCoarseLocationManifest());
                activeTags.AppendLine(coarseTag.Trim());
            }

            if (stripPhone)
            {
                string phoneTag = LoadTemplateFile(MultipleAdsPaths.ManifestTemplatesPath, "manifest-strip-phone-state.txt", GetFallbackPhoneStateManifest());
                activeTags.AppendLine(phoneTag.Trim());
            }

            if (stripStorage)
            {
                string storageTag = LoadTemplateFile(MultipleAdsPaths.ManifestTemplatesPath, "manifest-strip-external-storage.txt", GetFallbackExternalStorageManifest());
                activeTags.AppendLine(storageTag.Trim());
            }

            if (stripAdId)
            {
                string children = LoadTemplateFile(MultipleAdsPaths.ManifestTemplatesPath, "manifest-strip-ad-id.txt", GetFallbackChildrenManifest());
                activeTags.AppendLine(children.Trim());
            }

            var blockBuilder = new StringBuilder();
            blockBuilder.AppendLine($"    {ManifestStartMarker}");
            blockBuilder.AppendLine("    <!-- Automatically appended by MultipleAds before build. Do not edit between these markers. -->");
            var lines = activeTags.ToString().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    blockBuilder.AppendLine($"    {line.Trim()}");
                }
            }
            blockBuilder.AppendLine($"    {ManifestEndMarker}");
            string blockContent = blockBuilder.ToString();

            EnsureAndroidPluginsFolderExists();

            if (File.Exists(ManifestPath))
            {
                string existingXml = File.ReadAllText(ManifestPath);

                if (!existingXml.Contains("xmlns:tools=\"http://schemas.android.com/tools\""))
                {
                    int manifestTagIdx = existingXml.IndexOf("<manifest", StringComparison.OrdinalIgnoreCase);
                    if (manifestTagIdx >= 0)
                    {
                        int insertPos = manifestTagIdx + "<manifest".Length;
                        existingXml = existingXml.Insert(insertPos, "\n          xmlns:tools=\"http://schemas.android.com/tools\"");
                    }
                }

                int startIndex = existingXml.IndexOf(ManifestStartMarker, StringComparison.Ordinal);
                int endIndex = existingXml.IndexOf(ManifestEndMarker, StringComparison.Ordinal);

                if (startIndex >= 0 && endIndex > startIndex)
                {
                    endIndex += ManifestEndMarker.Length;
                    string updated = existingXml.Substring(0, startIndex).TrimEnd() + "\n" + blockContent + existingXml.Substring(endIndex).TrimStart();
                    File.WriteAllText(ManifestPath, updated);
                    Debug.Log($"[MultipleAdsAndroidOptimizer] Updated MultipleAds section in existing {ManifestPath}.");
                }
                else
                {
                    int appTagIndex = existingXml.IndexOf("<application", StringComparison.OrdinalIgnoreCase);
                    if (appTagIndex >= 0)
                    {
                        string updated = existingXml.Substring(0, appTagIndex) + blockContent + "\n    " + existingXml.Substring(appTagIndex);
                        File.WriteAllText(ManifestPath, updated);
                        Debug.Log($"[MultipleAdsAndroidOptimizer] Injected MultipleAds permission removal tags into existing {ManifestPath} before <application>.");
                    }
                    else
                    {
                        int closingManifest = existingXml.LastIndexOf("</manifest>", StringComparison.OrdinalIgnoreCase);
                        if (closingManifest >= 0)
                        {
                            string updated = existingXml.Substring(0, closingManifest) + blockContent + "\n" + existingXml.Substring(closingManifest);
                            File.WriteAllText(ManifestPath, updated);
                            Debug.Log($"[MultipleAdsAndroidOptimizer] Injected MultipleAds permission removal tags into existing {ManifestPath}.");
                        }
                    }
                }
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                sb.AppendLine("<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"");
                sb.AppendLine("          xmlns:tools=\"http://schemas.android.com/tools\"");
                sb.AppendLine("          package=\"com.unity3d.player\">");
                sb.AppendLine();
                sb.Append(blockContent);
                sb.AppendLine();
                sb.AppendLine("    <application>");
                sb.AppendLine("    </application>");
                sb.AppendLine("</manifest>");

                File.WriteAllText(ManifestPath, sb.ToString());
                Debug.Log($"[MultipleAdsAndroidOptimizer] Created {ManifestPath} with MultipleAds template rules.");
            }
        }

        public static int CopyPackageTemplatesToAssets(bool overwrite = false)
        {
            int copied = 0;
            string packageTemplatesDir = MultipleAdsPaths.PackageTemplatesPath;
            string projectTemplatesDir = MultipleAdsPaths.LegacyProjectTemplatesPath;

            if (!Directory.Exists(packageTemplatesDir)) return 0;

            string[] subDirs = new[] { "Manifest", "Proguard" };
            foreach (var sub in subDirs)
            {
                string pkgSub = Path.Combine(packageTemplatesDir, sub).Replace('\\', '/');
                string projSub = Path.Combine(projectTemplatesDir, sub).Replace('\\', '/');

                if (!Directory.Exists(pkgSub)) continue;
                if (!Directory.Exists(projSub)) Directory.CreateDirectory(projSub);

                foreach (var file in Directory.GetFiles(pkgSub, "*.txt"))
                {
                    string fileName = Path.GetFileName(file);
                    string targetFile = Path.Combine(projSub, fileName).Replace('\\', '/');
                    if (!File.Exists(targetFile) || overwrite)
                    {
                        File.Copy(file, targetFile, true);
                        copied++;
                    }
                }
            }

            if (copied > 0)
            {
                AssetDatabase.Refresh();
                Debug.Log($"[MultipleAdsAndroidOptimizer] Copied {copied} templates from package to {projectTemplatesDir}.");
            }

            return copied;
        }

        private static void EnsureAndroidPluginsFolderExists()
        {
            string androidPluginsDir = "Assets/Plugins/Android";
            if (!Directory.Exists(androidPluginsDir))
            {
                Directory.CreateDirectory(androidPluginsDir);
            }
        }

        private static string GetFallbackCoreProguard()
        {
            return @"# General Android & Unity optimization
-keepattributes *Annotation*
-keepattributes SourceFile,LineNumberTable
-keepclassmembers class * implements android.os.IInterface {
    <methods>;
}";
        }

        private static string GetFallbackGoogleAdsProguard()
        {
            return @"-keep class com.google.unity.** { *; }
-keep public class com.google.android.gms.ads.** { public *; }
-keep public class com.google.ads.** { public *; }
-dontwarn com.google.android.libraries.ads.mobile.sdk.**";
        }

        private static string GetFallbackLevelPlayProguard()
        {
            return @"-keepclassmembers class com.ironsource.sdk.controller.IronSourceWebView$JSInterface {
    public *;
}
-dontwarn com.ironsource.**
-keep class com.ironsource.mediationsdk.** { *; }
-keep class com.ironsource.lifecycle.** { *; }";
        }

        private static string GetFallbackAppLovinProguard()
        {
            return @"-dontwarn com.applovin.**
-keep class com.applovin.** { *; }";
        }

        private static string GetFallbackVungleProguard()
        {
            return @"-dontwarn com.vungle.**
-keep class com.vungle.** { *; }";
        }

        private static string GetFallbackCoreManifest()
        {
            return @"<uses-permission android:name=""android.permission.ACCESS_FINE_LOCATION"" tools:node=""remove"" />
<uses-permission android:name=""android.permission.ACCESS_COARSE_LOCATION"" tools:node=""remove"" />
<uses-permission android:name=""android.permission.READ_PHONE_STATE"" tools:node=""remove"" />
<uses-permission android:name=""android.permission.WRITE_EXTERNAL_STORAGE"" tools:node=""remove"" />";
        }

        private static string GetFallbackFineLocationManifest()
        {
            return @"<uses-permission android:name=""android.permission.ACCESS_FINE_LOCATION"" tools:node=""remove"" />";
        }

        private static string GetFallbackCoarseLocationManifest()
        {
            return @"<uses-permission android:name=""android.permission.ACCESS_COARSE_LOCATION"" tools:node=""remove"" />";
        }

        private static string GetFallbackPhoneStateManifest()
        {
            return @"<uses-permission android:name=""android.permission.READ_PHONE_STATE"" tools:node=""remove"" />";
        }

        private static string GetFallbackExternalStorageManifest()
        {
            return @"<uses-permission android:name=""android.permission.WRITE_EXTERNAL_STORAGE"" tools:node=""remove"" />";
        }

        private static string GetFallbackChildrenManifest()
        {
            return @"<uses-permission android:name=""com.google.android.gms.permission.AD_ID"" tools:node=""remove"" />";
        }

        #endregion
    }
}
