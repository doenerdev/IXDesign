using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Windows.Kinect;

public class BodyData {

    private Dictionary<JointType, Vector3> _JointPositions = new Dictionary<JointType, Vector3>();
    private Dictionary<JointType, Quaternion> _JointOrientations = new Dictionary<JointType, Quaternion>();

    public Dictionary<JointType, Vector3> JointPostions
    {
        get { return _JointPositions; }
    }

    public Dictionary<JointType, Quaternion> JOintQuaternions
    {
        get { return _JointOrientations; }
    }

    public BodyData()
    {
        InitJointLists();
    }

    public BodyData(Body body)
    {
        UpdateBodyData(body);
    }

    private void InitJointLists()
    {
        foreach (JointType type in Enum.GetValues(typeof(JointType)))
        {
            _JointPositions.Add(type, Vector3.zero);
            _JointOrientations.Add(type, Quaternion.identity);
        }
    }

    public void UpdateBodyData(Body body)
    {
        foreach (KeyValuePair<JointType, Windows.Kinect.Joint> joint in body.Joints)
        {
            Vector3 pos = new Vector3(joint.Value.Position.X, joint.Value.Position.Y, joint.Value.Position.Z);
            _JointPositions[joint.Key] = pos;
        }

        foreach (KeyValuePair<JointType, Windows.Kinect.JointOrientation> jointOrientation in body.JointOrientations)
        {
            Quaternion orientation = new Quaternion(jointOrientation.Value.Orientation.X, jointOrientation.Value.Orientation.Y, jointOrientation.Value.Orientation.Z, jointOrientation.Value.Orientation.W);
            _JointOrientations[jointOrientation.Key] = orientation;
        }

    }

    public string ToJson()
    {
        string json = "{ 'Positions': {";
        int loopCounter = 0;

        foreach (KeyValuePair<JointType, Vector3> joint in _JointPositions)
        {
            json += "'" + joint.Key + "': {";
            json += "'X': " + joint.Value.x;
            json += "'Y': " + joint.Value.y;
            json += "'Z': " + joint.Value.z;
            json += "}";
            if (loopCounter < _JointPositions.Count -1)
            {
                json += ",";
            }
            loopCounter++;
        }

        json += "} 'Orientations': {";
        loopCounter = 0;

        foreach (KeyValuePair<JointType, Quaternion> joint in _JointOrientations)
        {
            json += "'" + joint.Key + "': {";
            json += "'X': " + joint.Value.x;
            json += "'Y': " + joint.Value.y;
            json += "'Z': " + joint.Value.z;
            json += "'W': " + joint.Value.w;
            json += "}";
            if (loopCounter < _JointPositions.Count - 1)
            {
                json += ",";
            }
            loopCounter++;
        }

        json += "}}";

        return json;
    }

}
