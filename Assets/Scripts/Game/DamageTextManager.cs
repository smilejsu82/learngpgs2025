using UnityEngine;

public class DamageTextManager : MonoBehaviour
{
    public static DamageTextManager Instance;

    public DamageText damageTextPrefab;

    public Canvas canvas;                 // Game UI Canvas
    public RectTransform parentTransform; // Canvas 자식: DamageTextLayer
    public Camera worldCamera;            // 몬스터를 보는 카메라 (없으면 MainCamera)

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void ShowDamage(int amount, Vector3 worldPosition)
    {
        if (damageTextPrefab == null ||
            canvas == null ||
            parentTransform == null)
        {
            Debug.LogWarning("[DamageTextManager] Setup not complete.");
            return;
        }

        // 실제 월드 카메라
        Camera cam = worldCamera != null
            ? worldCamera
            : (canvas.worldCamera != null ? canvas.worldCamera : Camera.main);

        if (cam == null)
        {
            Debug.LogWarning("[DamageTextManager] world camera is null.");
            return;
        }

        // 1) 월드 → 스크린 좌표
        Vector3 screenPos3 = cam.WorldToScreenPoint(worldPosition);
        Vector2 screenPos = new Vector2(screenPos3.x, screenPos3.y);

        // 2) 스크린 → DamageTextLayer(parentTransform) 기준 로컬 좌표
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentTransform,                                             // 기준 RectTransform
            screenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
            out localPos
        );

        // 3) DamageTextLayer 밑에 생성 후, 초기화
        DamageText instance = Instantiate(damageTextPrefab, parentTransform);
        instance.Initialize(amount.ToString(), localPos);

        Debug.Log($"[DamageText] world={worldPosition}, screen={screenPos}, local={localPos}");
    }

}
