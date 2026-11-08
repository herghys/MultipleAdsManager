# Changelog

All notable changes to this package will be documented in this file.

## [1.0.4] - 2026-11-08
### Added
- **Ad Open Callback Overloads**: Added dedicated `onOpened` action callback overloads across `IAdProvider`, `MultipleAdsManager`, and all provider implementations/templates (`GoogleAds`, `LevelPlay`, `UnityAds`, `AppLovin`, `Vungle`, `MockAds`):
  - `ShowInterstitial(Action onOpened, Action onClosed)`
  - `ShowInterstitial(string adUnitId, string placement, Action onOpened, Action onClosed)`
  - `ShowRewarded(Action onRewardEarned, Action onOpened, Action onClosed)`
  - `ShowRewarded(string adUnitId, string placement, Action onRewardEarned, Action onOpened, Action onClosed)`
  - `ShowRewarded(string adUnitId, string placement, Action<AdReward> onRewardEarned, Action onOpened, Action onClosed)`
- **Module Script Auto-Patching on Update**: Added automatic detection and patching of installed module provider scripts in `Assets/MultipleAds/Modules/` whenever the package updates, interface changes, or on domain reload. Outdated scripts are automatically updated to match the latest template signatures (with `.bak` backups created if modified), eliminating CS0535 compilation errors.
- **Package Update Checker ("Check for Updates")**: Introduced `MultipleAdsUpdateManager` with `Tools > MultipleAds > Check for Updates`, automatic update checks on opening any MultipleAds setup window, and a prominent in-window notification banner with "Update Package", "Changelog", and "Dismiss" controls.
- **Manual Module Repair & Sync**: Added `Tools > MultipleAds > Patch Installed Modules (Repair Scripts)` and a "Patch / Sync Modules" button in `MultipleAdsModulesWindow`.
- **Backwards Compatibility**: Preserved all original single-callback and parameterless overloads (`ShowInterstitial(Action onClosed = null)`, `ShowInterstitial(string adUnitId, string placement, Action onClosed = null)`, `ShowRewarded(Action onRewardEarned, Action onClosed = null)`, etc.) so existing code without `onOpened` remains fully supported without breaking changes.
- **`TEST_ADS` Scripting Define**: Enforced editor-only test ad IDs gated behind `#if UNITY_EDITOR && TEST_ADS`, ensuring test IDs are strictly prohibited in build artifacts.

### Fixed
- **LoadCheck Infinite Loop**: Fixed an issue where `LoadCheck` would run indefinitely and repeatedly log `[MultipleAds] LoadCheck: Missing AdsConfig for Google Mobile Ads (AdMob). Auto-creating...` when script compilation errors were present. Added `EditorUtility.scriptCompilationFailed` validation, disk existence checks before asset recreation, and rescan throttling.
- Fixed compilation error `CS0535` across all ad providers and module templates by properly implementing both legacy and `onOpened` overloads for `IAdProvider`.

## [1.0.2] - 2026-11-04
### Added
- **Unity Ads Module Support**: Added `UnityAds` (`com.unity.ads`) module definition, `UnityAdsConfig` and `UnityAdsProvider` templates (`UNITY_ADS` & `UNITY_ADS_SDK_INSTALLED`), default format test ad IDs (`Banner_Android`/`Banner_iOS`, `Interstitial_Android`/`Interstitial_iOS`, `Rewarded_Android`/`Rewarded_iOS`), and ProGuard rules (`proguard-unityads.txt`).
- **Android Optimization for Unity Ads**: Added `EnableProguardUnityAds` toggle and `UNITY_ADS` adapter/standalone SDK status in `MultipleAdsOptimizationWindow` and `MultipleAdsAndroidOptimizer`.
### Fixed
- Enforce Event Systems for mock disabled.

## [1.0.1] - 2026-11-02
### Added
- **Auto-Scan & Auto-Create Configs**: Automatically scan and create `MultipleAdsManager Config` and provider-specific `AdsConfig` assets upon installing or adding an ad provider.
- **Startup `LoadCheck` Verification**: Added one-time configuration validation on application load or editor window initialization to auto-generate missing configs without persistent background asset postprocessing overhead.
- **"Add Config" Quick Action**: Added explicit "Add Config" action button whenever an enabled ad provider is missing its configuration asset.

### Changed
- **Decoupled MockAds Config from Package**: Moved `MockAdsConfig.asset` out of package resources into the project's `Assets/MultipleAds/AdsConfig/`, keeping the package free of project assets while maintaining automatic presence checks and creation when missing.
- **Right-Aligned Module Action UI**: Redesigned right-hand action column across `MultipleAdsModulesWindow` and `MultipleAdsSetupWindow` with unified right alignment (`GUILayout.FlexibleSpace`, `RightBoldLabel`), standardized button widths (94px), concise "Install SDK" action buttons (192px) with package details in tooltips to prevent text cropping.
- **LevelPlay 9.5.1 Integration**: Updated LevelPlay template and integration to use modern `Unity.Services.LevelPlay` namespace, `LevelPlayBannerPosition.Center`, and current event callback signatures.

### Fixed
- Fixed compilation error `CS0246` from legacy ironSource mediation namespace in LevelPlay provider.
- Fixed text squishing, clipping, and irregular button offsets in module card rows.

## [1.0.0] - 2026-11-02
- Initial package release of MultipleAdsManager.
- Multi-network mediation support for Google Mobile Ads, LevelPlay, AppLovin, Vungle, and MockAds.
- Unified Runtime, Editor tools, and Samples.
- Bundled default ProGuard and AndroidManifest text templates directly inside the package (`Editor/Templates/`).
- Granular permission stripping toggles in Optimization section for `ACCESS_FINE_LOCATION`, `ACCESS_COARSE_LOCATION`, `READ_PHONE_STATE`, `WRITE_EXTERNAL_STORAGE`, and `AD_ID`.
- Modular toggle controls for Core and per-provider ProGuard / R8 rules with active module detection.
