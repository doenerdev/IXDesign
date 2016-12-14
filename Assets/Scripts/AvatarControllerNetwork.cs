using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;

public class AvatarControllerNetwork : AvatarController, IOSCSubscriber
{

    private Dictionary<int, Quaternion> _jointOrientations = new Dictionary<int, Quaternion>();
    private ushort _networkTrackedFrames = 0;
    private Animator _animator;

    [SerializeField] [Range(0, 60)] private ushort _networkAnimationFrameThreshold = 0;


    private new void Awake()
    {
        base.Awake();

        //register as a subscriber to the network handler for network messages
        NetworkHandler.Instance.Subscribe(this);

        for (int i = 0; i < 31; i++)
        {
            _jointOrientations[i] = Quaternion.identity;
        }

        _animator = GetComponent<Animator>();
        _animator.enabled = false;
    }

    private void Update()
    {
        UpdateTrackedFrameCounter();

        finitStateMachine.CurrentState.Reason();
        finitStateMachine.CurrentState.Act();

        /*if (isTracked == false)
        {
            ReturnToNetworkOrigin();
        }*/
    }

    /// <summary>
    /// Updates the avatar dependent on incoming network data
    /// </summary>
    /// <param name="UserID">User ID</param>
    public void UpdateAvatar(Int64 UserID, Dictionary<int, Quaternion> jointOrientations, Vector3 position)
    {
        if (!gameObject.activeInHierarchy || isAnimationRunning)
            return;

        // Get the KinectManager instance
        if (kinectManager == null)
        {
            kinectManager = KinectManager.Instance;
        }

        // move the avatar to its Kinect position
        if (!externalRootMotion)
        {
            MoveAvatar(UserID, position);
        }

        // get the left hand state and event
        if (kinectManager && kinectManager.GetJointTrackingState(UserID, (int)KinectInterop.JointType.HandLeft) != KinectInterop.TrackingState.NotTracked)
        {
            KinectInterop.HandState leftHandState = kinectManager.GetLeftHandState(UserID);
            InteractionManager.HandEventType leftHandEvent = InteractionManager.HandStateToEvent(leftHandState, lastLeftHandEvent);

            if (leftHandEvent != InteractionManager.HandEventType.None)
            {
                lastLeftHandEvent = leftHandEvent;
            }
        }

        // get the right hand state and event
        if (kinectManager && kinectManager.GetJointTrackingState(UserID, (int)KinectInterop.JointType.HandRight) != KinectInterop.TrackingState.NotTracked)
        {
            KinectInterop.HandState rightHandState = kinectManager.GetRightHandState(UserID);
            InteractionManager.HandEventType rightHandEvent = InteractionManager.HandStateToEvent(rightHandState, lastRightHandEvent);

            if (rightHandEvent != InteractionManager.HandEventType.None)
            {
                lastRightHandEvent = rightHandEvent;
            }
        }

        // rotate the avatar bones
        for (var boneIndex = 0; boneIndex < bones.Length; boneIndex++)
        {
            if (!bones[boneIndex] || isBoneDisabled[boneIndex])
            {
                continue;
                Debug.Log("Continue..........");
            }

            if (boneIndex2JointMap.ContainsKey(boneIndex))
            {
                KinectInterop.JointType joint = !mirroredMovement ? boneIndex2JointMap[boneIndex] : boneIndex2MirrorJointMap[boneIndex];
                TransformBone(joint, boneIndex, !mirroredMovement, jointOrientations[boneIndex]);
            }
        }
    }

    // Apply the rotations tracked by kinect to the joints.
    protected void TransformBone(KinectInterop.JointType joint, int boneIndex, bool flip, Quaternion jointRotation)
    {
   
        Transform boneTransform = bones[boneIndex];
        if (boneTransform == null || kinectManager == null)
            return;

        int iJoint = (int)joint;
        if (iJoint < 0)
            return;

        // Get Kinect joint orientation
        if (jointRotation == Quaternion.identity)
            return;

      

        // calculate the new orientation
        Quaternion newRotation = Kinect2AvatarRot(jointRotation, boneIndex);

        if (externalRootMotion)
        {
            newRotation = transform.rotation * newRotation;
        }

        // Smoothly transition to the new rotation
        if (smoothFactor != 0f)
            boneTransform.rotation = Quaternion.Slerp(boneTransform.rotation, newRotation, smoothFactor * Time.deltaTime);
        else
            boneTransform.rotation = newRotation;
    }

    /// <summary>
    /// Gets called when there is an incoming osc message
    /// </summary>
    public void OSCMessageUpdate(string message)
    {
        //early out if the threshold of needed tracked frames wasn't reached yet
        if (_networkTrackedFrames < _networkAnimationFrameThreshold)
            return;

        var data = LiteralDataHelper.LiteralDataToJointOrientations(message);
        var position = LiteralDataHelper.LiteralDataToRootPosition(message);
        UpdateAvatar(playerId, data, position);
    }

    /// <summary>
    /// Updates the counter for the tracked frames (network-wise)
    /// </summary>
    private void UpdateTrackedFrameCounter()
    {
        if (isTracked)
        {
            _networkTrackedFrames++;
        }
        else
        {
            _networkTrackedFrames = 0;
        }
    }

    private void ReturnToNetworkOrigin()
    {
        Vector3 targetPosition = new Vector3(GameManager.Instance.NetworkCharacterOrigin.position.x, GameManager.Instance.YPlanePosition, GameManager.Instance.ZPlanePosition);

        if (transform.position != targetPosition)
        {
            if (_animator.enabled == false)
            {
                _animator.enabled = true;
                _animator.SetBool("Walking", true);
                isAnimationRunning = true;

                if (transform.position.x < targetPosition.x)
                {
                    transform.eulerAngles = new Vector3(transform.rotation.eulerAngles.x, 90, transform.rotation.eulerAngles.z);
                }
                else
                {
                    transform.eulerAngles = new Vector3(transform.rotation.eulerAngles.x, -90, transform.rotation.eulerAngles.z);
                }
            }


            transform.position = Vector3.MoveTowards(transform.position, targetPosition, originReturnSpeed);
        }
        else
        {
            DisableAnimator();
        }

    }

    private void DisableAnimator()
    {
        _animator.SetBool("Walking", false);
        transform.eulerAngles = new Vector3(0, 0, 0);
        isAnimationRunning = false;
        _animator.enabled = false;
    }

    /// <summary>
    /// Gets called when the tracking state of the corresponding AvatarController changes
    /// </summary>
    public override void UpdateTrackingState(bool state)
    {
        isTracked = state;
        if (isTracked == true)
        {
            DisableAnimator();
        }
    }

    protected override FSMSystem CreateFSM()
    {
        WalkToOriginState walkToOrigin = new WalkToOriginState(gameObject, this, true);
        walkToOrigin.AddTransition(Transition.ArrivedAtOrigin, StateID.NetworkIdle);

        NetworkIdleState networkIdle = new NetworkIdleState(gameObject, this);
        networkIdle.AddTransition(Transition.NetworkControlStarted, StateID.NetworkControl);

        NetworkControlState networkControl = new NetworkControlState(gameObject, this);
        networkControl.AddTransition(Transition.NetworkControlEnded, StateID.WalkingToOrigin);

        FSMSystem fsm = new FSMSystem();
        fsm.AddState(walkToOrigin);
        fsm.AddState(networkIdle);
        fsm.AddState(networkControl);

        return fsm;
    }
}
