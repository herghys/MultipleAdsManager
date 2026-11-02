using System.IO;

namespace Herghys.MultipleAdsManager.Runtime
{
    public static class MultipleAdsPaths
    {
        public const string MainConfigsPath = "Assets/MultipleAds/Configs";
        public const string AdsConfigPath = "Assets/MultipleAds/AdsConfig";
        public const string ModulesPath = "Assets/MultipleAds/Modules";

        public const string TemplatesPath = "Packages/com.herghys.multipleadsmanager/Editor/Templates";
        public const string ProguardTemplatesPath = "Packages/com.herghys.multipleadsmanager/Editor/Templates/Proguard";
        public const string ManifestTemplatesPath = "Packages/com.herghys.multipleadsmanager/Editor/Templates/Manifest";

        public const string PackageTemplatesPath = TemplatesPath;
        public const string PackageProguardTemplatesPath = ProguardTemplatesPath;
        public const string PackageManifestTemplatesPath = ManifestTemplatesPath;

        public const string LegacyProjectTemplatesPath = "Assets/MultipleAds/Templates";

        public const string PluginResourcesConfigPath = "Packages/com.herghys.multipleadsmanager/Runtime/Resources/Config";
        public const string LegacyPluginResourcesConfigPath = "Assets/Plugins/Herghys/MultipleAds/Resources/Config";
        public const string ProjectResourcesConfigPath = "Assets/MultipleAds/Resources/Config";

        public const string MainConfigName = "MultipleAdsManager Config";
        public const string LegacyMainConfigName = "MultipleAdsConfig";

        public static string GetMainConfigDirectory() => MainConfigsPath;
        public static string GetAdsConfigDirectory() => AdsConfigPath;
        public static string GetModulesDirectory() => ModulesPath;
        public static string GetTemplatesDirectory() => TemplatesPath;
        public static string GetProguardTemplatesDirectory() => ProguardTemplatesPath;
        public static string GetManifestTemplatesDirectory() => ManifestTemplatesPath;

        public static string GetPackageTemplatesDirectory() => PackageTemplatesPath;
        public static string GetPackageProguardTemplatesDirectory() => PackageProguardTemplatesPath;
        public static string GetPackageManifestTemplatesDirectory() => PackageManifestTemplatesPath;

        public static string ResolveTemplatePath(string subDir, string fileName)
        {
            // 1. Primary: Package Editor Templates
            string packageEditorPath = $"{TemplatesPath}/{subDir}/{fileName}";
            if (File.Exists(packageEditorPath)) return packageEditorPath;

            // 2. Package Root Templates
            string packageRootPath = $"Packages/com.herghys.multipleadsmanager/Templates/{subDir}/{fileName}";
            if (File.Exists(packageRootPath)) return packageRootPath;

            // 3. Legacy project override (if user had custom templates in Assets)
            string legacyProjectPath = $"{LegacyProjectTemplatesPath}/{subDir}/{fileName}";
            if (File.Exists(legacyProjectPath)) return legacyProjectPath;

            return packageEditorPath;
        }

        public static string GetModuleDirectory(string moduleName)
        {
            return $"{ModulesPath}/{moduleName}";
        }

        public static string GetMainConfigAssetPath()
        {
            string primaryPath = $"{MainConfigsPath}/{MainConfigName}.asset";
            if (File.Exists(primaryPath))
                return primaryPath;

            string legacyInConfigs = $"{MainConfigsPath}/{LegacyMainConfigName}.asset";
            if (File.Exists(legacyInConfigs))
                return legacyInConfigs;

            return primaryPath;
        }

        public static string GetProviderConfigAssetPath(string providerConfigName)
        {
            // First check Resources/Config (standard for MockAppConfig)
            string pluginResPath = $"{PluginResourcesConfigPath}/{providerConfigName}.asset";
            if (File.Exists(pluginResPath))
                return pluginResPath;

            string legacyPluginResPath = $"{LegacyPluginResourcesConfigPath}/{providerConfigName}.asset";
            if (File.Exists(legacyPluginResPath))
                return legacyPluginResPath;

            string projectResPath = $"{ProjectResourcesConfigPath}/{providerConfigName}.asset";
            if (File.Exists(projectResPath))
                return projectResPath;

            // Handle MockAdsConfig <-> MockAppConfig aliases
            if (providerConfigName == "MockAdsConfig" || providerConfigName == "MockAppConfig")
            {
                string altName = providerConfigName == "MockAdsConfig" ? "MockAppConfig" : "MockAdsConfig";
                string altPluginRes = $"{PluginResourcesConfigPath}/{altName}.asset";
                if (File.Exists(altPluginRes)) return altPluginRes;
                string altLegacyRes = $"{LegacyPluginResourcesConfigPath}/{altName}.asset";
                if (File.Exists(altLegacyRes)) return altLegacyRes;
                string altProjectRes = $"{ProjectResourcesConfigPath}/{altName}.asset";
                if (File.Exists(altProjectRes)) return altProjectRes;
            }

            string primaryPath = $"{AdsConfigPath}/{providerConfigName}.asset";
            if (File.Exists(primaryPath))
                return primaryPath;

            string inConfigsPath = $"{MainConfigsPath}/{providerConfigName}.asset";
            if (File.Exists(inConfigsPath))
                return inConfigsPath;

            return primaryPath;
        }

        public static string GetConfigAssetPath(string configName)
        {
            if (configName == MainConfigName || configName == LegacyMainConfigName)
            {
                return GetMainConfigAssetPath();
            }
            return GetProviderConfigAssetPath(configName);
        }
    }
}
