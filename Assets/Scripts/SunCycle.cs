using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public enum SunCycleDirection
{
    Clockwise,
    CounterClockwise
}

public class SunCycle : MonoBehaviour
{
    [SerializeField] private Color _morningBackgroundColor;
    [SerializeField] private Color _dayBackgroundColor;
    [SerializeField] private Color _dawnBackgroundColor;
    [SerializeField]private Color _nightBackgroundColor;

    [SerializeField] [Range(0f, 0.5f)] private float _cycleSpeed;
    [SerializeField] private SunCycleDirection _cycleDirection;
    [SerializeField] [Range(0f, 360f)] private float _maxDeltaDaylight;
    [SerializeField] private Material backgroundMaterial;

    private float _initalSunRotation;

    private void Awake()
    {
        _initalSunRotation = transform.rotation.eulerAngles.x;
    }
	
	private void Update ()
	{
	    int direction = _cycleDirection == SunCycleDirection.Clockwise ? -1 : 1;

	    

	    transform.Rotate(_cycleSpeed * direction, 0, 0);
	}

    private float EulerAngleToClockDegree(float angle)
    {
        angle = (angle/360);
        angle = angle - (float) Math.Truncate(angle);
        return angle*360;
    }
}
