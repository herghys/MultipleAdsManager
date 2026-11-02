using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    public class ScopedRegistryDefinition
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public List<string> Scopes { get; set; } = new List<string>();
    }

    public static class MultipleAdsPackageManager
    {
        public static string ManifestPath => Path.GetFullPath("Packages/manifest.json").Replace('\\', '/');
        public static string PackagesLockPath => Path.GetFullPath("Packages/packages-lock.json").Replace('\\', '/');

        private static readonly Dictionary<string, bool> _typePresentCache = new Dictionary<string, bool>();
        private static HashSet<string> _resolvedPackagesCache = null;

        public static void Scan()
        {
            _resolvedPackagesCache = new HashSet<string>();
            _typePresentCache.Clear();

            if (File.Exists(PackagesLockPath))
            {
                try
                {
                    string text = File.ReadAllText(PackagesLockPath);
                    if (MiniJson.Parse(text) is Dictionary<string, object> root &&
                        root.TryGetValue("dependencies", out var depsObj) &&
                        depsObj is Dictionary<string, object> deps)
                    {
                        foreach (var key in deps.Keys)
                        {
                            _resolvedPackagesCache.Add(key);
                        }
                    }
                }
                catch { }
            }

            if (File.Exists(ManifestPath))
            {
                try
                {
                    string text = File.ReadAllText(ManifestPath);
                    if (MiniJson.Parse(text) is Dictionary<string, object> root &&
                        root.TryGetValue("dependencies", out var depsObj) &&
                        depsObj is Dictionary<string, object> deps)
                    {
                        foreach (var key in deps.Keys)
                        {
                            _resolvedPackagesCache.Add(key);
                        }
                    }
                }
                catch { }
            }
        }

        public static void InvalidatePackageCache()
        {
            _resolvedPackagesCache = null;
            _typePresentCache.Clear();
        }

        public static bool IsSdkTypePresent(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return false;

            if (_typePresentCache.TryGetValue(typeName, out bool present))
            {
                return present;
            }

            Type t = Type.GetType(typeName);
            if (t != null)
            {
                _typePresentCache[typeName] = true;
                return true;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetType(typeName, false, true) != null)
                    {
                        _typePresentCache[typeName] = true;
                        return true;
                    }
                }
                catch { }
            }

            _typePresentCache[typeName] = false;
            return false;
        }

        private const string SdkOverridePrefix = "MultipleAds_SdkOverride_";

        public static bool? GetSdkOverride(string moduleId)
        {
            if (string.IsNullOrEmpty(moduleId)) return null;
            string key = SdkOverridePrefix + moduleId;
            if (!EditorPrefs.HasKey(key)) return null;
            return EditorPrefs.GetBool(key);
        }

        public static void SetSdkOverride(string moduleId, bool state)
        {
            if (string.IsNullOrEmpty(moduleId)) return;
            string key = SdkOverridePrefix + moduleId;
            EditorPrefs.SetBool(key, state);
            Debug.Log($"[MultipleAdsPackageManager] Set SDK override for {moduleId}: {(state ? "Forced Installed (SDK Code Active)" : "Forced Not Installed (Stub Mode)")}.");
        }

        public static void ClearSdkOverride(string moduleId)
        {
            if (string.IsNullOrEmpty(moduleId)) return;
            string key = SdkOverridePrefix + moduleId;
            if (EditorPrefs.HasKey(key))
            {
                EditorPrefs.DeleteKey(key);
                Debug.Log($"[MultipleAdsPackageManager] Cleared SDK override for {moduleId}. Returned to auto-detection.");
            }
        }

        public static bool IsSdkInstalledAuto(AdsModuleDefinition module)
        {
            if (module == null) return false;
            if (string.IsNullOrEmpty(module.PackageId) && string.IsNullOrEmpty(module.SdkTypeCheck)) return true;

            if (!string.IsNullOrEmpty(module.SdkTypeCheck) && IsSdkTypePresent(module.SdkTypeCheck))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(module.PackageId) && IsPackageResolved(module.PackageId))
            {
                return true;
            }

            return false;
        }

        public static bool IsSdkInstalled(AdsModuleDefinition module)
        {
            if (module == null) return false;

            bool? manualOverride = GetSdkOverride(module.Id);
            if (manualOverride.HasValue)
            {
                return manualOverride.Value;
            }

            return IsSdkInstalledAuto(module);
        }

        public static bool SyncSdkInstalledDefines(BuildTargetGroup targetGroup = BuildTargetGroup.Unknown)
        {
            if (targetGroup == BuildTargetGroup.Unknown)
                targetGroup = MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup;

            var defineList = MultipleAdsBuildTargetCompat.GetDefinesList(targetGroup);
            bool changed = false;

            foreach (var module in MultipleAdsModuleManager.AllModules)
            {
                if (string.IsNullOrEmpty(module.DefineSymbol) || string.IsNullOrEmpty(module.SdkInstalledSymbol))
                    continue;

                bool moduleEnabled = defineList.Contains(module.DefineSymbol);
                bool sdkPresent = IsSdkInstalled(module);
                bool shouldHaveInstalledDefine = moduleEnabled && sdkPresent;

                if (shouldHaveInstalledDefine && !defineList.Contains(module.SdkInstalledSymbol))
                {
                    defineList.Add(module.SdkInstalledSymbol);
                    changed = true;
                    Debug.Log($"[MultipleAdsPackageManager] Detected active SDK for {module.DisplayName}. Added define '{module.SdkInstalledSymbol}'.");
                }
                else if (!shouldHaveInstalledDefine && defineList.Contains(module.SdkInstalledSymbol))
                {
                    defineList.Remove(module.SdkInstalledSymbol);
                    changed = true;
                    Debug.Log($"[MultipleAdsPackageManager] SDK not detected or module disabled for {module.DisplayName}. Removed define '{module.SdkInstalledSymbol}'.");
                }
            }

            if (changed)
            {
                MultipleAdsBuildTargetCompat.SetScriptingDefineSymbols(targetGroup, defineList);
            }

            return changed;
        }

        public static bool IsPackageInManifest(string packageId)
        {
            if (string.IsNullOrEmpty(packageId) || !File.Exists(ManifestPath)) return false;

            try
            {
                string text = File.ReadAllText(ManifestPath);
                if (MiniJson.Parse(text) is Dictionary<string, object> root &&
                    root.TryGetValue("dependencies", out var depsObj) &&
                    depsObj is Dictionary<string, object> deps)
                {
                    return deps.ContainsKey(packageId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MultipleAdsPackageManager] Failed to check manifest for package '{packageId}': {ex.Message}");
            }

            return false;
        }

        public static bool IsPackageResolved(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return false;
            if (_resolvedPackagesCache == null)
            {
                Scan();
            }
            return _resolvedPackagesCache != null && _resolvedPackagesCache.Contains(packageId);
        }

        public static string GetInstalledVersion(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return null;

            if (File.Exists(PackagesLockPath))
            {
                try
                {
                    string text = File.ReadAllText(PackagesLockPath);
                    if (MiniJson.Parse(text) is Dictionary<string, object> root &&
                        root.TryGetValue("dependencies", out var depsObj) &&
                        depsObj is Dictionary<string, object> deps &&
                        deps.TryGetValue(packageId, out var lockEntry))
                    {
                        if (lockEntry is Dictionary<string, object> lockDict &&
                            lockDict.TryGetValue("version", out var verObj))
                        {
                            return verObj?.ToString();
                        }
                    }
                }
                catch { }
            }

            if (File.Exists(ManifestPath))
            {
                try
                {
                    string text = File.ReadAllText(ManifestPath);
                    if (MiniJson.Parse(text) is Dictionary<string, object> root &&
                        root.TryGetValue("dependencies", out var depsObj) &&
                        depsObj is Dictionary<string, object> deps &&
                        deps.TryGetValue(packageId, out var ver))
                    {
                        return ver?.ToString();
                    }
                }
                catch { }
            }

            return null;
        }

        public static bool AddPackage(string packageId, string version, ScopedRegistryDefinition scopedRegistry = null, bool resolveImmediately = true)
        {
            if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(version)) return false;
            if (!File.Exists(ManifestPath))
            {
                Debug.LogError($"[MultipleAdsPackageManager] manifest.json not found at {ManifestPath}");
                return false;
            }

            try
            {
                string text = File.ReadAllText(ManifestPath);
                var root = MiniJson.Parse(text) as Dictionary<string, object>;
                if (root == null) root = new Dictionary<string, object>();

                if (scopedRegistry != null && !string.IsNullOrEmpty(scopedRegistry.Url))
                {
                    List<object> registriesList = null;
                    if (root.TryGetValue("scopedRegistries", out var regObj) && regObj is List<object> existingList)
                    {
                        registriesList = existingList;
                    }
                    else
                    {
                        registriesList = new List<object>();
                        root["scopedRegistries"] = registriesList;
                    }

                    Dictionary<string, object> targetReg = null;
                    foreach (var item in registriesList)
                    {
                        if (item is Dictionary<string, object> rDict &&
                            rDict.TryGetValue("url", out var urlVal) &&
                            string.Equals(urlVal?.ToString()?.TrimEnd('/'), scopedRegistry.Url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                        {
                            targetReg = rDict;
                            break;
                        }
                    }

                    if (targetReg != null)
                    {
                        List<object> currentScopes = null;
                        if (targetReg.TryGetValue("scopes", out var sObj) && sObj is List<object> sList)
                        {
                            currentScopes = sList;
                        }
                        else
                        {
                            currentScopes = new List<object>();
                            targetReg["scopes"] = currentScopes;
                        }

                        if (scopedRegistry.Scopes != null)
                        {
                            foreach (var scope in scopedRegistry.Scopes)
                            {
                                if (!currentScopes.Exists(s => string.Equals(s?.ToString(), scope, StringComparison.OrdinalIgnoreCase)))
                                {
                                    currentScopes.Add(scope);
                                }
                            }
                        }
                    }
                    else
                    {
                        var newReg = new Dictionary<string, object>
                        {
                            ["name"] = string.IsNullOrEmpty(scopedRegistry.Name) ? "Herghys" : scopedRegistry.Name,
                            ["url"] = scopedRegistry.Url,
                            ["scopes"] = new List<object>(scopedRegistry.Scopes ?? new List<string>())
                        };
                        registriesList.Add(newReg);
                    }
                }

                Dictionary<string, object> depsDict = null;
                if (root.TryGetValue("dependencies", out var dObj) && dObj is Dictionary<string, object> existingDeps)
                {
                    depsDict = existingDeps;
                }
                else
                {
                    depsDict = new Dictionary<string, object>();
                    root["dependencies"] = depsDict;
                }

                depsDict[packageId] = version;

                string serialized = MiniJson.Serialize(root);
                File.WriteAllText(ManifestPath, serialized);
                InvalidatePackageCache();
                Debug.Log($"[MultipleAdsPackageManager] Successfully added '{packageId}' ({version}) to Packages/manifest.json.");

                if (resolveImmediately)
                {
                    UnityEditor.PackageManager.Client.Resolve();
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MultipleAdsPackageManager] Error adding package '{packageId}': {ex.Message}");
                return false;
            }
        }

        public static bool RemovePackage(string packageId, bool resolveImmediately = true)
        {
            if (string.IsNullOrEmpty(packageId) || !File.Exists(ManifestPath)) return false;

            try
            {
                string text = File.ReadAllText(ManifestPath);
                var root = MiniJson.Parse(text) as Dictionary<string, object>;
                if (root == null) return false;

                if (root.TryGetValue("dependencies", out var dObj) && dObj is Dictionary<string, object> depsDict)
                {
                    if (depsDict.Remove(packageId))
                    {
                        string serialized = MiniJson.Serialize(root);
                        File.WriteAllText(ManifestPath, serialized);
                        InvalidatePackageCache();
                        Debug.Log($"[MultipleAdsPackageManager] Successfully removed '{packageId}' from Packages/manifest.json.");

                        if (resolveImmediately)
                        {
                            UnityEditor.PackageManager.Client.Resolve();
                        }

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MultipleAdsPackageManager] Error removing package '{packageId}': {ex.Message}");
            }

            return false;
        }

        #region MiniJson Parser and Serializer

        internal static class MiniJson
        {
            public static object Parse(string json)
            {
                int index = 0;
                return ParseValue(json, ref index);
            }

            private static object ParseValue(string json, ref int index)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length) return null;

                char c = json[index];
                if (c == '{') return ParseObject(json, ref index);
                if (c == '[') return ParseArray(json, ref index);
                if (c == '"') return ParseString(json, ref index);
                if (c == 't' || c == 'f') return ParseBool(json, ref index);
                if (c == 'n') return ParseNull(json, ref index);
                if (char.IsDigit(c) || c == '-') return ParseNumber(json, ref index);

                throw new Exception($"Unexpected token at index {index}: {c}");
            }

            private static void SkipWhitespace(string json, ref int index)
            {
                while (index < json.Length && char.IsWhiteSpace(json[index]))
                    index++;
            }

            private static Dictionary<string, object> ParseObject(string json, ref int index)
            {
                var dict = new Dictionary<string, object>();
                index++; // skip '{'

                while (index < json.Length)
                {
                    SkipWhitespace(json, ref index);
                    if (index >= json.Length) break;
                    if (json[index] == '}')
                    {
                        index++;
                        return dict;
                    }

                    if (json[index] == ',')
                    {
                        index++;
                        continue;
                    }

                    string key = ParseString(json, ref index);
                    SkipWhitespace(json, ref index);
                    if (index < json.Length && json[index] == ':') index++;
                    object val = ParseValue(json, ref index);
                    dict[key] = val;
                }

                return dict;
            }

            private static List<object> ParseArray(string json, ref int index)
            {
                var list = new List<object>();
                index++; // skip '['

                while (index < json.Length)
                {
                    SkipWhitespace(json, ref index);
                    if (index >= json.Length) break;
                    if (json[index] == ']')
                    {
                        index++;
                        return list;
                    }

                    if (json[index] == ',')
                    {
                        index++;
                        continue;
                    }

                    object val = ParseValue(json, ref index);
                    list.Add(val);
                }

                return list;
            }

            private static string ParseString(string json, ref int index)
            {
                SkipWhitespace(json, ref index);
                if (json[index] != '"') throw new Exception($"Expected string at {index}");
                index++;

                var sb = new StringBuilder();
                while (index < json.Length)
                {
                    char c = json[index++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (index >= json.Length) break;
                        char esc = json[index++];
                        switch (esc)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (index + 4 <= json.Length)
                                {
                                    string hex = json.Substring(index, 4);
                                    index += 4;
                                    sb.Append((char)Convert.ToInt32(hex, 16));
                                }
                                break;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }

                return sb.ToString();
            }

            private static bool ParseBool(string json, ref int index)
            {
                if (json.Substring(index).StartsWith("true"))
                {
                    index += 4;
                    return true;
                }
                if (json.Substring(index).StartsWith("false"))
                {
                    index += 5;
                    return false;
                }
                throw new Exception($"Invalid boolean at index {index}");
            }

            private static object ParseNull(string json, ref int index)
            {
                if (json.Substring(index).StartsWith("null"))
                {
                    index += 4;
                    return null;
                }
                throw new Exception($"Invalid null at index {index}");
            }

            private static object ParseNumber(string json, ref int index)
            {
                int start = index;
                while (index < json.Length && (char.IsDigit(json[index]) || json[index] == '-' || json[index] == '.' || json[index] == 'e' || json[index] == 'E' || json[index] == '+'))
                    index++;
                string numStr = json.Substring(start, index - start);
                if (long.TryParse(numStr, out long l)) return l;
                if (double.TryParse(numStr, out double d)) return d;
                return numStr;
            }

            public static string Serialize(object obj, int indent = 2)
            {
                var sb = new StringBuilder();
                SerializeValue(obj, sb, 0, indent);
                return sb.ToString();
            }

            private static void SerializeValue(object obj, StringBuilder sb, int level, int indent)
            {
                if (obj == null)
                {
                    sb.Append("null");
                }
                else if (obj is string s)
                {
                    sb.Append('"');
                    foreach (char c in s)
                    {
                        switch (c)
                        {
                            case '"': sb.Append("\\\""); break;
                            case '\\': sb.Append("\\\\"); break;
                            case '\b': sb.Append("\\b"); break;
                            case '\f': sb.Append("\\f"); break;
                            case '\n': sb.Append("\\n"); break;
                            case '\r': sb.Append("\\r"); break;
                            case '\t': sb.Append("\\t"); break;
                            default:
                                if (c < 32)
                                    sb.AppendFormat("\\u{0:x4}", (int)c);
                                else
                                    sb.Append(c);
                                break;
                        }
                    }
                    sb.Append('"');
                }
                else if (obj is bool b)
                {
                    sb.Append(b ? "true" : "false");
                }
                else if (obj is Dictionary<string, object> dict)
                {
                    if (dict.Count == 0)
                    {
                        sb.Append("{}");
                        return;
                    }
                    sb.AppendLine("{");
                    int count = 0;
                    string ind = new string(' ', (level + 1) * indent);
                    foreach (var kvp in dict)
                    {
                        sb.Append(ind);
                        sb.Append('"').Append(kvp.Key).Append("\": ");
                        SerializeValue(kvp.Value, sb, level + 1, indent);
                        count++;
                        if (count < dict.Count) sb.Append(',');
                        sb.AppendLine();
                    }
                    sb.Append(new string(' ', level * indent)).Append('}');
                }
                else if (obj is List<object> list)
                {
                    if (list.Count == 0)
                    {
                        sb.Append("[]");
                        return;
                    }
                    sb.AppendLine("[");
                    int count = 0;
                    string ind = new string(' ', (level + 1) * indent);
                    foreach (var item in list)
                    {
                        sb.Append(ind);
                        SerializeValue(item, sb, level + 1, indent);
                        count++;
                        if (count < list.Count) sb.Append(',');
                        sb.AppendLine();
                    }
                    sb.Append(new string(' ', level * indent)).Append(']');
                }
                else
                {
                    sb.Append(obj.ToString());
                }
            }
        }

        #endregion
    }

    [InitializeOnLoad]
    public static class MultipleAdsSdkDefineWatcher
    {
        static MultipleAdsSdkDefineWatcher()
        {
            EditorApplication.delayCall += () =>
            {
                MultipleAdsPackageManager.SyncSdkInstalledDefines(EditorUserBuildSettings.selectedBuildTargetGroup);
            };
        }
    }
}
