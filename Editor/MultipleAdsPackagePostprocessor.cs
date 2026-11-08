using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Herghys.MultipleAdsManager.Editor
{
    public class MultipleAdsPackagePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool packageOrManifestChanged = false;

            if (importedAssets != null)
            {
                foreach (var path in importedAssets)
                {
                    if (string.IsNullOrEmpty(path)) continue;
                    string norm = path.Replace('\\', '/');

                    if (norm.Contains("com.herghys.multipleadsmanager") ||
                        norm.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase) ||
                        norm.EndsWith("packages-lock.json", StringComparison.OrdinalIgnoreCase))
                    {
                        packageOrManifestChanged = true;
                        break;
                    }
                }
            }

            if (packageOrManifestChanged)
            {
                MultipleAdsModuleManager.CheckAndPatchInstalledModules(force: false, verbose: true);
            }
        }
    }
}
