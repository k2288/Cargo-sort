#if CARGO_LEVELPLAY
using System;
using System.Collections.Generic;
using CargoSort.Game;
using Unity.Services.LevelPlay;
using UnityEngine;

/// <summary>Rewarded and interstitial ads through Unity LevelPlay (SDK 9 ad unit API).</summary>
public sealed class LevelPlayAds : MonoBehaviour, IAdsService
{
    private const float RetryDelay = 15f;

    private LevelPlayRewardedAd rewarded;
    private LevelPlayInterstitialAd interstitial;
    private Action<bool> rewardedDone;
    private Action interstitialDone;
    private bool earnedReward;

    // SDK callbacks are moved to the main thread before touching the game.
    private readonly List<Action> mainThread = new List<Action>();

    public bool RewardedReady => rewarded != null && rewarded.IsAdReady();

    public void Begin()
    {
        LevelPlay.OnInitSuccess += _ => Post(CreateAdUnits);
        LevelPlay.OnInitFailed += error =>
        {
            Debug.LogWarning("[Ads] LevelPlay init failed: " + error);
            Post(() => Invoke(nameof(Init), RetryDelay * 2));
        };
        Init();
    }

    private void Init() => LevelPlay.Init(AdsConfig.AppKey);

    private void CreateAdUnits()
    {
        rewarded = new LevelPlayRewardedAd(AdsConfig.RewardedAdUnitId);
        rewarded.OnAdRewarded += (_, __) => Post(() => earnedReward = true);
        rewarded.OnAdClosed += _ => Post(FinishRewarded);
        rewarded.OnAdDisplayFailed += (_, __) => Post(() => { earnedReward = false; FinishRewarded(); });
        rewarded.OnAdLoadFailed += _ => Post(() => Invoke(nameof(LoadRewarded), RetryDelay));
        LoadRewarded();

        interstitial = new LevelPlayInterstitialAd(AdsConfig.InterstitialAdUnitId);
        interstitial.OnAdClosed += _ => Post(FinishInterstitial);
        interstitial.OnAdDisplayFailed += (_, __) => Post(FinishInterstitial);
        interstitial.OnAdLoadFailed += _ => Post(() => Invoke(nameof(LoadInterstitial), RetryDelay));
        LoadInterstitial();
    }

    private void LoadRewarded() => rewarded?.LoadAd();
    private void LoadInterstitial() => interstitial?.LoadAd();

    public void ShowRewarded(string placement, Action<bool> onDone)
    {
        if (!RewardedReady)
        {
            onDone?.Invoke(false);
            return;
        }
        rewardedDone = onDone;
        earnedReward = false;
        rewarded.ShowAd(placement);
    }

    public void ShowInterstitial(string placement, Action onDone)
    {
        // Never block the player: if no ad is ready, just continue.
        if (interstitial == null || !interstitial.IsAdReady())
        {
            onDone?.Invoke();
            return;
        }
        interstitialDone = onDone;
        interstitial.ShowAd(placement);
    }

    private void FinishRewarded()
    {
        var done = rewardedDone;
        rewardedDone = null;
        LoadRewarded();
        done?.Invoke(earnedReward);
    }

    private void FinishInterstitial()
    {
        var done = interstitialDone;
        interstitialDone = null;
        LoadInterstitial();
        done?.Invoke();
    }

    private void Post(Action action)
    {
        lock (mainThread) mainThread.Add(action);
    }

    private void Update()
    {
        Action[] pending;
        lock (mainThread)
        {
            if (mainThread.Count == 0) return;
            pending = mainThread.ToArray();
            mainThread.Clear();
        }
        foreach (var action in pending) action();
    }
}
#endif
