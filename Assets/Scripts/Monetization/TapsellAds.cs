#if CARGO_TAPSELL
using System;
using CargoSort.Game;
using TapsellPlusSDK;
using UnityEngine;

/// <summary>Rewarded and interstitial ads through Tapsell Plus (for Cafe Bazaar and Myket builds).</summary>
public sealed class TapsellAds : MonoBehaviour, IAdsService
{
    private const float RetryDelay = 15f;

    private string rewardedResponseId;
    private string interstitialResponseId;
    private bool requestingRewarded;
    private bool requestingInterstitial;
    private Action<bool> rewardedDone;
    private Action interstitialDone;
    private bool earnedReward;

    public bool RewardedReady => rewardedResponseId != null;

    public void Begin()
    {
        TapsellPlus.Initialize(AdsConfig.TapsellAppKey,
            _ =>
            {
                RequestRewarded();
                RequestInterstitial();
            },
            error => Debug.LogWarning("[Ads] Tapsell init failed: " + error));
    }

    private void RequestRewarded()
    {
        if (requestingRewarded || rewardedResponseId != null) return;
        requestingRewarded = true;
        TapsellPlus.RequestRewardedVideoAd(AdsConfig.TapsellRewardedZoneId,
            ad => { requestingRewarded = false; rewardedResponseId = ad.responseId; },
            _ => { requestingRewarded = false; Invoke(nameof(RequestRewarded), RetryDelay); });
    }

    private void RequestInterstitial()
    {
        if (requestingInterstitial || interstitialResponseId != null) return;
        requestingInterstitial = true;
        TapsellPlus.RequestInterstitialAd(AdsConfig.TapsellInterstitialZoneId,
            ad => { requestingInterstitial = false; interstitialResponseId = ad.responseId; },
            _ => { requestingInterstitial = false; Invoke(nameof(RequestInterstitial), RetryDelay); });
    }

    public void ShowRewarded(string placement, Action<bool> onDone)
    {
        if (!RewardedReady)
        {
            RequestRewarded();
            onDone?.Invoke(false);
            return;
        }
        string id = rewardedResponseId;
        rewardedResponseId = null;
        rewardedDone = onDone;
        earnedReward = false;
        TapsellPlus.ShowRewardedVideoAd(id,
            _ => { },
            _ => earnedReward = true,
            _ => FinishRewarded(),
            _ => { earnedReward = false; FinishRewarded(); });
    }

    public void ShowInterstitial(string placement, Action onDone)
    {
        // Never block the player: if no ad is ready, just continue.
        if (interstitialResponseId == null)
        {
            RequestInterstitial();
            onDone?.Invoke();
            return;
        }
        string id = interstitialResponseId;
        interstitialResponseId = null;
        interstitialDone = onDone;
        TapsellPlus.ShowInterstitialAd(id,
            _ => { },
            _ => FinishInterstitial(),
            _ => FinishInterstitial());
    }

    private void FinishRewarded()
    {
        var done = rewardedDone;
        rewardedDone = null;
        RequestRewarded();
        done?.Invoke(earnedReward);
    }

    private void FinishInterstitial()
    {
        var done = interstitialDone;
        interstitialDone = null;
        RequestInterstitial();
        done?.Invoke();
    }
}
#endif
