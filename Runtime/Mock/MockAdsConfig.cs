using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Mock
{
    [CreateAssetMenu(fileName = "MockAdsConfig", menuName = "MultipleAds/Mock Ads Config", order = 90)]
    public class MockAdsConfig : BaseAdConfig
    {
        public MockAdsConfig()
        {
            networkName = "MockAds";
            networkType = AdNetworkType.Mock;
            enabled = true;
            appName = "Default Apps";
            appStoreDetails = "com.DefaultCompany.DefaultApps";
            androidAppId = "mock-app-id";
            iosAppId = "mock-app-id";
            bannerWeight = 20;
            interstitialWeight = 20;
            rewardedWeight = 20;
            useTestAdIds = false;
        }

        public override void EnsureDefaultTestAdIds(bool overwrite = false)
        {
            // Mock does not use test IDs; mock IDs always persist directly
            if (testAdIds == null) testAdIds = new System.Collections.Generic.List<TestAdIdEntry>();
            testAdIds.Clear();
            useTestAdIds = false;
        }

        public override void GetDefaultTestAdIdForType(IAdsType type, out string androidId, out string iosId)
        {
            string id = AdsTypeEntry.GetDefaultMockId(type);
            androidId = id;
            iosId = id;
        }
    }

    [CreateAssetMenu(fileName = "MockAppConfig", menuName = "MultipleAds/Mock App Config", order = 91)]
    public class MockAppConfig : MockAdsConfig
    {
    }
}

namespace Herghys.MultipleAdsManager.Modules
{
    // Alias for backwards compatibility
    public class MockAdsConfig : Herghys.MultipleAdsManager.Mock.MockAdsConfig { }
}
