﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
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

    

}