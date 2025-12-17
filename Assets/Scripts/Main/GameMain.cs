using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;

public class GameMain : MonoBehaviour
{
    public Hero hero;
    public Button leftButton;
    public Button rightButton;
    public Button restartButton;
    public Button backButton;
    public Button gameOverButton;
    public TMP_Text stageText;
    public Image targetImage;
    public SpriteAtlas atlas;
    
    [Header("Score")]
    public TMP_Text scoreText;
    public int damageScore = 1;
    public int killScore = 50;
    public int mergeScore = 100;        // 합치기 점수
    
    [Header("Hero Level/XP")]
    public TMP_Text levelText;
    public TMP_Text xpText;
    public int xpPerKill = 10;          // 몬스터 킬당 지급할 경험치
    
    [Header("Game Over - Suika Style")]
    public float baseDangerLineY = 4.5f;      // Lv1 기준 라인 높이
    public float minDangerLineY = -1.23f;     // 최대로 내려갈 수 있는 높이
    public int maxLevelForMinDanger = 10;     // 이 레벨 이상이면 항상 minDangerLineY
    [NonSerialized]
    public float dangerLineY;                 // 실제 사용 중인 라인 높이

    public float velocityThreshold = 0.5f;
    public LineRenderer dangerLineRenderer;
    public GameObject gameOverUI;
    public MonsterGenerator monsterGenerator;
    
    [SerializeField]
    private float overflowGraceTime = 0.5f;   // 스폰 후 0.5초는 GameOver 판정에서 제외

    [SerializeField] private UILevelUpPopup uiLevelUpPopup;
    [SerializeField] private UIClearPopup uiClearPopup;
    [SerializeField] private UITargetPopup uiTargetPopup;
    private int score = 0;
    private bool isGameOver = false;
    private float moveX = 0f; 
    
    
    //몬스터 슬레이어 업적용 테스트 저장 
    private int monsterKillCount = 0;          // 총 킬 수
    private int monsterSlayerStage = 0;        // 현재까지 달성한 업적 단계 (0~4)

    private const string MonsterKillCountKey = "monsterKillCount";
    private const string MonsterSlayerStageKey = "monsterSlayerStage";
    private const string FirstGameOverKey    = "isFirstGameOver";


    private void Awake()
    {
        Application.runInBackground = true;
    }

    public void Init()
    {
        
    }

    private void Start()
    {
        AddPressEvent(leftButton, () => moveX = -1f, () => moveX = 0f);
        AddPressEvent(rightButton, () => moveX = 1f, () => moveX = 0f);
        
        // 안전장치: GameInfo가 null이면 여기서라도 만들어주기
        if (InfoManager.Instance.GameInfo == null)
        {
            if (InfoManager.Instance.IsNewbie())
                InfoManager.Instance.CreateGameInfo();
            else
                InfoManager.Instance.Load();
        }

        stageText.text = $"Stage {InfoManager.Instance.GameInfo.stageInfo.currentStage}";
        targetImage.sprite = atlas.GetSprite($"Icon{InfoManager.Instance.GameInfo.stageInfo.currentStage}_0");
        
        gameOverButton.onClick.AddListener(() =>
        {
            Debug.Log("onclick gameOverButton");
            GameOver();
        });
        
        backButton.onClick.AddListener(() =>
        {
            SceneManager.LoadScene("Home");
        });
        
        restartButton.onClick.AddListener(() =>
        {
            SceneManager.LoadScene("Game");
        });
        
        // 업적 관련 저장 값 로드 (게임 시작 시 한 번)
        monsterKillCount = PlayerPrefs.GetInt(MonsterKillCountKey, 0);
        monsterSlayerStage = PlayerPrefs.GetInt(MonsterSlayerStageKey, 0);
        Debug.Log($"[Init] monsterKillCount: {monsterKillCount}, stage: {monsterSlayerStage}");
        

        // Hero 공격 이벤트
        hero.onAttackEvent = (mon) =>
        {
            if (mon == null || isGameOver)
                return;

            if (mon.gameObject == null)
                return;

            int healthBefore = mon.GetCurrentHealth();
            
            // 데미지 적용
            mon.TakeDamage(hero.attackPower);
            
            // 데미지 점수
            AddScore(damageScore);
            
            // 킬 점수 (죽었는지 체크)
            if (healthBefore > 0 && mon.GetCurrentHealth() <= 0)
            {
                AddScore(killScore);
                
                // 몬스터 킬 시 Hero에게 경험치 지급
                if (hero != null)
                {
                    hero.AddXP(xpPerKill);
                }
                monsterKillCount++;
                PlayerPrefs.SetInt("monsterKillCount", monsterKillCount);
                PlayerPrefs.Save();
                Debug.Log("monsterKillCount: " + monsterKillCount);
                UpdateAchievementMonsterKillCount();
                
                //스테이지에 클리어 확인 
                //현재 스테이지 번호와 죽인 몬스터의 번호가 같으면 클리어  
                Debug.Log($"{mon.type} == {InfoManager.Instance.GameInfo.stageInfo.currentStage}");
                if (mon.type == InfoManager.Instance.GameInfo.stageInfo.currentStage)
                {
                    Debug.Log("Clear!!!");
                    this.ClearStage();
                }
            }
        };
        
        // Hero의 레벨/XP 이벤트 구독
        if (hero != null)
        {
            hero.onLevelUp += UpdateLevelUI;
            hero.onLevelUp += ShowLevelUpPopup;
            hero.onXPChange += UpdateLevelUI;
        }
        
        // 몬스터 합치기 이벤트
        Monster.onMonsterMerged = (monsterType, position) =>
        {
            if (isGameOver)
                return;

            int score = mergeScore * monsterType;
            AddScore(score);
            Debug.Log($"Monster merged! Type {monsterType} → {monsterType + 1}, Score +{score}");
        };
        
        UpdateScoreUI();
        UpdateLevelUI(); // 초기 레벨 UI 업데이트
        
        if (gameOverUI != null)
            gameOverUI.SetActive(false);
        
        // Danger line 설정 (Hero 레벨 반영)
        SetupDangerLine();
        
        if (monsterGenerator != null)
        {
            monsterGenerator.onBeforeSpawn = CheckOverflowBeforeSpawn;
        }
        
        //레벨업 팝업 이벤트 부착 
        uiLevelUpPopup.skipButton.onClick.AddListener(() => { });
        for (int i = 0; i < uiLevelUpPopup.upgradeButtons.Length; i++)
        {
            var idx = i;
            var upgradeButton =  uiLevelUpPopup.upgradeButtons[idx];
            upgradeButton.onClick.AddListener(() =>
            {
                Debug.Log($"upgrade selected: {idx}");

                switch (idx)
                {
                    case 0:
                        hero.UpgradeAttackPower();
                        break;
                    
                    case 1:
                        hero.UpgradeAttackSpeed();
                        break;
                    
                    case 2:
                        hero.UpgradeAttackDelay();
                        break;
                }
                
                uiLevelUpPopup.Close();
            });
        }

        uiTargetPopup.onClick = () =>
        {
            StartStage();
        };
        uiTargetPopup.Open();

    }

    private float stageStartTime;
    
    private void StartStage()
    {
        stageStartTime = Time.time;

        FirebaseManager.Instance.LogEventVersioned(
            AnalyticsEvent.StartStage,
            new Firebase.Analytics.Parameter("stage", InfoManager.Instance.GameInfo.stageInfo.currentStage)
        );
    }

    private void ClearStage()
    {
        Debug.Log("<color=lime>Clear</color>");
        isGameOver = true;
        InfoManager.Instance.GameInfo.stageInfo.currentStage++;
        InfoManager.Instance.Save();
        uiClearPopup.Open();
        SaveHighScore();
        
        float elapsed = Time.time - stageStartTime;

        FirebaseManager.Instance.LogEventVersioned(
            AnalyticsEvent.ClearStage,
            new Firebase.Analytics.Parameter("stage", InfoManager.Instance.GameInfo.stageInfo.currentStage - 1),
            new Firebase.Analytics.Parameter("clear_time", elapsed)
        );
    }
    
    public void FailStage()
    {
        float elapsed = Time.time - stageStartTime;

        FirebaseManager.Instance.LogEventVersioned(
            AnalyticsEvent.FailStage,
            new Firebase.Analytics.Parameter("stage", InfoManager.Instance.GameInfo.stageInfo.currentStage),
            new Firebase.Analytics.Parameter("elapsed", elapsed)
        );
    }


    // 몬스터 슬레이어 업적: 4단계 (1,3,6,10킬)
    private void UpdateAchievementMonsterKillCount()
    {
        // 현재 킬 수로부터 이론상 단계 계산 (0~4)
        int newStage = CalculateMonsterSlayerStage(monsterKillCount);

        // 이미 이 단계 이상이면 아무것도 하지 않음
        if (newStage <= monsterSlayerStage)
            return;

        // GPGS IncrementAchievement에 넘길 step 수 = (새 단계 - 기존 단계)
        int stepsToAdd = newStage - monsterSlayerStage;

        Debug.Log($"UpdateAchievementMonsterKillCount: kills={monsterKillCount}, stage {monsterSlayerStage} -> {newStage}, +{stepsToAdd} steps");

        monsterSlayerStage = newStage;
        PlayerPrefs.SetInt(MonsterSlayerStageKey, monsterSlayerStage);
        PlayerPrefs.Save();

        if (GPGSManager.Instance != null)
        {
            GPGSManager.Instance.IncrementAchievement(
                GPGSIds.achievement_2,
                stepsToAdd,
                success =>
                {
                    Debug.Log($"Increment achievement +{stepsToAdd} step success: {success}");
                });
        }
    }

// 킬 수에 따른 단계 계산
// 0킬   → 0단계
// 1킬   → 1단계
// 3킬   → 2단계
// 6킬   → 3단계
// 10킬~ → 4단계
    private int CalculateMonsterSlayerStage(int killCount)
    {
        if (killCount >= 10) return 4;
        if (killCount >= 6)  return 3;
        if (killCount >= 3)  return 2;
        if (killCount >= 1)  return 1;
        return 0;
    }



    private void Update()
    {
        if (isGameOver)
            return;
    
        // 매 프레임 오버플로우 체크
        if (IsOverflowing())
        {
            Debug.Log("GameOver by overflow (Update loop)");
            GameOver();
            return;
        }
    
        UpdateDangerLineColor();
    
        float keyboard = Input.GetAxisRaw("Horizontal");
        float finalX = keyboard != 0 ? keyboard : moveX;

        if (finalX != 0)
            hero.Move(new Vector2(finalX, 0));
        else
            hero.Idle();
    }

    private void SetupDangerLine()
    {
        if (dangerLineRenderer == null)
        {
            GameObject lineObj = new GameObject("DangerLine");
            dangerLineRenderer = lineObj.AddComponent<LineRenderer>();

            dangerLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            dangerLineRenderer.startWidth = 0.05f;
            dangerLineRenderer.endWidth = 0.05f;

            dangerLineRenderer.startColor = new Color(1f, 0.5f, 0f, 0.7f);
            dangerLineRenderer.endColor = new Color(1f, 0.5f, 0f, 0.7f);

            dangerLineRenderer.sortingOrder = 100;
        }

        // Hero 레벨을 반영해서 dangerLineY 갱신 후 실제 라인 위치도 갱신
        UpdateDangerLineByHeroLevel();
    }

    private void UpdateDangerLineByHeroLevel()
    {
        float targetY = baseDangerLineY;

        if (hero != null)
        {
            int level = Mathf.Max(hero.level, 1);

            // level 1 → baseDangerLineY
            // level maxLevelForMinDanger 이상 → minDangerLineY
            float t = 0f;
            if (maxLevelForMinDanger > 1)
            {
                t = Mathf.Clamp01((level - 1f) / (maxLevelForMinDanger - 1f));
            }

            targetY = Mathf.Lerp(baseDangerLineY, minDangerLineY, t);
        }

        dangerLineY = targetY;

        if (dangerLineRenderer != null)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                float halfWidth = cam.orthographicSize * cam.aspect;
                dangerLineRenderer.positionCount = 2;
                dangerLineRenderer.SetPosition(0, new Vector3(-halfWidth, dangerLineY, 0));
                dangerLineRenderer.SetPosition(1, new Vector3(halfWidth, dangerLineY, 0));
            }
        }
    }

    private void UpdateDangerLineColor()
    {
        if (dangerLineRenderer == null)
            return;
        
        bool isOverflowing = IsOverflowing();
        
        if (isOverflowing)
        {
            float alpha = 0.5f + 0.3f * Mathf.Sin(Time.time * 10f);
            Color dangerColor = new Color(1f, 0f, 0f, alpha);
            dangerLineRenderer.startColor = dangerColor;
            dangerLineRenderer.endColor = dangerColor;
        }
        else
        {
            Color normalColor = new Color(1f, 0.5f, 0f, 0.7f);
            dangerLineRenderer.startColor = normalColor;
            dangerLineRenderer.endColor = normalColor;
        }
    }

    private bool CheckOverflowBeforeSpawn()
    {
        if (isGameOver)
            return false;
        
        bool hasStillMonsterAboveLine = IsOverflowing();
        
        if (hasStillMonsterAboveLine)
        {
            Debug.Log("Game Over! Monster above danger line when trying to spawn!");
            GameOver();
            return false;
        }
        
        return true;
    }

    private bool IsOverflowing()
    {
        GameObject[] monsters = GameObject.FindGameObjectsWithTag("Monster");
    
        foreach (GameObject mon in monsters)
        {
            if (mon == null)
                continue;

            float monsterTop = GetMonsterTopPosition(mon);

            if (monsterTop > dangerLineY)
            {
                Rigidbody2D rb = mon.GetComponent<Rigidbody2D>();
                if (rb == null)
                    continue;

                // Monster 컴포넌트에서 spawnTime 가져오기
                Monster monsterComp = mon.GetComponent<Monster>();
                if (monsterComp != null)
                {
                    // 스폰된지 얼마 안 된 몬스터는 무시 (떨어지는 중이라 가정)
                    if (Time.time - monsterComp.spawnTime < overflowGraceTime)
                        continue;
                }

                // 충분히 오래 있었는데도 거의 안 움직이면 GameOver 판정
                if (rb.linearVelocity.magnitude < velocityThreshold)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private float GetMonsterTopPosition(GameObject monster)
    {
        float topY = monster.transform.position.y;
        
        Collider2D col = monster.GetComponent<Collider2D>();
        if (col != null)
        {
            topY = col.bounds.max.y;
            return topY;
        }
        
        SpriteRenderer sr = monster.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            topY = sr.bounds.max.y;
            return topY;
        }
        
        return topY;
    }

    private void GameOver()
    {
        if (isGameOver)
            return;

        isGameOver = true;

        if (monsterGenerator != null)
            monsterGenerator.enabled = false;

        if (hero != null)
            hero.OnGameOver();

        if (gameOverUI != null)
            gameOverUI.SetActive(true);
    
        // 이벤트 해제
        Monster.onMonsterMerged = null;
    
        Debug.Log($"Game Over! Final Score: {score:N0}");

        if (GPGSManager.Instance != null)
        {
            GPGSManager.Instance.PostScore(this.score);    
        }
    
        // 뉴비 탈출 업적: 첫 게임오버 시 1회만 달성
        int isFirstGameOver = PlayerPrefs.GetInt(FirstGameOverKey, 0);
        if (isFirstGameOver == 0)
        {
            PlayerPrefs.SetInt(FirstGameOverKey, 1);
            PlayerPrefs.Save();

            if (GPGSManager.Instance != null)
            {
                // 단발성 업적이라면 Unlock 방식
                GPGSManager.Instance.UnlockAchievement(
                    GPGSIds.achievement,
                    success =>
                    {
                        Debug.Log($"Newbie Escape achievement unlock: {success}");
                    });
            }
        }
        
        
        //로컬 저장
        SaveHighScore();

        FailStage();
    }

    private void SaveHighScore()
    {
        Debug.Log($"score: {score} vs GameInfo.scoreInfo.highScore: {InfoManager.Instance.GameInfo.scoreInfo.highScore}");
        
        if (this.score > InfoManager.Instance.GameInfo.scoreInfo.highScore)
        {
            InfoManager.Instance.UpdateHighScore(this.score);
            InfoManager.Instance.Save();
        }
        else
        {
            Debug.Log("최고점수가 아닙니다.");
        }
    }


    private void OnDestroy()
    {
        // 씬 전환 시 정적 이벤트 정리
        Monster.onMonsterMerged = null;
        
        // Hero 이벤트 구독 해제
        if (hero != null)
        {
            hero.onLevelUp -= UpdateLevelUI;
            hero.onLevelUp -= ShowLevelUpPopup;
            hero.onXPChange -= UpdateLevelUI;
        }
    }

    public void RestartGame()
    {
        Monster.onMonsterMerged = null;
        
        SceneManager.LoadScene(
            SceneManager.GetActiveScene().name
        );
    }

    private void AddScore(int points)
    {
        if (isGameOver)
            return;
        
        score += points;
        UpdateScoreUI();
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = score.ToString("N0");
        }
    }
    
    // Hero의 레벨과 경험치를 UI에 표시하는 메서드
    private void UpdateLevelUI()
    {
        if (hero == null) return;
        
        if (levelText != null)
        {
            levelText.text = $"Lv.{hero.level}";
        }
        
        if (xpText != null)
        {
            xpText.text = $"XP: {hero.currentXP:N0} / {hero.xpToNextLevel:N0}";
        }

        // 레벨/XP 변경 시 danger line도 재계산
        UpdateDangerLineByHeroLevel();
    }

    private void ShowLevelUpPopup()
    {
        Debug.Log("Show Level Up Popup");
        uiLevelUpPopup.Open();
    }

    public int GetScore()
    {
        return score;
    }

    private void AddPressEvent(Button button, Action onDown, Action onUp)
    {
        if (button == null)
            return;

        var trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        var down = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerDown
        };
        down.callback.AddListener(_ => onDown());
        trigger.triggers.Add(down);

        var up = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerUp
        };
        up.callback.AddListener(_ => onUp());
        trigger.triggers.Add(up);
    }


    private void OnDrawGizmos()
    {
        bool isOverflowing = Application.isPlaying && IsOverflowing();
        Gizmos.color = isOverflowing ? Color.red : new Color(1f, 0.5f, 0f, 0.5f);
        Camera cam = Camera.main;
        if (cam != null)
        {
            float halfWidth = cam.orthographicSize * cam.aspect;
            Vector3 left = new Vector3(-halfWidth, dangerLineY, 0);
            Vector3 right = new Vector3(halfWidth, dangerLineY, 0);
            Gizmos.DrawLine(left, right);
            
            if (Application.isPlaying)
            {
                GameObject[] monsters = GameObject.FindGameObjectsWithTag("Monster");
                foreach (GameObject mon in monsters)
                {
                    if (mon == null) continue;

                    float topY = GetMonsterTopPosition(mon);
                    if (topY > dangerLineY)
                    {
                        Rigidbody2D rb = mon.GetComponent<Rigidbody2D>();
                        if (rb != null && rb.linearVelocity.magnitude < velocityThreshold)
                        {
                            Gizmos.color = Color.red;
                            Gizmos.DrawWireSphere(mon.transform.position, 0.5f);
                        }
                        else
                        {
                            Gizmos.color = Color.yellow;
                            Gizmos.DrawWireCube(mon.transform.position, Vector3.one * 0.5f);
                        }
                    }
                }
            }
        }
    }

    
}
