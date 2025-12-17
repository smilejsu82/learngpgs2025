using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

public class UIStagePanel : MonoBehaviour
{
    public SpriteAtlas atlas;

    public Button[] buttons;

    public System.Action onSelectStage;
    
    void Start()
    {
        InitButtons();
    }
    
    private void InitButtons()
    {
        // 1) 아틀라스 안에 뭐가 들어있는지 확인
        var all = new Sprite[atlas.spriteCount];
        atlas.GetSprites(all);
        foreach (var s in all)
        {
            //Debug.Log($"[Atlas] {s.name}");
        }

        // 2) 기존 코드
        for (int i = 0; i < buttons.Length; i++)
        {
            var idx = i;
            var button = buttons[i];
            var image = button.GetComponent<Image>();

            // 만약 이름이 Icon1_0, Icon2_0 규칙이면 이렇게:
            var spriteName = $"Icon{idx + 1}_0";
            var sprite = atlas.GetSprite(spriteName);
            //Debug.Log($"=> {spriteName}, {sprite}");

            image.sprite = sprite;

            var stageNum = idx + 1;
            UpdateButtonState(image, stageNum);

            button.onClick.AddListener(() =>
            {
                Debug.Log(stageNum);
                if (stageNum == InfoManager.Instance.GameInfo.stageInfo.currentStage)
                {
                    //전면 광고 
                    AdmobManager.Instance.ShowInterstitialAd(() =>
                    {
                        onSelectStage();    
                    });
                }
            });
        }
    }
    
    /// <summary>
    /// GameInfo가 변경되었을 때 UI 업데이트 (클라우드 로드 후 호출)
    /// </summary>
    public void UpdateUI()
    {
        if (InfoManager.Instance == null || InfoManager.Instance.GameInfo == null)
        {
            Debug.LogWarning("[UIStagePanel] Cannot update UI: GameInfo is null");
            return;
        }
        
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            var image = button.GetComponent<Image>();
            var stageNum = i + 1;
            UpdateButtonState(image, stageNum);
        }
        
        Debug.Log("[UIStagePanel] UI updated with new GameInfo");
    }
    
    private void UpdateButtonState(Image image, int stageNum)
    {
        if (InfoManager.Instance.GameInfo.stageInfo.currentStage >= stageNum)
        {
            image.color = Color.white;    
        }
        else
        {
            image.color = Color.black;
        }
    }

}
