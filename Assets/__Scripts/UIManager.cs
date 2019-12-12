using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
}
