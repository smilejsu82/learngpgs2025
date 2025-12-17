using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Analytics;
using Firebase.Auth;
using Firebase.Extensions;   // ContinueWithOnMainThread
using Google;
using UnityEngine;

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance;

    private FirebaseAuth auth;
    
    // Firebase / Analytics 초기화 여부
    private bool isFirebaseInitialized = false;
    private bool isAnalyticsEnabled = false;

    // 외부에서 구독 가능한 이벤트
    public event Action<FirebaseUser> OnSignInSuccess;
    public event Action<string> OnSignInFailed;

    // 이벤트별 버전 매핑 테이블 (기존 AnalyticsEventVersionMap 통합)
    private readonly Dictionary<string, string> eventVersionMap = new()
    {
        { AnalyticsEvent.Login,        AnalyticsVersion.V1 },
        { AnalyticsEvent.StartStage,        AnalyticsVersion.V1 },
        { AnalyticsEvent.ClearStage,        AnalyticsVersion.V1 },
        { AnalyticsEvent.FailStage,        AnalyticsVersion.V1 },
        { AnalyticsEvent.GetGold100,        AnalyticsVersion.V1 }
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Firebase Auth 인스턴스 (의존성 체크 전에 접근해도 내부에서 나중에 붙는다)
        auth = FirebaseAuth.DefaultInstance;

        // Google Sign-In 설정
        GoogleSignIn.Configuration = new GoogleSignInConfiguration
        {
            WebClientId   = "662869890482-fngbfhs2hjo2mib3pllhl8nu5mqkvlgh.apps.googleusercontent.com",
            RequestIdToken = true,
            RequestEmail   = true
        };
        
        // Firebase 초기화 (fire-and-forget)
        _ = InitializeFirebaseAsync();
    }

    private async Task InitializeFirebaseAsync()
    {
        var status = await FirebaseApp.CheckDependenciesAsync();

        if (status == DependencyStatus.Available)
        {
            FirebaseApp app = FirebaseApp.DefaultInstance;

            // Analytics 활성화
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
            isFirebaseInitialized = true;
            isAnalyticsEnabled    = true;

            Debug.Log("[Firebase] Initialization completed. Analytics enabled.");
        }
        else
        {
            Debug.LogError($"[Firebase] Dependency error: {status}");
        }
    }

    private void Start()
    {
        Debug.Log("FirebaseManager Start");
    }

    // ------------------------
    // Google 로그인 + Auth
    // ------------------------
    public void SignInWithGoogle()
    {
        Debug.Log("Google Sign-In Start");

        GoogleSignIn.DefaultInstance.SignIn().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                string error = task.Exception != null ? task.Exception.ToString() : "Unknown error";
                Debug.LogError("Google Sign-In Failed: " + error);
                OnSignInFailed?.Invoke(error);
                return;
            }

            if (task.IsCanceled)
            {
                Debug.LogWarning("Google Sign-In Canceled");
                OnSignInFailed?.Invoke("Canceled");
                return;
            }

            GoogleSignInUser user = task.Result;

            Debug.Log("Google Sign-In Success");
            Debug.Log("DisplayName: " + user.DisplayName);
            Debug.Log("Email: " + user.Email);
            Debug.Log("IdToken: " + user.IdToken);

            Credential credential = GoogleAuthProvider.GetCredential(user.IdToken, null);

            auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(authTask =>
            {
                if (authTask.IsFaulted)
                {
                    string error = authTask.Exception != null ? authTask.Exception.ToString() : "Unknown error";
                    Debug.LogError("Firebase Auth Failed: " + error);
                    OnSignInFailed?.Invoke(error);
                    return;
                }

                if (authTask.IsCanceled)
                {
                    Debug.LogWarning("Firebase Auth Canceled");
                    OnSignInFailed?.Invoke("Canceled");
                    return;
                }

                FirebaseUser newUser = authTask.Result;
                Debug.Log("Firebase Login Success");
                Debug.Log("UID: " + newUser.UserId);
                Debug.Log("Name: " + newUser.DisplayName);

                // Analytics: UserId 설정
                if (isAnalyticsEnabled)
                {
                    FirebaseAnalytics.SetUserId(newUser.UserId);
                    FirebaseAnalytics.SetUserProperty("login_provider", "google");
                }

                // 로그인 이벤트 로깅 (버전 포함)
                LogEventVersioned(AnalyticsEvent.Login);

                OnSignInSuccess?.Invoke(newUser);
            });
        });
    }

    // ------------------------
    // Analytics 헬퍼 (기존 AnalyticsLogger + VersionMap 통합)
    // ------------------------

    // 이벤트 이름에 버전 prefix 붙이기
    private string GetVersionedEventName(string baseName)
    {
        if (eventVersionMap.TryGetValue(baseName, out var version))
        {
            return $"{version}_{baseName}";
        }

        // 매핑 없으면 기본 v1
        return $"v1_{baseName}";
    }

    // 순수 이름 그대로 이벤트 로그
    public void LogEventRaw(string eventName)
    {
        if (!isAnalyticsEnabled)
        {
            Debug.LogWarning($"[FirebaseAnalytics] Not initialized. Raw Event Skipped: {eventName}");
            return;
        }

        FirebaseAnalytics.LogEvent(eventName);
        Debug.Log($"[FirebaseAnalytics] Raw Event: {eventName}");
    }

    // 버전 붙인 이름으로 이벤트 로그 (파라미터 없는 버전)
    public void LogEventVersioned(string baseEventName)
    {
        if (!isAnalyticsEnabled)
        {
            Debug.LogWarning($"[FirebaseAnalytics] Not initialized. Event Skipped: {baseEventName}");
            return;
        }

        string eventNameWithVersion = GetVersionedEventName(baseEventName);
        FirebaseAnalytics.LogEvent(eventNameWithVersion);

        Debug.Log($"[FirebaseAnalytics] Event: {eventNameWithVersion}");
    }

    // 버전 붙인 이름으로 이벤트 로그 (파라미터 포함)
    public void LogEventVersioned(string baseEventName, params Parameter[] parameters)
    {
        if (!isAnalyticsEnabled)
        {
            Debug.LogWarning($"[FirebaseAnalytics] Not initialized. Event Skipped: {baseEventName}");
            return;
        }

        string eventNameWithVersion = GetVersionedEventName(baseEventName);
        FirebaseAnalytics.LogEvent(eventNameWithVersion, parameters);

        Debug.Log($"[FirebaseAnalytics] Event: {eventNameWithVersion} / Params: {parameters?.Length ?? 0}");
    }

    // 필요하면 외부에서 Analytics 사용 가능 여부 확인용
    public bool IsAnalyticsReady => isAnalyticsEnabled;
}
