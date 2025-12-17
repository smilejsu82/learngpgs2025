using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DamageText : MonoBehaviour
{
    [Header("Motion")]
    public float moveUpDistance = 80f;
    public float duration = 0.7f;

    [Tooltip("시작할 때 팍 튀어오르는 정도")]
    public float popScale = 1.4f;          // 처음에 커졌다가
    public float endScale = 1.0f;          // 서서히 1로 수렴

    [Tooltip("살짝 좌우로 랜덤 오프셋")]
    public float randomXOffset = 20f;

    [Header("Curves")]
    // 0~1 구간, 기본값은 Inspector에서 조절 가능
    public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 1f, 1, 0f);

    private RectTransform rect;
    private TextMeshProUGUI tmpText;
    private Text uiText;

    private Vector2 startPos;
    private Vector3 startScale;
    private float elapsed;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        tmpText = GetComponentInChildren<TextMeshProUGUI>();
        uiText = GetComponentInChildren<Text>();
    }

    // DamageTextManager에서 반드시 이걸 호출해서 초기 세팅
    public void Initialize(string text, Vector2 anchoredPos)
    {
        if (rect == null)
            rect = GetComponent<RectTransform>();

        // 약간 좌우 랜덤 오프셋
        anchoredPos.x += Random.Range(-randomXOffset, randomXOffset);

        rect.anchoredPosition = anchoredPos;
        startPos = anchoredPos;

        // 시작 스케일은 살짝 작게 두고, popScale까지 튀어오르게
        startScale = Vector3.one;

        if (tmpText != null)
            tmpText.text = text;
        else if (uiText != null)
            uiText.text = text;

        elapsed = 0f;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        // 이동: moveCurve로 부드럽게 위로 떠오르게 (처음엔 빠르게, 위로 갈수록 천천히)
        float moveT = moveCurve.Evaluate(t);
        float yOffset = moveUpDistance * moveT;
        rect.anchoredPosition = startPos + new Vector2(0f, yOffset);

        // 스케일: 처음에 popScale까지 확 커졌다가 endScale로 수렴
        float scaleT = Mathf.SmoothStep(popScale, endScale, t);
        transform.localScale = startScale * scaleT;

        // 알파 페이드
        float alpha = alphaCurve.Evaluate(t);
        if (tmpText != null)
        {
            var c = tmpText.color;
            c.a = alpha;
            tmpText.color = c;
        }
        if (uiText != null)
        {
            var c = uiText.color;
            c.a = alpha;
            uiText.color = c;
        }

        if (elapsed >= duration)
        {
            Destroy(gameObject);
        }
    }
}
