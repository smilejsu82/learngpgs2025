using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class TitleMain : MonoBehaviour
{
    public TMP_Text versionText;
    public TMP_Text userIdText;
    public TMP_Text userNameText;
    public Image profileImage;
    public Button googleSignInButton;
    
    void Start()
    {
        AdmobManager.Instance.RequestBannerAd();
        
        versionText.text = GetFullVersionString();
        
        Debug.Log($"<color=yellow>{BuildModeSettings.Instance.currentMode}</color>");
        

        // GPGSManager.Instance.OnUserDataLoaded += (userId, userName, profileImage) =>
        // {
        //     userIdText.text = userId;
        //     userNameText.text = userName;
        //     this.profileImage.sprite = TextureToSprite(profileImage);
        // };

        if (BuildModeSettings.Instance.currentMode == BuildMode.Release)
        {
            FirebaseManager.Instance.OnSignInSuccess += (firebaseUser) =>
            {
                Debug.Log($"{firebaseUser.UserId}");
                Debug.Log($"{firebaseUser.Email}");
                Debug.Log($"{firebaseUser.DisplayName}");
                Debug.Log($"{firebaseUser.PhotoUrl}");

                CheckGameInfo();
            
                SceneManager.LoadScene("Home");
            };
            FirebaseManager.Instance.OnSignInFailed += (message) =>
            {
                Debug.Log(message);
            };
        }
        
        
        googleSignInButton.onClick.AddListener(() =>
        {
            if (BuildModeSettings.Instance.currentMode == BuildMode.Release)
            {
                FirebaseManager.Instance.SignInWithGoogle();
            }
            else
            {
                CheckGameInfo();
                SceneManager.LoadScene("Home");
            }
        });
    }

    private void CheckGameInfo()
    {
        //뉴비체크 
        if (InfoManager.Instance.IsNewbie())
        {
            //최초 GameInfo 만들고 저장 하기
            InfoManager.Instance.CreateGameInfo();
        }
        else
        {
            //GameInfo 불러오기 
            InfoManager.Instance.Load();
        }
    }

    public static Sprite TextureToSprite(Texture2D texture, float pixelsPerUnit = 100f)
    {
        if (texture == null)
            return null;

        return Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),   // Pivot: Center
            pixelsPerUnit
        );
    }


    public static int GetAndroidVersionCode()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var packageManager = activity.Call<AndroidJavaObject>("getPackageManager");
            var packageName = activity.Call<string>("getPackageName");
            var packageInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", packageName, 0);

            return packageInfo.Get<int>("versionCode");
        }
#else
        return 0;
#endif
    }

    public static string GetFullVersionString()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        int code = GetAndroidVersionCode();
        return $"{Application.version}+{code}";
#elif UNITY_EDITOR // 에디터에서
        int editorCode = PlayerSettings.Android.bundleVersionCode;
        return $"{Application.version}+{editorCode}";
#elif UNITY_IOS && !UNITY_EDITOR
        return $"{Application.version}";
#else
        return Application.version;
#endif
    }
}