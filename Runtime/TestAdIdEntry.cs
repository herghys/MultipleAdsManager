using System;
using UnityEngine;

namespace Herghys.MultipleAdsManager.Runtime
{
    [Serializable]
    public class TestAdIdEntry
    {
        [SerializeField] private IAdsType adType = IAdsType.Banner;
        [SerializeField] private bool enabled = true;
        [SerializeField] private string androidTestAdId = "";
        [SerializeField] private string iosTestAdId = "";

        public TestAdIdEntry() { }

        public TestAdIdEntry(IAdsType type, string androidId, string iosId = "", bool isEnabled = true)
        {
            adType = type;
            androidTestAdId = androidId ?? "";
            iosTestAdId = string.IsNullOrEmpty(iosId) ? (androidId ?? "") : iosId;
            enabled = isEnabled;
        }

        public IAdsType AdType
        {
            get => adType;
            set => adType = value;
        }

        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        public string AndroidTestAdId
        {
            get => androidTestAdId;
            set => androidTestAdId = value;
        }

        public string IosTestAdId
        {
            get => iosTestAdId;
            set => iosTestAdId = value;
        }

        public string TestAdId
        {
            get
            {
#if UNITY_IOS
                return !string.IsNullOrEmpty(iosTestAdId) ? iosTestAdId : androidTestAdId;
#elif UNITY_ANDROID
                return androidTestAdId;
#elif UNITY_EDITOR
                return UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS
                    ? (!string.IsNullOrEmpty(iosTestAdId) ? iosTestAdId : androidTestAdId)
                    : androidTestAdId;
#else
                return androidTestAdId;
#endif
            }
        }

        public bool HasValidTestId => !string.IsNullOrWhiteSpace(androidTestAdId) || !string.IsNullOrWhiteSpace(iosTestAdId);

        public override string ToString()
        {
            return $"[{adType}] Enabled: {enabled}, Android: '{androidTestAdId}', iOS: '{iosTestAdId}'";
        }
    }
}
