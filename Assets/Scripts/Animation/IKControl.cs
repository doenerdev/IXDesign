using UnityEngine;
using System;
using System.Collections;


public enum IKControlMode
{
    LeftHand,
    RightHand,
}

[RequireComponent(typeof(Animator))]
public class IKControl : MonoBehaviour
{

    protected Animator animator;

    [SerializeField] private bool _ikActive = false;
    [SerializeField] private Transform _rightHand = null;
    [SerializeField] private Transform _leftHand = null;
    [SerializeField] private IKControlMode _controlMode = IKControlMode.RightHand;
    [SerializeField][Range(0.0f, 0.1f)] private float _speed;

    private void Start()
    {
        animator = GetComponent<Animator>();
    }
    
    private void Update()
    {
        if (Input.GetKeyDown("a"))
        {
            if(_controlMode == IKControlMode.LeftHand)
                _controlMode = IKControlMode.RightHand;
            else
                _controlMode = IKControlMode.LeftHand;
        }

        HandleIKInput();
    }

    private void OnAnimatorIK()
    {
        Debug.Log("Test");
        if (animator)
        {
            if (_ikActive)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1.0f);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1.0f);
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1.0f);
                animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1.0f);

                if (AvatarIKGoal.LeftHand != null)
                {
                    animator.SetIKPosition(AvatarIKGoal.LeftHand, _leftHand.position);
                    animator.SetIKRotation(AvatarIKGoal.LeftHand, _leftHand.rotation);
                }
                if (AvatarIKGoal.RightHand != null)
                {
                    animator.SetIKPosition(AvatarIKGoal.RightHand, _rightHand.position);
                    animator.SetIKRotation(AvatarIKGoal.RightHand, _rightHand.rotation);
                }

            }

            else
            {
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0);
                animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0);
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0);
            }
        }
    }

    private void HandleIKInput()
    {
        Transform target;
        if (_controlMode == IKControlMode.LeftHand)
            target = _leftHand;
        else
            target = _rightHand;


        if (Input.GetKey("up"))
        {
            target.transform.position = new Vector3(target.transform.position.x, target.transform.position.y + _speed, target.transform.position.z);
        }
        else if (Input.GetKey("down"))
        {
            target.transform.position = new Vector3(target.transform.position.x, target.transform.position.y - _speed, target.transform.position.z);
        }
        else if (Input.GetKey("left"))
        {
            target.transform.position = new Vector3(target.transform.position.x + _speed, target.transform.position.y, target.transform.position.z);
        }
        else if (Input.GetKey("right"))
        {
            target.transform.position = new Vector3(target.transform.position.x - _speed, target.transform.position.y, target.transform.position.z);
        }
        else if (Input.GetKey("page up"))
        {
            target.transform.position = new Vector3(target.transform.position.x, target.transform.position.y, target.transform.position.z - _speed);
        }
        else if (Input.GetKey("page down"))
        {
            target.transform.position = new Vector3(target.transform.position.x, target.transform.position.y, target.transform.position.z + _speed);
        }
    }
}