using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Offers methods for converting data types to a literal (string) data representation
/// </summary>
public static class LiteralDataHelper
{

    private const char QBegin = '(';
    private const char QSep = ',';
    private const char QEnd = ')';
    private const char RBegin = '{';
    private const char RSep = QSep;
    private const char REnd = '}';
    private static Regex rootPositionRegEx = new Regex(@"\{([^\}]+)\}");

    /// <summary>
    /// Converts a joint orientation (quaternion) dictionary to literal data
    /// </summary>
    public static string JointOrientationsToLiteralData(Dictionary<int, Quaternion> orientations, Vector3 rootPosition)
    {
        string literalData = "";
        literalData += RBegin.ToString() + rootPosition.x + RSep.ToString() + rootPosition.y + RSep.ToString() + rootPosition.z + REnd.ToString();
        foreach (KeyValuePair<int, Quaternion> orientation in orientations)
        {
            literalData += QBegin.ToString() + orientation.Value.x + QSep.ToString() + orientation.Value.y + QSep.ToString() + orientation.Value.z + QSep.ToString() + orientation.Value.w + QEnd.ToString();
        }
        return literalData;
    }

    /// <summary>
    /// Converts literal data to a joint orientation (quaternion) dictionary
    /// </summary>
    public static Dictionary<int, Quaternion> LiteralDataToJointOrientations(string data)
    {
        Dictionary<int, Quaternion> orientations = new Dictionary<int, Quaternion>();
        Quaternion quaternionBuffer = new Quaternion();
        string valueBuffer = "";
        ushort quaternionCounter = 0;
        int orientationsCounter = 0;

        //remove the root position info from the string
        data = rootPositionRegEx.Replace(data, "");

        for (int i = 0; i < 31; i++)
        {
            orientations[i] = Quaternion.identity;
        }

        for (int i = 0; i < data.Length; i++)
        {
            switch (data[i])
            {
                case QBegin:
                    valueBuffer = "";
                    quaternionCounter = 0;
                    break;
                case QSep:
                    quaternionBuffer[quaternionCounter] = Convert.ToSingle(valueBuffer);
                    quaternionCounter++;
                    valueBuffer = "";
                    break;
                case QEnd:
                    quaternionBuffer[quaternionCounter] = Convert.ToSingle(valueBuffer);
                    orientations[orientationsCounter] = quaternionBuffer;
                    orientationsCounter++;
                    break;
                default:
                    valueBuffer += data[i];
                    break;
            }
        }

        return orientations;
    }

    public static Vector3 LiteralDataToRootPosition(string data)
    {
        //extract the position data substring
        data = rootPositionRegEx.Match(data).ToString();

        Vector3 position = new Vector3();
        string valueBuffer = "";
        ushort coordinateCounter = 0;
        int orientationsCounter = 0;

        for (int i = 0; i < data.Length; i++)
        {
            switch (data[i])
            {
                case RBegin:
                    valueBuffer = "";
                    coordinateCounter = 0;
                    break;
                case RSep:
                    position[coordinateCounter] = Convert.ToSingle(valueBuffer);
                    coordinateCounter++;
                    valueBuffer = "";
                    break;
                case REnd:
                    position[coordinateCounter] = Convert.ToSingle(valueBuffer);
                    return position;
                    break;
                default:
                    valueBuffer += data[i];
                    break;
            }
        }

        return position;
    }
}