using UnityEngine;

public class Hero : MonoBehaviour
{
    public static Hero Instance;
    public const int DefaultAttackPower = 20;
    public const float DefaultSpeed = 1.0f;
    public const float DefaultAttackDelay = 0.5f;
    public const int DefaultXpToNextLevel = 100;

    public int attackPower = DefaultAttackPower;
    public float speed = DefaultSpeed;
    public float attackDelay = DefaultAttackDelay;
    public int xpToNextLevel = DefaultXpToNextLevel;
    
    
    private Animator anim;
    private Vector2 dir;

    private float minX;
    private float maxX;

    private bool isAttacking = false;
    private bool canAttack = true;
    
    [Header("Hero Level/XP")] // ✨ 추가: 레벨 및 경험치 관련 필드
    public int level = 1;
    public int currentXP = 0;
    public float xpToNextLevelMultiplier = 1.5f; // 다음 레벨업 필요 경험치 증가 배율
    public int attackPowerIncreasePerLevel = 5; // 레벨업당 공격력 증가량

    [Header("Animation Speed")]
    public float baseSpeed = 1.0f;          // 기본 이동속도 (애니메이션 속도 계산용)
    public float baseAttackDelay = 0.5f;    // 기본 공격 딜레이 (애니메이션 속도 계산용)

    [Header("Game State")]
    private bool isGameOver = false;        // 게임 오버 상태

    // ✨ 추가: 레벨/XP 변경 시 GameMain에 알릴 이벤트
    public System.Action onLevelUp;
    public System.Action onXPChange; 
    public System.Action<Monster> onAttackEvent;
    
    private Monster currentTarget;          // 현재 타겟
    private bool isTargetLocked = false;    // 타겟 고정 여부
    
    [Header("Hit Effects By Level")]
    public GameObject[] hitEffectPrefabsByLevel; 
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    
    void Start()
    {
        anim = GetComponent<Animator>();

        float halfWidth = Camera.main.orthographicSize * Camera.main.aspect;
        float heroHalfWidth = GetComponent<SpriteRenderer>().bounds.extents.x;

        minX = -halfWidth + heroHalfWidth;
        maxX = halfWidth - heroHalfWidth;

        // 세이브된 캐릭터 스탯 적용
        if (!InfoManager.Instance.IsNewbie())
        {
            // 아직 아무도 Load 안 했으면 여기서라도 로드
            if (InfoManager.Instance.GameInfo == null)
            {
                InfoManager.Instance.Load();
            }

            var ci = InfoManager.Instance.GameInfo.characterInfo;
            if (ci == null)
            {
                ci = new CharacterInfo();
                InfoManager.Instance.GameInfo.characterInfo = ci;
            }

            level         = ci.level;
            currentXP     = ci.currentXP;
            xpToNextLevel = ci.xpToNextLevel;

            attackPower   = ci.attackPower;
            speed         = ci.speed;
            attackDelay   = ci.attackDelay;
        }

        UpdateAnimationSpeed();
    }


    
    void Update()
    {
        if (isGameOver)
            return;

        if (dir != Vector2.zero)
        {
            Vector3 pos = transform.position;
            pos += (Vector3)dir * speed * Time.deltaTime;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            transform.position = pos;
        }

        if (currentTarget != null && (currentTarget.gameObject == null || currentTarget.GetCurrentHealth() <= 0))
        {
            Debug.Log("Current target is dead or destroyed. Unlocking target.");
            currentTarget = null;
            isTargetLocked = false;
        }
    }

    // ✨ 추가: 경험치 획득 및 레벨업 로직
    public void AddXP(int xp)
    {
        if (isGameOver) return;
        
        currentXP += xp;
        onXPChange?.Invoke();

        while (currentXP >= xpToNextLevel)
        {
            LevelUp();
        }

        // 레벨업이 안 되어도, 경험치만 올라간 상태를 저장하고 싶다면
        InfoManager.Instance.SaveCharacterInfoFromHero(this);
    }

    
    // ✨ 추가: 레벨업 처리 로직
    private void LevelUp()
    {
        level++;
        currentXP -= xpToNextLevel;

        xpToNextLevel = Mathf.RoundToInt(xpToNextLevel * xpToNextLevelMultiplier / 10f) * 10;

        onLevelUp?.Invoke();

        // 레벨업 정보 저장
        InfoManager.Instance.SaveCharacterInfoFromHero(this);

        Debug.Log($"Hero Leveled Up to Level {level}! Attack Power increased to {attackPower}. Next XP: {xpToNextLevel}");
    }


    public void UpgradeAttackPower()
    {
        attackPower += attackPowerIncreasePerLevel;
        InfoManager.Instance.SaveCharacterInfoFromHero(this);
    }

    public void UpgradeAttackSpeed()
    {
        speed *= 1.05f;
        UpdateAnimationSpeed();
        InfoManager.Instance.SaveCharacterInfoFromHero(this);
    }

    public void UpgradeAttackDelay()
    {
        attackDelay *= 0.95f;
        UpdateAnimationSpeed();
        InfoManager.Instance.SaveCharacterInfoFromHero(this);
    }


    // 게임 오버 시 호출
    public void OnGameOver()
    {
        isGameOver = true;
        isAttacking = false;
        isTargetLocked = false;
        currentTarget = null;
        dir = Vector2.zero;
        
        CancelInvoke();
        
        anim.SetInteger("State", 0);
        anim.speed = 1.0f;
        
        Debug.Log("Hero stopped - Game Over");
    }

    // 이동속도나 공격속도가 변경되었을 때 호출
    public void UpdateAnimationSpeed()
    {
        float moveAnimSpeed = speed / baseSpeed;
        float attackAnimSpeed = baseAttackDelay / attackDelay;
        
        anim.speed = 1.0f;
        
        Debug.Log($"Animation Speed - Move: {moveAnimSpeed:F2}x, Attack: {attackAnimSpeed:F2}x");
    }

    // 업그레이드 메서드들 (레벨업 시스템이 도입되면서 직접 호출은 줄어들 수 있음)
    public void UpgradeSpeed(float newSpeed)
    {
        speed = newSpeed;
        UpdateAnimationSpeed();
        Debug.Log($"Speed upgraded to: {speed}");
    }

    public void UpgradeAttackDelay(float newDelay)
    {
        attackDelay = newDelay;
        UpdateAnimationSpeed();
        Debug.Log($"Attack delay upgraded to: {attackDelay}");
    }

    public void UpgradeAttackPower(int newPower)
    {
        attackPower = newPower;
        Debug.Log($"Attack power upgraded to: {attackPower}");
    }

    public void Idle()
    {
        if (isGameOver)
            return;

        dir = Vector2.zero;

        if (isAttacking)
            return;

        anim.SetInteger("State", 0); // Idle
        anim.speed = 1.0f;
    }

    public void Move(Vector2 dir)
    {
        if (isGameOver)
            return;

        isAttacking = false;
        isTargetLocked = false;
        currentTarget = null;

        this.dir = dir;

        if (dir.x != 0)
        {
            float sign = dir.x > 0 ? 1f : -1f;
            transform.localScale = new Vector3(sign, 1, 1);
        }

        anim.SetInteger("State", 1); // Run
        
        float moveAnimSpeed = speed / baseSpeed;
        anim.speed = moveAnimSpeed;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (isGameOver)
            return;

        if (!other.CompareTag("Monster"))
            return;

        if (dir != Vector2.zero)
        {
            isAttacking = false;
            isTargetLocked = false;
            currentTarget = null;
            return;
        }

        if (!isTargetLocked || currentTarget == null || currentTarget.gameObject == null)
        {
            Monster monster = other.GetComponent<Monster>();
            if (monster != null)
            {
                currentTarget = monster;
                isTargetLocked = true;
                Debug.Log($"Target locked: {monster.gameObject.name}");
            }
        }

        if (currentTarget == null)
        {
            isAttacking = false;
            return;
        }

        isAttacking = true;

        float monsterX = currentTarget.transform.position.x;
        transform.localScale = (monsterX > transform.position.x)
            ? new Vector3(1, 1, 1)
            : new Vector3(-1, 1, 1);

        if (canAttack)
        {
            Attack();
        }
    }


    private void OnTriggerExit2D(Collider2D other)
    {
        if (isGameOver)
            return;

        if (!other.CompareTag("Monster"))
            return;

        Monster monster = other.GetComponent<Monster>();
        if (monster == currentTarget)
        {
            Debug.Log($"Target exited: {monster.gameObject.name}");
            isAttacking = false;
            isTargetLocked = false;
            currentTarget = null;

            if (dir == Vector2.zero)
            {
                anim.SetInteger("State", 0);
                anim.speed = 1.0f;
            }
            else
            {
                anim.SetInteger("State", 1);
                float moveAnimSpeed = speed / baseSpeed;
                anim.speed = moveAnimSpeed;
            }
        }
    }

    private void Attack()
    {
        if (isGameOver)
            return;

        canAttack = false;

        anim.SetInteger("State", 2); // Attack
        
        float attackAnimSpeed = baseAttackDelay / attackDelay;
        anim.speed = attackAnimSpeed;

        Invoke(nameof(ResetAttack), attackDelay);
    }

    private void ResetAttack()
    {
        if (isGameOver)
            return;

        canAttack = true;

        if (!isAttacking)
        {
            if (dir == Vector2.zero)
            {
                anim.SetInteger("State", 0);
                anim.speed = 1.0f;
            }
            else
            {
                anim.SetInteger("State", 1);
                float moveAnimSpeed = speed / baseSpeed;
                anim.speed = moveAnimSpeed;
            }
        }
    }

    // 공격 애니메이션의 이벤트 함수 (Animation Event로 호출)
    public void OnAttackImpact()
    {
        if (isGameOver)
            return;

        Debug.Log("Attack Impact");
    
        if (currentTarget == null || currentTarget.gameObject == null)
        {
            Debug.Log("Target is invalid at attack impact.");
            currentTarget = null;
            isTargetLocked = false;
            return;
        }
    
        // GameMain.cs로 공격 이벤트를 전달
        onAttackEvent?.Invoke(currentTarget);
    
        // 공격 후 타겟의 생존 여부를 다시 체크
        if (currentTarget != null && currentTarget.GetCurrentHealth() <= 0)
        {
            Debug.Log("Target killed. Unlocking target.");
            currentTarget = null;
            isTargetLocked = false;
        }
    }
    
    public GameObject GetHitEffectPrefab()
    {
        // 배열이 비어있으면 null 반환
        if (hitEffectPrefabsByLevel == null || hitEffectPrefabsByLevel.Length == 0)
            return null;

        // Hero.level 은 사람 기준 1부터 시작하니까 index 는 level - 1
        int index = level - 1;

        // 범위 안전 처리
        if (index < 0)
            index = 0;

        if (index >= hitEffectPrefabsByLevel.Length)
            index = hitEffectPrefabsByLevel.Length - 1;

        return hitEffectPrefabsByLevel[index];
    }

}