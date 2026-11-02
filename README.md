# Multiple Ads Manager

A modular mobile ads mediation system for Unity supporting Google Mobile Ads, LevelPlay, AppLovin, Vungle, and MockAds.

---

## Requirements

- **Unity:** 2021.3 or higher

---

## Installation

Choose one of the installation methods below:

### 1. Via Git URL (Recommended)

#### Option A: Using Unity Package Manager GUI
1. In Unity, open **Window** > **Package Manager**.
2. Click the **+** (add) button in the top-left corner.
3. Select **Add package from git URL...**.
4. Enter the repository URL:
   ```text
   https://github.com/herghys/MultipleAdsManager.git
   ```
   *(Optional) To target a specific release tag:*
   ```text
   https://github.com/herghys/MultipleAdsManager.git#1.0.0
   ```
5. Click **Add**.

#### Option B: Editing `manifest.json`
Add the package to your project's `Packages/manifest.json` under `dependencies`:

```json
{
  "dependencies": {
    "com.herghys.multipleadsmanager": "https://github.com/herghys/MultipleAdsManager.git"
  }
}
```

---

### 2. Via Tarball (`.tgz`)

1. Go to the [GitHub Releases](https://github.com/herghys/MultipleAdsManager/releases) page.
2. Download the latest `.tgz` package file (e.g., `com.herghys.multipleadsmanager-1.0.0.tgz`).
3. In Unity, open **Window** > **Package Manager**.
4. Click the **+** button and select **Add package from tarball...**.
5. Browse and select the downloaded `.tgz` file.

---

## License

This project is licensed under the [MIT License](LICENSE.md).
