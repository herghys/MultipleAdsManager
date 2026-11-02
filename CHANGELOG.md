# Changelog

All notable changes to this package will be documented in this file.

## [1.0.0] - 2026-11-02
- Initial package release of MultipleAdsManager.
- Multi-network mediation support for Google Mobile Ads, LevelPlay, AppLovin, Vungle, and MockAds.
- Unified Runtime, Editor tools, and Samples.
- Bundled default ProGuard and AndroidManifest text templates directly inside the package (`Editor/Templates/`).
- Granular permission stripping toggles in Optimization section for `ACCESS_FINE_LOCATION`, `ACCESS_COARSE_LOCATION`, `READ_PHONE_STATE`, `WRITE_EXTERNAL_STORAGE`, and `AD_ID`.
- Modular toggle controls for Core and per-provider ProGuard / R8 rules with active module detection.
