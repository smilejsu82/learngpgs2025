// Assets/Editor/LongPathScannerWindow.cs

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public class LongPathScannerWindow : EditorWindow
{
    [Serializable]
    private class Result
    {
        public string assetPath;       // "Assets/..." (파일이면 AssetDatabase 경로)
        public string fullPath;        // 절대경로
        public int length;             // fullPath.Length
        public UnityEngine.Object obj; // 에디터에서 핑/선택용
        public bool isDirectory;
    }

    private const int DefaultThreshold = 260;

    private int threshold = DefaultThreshold;

    // 드래그 드롭 폴더 선택용
    private DefaultAsset targetFolderAsset = null;

    private bool includeMetaFiles = false;
    private bool includeDirectories = false;
    private bool sortByLengthDesc = true;

    private string status = "";
    private Vector2 scroll;

    private List<Result> results = new List<Result>();

    [MenuItem("Tools/Long Path Scanner")]
    public static void Open()
    {
        var w = GetWindow<LongPathScannerWindow>("Long Path Scanner");
        w.minSize = new Vector2(760, 440);
        w.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Scan Target", EditorStyles.boldLabel);

            // 폴더 드래그&드롭
            targetFolderAsset = (DefaultAsset)EditorGUILayout.ObjectField(
                "Folder (drag & drop)",
                targetFolderAsset,
                typeof(DefaultAsset),
                false
            );

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Resolved path", GUILayout.Width(100));
                EditorGUILayout.SelectableLabel(GetResolvedTargetPath(), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        EditorGUILayout.Space(6);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);

            threshold = EditorGUILayout.IntField("Threshold (chars)", threshold);
            if (threshold <= 0) threshold = DefaultThreshold;

            includeMetaFiles = EditorGUILayout.Toggle("Include .meta files", includeMetaFiles);
            includeDirectories = EditorGUILayout.Toggle("Include directories", includeDirectories);
            sortByLengthDesc = EditorGUILayout.Toggle("Sort by length desc", sortByLengthDesc);

            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan", GUILayout.Height(28)))
                {
                    Scan();
                }

                if (GUILayout.Button("Clear", GUILayout.Height(28)))
                {
                    results.Clear();
                    status = "";
                    Repaint();
                }

                EditorGUI.BeginDisabledGroup(results.Count == 0);
                if (GUILayout.Button("Copy as CSV", GUILayout.Height(28)))
                {
                    CopyAsCsv();
                }
                if (GUILayout.Button("Export CSV...", GUILayout.Height(28)))
                {
                    ExportCsv();
                }
                EditorGUI.EndDisabledGroup();
            }
        }

        EditorGUILayout.Space(6);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(string.IsNullOrEmpty(status) ? "Ready." : status, MessageType.Info);
        }

        EditorGUILayout.Space(6);

        DrawResults();
    }

    private void DrawResults()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField($"Results ({results.Count})", EditorStyles.boldLabel);

            if (results.Count == 0)
            {
                EditorGUILayout.LabelField("No results. Choose a folder and click Scan.");
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Len", GUILayout.Width(50));
                EditorGUILayout.LabelField("Path (click to ping)");
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(4);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(r.length.ToString(), GUILayout.Width(50));

                    if (GUILayout.Button(r.assetPath, EditorStyles.linkLabel))
                    {
                        SelectAndPing(r);
                    }

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Ping", GUILayout.Width(60)))
                    {
                        SelectAndPing(r);
                    }

                    if (GUILayout.Button("Copy Full", GUILayout.Width(85)))
                    {
                        EditorGUIUtility.systemCopyBuffer = r.fullPath;
                        status = $"Copied full path: {r.length} chars";
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void Scan()
    {
        results.Clear();

        var targetFullPath = ResolveTargetFullPath();
        if (string.IsNullOrEmpty(targetFullPath))
        {
            status = "Invalid folder. Drag a folder from the Project window into 'Folder (drag & drop)'.";
            return;
        }

        if (!Directory.Exists(targetFullPath))
        {
            status = $"Target directory not found: {targetFullPath}";
            return;
        }

        var startTime = DateTime.Now;

        try
        {
            // 파일
            var allFiles = Directory.GetFiles(targetFullPath, "*", SearchOption.AllDirectories);
            foreach (var fileFullPath in allFiles)
            {
                if (!includeMetaFiles && fileFullPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                int len = fileFullPath.Length;
                if (len <= threshold)
                    continue;

                var assetPath = FullPathToAssetPathIfUnderAssets(fileFullPath, out var isUnderAssets);
                UnityEngine.Object obj = null;

                if (isUnderAssets)
                    obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

                results.Add(new Result
                {
                    assetPath = isUnderAssets ? assetPath : fileFullPath.Replace('\\', '/'),
                    fullPath = NormalizeSlash(fileFullPath),
                    length = len,
                    obj = obj,
                    isDirectory = false
                });
            }

            // 폴더(옵션)
            if (includeDirectories)
            {
                var allDirs = Directory.GetDirectories(targetFullPath, "*", SearchOption.AllDirectories);
                foreach (var dirFullPath in allDirs)
                {
                    int len = dirFullPath.Length;
                    if (len <= threshold)
                        continue;

                    var assetPath = FullPathToAssetPathIfUnderAssets(dirFullPath, out var isUnderAssets);
                    UnityEngine.Object obj = null;

                    if (isUnderAssets)
                        obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

                    results.Add(new Result
                    {
                        assetPath = isUnderAssets ? assetPath : dirFullPath.Replace('\\', '/'),
                        fullPath = NormalizeSlash(dirFullPath),
                        length = len,
                        obj = obj,
                        isDirectory = true
                    });
                }
            }

            results = sortByLengthDesc
                ? results.OrderByDescending(r => r.length).ThenBy(r => r.assetPath).ToList()
                : results.OrderBy(r => r.length).ThenBy(r => r.assetPath).ToList();

            var elapsed = DateTime.Now - startTime;
            status = $"Scanned: {NormalizeSlash(targetFullPath)} | Over {threshold} chars: {results.Count} item(s) | {elapsed.TotalSeconds:0.00}s";
        }
        catch (Exception e)
        {
            status = "Scan failed: " + e.Message;
        }

        Repaint();
    }

    private string GetResolvedTargetPath()
    {
        var p = ResolveTargetFullPath();
        return string.IsNullOrEmpty(p) ? "(none)" : NormalizeSlash(p);
    }

    private string ResolveTargetFullPath()
    {
        // 폴더를 지정하지 않으면 Assets 전체
        if (targetFolderAsset == null)
            return Application.dataPath;

        var assetPath = AssetDatabase.GetAssetPath(targetFolderAsset);
        if (string.IsNullOrEmpty(assetPath))
            return null;

        if (!AssetDatabase.IsValidFolder(assetPath))
            return null;

        // "Assets/..." -> 절대경로 변환
        // Application.dataPath = ".../<Project>/Assets"
        var dataPath = NormalizeSlash(Application.dataPath); // .../Assets
        var projectRoot = dataPath.Substring(0, dataPath.Length - "/Assets".Length); // .../<Project>
        var fullPath = NormalizeSlash(Path.GetFullPath(Path.Combine(projectRoot, assetPath)));

        return fullPath;
    }

    private static string NormalizeSlash(string path)
    {
        return path?.Replace('\\', '/');
    }

    private static string FullPathToAssetPathIfUnderAssets(string fullPath, out bool isUnderAssets)
    {
        fullPath = NormalizeSlash(fullPath);
        var dataPath = NormalizeSlash(Application.dataPath); // .../Assets

        if (fullPath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            isUnderAssets = true;
            return "Assets" + fullPath.Substring(dataPath.Length);
        }

        isUnderAssets = false;
        return null;
    }

    private void SelectAndPing(Result r)
    {
        if (r.obj == null)
        {
            // Assets 밖이면 핑 불가 (절대경로만 표시)
            status = $"Cannot ping (not an asset): {r.assetPath}";
            return;
        }

        Selection.activeObject = r.obj;
        EditorGUIUtility.PingObject(r.obj);
    }

    private void CopyAsCsv()
    {
        var sb = BuildCsv();
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        status = $"Copied CSV to clipboard. Rows: {results.Count}";
    }

    private void ExportCsv()
    {
        var path = EditorUtility.SaveFilePanel(
            "Export Long Path Results",
            Application.dataPath,
            "long_path_results.csv",
            "csv"
        );

        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            File.WriteAllText(path, BuildCsv().ToString(), Encoding.UTF8);
            status = $"Exported: {path}";
        }
        catch (Exception e)
        {
            status = "Export failed: " + e.Message;
        }
    }

    private StringBuilder BuildCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("length,isDirectory,assetOrFullPath,fullPath");

        foreach (var r in results)
        {
            sb.Append(r.length);
            sb.Append(',');
            sb.Append(r.isDirectory ? "1" : "0");
            sb.Append(',');
            sb.Append(EscapeCsv(r.assetPath));
            sb.Append(',');
            sb.Append(EscapeCsv(r.fullPath));
            sb.AppendLine();
        }

        return sb;
    }

    private static string EscapeCsv(string s)
    {
        if (s == null) return "";
        bool needQuote = s.Contains(",") || s.Contains("\"") || s.Contains("\n") || s.Contains("\r");
        s = s.Replace("\"", "\"\"");
        return needQuote ? $"\"{s}\"" : s;
    }
}
#endif
