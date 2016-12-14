using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public class AvatarControllerLocal : AvatarController
{

    private Animator _animator;
    private int _lastMessageID = 1;
    private int _maxMessageID = 99999;

    [SerializeField] private AvatarControllerNetwork _networkController;

    public AvatarControllerNetwork NetworkController
    {
        get { return _networkController; }
    }

    private new void Awake()
    {
        base.Awake();
        _animator = GetComponent<Animator>();
        _animator.enabled = false;
    }

    private void Update()
    {
        if (isTracked)
        {
            SendKinectData();
        }

        finitStateMachine.CurrentState.Reason();
        finitStateMachine.CurrentState.Act();
    }

    private void SendIsTrackedFlag(bool flag)
    {
        NetworkHandler.Instance.SendOSCTrackingFlag(flag);
    }

    /// <summary>
    /// Sends the kinect data of the current frame to the network via OSC
    /// </summary>
    private void SendKinectData()
    {
        Dictionary<int, Quaternion> jointOrientations = new Dictionary<int, Quaternion>();

        //early out if there there is no kinectManager
        if ( KinectManager.Instance == null)
            return;

        //loop through all bones and gather the corresponding kinect joint orientations
        for (int i = 0; i < 31; i++)
        {
            if (BoneIndex2JointMap.ContainsKey(i))
            {
                KinectInterop.JointType joint = !mirroredMovement
                    ? BoneIndex2JointMap[i]
                    : BoneIndex2MirrorJointMap[i];
                var jointIndex = (int)joint;
                jointOrientations[i] = KinectManager.Instance.GetJointOrientation(playerId, jointIndex,
                    !mirroredMovement);
            }

        }

        Vector3 rootPosition = KinectManager.Instance.GetUserPosition(playerId);

        //convert the orientation data to a string for network transport
        string literalData = LiteralDataHelper.JointOrientationsToLiteralData(jointOrientations, rootPosition);

        //send the data via osc
        int messageID;
        if (_lastMessageID > _maxMessageID)
        {
            messageID = 0;
        }
        else
        {
            messageID = _lastMessageID + 1;
        }
        _lastMessageID = messageID;
        NetworkHandler.Instance.SendOSCKinectData(literalData, messageID);
    }

    /// <summary>
    /// Updates the avatar each frame.
    /// </summary>
    /// <param name="UserID">User ID</param>
    public void UpdateAvatar(Int64 UserID)
    {
        if (!gameObject.activeInHierarchy)
            return;

        // Get the KinectManager instance
        if (kinectManager == null)
        {
            kinectManager = KinectManager.Instance;
        }

        //AH
        if (externalRootRotation)
        {
            transform.rotation = kinectManager.GetExternalRootRotation();
        }

        // move the avatar to its Kinect position
        if (!externalRootMotion && !kinectManager.IsJointTracked(UserID, (int)KinectInterop.JointType.SpineBase))
        {
            // get the position of user's spine base
            Vector3 trans = kinectManager.GetUserPosition(UserID);
            MoveAvatar(UserID, trans);
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
                continue;

            if (boneIndex2JointMap.ContainsKey(boneIndex))
            {
                KinectInterop.JointType joint = !mirroredMovement ? boneIndex2JointMap[boneIndex] : boneIndex2MirrorJointMap[boneIndex];
                TransformBone(UserID, joint, boneIndex, !mirroredMovement);

            }
            else if (specIndex2JointMap.ContainsKey(boneIndex)) //special bones are ShoulderLeft and ShoulderRight
            {
                // special bones (clavicles)
                List<KinectInterop.JointType> alJoints = !mirroredMovement ? specIndex2JointMap[boneIndex] : specIndex2MirrorMap[boneIndex];

                if (alJoints.Count >= 2)
                {
                    //Debug.Log(alJoints[0].ToString());
                    Vector3 baseDir = alJoints[0].ToString().EndsWith("Left") ? Vector3.left : Vector3.right;
                    TransformSpecialBone(UserID, alJoints[0], alJoints[1], boneIndex, baseDir, !mirroredMovement);
                }
            }
        }
    }

    // Apply the rotations tracked by kinect to the joints.
    protected void TransformBone(Int64 userId, KinectInterop.JointType joint, int boneIndex, bool flip)
    {
        Transform boneTransform = bones[boneIndex];
        if (boneTransform == null || kinectManager == null)
            return;

        int iJoint = (int)joint;
        if (iJoint < 0 || !kinectManager.IsJointTracked(userId, iJoint))
            return;

        // Get Kinect joint orientation
        Quaternion jointRotation = kinectManager.GetJointOrientation(userId, iJoint, flip);
        if (jointRotation == Quaternion.identity)
            return;

        // calculate the new orientation
        Quaternion newRotation = Kinect2AvatarRot(jointRotation, boneIndex);

        //AH
        if (externalRootRotation)
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
    /// Updates the the tracking state (on changes) in the corresponding CharacterHandler
    /// </summary>
    public override void UpdateTrackingState(bool isTracked)
    {
        if (isTracked != this.isTracked)
        {
            this.isTracked = isTracked;
            SendIsTrackedFlag(isTracked);
            //NetworkController.UpdateTrackingState(isTracked);
        }

    }

    protected override FSMSystem CreateFSM()
    {
        WalkToOriginState walkToOrigin = new WalkToOriginState(gameObject, this, false);
        walkToOrigin.AddTransition(Transition.ArrivedAtOrigin, StateID.LocalIdle);

        LocalIdleState localIdle = new LocalIdleState(gameObject, this);
        localIdle.AddTransition(Transition.LocalControlStarted, StateID.LocalControl);

        LocalControlState localControl = new LocalControlState(gameObject, this);
        localControl.AddTransition(Transition.LocalControlEnded, StateID.WalkingToOrigin);

        FSMSystem fsm = new FSMSystem();
        fsm.AddState(walkToOrigin);
        fsm.AddState(localIdle);
        fsm.AddState(localControl);

        return fsm;
    }
}
