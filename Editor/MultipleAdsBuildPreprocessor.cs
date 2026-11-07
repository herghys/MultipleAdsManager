using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    public class MultipleAdsBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidateTestAdIdsBeforeBuild(report.summary.platform);
        }

        public static bool ValidateTestAdIdsBeforeBuild(BuildTarget platform, bool promptDialog = true)
        {
            var missingPlacements = GetMissingProductionPlacements(platform);
            var activeTestPlacements = GetActiveTestAdPlacements();

            if (activeTestPlacements.Count > 0)
            {
                Debug.Log($"[MultipleAdsBuildPreprocessor] Note: {activeTestPlacements.Count} ad unit(s) have 'Use Test Ad ID' enabled in Editor. Test ads are strictly restricted to #UNITY_EDITOR and production IDs are enforced for this build ({platform}).");
            }

            if (missingPlacements.Count == 0) return true;

            var sb = new StringBuilder();
            sb.AppendLine("The following Ad Unit(s) do not have a production Ad ID configured for this build platform:");
            foreach (var item in missingPlacements)
            {
                sb.AppendLine($"• {item}");
            }
            sb.AppendLine("\nTest Ads are strictly restricted to #UNITY_EDITOR and will NOT run in this build.");

            string warningMessage = sb.ToString();
            Debug.LogWarning($"[MultipleAdsBuildPreprocessor] {warningMessage}");

            if (promptDialog && !Application.isBatchMode)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Warning: Missing Production Ad IDs",
                    $"{warningMessage}\nTarget Platform: {platform}\n\n" +
                    "If you continue, these ad units will not be able to serve live ads on this platform.\n\n" +
                    "Do you want to continue with missing production IDs, or Cancel Build?",
                    "Continue Anyway",
                    "Cancel Build"
                );

                if (!proceed)
                {
                    throw new BuildFailedException("[MultipleAds] Build cancelled by developer: Production Ad IDs were missing.");
                }
            }

            return true;
        }

        public static List<string> GetMissingProductionPlacements(BuildTarget platform)
        {
            bool isIos = platform == BuildTarget.iOS;
            var result = new List<string>();
            var mainConfig = MultipleAdsConfigGenerator.GetConfig();
            if (mainConfig != null && mainConfig.AdEntries != null)
            {
                foreach (var entry in mainConfig.AdEntries)
                {
                    if (entry == null || entry.ProviderAdIds == null) continue;
                    foreach (var p in entry.ProviderAdIds)
                    {
                        if (p != null && p.NetworkType != AdNetworkType.Mock)
                        {
                            bool hasProdId = isIos ? !string.IsNullOrWhiteSpace(p.IosAdId) : !string.IsNullOrWhiteSpace(p.AndroidAdId);
                            if (!hasProdId)
                            {
                                result.Add($"[{entry.AdType}] '{entry.AdName}' (Placement: '{entry.AdPlacement}') -> {p.NetworkType} (missing {(isIos ? "iOS" : "Android")} production ID)");
                            }
                        }
                    }
                }
            }
            return result;
        }

        public static List<string> GetActiveTestAdPlacements()
        {
            var result = new List<string>();
            var mainConfig = MultipleAdsConfigGenerator.GetConfig();
            if (mainConfig != null && mainConfig.AdEntries != null)
            {
                foreach (var entry in mainConfig.AdEntries)
                {
                    if (entry == null || entry.ProviderAdIds == null) continue;
                    foreach (var p in entry.ProviderAdIds)
                    {
                        if (p != null && p.UseTestAdId)
                        {
                            result.Add($"[{entry.AdType}] '{entry.AdName}' (Placement: '{entry.AdPlacement}') -> {p.NetworkType}");
                        }
                    }
                }
            }
            return result;
        }
    }
}