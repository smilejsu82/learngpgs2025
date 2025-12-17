using TMPro;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

public class UITargetPopup : MonoBehaviour
{
    public SpriteAtlas atlas;
    public Image targetImage;
    public Button btn;

    public System.Action onClick;
    void Start()
    {
        string spritename = $"Icon{InfoManager.Instance.GameInfo.stageInfo.currentStage}_0";
        targetImage.sprite = atlas.GetSprite(spritename);
        btn.onClick.AddListener(() =>
        {
            onClick();
            Close();
        });
    }

    public void Open()
    {
        Time.timeScale = 0;
        gameObject.SetActive(true);
    }

    public void Close()
    {
        Time.timeScale = 1;
        gameObject.SetActive(false);
    }
}
