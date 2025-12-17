using System.Collections.Generic;
using UnityEngine;

// ✅ 광고 및 수익 전용 Analytics 이벤트
public static class AdRevenueEvent
{
    // 📌 배너 광고 관련
    public const string BannerAdRequest = "banner_ad_request";           // 배너 광고 요청
    public const string BannerAdLoad = "banner_ad_load";                 // 배너 광고 로드 성공
    public const string BannerAdLoadFail = "banner_ad_load_fail";        // 배너 광고 로드 실패
    public const string BannerAdShow = "banner_ad_show";                 // 배너 광고 표시
    public const string BannerAdClick = "banner_ad_click";               // 배너 광고 클릭
    public const string BannerAdRevenue = "banner_ad_revenue";           // 배너 광고 수익

    // 📌 전면 광고 관련
    public const string InterstitialAdRequest = "interstitial_ad_request";       // 전면 광고 요청
    public const string InterstitialAdLoad = "interstitial_ad_load";             // 전면 광고 로드 성공
    public const string InterstitialAdLoadFail = "interstitial_ad_load_fail";    // 전면 광고 로드 실패
    public const string InterstitialAdShow = "interstitial_ad_show";             // 전면 광고 표시
    public const string InterstitialAdClick = "interstitial_ad_click";           // 전면 광고 클릭
    public const string InterstitialAdClose = "interstitial_ad_close";           // 전면 광고 닫힘
    public const string InterstitialAdRevenue = "interstitial_ad_revenue";       // 전면 광고 수익

    // 📌 보상형 광고 관련
    public const string RewardedAdRequest = "rewarded_ad_request";               // 보상형 광고 요청
    public const string RewardedAdLoad = "rewarded_ad_load";                     // 보상형 광고 로드 성공
    public const string RewardedAdLoadFail = "rewarded_ad_load_fail";            // 보상형 광고 로드 실패
    public const string RewardedAdShow = "rewarded_ad_show";                     // 보상형 광고 표시
    public const string RewardedAdClick = "rewarded_ad_click";                   // 보상형 광고 클릭
    public const string RewardedAdComplete = "rewarded_ad_complete";             // 보상형 광고 완료 (보상 지급)
    public const string RewardedAdSkip = "rewarded_ad_skip";                     // 보상형 광고 중도 종료
    public const string RewardedAdRevenue = "rewarded_ad_revenue";               // 보상형 광고 수익

    // 📌 광고 퍼포먼스 관련
    public const string AdSessionStart = "ad_session_start";                     // 광고 세션 시작
    public const string AdSessionEnd = "ad_session_end";                         // 광고 세션 종료
    public const string AdUserEngagement = "ad_user_engagement";                 // 사용자 광고 참여도
    public const string AdRetryAttempt = "ad_retry_attempt";                     // 광고 재시도 시도
    public const string AdNetworkError = "ad_network_error";                     // 광고 네트워크 오류

    // 📌 수익 관련
    public const string DailyAdRevenue = "daily_ad_revenue";                     // 일일 광고 수익
    public const string UserAdValue = "user_ad_value";                           // 사용자별 광고 가치
    public const string AdConversionRate = "ad_conversion_rate";                 // 광고 전환율
    public const string AdFillRate = "ad_fill_rate";                             // 광고 채움률

    // 📌 사용자 행동 분석 (광고 관련)
    public const string AdOptIn = "ad_opt_in";                                   // 광고 수용 (Yes 선택)
    public const string AdOptOut = "ad_opt_out";                                 // 광고 거부 (No 선택)
    public const string AdFrequencyCap = "ad_frequency_cap";                     // 광고 빈도 제한 도달
}