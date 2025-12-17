using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;

public class AppPathJsonManager : EditorWindow
{
    private string[] jsonFiles;
    private string selectedFilePath;
    private Vector2 fileScroll, previewScroll;
    private JToken selectedJson;
    private string jsonTextCache = ""; // 텍스트 탭에서 편집 내용
    private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();
    private int selectedTab = 0; // 0: 트리 보기, 1: 텍스트 보기

    // Splitter 상태
    private float leftPanelWidth = 250f;
    private bool isResizing = false;
    private readonly float splitterWidth = 5f;

    // JSON 유효성
    private bool isJsonValid = true;
    private string jsonErrorMessage = "";

    [MenuItem("Tools/App Path JSON Manager")]
    public static void Open()
    {
        GetWindow<AppPathJsonManager>("JSON Manager");
    }

    private void OnEnable()
    {
        RefreshFileList();
    }

    private void RefreshFileList()
    {
        jsonFiles = Directory.GetFiles(Application.persistentDataPath, "*.json");
        foldoutStates.Clear();
        selectedFilePath = null;
        selectedJson = null;
        jsonTextCache = "";
        isJsonValid = true;
        jsonErrorMessage = "";
    }

    private void OnGUI()
    {
        Rect totalRect = new Rect(0, 0, position.width, position.height);

        // 좌측 파일 목록
        Rect leftRect = new Rect(0, 0, leftPanelWidth, totalRect.height);
        DrawFileList(leftRect);

        // 분리선
        Rect splitterRect = new Rect(leftRect.xMax, 0, splitterWidth, totalRect.height);
        EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);
        HandleSplitter(splitterRect);

        // 우측 미리보기
        Rect rightRect = new Rect(splitterRect.xMax, 0, totalRect.width - splitterRect.xMax, totalRect.height);
        DrawPreview(rightRect);
    }

    private void HandleSplitter(Rect splitterRect)
    {
        Event e = Event.current;
        switch (e.type)
        {
            case EventType.MouseDown:
                if (splitterRect.Contains(e.mousePosition))
                {
                    isResizing = true;
                    e.Use();
                }
                break;
            case EventType.MouseDrag:
                if (isResizing)
                {
                    leftPanelWidth = Mathf.Clamp(e.mousePosition.x, 150, position.width - 150);
                    Repaint();
                    e.Use();
                }
                break;
            case EventType.MouseUp:
                if (isResizing)
                {
                    isResizing = false;
                    e.Use();
                }
                break;
        }
    }

    private void DrawFileList(Rect rect)
    {
        GUILayout.BeginArea(rect, EditorStyles.helpBox);
        EditorGUILayout.LabelField("JSON Files", EditorStyles.boldLabel);
        fileScroll = EditorGUILayout.BeginScrollView(fileScroll);

        if (jsonFiles != null)
        {
            foreach (var file in jsonFiles)
            {
                EditorGUILayout.BeginHorizontal();

                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                if (file == selectedFilePath)
                    buttonStyle.normal.background = MakeTex(2, 2, new Color(0.3f, 0.6f, 1f, 0.5f));

                if (GUILayout.Button(Path.GetFileName(file), buttonStyle, GUILayout.ExpandWidth(true)))
                {
                    selectedFilePath = file;
                    LoadJson(file);
                }

                if (GUILayout.Button("삭제", GUILayout.Width(60)))
                {
                    if (EditorUtility.DisplayDialog("삭제 확인", $"{Path.GetFileName(file)}을(를) 삭제하시겠습니까?", "Yes", "No"))
                    {
                        try
                        {
                            File.Delete(file);
                            RefreshFileList();
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError("파일 삭제 실패: " + ex.Message);
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("새로고침", GUILayout.Height(22)))
            RefreshFileList();

        GUILayout.EndArea();
    }

    private void DrawPreview(Rect rect)
    {
        GUILayout.BeginArea(rect, EditorStyles.helpBox);

        if (selectedJson != null)
        {
            // JSON 유효성 경고 표시
            if (!isJsonValid)
            {
                GUIStyle warningStyle = new GUIStyle(EditorStyles.boldLabel);
                warningStyle.normal.textColor = Color.red;
                EditorGUILayout.LabelField("⚠️ JSON 오류: " + jsonErrorMessage, warningStyle, GUILayout.Height(20));
            }

            // 탭
            string[] tabs = { "트리 보기", "텍스트 보기" };
            selectedTab = GUILayout.Toolbar(selectedTab, tabs);

            previewScroll = EditorGUILayout.BeginScrollView(previewScroll);

            if (selectedTab == 0)
            {
                DrawJsonTree(selectedJson, selectedFilePath, 0, "root");
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                jsonTextCache = EditorGUILayout.TextArea(jsonTextCache, GUILayout.ExpandHeight(true));
                if (EditorGUI.EndChangeCheck())
                {
                    ValidateJson(jsonTextCache);
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // 트리 탭 접기/펼치기
            if (selectedTab == 0)
            {
                bool anyFolded = false;
                foreach (var s in foldoutStates.Values)
                    if (!s) { anyFolded = true; break; }

                if (GUILayout.Button(anyFolded ? "모두 펼치기" : "모두 접기", GUILayout.Width(120), GUILayout.Height(25)))
                    SetAllFoldouts(anyFolded);
            }

            // 파일 위치 열기
            GUI.enabled = !string.IsNullOrEmpty(selectedFilePath) && File.Exists(selectedFilePath);
            if (GUILayout.Button("📂 파일 위치 열기", GUILayout.Width(160), GUILayout.Height(25)))
                EditorUtility.RevealInFinder(selectedFilePath);
            GUI.enabled = true;

            // 텍스트 탭에서만 저장 버튼
            if (selectedTab == 1)
            {
                GUI.enabled = isJsonValid; // 유효할 때만 저장 가능
                if (GUILayout.Button("💾 저장", GUILayout.Width(100), GUILayout.Height(25)))
                {
                    try
                    {
                        File.WriteAllText(selectedFilePath, jsonTextCache);
                        EditorUtility.DisplayDialog("저장 완료", "파일이 성공적으로 저장되었습니다.", "OK");
                        LoadJson(selectedFilePath); // 다시 로드
                    }
                    catch (System.Exception ex)
                    {
                        EditorUtility.DisplayDialog("저장 실패", "파일 저장 실패\n" + ex.Message, "OK");
                    }
                }
                GUI.enabled = true;
            }

            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.LabelField("JSON 파일을 선택하세요.");
        }

        GUILayout.EndArea();
    }

    private void ValidateJson(string json)
    {
        try
        {
            JToken.Parse(json);
            isJsonValid = true;
            jsonErrorMessage = "";
        }
        catch (System.Exception ex)
        {
            isJsonValid = false;
            jsonErrorMessage = ex.Message;
        }
    }

    private void LoadJson(string file)
    {
        try
        {
            string jsonText = File.ReadAllText(file);
            selectedJson = JToken.Parse(jsonText);
            jsonTextCache = jsonText;
            foldoutStates.Clear();
            isJsonValid = true;
            jsonErrorMessage = "";
        }
        catch (System.Exception e)
        {
            Debug.LogError("JSON 파싱 실패: " + e.Message);
            selectedJson = null;
            jsonTextCache = "";
            isJsonValid = false;
            jsonErrorMessage = e.Message;
        }
    }

    private void DrawJsonTree(JToken token, string path, int indent, string label)
    {
        if (token is JObject obj)
        {
            bool foldout = GetFoldout(path, true);
            foldout = EditorGUILayout.Foldout(foldout, label, true);
            SetFoldout(path, foldout);

            if (foldout)
            {
                foreach (var prop in obj.Properties())
                {
                    EditorGUI.indentLevel = indent + 1;
                    DrawJsonTree(prop.Value, path + "." + prop.Name, indent + 1, prop.Name);
                }
            }
        }
        else if (token is JArray array)
        {
            bool foldout = GetFoldout(path, false);
            foldout = EditorGUILayout.Foldout(foldout, $"{label} [{array.Count}]", true);
            SetFoldout(path, foldout);

            if (foldout)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    EditorGUI.indentLevel = indent + 1;
                    DrawJsonTree(array[i], path + $"[{i}]", indent + 1, $"[{i}]");
                }
            }
        }
        else
        {
            EditorGUI.indentLevel = indent;
            EditorGUILayout.LabelField($"{label}: {token?.ToString() ?? "null"}");
        }
    }

    private bool GetFoldout(string key, bool defaultValue)
    {
        if (!foldoutStates.ContainsKey(key))
            foldoutStates[key] = defaultValue;
        return foldoutStates[key];
    }

    private void SetFoldout(string key, bool value)
    {
        foldoutStates[key] = value;
    }

    private void SetAllFoldouts(bool fold)
    {
        List<string> keys = new List<string>(foldoutStates.Keys);
        foreach (var key in keys)
            foldoutStates[key] = fold;
        Repaint();
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i)
            pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}