using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Hero))]
public class HeroEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 기본 Inspector 그리기
        DrawDefaultInspector();
        
        Hero hero = (Hero)target;
        
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("=== Upgrade System ===", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);
        
        // 이동속도 업그레이드
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Current Speed: {hero.speed:F2}", GUILayout.Width(150));
        if (GUILayout.Button("Speed +0.5", GUILayout.Width(100)))
        {
            Undo.RecordObject(hero, "Upgrade Speed");
            hero.UpgradeSpeed(hero.speed + 0.5f);
            EditorUtility.SetDirty(hero);
        }
        if (GUILayout.Button("Speed +1.0", GUILayout.Width(100)))
        {
            Undo.RecordObject(hero, "Upgrade Speed");
            hero.UpgradeSpeed(hero.speed + 1.0f);
            EditorUtility.SetDirty(hero);
        }
        EditorGUILayout.EndHorizontal();
        
        // 공격 딜레이 업그레이드
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Current Attack Delay: {hero.attackDelay:F2}s", GUILayout.Width(150));
        if (GUILayout.Button("Delay -0.05", GUILayout.Width(100)))
        {
            Undo.RecordObject(hero, "Upgrade Attack Speed");
            float newDelay = Mathf.Max(0.1f, hero.attackDelay - 0.05f);
            hero.UpgradeAttackDelay(newDelay);
            EditorUtility.SetDirty(hero);
        }
        if (GUILayout.Button("Delay -0.1", GUILayout.Width(100)))
        {
            Undo.RecordObject(hero, "Upgrade Attack Speed");
            float newDelay = Mathf.Max(0.1f, hero.attackDelay - 0.1f);
            hero.UpgradeAttackDelay(newDelay);
            EditorUtility.SetDirty(hero);
        }
        EditorGUILayout.EndHorizontal();
        
        // 공격력 업그레이드
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Current Attack Power: {hero.attackPower}", GUILayout.Width(150));
        if (GUILayout.Button("Power +5", GUILayout.Width(100)))
        {
            Undo.RecordObject(hero, "Upgrade Attack Power");
            hero.UpgradeAttackPower(hero.attackPower + 5);
            EditorUtility.SetDirty(hero);
        }
        if (GUILayout.Button("Power +10", GUILayout.Width(100)))
        {
            Undo.RecordObject(hero, "Upgrade Attack Power");
            hero.UpgradeAttackPower(hero.attackPower + 10);
            EditorUtility.SetDirty(hero);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // 리셋 버튼
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset All Stats", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Reset Stats", 
                "Are you sure you want to reset all stats to default?", 
                "Yes", "No"))
            {
                Undo.RecordObject(hero, "Reset Hero Stats");
                hero.UpgradeSpeed(hero.baseSpeed);
                hero.UpgradeAttackDelay(hero.baseAttackDelay);
                hero.UpgradeAttackPower(20);
                EditorUtility.SetDirty(hero);
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // 현재 스탯 정보
        EditorGUILayout.HelpBox(
            $"Move Animation Speed: {(hero.speed / hero.baseSpeed):F2}x\n" +
            $"Attack Animation Speed: {(hero.baseAttackDelay / hero.attackDelay):F2}x",
            MessageType.Info
        );
    }
}