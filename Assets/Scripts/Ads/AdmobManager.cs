using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Api;

public class AdmobManager : MonoBehaviour
{
    public static AdmobManager Instance { get; private set; }

    [Header("Ad Unit IDs - Platform Specific")]
    // 배너 광고 ID
    // 실제 배너광고 ID : ca-app-pub-2695041050064773/4046348337
    private string bannerAdUnitId = "ca-app-pub-3940256099942544/6300978111";

    // 전면 광고 ID
    private string interstitialAdUnitId = "\tca-app-pub-3940256099942544/1033173712";

    // 보상형 광고 ID
    // 보상형 광고 ID :ca-app-pub-2695041050064773/8906302081
    private string rewardedAdUnitId = "ca-app-pub-2695041050064773/8906302081"; //"ca-app-pub-3940256099942544/5224354917";

    [SerializeField] private List<string> testDeviceIds = new List<string>
    {
        
        
    };


    private BannerView bannerView;
    private InterstitialAd interstitialAd;
    private RewardedAd rewardedAd;

    private Action onRewardedAdSuccess;
    private Action onAdFinish;
    private Action rewardedAdCallbackSuccess;
    private Action rewardedAdCallbackFail;
    private bool isInitialized = false;

    // ✅ 보상형 광고 실패 반복 제한 관련 변수
    private int rewardedAdRetryCount = 0;
    private const int maxRewardedAdRetry = 3;
    private float lastRewardedAdLoadTime = 0f;
    private const float minRetryInterval = 10f; // 초 단위

    // ✅ 타이밍 문제 해결을 위한 변수들
    private bool hasEarnedReward = false; // 보상 여부 추적용
    private bool isProcessingReward = false; // 보상 처리 중 플래그

    // ✅ Analytics 관련 변수들
    private float adSessionStartTime = 0f;
    private int adsShownInSession = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        ConfigureTestDevices();

        // 동의 준비되면 초기화
        if (ConsentManager.Instance != null)
        {
            ConsentManager.Instance.OnConsentReady += OnConsentReadyHandler;
            if (ConsentManager.Instance.CanRequestAds)
            {
                OnConsentReadyHandler(true);
            }
        }
        else
        {
            // 동의 매니저가 없으면 바로 진행(레거시)
            InitializeAdMob();
            StartAdSession();
        }
    }

    private void OnConsentReadyHandler(bool _)
    {
        InitializeAdMob();
        
        // ✅ 광고 세션 시작 Analytics
        StartAdSession();
    }

    private void ConfigureTestDevices()
    {
        if (testDeviceIds != null && testDeviceIds.Count > 0)
        {
            var config = new RequestConfiguration
            {
                TestDeviceIds = testDeviceIds
            };
            MobileAds.SetRequestConfiguration(config);
            Debug.Log("📱 테스트 디바이스 ID 적용됨");
        }
    }

    private void InitializeAdMob()
    {
        // 플랫폼별 광고 ID 로그 출력
        Debug.Log($"🔧 AdMob 초기화 시작 - 플랫폼: {Application.platform}");
        Debug.Log($"📢 배너 광고 ID: {bannerAdUnitId}");
        Debug.Log($"📺 전면 광고 ID: {interstitialAdUnitId}");
        Debug.Log($"🎁 보상형 광고 ID: {rewardedAdUnitId}");

        MobileAds.Initialize(initStatus =>
        {
            isInitialized = true;
            Debug.Log("✅ AdMob Initialized");

            LoadRewardedAd();    // 최초 로딩
            LoadInterstitialAd();
        });
    }

    // ✅ 광고 세션 관리
    private void StartAdSession()
    {
        adSessionStartTime = Time.time;
        adsShownInSession = 0;
    }

    private void EndAdSession()
    {
        float sessionDuration = Time.time - adSessionStartTime;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            EndAdSession();
        }
        else
        {
            StartAdSession();
        }
    }

    private void OnDestroy()
    {
        EndAdSession();
    }

    // ─────────────────────────────────────
    // 📌 배너 광고
    // ─────────────────────────────────────
    public void RequestBannerAd()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("⚠️ AdMob 아직 초기화되지 않음");
            return;
        }

        // ✅ 배너 광고 요청 Analytics

        bannerView?.Destroy();
        bannerView = new BannerView(bannerAdUnitId, AdSize.Banner, AdPosition.Bottom);
        
        // ✅ 배너 이벤트 핸들러 등록
        bannerView.OnBannerAdLoaded += () =>
        {
            Debug.Log("✅ 배너 광고 로드 완료");
            adsShownInSession++;
        };

        bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
        {
            Debug.LogWarning($"❌ 배너 광고 로드 실패: {error.GetMessage()}");
        };

        bannerView.OnAdClicked += () =>
        {
            Debug.Log("🖱️ 배너 광고 클릭됨");
        };

        bannerView.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log($"💰 배너 광고 수익: {adValue.Value} {adValue.CurrencyCode}");
        };

        bannerView.LoadAd(BuildAdRequest());
        Debug.Log("📢 배너 광고 요청됨");
    }

    public void HideBanner() => bannerView?.Hide();
    public void DestroyBanner() => bannerView?.Destroy();

    // ─────────────────────────────────────
    // 📌 전면 광고
    // ─────────────────────────────────────
    public void LoadInterstitialAd()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("⚠️ AdMob 초기화 전이라 전면 광고 로드 불가");
            return;
        }

        // ✅ 전면 광고 요청 Analytics

        interstitialAd?.Destroy();
        interstitialAd = null;

        InterstitialAd.Load(interstitialAdUnitId, BuildAdRequest(), (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogWarning($"❌ 전면 광고 로드 실패: {error?.GetMessage()}");
                return;
            }

            interstitialAd = ad;
            interstitialAd.OnAdFullScreenContentClosed += HandleAdClosed;
            interstitialAd.OnAdFullScreenContentFailed += HandleAdFailed;

            // ✅ 전면 광고 이벤트 핸들러 등록
            interstitialAd.OnAdClicked += () =>
            {
                Debug.Log("🖱️ 전면 광고 클릭됨");
            };

            interstitialAd.OnAdPaid += (AdValue adValue) =>
            {
                Debug.Log($"💰 전면 광고 수익: {adValue.Value} {adValue.CurrencyCode}");
            };

            Debug.Log("✅ 전면 광고 로드 완료");
        });
    }

    public void ShowInterstitialAd(Action onFinish)
    {

        if (interstitialAd != null && interstitialAd.CanShowAd())
        {
            onAdFinish = onFinish;
            
            // ✅ 전면 광고 표시 Analytics
			// GA alias: interstitial_ad_show (publisher key metric)
			try
			{
				int stageIndex = 0;
				try
				{
					//stageIndex = GameManager.Instance?.character?.GetCharacterInfo()?.expLevel ?? 0;
				}
				catch { /* ignore */ }
				
			}
			catch (System.Exception ex)
			{
				Debug.LogWarning($"GameplayAnalytics: interstitial_ad_show log failed: {ex.Message}");
			}
            adsShownInSession++;
            
            interstitialAd.Show();
            Debug.Log("📢 전면 광고 표시됨");
        }
        else
        {
            Debug.Log("⚠️ 전면 광고 준비 안됨 → 바로 진행");
            onFinish?.Invoke();
        }
    }

    private void HandleAdClosed()
    {
        Debug.Log("📴 전면 광고 닫힘");
        
        // ✅ 전면 광고 닫힘 Analytics
        
        interstitialAd?.Destroy();
        interstitialAd = null;

        onAdFinish?.Invoke();
        onAdFinish = null;

        LoadInterstitialAd(); // 자동 재로드
    }

    private void HandleAdFailed(AdError error)
    {
        Debug.LogWarning($"⚠️ 전면 광고 표시 실패: {error?.GetMessage()}");
        
        // ✅ 전면 광고 실패 Analytics
        
        interstitialAd = null;

        onAdFinish?.Invoke();
        onAdFinish = null;

        LoadInterstitialAd(); // 실패 후 재시도
    }

    public bool IsInterstitialAdReady() => interstitialAd != null && interstitialAd.CanShowAd();

    // ─────────────────────────────────────
    // 📌 보상형 광고 - 타이밍 문제 해결 + Analytics 버전
    // ─────────────────────────────────────
    public void LoadRewardedAd()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("⚠️ AdMob 초기화 전이라 보상형 광고 로드 불가");
            return;
        }

        // ✅ 쿨타임 체크
        if (Time.time - lastRewardedAdLoadTime < minRetryInterval)
        {
            Debug.LogWarning("⏳ 광고 재시도 쿨타임 중");
            return;
        }

        // ✅ 최대 재시도 체크 (단, 성공 후에는 리셋 가능)
        if (rewardedAdRetryCount >= maxRewardedAdRetry)
        {
            Debug.LogError("🚫 광고 로드 최대 재시도 초과");
            return;
        }

        lastRewardedAdLoadTime = Time.time;

        // ✅ 재시도 Analytics
        if (rewardedAdRetryCount > 0)
        {
        }

        // ✅ 보상형 광고 요청 Analytics

        // ✅ 기존 광고 정리
        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
            rewardedAd = null;
        }

        Debug.Log($"🔄 보상형 광고 로드 중... (시도: {rewardedAdRetryCount + 1}/{maxRewardedAdRetry})");

        RewardedAd.Load(rewardedAdUnitId, BuildAdRequest(), (RewardedAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null)
            {
                rewardedAdRetryCount++; // ✅ 실패 시에만 카운트 증가
                Debug.LogError($"❌ 보상형 광고 로드 실패: {error?.GetMessage()}");
                
                // ✅ 보상형 광고 로드 실패 Analytics
                
                // ✅ 자동 재시도 (쿨타임 후)
                if (rewardedAdRetryCount < maxRewardedAdRetry)
                {
                    Invoke(nameof(LoadRewardedAd), minRetryInterval);
                }
                return;
            }

            Debug.Log("✅ 보상형 광고 로드 완료: " + ad.GetResponseInfo());
            
            // ✅ 보상형 광고 로드 성공 Analytics

            // ✅ 성공 시 재시도 카운트 리셋
            rewardedAdRetryCount = 0;
            rewardedAd = ad;
            RegisterEventHandlers(rewardedAd);
        });
    }

    // 동의/지역에 따라 비개인화 파라미터 적용
    private AdRequest BuildAdRequest()
    {
        var request = new AdRequest();
#if UNITY_ANDROID || UNITY_IOS
        // UMP가 준비되지 않았거나, 사용자 동의가 광고 요청 허용이 아닌 경우 비개인화 요청
        bool canRequest = ConsentManager.Instance == null || ConsentManager.Instance.CanRequestAds;
        if (!canRequest)
        {
            if (request.Extras == null)
            {
                request.Extras = new System.Collections.Generic.Dictionary<string, string>();
            }
            request.Extras["npa"] = "1";
        }
#endif
        return request;
    }

    public void ShowRewardedAd(Action onSuccess, Action onSkippedOrFailed)
    {
        if (rewardedAd == null || !rewardedAd.CanShowAd())
        {
            Debug.LogWarning("⚠️ 광고가 준비되지 않음 - 재로드 시도");
            
            // ✅ 보상형 광고 준비 안됨 Analytics
            
            onSkippedOrFailed?.Invoke();
            
            // ✅ 광고가 없으면 즉시 재로드 시도
            LoadRewardedAd();
            return;
        }

        // ✅ 보상 플래그 초기화 (중요!)
        hasEarnedReward = false;
        isProcessingReward = false;
        Debug.Log("🔄 hasEarnedReward 및 isProcessingReward 초기화됨: false");

        // ✅ 콜백 저장
        rewardedAdCallbackSuccess = onSuccess;
        rewardedAdCallbackFail = onSkippedOrFailed;

        // ✅ 보상형 광고 표시 Analytics
        adsShownInSession++;

        Debug.Log("📺 보상형 광고 표시 시작");

        try
        {
            rewardedAd.Show((Reward reward) =>
            {
                // ✅ 보상 받음 플래그 설정 (중요!)
                hasEarnedReward = true;
                Debug.Log($"🎁 보상 획득! {reward.Type}, {reward.Amount}");
                Debug.Log($"✅ hasEarnedReward 설정됨: {hasEarnedReward}");
                Debug.Log($"⏰ 보상 시점: {System.DateTime.Now:HH:mm:ss.fff}");
                
                // ✅ 보상 콜백은 여기서 호출하지 않음 (OnRewardedAdClosed에서 지연 처리)
            });
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"💥 광고 표시 중 예외 발생: {ex.Message}");
            
            // ✅ 보상형 광고 예외 Analytics
            
            hasEarnedReward = false;
            isProcessingReward = false;
            onSkippedOrFailed?.Invoke();
            ClearRewardedAdCallbacks();
            LoadRewardedAd();
        }
    }

    private void OnRewardedAdClosed()
    {
        Debug.Log("📴 보상형 광고 닫힘");
        Debug.Log($"🔍 hasEarnedReward 상태 확인: {hasEarnedReward}");
        Debug.Log($"🔍 isProcessingReward 상태 확인: {isProcessingReward}");
        Debug.Log($"⏰ 광고 닫힘 시점: {System.DateTime.Now:HH:mm:ss.fff}");

        // ✅ 이미 보상 처리 중이면 무시
        if (isProcessingReward)
        {
            Debug.Log("⏳ 이미 보상 처리 중 - OnRewardedAdClosed 무시");
            return;
        }

        isProcessingReward = true;

        // ✅ 짧은 지연 후 보상 상태 재확인 (보상 콜백이 늦을 수 있음)
        StartCoroutine(ProcessRewardWithDelay());
    }

    private System.Collections.IEnumerator ProcessRewardWithDelay()
    {
        Debug.Log("⏳ 보상 처리 지연 시작 - 0.2초 대기");
        
		// 600ms 대기 (보상 콜백이 늦게 오는 경우 대비)
		yield return new WaitForSeconds(0.6f);
        
        Debug.Log($"🔍 지연 후 hasEarnedReward 최종 상태: {hasEarnedReward}");
        
        // ✅ 최종 보상 상태에 따라 콜백 호출
        if (hasEarnedReward)
        {
            Debug.Log("✅ [지연 처리] 광고 시청 완료 (보상 지급)");
            Debug.Log("🔄 SUCCESS 콜백 호출 시도");
            
			// ✅ 보상형 광고 완료 Analytics
			// GA: rewarded_ad_watch (publisher key metric) - reward_type=generic
			try
			{
			}
			catch (System.Exception ex)
			{
				Debug.LogWarning($"GameplayAnalytics: rewarded_ad_watch log failed: {ex.Message}");
			}
            
            try
            {
                rewardedAdCallbackSuccess?.Invoke();
                Debug.Log("✅ SUCCESS 콜백 호출 완료");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ SUCCESS 콜백 호출 중 예외: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning("⛔ [지연 처리] 광고 중간에 닫힘 또는 보상 조건 미충족");
            Debug.Log("🔄 FAIL 콜백 호출 시도");
            
            // ✅ 보상형 광고 건너뛰기 Analytics
            
            try
            {
                rewardedAdCallbackFail?.Invoke();
                Debug.Log("✅ FAIL 콜백 호출 완료");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ FAIL 콜백 호출 중 예외: {ex.Message}");
            }
        }

        // ✅ 정리 작업
        ClearRewardedAdCallbacks();
        
        // ✅ 보상 플래그 리셋
        hasEarnedReward = false;
        isProcessingReward = false;
        Debug.Log("🔄 hasEarnedReward 및 isProcessingReward 리셋됨");
        
        // ✅ 새 광고 로드
        LoadRewardedAd();
    }

    private void OnRewardedAdFailed(AdError error)
    {
        Debug.LogError($"❌ 보상형 광고 실패: {error?.GetMessage()}");
        
        // ✅ 보상형 광고 실패 Analytics
        
        if (!isProcessingReward)
        {
            isProcessingReward = true;
            rewardedAdCallbackFail?.Invoke();
            ClearRewardedAdCallbacks();
            
            hasEarnedReward = false;
            isProcessingReward = false;
            LoadRewardedAd();
        }
    }

    // ✅ 콜백 초기화 메서드 추가
    private void ClearRewardedAdCallbacks()
    {
        rewardedAdCallbackSuccess = null;
        rewardedAdCallbackFail = null;
    }

    private void RegisterEventHandlers(RewardedAd ad)
    {
        // ✅ 기존 이벤트 제거 후 등록 방지를 위해 한번만 등록
        ad.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log($"💰 보상형 광고 수익: {adValue.Value} {adValue.CurrencyCode}");
        };

        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("👁️ 광고 노출 기록됨");
        };

        ad.OnAdClicked += () =>
        {
            Debug.Log("🖱️ 보상형 광고 클릭됨");
        };

        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("📺 광고 전체화면 시작");
            Debug.Log($"🔍 광고 시작 시 hasEarnedReward: {hasEarnedReward}");
            Debug.Log($"⏰ 광고 시작 시점: {System.DateTime.Now:HH:mm:ss.fff}");
        };

        // ✅ 여기서 닫힘/실패 이벤트 등록
        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log($"⏰ 광고 닫힘 이벤트 시점: {System.DateTime.Now:HH:mm:ss.fff}");
            OnRewardedAdClosed();
        };
        
        ad.OnAdFullScreenContentFailed += OnRewardedAdFailed;
    }

    // ✅ 보상형 광고 상태 확인 메서드 개선
    public bool IsRewardedAdReady()
    {
        bool isReady = rewardedAd != null && rewardedAd.CanShowAd();
        
        if (!isReady && rewardedAdRetryCount < maxRewardedAdRetry)
        {
            Debug.Log("🔄 광고 준비 안됨 - 백그라운드 로드 시도");
            LoadRewardedAd();
        }
        
        return isReady;
    }

    // ✅ 재시도 카운트 리셋 메서드 (필요시 외부에서 호출)
    public void ResetRewardedAdRetryCount()
    {
        rewardedAdRetryCount = 0;
        Debug.Log("🔄 보상형 광고 재시도 카운트 리셋");
    }

    // ✅ 사용자 광고 참여도 로그 (외부에서 호출)
    public void LogUserAdEngagement(float engagementScore)
    {
    }

    // ✅ 디버깅용 상태 출력 메서드
    public void PrintAdStatus()
    {
        Debug.Log($"📊 광고 상태 정보:");
        Debug.Log($"   - 현재 플랫폼: {Application.platform}");
        Debug.Log($"   - AdMob 초기화: {isInitialized}");
        Debug.Log($"   - 배너광고 ID: {bannerAdUnitId}");
        Debug.Log($"   - 전면광고 ID: {interstitialAdUnitId}");
        Debug.Log($"   - 보상광고 ID: {rewardedAdUnitId}");
        Debug.Log($"   - 전면광고 준비: {IsInterstitialAdReady()}");
        Debug.Log($"   - 보상광고 준비: {IsRewardedAdReady()}");
        Debug.Log($"   - 보상광고 재시도: {rewardedAdRetryCount}/{maxRewardedAdRetry}");
        Debug.Log($"   - 마지막 로드 시간: {Time.time - lastRewardedAdLoadTime:F1}초 전");
        Debug.Log($"   - 세션 광고 수: {adsShownInSession}");
    }

    // ✅ 일일 광고 수익 로그 (외부에서 호출)
    public void LogDailyRevenue(double totalRevenue)
    {
    }

    // ✅ 광고 전환율 로그 (외부에서 호출)
    public void LogAdConversionRate(float conversionRate, string adType)
    {
    }

    // ✅ 광고 채움률 로그 (외부에서 호출)
    public void LogAdFillRate(float fillRate, string adType)
    {
    }

    // ✅ 네트워크 오류 로그
    private void LogNetworkError(string adType, string errorCode, string errorMessage)
    {
    }

    // ✅ 긴급 디버깅용 강제 성공 메서드 (필요시)
    [System.Obsolete("디버깅용 메서드")]
    public void ForceSuccessForTesting()
    {
        Debug.Log("🧪 [테스트] 강제 성공 처리");
        hasEarnedReward = true;
        OnRewardedAdClosed();
    }

    // ✅ Analytics 테스트 메서드 (디버깅용)
    [System.Obsolete("디버깅용 메서드")]
    public void TestAnalytics()
    {
        Debug.Log("🧪 Analytics 테스트 시작");
        
        // 테스트 이벤트들
        
        Debug.Log("✅ Analytics 테스트 완료");
    }
}