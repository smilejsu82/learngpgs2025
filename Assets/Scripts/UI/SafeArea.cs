using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    RectTransform panel;
    Rect lastSafeArea;
    Vector2 lastScreenSize;

    void OnEnable()
    {
        panel = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    void Update()
    {
        // Device Simulator가 해상도 바꾸는 중이면 패스
        if (!IsValidScreenState())
            return;

        // safeArea나 해상도 바뀐 경우에만 갱신
        if (Screen.safeArea != lastSafeArea || 
            lastScreenSize.x != Screen.width || 
            lastScreenSize.y != Screen.height)
        {
            ApplySafeArea();
        }
    }

    bool IsValidScreenState()
    {
        // 화면 크기 검증
        if (Screen.width <= 0 || Screen.height <= 0)
            return false;

        // SafeArea 검증
        Rect safeArea = Screen.safeArea;
        if (float.IsNaN(safeArea.x) || float.IsNaN(safeArea.y) ||
            float.IsNaN(safeArea.width) || float.IsNaN(safeArea.height) ||
            float.IsInfinity(safeArea.x) || float.IsInfinity(safeArea.y) ||
            float.IsInfinity(safeArea.width) || float.IsInfinity(safeArea.height))
            return false;

        return true;
    }

    void ApplySafeArea()
    {
        if (!IsValidScreenState())
            return;

        Rect safeArea = Screen.safeArea;
        lastSafeArea = safeArea;
        lastScreenSize = new Vector2(Screen.width, Screen.height);

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        // 최종 anchor 값 검증
        if (!IsValidVector2(anchorMin) || !IsValidVector2(anchorMax))
            return;

        panel.anchorMin = anchorMin;
        panel.anchorMax = anchorMax;
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
        panel.anchoredPosition = Vector2.zero;
    }

    bool IsValidVector2(Vector2 v)
    {
        return !float.IsNaN(v.x) && !float.IsNaN(v.y) &&
               !float.IsInfinity(v.x) && !float.IsInfinity(v.y);
    }
}