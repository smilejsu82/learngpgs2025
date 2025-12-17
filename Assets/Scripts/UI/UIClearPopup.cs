using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIClearPopup : MonoBehaviour
{
    public Button homeButton;
    
    void Start()
    {
        homeButton.onClick.AddListener(() =>
        {
            SceneManager.LoadScene("Home");
        });   
    }

    public void Open()
    {
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
