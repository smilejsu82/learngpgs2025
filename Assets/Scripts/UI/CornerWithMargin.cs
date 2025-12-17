using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class CornerWithMargin : MonoBehaviour
{
    public enum Corner
    {
        BottomRight,
        BottomLeft,
        TopRight,
        TopLeft
    }

    public Corner corner = Corner.BottomRight;
    public Vector2 margin = new Vector2(32f, 32f);

    void OnEnable()
    {
        Apply();
    }

    void OnValidate()
    {
        Apply();
    }

    void Apply()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        Vector2 anchor = Vector2.zero;
        Vector2 pos = Vector2.zero;

        switch (corner)
        {
            case Corner.BottomRight:
                anchor = new Vector2(1f, 0f);
                pos = new Vector2(-margin.x, margin.y);
                break;
            case Corner.BottomLeft:
                anchor = new Vector2(0f, 0f);
                pos = new Vector2(margin.x, margin.y);
                break;
            case Corner.TopRight:
                anchor = new Vector2(1f, 1f);
                pos = new Vector2(-margin.x, -margin.y);
                break;
            case Corner.TopLeft:
                anchor = new Vector2(0f, 1f);
                pos = new Vector2(margin.x, -margin.y);
                break;
        }

        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
    }
}