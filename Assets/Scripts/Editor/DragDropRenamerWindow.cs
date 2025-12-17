// Assets/Editor/DragDropRenamerWindow.cs
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public class DragDropRenamerWindow : EditorWindow
{
    private List<GameObject> objects = new List<GameObject>();
    private ReorderableList reorderableList;

    private string prefix = "Stage";
    private int startIndex = 1;
    private int padding = 2;

    private Vector2 scrollPos;
    private bool foldout = true;

    [MenuItem("Tools/DragDrop Renamer")]
    private static void Open()
    {
        GetWindow<DragDropRenamerWindow>("DragDrop Renamer");
    }

    private void OnEnable()
    {
        if (objects == null)
            objects = new List<GameObject>();

        reorderableList = new ReorderableList(objects, typeof(GameObject), true, true, true, true);

        reorderableList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Objects");
        };

        reorderableList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;

            objects[index] = (GameObject)EditorGUI.ObjectField(
                rect,
                objects[index],
                typeof(GameObject),
                true);
        };

        reorderableList.onAddCallback = list =>
        {
            foreach (var go in Selection.gameObjects)
            {
                if (!objects.Contains(go))
                    objects.Add(go);
            }
        };
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Hierarchy에서 오브젝트를 아래 영역으로 드래그하세요.");
        Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "여기에 드래그 드롭");

        HandleDragAndDrop(dropArea);

        EditorGUILayout.Space();

        foldout = EditorGUILayout.Foldout(foldout, "Rename Targets", true);

        if (foldout)
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(300));

            if (reorderableList != null)
                reorderableList.DoLayoutList();

            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space();

        prefix = EditorGUILayout.TextField("Prefix", prefix);
        startIndex = EditorGUILayout.IntField("Start Index", startIndex);
        padding = EditorGUILayout.IntField("Number Padding", padding);

        EditorGUILayout.Space();

        if (GUILayout.Button("Rename Objects"))
        {
            RenameObjects();
        }
    }

    private void HandleDragAndDrop(Rect dropArea)
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

                foreach (Object dragged in DragAndDrop.objectReferences)
                {
                    GameObject go = dragged as GameObject;
                    if (go != null && !objects.Contains(go))
                    {
                        objects.Add(go);
                    }
                }
            }

            e.Use();
        }
    }

    private void RenameObjects()
    {
        if (objects == null || objects.Count == 0)
        {
            Debug.LogWarning("변경할 오브젝트가 없습니다.");
            return;
        }

        Undo.RecordObjects(objects.ToArray(), "Batch Rename");

        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i] == null)
                continue;

            int index = startIndex + i;
            string indexStr = index.ToString().PadLeft(padding, '0');
            objects[i].name = $"{prefix}_{indexStr}";
        }

        Debug.Log("이름 변경 완료");
    }
}
