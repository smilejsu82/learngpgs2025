using System;
using GoogleMobileAds.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

public class App : MonoBehaviour
{
    private void Awake()
    {
        
        DontDestroyOnLoad(this.gameObject);
    }
    
    void Start()
    {
        var ao = SceneManager.LoadSceneAsync("Title");
        ao.completed += (oper) =>
        {
            Debug.Log("Title Scene Load Complete!");
        };
    }
}
