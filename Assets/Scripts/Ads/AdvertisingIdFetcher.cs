using UnityEngine;

public class AdvertisingIdFetcher : MonoBehaviour
{
    void Start()
    {
        // 시작 시 자동으로 광고 ID 가져오기
        GetAdvertisingId();
    }
    
    public void GetAdvertisingId()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaClass up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = up.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject contentResolver = currentActivity.Call<AndroidJavaObject>("getContentResolver");
            
            AndroidJavaClass secureClass = new AndroidJavaClass("android.provider.Settings$Secure");
            string androidId = secureClass.CallStatic<string>("getString", contentResolver, "android_id");
            
            Debug.Log("Android ID: " + androidId);
            
            // 광고 ID는 별도로 가져와야 함
            GetGoogleAdvertisingId();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Android ID 가져오기 실패: " + e.Message);
        }
#else
        Debug.Log("Android 빌드에서만 광고 ID를 가져올 수 있습니다. (에디터에서는 동작하지 않음)");
#endif
    }
    
    private void GetGoogleAdvertisingId()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaClass advertisingIdClient = new AndroidJavaClass("com.google.android.gms.ads.identifier.AdvertisingIdClient");
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            
            // getAdvertisingIdInfo는 메인 스레드에서 호출하면 안됨 (NetworkOnMainThreadException)
            // 비동기 처리 필요
            AndroidJavaObject info = advertisingIdClient.CallStatic<AndroidJavaObject>("getAdvertisingIdInfo", activity);
            string advertisingId = info.Call<string>("getId");
            bool isLimitAdTrackingEnabled = info.Call<bool>("isLimitAdTrackingEnabled");
            
            Debug.Log("Advertising ID: " + advertisingId);
            Debug.Log("광고 추적 제한: " + isLimitAdTrackingEnabled);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Advertising ID 가져오기 실패: " + e.Message);
        }
#endif
    }
}