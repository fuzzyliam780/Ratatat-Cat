using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UI : MonoBehaviour
{
    float aspect_ratio;
    // Start is called before the first frame update
    void Start()
    {
        aspect_ratio = Screen.width / Screen.height;
    }

    // Update is called once per frame
    void Update()
    {
        if (getAspectRatio() != aspect_ratio)
        {
            aspect_ratio = getAspectRatio();

            
        }
    }

    float getAspectRatio()
    {
        return Screen.width / Screen.height;
    }
}
