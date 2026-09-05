using System;
using System.Threading.Tasks;
using Project333.Runtime.Application.Ads;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace Project333.Runtime.Presentation.Ads.LevelPlayIntegration
{
    public sealed class LevelPlayRewardedAdService : IRewardedAdService
    {
        private const int NoFillErrorCode = 509;
        private static readonly int[] LoadRetryDelaySeconds = { 5, 15, 30, 60 };

        private LevelPlayRewardedAd _rewardedAd;
        private bool _initializationRequested;
        private bool _initialized;
        private bool _isReady;
        private bool _isLoading;
        private bool _isShowing;
        private bool _isQuitting;
        private int _consecutiveLoadFailures;
        private int _loadGeneration;
        private string _appKey = string.Empty;
        private string _rewardedAdUnitId = string.Empty;
        private string _statusMessage = "광고 SDK 초기화 대기 중입니다.";

        public event Action StateChanged;
        public event Action Rewarded;
        public event Action Closed;

        public bool IsReady => _initialized && _isReady && !_isShowing;
        public bool IsShowing => _isShowing;
        public string StatusMessage => _statusMessage;

        public void Initialize(string appKey, string providerUserId, string rewardedAdUnitId)
        {
            appKey = appKey?.Trim() ?? string.Empty;
            providerUserId = providerUserId?.Trim() ?? string.Empty;
            rewardedAdUnitId = rewardedAdUnitId?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(appKey) || string.IsNullOrWhiteSpace(rewardedAdUnitId))
            {
                SetState("LevelPlay App Key와 Rewarded Ad Unit ID를 입력해 주세요.");
                return;
            }

            if (_initializationRequested)
            {
                if (!string.Equals(_appKey, appKey, StringComparison.Ordinal) ||
                    !string.Equals(_rewardedAdUnitId, rewardedAdUnitId, StringComparison.Ordinal))
                {
                    SetState("광고 설정이 바뀌었습니다. 앱을 다시 실행해 주세요.");
                }

                return;
            }

            _appKey = appKey;
            _rewardedAdUnitId = rewardedAdUnitId;
            _initializationRequested = true;
            UnityEngine.Application.quitting += HandleApplicationQuitting;
            global::Unity.Services.LevelPlay.LevelPlay.OnInitSuccess += HandleInitializationSucceeded;
            global::Unity.Services.LevelPlay.LevelPlay.OnInitFailed += HandleInitializationFailed;
            SetState("광고 SDK 초기화 중입니다.");
            global::Unity.Services.LevelPlay.LevelPlay.Init(_appKey, providerUserId);
        }

        public bool TryShow(string placementName, string dynamicUserId, out string errorMessage)
        {
            placementName = placementName?.Trim() ?? string.Empty;
            dynamicUserId = dynamicUserId?.Trim() ?? string.Empty;

            if (!IsReady || _rewardedAd == null)
            {
                errorMessage = string.IsNullOrWhiteSpace(_statusMessage)
                    ? "광고가 아직 준비되지 않았습니다."
                    : _statusMessage;
                return false;
            }

            if (string.IsNullOrWhiteSpace(dynamicUserId))
            {
                errorMessage = "서버 광고 인증 값이 없습니다.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(placementName) &&
                LevelPlayRewardedAd.IsPlacementCapped(placementName))
            {
                errorMessage = "현재 광고 노출 제한에 도달했습니다.";
                return false;
            }

            if (!global::Unity.Services.LevelPlay.LevelPlay.SetDynamicUserId(dynamicUserId))
            {
                errorMessage = "광고 서버 인증 값을 LevelPlay에 설정하지 못했습니다.";
                return false;
            }

            _isShowing = true;
            _isReady = false;
            SetState("광고 재생 중입니다.");

            if (string.IsNullOrWhiteSpace(placementName))
            {
                _rewardedAd.ShowAd();
            }
            else
            {
                _rewardedAd.ShowAd(placementName: placementName);
            }

            errorMessage = string.Empty;
            return true;
        }

        private void HandleInitializationSucceeded(LevelPlayConfiguration configuration)
        {
            _initialized = true;
            _rewardedAd = new LevelPlayRewardedAd(_rewardedAdUnitId);
            _rewardedAd.OnAdLoaded += HandleAdLoaded;
            _rewardedAd.OnAdLoadFailed += HandleAdLoadFailed;
            _rewardedAd.OnAdDisplayed += HandleAdDisplayed;
            _rewardedAd.OnAdDisplayFailed += HandleAdDisplayFailed;
            _rewardedAd.OnAdRewarded += HandleAdRewarded;
            _rewardedAd.OnAdClosed += HandleAdClosed;
            LoadAd("광고 불러오는 중입니다.", resetFailureCount: true);
        }

        private void HandleInitializationFailed(LevelPlayInitError error)
        {
            _initialized = false;
            _isReady = false;
            _isLoading = false;
            _isShowing = false;
            _loadGeneration++;
            SetState($"광고 SDK 초기화 실패: {error}");
        }

        private void HandleAdLoaded(LevelPlayAdInfo adInfo)
        {
            _loadGeneration++;
            _consecutiveLoadFailures = 0;
            _isLoading = false;
            _isReady = true;
            _isShowing = false;
            SetState("광고 준비 완료.");
        }

        private void HandleAdLoadFailed(LevelPlayAdError error)
        {
            _isLoading = false;
            _isReady = false;
            _isShowing = false;

            var retryDelay = LoadRetryDelaySeconds[Math.Min(
                _consecutiveLoadFailures,
                LoadRetryDelaySeconds.Length - 1)];
            _consecutiveLoadFailures++;
            var retryGeneration = ++_loadGeneration;

            if (error != null && error.ErrorCode == NoFillErrorCode)
            {
                SetState($"현재 제공 가능한 광고가 없습니다. {retryDelay}초 후 자동으로 다시 시도합니다.");
            }
            else
            {
                SetState($"광고를 불러오지 못했습니다. {retryDelay}초 후 자동으로 다시 시도합니다. ({error})");
            }

            RetryLoadAfterDelay(retryGeneration, retryDelay);
        }

        private void HandleAdDisplayed(LevelPlayAdInfo adInfo)
        {
            _isShowing = true;
            SetState("광고 재생 중입니다.");
        }

        private void HandleAdDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            _isShowing = false;
            _isReady = false;
            SetState($"광고를 표시하지 못했습니다: {error}");
            Closed?.Invoke();
            LoadAd("광고를 다시 준비하는 중입니다.", resetFailureCount: true);
        }

        private void HandleAdRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
        {
            Rewarded?.Invoke();
        }

        private void HandleAdClosed(LevelPlayAdInfo adInfo)
        {
            _isShowing = false;
            _isReady = false;
            SetState("다음 광고를 준비하는 중입니다.");
            Closed?.Invoke();
            LoadAd("다음 광고를 준비하는 중입니다.", resetFailureCount: true);
        }

        private void LoadAd(string statusMessage, bool resetFailureCount)
        {
            if (_rewardedAd == null || _isLoading || _isShowing || _isQuitting)
            {
                return;
            }

            _loadGeneration++;
            if (resetFailureCount)
            {
                _consecutiveLoadFailures = 0;
            }

            _isLoading = true;
            _isReady = false;
            SetState(statusMessage);
            _rewardedAd.LoadAd();
        }

        private async void RetryLoadAfterDelay(int retryGeneration, int retryDelaySeconds)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
            }
            catch (Exception)
            {
                return;
            }

            if (_isQuitting ||
                retryGeneration != _loadGeneration ||
                _rewardedAd == null ||
                _isReady ||
                _isLoading ||
                _isShowing)
            {
                return;
            }

            LoadAd("광고를 다시 불러오는 중입니다.", resetFailureCount: false);
        }

        private void HandleApplicationQuitting()
        {
            _isQuitting = true;
            _loadGeneration++;
        }

        private void SetState(string message)
        {
            _statusMessage = message ?? string.Empty;
            StateChanged?.Invoke();
        }
    }

    public static class LevelPlayRewardedAdBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterProvider()
        {
            RewardedAdServiceRegistry.RegisterFactory(() => new LevelPlayRewardedAdService());
        }
    }
}
