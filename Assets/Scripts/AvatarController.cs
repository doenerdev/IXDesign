﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;

[RequireComponent(typeof(Animator))]
public abstract class AvatarController : MonoBehaviour {


    [Tooltip("Index of the player, tracked by this component. 0 means the 1st player, 1 - the 2nd one, 2 - the 3rd one, etc.")]
    public int playerIndex = 0;

    //AH
    [Tooltip("Whether the avatar's root rotation is applied by other component or script.")]
    public bool externalRootRotation = true;

    [Tooltip("Whether the avatar is facing the player or not.")]
    public bool mirroredMovement = false;

    [Tooltip("Whether the avatar is allowed to move vertically or not.")]
    public bool verticalMovement = false;

    [Tooltip("Whether the avatar's root motion is applied by other component or script.")]
    public bool externalRootMotion = false;

    [Tooltip("Whether the finger orientations are allowed or not.")]
    public bool fingerOrientations = false;

    [Tooltip("Rate at which the avatar will move through the scene.")]
    public float moveRate = 1f;

    [Tooltip("Smooth factor used for avatar movements and joint rotations.")]
    public float smoothFactor = 5f;

    [Tooltip("Game object this transform is relative to (optional).")]
    public GameObject offsetNode;

    [Tooltip("If enabled, makes the avatar position relative to this camera to be the same as the player's position to the sensor.")]
    public Camera posRelativeToCamera;

    [Tooltip("Whether the avatar's position should match the color image (in Pos-rel-to-camera mode only).")]
    public bool posRelOverlayColor = false;

    [Tooltip("Whether z-axis movement needs to be inverted (Pos-Relative mode only).")]
    public bool posRelInvertedZ = false;

    [Tooltip("Whether the avatar's feet must stick to the ground.")]
    public bool groundedFeet = false;

    [Tooltip("Vertical offset of the avatar with respect to the position of user's spine-base.")]
    [Range(-0.5f, 0.5f)]
    public float verticalOffset = 0f;

    [Tooltip("Forward (Z) offset of the avatar with respect to the position of user's spine-base.")]
    [Range(-0.5f, 0.5f)]
    public float forwardOffset = 0f;

    [Tooltip("Whether to move the character on the z-axis.")]
    public bool ZMovement = true;

    // userId of the player
    [NonSerialized]
    public Int64 playerId = 0;


    // The body root node
    protected Transform bodyRoot;

    // Variable to hold all them bones. It will initialize the same size as initialRotations.
    protected Transform[] bones;

    // Rotations of the bones when the Kinect tracking starts.
    protected Quaternion[] initialRotations;
    protected Quaternion[] localRotations;
    protected bool[] isBoneDisabled;

    // Local rotations of finger bones
    protected Dictionary<HumanBodyBones, Quaternion> fingerBoneLocalRotations = new Dictionary<HumanBodyBones, Quaternion>();
    protected Dictionary<HumanBodyBones, Vector3> fingerBoneLocalAxes = new Dictionary<HumanBodyBones, Vector3>();

    // Initial position and rotation of the transform
    protected Vector3 initialPosition;
    protected Quaternion initialRotation;
    protected Vector3 offsetNodePos;
    protected Quaternion offsetNodeRot;
    protected Vector3 bodyRootPosition;

    // Calibration Offset Variables for Character Position.
    protected bool offsetCalibrated = false;
    protected Vector3 offsetPos = Vector3.zero;
    //protected float xOffset, yOffset, zOffset;
    //protected Quaternion originalRotation;

    // whether the parent transform obeys physics
    protected bool isRigidBody = false;

    // protected instance of the KinectManager
    protected KinectManager kinectManager;

    // last hand events
    protected InteractionManager.HandEventType lastLeftHandEvent = InteractionManager.HandEventType.Release;
    protected InteractionManager.HandEventType lastRightHandEvent = InteractionManager.HandEventType.Release;

    // fist states
    protected bool bLeftFistDone = false;
    protected bool bRightFistDone = false;

    // grounder constants and variables
    protected const int raycastLayers = ~2;  // Ignore Raycast
    protected const float maxFootDistanceGround = 0.02f;  // maximum distance from lower foot to the ground
    protected const float maxFootDistanceTime = 0.2f; // 1.0f;  // maximum allowed time, the lower foot to be distant from the ground
    protected Transform leftFoot, rightFoot;

    protected float fFootDistanceInitial = 0f;
    protected float fFootDistance = 0f;
    protected float fFootDistanceTime = 0f;
    
    //determines whether the avatar controller is currently tracked and controlled by kinect data
    protected bool isTracked = false;
    //determines whether there's an animation playing
    protected bool isAnimationRunning = false;
    //the finite state machine
    protected FSMSystem finitStateMachine;

    //the speed with wich the character moves to its initial position
    [SerializeField]
    [Range(0f, 0.1f)]
    protected float originReturnSpeed;

    [Tooltip("A maximum movement distance threshold to preevent location hopping when tracking is incorrect or tracking is lost")]
    [SerializeField][Range(0f, 2f)] protected float maxMovementThreshold;

    [SerializeField] protected GameObject meshes;
    [SerializeField] protected ParticleSystem userControlParticleEffect;

    public bool IsTracked
    {
        get { return isTracked; }
    }

    public bool IsAnimationRunning
    {
        get { return isAnimationRunning; }
        set { isAnimationRunning = value; }
    }

    public float OriginReturnSpeed
    {
        get { return originReturnSpeed; }
    }

    public Animator Animator
    {
        get { return GetComponent<Animator>(); }
    }

    public GameObject Meshes
    {
        get { return meshes; }
    }

    public ParticleSystem UserControlParticleEffect
    {
        get { return userControlParticleEffect; }
    }


    /// <summary>
    /// Gets the number of bone transforms (array length).
    /// </summary>
    /// <returns>The number of bone transforms.</returns>
    public int GetBoneTransformCount()
    {
        return bones != null ? bones.Length : 0;
    }

    /// <summary>
    /// Gets the bone transform by index.
    /// </summary>
    /// <returns>The bone transform.</returns>
    /// <param name="index">Index</param>
    public Transform GetBoneTransform(int index)
    {
        if (index >= 0 && index < bones.Length)
        {
            return bones[index];
        }

        return null;
    }

    /// <summary>
    /// Disables the bone and optionally resets its orientation.
    /// </summary>
    /// <param name="index">Bone index.</param>
    /// <param name="resetBone">If set to <c>true</c> resets bone orientation.</param>
    public void DisableBone(int index, bool resetBone)
    {
        if (index >= 0 && index < bones.Length)
        {
            isBoneDisabled[index] = true;

            if (resetBone && bones[index] != null)
            {
                bones[index].rotation = localRotations[index];
            }
        }
    }

    /// <summary>
    /// Enables the bone, so AvatarController could update its orientation.
    /// </summary>
    /// <param name="index">Bone index.</param>
    public void EnableBone(int index)
    {
        if (index >= 0 && index < bones.Length)
        {
            isBoneDisabled[index] = false;
        }
    }

    /// <summary>
    /// Determines whether the bone orientation update is enabled or not.
    /// </summary>
    /// <returns><c>true</c> if the bone update is enabled; otherwise, <c>false</c>.</returns>
    /// <param name="index">Bone index.</param>
    public bool IsBoneEnabled(int index)
    {
        if (index >= 0 && index < bones.Length)
        {
            return !isBoneDisabled[index];
        }

        return false;
    }

    /// <summary>
    /// Gets the bone index by joint type.
    /// </summary>
    /// <returns>The bone index.</returns>
    /// <param name="joint">Joint type</param>
    /// <param name="bMirrored">If set to <c>true</c> gets the mirrored joint index.</param>
    public int GetBoneIndexByJoint(KinectInterop.JointType joint, bool bMirrored)
    {
        int boneIndex = -1;

        if (jointMap2boneIndex.ContainsKey(joint))
        {
            boneIndex = !bMirrored ? jointMap2boneIndex[joint] : mirrorJointMap2boneIndex[joint];
        }

        return boneIndex;
    }

    /// <summary>
    /// Gets the special index by two joint types.
    /// </summary>
    /// <returns>The spec index by joint.</returns>
    /// <param name="joint1">Joint 1 type.</param>
    /// <param name="joint2">Joint 2 type.</param>
    /// <param name="bMirrored">If set to <c>true</c> gets the mirrored joint index.</param>
    public int GetSpecIndexByJoint(KinectInterop.JointType joint1, KinectInterop.JointType joint2, bool bMirrored)
    {
        int boneIndex = -1;

        if ((joint1 == KinectInterop.JointType.ShoulderLeft && joint2 == KinectInterop.JointType.SpineShoulder) ||
           (joint2 == KinectInterop.JointType.ShoulderLeft && joint1 == KinectInterop.JointType.SpineShoulder))
        {
            return (!bMirrored ? 25 : 26);
        }
        else if ((joint1 == KinectInterop.JointType.ShoulderRight && joint2 == KinectInterop.JointType.SpineShoulder) ||
                (joint2 == KinectInterop.JointType.ShoulderRight && joint1 == KinectInterop.JointType.SpineShoulder))
        {
            return (!bMirrored ? 26 : 25);
        }
        else if ((joint1 == KinectInterop.JointType.HandTipLeft && joint2 == KinectInterop.JointType.HandLeft) ||
                (joint2 == KinectInterop.JointType.HandTipLeft && joint1 == KinectInterop.JointType.HandLeft))
        {
            return (!bMirrored ? 27 : 28);
        }
        else if ((joint1 == KinectInterop.JointType.HandTipRight && joint2 == KinectInterop.JointType.HandRight) ||
                (joint2 == KinectInterop.JointType.HandTipRight && joint1 == KinectInterop.JointType.HandRight))
        {
            return (!bMirrored ? 28 : 27);
        }
        else if ((joint1 == KinectInterop.JointType.ThumbLeft && joint2 == KinectInterop.JointType.HandLeft) ||
                (joint2 == KinectInterop.JointType.ThumbLeft && joint1 == KinectInterop.JointType.HandLeft))
        {
            return (!bMirrored ? 29 : 30);
        }
        else if ((joint1 == KinectInterop.JointType.ThumbRight && joint2 == KinectInterop.JointType.HandRight) ||
                (joint2 == KinectInterop.JointType.ThumbRight && joint1 == KinectInterop.JointType.HandRight))
        {
            return (!bMirrored ? 30 : 29);
        }

        return boneIndex;
    }


    // transform caching gives performance boost since Unity calls GetComponent<Transform>() each time you call transform 
    protected Transform _transformCache;
    public new Transform transform
    {
        get
        {
            if (!_transformCache)
            {
                _transformCache = base.transform;
            }

            return _transformCache;
        }
    }

    protected abstract FSMSystem CreateFSM();

    public void SetTransition(Transition t) { finitStateMachine.PerformTransition(t); }

    public void Awake()
    {
        // check for double start
        if (bones != null)
            return;
        if (!gameObject.activeInHierarchy)
            return;

        // inits the bones array
        bones = new Transform[31];

        // Map bones to the points the Kinect tracks
        MapBones();

        // Set model's arms to be in T-pose, if needed
        SetModelArmsInTpose();

        // Initial rotations and directions of the bones.
        initialRotations = new Quaternion[bones.Length];
        localRotations = new Quaternion[bones.Length];
        isBoneDisabled = new bool[bones.Length];

        // Get initial bone rotations
        GetInitialRotations();

        // enable all bones
        for (int i = 0; i < bones.Length; i++)
        {
            isBoneDisabled[i] = false;
        }

        // get initial distance to ground
        fFootDistanceInitial = GetDistanceToGround();
        fFootDistance = 0f;
        fFootDistanceTime = 0f;

        // if parent transform uses physics
        isRigidBody = (gameObject.GetComponent<Rigidbody>() != null);

        finitStateMachine = CreateFSM();
    }

    /// <summary>
    /// Updates the the tracking state (on changes) in the corresponding CharacterHandler
    /// </summary>
    public abstract void UpdateTrackingState(bool isTracked);
    
    /// <summary>
    /// Resets bones to their initial positions and rotations.
    /// </summary>
    public virtual void ResetToInitialPosition(bool moveAvatar = true)
    {
        playerId = 0;

        if (bones == null)
            return;

        // For each bone that was defined, reset to initial position.
        transform.rotation = Quaternion.identity;

        for (int pass = 0; pass < 2; pass++)  // 2 passes because clavicles are at the end
        {
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null)
                {
                    bones[i].rotation = initialRotations[i];
                }
            }
        }

        // reset finger bones to initial position
        Animator animatorComponent = GetComponent<Animator>();
        foreach (HumanBodyBones bone in fingerBoneLocalRotations.Keys)
        {
            Transform boneTransform = animatorComponent ? animatorComponent.GetBoneTransform(bone) : null;

            if (boneTransform)
            {
                boneTransform.localRotation = fingerBoneLocalRotations[bone];
            }
        }

        //		if(bodyRoot != null)
        //		{
        //			bodyRoot.localPosition = Vector3.zero;
        //			bodyRoot.localRotation = Quaternion.identity;
        //		}

        // Restore the offset's position and rotation
        if (offsetNode != null)
        {
            if (moveAvatar)
            {
                offsetNode.transform.position = offsetNodePos;
            }
            offsetNode.transform.rotation = offsetNodeRot;
        }

        if (moveAvatar)
        {
            transform.position = initialPosition;
        }

        transform.rotation = initialRotation;
    }

    /// <summary>
    /// Invoked on the successful calibration of the player.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    public virtual void SuccessfulCalibration(Int64 userId)
    {
        playerId = userId;

        // reset the models position
        if (offsetNode != null)
        {
            offsetNode.transform.position = offsetNodePos;
            offsetNode.transform.rotation = offsetNodeRot;
        }

        transform.position = initialPosition;
        transform.rotation = initialRotation;

        //		// enable all bones
        //		for(int i = 0; i < bones.Length; i++)
        //		{
        //			isBoneDisabled[i] = false;
        //		}

        // re-calibrate the position offset
        offsetCalibrated = false;
    }


    // Apply the rotations tracked by kinect to a special joint
    protected void TransformSpecialBone(Int64 userId, KinectInterop.JointType joint, KinectInterop.JointType jointParent, int boneIndex, Vector3 baseDir, bool flip)
    {
        Transform boneTransform = bones[boneIndex];
        if (boneTransform == null || kinectManager == null)
            return;

        if (!kinectManager.IsJointTracked(userId, (int)joint) ||
           !kinectManager.IsJointTracked(userId, (int)jointParent))
        {
            return;
        }

        if (boneIndex >= 27 && boneIndex <= 30)
        {
            // fingers or thumbs
            if (fingerOrientations)
            {
                TransformSpecialBoneFingers(userId, (int)joint, boneIndex, flip);
            }

            return;
        }

        Vector3 jointDir = kinectManager.GetJointDirection(userId, (int)joint, false, true);
        Quaternion jointRotation = jointDir != Vector3.zero ? Quaternion.FromToRotation(baseDir, jointDir) : Quaternion.identity;

        if (!flip)
        {
            Vector3 mirroredAngles = jointRotation.eulerAngles;
            mirroredAngles.y = -mirroredAngles.y;
            mirroredAngles.z = -mirroredAngles.z;

            jointRotation = Quaternion.Euler(mirroredAngles);
        }

        if (jointRotation != Quaternion.identity)
        {
            // Smoothly transition to the new rotation
            Quaternion newRotation = Kinect2AvatarRot(jointRotation, boneIndex);

            //AH
            if (externalRootRotation)
            {
                newRotation = transform.rotation * newRotation;
            }

            if (smoothFactor != 0f)
                boneTransform.rotation = Quaternion.Slerp(boneTransform.rotation, newRotation, smoothFactor * Time.deltaTime);
            else
                boneTransform.rotation = newRotation;
        }

    }

    // Apply the rotations tracked by kinect to fingers (one joint = multiple bones)
    protected void TransformSpecialBoneFingers(Int64 userId, int joint, int boneIndex, bool flip)
    {
        // check for hand grips
        if (joint == (int)KinectInterop.JointType.HandTipLeft || joint == (int)KinectInterop.JointType.ThumbLeft)
        {
            if (lastLeftHandEvent == InteractionManager.HandEventType.Grip)
            {
                if (!bLeftFistDone)
                {
                    float angleSign = !mirroredMovement /**(boneIndex == 27 || boneIndex == 29)*/ ? -1f : -1f;
                    float angleRot = angleSign * 60f;

                    TransformSpecialBoneFist(boneIndex, angleRot);
                    bLeftFistDone = (boneIndex >= 29);
                }

                return;
            }
            else if (bLeftFistDone && lastLeftHandEvent == InteractionManager.HandEventType.Release)
            {
                TransformSpecialBoneUnfist(boneIndex);
                bLeftFistDone = !(boneIndex >= 29);
            }
        }
        else if (joint == (int)KinectInterop.JointType.HandTipRight || joint == (int)KinectInterop.JointType.ThumbRight)
        {
            if (lastRightHandEvent == InteractionManager.HandEventType.Grip)
            {
                if (!bRightFistDone)
                {
                    float angleSign = !mirroredMovement /**(boneIndex == 27 || boneIndex == 29)*/ ? -1f : -1f;
                    float angleRot = angleSign * 60f;

                    TransformSpecialBoneFist(boneIndex, angleRot);
                    bRightFistDone = (boneIndex >= 29);
                }

                return;
            }
            else if (bRightFistDone && lastRightHandEvent == InteractionManager.HandEventType.Release)
            {
                TransformSpecialBoneUnfist(boneIndex);
                bRightFistDone = !(boneIndex >= 29);
            }
        }

        // get the animator component
        Animator animatorComponent = GetComponent<Animator>();
        if (!animatorComponent)
            return;

        // Get Kinect joint orientation
        Quaternion jointRotation = kinectManager.GetJointOrientation(userId, joint, flip);
        if (jointRotation == Quaternion.identity)
            return;

        // calculate the new orientation
        Quaternion newRotation = Kinect2AvatarRot(jointRotation, boneIndex);

        //AH
        if (externalRootRotation)
        {
            newRotation = transform.rotation * newRotation;
        }

        // get the list of bones
        //List<HumanBodyBones> alBones = flip ? specialIndex2MultiBoneMap[boneIndex] : specialIndex2MirrorBoneMap[boneIndex];
        List<HumanBodyBones> alBones = specialIndex2MultiBoneMap[boneIndex];

        // Smoothly transition to the new rotation
        for (int i = 0; i < alBones.Count; i++)
        {
            Transform boneTransform = animatorComponent.GetBoneTransform(alBones[i]);
            if (!boneTransform)
                continue;

            if (smoothFactor != 0f)
                boneTransform.rotation = Quaternion.Slerp(boneTransform.rotation, newRotation, smoothFactor * Time.deltaTime);
            else
                boneTransform.rotation = newRotation;
        }
    }

    // Apply the rotations needed to transform fingers to fist
    protected void TransformSpecialBoneFist(int boneIndex, float angle)
    {
        //		// do fist only for fingers
        //		if(boneIndex != 27 && boneIndex != 28)
        //			return;

        // get the animator component
        Animator animatorComponent = GetComponent<Animator>();
        if (!animatorComponent)
            return;

        // get the list of bones
        List<HumanBodyBones> alBones = specialIndex2MultiBoneMap[boneIndex];

        for (int i = 0; i < alBones.Count; i++)
        {
            if (i < 1 && (boneIndex == 29 || boneIndex == 30))  // skip the first two thumb bones
                continue;

            HumanBodyBones bone = alBones[i];
            Transform boneTransform = animatorComponent.GetBoneTransform(bone);

            // set the fist rotation
            if (boneTransform && fingerBoneLocalAxes[bone] != Vector3.zero)
            {
                Quaternion qRotFinger = Quaternion.AngleAxis(angle, fingerBoneLocalAxes[bone]);
                boneTransform.localRotation = fingerBoneLocalRotations[bone] * qRotFinger;
            }
        }

    }

    // Apply the initial rotations fingers
    protected void TransformSpecialBoneUnfist(int boneIndex)
    {
        //		// do fist only for fingers
        //		if(boneIndex != 27 && boneIndex != 28)
        //			return;

        // get the animator component
        Animator animatorComponent = GetComponent<Animator>();
        if (!animatorComponent)
            return;

        // get the list of bones
        List<HumanBodyBones> alBones = specialIndex2MultiBoneMap[boneIndex];

        for (int i = 0; i < alBones.Count; i++)
        {
            HumanBodyBones bone = alBones[i];
            Transform boneTransform = animatorComponent.GetBoneTransform(bone);

            // set the initial rotation
            if (boneTransform)
            {
                boneTransform.localRotation = fingerBoneLocalRotations[bone];
            }
        }
    }

    // Moves the avatar - gets the tracked position of the user and applies it to avatar.
    protected void MoveAvatar(Int64 UserID, Vector3 trans)
    {
        //early out if the position data is likely to result from lost tracking - not perfect
        if (trans == Vector3.zero)
        {
            return;
        }

        if ((moveRate == 0f) || !kinectManager)
        {
            return;
        }

        // use the color overlay position if needed
        if (posRelativeToCamera && posRelOverlayColor)
        {
            Rect backgroundRect = posRelativeToCamera.pixelRect;
            PortraitBackground portraitBack = PortraitBackground.Instance;

            if (portraitBack && portraitBack.enabled)
            {
                backgroundRect = portraitBack.GetBackgroundRect();
            }

            trans = kinectManager.GetJointPosColorOverlay(UserID, (int)KinectInterop.JointType.SpineBase, posRelativeToCamera, backgroundRect);
        }

        // invert the z-coordinate, if needed
        if (posRelativeToCamera && posRelInvertedZ)
        {
            trans.z = -trans.z;
        }

        if (!offsetCalibrated)
        {
            offsetCalibrated = true;

            offsetPos.x = trans.x;  // !mirroredMovement ? trans.x * moveRate : -trans.x * moveRate;
            offsetPos.y = trans.y;  // trans.y * moveRate;
            offsetPos.z = !mirroredMovement && !posRelativeToCamera ? -trans.z : trans.z;  // -trans.z * moveRate;

            if (posRelativeToCamera)
            {
                Vector3 cameraPos = posRelativeToCamera.transform.position;
                Vector3 bodyRootPos = bodyRoot != null ? bodyRoot.position : transform.position;
                Vector3 hipCenterPos = bodyRoot != null ? bodyRoot.position : (bones != null && bones.Length > 0 && bones[0] != null ? bones[0].position : Vector3.zero);

                float yRelToAvatar = 0f;
                if (verticalMovement)
                {
                    yRelToAvatar = (trans.y - cameraPos.y) - (hipCenterPos - bodyRootPos).magnitude;
                }
                else
                {
                    yRelToAvatar = bodyRootPos.y - cameraPos.y;
                }

                Vector3 relativePos = new Vector3(trans.x, yRelToAvatar, trans.z);
                Vector3 newBodyRootPos = cameraPos + relativePos;
                if (ZMovement == false)
                    newBodyRootPos.z = transform.position.z;

                //				if(offsetNode != null)
                //				{
                //					newBodyRootPos += offsetNode.transform.position;
                //				}

                if (bodyRoot != null)
                {
                    bodyRoot.position = newBodyRootPos;
                }
                else
                {
                    transform.position = newBodyRootPos;
                }

                bodyRootPosition = newBodyRootPos;
            }
        }

        // transition to the new position
        Vector3 targetPos = bodyRootPosition + Kinect2AvatarPos(trans, verticalMovement);

        if (isRigidBody && !verticalMovement)
        {
            // workaround for obeying the physics (e.g. gravity falling)
            targetPos.y = bodyRoot != null ? bodyRoot.position.y : transform.position.y;
        }

        if (verticalMovement && verticalOffset != 0f &&
            bones[0] != null && bones[3] != null)
        {
            Vector3 dirSpine = bones[3].position - bones[0].position;
            targetPos += dirSpine.normalized * verticalOffset;
        }

        if (forwardOffset != 0f &&
            bones[0] != null && bones[3] != null && bones[5] != null && bones[11] != null)
        {
            Vector3 dirSpine = (bones[3].position - bones[0].position).normalized;
            Vector3 dirShoulders = (bones[11].position - bones[5].position).normalized;
            Vector3 dirForward = Vector3.Cross(dirShoulders, dirSpine).normalized;

            targetPos += dirForward * forwardOffset;
        }

        if (groundedFeet)
        {
            // keep the current correction
            float fLastTgtY = targetPos.y;
            targetPos.y += fFootDistance;

            float fNewDistance = GetDistanceToGround();
            float fNewDistanceTime = Time.time;

            //			Debug.Log(string.Format("PosY: {0:F2}, LastY: {1:F2},  TgrY: {2:F2}, NewDist: {3:F2}, Corr: {4:F2}, Time: {5:F2}", bodyRoot != null ? bodyRoot.position.y : transform.position.y,
            //				fLastTgtY, targetPos.y, fNewDistance, fFootDistance, fNewDistanceTime));

            if (Mathf.Abs(fNewDistance) >= 0.01f && Mathf.Abs(fNewDistance - fFootDistanceInitial) >= maxFootDistanceGround)
            {
                if ((fNewDistanceTime - fFootDistanceTime) >= maxFootDistanceTime)
                {
                    fFootDistance += (fNewDistance - fFootDistanceInitial);
                    fFootDistanceTime = fNewDistanceTime;

                    targetPos.y = fLastTgtY + fFootDistance;

                    //					Debug.Log(string.Format("   >> change({0:F2})! - Corr: {1:F2}, LastY: {2:F2},  TgrY: {3:F2} at time {4:F2}", 
                    //								(fNewDistance - fFootDistanceInitial), fFootDistance, fLastTgtY, targetPos.y, fFootDistanceTime));
                }
            }
            else
            {
                fFootDistanceTime = fNewDistanceTime;
            }
        }

        if (ZMovement == false)
            targetPos.z = transform.position.z;

        if (bodyRoot)
        {
            bodyRoot.position = smoothFactor != 0f ?
                Vector3.Lerp(bodyRoot.position, targetPos, smoothFactor * Time.deltaTime) : targetPos;
        }
        else
        {
            transform.position = smoothFactor != 0f ?
                Vector3.Lerp(transform.position, targetPos, smoothFactor * Time.deltaTime) : targetPos;
        }
    }

    // Set model's arms to be in T-pose
    protected void SetModelArmsInTpose()
    {
        Vector3 vTposeLeftDir = transform.TransformDirection(Vector3.left);
        Vector3 vTposeRightDir = transform.TransformDirection(Vector3.right);
        //Animator animator = GetComponent<Animator>();

        Transform transLeftUarm = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.ShoulderLeft, false)); // animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform transLeftLarm = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.ElbowLeft, false)); // animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        Transform transLeftHand = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.WristLeft, false)); // animator.GetBoneTransform(HumanBodyBones.LeftHand);

        if (transLeftUarm != null && transLeftLarm != null)
        {
            Vector3 vUarmLeftDir = transLeftLarm.position - transLeftUarm.position;
            float fUarmLeftAngle = Vector3.Angle(vUarmLeftDir, vTposeLeftDir);

            if (Mathf.Abs(fUarmLeftAngle) >= 5f)
            {
                Quaternion vFixRotation = Quaternion.FromToRotation(vUarmLeftDir, vTposeLeftDir);
                transLeftUarm.rotation = vFixRotation * transLeftUarm.rotation;
            }

            if (transLeftHand != null)
            {
                Vector3 vLarmLeftDir = transLeftHand.position - transLeftLarm.position;
                float fLarmLeftAngle = Vector3.Angle(vLarmLeftDir, vTposeLeftDir);

                if (Mathf.Abs(fLarmLeftAngle) >= 5f)
                {
                    Quaternion vFixRotation = Quaternion.FromToRotation(vLarmLeftDir, vTposeLeftDir);
                    transLeftLarm.rotation = vFixRotation * transLeftLarm.rotation;
                }
            }
        }

        Transform transRightUarm = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.ShoulderRight, false)); // animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform transRightLarm = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.ElbowRight, false)); // animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Transform transRightHand = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.WristRight, false)); // animator.GetBoneTransform(HumanBodyBones.RightHand);

        if (transRightUarm != null && transRightLarm != null)
        {
            Vector3 vUarmRightDir = transRightLarm.position - transRightUarm.position;
            float fUarmRightAngle = Vector3.Angle(vUarmRightDir, vTposeRightDir);

            if (Mathf.Abs(fUarmRightAngle) >= 5f)
            {
                Quaternion vFixRotation = Quaternion.FromToRotation(vUarmRightDir, vTposeRightDir);
                transRightUarm.rotation = vFixRotation * transRightUarm.rotation;
            }

            if (transRightHand != null)
            {
                Vector3 vLarmRightDir = transRightHand.position - transRightLarm.position;
                float fLarmRightAngle = Vector3.Angle(vLarmRightDir, vTposeRightDir);

                if (Mathf.Abs(fLarmRightAngle) >= 5f)
                {
                    Quaternion vFixRotation = Quaternion.FromToRotation(vLarmRightDir, vTposeRightDir);
                    transRightLarm.rotation = vFixRotation * transRightLarm.rotation;
                }
            }
        }

    }

    // If the bones to be mapped have been declared, map that bone to the model.
    protected virtual void MapBones()
    {
        //		// make OffsetNode as a parent of model transform.
        //		offsetNode = new GameObject(name + "Ctrl") { layer = transform.gameObject.layer, tag = transform.gameObject.tag };
        //		offsetNode.transform.position = transform.position;
        //		offsetNode.transform.rotation = transform.rotation;
        //		offsetNode.transform.parent = transform.parent;

        //		// take model transform as body root
        //		transform.parent = offsetNode.transform;
        //		transform.localPosition = Vector3.zero;
        //		transform.localRotation = Quaternion.identity;

        //bodyRoot = transform;

        // get bone transforms from the animator component
        Animator animatorComponent = GetComponent<Animator>();

        for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
        {
            if (!boneIndex2MecanimMap.ContainsKey(boneIndex))
                continue;

            bones[boneIndex] = animatorComponent ? animatorComponent.GetBoneTransform(boneIndex2MecanimMap[boneIndex]) : null;
        }
    }

    // Capture the initial rotations of the bones
    protected void GetInitialRotations()
    {
        // save the initial rotation
        if (offsetNode != null)
        {
            offsetNodePos = offsetNode.transform.position;
            offsetNodeRot = offsetNode.transform.rotation;
        }

        initialPosition = transform.position;
        initialRotation = transform.rotation;

        //		if(offsetNode != null)
        //		{
        //			initialRotation = Quaternion.Inverse(offsetNodeRot) * initialRotation;
        //		}

        transform.rotation = Quaternion.identity;

        // save the body root initial position
        if (bodyRoot != null)
        {
            bodyRootPosition = bodyRoot.position;
        }
        else
        {
            bodyRootPosition = transform.position;
        }

        if (offsetNode != null)
        {
            bodyRootPosition = bodyRootPosition - offsetNodePos;
        }

        // save the initial bone rotations
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] != null)
            {
                initialRotations[i] = bones[i].rotation;
                localRotations[i] = bones[i].localRotation;
            }
        }

        // get finger bones' local rotations
        Animator animatorComponent = GetComponent<Animator>();
        foreach (int boneIndex in specialIndex2MultiBoneMap.Keys)
        {
            List<HumanBodyBones> alBones = specialIndex2MultiBoneMap[boneIndex];
            //Transform handTransform = animatorComponent.GetBoneTransform((boneIndex == 27 || boneIndex == 29) ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);

            for (int b = 0; b < alBones.Count; b++)
            {
                HumanBodyBones bone = alBones[b];
                Transform boneTransform = animatorComponent ? animatorComponent.GetBoneTransform(bone) : null;

                // get the finger's 1st transform
                Transform fingerBaseTransform = animatorComponent.GetBoneTransform(alBones[b - (b % 3)]);
                //Vector3 vBoneDirParent = handTransform && fingerBaseTransform ? (handTransform.position - fingerBaseTransform.position).normalized : Vector3.zero;

                // get the finger's 2nd transform
                Transform baseChildTransform = fingerBaseTransform && fingerBaseTransform.childCount > 0 ? fingerBaseTransform.GetChild(0) : null;
                Vector3 vBoneDirChild = baseChildTransform && fingerBaseTransform ? (baseChildTransform.position - fingerBaseTransform.position).normalized : Vector3.zero;
                Vector3 vOrthoDirChild = Vector3.Cross(vBoneDirChild, Vector3.up).normalized;

                if (boneTransform)
                {
                    fingerBoneLocalRotations[bone] = boneTransform.localRotation;

                    if (vBoneDirChild != Vector3.zero)
                    {
                        fingerBoneLocalAxes[bone] = boneTransform.InverseTransformDirection(vOrthoDirChild).normalized;
                    }
                    else
                    {
                        fingerBoneLocalAxes[bone] = Vector3.zero;
                    }

                    //					Transform bparTransform = boneTransform ? boneTransform.parent : null;
                    //					Transform bchildTransform = boneTransform && boneTransform.childCount > 0 ? boneTransform.GetChild(0) : null;
                    //
                    //					// get the finger base transform (1st joint)
                    //					Transform fingerBaseTransform = animatorComponent.GetBoneTransform(alBones[b - (b % 3)]);
                    //					Vector3 vBoneDir2 = (handTransform.position - fingerBaseTransform.position).normalized;
                    //
                    //					// set the fist rotation
                    //					if(boneTransform && fingerBaseTransform && handTransform)
                    //					{
                    //						Vector3 vBoneDir = bchildTransform ? (bchildTransform.position - boneTransform.position).normalized :
                    //							(bparTransform ? (boneTransform.position - bparTransform.position).normalized : Vector3.zero);
                    //
                    //						Vector3 vOrthoDir = Vector3.Cross(vBoneDir2, vBoneDir).normalized;
                    //						fingerBoneLocalAxes[bone] = boneTransform.InverseTransformDirection(vOrthoDir);
                    //					}
                }
            }
        }

        // Restore the initial rotation
        transform.rotation = initialRotation;
    }

    // Converts kinect joint rotation to avatar joint rotation, depending on joint initial rotation and offset rotation
    protected Quaternion Kinect2AvatarRot(Quaternion jointRotation, int boneIndex)
    {
        Quaternion newRotation = jointRotation * initialRotations[boneIndex];
        //newRotation = initialRotation * newRotation;

        //		if(offsetNode != null)
        //		{
        //			newRotation = offsetNode.transform.rotation * newRotation;
        //		}
        //		else
        {
            newRotation = initialRotation * newRotation;
        }

        return newRotation;
    }

    // Converts Kinect position to avatar skeleton position, depending on initial position, mirroring and move rate
    protected Vector3 Kinect2AvatarPos(Vector3 jointPosition, bool bMoveVertically)
    {
        float xPos = (jointPosition.x - offsetPos.x) * moveRate;
        float yPos = (jointPosition.y - offsetPos.y) * moveRate;
        float zPos = !mirroredMovement && !posRelativeToCamera ? (-jointPosition.z - offsetPos.z) * moveRate : (jointPosition.z - offsetPos.z) * moveRate;

        Vector3 newPosition = new Vector3(xPos, bMoveVertically ? yPos : 0f, zPos);

        Quaternion posRotation = mirroredMovement ? Quaternion.Euler(0f, 180f, 0f) * initialRotation : initialRotation;
        newPosition = posRotation * newPosition;

        if (offsetNode != null)
        {
            //newPosition += offsetNode.transform.position;
            newPosition = offsetNode.transform.position;
        }

        return newPosition;
    }

    // returns distance from the given transform to the underlying object. The player must be in IgnoreRaycast layer.
    protected virtual float GetTransformDistanceToGround(Transform trans)
    {
        if (!trans)
            return 0f;

        //		RaycastHit hit;
        //		if(Physics.Raycast(trans.position, Vector3.down, out hit, 2f, raycastLayers))
        //		{
        //			return -hit.distance;
        //		}
        //		else if(Physics.Raycast(trans.position, Vector3.up, out hit, 2f, raycastLayers))
        //		{
        //			return hit.distance;
        //		}
        //		else
        //		{
        //			if (trans.position.y < 0)
        //				return -trans.position.y;
        //			else
        //				return 1000f;
        //		}

        return -trans.position.y;
    }

    // returns the lower distance distance from left or right foot to the ground, or 1000f if no LF/RF transforms are found
    protected virtual float GetDistanceToGround()
    {
        if (leftFoot == null && rightFoot == null)
        {
            //			Animator animatorComponent = GetComponent<Animator>();
            //
            //			if(animatorComponent)
            //			{
            //				leftFoot = animatorComponent.GetBoneTransform(HumanBodyBones.LeftFoot);
            //				rightFoot = animatorComponent.GetBoneTransform(HumanBodyBones.RightFoot);
            //			}

            leftFoot = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.FootLeft, false));
            rightFoot = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.FootRight, false));

            if (leftFoot == null || rightFoot == null)
            {
                leftFoot = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.AnkleLeft, false));
                rightFoot = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.AnkleRight, false));
            }
        }

        float fDistMin = 1000f;
        float fDistLeft = leftFoot ? GetTransformDistanceToGround(leftFoot) : fDistMin;
        float fDistRight = rightFoot ? GetTransformDistanceToGround(rightFoot) : fDistMin;
        fDistMin = Mathf.Abs(fDistLeft) < Mathf.Abs(fDistRight) ? fDistLeft : fDistRight;

        if (fDistMin == 1000f)
        {
            fDistMin = 0f; // fFootDistanceInitial;
        }

        //		Debug.Log (string.Format ("LFootY: {0:F2}, Dist: {1:F2}, RFootY: {2:F2}, Dist: {3:F2}, Min: {4:F2}", leftFoot ? leftFoot.position.y : 0f, fDistLeft,
        //						rightFoot ? rightFoot.position.y : 0f, fDistRight, fDistMin));

        return fDistMin;
    }

    //	protected void OnCollisionEnter(Collision col)
    //	{
    //		Debug.Log("Collision entered");
    //	}
    //
    //	protected void OnCollisionExit(Collision col)
    //	{
    //		Debug.Log("Collision exited");
    //	}

    // dictionaries to speed up bones' processing
    // the author of the terrific idea for kinect-joints to mecanim-bones mapping
    // along with its initial implementation, including following dictionary is
    // Mikhail Korchun (korchoon@gmail.com). Big thanks to this guy!
    protected static readonly Dictionary<int, HumanBodyBones> boneIndex2MecanimMap = new Dictionary<int, HumanBodyBones>
    {
        {0, HumanBodyBones.Hips},
        {1, HumanBodyBones.Spine},
//        {2, HumanBodyBones.Chest},
		{3, HumanBodyBones.Neck},
//		{4, HumanBodyBones.Head},
		
		{5, HumanBodyBones.LeftUpperArm},
        {6, HumanBodyBones.LeftLowerArm},
        {7, HumanBodyBones.LeftHand},
//		{8, HumanBodyBones.LeftIndexProximal},
//		{9, HumanBodyBones.LeftIndexIntermediate},
//		{10, HumanBodyBones.LeftThumbProximal},
		
		{11, HumanBodyBones.RightUpperArm},
        {12, HumanBodyBones.RightLowerArm},
        {13, HumanBodyBones.RightHand},
//		{14, HumanBodyBones.RightIndexProximal},
//		{15, HumanBodyBones.RightIndexIntermediate},
//		{16, HumanBodyBones.RightThumbProximal},
		
		{17, HumanBodyBones.LeftUpperLeg},
        {18, HumanBodyBones.LeftLowerLeg},
        {19, HumanBodyBones.LeftFoot},
//		{20, HumanBodyBones.LeftToes},
		
		{21, HumanBodyBones.RightUpperLeg},
        {22, HumanBodyBones.RightLowerLeg},
        {23, HumanBodyBones.RightFoot},
//		{24, HumanBodyBones.RightToes},
		
		{25, HumanBodyBones.LeftShoulder},
        {26, HumanBodyBones.RightShoulder},
        {27, HumanBodyBones.LeftIndexProximal},
        {28, HumanBodyBones.RightIndexProximal},
        {29, HumanBodyBones.LeftThumbProximal},
        {30, HumanBodyBones.RightThumbProximal},
    };

    protected static readonly Dictionary<int, KinectInterop.JointType> boneIndex2JointMap = new Dictionary<int, KinectInterop.JointType>
    {
        {0, KinectInterop.JointType.SpineBase},
        {1, KinectInterop.JointType.SpineMid},
        {2, KinectInterop.JointType.SpineShoulder},
        {3, KinectInterop.JointType.Neck},
        {4, KinectInterop.JointType.Head},

        {5, KinectInterop.JointType.ShoulderLeft},
        {6, KinectInterop.JointType.ElbowLeft},
        {7, KinectInterop.JointType.WristLeft},
        {8, KinectInterop.JointType.HandLeft},

        {9, KinectInterop.JointType.HandTipLeft},
        {10, KinectInterop.JointType.ThumbLeft},

        {11, KinectInterop.JointType.ShoulderRight},
        {12, KinectInterop.JointType.ElbowRight},
        {13, KinectInterop.JointType.WristRight},
        {14, KinectInterop.JointType.HandRight},

        {15, KinectInterop.JointType.HandTipRight},
        {16, KinectInterop.JointType.ThumbRight},

        {17, KinectInterop.JointType.HipLeft},
        {18, KinectInterop.JointType.KneeLeft},
        {19, KinectInterop.JointType.AnkleLeft},
        {20, KinectInterop.JointType.FootLeft},

        {21, KinectInterop.JointType.HipRight},
        {22, KinectInterop.JointType.KneeRight},
        {23, KinectInterop.JointType.AnkleRight},
        {24, KinectInterop.JointType.FootRight},
    };

    protected static readonly Dictionary<int, List<KinectInterop.JointType>> specIndex2JointMap = new Dictionary<int, List<KinectInterop.JointType>>
    {
        {25, new List<KinectInterop.JointType> {KinectInterop.JointType.ShoulderLeft, KinectInterop.JointType.SpineShoulder} },
        {26, new List<KinectInterop.JointType> {KinectInterop.JointType.ShoulderRight, KinectInterop.JointType.SpineShoulder} },
        {27, new List<KinectInterop.JointType> {KinectInterop.JointType.HandTipLeft, KinectInterop.JointType.HandLeft} },
        {28, new List<KinectInterop.JointType> {KinectInterop.JointType.HandTipRight, KinectInterop.JointType.HandRight} },
        {29, new List<KinectInterop.JointType> {KinectInterop.JointType.ThumbLeft, KinectInterop.JointType.HandLeft} },
        {30, new List<KinectInterop.JointType> {KinectInterop.JointType.ThumbRight, KinectInterop.JointType.HandRight} },
    };

    protected static readonly Dictionary<int, KinectInterop.JointType> boneIndex2MirrorJointMap = new Dictionary<int, KinectInterop.JointType>
    {
        {0, KinectInterop.JointType.SpineBase},
        {1, KinectInterop.JointType.SpineMid},
        {2, KinectInterop.JointType.SpineShoulder},
        {3, KinectInterop.JointType.Neck},
        {4, KinectInterop.JointType.Head},

        {5, KinectInterop.JointType.ShoulderRight},
        {6, KinectInterop.JointType.ElbowRight},
        {7, KinectInterop.JointType.WristRight},
        {8, KinectInterop.JointType.HandRight},

        {9, KinectInterop.JointType.HandTipRight},
        {10, KinectInterop.JointType.ThumbRight},

        {11, KinectInterop.JointType.ShoulderLeft},
        {12, KinectInterop.JointType.ElbowLeft},
        {13, KinectInterop.JointType.WristLeft},
        {14, KinectInterop.JointType.HandLeft},

        {15, KinectInterop.JointType.HandTipLeft},
        {16, KinectInterop.JointType.ThumbLeft},

        {17, KinectInterop.JointType.HipRight},
        {18, KinectInterop.JointType.KneeRight},
        {19, KinectInterop.JointType.AnkleRight},
        {20, KinectInterop.JointType.FootRight},

        {21, KinectInterop.JointType.HipLeft},
        {22, KinectInterop.JointType.KneeLeft},
        {23, KinectInterop.JointType.AnkleLeft},
        {24, KinectInterop.JointType.FootLeft},
    };

    protected static readonly Dictionary<int, List<KinectInterop.JointType>> specIndex2MirrorMap = new Dictionary<int, List<KinectInterop.JointType>>
    {
        {25, new List<KinectInterop.JointType> {KinectInterop.JointType.ShoulderRight, KinectInterop.JointType.SpineShoulder} },
        {26, new List<KinectInterop.JointType> {KinectInterop.JointType.ShoulderLeft, KinectInterop.JointType.SpineShoulder} },
        {27, new List<KinectInterop.JointType> {KinectInterop.JointType.HandTipRight, KinectInterop.JointType.HandRight} },
        {28, new List<KinectInterop.JointType> {KinectInterop.JointType.HandTipLeft, KinectInterop.JointType.HandLeft} },
        {29, new List<KinectInterop.JointType> {KinectInterop.JointType.ThumbRight, KinectInterop.JointType.HandRight} },
        {30, new List<KinectInterop.JointType> {KinectInterop.JointType.ThumbLeft, KinectInterop.JointType.HandLeft} },
    };

    protected static readonly Dictionary<KinectInterop.JointType, int> jointMap2boneIndex = new Dictionary<KinectInterop.JointType, int>
    {
        {KinectInterop.JointType.SpineBase, 0},
        {KinectInterop.JointType.SpineMid, 1},
        {KinectInterop.JointType.SpineShoulder, 2},
        {KinectInterop.JointType.Neck, 3},
        {KinectInterop.JointType.Head, 4},

        {KinectInterop.JointType.ShoulderLeft, 5},
        {KinectInterop.JointType.ElbowLeft, 6},
        {KinectInterop.JointType.WristLeft, 7},
        {KinectInterop.JointType.HandLeft, 8},

        {KinectInterop.JointType.HandTipLeft, 9},
        {KinectInterop.JointType.ThumbLeft, 10},

        {KinectInterop.JointType.ShoulderRight, 11},
        {KinectInterop.JointType.ElbowRight, 12},
        {KinectInterop.JointType.WristRight, 13},
        {KinectInterop.JointType.HandRight, 14},

        {KinectInterop.JointType.HandTipRight, 15},
        {KinectInterop.JointType.ThumbRight, 16},

        {KinectInterop.JointType.HipLeft, 17},
        {KinectInterop.JointType.KneeLeft, 18},
        {KinectInterop.JointType.AnkleLeft, 19},
        {KinectInterop.JointType.FootLeft, 20},

        {KinectInterop.JointType.HipRight, 21},
        {KinectInterop.JointType.KneeRight, 22},
        {KinectInterop.JointType.AnkleRight, 23},
        {KinectInterop.JointType.FootRight, 24},
    };

    protected static readonly Dictionary<KinectInterop.JointType, int> mirrorJointMap2boneIndex = new Dictionary<KinectInterop.JointType, int>
    {
        {KinectInterop.JointType.SpineBase, 0},
        {KinectInterop.JointType.SpineMid, 1},
        {KinectInterop.JointType.SpineShoulder, 2},
        {KinectInterop.JointType.Neck, 3},
        {KinectInterop.JointType.Head, 4},

        {KinectInterop.JointType.ShoulderRight, 5},
        {KinectInterop.JointType.ElbowRight, 6},
        {KinectInterop.JointType.WristRight, 7},
        {KinectInterop.JointType.HandRight, 8},

        {KinectInterop.JointType.HandTipRight, 9},
        {KinectInterop.JointType.ThumbRight, 10},

        {KinectInterop.JointType.ShoulderLeft, 11},
        {KinectInterop.JointType.ElbowLeft, 12},
        {KinectInterop.JointType.WristLeft, 13},
        {KinectInterop.JointType.HandLeft, 14},

        {KinectInterop.JointType.HandTipLeft, 15},
        {KinectInterop.JointType.ThumbLeft, 16},

        {KinectInterop.JointType.HipRight, 17},
        {KinectInterop.JointType.KneeRight, 18},
        {KinectInterop.JointType.AnkleRight, 19},
        {KinectInterop.JointType.FootRight, 20},

        {KinectInterop.JointType.HipLeft, 21},
        {KinectInterop.JointType.KneeLeft, 22},
        {KinectInterop.JointType.AnkleLeft, 23},
        {KinectInterop.JointType.FootLeft, 24},
    };


    protected static readonly Dictionary<int, List<HumanBodyBones>> specialIndex2MultiBoneMap = new Dictionary<int, List<HumanBodyBones>>
    {
        {27, new List<HumanBodyBones> {  // left fingers
				HumanBodyBones.LeftIndexProximal,
                HumanBodyBones.LeftIndexIntermediate,
                HumanBodyBones.LeftIndexDistal,
                HumanBodyBones.LeftMiddleProximal,
                HumanBodyBones.LeftMiddleIntermediate,
                HumanBodyBones.LeftMiddleDistal,
                HumanBodyBones.LeftRingProximal,
                HumanBodyBones.LeftRingIntermediate,
                HumanBodyBones.LeftRingDistal,
                HumanBodyBones.LeftLittleProximal,
                HumanBodyBones.LeftLittleIntermediate,
                HumanBodyBones.LeftLittleDistal,
            }},
        {28, new List<HumanBodyBones> {  // right fingers
				HumanBodyBones.RightIndexProximal,
                HumanBodyBones.RightIndexIntermediate,
                HumanBodyBones.RightIndexDistal,
                HumanBodyBones.RightMiddleProximal,
                HumanBodyBones.RightMiddleIntermediate,
                HumanBodyBones.RightMiddleDistal,
                HumanBodyBones.RightRingProximal,
                HumanBodyBones.RightRingIntermediate,
                HumanBodyBones.RightRingDistal,
                HumanBodyBones.RightLittleProximal,
                HumanBodyBones.RightLittleIntermediate,
                HumanBodyBones.RightLittleDistal,
            }},
        {29, new List<HumanBodyBones> {  // left thumb
				HumanBodyBones.LeftThumbProximal,
                HumanBodyBones.LeftThumbIntermediate,
                HumanBodyBones.LeftThumbDistal,
            }},
        {30, new List<HumanBodyBones> {  // right thumb
				HumanBodyBones.RightThumbProximal,
                HumanBodyBones.RightThumbIntermediate,
                HumanBodyBones.RightThumbDistal,
            }},
    };

    public Dictionary<int, KinectInterop.JointType> BoneIndex2MirrorJointMap
    {
        get
        {
            return boneIndex2MirrorJointMap;
        }
    }

    public Dictionary<int, KinectInterop.JointType> BoneIndex2JointMap
    {
        get
        {
            return boneIndex2JointMap;
        }
    }
}
