// Assets/Editor/MonsterPrefabGeneratorWindow.cs

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class MonsterPrefabGeneratorWindow : EditorWindow
{
    // 템플릿으로 사용할 기본 몬스터 프리팹 (옵션)
    [SerializeField] private GameObject basePrefab;

    // 드래그해서 넣을 스프라이트들
    [SerializeField] private List<Sprite> sprites = new List<Sprite>();

    private Vector2 scrollPos;
    private string prefabPrefix = "monster_";
    private int startIndex = 0;

    [MenuItem("Tools/Monsters/Monster Prefab Generator")]
    private static void Open()
    {
        GetWindow<MonsterPrefabGeneratorWindow>("Monster Prefab Generator");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("1. 템플릿 프리팹(선택사항)", EditorStyles.boldLabel);
        basePrefab = (GameObject)EditorGUILayout.ObjectField(
            "Base Prefab (optional)", basePrefab, typeof(GameObject), false);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("2. 스프라이트 드래그", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Project 뷰에서 Sprite들 여러 개 선택해서 아래 박스로 드래그하세요.", MessageType.Info);

        Rect dropArea = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "여기에 드래그 드롭");

        HandleSpriteDragAndDrop(dropArea);

        if (GUILayout.Button("현재 선택된 오브젝트에서 스프라이트 가져오기"))
        {
            LoadSpritesFromSelection();
        }

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Sprite List", EditorStyles.boldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(250));
        for (int i = 0; i < sprites.Count; i++)
        {
            sprites[i] = (Sprite)EditorGUILayout.ObjectField($"[{i}]",
                sprites[i], typeof(Sprite), false);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("3. 생성 옵션", EditorStyles.boldLabel);
        prefabPrefix = EditorGUILayout.TextField("Prefab Prefix", prefabPrefix);
        startIndex = EditorGUILayout.IntField("Start Index", startIndex);

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Prefabs"))
        {
            GeneratePrefabs();
        }
    }

    private void HandleSpriteDragAndDrop(Rect dropArea)
    {
        Event e = Event.current;

        if (!dropArea.Contains(e.mousePosition))
            return;

        if (e.type == EventType.DragUpdated || e.type == EventType.DragPerform)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                foreach (Object obj in DragAndDrop.objectReferences)
                {
                    // 직접 Sprite가 들어온 경우
                    if (obj is Sprite sprite && !sprites.Contains(sprite))
                    {
                        sprites.Add(sprite);
                        continue;
                    }

                    // Texture2D 드래그한 경우 → Import에서 Sprite 뽑기
                    if (obj is Texture2D tex)
                    {
                        string path = AssetDatabase.GetAssetPath(tex);
                        string texName = Path.GetFileNameWithoutExtension(path);

                        var insideSprites = AssetDatabase.LoadAllAssetsAtPath(path)
                            .OfType<Sprite>()
                            .Where(s => s.name == texName || s.name == texName + "_0");   // 여기 수정

                        foreach (var s in insideSprites)
                        {
                            if (!sprites.Contains(s))
                                sprites.Add(s);
                        }

                        continue;
                    }


                    // 폴더 드래그한 경우 → 모든 스프라이트 수집
                    string folderPath = AssetDatabase.GetAssetPath(obj);
                    if (AssetDatabase.IsValidFolder(folderPath))
                    {
                        string[] files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                            .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".psd"))
                            .ToArray();

                        foreach (string file in files)
                        {
                            string texName = Path.GetFileNameWithoutExtension(file);

                            var folderSprites = AssetDatabase.LoadAllAssetsAtPath(file)
                                .OfType<Sprite>()
                                .Where(s => s.name == texName || s.name == texName + "_0");  // 여기 수정

                            foreach (var s in folderSprites)
                            {
                                if (!sprites.Contains(s))
                                    sprites.Add(s);
                            }
                        }
                    }

                }

                // 이름 순 정렬
                sprites = sprites
                    .OrderBy(s =>
                    {
                        string n = System.Text.RegularExpressions.Regex.Replace(s.name, @"\D", "");
                        return int.TryParse(n, out int number) ? number : int.MaxValue;
                    })
                    .ToList();

            }

            e.Use();
        }
    }


    private void LoadSpritesFromSelection()
    {
        var result = new List<Sprite>();

        foreach (var obj in Selection.objects)
        {
            // 이미 Sprite로 선택된 경우
            if (obj is Sprite s)
            {
                if (!result.Contains(s))
                    result.Add(s);
            }
            // Texture2D로 선택된 경우 → 안에 있는 Sprite 뽑기
            else if (obj is Texture2D tex)
            {
                string path = AssetDatabase.GetAssetPath(tex);
                string texName = Path.GetFileNameWithoutExtension(path);

                var insideSprites = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<Sprite>()
                    .Where(sp => sp.name == texName || sp.name == texName + "_0"); // 여기 수정

                foreach (var sp in insideSprites)
                {
                    if (!result.Contains(sp))
                        result.Add(sp);
                }
            }

        }

        // 숫자 기준 정렬
        sprites = sprites
            .OrderBy(sp =>
            {
                string n = System.Text.RegularExpressions.Regex.Replace(sp.name, @"\D", "");
                return int.TryParse(n, out int number) ? number : int.MaxValue;
            })
            .ToList();

    }


    private void GeneratePrefabs()
    {
        if (sprites == null || sprites.Count == 0)
        {
            Debug.LogWarning("스프라이트가 없습니다.");
            return;
        }

        // 저장할 폴더 선택
        string folder = EditorUtility.OpenFolderPanel("프리팹을 저장할 폴더 선택", "Assets", "");
        if (string.IsNullOrEmpty(folder))
            return;

        if (!folder.StartsWith(Application.dataPath))
        {
            Debug.LogError("Assets 폴더 안쪽만 선택할 수 있습니다.");
            return;
        }

        string relativeFolder = "Assets" + folder.Substring(Application.dataPath.Length);

        List<GameObject> createdPrefabs = new List<GameObject>();

        for (int i = 0; i < sprites.Count; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null)
                continue;

            int index = startIndex + i;
            string prefabName = $"{prefabPrefix}{index}";

            // 인스턴스 복사
            GameObject instance;

            if (basePrefab != null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            }
            else
            {
                instance = new GameObject(prefabName);
                instance.AddComponent<SpriteRenderer>();
            }

            instance.name = prefabName;

            // SpriteRenderer 설정
            SpriteRenderer sr = instance.GetComponentInChildren<SpriteRenderer>();
            if (sr == null)
                sr = instance.AddComponent<SpriteRenderer>();

            sr.sprite = sprite;

            // Monster 컴포넌트 설정
            Monster m = instance.GetComponent<Monster>();
            if (m == null)
                m = instance.AddComponent<Monster>();

            m.type = index; // Type = 인덱스 설정 (monster_0 -> type = 0)

            // 프리팹 저장
            string path = Path.Combine(relativeFolder, prefabName + ".prefab");
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            createdPrefabs.Add(prefab);

            DestroyImmediate(instance);
        }

        // NextType Prefab 연결하기
        for (int i = 0; i < createdPrefabs.Count; i++)
        {
            Monster m = createdPrefabs[i].GetComponent<Monster>();

            if (i < createdPrefabs.Count - 1)
                m.nextTypePrefab = createdPrefabs[i + 1]; // 다음 프리팹
            else
                m.nextTypePrefab = null; // 마지막은 없음

            EditorUtility.SetDirty(m);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Monster Prefabs {createdPrefabs.Count}개 생성 및 Monster 컴포넌트 자동 설정 완료");
    }
}