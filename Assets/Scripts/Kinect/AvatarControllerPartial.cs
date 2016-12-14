using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;

/*
public partial class AvatarController : MonoBehaviour {

    private CharacterHandler charHandler;
    private bool isTracked;

    [SerializeField]
    private CharacterHandler networkCharHandler;

    public CharacterHandler CharHandler
    {
        get
        {
            return charHandler;
        }
    }


    /// <summary>
	/// Updates the avatar dependent on incoming network data
	/// </summary>
	/// <param name="UserID">User ID</param>
    public void UpdateAvatarNetwork(Int64 UserID, Dictionary<int, Quaternion> jointOrientations)
    {
        
        if (!gameObject.activeInHierarchy || (charHandler.NetworkCharacter && charHandler.AnimationRunning))
            return;

        // Get the KinectManager instance
        if (kinectManager == null)
        {
            kinectManager = KinectManager.Instance;
        }

        // move the avatar to its Kinect position
        if (!externalRootMotion)
        {
            MoveAvatar(UserID);
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
                TransformBoneNetwork(UserID, joint, boneIndex, !mirroredMovement, jointOrientations[boneIndex]);
            }
        }
    }

    // Apply the rotations tracked by kinect to the joints.
    protected void TransformBoneNetwork(Int64 userId, KinectInterop.JointType joint, int boneIndex, bool flip, Quaternion jointRotation)
    {
        Transform boneTransform = bones[boneIndex];
        if (boneTransform == null || kinectManager == null)
            return;

        int iJoint = (int)joint;
        if (iJoint < 0 || !kinectManager.IsJointTracked(userId, iJoint))
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

}
*/