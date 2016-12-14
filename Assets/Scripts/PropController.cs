using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class PropController : MonoBehaviour
{
    [Tooltip("Duration of the default fade-in animation (in sec)")]
    public float fadeInDuration = 1f;

    [Tooltip("Duration of the default fade-out animation (in sec)")]
    public float fadeOutDuration = 1f;

    [Tooltip("Apply the users movement in X direction to prop?")]
    public bool ApplyUserX = true;

    [Tooltip("Apply the users movement in Y direction to prop?")]
    public bool ApplyUserY = true;

    [Tooltip("Apply the users movement in Z direction to prop?")]
    public bool ApplyUserZ = true;

    private List<Renderer> Renderer = new List<Renderer>();
    private PropsManager propsManager;
    private Animator Animator;

    private bool fadeOut;
    private bool fadeIn;
    private float fadeStart;
    private long UserId;

    // Use this for initialization
    void Start()
    {
        GetComponentsInChildren(true, Renderer);
        Animator = GetComponent<Animator>();

        //Make prop invisible before fading in
        SetOpacity(0, 0, 1);
    }

    public void Init(long userId, PropsManager pm)
    {
        UserId = userId;
        propsManager = pm;
        FadeIn();
    }

    // Update is called once per frame
    void Update()
    {
        //Fade in prop
        if (fadeIn)
        {
            float t = (Time.time - fadeStart) / fadeInDuration;

            if (t > 1)
            {
                fadeIn = false;
            }
            else
            {
                SetOpacity(0, 1, t);
            }
        }
        //Fade out prop
        else if (fadeOut)
        {
            float t = (Time.time - fadeStart) / fadeOutDuration;

            if (t > 1)
            {
                propsManager.RemoveProp(UserId);
            }
            else
            {
                SetOpacity(1, 0, t);
            }
        }
        else if (Animator != null)
        {
            //TODO Überprüfen! Remove prop if animaton has finished
            if (Animator.GetBool("fadeout") && !Animator.GetCurrentAnimatorStateInfo(0).IsName("fadeoutAnimation"))
            {
                propsManager.RemoveProp(UserId);
            }
        }
    }

    private void SetOpacity(float from, float to, float t)
    {
        foreach (Renderer r in Renderer)
        {
            foreach (Material mat in r.materials)
            {
                //Material has to have Rendering Mode set to 'Fade'
                mat.color = new Vector4(mat.color.r, mat.color.g, mat.color.b, Mathf.SmoothStep(from, to, t));
            }
        }
    }

    private void FadeIn()
    {
        if (Animator != null)
        {
            Animator.SetBool("fadein", true);
        }
        else
        {
            fadeStart = Time.time;
            fadeIn = true;
        }
    }

    public void FadeOut()
    {
        if (Animator != null)
        {
            Animator.SetBool("fadeout", true);
        }
        else
        {
            fadeStart = Time.time;
            fadeOut = true;
        }
    }

    public bool IsFadingOut()
    {
        return Animator != null ? Animator.GetBool("fadeout") : fadeOut;
    }

}
