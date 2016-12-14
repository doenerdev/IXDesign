using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Kinect = Windows.Kinect;

public class KinectHandler : MonoBehaviour
{
    [Range(0, 10000)][SerializeField]private uint _ReturnToIdle_Threshold = 3600;

    private static KinectHandler _Instance;
    private List<long> _currentTrackingIDs = new List<long>();
    private long _currentActiveTrackingID = -1;
    private uint _noTrackingTimer = 0;
    private Dictionary<long, BodyData> _bodyData;

    // makes the class a singleton instance
    public static KinectHandler Instance
    {
        get
        {
            _Instance = Object.FindObjectOfType<KinectHandler>();
            if (_Instance == null)
            {
                _Instance = Camera.main.gameObject.AddComponent<KinectHandler>();
            }
            return _Instance;
        }
    }
	
	private void Update () {
        GatherBodyData();
    }

    /// <summary>
    /// Gather the kinect's tracked body data of the current frame
    /// </summary>
    private void GatherBodyData()
    {
        _bodyData = new Dictionary<long, BodyData>();
        Kinect.Body[] data = BodySourceManager.Instance.GetData();
        if (data == null)
            return;

        List<long> trackedIDs = new List<long>();
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == null)
                continue;

            if (data[i].IsTracked)
            {
                trackedIDs.Add((long)data[i].TrackingId);
                _bodyData.Add((long)data[i].TrackingId, new BodyData(data[i]));
            }
        }

        // Delete untracked bodies and reset the active tracking id if need be
        foreach (long currentTrackingID in _currentTrackingIDs)
        {
            if (!trackedIDs.Contains(currentTrackingID))
            {
                if (currentTrackingID == _currentActiveTrackingID)
                {
                    _currentActiveTrackingID = -1;
                }
            }
        }
        _currentTrackingIDs = trackedIDs;

        //Check if the once active body was deleted and try to find a new one
        if (_currentActiveTrackingID < 0)
        {
            FindNewActiveBody();
        }
    }


    /// <summary>
    /// Find a new active body for tracking 
    /// </summary>
    private void FindNewActiveBody()
    {
        if (_currentTrackingIDs.Count > 0)
        {
            _currentActiveTrackingID = (long) _currentTrackingIDs[0]; //needs to be updated later
            _noTrackingTimer = 0;
        }
        else
        {
            _currentActiveTrackingID = -1;
            _noTrackingTimer++;
        }
    }

    
}
