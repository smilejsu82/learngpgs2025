using UnityEngine;

public class MonsterGenerator : MonoBehaviour
{
    public Transform leftPoint;
    public Transform rightPoint;
    public GameObject[] monsterPrefabs;

    [Header("Base Spawn Interval")]
    public float baseInterval = 1.0f;  // 레벨1 기준
    public float intervalDecreasePerLevel = 0.1f; // 레벨당 감소량
    public float minInterval = 0.1f;   // 최소 스폰 간격

    public System.Func<bool> onBeforeSpawn;

    private float timer = 0f;

    private void Update()
    {
        timer += Time.deltaTime;

        float currentInterval = GetSpawnInterval();

        if (timer >= currentInterval)
        {
            SpawnMonster();
            timer = 0f;
        }
    }

    private float GetSpawnInterval()
    {
        // Hero 없으면 그냥 baseInterval 사용
        if (Hero.Instance == null)
            return baseInterval;

        int level = Hero.Instance.level;

        // 레벨 기반 계산
        float interval = baseInterval - (level - 1) * intervalDecreasePerLevel;

        // 최소값 적용
        return Mathf.Max(interval, minInterval);
    }

    void SpawnMonster()
    {
        if (onBeforeSpawn != null)
        {
            if (!onBeforeSpawn.Invoke())
            {
                Debug.Log("Cannot spawn - Game Over condition met!");
                return;
            }
        }

        if (monsterPrefabs == null || monsterPrefabs.Length == 0)
        {
            Debug.LogWarning("[MonsterGenerator] monsterPrefabs is empty");
            return;
        }

        int index = Random.Range(0, monsterPrefabs.Length);
        GameObject selectedPrefab = monsterPrefabs[index];

        float randomX = Random.Range(leftPoint.position.x, rightPoint.position.x);
        Vector3 spawnPos = new Vector3(randomX, 6.04f, transform.position.z);

        Instantiate(selectedPrefab, spawnPos, Quaternion.identity);
    }
}