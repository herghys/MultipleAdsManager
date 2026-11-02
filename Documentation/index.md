# Multiple Ads Manager

Modular mobile ads mediation system for Unity supporting Google Mobile Ads, LevelPlay, AppLovin, Vungle, and MockAds.

## Features

- Multi-network mediation (Google Mobile Ads, Unity LevelPlay, AppLovin MAX, Vungle).
- In-editor and offline testing via built-in MockAds provider.
- Centralized configuration via ScriptableObject (`MultipleAdsManager Config`).
- Configurable waterfall and weight-based mediation.
- Editor window for SDK installation, module toggles, and Android optimization.

## Quick Start

1. Open **Tools > Herghys > Multiple Ads > Settings** to configure your ad networks.
2. Call initialization at startup:
```csharp
MultipleAdsManager.Instance.Initialize();
```
3. Show ads in your game:
```csharp
// Banner
MultipleAdsManager.Instance.ShowBanner(BannerPosition.Bottom);

// Interstitial
MultipleAdsManager.Instance.ShowInterstitial("placement_name", () => {
    // On ad closed
});

// Rewarded
MultipleAdsManager.Instance.ShowRewarded("placement_name", () => {
    // Reward player
});
```
