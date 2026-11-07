using System;
using UnityEngine;
using UnityEngine.UI;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Mock
{
    public class MockAdView : MonoBehaviour
    {
        public enum MockAdType
        {
            Banner,
            Interstitial,
            Rewarded
        }

        [SerializeField] private MockAdType adType = MockAdType.Banner;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button claimButton;
        [SerializeField] private Text titleText;
        [SerializeField] private Text messageText;
        [SerializeField] private RectTransform adPanelRect;

        private Action onClosedCallback;
        private Action onRewardEarnedCallback;

        public MockAdType Type => adType;

        private void Awake()
        {
#if !UNITY_EDITOR
            Destroy(gameObject);
            return;
#else
            EnsureValidFonts();

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseClicked);
            }

            if (claimButton != null)
            {
                claimButton.onClick.AddListener(OnClaimClicked);
            }
#endif
        }

        private void EnsureValidFonts()
        {
            Font runtimeFont = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") 
                               ?? Font.CreateDynamicFontFromOSFont("Arial", 16);

            foreach (var txt in GetComponentsInChildren<Text>(true))
            {
                if (txt != null && txt.font == null)
                {
                    txt.font = runtimeFont;
                }
            }
        }

        public void SetupBanner(BannerPosition position, Action onClosed = null)
        {
            adType = MockAdType.Banner;
            onClosedCallback = onClosed;

            if (titleText != null)
            {
                titleText.text = $"Mock Banner Ad ({position})";
            }

            if (adPanelRect != null)
            {
                ApplyBannerPosition(position);
            }
        }

        public void SetupInterstitial(string placement, Action onClosed)
        {
            adType = MockAdType.Interstitial;
            onClosedCallback = onClosed;

            if (titleText != null)
            {
                titleText.text = "Mock Interstitial Ad";
            }

            if (messageText != null)
            {
                messageText.text = string.IsNullOrEmpty(placement)
                    ? "Full-screen interstitial ad simulation.\nClick Close to return to the game."
                    : $"Placement: {placement}\nClick Close to return to the game.";
            }
        }

        public void SetupRewarded(string placement, Action onReward, Action onClosed)
        {
            adType = MockAdType.Rewarded;
            onRewardEarnedCallback = onReward;
            onClosedCallback = onClosed;

            if (titleText != null)
            {
                titleText.text = "Mock Rewarded Video Ad";
            }

            if (messageText != null)
            {
                messageText.text = string.IsNullOrEmpty(placement)
                    ? "Simulated rewarded video ad.\nClick Claim Reward to grant in-game currency/bonus."
                    : $"Placement: {placement}\nClick Claim Reward to grant bonus.";
            }
        }

        public void ApplyBannerPosition(BannerPosition position)
        {
            if (adPanelRect == null) return;

            switch (position)
            {
                case BannerPosition.Top:
                case BannerPosition.TopLeft:
                case BannerPosition.TopRight:
                    adPanelRect.anchorMin = new Vector2(0.5f, 1f);
                    adPanelRect.anchorMax = new Vector2(0.5f, 1f);
                    adPanelRect.pivot = new Vector2(0.5f, 1f);
                    adPanelRect.anchoredPosition = new Vector2(0f, 0f);
                    break;

                case BannerPosition.Bottom:
                case BannerPosition.BottomLeft:
                case BannerPosition.BottomRight:
                    adPanelRect.anchorMin = new Vector2(0.5f, 0f);
                    adPanelRect.anchorMax = new Vector2(0.5f, 0f);
                    adPanelRect.pivot = new Vector2(0.5f, 0f);
                    adPanelRect.anchoredPosition = new Vector2(0f, 0f);
                    break;

                case BannerPosition.Center:
                    adPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
                    adPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
                    adPanelRect.pivot = new Vector2(0.5f, 0.5f);
                    adPanelRect.anchoredPosition = Vector2.zero;
                    break;
            }
        }

        private void OnCloseClicked()
        {
            onClosedCallback?.Invoke();
            Destroy(gameObject);
        }

        private void OnClaimClicked()
        {
            onRewardEarnedCallback?.Invoke();
            onClosedCallback?.Invoke();
            Destroy(gameObject);
        }
    }
}