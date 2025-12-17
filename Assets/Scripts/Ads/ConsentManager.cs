using System;
using UnityEngine;
#if UNITY_ANDROID || UNITY_IOS
using GoogleMobileAds.Ump.Api;
#endif

public class ConsentManager : MonoBehaviour
{
    public static ConsentManager Instance { get; private set; }

    public bool CanRequestAds { get; private set; }
    public event Action<bool> OnConsentReady;

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
#if UNITY_ANDROID || UNITY_IOS
        RequestConsent();
#else
        CanRequestAds = true;
        OnConsentReady?.Invoke(true);
#endif
    }

#if UNITY_ANDROID || UNITY_IOS
    private void RequestConsent()
    {
        var request = new ConsentRequestParameters
        {
            TagForUnderAgeOfConsent = false
        };

        ConsentInformation.Update(request, (FormError updateError) =>
        {
            if (updateError != null)
            {
                Debug.LogWarning($"[UMP] Update error: {updateError.Message}");
                CanRequestAds = true; // 실패 시 광고 요청은 허용(비개인화로 처리됨)
                OnConsentReady?.Invoke(CanRequestAds);
                return;
            }

            LoadAndShowConsentFormIfRequired();
        });
    }

    private void LoadAndShowConsentFormIfRequired()
    {
        ConsentForm.LoadAndShowConsentFormIfRequired((FormError loadError) =>
        {
            if (loadError != null)
            {
                Debug.LogWarning($"[UMP] Load/Show form error: {loadError.Message}");
            }

            CanRequestAds = ConsentInformation.CanRequestAds();
            OnConsentReady?.Invoke(CanRequestAds);
        });
    }
#endif
}





