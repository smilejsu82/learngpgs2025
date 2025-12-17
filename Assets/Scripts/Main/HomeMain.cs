using System;
using System.IO;
using GooglePlayGames.BasicApi.SavedGame;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HomeMain : MonoBehaviour
{
    public Button leaderboardButton;
    public Button achievementButton;

    public Button saveToCloudButton;
    public Button loadFromCloudButton;

    public Button getGoldButton;
    public TMP_Text goldText;

    public Button localGameInfoBackupButton;
    public Button localBackupGameInfoSaveToCloudButton;
    
    //public Button[] stageButtons;
    public UIStagePanel uiStagePanel;


    public UICloudConflictPanel uiCloudConflictPanel; 
    
    
    private void OnEnable()
    {
        if (GPGSManager.Instance == null) return;
            
        GPGSManager.Instance.onSaveToCloudSuccess += SaveToCloudEventHandler;
        GPGSManager.Instance.onSaveToCloudFailed += SaveToCloudFailedEventHandler;
        GPGSManager.Instance.onSaveGameConflict += SaveGameConflictEventHandler;
        GPGSManager.Instance.onSaveConflictDetected += SaveConflictDetectedEventHandler;
        GPGSManager.Instance.onLoadFromCloudFailed += LoadFromCloudFailedEventHandler;
        GPGSManager.Instance.onLoadFromCloudSuccess += LoadFromCloudSuccessEventHandler;
        GPGSManager.Instance.onLoadFromCloud += LoadFromCloudEventHandler;
    }
    
    /// <summary>
    /// 저장 시 충돌 감지 이벤트 핸들러 (새로운 방식)
    /// </summary>
    private void SaveConflictDetectedEventHandler(GameInfo cloudGameInfo, GameInfo localGameInfo)
    {
        Debug.Log("[HomeMain] SaveConflictDetectedEventHandler");
        Debug.Log($"[HomeMain] Cloud Stage: {cloudGameInfo?.stageInfo?.currentStage}, Local Stage: {localGameInfo?.stageInfo?.currentStage}");
        
        // 저장 충돌 UI 표시
        uiCloudConflictPanel.ShowSaveConflict(cloudGameInfo, localGameInfo);
    }

    private void LoadFromCloudEventHandler(GameInfo gameInfo)
    {
        uiCloudConflictPanel.Show(gameInfo); 
    }

    private void LoadFromCloudSuccessEventHandler()
    {
        loadFromCloudButton.interactable = true;
        Debug.Log("6. LoadFromCloudSuccessEventHandler");
        
        // 클라우드 데이터 로드 후 스테이지 패널 UI 업데이트
        uiStagePanel.UpdateUI();
    }

    private void LoadFromCloudFailedEventHandler()
    {
        loadFromCloudButton.interactable = true;
        Debug.Log("LoadFromCloudFailedEventHandler");
    }

    void Start()
    {

        AdmobManager.Instance.RequestBannerAd();
        
        uiStagePanel.onSelectStage = () =>
        {
            var ao = SceneManager.LoadSceneAsync("Game");
            ao.completed += (oper) =>
            {
                var gameMain = GameObject.FindFirstObjectByType<GameMain>();
                gameMain.Init();
            };
            
        };

        achievementButton.onClick.AddListener(() =>
        {
            GPGSManager.Instance.ShowAchievementsUI();    
        });
        
        leaderboardButton.onClick.AddListener(() =>
        {
            GPGSManager.Instance.ShowAllLeaderboardsUI();
        });
        
        loadFromCloudButton.onClick.AddListener(() =>
        {
            Debug.Log("1. Load From Cloud");
            loadFromCloudButton.interactable = false;
            GPGSManager.Instance.LoadFromCloud();
        });

        saveToCloudButton.onClick.AddListener(() =>
        {
            Debug.Log("클라우드 저장 버튼 클릭 됨");
            //글로벌 로딩 보여주기 
            saveToCloudButton.interactable = false;
            GPGSManager.Instance.SaveToCloud();
        });
        
        localGameInfoBackupButton.onClick.AddListener(() =>
        {
            GameInfo copiedGameInfo = InfoManager.Instance.GameInfo.DeepCopy();
            var backupGameInfoPath = Path.Combine(Application.persistentDataPath, "game_info_back.json");
            string backupGameInfoJson = JsonConvert.SerializeObject(copiedGameInfo);
            Debug.Log(backupGameInfoJson);
            File.WriteAllText(backupGameInfoPath,backupGameInfoJson);
            Debug.Log("copiedGameInfo가 저장되었습니다.");
        });
        
        localBackupGameInfoSaveToCloudButton.onClick.AddListener(() =>
        {
            var backupGameInfoPath = Path.Combine(Application.persistentDataPath, "game_info_back.json");
    
            if (!File.Exists(backupGameInfoPath))
            {
                Debug.LogWarning($"[HomeMain] Backup file not found: {backupGameInfoPath}");
                return;
            }

            try
            {
                string backupJson = File.ReadAllText(backupGameInfoPath);
                var backupGameInfo = JsonConvert.DeserializeObject<GameInfo>(backupJson);

                if (backupGameInfo == null)
                {
                    Debug.LogError("[HomeMain] Failed to deserialize backup GameInfo.");
                    return;
                }

                Debug.Log("[HomeMain] Uploading backup GameInfo to cloud...");
                // 필요하다면 버튼 비활성화도 같이
                saveToCloudButton.interactable = false;
                GPGSManager.Instance.SaveToCloud(backupGameInfo);
            }
            catch (Exception e)
            {
                Debug.LogError($"[HomeMain] Error while loading backup GameInfo: {e}");
            }
        });

        getGoldButton.onClick.AddListener(() =>
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Debug.Log("인터넷 연결 없음");
            }
            else
            {
                Debug.Log("인터넷 연결 가능");
                
                FirebaseManager.Instance.LogEventVersioned(AnalyticsEvent.GetGold100);
                
                AdmobManager.Instance.ShowRewardedAd(() =>
                {
                    InfoManager.Instance.GameInfo.gold += 100;
                    string formatted = $"{InfoManager.Instance.GameInfo.gold.ToString():N0}";
                    goldText.text = formatted;
                    InfoManager.Instance.Save();
                
                    Debug.Log("보상형 광고보기 이후 보상 성공");
                
                }, () =>
                {
                    Debug.Log("보상형 광고보기 이후 보상 받기 실패");
                });
            }
            
        });
        
        
        //UI초기화 
        string formatted = $"{InfoManager.Instance.GameInfo.gold.ToString():N0}";
        goldText.text = formatted;


    }

    private void SaveToCloudEventHandler()
    {
        //글로벌 로딩 가리기 
        saveToCloudButton.interactable = true;
        Debug.Log("SaveToCloudEventHandler");
    }

    private void SaveToCloudFailedEventHandler()
    {
        //글로벌 로딩 가리기 
        Debug.Log("SaveToCloudFailedEventHandler");
        saveToCloudButton.interactable = true;
    }


    private void OnDisable()
    {
        if (GPGSManager.Instance == null) return;
        
        GPGSManager.Instance.onSaveToCloudSuccess -= SaveToCloudEventHandler;
        GPGSManager.Instance.onSaveToCloudFailed -= SaveToCloudFailedEventHandler;
        GPGSManager.Instance.onSaveGameConflict -= SaveGameConflictEventHandler;
        GPGSManager.Instance.onSaveConflictDetected -= SaveConflictDetectedEventHandler;
        GPGSManager.Instance.onLoadFromCloudFailed -= LoadFromCloudFailedEventHandler;
        GPGSManager.Instance.onLoadFromCloudSuccess -= LoadFromCloudSuccessEventHandler;
        GPGSManager.Instance.onLoadFromCloud -= LoadFromCloudEventHandler;
    }

    private void SaveGameConflictEventHandler(bool isSavingCurrentRequest, ISavedGameMetadata original, GameInfo originalInfo, 
        ISavedGameMetadata unmerged, GameInfo unmergedInfo)
    {
        Debug.Log($"[SaveGameConflictEventHandler] : original.LastModifiedTimestamp : {original.LastModifiedTimestamp}, unmerged.LastModifiedTimestamp : {unmerged.LastModifiedTimestamp}");
        
        uiCloudConflictPanel.Show(isSavingCurrentRequest, original,  originalInfo, unmerged, unmergedInfo);
        
    }
}
