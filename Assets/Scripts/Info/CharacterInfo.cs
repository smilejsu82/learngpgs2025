using UnityEngine;

[System.Serializable]
public class CharacterInfo
{
    public int level;
    public int currentXP;
    public int xpToNextLevel;

    public int attackPower;      // 공격력
    public float speed;          // 이동 속도
    public float attackDelay;    // 공격 딜레이

    public CharacterInfo()
    {
        level = 1;
        currentXP = 0;
        xpToNextLevel = Hero.DefaultXpToNextLevel;

        attackPower = Hero.DefaultAttackPower;
        speed = Hero.DefaultSpeed;
        attackDelay = Hero.DefaultAttackDelay;
    }

}