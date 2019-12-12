using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public GameObject button1;
    public GameObject button2;

    void Start()
    {
        button1.SetActive(true);
        button2.SetActive(false);
    }

    public void clickThisButton()
    {
        button1.SetActive(false);
        button2.SetActive(true);
    }
    public void StartGame()
    {
        SceneManager.LoadScene("__Bartok_Scene_0");

    }

    public void RulesScene()
    {
        SceneManager.LoadScene("RulesScene");
    }

    public void StartScreen()
    {
        SceneManager.LoadScene("StartScene");

    }

    public void RulesScene2()
    {
        SceneManager.LoadScene("RulesScene2");
    }

    public void RulesScene3()
    {
        SceneManager.LoadScene("RulesScene3");
    }
}
