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
            var activeTestPlacements = GetActiveTestAdPlacements();
            if (activeTestPlacements.Count == 0) return true;

            var sb = new StringBuilder();
            sb.AppendLine("The following Ad Unit(s) have 'Use Test Ad ID' enabled:");
            foreach (var item in activeTestPlacements)
            {
                sb.AppendLine($"• {item}");
            }

            string warningMessage = sb.ToString();
            Debug.LogWarning($"[MultipleAdsBuildPreprocessor] {warningMessage}");

            if (promptDialog && !Application.isBatchMode)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Warning: Test Ad IDs Are Active!",
                    $"{warningMessage}\nTarget Platform: {platform}\n\n" +
                    "If you are building a PRODUCTION release, you should CANCEL the build and switch to Production Ad IDs.\n\n" +
                    "Do you want to continue with Test Ad IDs, or Cancel Build?",
                    "Continue with Test Ads",
                    "Cancel Build"
                );

                if (!proceed)
                {
                    throw new BuildFailedException("[MultipleAds] Build cancelled by developer: Test Ad IDs were still active.");
                }
            }

            return true;
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
