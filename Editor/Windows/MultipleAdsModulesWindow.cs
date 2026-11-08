using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    public class MultipleAdsModulesWindow : MultipleAdsEditorWindowBase
    {
        private List<ModuleUIState> moduleStates = null;

        private static GUIStyle rightBoldLabel;
        private static GUIStyle RightBoldLabel
        {
            get
            {
                if (rightBoldLabel == null)
                {
                    rightBoldLabel = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleRight
                    };
                }
                return rightBoldLabel;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            MultipleAdsModuleManager.EnsureDefaultModules();
            BuildTargetGroup target = MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup;
            moduleStates = ModuleUIState.GetStates(target, forceRescan: false);
        }

        public override void RefreshAfterScan()
        {
            base.RefreshAfterScan();
            BuildTargetGroup target = MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup;
            moduleStates = ModuleUIState.GetStates(target, forceRescan: false);
            Repaint();
        }

        public void RescanModules(bool clearOverrides = false)
        {
            BuildTargetGroup target = MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup;
            if (clearOverrides)
            {
                ModuleUIState.ScanAll(target, clearOverrides: true);
            }
            MultipleAdsConfigGenerator.AutoScanAndCreateConfigs(forceRescan: true);
            moduleStates = ModuleUIState.ScanAll(target, clearOverrides: false);
            LoadOrCreateConfig();
            Repaint();
        }

        private void OnGUI()
        {
            if (mainConfig == null) LoadOrCreateConfig();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawSharedHeader("Modules", "Ad Network Modules", 
                "Enable or disable ad networks. Third-party SDKs can be installed automatically via UPM or imported manually.");

            EditorGUILayout.Space(4);
            DrawModulesList();

            EditorGUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        private void DrawModulesList()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Available Ad Networks (Assets/MultipleAds/Modules)", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Patch / Sync Modules", "Check and patch all installed module scripts to match the latest package interface and templates."), EditorStyles.miniButton, GUILayout.Width(130), GUILayout.Height(18)))
            {
                int patched = MultipleAdsModuleManager.CheckAndPatchInstalledModules(force: true, verbose: true);
                RescanModules();
                EditorUtility.DisplayDialog("Module Script Patch", $"Module synchronization complete.\n\nPatched/updated: {patched} module(s) to match package v{MultipleAdsUpdateManager.CurrentVersion}.", "OK");
            }

            if (GUILayout.Button(new GUIContent("Scan All", "Scan all packages and assemblies for every module"), EditorStyles.miniButton, GUILayout.Width(80), GUILayout.Height(18)))
            {
                RescanModules(clearOverrides: true);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            BuildTargetGroup target = MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup;

            moduleStates = ModuleUIState.GetStates(target, forceRescan: false);

            if (moduleStates != null)
            {
                foreach (var state in moduleStates)
                {
                    DrawModuleRow(target, state);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawModuleRow(BuildTargetGroup target, ModuleUIState state)
        {
            if (state == null || state.Definition == null) return;
            var module = state.Definition;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(module.DisplayName, EditorStyles.boldLabel);
            string symbolStr = string.IsNullOrEmpty(module.DefineSymbol) ? "Default Built-In" : module.DefineSymbol;
            EditorGUILayout.LabelField($"{module.Description} [{symbolStr}]", EditorStyles.wordWrappedMiniLabel);

            if (!string.IsNullOrEmpty(module.PackageId))
            {
                string statusText = !state.Scanned
                    ? "Not Scanned"
                    : (state.IsEffectiveInstalled ? "Installed" : "Not Installed");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"SDK: {module.PackageId} [{statusText}]", EditorStyles.miniLabel, GUILayout.Width(270));

                if (GUILayout.Button(new GUIContent("Scan", $"Scan package & assembly presence for {module.DisplayName}"), EditorStyles.miniButton, GUILayout.Width(58), GUILayout.Height(17)))
                {
                    state.ScanThisModule(target);
                    GUIUtility.ExitGUI();
                }

                EditorGUI.BeginChangeCheck();
                bool newToggleVal = EditorGUILayout.ToggleLeft(
                    new GUIContent("SDK Installed", "Manually toggle whether this SDK is considered installed. Allows forcing real SDK code or testing in safe Stub mode."),
                    state.IsEffectiveInstalled,
                    GUILayout.Width(105)
                );
                if (EditorGUI.EndChangeCheck())
                {
                    MultipleAdsPackageManager.SetSdkOverride(module.Id, newToggleVal);
                    MultipleAdsPackageManager.SyncSdkInstalledDefines(target);
                    state.SdkOverride = newToggleVal;
                    state.IsEffectiveInstalled = newToggleVal;
                }

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("Built-in Simulator (No external SDK required)", EditorStyles.miniLabel);
            }



            EditorGUILayout.EndVertical();

            GUILayout.Space(6);

            EditorGUILayout.BeginVertical(GUILayout.Width(200));

            const float actionBtnWidth = 94f;
            const float singleRowWidth = 192f; // 94 + 4 + 94

            if (module.IsDefault)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Default Module", RightBoldLabel, GUILayout.Height(20));
                GUILayout.Space(6);
                if (state.Config != null)
                {
                    if (GUILayout.Button("Open Config", EditorStyles.miniButton, GUILayout.Width(actionBtnWidth), GUILayout.Height(20)))
                    {
                        OpenConfig(state.Config);
                    }
                }
                else
                {
                    if (GUILayout.Button(new GUIContent("Add Config", $"Create missing AdsConfig for {module.DisplayName}"), EditorStyles.miniButton, GUILayout.Width(actionBtnWidth), GUILayout.Height(20)))
                    {
                        state.Config = MultipleAdsConfigGenerator.EnsureProviderConfig(module, mainConfig, saveAssets: true);
                        if (state.Config != null)
                        {
                            OpenConfig(state.Config);
                        }
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            else if (!state.IsAdded)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Not Added", RightBoldLabel, GUILayout.Height(20));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ Add", GUILayout.Width(actionBtnWidth), GUILayout.Height(22)))
                {
                    PromptAddModule(module, downloadFromRemote: false);
                    GUIUtility.ExitGUI();
                }

                GUILayout.Space(4);

                if (GUILayout.Button("Branch ▾", GUILayout.Width(actionBtnWidth), GUILayout.Height(22)))
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Add from Local Template"), false, () => PromptAddModule(module, false));
                    menu.AddItem(new GUIContent("Download from Remote Branch..."), false, () => PromptAddModule(module, true));
                    menu.ShowAsContext();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(state.HasDefine ? "Enabled" : "Disabled", RightBoldLabel, GUILayout.Height(20));
                GUILayout.Space(6);

                if (state.Config != null)
                {
                    if (GUILayout.Button("Open Config", EditorStyles.miniButton, GUILayout.Width(actionBtnWidth), GUILayout.Height(20)))
                    {
                        OpenConfig(state.Config);
                    }
                }
                else
                {
                    if (GUILayout.Button(new GUIContent("Add Config", $"Create missing AdsConfig for {module.DisplayName}"), EditorStyles.miniButton, GUILayout.Width(actionBtnWidth), GUILayout.Height(20)))
                    {
                        state.Config = MultipleAdsConfigGenerator.EnsureProviderConfig(module, mainConfig, saveAssets: true);
                        if (state.Config != null)
                        {
                            OpenConfig(state.Config);
                        }
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                string btnText = state.HasDefine ? "Disable" : "Enable";
                if (GUILayout.Button(btnText, GUILayout.Width(actionBtnWidth), GUILayout.Height(22)))
                {
                    if (state.HasDefine)
                    {
                        RemoveDefine(target, module.DefineSymbol);
                        state.HasDefine = false;
                    }
                    else
                    {
                        bool isSdkInstalled = state.IsEffectiveInstalled;
                        if (!string.IsNullOrEmpty(module.PackageId) && !isSdkInstalled)
                        {
                            int choice = EditorUtility.DisplayDialogComplex(
                                $"Enable {module.DisplayName}",
                                $"The SDK for {module.DisplayName} ({module.PackageId}) is not yet detected in your project.\n\n" +
                                $"You can still enable {module.DisplayName} to configure its ad units and weights in mediation—it will safely run in Stub mode until the SDK is installed.\n\n" +
                                $"Would you like to auto-install the SDK via UPM now, or enable anyway in Stub mode?",
                                "Auto Install SDK & Enable",
                                "Enable in Stub Mode",
                                "Cancel"
                            );

                            if (choice == 0)
                            {
                                MultipleAdsPackageManager.InstallSdkForModule(module, target);
                                state.HasDefine = true;
                            }
                            else if (choice == 1)
                            {
                                AddDefine(target, module.DefineSymbol);
                                state.HasDefine = true;
                            }
                            else
                            {
                                return;
                            }
                        }
                        else
                        {
                            AddDefine(target, module.DefineSymbol);
                            state.HasDefine = true;
                        }
                    }
                    MultipleAdsSetupWindow.SyncConfigsWithActiveDefines(mainConfig, true);
                    RescanModules();
                    GUIUtility.ExitGUI();
                }

                GUILayout.Space(4);

                if (GUILayout.Button("Remove", GUILayout.Width(actionBtnWidth), GUILayout.Height(22)))
                {
                    string sdkWarning = !string.IsNullOrEmpty(module.PackageId) ? $"\nIts SDK package ({module.PackageId}) will also be removed from Packages/manifest.json." : "";
                    if (EditorUtility.DisplayDialog($"Remove {module.DisplayName} Module",
                        $"Are you sure you want to remove {module.DisplayName} from Assets/MultipleAds/Modules/?\nIts scripting define and active config will also be removed.{sdkWarning}",
                        "Remove", "Cancel"))
                    {
                        MultipleAdsModuleManager.RemoveModule(module.Id, target);
                        MultipleAdsSetupWindow.SyncConfigsWithActiveDefines(mainConfig, true);
                        RescanModules();
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(module.PackageId) && !state.IsEffectiveInstalled)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Install SDK", $"Install package {module.PackageId} via UPM"), EditorStyles.miniButton, GUILayout.Width(singleRowWidth), GUILayout.Height(20)))
                    {
                        MultipleAdsPackageManager.InstallSdkForModule(module, target);
                        RescanModules();
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void PromptAddModule(AdsModuleDefinition module, bool downloadFromRemote)
        {
            if (module == null) return;

            if (string.IsNullOrEmpty(module.PackageId))
            {
                MultipleAdsModuleManager.AddModule(module.Id, autoInstallSdk: false, downloadFromRemote, success =>
                {
                    MultipleAdsSetupWindow.SyncConfigsWithActiveDefines(mainConfig, true);
                    RescanModules();
                });
                return;
            }

            int choice = EditorUtility.DisplayDialogComplex(
                $"Add {module.DisplayName} Module",
                $"How would you like to install the SDK for {module.DisplayName} ({module.PackageId})?\n\n" +
                $"• Auto Install (UPM): Configures 'Herghys' Scoped Registry & adds '{module.PackageId}': '{module.PackageVersion}' to Packages/manifest.json via UPM.\n\n" +
                $"• Manual Install: Adds module template only. You will import the SDK manually (.unitypackage, git URL, or Asset Store).\n\n" +
                $"The module will run in safe stub mode until the SDK is detected in the project.",
                "Auto Install (UPM)",
                "Manual Install",
                "Cancel"
            );

            if (choice == 0)
            {
                MultipleAdsModuleManager.AddModule(module.Id, autoInstallSdk: true, downloadFromRemote, success =>
                {
                    MultipleAdsSetupWindow.SyncConfigsWithActiveDefines(mainConfig, true);
                    RescanModules();
                });
            }
            else if (choice == 1)
            {
                MultipleAdsModuleManager.AddModule(module.Id, autoInstallSdk: false, downloadFromRemote, success =>
                {
                    MultipleAdsSetupWindow.SyncConfigsWithActiveDefines(mainConfig, true);
                    RescanModules();
                });
            }
        }

        private void OpenConfig(BaseAdConfig config)
        {
            if (config != null)
            {
                EditorGUIUtility.PingObject(config);
                Selection.activeObject = config;
            }
        }

        private static void AddDefine(BuildTargetGroup target, string symbol)
        {
            MultipleAdsBuildTargetCompat.AddDefine(target, symbol);
        }

        private static void RemoveDefine(BuildTargetGroup target, string symbol)
        {
            MultipleAdsBuildTargetCompat.RemoveDefine(target, symbol);
        }
    }
}
