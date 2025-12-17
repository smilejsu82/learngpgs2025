using UnityEngine;
using System.Collections;

public class Monster : MonoBehaviour
{
    public int type = 1;
    public GameObject nextTypePrefab;
    
    [Header("Health")]
    public int maxHealth = 100;
    private int currentHealth;

    [Header("Health Scaling By Hero Level")]
    // Hero 레벨에 따라 체력 스케일링 할지 여부
    public bool scaleWithHeroLevel = true;

    // 레벨 1을 기준으로, 레벨이 1 오를 때마다 몇 %씩 증가할지
    // 예: 0.1f → 레벨당 +10% (Lv3면 +20%)
    public float healthIncreasePerLevel = 0.1f;

    [Header("Hit Effect")]
    public float flashDuration = 0.1f;
    public GameObject hitPrefab;   // 히트 이펙트 프리팹

    [Header("Knockback")]
    public bool enableKnockback = true;
    public float knockbackForce = 3f;       // 넉백 힘 크기
    public float knockbackUpFactor = 0.2f;  // 살짝 위로 튀게 하고 싶을 때

    // 합치기 이벤트 (타입, 위치)
    public static System.Action<int, Vector3> onMonsterMerged;
    
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    
    public float spawnTime;   // 스폰된 시간 기록

    void Awake()
    {
        spawnTime = Time.time;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Hero 레벨에 따라 maxHealth 스케일링
        if (scaleWithHeroLevel && Hero.Instance != null)
        {
            int heroLevel = Mathf.Max(Hero.Instance.level, 1); // 최소 1

            // level 1 → factor = 1.0
            // level 2 → factor = 1.0 + healthIncreasePerLevel
            // level 3 → factor = 1.0 + healthIncreasePerLevel * 2 ...
            float factor = 1f + (heroLevel - 1) * healthIncreasePerLevel;

            // 음수나 0 방지
            factor = Mathf.Max(factor, 0.1f);

            int scaledMax = Mathf.RoundToInt(maxHealth * factor);
            Debug.Log($"[Monster] Hero Lv.{heroLevel}, HP Scale Factor {factor:F2}, {maxHealth} → {scaledMax}");
            maxHealth = scaledMax;
        }

        currentHealth = maxHealth;
        originalColor = spriteRenderer.color;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log($"Monster Type {type} took {damage} damage. Current HP: {currentHealth}/{maxHealth}");

        // 데미지 텍스트 생성
        if (DamageTextManager.Instance != null)
        {
            var sr = GetComponent<SpriteRenderer>();
            Vector3 top = transform.position;

            if (sr != null)
            {
                top = sr.bounds.center + Vector3.up * sr.bounds.extents.y;
            }

            DamageTextManager.Instance.ShowDamage(damage, top + Vector3.up * 0.2f);
        }

        // 넉백 적용 (플레이어 반대 방향)
        ApplyKnockback();

        // 히트 이펙트
        SpawnHitEffect();

        StartCoroutine(FlashWhite());

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void ApplyKnockback()
    {
        if (!enableKnockback)
            return;

        if (rb == null)
            return;

        if (Hero.Instance == null)
            return;

        // 1) 움직일 수 있는 상태로 보장
        rb.bodyType = RigidbodyType2D.Dynamic;  // 혹시 Kinematic/Static이면 강제로 Dynamic
        rb.WakeUp();                            // 수면 상태일 수 있으니 깨우기

        // 2) 포지션이 얼어 있으면 풀어주고, 회전 고정은 유지
        var constraints = rb.constraints;

        // 회전은 계속 고정해도 됨 (수박게임처럼 세워진 상태 유지)
        bool freezeRot = (constraints & RigidbodyConstraints2D.FreezeRotation) != 0;

        // X/Y 포지션 고정 비활성화
        constraints &= ~RigidbodyConstraints2D.FreezePositionX;
        constraints &= ~RigidbodyConstraints2D.FreezePositionY;

        if (freezeRot)
            constraints |= RigidbodyConstraints2D.FreezeRotation;

        rb.constraints = constraints;

        // 3) 방향: 몬스터 - 히어로 → 히어로 반대 방향
        Vector2 dir = (Vector2)(transform.position - Hero.Instance.transform.position);
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.right; // 혹시나 완전 같은 위치면 기본 방향

        dir.Normalize();

        // 살짝 위로 튀게
        dir += Vector2.up * knockbackUpFactor;
        dir.Normalize();

        // 4) 기존 속도 지우고 강하게 한 방
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(dir * knockbackForce, ForceMode2D.Impulse);
    }

    private void SpawnHitEffect()
    {
        // 기본 프리팹은 Monster에 있는 hitPrefab
        GameObject prefab = hitPrefab;

        // Hero가 있으면 Hero에서 레벨별 프리팹 가져와서 덮어쓰기
        if (Hero.Instance != null)
        {
            GameObject heroPrefab = Hero.Instance.GetHitEffectPrefab();
            if (heroPrefab != null)
            {
                prefab = heroPrefab;
            }
        }

        if (prefab == null)
            return;

        GameObject hitObj = Instantiate(prefab, transform.position, Quaternion.identity);

        // 파티클 duration 끝나면 제거
        ParticleSystem ps = hitObj.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            float duration = main.duration;

            float maxStartLifetime = 0f;
            if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants)
                maxStartLifetime = main.startLifetime.constantMax;
            else
                maxStartLifetime = main.startLifetime.constant;

            Destroy(hitObj, duration + maxStartLifetime);
        }
        else
        {
            Destroy(hitObj, 2f);
        }
    }

    private IEnumerator FlashWhite()
    {
        spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.color = originalColor;
    }

    private void Die()
    {
        Debug.Log($"Monster Type {type} died!");
        Destroy(gameObject);
    }

    public int GetCurrentHealth()
    {
        return currentHealth;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Ground"))
        {
            transform.rotation = Quaternion.identity;

            if (rb != null)
            {
                rb.freezeRotation = true;
                rb.angularVelocity = 0f;
            }

            return;
        }

        Monster otherMonster = collision.collider.GetComponent<Monster>();
        if (otherMonster == null)
            return;

        if (otherMonster.type != this.type)
            return;

        // 인스턴스 정렬: 한 쪽만 합치기 로직 실행
        if (this.GetInstanceID() > otherMonster.GetInstanceID())
            return;

        if (nextTypePrefab == null)
        {
            Debug.LogWarning($"[Monster] Type {type} has no nextTypePrefab assigned. This is the max type.");
            return;
        }

        // 여기부터 추가: currentStage를 넘는 타입으로는 합쳐지지 않도록 제한
        int currentStage = 9999; // 기본값 (안전용)

        if (InfoManager.Instance != null &&
            InfoManager.Instance.GameInfo != null &&
            InfoManager.Instance.GameInfo.stageInfo != null)
        {
            currentStage = InfoManager.Instance.GameInfo.stageInfo.currentStage;
        }

        int mergedType = type + 1;

        if (mergedType > currentStage)
        {
            Debug.Log($"[Monster] Merge blocked: {type} + {type} → {mergedType} (stage {currentStage} 한도 초과)");
            return;
        }

        // 합치기 위치
        Vector3 spawnPos = (this.transform.position + otherMonster.transform.position) * 0.5f;
        
        // 다음 타입 생성
        GameObject newMonster = Instantiate(nextTypePrefab, spawnPos, Quaternion.identity);
        
        Debug.Log($"Merged Type {type} + Type {type} → Type {mergedType}");
        
        // 합치기 이벤트 발생 (점수 처리용)
        onMonsterMerged?.Invoke(type, spawnPos);

        // 둘 다 제거
        Destroy(otherMonster.gameObject);
        Destroy(this.gameObject);
    }

}
