using System;
using System.Collections;
using System.Text;
using UnityEngine;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;
using Newtonsoft.Json;
using UnityEngine.SocialPlatforms;
using UnityEngine.Networking;
using UnityEngine.Events;

public class GPGSManager : MonoBehaviour
{
    public static GPGSManager Instance;

    // 클라우드 저장/로드 이벤트 추가
    public event Action onSaveToCloudSuccess;
    public event Action onSaveToCloudFailed;
    
    // 클라우드 로드 이벤트 추가
    public event Action onLoadFromCloudSuccess;
    public event Action onLoadFromCloudFailed;
    
    public event Action<GameInfo> onLoadFromCloud;    //무조건 선택 하게 보여준다 
    
    // 저장 시 충돌 감지 이벤트 (클라우드 데이터, 로컬 데이터)
    public event Action<GameInfo, GameInfo> onSaveConflictDetected;

    public event Action<bool, ISavedGameMetadata, GameInfo, ISavedGameMetadata, GameInfo> onSaveGameConflict;

    // 캐시된 프로필 이미지
    private Texture2D cachedProfileImage;

    // 이벤트 정의
    public event Action<SignInStatus> OnAuthenticationCompleted;
    public event Action<string, string, Texture2D> OnUserDataLoaded; // userName, userId, profileImage
    public event Action<Texture2D> OnProfileImageLoaded;
    
    private IConflictResolver pendingResolver;
    private ISavedGameMetadata pendingOriginalMeta;
    private byte[] pendingOriginalData;
    private ISavedGameMetadata pendingUnmergedMeta;
    private byte[] pendingUnmergedData;
    
    private GameInfo overrideSaveGameInfo;
    
    // 저장 충돌 시 클라우드 데이터 임시 저장
    private GameInfo pendingCloudGameInfo;
    private ISavedGameMetadata pendingSaveMetadata;

    // UnityEvent (Inspector에서 설정 가능)
    [System.Serializable]
    public class UserDataEvent : UnityEvent<string, string, Texture2D> { }
    
    [Header("Events")]
    public UnityEvent onAuthenticationSuccess;
    public UnityEvent onAuthenticationFailed;
    public UserDataEvent onUserDataLoaded;

    private void Awake()
    {
        // 싱글턴 중복 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        PlayGamesPlatform.DebugLogEnabled = true;
        
        
        
        // Social API Provider를 GPGS로 설정
        PlayGamesPlatform.Activate();
    }

    private void Start()
    {
        // 인증 시도
        Authenticate();
    }

    /// <summary>
    /// GPGS 인증을 시작합니다.
    /// </summary>
    public void Authenticate()
    {
        PlayGamesPlatform.Instance.Authenticate(ProcessAuthentication);
    }

    private void ProcessAuthentication(SignInStatus status)
    {
        Debug.Log($"[GPGS] Authentication Status: {status}");

        // 인증 완료 이벤트 발생
        OnAuthenticationCompleted?.Invoke(status);
        
        if (status == SignInStatus.Success)
        {
            Debug.Log($"[GPGS] UserName: {Social.localUser.userName}");
            Debug.Log($"[GPGS] UserID: {Social.localUser.id}");
            
            // UnityEvent 호출
            onAuthenticationSuccess?.Invoke();
            
            // 프로필 이미지 로드
            StartCoroutine(LoadProfileImage((texture) =>
            {
                if (texture != null)
                {
                    Debug.Log($"[GPGS] Profile image loaded successfully: {texture.width}x{texture.height}");
                    cachedProfileImage = texture;
                    
                    // 프로필 이미지 로드 이벤트 발생
                    OnProfileImageLoaded?.Invoke(texture);
                }
                else
                {
                    Debug.LogWarning("[GPGS] Failed to load profile image");
                }
                
                // 모든 사용자 데이터 로드 완료 이벤트 발생 (이미지 유무와 관계없이 진행)
                string userName = GetUserName();
                string userId = GetUserId();
                OnUserDataLoaded?.Invoke(userName, userId, cachedProfileImage);
                
                // UnityEvent 호출
                onUserDataLoaded?.Invoke(userName, userId, cachedProfileImage);
            }));
        }
        else
        {
            Debug.LogWarning($"[GPGS] Authentication failed: {status}. Attempting manual sign-in.");
            
            // UnityEvent 호출 (인증 실패)
            onAuthenticationFailed?.Invoke();
            
            // 수동 인증 시도 (사용자에게 팝업 표시)
            PlayGamesPlatform.Instance.ManuallyAuthenticate(inStatus =>
            {
                Debug.Log($"[GPGS] ManuallyAuthenticate Status: {inStatus}");
                // 수동 인증 후에도 실패하면 추가적인 처리가 필요할 수 있습니다.
            });
        }
    }

    /// <summary>
    /// 프로필 이미지를 로드합니다. 이미 로드된 경우 캐시된 이미지를 반환합니다.
    /// </summary>
    public IEnumerator LoadProfileImage(System.Action<Texture2D> onLoaded)
    {
        // 인증 체크
        
        Debug.Log($"[GPGS] Loading profile image");
        Debug.Log($"[GPGS] {PlayGamesPlatform.Instance.IsAuthenticated()}");    //true
        Debug.Log($"[GPGS] {Social.localUser.authenticated}");  //false
        
        
        if (!IsAuthenticated()) // 수정: Social.localUser.authenticated 대신 IsAuthenticated() 사용
        {
            Debug.LogWarning("[GPGS] User not authenticated");
            onLoaded?.Invoke(null);
            yield break;
        }

        // 캐시된 이미지가 있으면 즉시 반환
        if (cachedProfileImage != null)
        {
            Debug.Log("[GPGS] Using cached profile image");
            onLoaded?.Invoke(cachedProfileImage);
            yield break;
        }

        // AvatarURL 가져오기
        string imageUrl = GetProfileImageUrl();
        
        if (string.IsNullOrEmpty(imageUrl))
        {
            Debug.LogError("[GPGS] Avatar URL is null or empty. Attempting fallback.");
            
            // 폴백 시도
            yield return StartCoroutine(LoadProfileImageFallback(onLoaded));
            yield break;
        }

        Debug.Log($"[GPGS] Loading profile image from: {imageUrl}");

        // UnityWebRequest로 이미지 다운로드
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                cachedProfileImage = texture;
                Debug.Log("[GPGS] Profile image downloaded successfully");
                onLoaded?.Invoke(texture);
            }
            else
            {
                Debug.LogError($"[GPGS] Failed to download profile image: {request.error}. Attempting fallback.");
                
                // 폴백 시도
                yield return StartCoroutine(LoadProfileImageFallback(onLoaded));
            }
        }
    }

    /// <summary>
    /// Social.localUser.image를 사용한 폴백 방식
    /// </summary>
    private IEnumerator LoadProfileImageFallback(System.Action<Texture2D> onLoaded)
    {
        Debug.Log("[GPGS] Attempting fallback: Social.localUser.image");
        
        float timeout = 5f;
        float elapsed = 0f;

        while (Social.localUser.image == null && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (Social.localUser.image != null)
        {
            cachedProfileImage = Social.localUser.image;
            Debug.Log("[GPGS] Fallback successful");
            onLoaded?.Invoke(Social.localUser.image);
        }
        else
        {
            Debug.LogError("[GPGS] All methods failed to load profile image");
            onLoaded?.Invoke(null);
        }
    }

    /// <summary>
    /// 프로필 이미지 URL을 가져옵니다.
    /// </summary>
    public string GetProfileImageUrl()
    {
        if (!IsAuthenticated())
        {
            Debug.LogWarning("[GPGS] Cannot get profile URL: User not authenticated");
            return null;
        }

        try
        {
            // PlayGamesLocalUser 타입으로 캐스팅하여 AvatarURL에 접근
            PlayGamesLocalUser localUser = (PlayGamesLocalUser)Social.localUser;
            return localUser.AvatarURL;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GPGS] Error getting avatar URL: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 캐시된 프로필 이미지를 가져옵니다.
    /// </summary>
    public Texture2D GetCachedProfileImage()
    {
        return cachedProfileImage;
    }

    /// <summary>
    /// 현재 사용자가 인증되었는지 확인합니다. (수정: PlayGamesPlatform 사용)
    /// </summary>
    public bool IsAuthenticated()
    {
        return PlayGamesPlatform.Instance != null && PlayGamesPlatform.Instance.IsAuthenticated();
    }

    /// <summary>
    /// 사용자 이름을 가져옵니다.
    /// </summary>
    public string GetUserName()
    {
        return IsAuthenticated() ? Social.localUser.userName : "Guest";
    }

    /// <summary>
    /// 사용자 ID를 가져옵니다.
    /// </summary>
    public string GetUserId()
    {
        return IsAuthenticated() ? Social.localUser.id : null;
    }
    
    
    public void ShowAllLeaderboardsUI()
    {
        if (IsAuthenticated())
        {
            // Google Play Game Services에서 제공하는 기본 리더보드 UI 호출
            PlayGamesPlatform.Instance.ShowLeaderboardUI();
        }
        else
        {
            Debug.Log("[GPGS] User not authenticated, cannot show leaderboards. Attempting re-authentication.");
            // 필요하면 여기서 재인증 시도
            Authenticate();
        }
    }

    public void PostScore(int score)
    {
        if (!IsAuthenticated())
        {
            Debug.LogWarning("[GPGS] Cannot post score: User not authenticated.");
            return;
        }

        Debug.Log($"[GPGSManager] Posting score: {score}");
        
        // TODO: GPGSIds.leaderboard는 사용자 환경에 맞춰야 합니다.
        Social.ReportScore(score, GPGSIds.leaderboard, (bool success) => {
            // Handle success or failure
            Debug.Log($"[ReportScore] : {score}, {success}");
        });
    }

    public void ShowAchievementsUI()
    {
        if (IsAuthenticated())
        {
            Social.ShowAchievementsUI();
        }
        else
        {
            Debug.LogWarning("[GPGS] Cannot show achievements: User not authenticated.");
            Authenticate();
        }
    }

    //단일 업적 
    public void UnlockAchievement(string achievementId, System.Action<bool> callback = null)
    {
        if (!IsAuthenticated()) return;
        
        Debug.Log($"[GPGSManager] ReportProgress (Unlock): {achievementId}");
        
        // 100.0f로 보고하면 즉시 잠금 해제됩니다.
        Social.ReportProgress(achievementId, 100.0f, callback);
    }
    
    //단계별 업적 
    public void IncrementAchievement(string achievementId, int steps, Action<bool> callback = null)
    {
        if (!IsAuthenticated()) return;

        Debug.Log($"[GPGSManager] IncrementAchievement: {achievementId}, steps: {steps}");
        
        PlayGamesPlatform.Instance.IncrementAchievement(achievementId, steps, callback);
    }

    // --- 클라우드 저장/로드 기능 ---
    private bool isSavingCurrentRequest; // SaveToCloud에서 true, LoadFromCloud에서 false로 설정
    private bool isCloudOperationInProgress; // 중복 요청 방지용
    
    private const string SAVE_SLOT_NAME = "game_info"; // 저장 슬롯명 상수화
    
    /// <summary>
    /// 클라우드 작업이 진행 중인지 확인
    /// </summary>
    public bool IsCloudOperationInProgress => isCloudOperationInProgress;
    
    // 기본: 현재 InfoManager.Instance.GameInfo를 저장 (충돌 체크 포함)
    public void SaveToCloud()
    {
        Debug.Log("[GPGS] 1. SaveToCloud (with conflict check)");
        Debug.Log($"[GPGS] IsAuthenticated: {IsAuthenticated()}");
        
        if (!IsAuthenticated())
        {
            Debug.LogWarning("[GPGS] SaveToCloud failed: Not authenticated");
            onSaveToCloudFailed?.Invoke();
            return;
        }
        
        // 중복 요청 방지
        if (isCloudOperationInProgress)
        {
            Debug.LogWarning("[GPGS] SaveToCloud ignored: Another cloud operation is in progress");
            return;
        }

        isCloudOperationInProgress = true;
        
        // 먼저 클라우드 데이터를 읽어서 비교
        Debug.Log("[GPGS] Reading cloud data first for conflict check...");
        OpenSavedGameForConflictCheck(SAVE_SLOT_NAME);
    }
    
    /// <summary>
    /// 충돌 체크를 위해 클라우드 데이터를 먼저 읽기
    /// </summary>
    private void OpenSavedGameForConflictCheck(string filename)
    {
        Debug.Log($"[GPGS] 2. OpenSavedGameForConflictCheck: {filename}");
        
        ISavedGameClient savedGameClient = PlayGamesPlatform.Instance.SavedGame;
        
        // 항상 네트워크에서 최신 데이터 읽기
        savedGameClient.OpenWithAutomaticConflictResolution(
            filename,
            DataSource.ReadNetworkOnly,
            ConflictResolutionStrategy.UseLongestPlaytime,
            OnSavedGameOpenedForConflictCheck
        );
    }
    
    /// <summary>
    /// 충돌 체크용 Open 완료 콜백
    /// </summary>
    private void OnSavedGameOpenedForConflictCheck(SavedGameRequestStatus status, ISavedGameMetadata gameMetadata)
    {
        Debug.Log($"[GPGS] 3. OnSavedGameOpenedForConflictCheck: {status}");
        
        if (status != SavedGameRequestStatus.Success)
        {
            Debug.LogError($"[GPGS] Failed to open saved game for conflict check: {status}");
            isCloudOperationInProgress = false;
            onSaveToCloudFailed?.Invoke();
            return;
        }
        
        // 메타데이터 저장 (나중에 저장할 때 사용)
        pendingSaveMetadata = gameMetadata;
        
        // 클라우드 데이터 읽기
        ISavedGameClient savedGameClient = PlayGamesPlatform.Instance.SavedGame;
        savedGameClient.ReadBinaryData(gameMetadata, OnCloudDataReadForConflictCheck);
    }
    
    /// <summary>
    /// 충돌 체크용 데이터 읽기 완료 콜백
    /// </summary>
    private void OnCloudDataReadForConflictCheck(SavedGameRequestStatus status, byte[] data)
    {
        Debug.Log($"[GPGS] 4. OnCloudDataReadForConflictCheck: {status}");
        
        if (status != SavedGameRequestStatus.Success)
        {
            Debug.LogError($"[GPGS] Failed to read cloud data for conflict check: {status}");
            isCloudOperationInProgress = false;
            onSaveToCloudFailed?.Invoke();
            return;
        }
        
        // 클라우드 데이터가 없는 경우 (첫 저장)
        if (data == null || data.Length == 0)
        {
            Debug.Log("[GPGS] No cloud data exists. Proceeding with save...");
            pendingCloudGameInfo = null;
            ProceedWithSave();
            return;
        }
        
        // 클라우드 데이터 파싱
        GameInfo cloudGameInfo = DecodeGameInfo(data);
        if (cloudGameInfo == null)
        {
            Debug.LogWarning("[GPGS] Failed to decode cloud data. Proceeding with save...");
            pendingCloudGameInfo = null;
            ProceedWithSave();
            return;
        }
        
        // 로컬 데이터 가져오기
        GameInfo localGameInfo = overrideSaveGameInfo ?? InfoManager.Instance?.GameInfo;
        if (localGameInfo == null)
        {
            Debug.LogError("[GPGS] Local GameInfo is null. Cannot save.");
            isCloudOperationInProgress = false;
            onSaveToCloudFailed?.Invoke();
            return;
        }
        
        // 클라우드에 데이터가 있으면 무조건 확인 팝업 표시
        long cloudTicks = cloudGameInfo.savedAtTicks;
        long localTicks = localGameInfo.savedAtTicks;
        
        Debug.Log($"[GPGS] Cloud savedAtTicks: {cloudTicks}, Local savedAtTicks: {localTicks}");
        Debug.Log($"[GPGS] Cloud Stage: {cloudGameInfo.stageInfo?.currentStage ?? 0}, Local Stage: {localGameInfo.stageInfo?.currentStage ?? 0}");
        
        // 클라우드에 데이터가 있으면 무조건 사용자에게 확인 요청
        pendingCloudGameInfo = cloudGameInfo;
        
        // 충돌 이벤트 발생 (UI에서 처리)
        Debug.Log("[GPGS] ⚡ Cloud data exists. Asking user for confirmation...");
        onSaveConflictDetected?.Invoke(cloudGameInfo, localGameInfo);
    }
    
    /// <summary>
    /// 충돌 해결 후 로컬 데이터로 저장 진행
    /// </summary>
    public void ConfirmSaveWithLocalData()
    {
        Debug.Log("[GPGS] ConfirmSaveWithLocalData - Saving local data to cloud...");
        
        // LoadFromCloud 시나리오에서는 pendingSaveMetadata가 없을 수 있음
        if (pendingSaveMetadata == null)
        {
            Debug.Log("[GPGS] No pendingSaveMetadata - this was a LoadFromCloud conflict");
            Debug.Log("[GPGS] Local data kept, no save needed");
        
            pendingCloudGameInfo = null;
            overrideSaveGameInfo = null;
            isCloudOperationInProgress = false;
        
            // 로컬 데이터 유지 성공으로 처리
            onLoadFromCloudSuccess?.Invoke();
            return;
        }
    
        // SaveToCloud 시나리오일 경우 정상 진행
        ProceedWithSave();
    }
    
    /// <summary>
    /// 충돌 해결 후 클라우드 데이터 유지 (저장 취소)
    /// </summary>
    public void ConfirmKeepCloudData()
    {
        Debug.Log("[GPGS] ConfirmKeepCloudData - Keeping cloud data, cancelling save.");
        
        // 클라우드 데이터를 로컬에 적용
        if (pendingCloudGameInfo != null)
        {
            InfoManager.Instance.GameInfo = pendingCloudGameInfo;
            InfoManager.Instance.Save();
            Debug.Log("[GPGS] Cloud data applied to local.");
        }
        
        pendingCloudGameInfo = null;
        pendingSaveMetadata = null;
        overrideSaveGameInfo = null;
        isCloudOperationInProgress = false;
        
        // 성공으로 처리 (클라우드 데이터 유지했으므로)
        onSaveToCloudSuccess?.Invoke();
    }
    
    /// <summary>
    /// 저장 취소
    /// </summary>
    public void CancelSaveToCloud()
    {
        Debug.Log("[GPGS] CancelSaveToCloud");
        
        pendingCloudGameInfo = null;
        pendingSaveMetadata = null;
        overrideSaveGameInfo = null;
        isCloudOperationInProgress = false;
        
        onSaveToCloudFailed?.Invoke();
    }
    
    /// <summary>
    /// 실제 저장 진행
    /// </summary>
    private void ProceedWithSave()
    {
        Debug.Log("[GPGS] 5. ProceedWithSave");
        
        if (pendingSaveMetadata == null)
        {
            Debug.LogError("[GPGS] pendingSaveMetadata is null. Cannot save.");
            isCloudOperationInProgress = false;
            onSaveToCloudFailed?.Invoke();
            return;
        }
        
        try
        {
            GameInfo targetGameInfo = overrideSaveGameInfo ?? InfoManager.Instance?.GameInfo;
            
            if (targetGameInfo == null)
            {
                Debug.LogError("[GPGS] GameInfo is null. Cannot save.");
                isCloudOperationInProgress = false;
                onSaveToCloudFailed?.Invoke();
                return;
            }
            
            string gameInfoJson = JsonConvert.SerializeObject(targetGameInfo);
            Debug.Log($"[GPGS] Serialized GameInfo size: {gameInfoJson.Length} characters.");
            byte[] bytes = Encoding.UTF8.GetBytes(gameInfoJson);
            SaveGame(pendingSaveMetadata, bytes);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GPGS] Failed to serialize game data: {e.Message}");
            isCloudOperationInProgress = false;
            onSaveToCloudFailed?.Invoke();
        }
        finally
        {
            pendingCloudGameInfo = null;
            pendingSaveMetadata = null;
            overrideSaveGameInfo = null;
        }
    }

    // 오버로드: 지정한 GameInfo를 클라우드에 저장하고 싶을 때 사용
    public void SaveToCloud(GameInfo gameInfoOverride)
    {
        if (gameInfoOverride == null)
        {
            Debug.LogError("[GPGS] SaveToCloud(GameInfo) called with null override.");
            onSaveToCloudFailed?.Invoke();
            return;
        }

        overrideSaveGameInfo = gameInfoOverride;
        SaveToCloud(); // 나머지 흐름은 동일
    }


    /// <summary>
    /// 클라우드에서 저장된 데이터를 로드합니다.
    /// </summary>
    public void LoadFromCloud()
    {
        Debug.Log("[GPGS] 1. LoadFromCloud");
        Debug.Log($"[GPGS] IsAuthenticated: {IsAuthenticated()}");
        
        if (!IsAuthenticated())
        {
            Debug.LogWarning("[GPGS] LoadFromCloud failed: Not authenticated");
            onLoadFromCloudFailed?.Invoke();
            return;
        }
        
        // 중복 요청 방지
        if (isCloudOperationInProgress)
        {
            Debug.LogWarning("[GPGS] LoadFromCloud ignored: Another cloud operation is in progress");
            return;
        }

        isCloudOperationInProgress = true;
        isSavingCurrentRequest = false;
        OpenSavedGame(SAVE_SLOT_NAME);
    }

    // ⚠️ 테스트용: true로 설정하면 항상 네트워크에서 읽어 충돌 발생 확률 증가
    // ⚠️ 배포(Release) 빌드 전에 반드시 false로 설정하세요!
    [Header("Debug (배포 전 false로 변경!)")]
    [SerializeField] private bool forceNetworkRead = true;  // 테스트용 true
    
    void OpenSavedGame(string filename)
    {
        Debug.Log($"[GPGS] 2. OpenSavedGame: {filename}");
        Debug.Log($"[GPGS] forceNetworkRead = {forceNetworkRead}");
        Debug.Log($"[GPGS] isSavingCurrentRequest = {isSavingCurrentRequest}");
        
        ISavedGameClient savedGameClient = PlayGamesPlatform.Instance.SavedGame;

        // 테스트용: ReadNetworkOnly를 사용하면 항상 서버에서 최신 데이터를 가져옴
        // 로컬 캐시를 무시하므로 충돌 발생 확률이 높아짐
        var dataSource = forceNetworkRead ? DataSource.ReadNetworkOnly : DataSource.ReadCacheOrNetwork;
        
        Debug.Log($"[GPGS] OpenSavedGame with DataSource: {dataSource}");

        Debug.Log($"[GPGS] Calling OpenWithManualConflictResolution...");
        Debug.Log($"[GPGS] - filename: {filename}");
        Debug.Log($"[GPGS] - dataSource: {dataSource}");
        Debug.Log($"[GPGS] - prefetchDataOnConflict: true");
        
        savedGameClient.OpenWithManualConflictResolution(
            filename, 
            dataSource,
            prefetchDataOnConflict: true,
            conflictCallback: OnSaveGameConflict,
            completedCallback: OnSavedGameOpened
        );
    }
    
    private void OnSaveGameConflict(
        IConflictResolver resolver,
        ISavedGameMetadata original, byte[] originalData,
        ISavedGameMetadata unmerged, byte[] unmergedData)
    {
        Debug.Log("[GPGS] 3. SaveGame conflict detected");

        // 1) 나중에 UI에서 사용할 수 있도록 캐싱
        pendingResolver      = resolver;
        pendingOriginalMeta  = original;
        pendingOriginalData  = originalData;
        pendingUnmergedMeta  = unmerged;
        pendingUnmergedData  = unmergedData;

        // 2) JSON 디코드해서 게임 상태 비교용 정보 뽑기
        var originalInfo = DecodeGameInfo(originalData);
        var unmergedInfo = DecodeGameInfo(unmergedData);

        // 예: 레벨, 골드, 마지막 플레이 시간 등
        Debug.Log($"[GPGS] Original:  savedAtTicks={originalInfo?.savedAtTicks}");
        Debug.Log($"[GPGS] Unmerged: savedAtTicks={unmergedInfo?.savedAtTicks}");

        // 3) 여기서 “스팀 클라우드 스타일” 팝업을 띄운다.
        //    UI 쪽에서 Cloud / This Device 선택 버튼을 만들고,
        //    그 버튼이 아래 ResolveConflictUseOriginal / ResolveConflictUseUnmerged 를 호출하게 하면 된다.
        
        Debug.Log($"isSavingCurrentRequest: {isSavingCurrentRequest}");

        onSaveGameConflict?.Invoke(isSavingCurrentRequest, original, originalInfo, unmerged, unmergedInfo);
        
    }
    
    
    // 저장/로드 시 Saved Game Open 콜백
    public void OnSavedGameOpened(SavedGameRequestStatus status, ISavedGameMetadata gameMetadata) {
        
        Debug.Log($"[GPGS] 4. OnSavedGameOpened: {status}");
        
        if (status != SavedGameRequestStatus.Success)
        {
            Debug.LogError($"[GPGS] Failed to open saved game: {status}");
            
            // 작업 완료 플래그 해제
            isCloudOperationInProgress = false;

            if (isSavingCurrentRequest)
                onSaveToCloudFailed?.Invoke();
            else
                onLoadFromCloudFailed?.Invoke();

            return;
        }


        // 여기부터는 open 성공

        Debug.Log($"isSavingCurrentRequest: {isSavingCurrentRequest}");
        
        if (isSavingCurrentRequest)
        {
            try
            {
                // 1) 우선 overrideSaveGameInfo 가 있으면 그것을 사용
                GameInfo targetGameInfo = overrideSaveGameInfo;

                // 2) 없으면 기존처럼 InfoManager.Instance.GameInfo 사용
                if (targetGameInfo == null)
                {
                    if (InfoManager.Instance == null || InfoManager.Instance.GameInfo == null)
                    {
                        Debug.LogError("[GPGS] InfoManager or GameInfo is null. Cannot save.");
                        isCloudOperationInProgress = false;
                        onSaveToCloudFailed?.Invoke();
                        return;
                    }

                    targetGameInfo = InfoManager.Instance.GameInfo;
                }

                string gameInfoJson = JsonConvert.SerializeObject(targetGameInfo);
                Debug.Log($"[GPGS] Serialized GameInfo size: {gameInfoJson.Length} characters.");
                byte[] bytes = Encoding.UTF8.GetBytes(gameInfoJson);
                SaveGame(gameMetadata, bytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GPGS] Failed to serialize game data: {e.Message}");
                isCloudOperationInProgress = false;
                onSaveToCloudFailed?.Invoke();
            }
            finally
            {
                // 한 번 사용 후에는 반드시 비워줘야 다음 저장에 영향 없음
                overrideSaveGameInfo = null;
            }
        }
        else
        {
            // 로드 요청: 클라우드에서 데이터 읽기
            Debug.Log("[GPGS] Reading saved game data from cloud...");
            ISavedGameClient savedGameClient = PlayGamesPlatform.Instance.SavedGame;
            savedGameClient.ReadBinaryData(gameMetadata, OnSavedGameDataRead);
        }

    }
    
    // 클라우드 데이터 읽기 콜백 (LoadFromCloud용)
    private void OnSavedGameDataRead(SavedGameRequestStatus status, byte[] data)
    {
        Debug.Log($"[GPGS] 5. OnSavedGameDataRead: {status}");

        if (status != SavedGameRequestStatus.Success)
        {
            Debug.LogError($"[GPGS] Failed to read saved game data: {status}");
            isCloudOperationInProgress = false;
            onLoadFromCloudFailed?.Invoke();
            return;
        }

        // 데이터가 아예 없는 경우 (처음 저장 전 등)
        if (data == null || data.Length == 0)
        {
            Debug.Log("[GPGS] No data found in cloud (empty save).");
            isCloudOperationInProgress = false;
            onLoadFromCloudSuccess?.Invoke();
            return;
        }

        try
        {
            string gameInfoJson = Encoding.UTF8.GetString(data);
            Debug.Log($"[GPGS] Game data loaded from cloud. Json length: {gameInfoJson.Length}");

            // 실제 GameInfo 타입으로 역직렬화
            var loadedInfo = JsonConvert.DeserializeObject<GameInfo>(gameInfoJson);
            if (loadedInfo == null)
            {
                Debug.LogError("[GPGS] Deserialized GameInfo is null.");
                isCloudOperationInProgress = false;
                onLoadFromCloudFailed?.Invoke();
                return;
            }

            if (InfoManager.Instance == null)
            {
                Debug.LogError("[GPGS] InfoManager.Instance is null. Cannot apply loaded data.");
                isCloudOperationInProgress = false;
                onLoadFromCloudFailed?.Invoke();
                return;
            }
            
            Debug.Log($"[GPGS] 클라우드 데이터 savedAtTicks: {loadedInfo.savedAtTicks}");
            Debug.Log($"[GPGS] 로컬 데이터 savedAtTicks: {InfoManager.Instance.GameInfo.savedAtTicks}");
            
            // 주의: onLoadFromCloud 이벤트 후 UI에서 선택 완료 시 
            // NotifyLoadFromCloudSuccess/Failed가 호출되어야 isCloudOperationInProgress가 해제됨
            onLoadFromCloud?.Invoke(loadedInfo);    //loadedInfo : 클라우드 정보
        }
        catch (Exception e)
        {
            Debug.LogError($"[GPGS] Failed to deserialize game data: {e}");
            isCloudOperationInProgress = false;
            onLoadFromCloudFailed?.Invoke();
        }
    }

    
    void SaveGame (ISavedGameMetadata game, byte[] savedData) {
        
        Debug.Log($"[GPGS] SaveGame initiated. Data size: {savedData.Length}");
        
        ISavedGameClient savedGameClient = PlayGamesPlatform.Instance.SavedGame;

        SavedGameMetadataUpdate.Builder builder = new SavedGameMetadataUpdate.Builder();
        builder = builder
            .WithUpdatedDescription("Saved game at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        
        SavedGameMetadataUpdate updatedMetadata = builder.Build();
        savedGameClient.CommitUpdate(game, updatedMetadata, savedData, OnSavedGameWritten);
    }
    
    public void OnSavedGameWritten (SavedGameRequestStatus status, ISavedGameMetadata game) {
        
        Debug.Log($"[GPGS] 6. OnSavedGameWritten: {status}");
        
        // 작업 완료 플래그 해제
        isCloudOperationInProgress = false;
        
        if (status == SavedGameRequestStatus.Success) {
            Debug.Log("[GPGS] 클라우드 저장 완료");
            onSaveToCloudSuccess?.Invoke();
        } else {
            Debug.LogError($"[GPGS] 클라우드 저장 실패: {status}");
            onSaveToCloudFailed?.Invoke();
        }
    }
    
    private GameInfo DecodeGameInfo(byte[] data)
    {
        if (data == null || data.Length == 0)
            return null;

        try
        {
            string json = System.Text.Encoding.UTF8.GetString(data);
            return JsonConvert.DeserializeObject<GameInfo>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GPGS] DecodeGameInfo failed: {e}");
            return null;
        }
    }
    
    public void ResolveConflictUseOriginal()
    {
        if (pendingResolver == null || pendingOriginalMeta == null)
        {
            Debug.LogError("[GPGS] No pending conflict (Original).");
            // 에러 발생 시 작업 완료 플래그 해제 및 실패 이벤트 호출
            isCloudOperationInProgress = false;
            if (isSavingCurrentRequest)
                onSaveToCloudFailed?.Invoke();
            else
                onLoadFromCloudFailed?.Invoke();
            return;
        }

        Debug.Log("[GPGS] Resolving conflict: using Original metadata");
        // 참고: ChooseMetadata 후 OnSavedGameOpened가 다시 호출됨
        pendingResolver.ChooseMetadata(pendingOriginalMeta);
        ClearPendingConflict();
    }

    public void ResolveConflictUseUnmerged()
    {
        if (pendingResolver == null || pendingUnmergedMeta == null)
        {
            Debug.LogError("[GPGS] No pending conflict (Unmerged).");
            // 에러 발생 시 작업 완료 플래그 해제 및 실패 이벤트 호출
            isCloudOperationInProgress = false;
            if (isSavingCurrentRequest)
                onSaveToCloudFailed?.Invoke();
            else
                onLoadFromCloudFailed?.Invoke();
            return;
        }

        Debug.Log("[GPGS] Resolving conflict: using Unmerged metadata");
        // 참고: ChooseMetadata 후 OnSavedGameOpened가 다시 호출됨
        pendingResolver.ChooseMetadata(pendingUnmergedMeta);
        ClearPendingConflict();
    }

    private void ClearPendingConflict()
    {
        pendingResolver     = null;
        pendingOriginalMeta = null;
        pendingOriginalData = null;
        pendingUnmergedMeta = null;
        pendingUnmergedData = null;
    }
    
    public void CancelConflictAndFailSave()
    {
        ClearPendingConflict();
        
        // 작업 완료 플래그 해제
        isCloudOperationInProgress = false;
        
        // 요청 타입에 따라 적절한 실패 이벤트 호출
        if (isSavingCurrentRequest)
            onSaveToCloudFailed?.Invoke();
        else
            onLoadFromCloudFailed?.Invoke();
    }
    
    // --- 외부에서 이벤트 호출용 메서드 (UICloudConflictPanel 등에서 사용) ---
    
    /// <summary>
    /// 클라우드 로드 성공 이벤트를 외부에서 호출할 때 사용
    /// </summary>
    public void NotifyLoadFromCloudSuccess()
    {
        Debug.Log("[GPGS] NotifyLoadFromCloudSuccess");
        isCloudOperationInProgress = false;
        onLoadFromCloudSuccess?.Invoke();
    }
    
    /// <summary>
    /// 클라우드 로드 실패 이벤트를 외부에서 호출할 때 사용
    /// </summary>
    public void NotifyLoadFromCloudFailed()
    {
        Debug.Log("[GPGS] NotifyLoadFromCloudFailed");
        isCloudOperationInProgress = false;
        onLoadFromCloudFailed?.Invoke();
    }


}