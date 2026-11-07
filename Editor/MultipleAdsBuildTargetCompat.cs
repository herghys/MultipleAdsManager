using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.Build;
#endif

namespace Herghys.MultipleAdsManager.Editor
{
    public static class MultipleAdsBuildTargetCompat
    {
        public static BuildTargetGroup SelectedBuildTargetGroup => EditorUserBuildSettings.selectedBuildTargetGroup;

        public static string GetScriptingDefineSymbols(BuildTargetGroup targetGroup)
        {
            if (targetGroup == BuildTargetGroup.Unknown)
                targetGroup = SelectedBuildTargetGroup;

#if UNITY_2021_2_OR_NEWER
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            return PlayerSettings.GetScriptingDefineSymbols(namedTarget);
#else
            #pragma warning disable CS0618
            return PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            #pragma warning restore CS0618
#endif
        }

        public static List<string> GetDefinesList(BuildTargetGroup targetGroup)
        {
            string defines = GetScriptingDefineSymbols(targetGroup);
            if (string.IsNullOrEmpty(defines)) return new List<string>();
            return defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(d => d.Trim())
                          .Where(d => !string.IsNullOrEmpty(d))
                          .ToList();
        }

        public static void SetScriptingDefineSymbols(BuildTargetGroup targetGroup, IEnumerable<string> defines)
        {
            if (targetGroup == BuildTargetGroup.Unknown)
                targetGroup = SelectedBuildTargetGroup;

            string definesString = defines != null ? string.Join(";", defines.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct()) : "";
#if UNITY_2021_2_OR_NEWER
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            PlayerSettings.SetScriptingDefineSymbols(namedTarget, definesString);
#else
            #pragma warning disable CS0618
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, definesString);
            #pragma warning restore CS0618
#endif
        }

        public static bool HasDefine(BuildTargetGroup targetGroup, string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return false;
            return GetDefinesList(targetGroup).Contains(symbol);
        }

        public static bool AddDefine(BuildTargetGroup targetGroup, string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return false;
            var list = GetDefinesList(targetGroup);
            if (!list.Contains(symbol))
            {
                list.Add(symbol);
                SetScriptingDefineSymbols(targetGroup, list);
                return true;
            }
            return false;
        }

        public static bool RemoveDefine(BuildTargetGroup targetGroup, string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return false;
            var list = GetDefinesList(targetGroup);
            if (list.Remove(symbol))
            {
                SetScriptingDefineSymbols(targetGroup, list);
                return true;
            }
            return false;
        }

        public const string TestAdsDefine = "TEST_ADS";

        public static bool IsTestAdsDefineActive()
        {
            return HasDefine(SelectedBuildTargetGroup, TestAdsDefine);
        }

        public static bool SetTestAdsDefine(bool enable)
        {
            var targetGroups = new[]
            {
                SelectedBuildTargetGroup,
                BuildTargetGroup.Android,
                BuildTargetGroup.iOS,
                BuildTargetGroup.Standalone
            }.Distinct();

            bool anyChanged = false;
            foreach (var group in targetGroups)
            {
                if (group == BuildTargetGroup.Unknown) continue;
                var list = GetDefinesList(group);
                bool has = list.Contains(TestAdsDefine);
                if (enable && !has)
                {
                    list.Add(TestAdsDefine);
                    SetScriptingDefineSymbols(group, list);
                    anyChanged = true;
                }
                else if (!enable && has)
                {
                    list.Remove(TestAdsDefine);
                    SetScriptingDefineSymbols(group, list);
                    anyChanged = true;
                }
            }
            return anyChanged;
        }

        public static string GetApplicationIdentifier(BuildTargetGroup targetGroup)
        {
            if (targetGroup == BuildTargetGroup.Unknown)
                targetGroup = SelectedBuildTargetGroup;

#if UNITY_2021_2_OR_NEWER
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            return PlayerSettings.GetApplicationIdentifier(namedTarget);
#else
            #pragma warning disable CS0618
            return PlayerSettings.GetApplicationIdentifier(targetGroup);
            #pragma warning restore CS0618
#endif
        }

        public static void SetApplicationIdentifier(BuildTargetGroup targetGroup, string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return;
            if (targetGroup == BuildTargetGroup.Unknown)
                targetGroup = SelectedBuildTargetGroup;

#if UNITY_2021_2_OR_NEWER
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            PlayerSettings.SetApplicationIdentifier(namedTarget, identifier);
#else
            #pragma warning disable CS0618
            PlayerSettings.SetApplicationIdentifier(targetGroup, identifier);
            #pragma warning restore CS0618
#endif
        }
    }
}
