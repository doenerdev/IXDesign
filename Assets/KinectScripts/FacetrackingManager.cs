using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
//using System.Runtime.InteropServices;
using System.Text;


/// <summary>
/// Facetracking manager is the component that deals with head and face tracking.
/// </summary>
public class FacetrackingManager : MonoBehaviour
{

    [Tooltip("Index of the player, tracked by this component. 0 means the 1st player, 1 - the 2nd one, 2 - the 3rd one, etc.")]
    public int playerIndex = 0;

    [Tooltip("Whether to utilize the HD-face model functionality or not.")]
    public bool getFaceModelData = false;

    [Tooltip("Whether to display the face rectangle over the color camera feed.")]
    public bool displayFaceRect = false;

    [Tooltip("Time tolerance (in seconds), when the face is allowed not to be tracked without losing it.")]
    public float faceTrackingTolerance = 0.25f;

    [Tooltip("Game object that will be used to display the HD-face model mesh in the scene.")]
    public GameObject faceModelMesh = null;

    [Tooltip("Whether the HD-face model mesh should be mirrored or not.")]
    public bool mirroredModelMesh = true;

    public enum TextureType : int { None, ColorMap, FaceRectangle }
    [Tooltip("Whether the HD-face model mesh should be textured or not.")]
    public TextureType texturedModelMesh = TextureType.ColorMap;

    [Tooltip("Camera that will be used to overlay face mesh over the background.")]
    public Camera foregroundCamera;

    [Tooltip("Scale factor for the face mesh.")]
    [Range(0.1f, 2.0f)]
    public float modelMeshScale = 1f;

    [Tooltip("Vertical offset of the mesh above the head (in meters).")]
    [Range(-0.5f, 0.5f)]
    public float verticalMeshOffset = 0f;

    [Tooltip("GUI-Text to display the FT-manager debug messages.")]
    public GUIText debugText;

    // Is currently tracking user's face
    private bool isTrackingFace = false;
    private float lastFaceTrackedTime = 0f;

    // Skeleton ID of the tracked face
    //private long faceTrackingID = 0;

    // Animation units
    private Dictionary<KinectInterop.FaceShapeAnimations, float> dictAU = new Dictionary<KinectInterop.FaceShapeAnimations, float>();
    private bool bGotAU = false;

    // Shape units
    private Dictionary<KinectInterop.FaceShapeDeformations, float> dictSU = new Dictionary<KinectInterop.FaceShapeDeformations, float>();
    private bool bGotSU = false;

    // whether the face model mesh was initialized
    private bool bFaceModelMeshInited = false;

    // Vertices, UV and triangles of the face model
    private Vector3[] avModelVertices = null;
    private Vector2[] avModelUV = null;
    private bool bGotModelVertices = false;
    private bool bGotModelVerticesFromDC = false;

    private int[] avModelTriangles = null;
    private bool bGotModelTriangles = false;
    private bool bGotModelTrianglesFromDC = false;

    // Head position and rotation
    private Vector3 headPos = Vector3.zero;
    private bool bGotHeadPos = false;

    private Quaternion headRot = Quaternion.identity;
    private bool bGotHeadRot = false;

    // Tracked face rectangle
    private Rect faceRect = new Rect();
    //private bool bGotFaceRect;

    // primary user ID, as reported by KinectManager
    private long primaryUserID = 0;

    // primary sensor data structure
    private KinectInterop.SensorData sensorData = null;

    // Bool to keep track of whether face-tracking system has been initialized
    private bool isFacetrackingInitialized = false;

    // The single instance of FacetrackingManager
    private static FacetrackingManager instance;


    /// <summary>
    /// Gets the single FacetrackingManager instance.
    /// </summary>
    /// <value>The FacetrackingManager instance.</value>
    public static FacetrackingManager Instance
    {
        get
        {
            return instance;
        }
    }

    /// <summary>
    /// Determines the facetracking system was successfully initialized, false otherwise.
    /// </summary>
    /// <returns><c>true</c> if the facetracking system was successfully initialized; otherwise, <c>false</c>.</returns>
    public bool IsFaceTrackingInitialized()
    {
        return isFacetrackingInitialized;
    }

    /// <summary>
    /// Determines whether this the sensor is currently tracking a face.
    /// </summary>
    /// <returns><c>true</c> if the sensor is tracking a face; otherwise, <c>false</c>.</returns>
    public bool IsTrackingFace()
    {
        return isTrackingFace;
    }

    /// <summary>
    /// Gets the current user ID, or 0 if no user is currently tracked.
    /// </summary>
    /// <returns>The face tracking I.</returns>
    public long GetFaceTrackingID()
    {
        return isTrackingFace ? primaryUserID : 0;
    }

    /// <summary>
    /// Determines whether the sensor is currently tracking the face of the specified user.
    /// </summary>
    /// <returns><c>true</c> if the sensor is currently tracking the face of the specified user; otherwise, <c>false</c>.</returns>
    /// <param name="userId">User ID</param>
    public bool IsTrackingFace(long userId)
    {
        if (sensorData != null && sensorData.sensorInterface != null)
        {
            return sensorData.sensorInterface.IsFaceTracked(userId);
        }

        return false;
    }

    /// <summary>
    /// Gets the head position of the currently tracked user.
    /// </summary>
    /// <returns>The head position.</returns>
    /// <param name="bMirroredMovement">If set to <c>true</c> returns mirorred head position.</param>
    public Vector3 GetHeadPosition(bool bMirroredMovement)
    {
        Vector3 vHeadPos = headPos; // bGotHeadPos ? headPos : Vector3.zero;

        if (!bMirroredMovement)
        {
            vHeadPos.z = -vHeadPos.z;
        }

        return vHeadPos;
    }

    /// <summary>
    /// Gets the head position of the specified user.
    /// </summary>
    /// <returns>The head position.</returns>
    /// <param name="userId">User ID</param>
    /// <param name="bMirroredMovement">If set to <c>true</c> returns mirorred head position.</param>
    public Vector3 GetHeadPosition(long userId, bool bMirroredMovement)
    {
        Vector3 vHeadPos = Vector3.zero;
        bool bGotPosition = sensorData.sensorInterface.GetHeadPosition(userId, ref vHeadPos);

        if (bGotPosition)
        {
            if (!bMirroredMovement)
            {
                vHeadPos.z = -vHeadPos.z;
            }

            return vHeadPos;
        }

        return Vector3.zero;
    }

    /// <summary>
    /// Gets the head rotation of the currently tracked user.
    /// </summary>
    /// <returns>The head rotation.</returns>
    /// <param name="bMirroredMovement">If set to <c>true</c> returns mirorred head rotation.</param>
    public Quaternion GetHeadRotation(bool bMirroredMovement)
    {
        Vector3 rotAngles = headRot.eulerAngles; // bGotHeadRot ? headRot.eulerAngles : Vector3.zero;

        if (bMirroredMovement)
        {
            rotAngles.x = -rotAngles.x;
            rotAngles.z = -rotAngles.z;
        }
        else
        {
            rotAngles.x = -rotAngles.x;
            rotAngles.y = -rotAngles.y;
        }

        return Quaternion.Euler(rotAngles);
    }

    /// <summary>
    /// Gets the head rotation of the specified user.
    /// </summary>
    /// <returns>The head rotation.</returns>
    /// <param name="userId">User ID</param>
    /// <param name="bMirroredMovement">If set to <c>true</c> returns mirorred head rotation.</param>
    public Quaternion GetHeadRotation(long userId, bool bMirroredMovement)
    {
        Quaternion vHeadRot = Quaternion.identity;
        bool bGotRotation = sensorData.sensorInterface.GetHeadRotation(userId, ref vHeadRot);

        if (bGotRotation)
        {
            Vector3 rotAngles = vHeadRot.eulerAngles;

            if (bMirroredMovement)
            {
                rotAngles.x = -rotAngles.x;
                rotAngles.z = -rotAngles.z;
            }
            else
            {
                rotAngles.x = -rotAngles.x;
                rotAngles.y = -rotAngles.y;
            }

            return Quaternion.Euler(rotAngles);
        }

        return Quaternion.identity;
    }

    /// <summary>
    /// Gets the tracked face rectangle of the specified user in color image coordinates, or zero-rect if the user's face is not tracked.
    /// </summary>
    /// <returns>The face rectangle, in color image coordinates.</returns>
    /// <param name="userId">User ID</param>
    public Rect GetFaceColorRect(long userId)
    {
        Rect faceColorRect = new Rect();
        sensorData.sensorInterface.GetFaceRect(userId, ref faceColorRect);

        return faceColorRect;
    }

    /// <summary>
    /// Determines whether there are valid anim units.
    /// </summary>
    /// <returns><c>true</c> if there are valid anim units; otherwise, <c>false</c>.</returns>
    public bool IsGotAU()
    {
        return bGotAU;
    }

    /// <summary>
    /// Gets the animation unit value at given index, or 0 if the index is invalid.
    /// </summary>
    /// <returns>The animation unit value.</returns>
    /// <param name="faceAnimKey">Face animation unit.</param>
    public float GetAnimUnit(KinectInterop.FaceShapeAnimations faceAnimKey)
    {
        if (dictAU.ContainsKey(faceAnimKey))
        {
            return dictAU[faceAnimKey];
        }

        return 0.0f;
    }

    /// <summary>
    /// Gets all animation units for the specified user.
    /// </summary>
    /// <returns><c>true</c>, if the user's face is tracked, <c>false</c> otherwise.</returns>
    /// <param name="userId">User ID</param>
    /// <param name="dictAnimUnits">Animation units dictionary, to get the results.</param>
    public bool GetUserAnimUnits(long userId, ref Dictionary<KinectInterop.FaceShapeAnimations, float> dictAnimUnits)
    {
        if (sensorData != null && sensorData.sensorInterface != null)
        {
            bool bGotIt = sensorData.sensorInterface.GetAnimUnits(userId, ref dictAnimUnits);
            return bGotIt;
        }

        return false;
    }

    /// <summary>
    /// Determines whether there are valid shape units.
    /// </summary>
    /// <returns><c>true</c> if there are valid shape units; otherwise, <c>false</c>.</returns>
    public bool IsGotSU()
    {
        return bGotSU;
    }

    /// <summary>
    /// Gets the shape unit value at given index, or 0 if the index is invalid.
    /// </summary>
    /// <returns>The shape unit value.</returns>
    /// <param name="faceShapeKey">Face shape unit.</param>
    public float GetShapeUnit(KinectInterop.FaceShapeDeformations faceShapeKey)
    {
        if (dictSU.ContainsKey(faceShapeKey))
        {
            return dictSU[faceShapeKey];
        }

        return 0.0f;
    }

    /// <summary>
    /// Gets all animation units for the specified user.
    /// </summary>
    /// <returns><c>true</c>, if the user's face is tracked, <c>false</c> otherwise.</returns>
    /// <param name="userId">User ID</param>
    /// <param name="dictShapeUnits">Shape units dictionary, to get the results.</param>
    public bool GetUserShapeUnits(long userId, ref Dictionary<KinectInterop.FaceShapeDeformations, float> dictShapeUnits)
    {
        if (sensorData != null && sensorData.sensorInterface != null)
        {
            bool bGotIt = sensorData.sensorInterface.GetShapeUnits(userId, ref dictShapeUnits);
            return bGotIt;
        }

        return false;
    }

    /// <summary>
    /// Gets the count of face model vertices.
    /// </summary>
    /// <returns>The count of face model vertices.</returns>
    public int GetFaceModelVertexCount()
    {
        if (avModelVertices != null)
        {
            return avModelVertices.Length;
        }

        return 0;
    }

    /// <summary>
    /// Gets the face model vertex, if a face model is available and the index is in range; Vector3.zero otherwise.
    /// </summary>
    /// <returns>The face model vertex.</returns>
    /// <param name="index">Vertex index, or Vector3.zero</param>
    public Vector3 GetFaceModelVertex(int index)
    {
        if (avModelVertices != null)
        {
            if (index >= 0 && index < avModelVertices.Length)
            {
                return avModelVertices[index];
            }
        }

        return Vector3.zero;
    }

    /// <summary>
    /// Gets all face model vertices, if a face model is available; null otherwise.
    /// </summary>
    /// <returns>The face model vertices, or null.</returns>
    public Vector3[] GetFaceModelVertices()
    {
        return avModelVertices;
    }

    /// <summary>
    /// Gets the count of face model vertices for the specified user
    /// </summary>
    /// <returns>The count of face model vertices.</returns>
    /// <param name="userId">User ID</param>
    public int GetUserFaceVertexCount(long userId)
    {
        if (sensorData != null && sensorData.sensorInterface != null)
        {
            int iVertCount = sensorData.sensorInterface.GetFaceModelVerticesCount(userId);
            return iVertCount;
        }

        return 0;
    }

    /// <summary>
    /// Gets all face model vertices for the specified user.
    /// </summary>
    /// <returns><c>true</c>, if the user's face is tracked, <c>false</c> otherwise.</returns>
    /// <param name="userId">User ID</param>
    /// <param name="avVertices">Reference to array of vertices, to get the result.</param>
    public bool GetUserFaceVertices(long userId, ref Vector3[] avVertices)
    {
        if (sensorData != null && sensorData.sensorInterface != null)
        {
            bool bGotIt = sensorData.sensorInterface.GetFaceModelVertices(userId, ref avVertices);
            return bGotIt;
        }

        return false;
    }

    /// <summary>
    /// Gets the count of face model triangles.
    /// </summary>
    /// <returns>The count of face model triangles.</returns>
    public int GetFaceModelTriangleCount()
    {
        if (avModelTriangles != null)
        {
            return avModelTriangles.Length;
        }

        return 0;
    }

    /// <summary>
    /// Gets the face model triangle indices, if a face model is available; null otherwise.
    /// </summary>
    /// <returns>The face model triangle indices, or null.</returns>
    /// <param name="bMirroredModel">If set to <c>true</c> gets mirorred model indices.</param>
    public int[] GetFaceModelTriangleIndices(bool bMirroredModel)
    {
        if (avModelTriangles != null)
        {
            return avModelTriangles;
        }

        return null;
    }


    //----------------------------------- end of public functions --------------------------------------//

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        try
        {
            // get sensor data
            KinectManager kinectManager = KinectManager.Instance;
            if (kinectManager && kinectManager.IsInitialized())
            {
                sensorData = kinectManager.GetSensorData();
            }

            if (sensorData == null || sensorData.sensorInterface == null)
            {
                throw new Exception("Face tracking cannot be started, because KinectManager is missing or not initialized.");
            }

            if (debugText != null)
            {
                debugText.text = "Please, wait...";
            }

            // ensure the needed dlls are in place and face tracking is available for this interface
            bool bNeedRestart = false;
            if (sensorData.sensorInterface.IsFaceTrackingAvailable(ref bNeedRestart))
            {
                if (bNeedRestart)
                {
                    KinectInterop.RestartLevel(gameObject, "FM");
                    return;
                }
            }
            else
            {
                string sInterfaceName = sensorData.sensorInterface.GetType().Name;
                throw new Exception(sInterfaceName + ": Face tracking is not supported!");
            }

            // Initialize the face tracker
            if (!sensorData.sensorInterface.InitFaceTracking(getFaceModelData, displayFaceRect))
            {
                throw new Exception("Face tracking could not be initialized.");
            }

            isFacetrackingInitialized = true;

            //DontDestroyOnLoad(gameObject);

            if (debugText != null)
            {
                debugText.text = "Ready.";
            }
        }
        catch (DllNotFoundException ex)
        {
            Debug.LogError(ex.ToString());
            if (debugText != null)
                debugText.text = "Please check the Kinect and FT-Library installations.";
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.ToString());
            if (debugText != null)
                debugText.text = ex.Message;
        }

        //AH
        CreateDisplays();
    }

    void OnDestroy()
    {
        if (isFacetrackingInitialized && sensorData != null && sensorData.sensorInterface != null)
        {
            // finish face tracking
            sensorData.sensorInterface.FinishFaceTracking();
        }

        //		// clean up
        //		Resources.UnloadUnusedAssets();
        //		GC.Collect();

        isFacetrackingInitialized = false;
        instance = null;
    }

    void Update()
    {
        if (isFacetrackingInitialized)
        {
            KinectManager kinectManager = KinectManager.Instance;
            if (kinectManager && kinectManager.IsInitialized())
            {
                //AH primaryUserID angepasst
                primaryUserID = kinectManager.GetPrimaryUserID();
            }

            // update the face tracker
            isTrackingFace = false;

            if (sensorData.sensorInterface.UpdateFaceTracking())
            {
                // estimate the tracking state
                isTrackingFace = sensorData.sensorInterface.IsFaceTracked(primaryUserID);

                if (!isTrackingFace && (Time.realtimeSinceStartup - lastFaceTrackedTime) <= faceTrackingTolerance)
                {
                    // allow tolerance in tracking
                    isTrackingFace = true;
                }

                // get the facetracking parameters
                if (isTrackingFace)
                {
                    lastFaceTrackedTime = Time.realtimeSinceStartup;

                    // get face rectangle
                    /**bGotFaceRect =*/
                    sensorData.sensorInterface.GetFaceRect(primaryUserID, ref faceRect);

                    // get head position
                    bGotHeadPos = sensorData.sensorInterface.GetHeadPosition(primaryUserID, ref headPos);

                    // get head rotation
                    bGotHeadRot = sensorData.sensorInterface.GetHeadRotation(primaryUserID, ref headRot);

                    // get the animation units
                    bGotAU = sensorData.sensorInterface.GetAnimUnits(primaryUserID, ref dictAU);

                    // get the shape units
                    bGotSU = sensorData.sensorInterface.GetShapeUnits(primaryUserID, ref dictSU);

                    //if(faceModelMesh != null && faceModelMesh.activeInHierarchy)
                    {
                        // apply model vertices to the mesh
                        if (!bFaceModelMeshInited)
                        {
                            bFaceModelMeshInited = CreateFaceModelMesh(faceModelMesh, headPos, ref avModelVertices, ref avModelUV, ref bGotModelVertices);
                        }
                    }

                    if (getFaceModelData && bFaceModelMeshInited)
                    {
                        UpdateFaceModelMesh(primaryUserID, faceModelMesh, headPos, headRot, faceRect, ref avModelVertices, ref avModelUV, ref bGotModelVertices);
                    }
                }
            }

            if (faceModelMesh != null && bFaceModelMeshInited)
            {
                faceModelMesh.SetActive(isTrackingFace);
            }

        }
    }

    void OnGUI()
    {
        if (isFacetrackingInitialized)
        {
            if (debugText != null)
            {
                if (isTrackingFace)
                {
                    debugText.text = "Tracking - BodyID: " + primaryUserID;
                }
                else
                {
                    debugText.text = "Not tracking...";
                }
            }
        }
    }


    public bool CreateFaceModelMesh(GameObject faceModelMesh, Vector3 headPos, ref Vector3[] avModelVertices, ref Vector2[] avModelUV, ref bool bGotModelVertices)
    {
        //		if(faceModelMesh == null)
        //			return false;

        if (avModelVertices == null && !bGotModelVerticesFromDC)
        {
            int iNumVertices = sensorData.sensorInterface.GetFaceModelVerticesCount(0);
            if (iNumVertices <= 0)
                return false;

            avModelVertices = new Vector3[iNumVertices];
            bGotModelVertices = sensorData.sensorInterface.GetFaceModelVertices(0, ref avModelVertices);

            avModelUV = new Vector2[iNumVertices];

            if (!bGotModelVertices)
                return false;
        }

        // make vertices relative to the head pos
        Matrix4x4 kinectToWorld = KinectManager.Instance ? KinectManager.Instance.GetKinectToWorldMatrix() : Matrix4x4.identity;
        Vector3 headPosWorld = kinectToWorld.MultiplyPoint3x4(headPos);

        if (!bGotModelVerticesFromDC)
        {
            for (int i = 0; i < avModelVertices.Length; i++)
            {
                avModelVertices[i] = kinectToWorld.MultiplyPoint3x4(avModelVertices[i]) - headPosWorld;
            }
        }

        if (avModelTriangles == null && !bGotModelTrianglesFromDC)
        {
            int iNumTriangles = sensorData.sensorInterface.GetFaceModelTrianglesCount();
            if (iNumTriangles <= 0)
                return false;

            avModelTriangles = new int[iNumTriangles];
            bGotModelTriangles = sensorData.sensorInterface.GetFaceModelTriangles(mirroredModelMesh, ref avModelTriangles);

            if (!bGotModelTriangles)
                return false;
        }

        if (faceModelMesh)
        {
            Mesh mesh = new Mesh();
            mesh.name = "FaceMesh";
            faceModelMesh.GetComponent<MeshFilter>().mesh = mesh;

            mesh.vertices = avModelVertices;
            //mesh.uv = avModelUV;

            mesh.triangles = avModelTriangles;
            mesh.RecalculateNormals();

            faceModelMesh.transform.position = headPos;
            //faceModelMesh.transform.rotation = faceModelRot;
        }

        //bFaceModelMeshInited = true;
        return true;
    }


    public void UpdateFaceModelMesh(long userId, GameObject faceModelMesh, Vector3 headPos, Quaternion headRot, Rect faceRect,
                                    ref Vector3[] avModelVertices, ref Vector2[] avModelUV, ref bool bGotModelVertices)
    {
        if (!bGotModelVerticesFromDC)
        {
            // init the vertices array if needed
            if (avModelVertices == null)
            {
                int iNumVertices = sensorData.sensorInterface.GetFaceModelVerticesCount(userId);
                avModelVertices = new Vector3[iNumVertices];
            }

            // get face model vertices
            bGotModelVertices = sensorData.sensorInterface.GetFaceModelVertices(userId, ref avModelVertices);
        }

        if (bGotModelVertices && faceModelMesh != null)
        {
            //Quaternion faceModelRot = faceModelMesh.transform.rotation;
            //faceModelMesh.transform.rotation = Quaternion.identity;

            KinectManager kinectManager = KinectManager.Instance;

            if (!bGotModelVerticesFromDC)
            {
                if (texturedModelMesh != TextureType.None)
                {
                    float colorWidth = (float)kinectManager.GetColorImageWidth();
                    float colorHeight = (float)kinectManager.GetColorImageHeight();

                    //bool bGotFaceRect = sensorData.sensorInterface.GetFaceRect(userId, ref faceRect);
                    bool faceRectValid = /**bGotFaceRect &&*/ faceRect.width > 0 && faceRect.height > 0;

                    if (texturedModelMesh == TextureType.ColorMap &&
                        faceModelMesh.GetComponent<MeshRenderer>().material.mainTexture == null)
                    {
                        faceModelMesh.GetComponent<MeshRenderer>().material.mainTexture = kinectManager.GetUsersClrTex();
                    }

                    for (int i = 0; i < avModelVertices.Length; i++)
                    {
                        Vector2 posDepth = kinectManager.MapSpacePointToDepthCoords(avModelVertices[i]);

                        bool bUvSet = false;
                        if (posDepth != Vector2.zero)
                        {
                            ushort depth = kinectManager.GetDepthForPixel((int)posDepth.x, (int)posDepth.y);
                            Vector2 posColor = kinectManager.MapDepthPointToColorCoords(posDepth, depth);

                            if (posColor != Vector2.zero && !float.IsInfinity(posColor.x) && !float.IsInfinity(posColor.y))
                            {
                                if (texturedModelMesh == TextureType.ColorMap)
                                {
                                    avModelUV[i] = new Vector2(posColor.x / colorWidth, posColor.y / colorHeight);
                                    bUvSet = true;
                                }
                                else if (texturedModelMesh == TextureType.FaceRectangle && faceRectValid)
                                {
                                    avModelUV[i] = new Vector2((posColor.x - faceRect.x) / faceRect.width,
                                        -(posColor.y - faceRect.y) / faceRect.height);
                                    bUvSet = true;
                                }
                            }
                        }

                        if (!bUvSet)
                        {
                            avModelUV[i] = Vector2.zero;
                        }
                    }
                }
                else
                {
                    if (faceModelMesh.GetComponent<MeshRenderer>().material.mainTexture != null)
                    {
                        faceModelMesh.GetComponent<MeshRenderer>().material.mainTexture = null;
                    }
                }
            }

            if (!bGotModelVerticesFromDC)
            {
                // make vertices relative to the head pos
                Matrix4x4 kinectToWorld = kinectManager ? kinectManager.GetKinectToWorldMatrix() : Matrix4x4.identity;
                Vector3 headPosWorld = kinectToWorld.MultiplyPoint3x4(headPos);

                if (verticalMeshOffset != 0f)
                {
                    Vector3 headPosOfs = headRot * new Vector3(0, -verticalMeshOffset, 0);
                    headPosWorld += headPosOfs;
                }

                for (int i = 0; i < avModelVertices.Length; i++)
                {
                    avModelVertices[i] = kinectToWorld.MultiplyPoint3x4(avModelVertices[i]) - headPosWorld;
                }
            }

            Mesh mesh = faceModelMesh.GetComponent<MeshFilter>().mesh;
            mesh.vertices = avModelVertices;

            if (texturedModelMesh != TextureType.None && avModelUV != null)
            {
                mesh.uv = avModelUV;
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // check for head pos overlay
            Vector3 newHeadPos = headPos;

            if (foregroundCamera)
            {
                // get the background rectangle (use the portrait background, if available)
                Rect backgroundRect = foregroundCamera.pixelRect;
                PortraitBackground portraitBack = PortraitBackground.Instance;

                if (portraitBack && portraitBack.enabled)
                {
                    backgroundRect = portraitBack.GetBackgroundRect();
                }

                if (kinectManager)
                {
                    Vector3 posColorOverlay = kinectManager.GetJointPosColorOverlay(primaryUserID, (int)KinectInterop.JointType.Head, foregroundCamera, backgroundRect);

                    if (posColorOverlay != Vector3.zero)
                    {
                        newHeadPos = posColorOverlay;
                    }
                }
            }

            if (!faceModelMesh.activeSelf)
            {
                faceModelMesh.SetActive(true);
            }

            faceModelMesh.transform.position = newHeadPos;
            //faceModelMesh.transform.rotation = faceModelRot;

            // apply scale factor
            if (faceModelMesh.transform.localScale.x != modelMeshScale)
            {
                faceModelMesh.transform.localScale = new Vector3(modelMeshScale, modelMeshScale, modelMeshScale);
            }
        }
        else
        {
            if (faceModelMesh && faceModelMesh.activeSelf)
            {
                faceModelMesh.SetActive(false);
            }
        }
    }

    // gets face basic parameters as csv line
    public string GetFaceParamsAsCsv()
    {
        // create the output string
        StringBuilder sbBuf = new StringBuilder();
        const char delimiter = ',';

        if (bGotHeadPos || bGotHeadRot)
        {
            sbBuf.Append("fp").Append(delimiter);

            // head pos
            sbBuf.Append(bGotHeadPos ? "1" : "0").Append(delimiter);

            if (bGotHeadPos)
            {
                sbBuf.AppendFormat("{0:F3}", headPos.x).Append(delimiter);
                sbBuf.AppendFormat("{0:F3}", headPos.y).Append(delimiter);
                sbBuf.AppendFormat("{0:F3}", headPos.z).Append(delimiter);
            }

            // head rot
            sbBuf.Append(bGotHeadRot ? "1" : "0").Append(delimiter);
            Vector3 vheadRot = headRot.eulerAngles;

            if (bGotHeadRot)
            {
                sbBuf.AppendFormat("{0:F3}", vheadRot.x).Append(delimiter);
                sbBuf.AppendFormat("{0:F3}", vheadRot.y).Append(delimiter);
                sbBuf.AppendFormat("{0:F3}", vheadRot.z).Append(delimiter);
            }

            // face rect
            sbBuf.Append("1").Append(delimiter);
            sbBuf.AppendFormat("{0:F0}", faceRect.x).Append(delimiter);
            sbBuf.AppendFormat("{0:F0}", faceRect.y).Append(delimiter);
            sbBuf.AppendFormat("{0:F0}", faceRect.width).Append(delimiter);
            sbBuf.AppendFormat("{0:F0}", faceRect.height).Append(delimiter);

            // animation units
            sbBuf.Append(bGotAU ? "1" : "0").Append(delimiter);

            if (bGotAU)
            {
                int enumCount = Enum.GetNames(typeof(KinectInterop.FaceShapeAnimations)).Length;
                sbBuf.Append(enumCount).Append(delimiter);

                for (int i = 0; i < enumCount; i++)
                {
                    float dictValue = dictAU[(KinectInterop.FaceShapeAnimations)i];
                    sbBuf.AppendFormat("{0:F3}", dictValue).Append(delimiter);
                }
            }

            // shape units
            sbBuf.Append(bGotSU ? "1" : "0").Append(delimiter);

            if (bGotSU)
            {
                int enumCount = Enum.GetNames(typeof(KinectInterop.FaceShapeDeformations)).Length;
                sbBuf.Append(enumCount).Append(delimiter);

                for (int i = 0; i < enumCount; i++)
                {
                    float dictValue = dictSU[(KinectInterop.FaceShapeDeformations)i];
                    sbBuf.AppendFormat("{0:F3}", dictValue).Append(delimiter);
                }
            }

            // any other parameters...
        }

        // remove the last delimiter
        if (sbBuf.Length > 0 && sbBuf[sbBuf.Length - 1] == delimiter)
        {
            sbBuf.Remove(sbBuf.Length - 1, 1);
        }

        return sbBuf.ToString();
    }

    // sets basic face parameters from a csv line
    public bool SetFaceParamsFromCsv(string sCsvLine)
    {
        if (sCsvLine.Length == 0)
            return false;

        // split the csv line in parts
        char[] delimiters = { ',' };
        string[] alCsvParts = sCsvLine.Split(delimiters);

        if (alCsvParts.Length < 1 || alCsvParts[0] != "fp")
            return false;

        int iIndex = 1;
        int iLength = alCsvParts.Length;

        if (iLength < (iIndex + 1))
            return false;

        // head pos
        bGotHeadPos = (alCsvParts[iIndex] == "1");
        iIndex++;

        if (bGotHeadPos && iLength >= (iIndex + 3))
        {
            float x = 0f, y = 0f, z = 0f;

            float.TryParse(alCsvParts[iIndex], out x);
            float.TryParse(alCsvParts[iIndex + 1], out y);
            float.TryParse(alCsvParts[iIndex + 2], out z);
            iIndex += 3;

            headPos = new Vector3(x, y, z);
        }

        // head rot
        bGotHeadRot = (alCsvParts[iIndex] == "1");
        iIndex++;

        if (bGotHeadRot && iLength >= (iIndex + 3))
        {
            float x = 0f, y = 0f, z = 0f;

            float.TryParse(alCsvParts[iIndex], out x);
            float.TryParse(alCsvParts[iIndex + 1], out y);
            float.TryParse(alCsvParts[iIndex + 2], out z);
            iIndex += 3;

            headRot = Quaternion.Euler(x, y, z);
        }

        // face rect
        bool bGotFaceRect = (alCsvParts[iIndex] == "1");
        iIndex++;

        if (bGotFaceRect && iLength >= (iIndex + 4))
        {
            float x = 0f, y = 0f, w = 0f, h = 0f;

            float.TryParse(alCsvParts[iIndex], out x);
            float.TryParse(alCsvParts[iIndex + 1], out y);
            float.TryParse(alCsvParts[iIndex + 2], out w);
            float.TryParse(alCsvParts[iIndex + 3], out h);
            iIndex += 4;

            faceRect.x = x; faceRect.y = y;
            faceRect.width = w; faceRect.height = h;
        }

        // animation units
        bGotAU = (alCsvParts[iIndex] == "1");
        iIndex++;

        if (bGotAU && iLength >= (iIndex + 1))
        {
            int count = 0;
            int.TryParse(alCsvParts[iIndex], out count);
            iIndex++;

            for (int i = 0; i < count && iLength >= (iIndex + 1); i++)
            {
                float v = 0;
                float.TryParse(alCsvParts[iIndex], out v);
                iIndex++;

                dictAU[(KinectInterop.FaceShapeAnimations)i] = v;
            }
        }

        // shape units
        bGotSU = (alCsvParts[iIndex] == "1");
        iIndex++;

        if (bGotSU && iLength >= (iIndex + 1))
        {
            int count = 0;
            int.TryParse(alCsvParts[iIndex], out count);
            iIndex++;

            for (int i = 0; i < count && iLength >= (iIndex + 1); i++)
            {
                float v = 0;
                float.TryParse(alCsvParts[iIndex], out v);
                iIndex++;

                dictSU[(KinectInterop.FaceShapeDeformations)i] = v;
            }
        }

        // any other parameters here...

        // emulate face tracking
        lastFaceTrackedTime = Time.realtimeSinceStartup;

        return true;
    }

    // gets face model vertices as csv line
    public string GetFaceVerticesAsCsv()
    {
        // create the output string
        StringBuilder sbBuf = new StringBuilder();
        const char delimiter = ',';

        if (bGotModelVertices && avModelVertices != null)
        {
            sbBuf.Append("fv").Append(delimiter);

            // model vertices
            int vertCount = avModelVertices.Length;
            sbBuf.Append(vertCount).Append(delimiter);

            for (int i = 0; i < vertCount; i++)
            {
                sbBuf.AppendFormat("{0:F3}", avModelVertices[i].x).Append(delimiter);
                sbBuf.AppendFormat("{0:F3}", avModelVertices[i].y).Append(delimiter);
                sbBuf.AppendFormat("{0:F3}", avModelVertices[i].z).Append(delimiter);
            }
        }

        // remove the last delimiter
        if (sbBuf.Length > 0 && sbBuf[sbBuf.Length - 1] == delimiter)
        {
            sbBuf.Remove(sbBuf.Length - 1, 1);
        }

        return sbBuf.ToString();
    }

    // sets face model vertices from a csv line
    public bool SetFaceVerticesFromCsv(string sCsvLine)
    {
        if (sCsvLine.Length == 0)
            return false;

        // split the csv line in parts
        char[] delimiters = { ',' };
        string[] alCsvParts = sCsvLine.Split(delimiters);

        if (alCsvParts.Length < 1 || alCsvParts[0] != "fv")
            return false;

        int iIndex = 1;
        int iLength = alCsvParts.Length;

        if (iLength < (iIndex + 1))
            return false;

        // model vertices
        int vertCount = 0;
        int.TryParse(alCsvParts[iIndex], out vertCount);
        iIndex++;

        if (vertCount > 0)
        {
            if (avModelVertices == null || avModelVertices.Length != vertCount)
            {
                avModelVertices = new Vector3[vertCount];
            }

            for (int i = 0; i < vertCount && iLength >= (iIndex + 3); i++)
            {
                float x = 0f, y = 0f, z = 0f;

                float.TryParse(alCsvParts[iIndex], out x);
                float.TryParse(alCsvParts[iIndex + 1], out y);
                float.TryParse(alCsvParts[iIndex + 2], out z);
                iIndex += 3;

                avModelVertices[i] = new Vector3(x, y, z);
            }

            bGotModelVertices = true;
            bGotModelVerticesFromDC = true;
        }

        return true;
    }

    // gets face model UVs as csv line
    public string GetFaceUvsAsCsv()
    {
        // create the output string
        StringBuilder sbBuf = new StringBuilder();
        const char delimiter = ',';

        if (bGotModelVertices && avModelUV != null)
        {
            sbBuf.Append("fu").Append(delimiter);

            // face rect width & height
            sbBuf.AppendFormat("{0:F0}", faceRect.width).Append(delimiter);
            sbBuf.AppendFormat("{0:F0}", faceRect.height).Append(delimiter);

            // model UVs
            int uvCount = avModelUV.Length;
            sbBuf.Append(uvCount).Append(delimiter);

            for (int i = 0; i < uvCount; i++)
            {
                sbBuf.AppendFormat("{0:F3}", avModelUV[i].x).Append(delimiter);
                sbBuf.AppendFormat("{0:F3}", avModelUV[i].y).Append(delimiter);
            }
        }

        // remove the last delimiter
        if (sbBuf.Length > 0 && sbBuf[sbBuf.Length - 1] == delimiter)
        {
            sbBuf.Remove(sbBuf.Length - 1, 1);
        }

        return sbBuf.ToString();
    }

    // sets face model UVs from a csv line
    public bool SetFaceUvsFromCsv(string sCsvLine)
    {
        if (sCsvLine.Length == 0)
            return false;

        // split the csv line in parts
        char[] delimiters = { ',' };
        string[] alCsvParts = sCsvLine.Split(delimiters);

        if (alCsvParts.Length < 1 || alCsvParts[0] != "fu")
            return false;

        int iIndex = 1;
        int iLength = alCsvParts.Length;

        if (iLength < (iIndex + 2))
            return false;

        // face width & height
        float w = 0f, h = 0f;

        float.TryParse(alCsvParts[iIndex], out w);
        float.TryParse(alCsvParts[iIndex + 1], out h);
        iIndex += 2;

        faceRect.width = w; faceRect.height = h;

        // model UVs
        int uvCount = 0;
        if (iLength >= (iIndex + 1))
        {
            int.TryParse(alCsvParts[iIndex], out uvCount);
            iIndex++;
        }

        if (uvCount > 0)
        {
            if (avModelUV == null || avModelUV.Length != uvCount)
            {
                avModelUV = new Vector2[uvCount];
            }

            for (int i = 0; i < uvCount && iLength >= (iIndex + 2); i++)
            {
                float x = 0f, y = 0f;

                float.TryParse(alCsvParts[iIndex], out x);
                float.TryParse(alCsvParts[iIndex + 1], out y);
                iIndex += 2;

                avModelUV[i] = new Vector2(x, y);
            }
        }

        return true;
    }

    // gets face model triangles as csv line
    public string GetFaceTrianglesAsCsv()
    {
        // create the output string
        StringBuilder sbBuf = new StringBuilder();
        const char delimiter = ',';

        if (avModelTriangles != null)
        {
            sbBuf.Append("ft").Append(delimiter);

            // model triangles
            int triCount = avModelTriangles.Length;
            sbBuf.Append(triCount).Append(delimiter);

            for (int i = 0; i < triCount; i++)
            {
                sbBuf.Append(avModelTriangles[i]).Append(delimiter);
            }
        }

        // remove the last delimiter
        if (sbBuf.Length > 0 && sbBuf[sbBuf.Length - 1] == delimiter)
        {
            sbBuf.Remove(sbBuf.Length - 1, 1);
        }

        return sbBuf.ToString();
    }

    // sets face model model from a csv line
    public bool SetFaceTrianglesFromCsv(string sCsvLine)
    {
        if (sCsvLine.Length == 0)
            return false;

        // split the csv line in parts
        char[] delimiters = { ',' };
        string[] alCsvParts = sCsvLine.Split(delimiters);

        if (alCsvParts.Length < 1 || alCsvParts[0] != "ft")
            return false;

        int iIndex = 1;
        int iLength = alCsvParts.Length;

        if (iLength < (iIndex + 1))
            return false;

        // model triangles
        int triCount = 0;
        int.TryParse(alCsvParts[iIndex], out triCount);
        iIndex++;

        if (triCount > 0)
        {
            if (avModelTriangles == null || avModelTriangles.Length != triCount)
            {
                avModelTriangles = new int[triCount];
            }

            for (int i = 0; i < triCount && iLength >= (iIndex + 1); i++)
            {
                int v = 0;

                int.TryParse(alCsvParts[iIndex], out v);
                iIndex++;

                avModelTriangles[i] = v;
            }

            bGotModelTriangles = true;
            bGotModelTrianglesFromDC = true;
        }

        return true;
    }


    /////////////////////////////////////////////////AH

    public GameObject MainCamera;
    public GameObject Display;

    //Display sizes
    [Range(1, 20)]
    public int ScaleFactorHigh = 3;

    [Range(1, 20)]
    public int ScaleFactorMedium = 5;

    [Range(1, 20)]
    public int ScaleFactorLow = 7;

    [Range(-10, 10)]
    public float VerticalDisplayOffset = 0.4f;

    [Tooltip("Time to wait (in seconds) before a temporarily unengaged user is declared as unengaged")]
    public float UnengagementThreshold = 5f;

    [Tooltip("Time to wait (in seconds) before a temporarily engaged user is declared as engaged")]
    public float EngagementThreshold = 2f;

    private Dictionary<long, EngagementData> dictUserIdToEngagement = new Dictionary<long, EngagementData>();

    public enum EngagementLevel
    {
        Unknown, //beyond tracking area
        None, //in tracking area, but not looking at display
        Low,
        Medium,
        High
    }

    public class EngagementData
    {
        public EngagementLevel Level;
        public float TimeEngaged;
        public float TimeUnengaged;
    }



    private void CreateDisplays()
    {
        Display.name = "Display_high";
        Display.transform.position = new Vector3(Display.transform.position.x, Display.transform.position.y + VerticalDisplayOffset, Display.transform.position.z);

        GameObject medium = Instantiate(Display);
        medium.name = "Display_medium";
        var oldScale = medium.transform.localScale;
        medium.transform.localScale = new Vector3(oldScale.x * (ScaleFactorHigh + (ScaleFactorMedium - ScaleFactorHigh) / 2), oldScale.y * ScaleFactorMedium, oldScale.z);
        Vector3 oldPos = medium.transform.position;
        medium.transform.position = new Vector3(oldPos.x, oldPos.y, oldPos.z - Display.transform.localScale.z);

        GameObject low = Instantiate(Display);
        low.name = "Display_low";
        oldScale = low.transform.localScale;
        low.transform.localScale = new Vector3(oldScale.x * (ScaleFactorMedium + (ScaleFactorLow - ScaleFactorMedium) / 2), oldScale.y * ScaleFactorLow, oldScale.z);
        oldPos = low.transform.position;
        low.transform.position = new Vector3(oldPos.x, oldPos.y, oldPos.z - 2 * Display.transform.localScale.z);

        oldScale = Display.transform.localScale;
        Display.transform.localScale = new Vector3(oldScale.x * ScaleFactorHigh, oldScale.y * ScaleFactorHigh, oldScale.z);

        medium.transform.parent = Display.transform;
        low.transform.parent = Display.transform;
    }

    public void CalcEngagementLevel(long userId)
    {
        KinectManager kinectManager = KinectManager.Instance;

        if (kinectManager && kinectManager.IsInitialized())
        {
            Quaternion headRotation = new Quaternion();
            bool bGotHeadRotation = sensorData.sensorInterface.GetHeadRotation(userId, ref headRotation);
            bool isFaceTracked = sensorData.sensorInterface.IsFaceTracked(userId);

            if (bGotHeadRotation && isFaceTracked)
            {
                var headPosWorldSpace = kinectManager.GetJointPosition(userId, (int)KinectInterop.JointType.Head);
                var layerMask = 1 << 8; //Select Layer 8: Raycast
                RaycastHit hitInfo;

                Ray ray = new Ray(new Vector3(headPosWorldSpace.x, headPosWorldSpace.y, headPosWorldSpace.z + MainCamera.transform.position.z),
                        Quaternion.Euler(headRotation.eulerAngles.x, -headRotation.eulerAngles.y, headRotation.eulerAngles.z) * Vector3.back);

                Debug.DrawRay(ray.origin, ray.direction * 100000, Color.cyan, 0.5f);

                if (Physics.Raycast(ray, out hitInfo, Mathf.Infinity, layerMask))
                {
                    //Debug.Log("Hitinfo: " + hitInfo.collider.gameObject.name);

                    EngagementLevel level;

                    switch (hitInfo.collider.gameObject.name)
                    {
                        case "Display_high":
                            level = EngagementLevel.High;
                            break;
                        case "Display_medium":
                            level = EngagementLevel.Medium;
                            break;
                        case "Display_low":
                            level = EngagementLevel.Low;
                            break;
                        default:
                            level = EngagementLevel.None;
                            break;
                    }

                    SetUserEngagement(userId, level, Time.time);
                    return;
                }
            }
        }

        SetUserEngagement(userId, EngagementLevel.None, Time.time);
    }

    public void SetUserEngagement(long userId, EngagementLevel level, float time)
    {
        //Modify Engagement Data
        if (dictUserIdToEngagement.ContainsKey(userId))
        {
            //Set engagement data if level changed only (keeps the timestamp)
            if (dictUserIdToEngagement[userId].Level != level)
            {
                //Set time if level changed between engaged -> unengaged
                if (dictUserIdToEngagement[userId].Level > EngagementLevel.None && level <= EngagementLevel.None)
                {
                    dictUserIdToEngagement[userId].TimeUnengaged = time;
                }
                //Set time if level changed between unengaged -> engaged
                else if (dictUserIdToEngagement[userId].Level <= EngagementLevel.None && level > EngagementLevel.None)
                {
                    float deltaTimeUnengaged = Time.time - dictUserIdToEngagement[userId].TimeUnengaged;

                    //DeltaTimeUnengaged exceeded UnengagementThreshold
                    if (deltaTimeUnengaged > UnengagementThreshold)
                    {
                        dictUserIdToEngagement[userId].TimeEngaged = time;
                    }

                    dictUserIdToEngagement[userId].TimeUnengaged = 0;
                }

                dictUserIdToEngagement[userId].Level = level;
            }
        }
        //Set Engagement Data for the first time
        else
        {
            if (level <= EngagementLevel.None)
            {
                dictUserIdToEngagement[userId] = new EngagementData { Level = level, TimeUnengaged = -(time + UnengagementThreshold * 2) };
            }
            else
            {
                dictUserIdToEngagement[userId] = new EngagementData { Level = level, TimeEngaged = time };
            }
        }
    }

    public void RemoveUserEngagement(long userId)
    {
        if (!dictUserIdToEngagement.Remove(userId))
        {
            Debug.Log(string.Format("Engagement Data of User {0} couldn't be deleted", userId));
        }
    }

    public EngagementLevel GetEngagementLevel(long userId)
    {
        return dictUserIdToEngagement.ContainsKey(userId) ? dictUserIdToEngagement[userId].Level : EngagementLevel.Unknown;
    }

    public EngagementData GetEngagementData(long userId)
    {
        return dictUserIdToEngagement.ContainsKey(userId) ? dictUserIdToEngagement[userId] : null;
    }

    public long GetClosestEngagedUser(ref long[] aUserIndexIds)
    {
        long userId = 0;
        EngagementLevel level = EngagementLevel.None;

        for (var i = 0; i < aUserIndexIds.Length; i++)
        {
            if (aUserIndexIds[i] == 0) continue;

            long tempUserId = aUserIndexIds[i];
            EngagementLevel tempLevel = dictUserIdToEngagement[tempUserId].Level;

            if (tempLevel > level && IsEngaged(tempUserId))
            {
                userId = tempUserId;
                level = tempLevel;
            }
        }

        return userId;
    }

    public bool IsEngaged(long userId)
    {
        if (!dictUserIdToEngagement.ContainsKey(userId)) return false;

        bool res = ((dictUserIdToEngagement[userId].Level > EngagementLevel.None) &&
            (Time.time - dictUserIdToEngagement[userId].TimeEngaged > EngagementThreshold)) ||
            ((dictUserIdToEngagement[userId].Level <= EngagementLevel.None) &&
            (Time.time - dictUserIdToEngagement[userId].TimeUnengaged <= UnengagementThreshold));

        //float deltaTime = dictUserIdToEngagement[userId].TimeUnengaged == 0f ?
        //    Time.time - dictUserIdToEngagement[userId].TimeEngaged : Time.time - dictUserIdToEngagement[userId].TimeUnengaged;

        //Debug.Log(String.Format("IsEngaged? {3} UserID: {0}, Level: {1}, TimeEng: {4}, TimeUneng: {2}, deltaTime: {5}",
        //    userId.ToString().Substring(15),
        //    dictUserIdToEngagement[userId].Level,
        //    dictUserIdToEngagement[userId].TimeUnengaged,
        //    res,
        //    dictUserIdToEngagement[userId].TimeEngaged,
        //    deltaTime));

        return res;
    }

}
