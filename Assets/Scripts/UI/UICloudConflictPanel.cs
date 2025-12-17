using System;
using GooglePlayGames.BasicApi.SavedGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UICloudConflictPanel : MonoBehaviour
{
    [Header("Cloud Save UI")]
    public TMP_Text cloudTitleText;      // "Cloud Save"
    public TMP_Text cloudTimeText;       // "Modified ..."
    public TMP_Text cloudDetailText;     // 레벨/골드 등

    [Header("Local Save UI")]
    public TMP_Text localTitleText;      // "Local Save"
    public TMP_Text localTimeText;
    public TMP_Text localDetailText;

    [Header("Buttons")]
    public Button useCloudButton;
    public Button useLocalButton;
    public Button cancelButton;

    // 버튼 초기화 플래그 분리
    private bool simpleButtonsInitialized = false;
    private bool conflictButtonsInitialized = false;
    private bool saveConflictButtonsInitialized = false;
    
    // 현재 표시 중인 클라우드 GameInfo (클로저 캡처 문제 해결)
    private GameInfo currentCloudGameInfo;

    private void Awake()
    {
        // 버튼 초기화는 Show 메서드에서 수행
    }

    /// <summary>
    /// 단순 클라우드/로컬 선택용 버튼 초기화 (Show(GameInfo) 전용)
    /// </summary>
    private void InitSimpleButtons()
    {
        if (simpleButtonsInitialized) return;
        simpleButtonsInitialized = true;

        useCloudButton.onClick.AddListener(() =>
        {
            // currentCloudGameInfo를 사용하여 클로저 캡처 문제 해결
            if (currentCloudGameInfo != null)
            {
                InfoManager.Instance.GameInfo = currentCloudGameInfo;
                InfoManager.Instance.Save();  // 로컬 파일에도 저장!
                Debug.Log("[UICloudConflictPanel] Cloud data selected, applied, and saved to local.");
            }
            // 선택 완료 후 성공 이벤트 발생
            GPGSManager.Instance?.NotifyLoadFromCloudSuccess();
            Hide();
        });

        useLocalButton.onClick.AddListener(() =>
        {
            // 로컬 데이터 유지 (아무것도 안 함)
            Debug.Log("[UICloudConflictPanel] Local data selected (kept current).");
            // 선택 완료 후 성공 이벤트 발생
            GPGSManager.Instance?.NotifyLoadFromCloudSuccess();
            Hide();
        });

        cancelButton.onClick.AddListener(() =>
        {
            Debug.Log("[UICloudConflictPanel] Selection cancelled.");
            // 취소 시 실패 이벤트 발생
            GPGSManager.Instance?.NotifyLoadFromCloudFailed();
            Hide();
        });
    }

    /// <summary>
    /// 충돌 해결용 버튼 초기화 (Show with conflict 전용)
    /// </summary>
    private void InitConflictButtons()
    {
        if (conflictButtonsInitialized) return;
        conflictButtonsInitialized = true;

        useCloudButton.onClick.AddListener(() =>
        {
            GPGSManager.Instance.ResolveConflictUseOriginal(); // original 쪽을 클라우드라고 가정
            Hide();
        });

        useLocalButton.onClick.AddListener(() =>
        {
            GPGSManager.Instance.ResolveConflictUseUnmerged(); // unmerged 쪽을 로컬로 가정
            Hide();
        });

        cancelButton.onClick.AddListener(() =>
        {
            // 취소 시: pendingResolver는 남아있지만, 유저에게 다시 저장 시도하도록 유도하는 정도면 충분
            GPGSManager.Instance.CancelConflictAndFailSave();
            Hide();
        });
    }

    /// <summary>
    /// 단순 클라우드 데이터 선택 UI 표시 (충돌 없이 클라우드/로컬 선택만 필요할 때)
    /// </summary>
    /// <param name="gameInfo">클라우드에서 로드한 GameInfo</param>
    public void Show(GameInfo gameInfo)
    {
        // 충돌 버튼이 이미 초기화되어 있으면 리스너 정리 후 단순 버튼으로 재초기화
        if (conflictButtonsInitialized)
        {
            ClearAllButtonListeners();
            conflictButtonsInitialized = false;
            simpleButtonsInitialized = false;
        }
        
        InitSimpleButtons();
        
        // 현재 클라우드 데이터 저장 (클로저 캡처 문제 해결)
        currentCloudGameInfo = gameInfo;
        
        cloudTitleText.text = "Cloud Data";
        localTitleText.text = "Local Data";
        
        // 클라우드 데이터 정보 표시
        if (gameInfo != null)
        {
            var cloudDateTime = new DateTime(gameInfo.savedAtTicks);
            cloudTimeText.text = $"Saved {cloudDateTime:yyyy-MM-dd HH:mm}";
            var cloudStage = gameInfo.stageInfo?.currentStage ?? 0;
            cloudDetailText.text = $"Stage: {cloudStage}";
        }
        else
        {
            cloudTimeText.text = "(No data)";
            cloudDetailText.text = "";
        }
        
        // 로컬 데이터 정보 표시
        if (InfoManager.Instance != null && InfoManager.Instance.GameInfo != null)
        {
            var localInfo = InfoManager.Instance.GameInfo;
            var localDateTime = new DateTime(localInfo.savedAtTicks);
            localTimeText.text = $"Saved {localDateTime:yyyy-MM-dd HH:mm}";
            var localStage = localInfo.stageInfo?.currentStage ?? 0;
            localDetailText.text = $"Stage: {localStage}";
        }
        else
        {
            localTimeText.text = "(No data)";
            localDetailText.text = "";
        }
        
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 클라우드 저장 충돌 해결 UI 표시
    /// </summary>
    public void Show(bool isSavingCurrentRequest, ISavedGameMetadata original, GameInfo originalInfo,
                     ISavedGameMetadata unmerged, GameInfo unmergedInfo)
    {
        /*
         *  클라우드 저장 
            Original 선택 = 클라우드 유지
            Unmerged 선택 = 로컬 데이터를 클라우드에 엎어쓰기
            
            클라우드 불러오기 
            Original 선택 = 로컬 유지
            Unmerged 선택 = 클라우드 데이터를 로컬에 덮어쓰기
         */
        
        // 단순 버튼이 이미 초기화되어 있으면 리스너 정리 후 충돌 버튼으로 재초기화
        if (simpleButtonsInitialized)
        {
            ClearAllButtonListeners();
            simpleButtonsInitialized = false;
            conflictButtonsInitialized = false;
        }
        
        InitConflictButtons();
        
        Debug.Log($"[UICloudConflictPanel] Show : {isSavingCurrentRequest}");

        // Cloud Save 쪽
        if (isSavingCurrentRequest)
        {
            cloudTitleText.text = "Cloud Save";
        }
        else
        {
            cloudTitleText.text = "Local Data";
        }

        var cloudTime = original.LastModifiedTimestamp;
        cloudTimeText.text = $"Modified {cloudTime.ToLocalTime():yyyy-MM-dd HH:mm}";

        if (originalInfo != null)
        {
            var originalStage = originalInfo.stageInfo?.currentStage ?? 0;
            cloudDetailText.text = $"Stage: {originalStage}";
        }
        else
        {
            cloudDetailText.text = "(No data)";
        }

        // Local Save 쪽
        if (isSavingCurrentRequest)
        {
            localTitleText.text = "Local Save";    
        }
        else
        {
            localTitleText.text = "Cloud Data";
        }

        var localTime = unmerged.LastModifiedTimestamp;
        localTimeText.text = $"Modified {localTime.ToLocalTime():yyyy-MM-dd HH:mm}";

        if (unmergedInfo != null)
        {
            var unmergedStage = unmergedInfo.stageInfo?.currentStage ?? 0;
            localDetailText.text = $"Stage: {unmergedStage}";
        }
        else
        {
            localDetailText.text = "(No data)";
        }

        gameObject.SetActive(true);
    }

    /// <summary>
    /// 저장 충돌 UI 표시 (새로운 직접 비교 방식)
    /// </summary>
    /// <param name="cloudGameInfo">클라우드에 저장된 GameInfo</param>
    /// <param name="localGameInfo">로컬에 있는 GameInfo</param>
    public void ShowSaveConflict(GameInfo cloudGameInfo, GameInfo localGameInfo)
    {
        Debug.Log("[UICloudConflictPanel] ShowSaveConflict");
        
        // 다른 버튼이 초기화되어 있으면 정리
        if (simpleButtonsInitialized || conflictButtonsInitialized)
        {
            ClearAllButtonListeners();
            simpleButtonsInitialized = false;
            conflictButtonsInitialized = false;
            saveConflictButtonsInitialized = false;
        }
        
        InitSaveConflictButtons();
        
        currentCloudGameInfo = cloudGameInfo;
        
        // 클라우드 데이터 표시
        cloudTitleText.text = "Cloud Data";
        if (cloudGameInfo != null)
        {
            var cloudDateTime = new DateTime(cloudGameInfo.savedAtTicks);
            cloudTimeText.text = $"Saved {cloudDateTime:yyyy-MM-dd HH:mm}";
            var cloudStage = cloudGameInfo.stageInfo?.currentStage ?? 0;
            cloudDetailText.text = $"Stage: {cloudStage}";
        }
        else
        {
            cloudTimeText.text = "(No data)";
            cloudDetailText.text = "";
        }
        
        // 로컬 데이터 표시
        localTitleText.text = "Local Data";
        if (localGameInfo != null)
        {
            var localDateTime = new DateTime(localGameInfo.savedAtTicks);
            localTimeText.text = $"Saved {localDateTime:yyyy-MM-dd HH:mm}";
            var localStage = localGameInfo.stageInfo?.currentStage ?? 0;
            localDetailText.text = $"Stage: {localStage}";
        }
        else
        {
            localTimeText.text = "(No data)";
            localDetailText.text = "";
        }
        
        gameObject.SetActive(true);
    }
    
    /// <summary>
    /// 저장 충돌 해결용 버튼 초기화
    /// </summary>
    private void InitSaveConflictButtons()
    {
        if (saveConflictButtonsInitialized) return;
        saveConflictButtonsInitialized = true;
        
        // Cloud 선택 = 클라우드 데이터 유지, 저장 취소
        useCloudButton.onClick.AddListener(() =>
        {
            Debug.Log("[UICloudConflictPanel] Save conflict: Keep cloud data");
            GPGSManager.Instance?.ConfirmKeepCloudData();
            Hide();
        });
        
        // Local 선택 = 로컬 데이터로 클라우드 덮어쓰기
        useLocalButton.onClick.AddListener(() =>
        {
            Debug.Log("[UICloudConflictPanel] Save conflict: Use local data");
            GPGSManager.Instance?.ConfirmSaveWithLocalData();
            Hide();
        });
        
        // 취소
        cancelButton.onClick.AddListener(() =>
        {
            Debug.Log("[UICloudConflictPanel] Save conflict: Cancelled");
            GPGSManager.Instance?.CancelSaveToCloud();
            Hide();
        });
    }

    /// <summary>
    /// 모든 버튼 리스너 정리
    /// </summary>
    private void ClearAllButtonListeners()
    {
        useCloudButton.onClick.RemoveAllListeners();
        useLocalButton.onClick.RemoveAllListeners();
        cancelButton.onClick.RemoveAllListeners();
    }

    public void Hide()
    {
        currentCloudGameInfo = null;
        gameObject.SetActive(false);
    }
}
