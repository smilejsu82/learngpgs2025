using System;
using UnityEngine;
using UnityEngine.UI;

public class UILevelUpPopup : MonoBehaviour
{
     public Button skipButton;
     public Button[] upgradeButtons;

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
