using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StopAnim : MonoBehaviour
{
    public Animator[] animators;
    private int frameCount = 0;
    public int frameCap = 10;

    private void Update()
    {
        if(frameCount <= frameCap)
        {
            frameCount++;
            Debug.Log(frameCount);
        }
        else
        {
            StopAnimation();
        }

    }
    public void StopAnimation()
    {
        for (int i = 0; i < animators.Length; i++)
        {
            animators[i].enabled = false;
        }
    }

}
