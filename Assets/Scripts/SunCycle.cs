using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public enum SunCycleDirection
{
    Clockwise,
    CounterClockwise
}

public enum TimeOfDay
{
    Dusk,
    Day,
    Dawn,
    Night
}

[RequireComponent(typeof(Light))]
public class SunCycle : MonoBehaviour
{
    [SerializeField] private Color _duskBackgroundColor;
    [SerializeField] private Color _dayBackgroundColor;
    [SerializeField] private Color _dawnBackgroundColor;
    [SerializeField]private Color _nightBackgroundColor;

    [SerializeField] private Color _duskAmbientColor;
    [SerializeField] private Color _dayAmbientColor;
    [SerializeField] private Color _dawnAmbientColor;
    [SerializeField] private Color _nightAmbientColor;

    [SerializeField]
    [Range(0, 360)]
    private float _duskSunXAngle;
    [SerializeField]
    [Range(0, 360)]
    private float _daySunXAngle;
    [SerializeField]
    [Range(0, 360)]
    private float _dawnSunXAngle;
    [SerializeField]
    [Range(0, 360)]
    private float _nightSunXAngle;
    [SerializeField]
    [Range(0, 360)]
    private float _nightSunXAngle2Dusk;

    [SerializeField] [Range(0, 360)] private int _cycleLength = 36;
    [SerializeField] private SunCycleDirection _cycleDirection;
    [SerializeField] [Range(0f, 360f)] private float _maxDeltaDaylight;
    [SerializeField] private Material _backgroundMaterial;
    [SerializeField] private TimeOfDay _intialTimeOfDay;
    [SerializeField] [Range(0f, 1f)] private float _dayLightIntensity;
    [SerializeField] [Range(0f, 1f)] private float _nightLightIntensity;
    [SerializeField] [Range(0f, 1f)] private float _dawnLightIntensity;
    [SerializeField] [Range(0f, 1f)] private float _duskLightIntensity;

    private Light _sun;
    private float _initalSunRotation;
    private TimeOfDay _timeOfDay = TimeOfDay.Day;
    private float _SinglePhaseLength;
    private int _cycleTimer = 0;
    private float _cycleTimer_Ticks = 0f;
    private Color _currentAmbientColor;
    private float _lerpTimer = 0f;

    private void Awake()
    {
        _sun = GetComponent<Light>();
        _initalSunRotation = transform.rotation.eulerAngles.x;
        _SinglePhaseLength = _cycleLength/4;
        InitializeCycle();

   
    }

    private void InitializeCycle()
    {
        if (_intialTimeOfDay == TimeOfDay.Dusk)
        {
            InitDusk();
        }
        else if (_intialTimeOfDay == TimeOfDay.Day)
        {
            InitDay();
        }
        else if (_intialTimeOfDay == TimeOfDay.Dawn)
        {
            InitDawn();
        }
        else
        {
           InitNight();
        }

        _cycleTimer = Mathf.RoundToInt(_SinglePhaseLength);
        Debug.Log(_SinglePhaseLength);
    }
	
	private void Update ()
	{
	    UpdateTimers();
        UpdateTimeOfDay();
	}

    private void UpdateTimers()
    {
        _cycleTimer_Ticks += Time.deltaTime;
        if (Mathf.RoundToInt(_cycleTimer_Ticks % 60) >= 1)
        {
            _cycleTimer_Ticks = 0f;
            _cycleTimer++;
        }

        if (_cycleTimer > _cycleLength)
        {
            _cycleTimer = 0;
            _cycleTimer_Ticks = 0f;
        }
 
        _lerpTimer += (Time.deltaTime/(_SinglePhaseLength * (_cycleLength/100f)));
    }

    private void UpdateTimeOfDay()
    {

        if (_cycleTimer >= (int)TimeOfDay.Night * _SinglePhaseLength)
        {
            if (_timeOfDay != TimeOfDay.Night)
            {
                InitNight();
            }
            else
            {
                transform.rotation = Quaternion.Lerp(Quaternion.Euler(_nightSunXAngle2Dusk, transform.rotation.y, transform.rotation.z), Quaternion.Euler(360+_duskSunXAngle, transform.rotation.y, transform.rotation.z), _lerpTimer);
                _sun.intensity = Mathf.Lerp(_nightLightIntensity, _duskLightIntensity, _lerpTimer);
                RenderSettings.ambientLight = Color.Lerp(_nightAmbientColor, _duskAmbientColor, _lerpTimer);
                _backgroundMaterial.color = Color.Lerp(_nightBackgroundColor, _duskBackgroundColor, _lerpTimer);
            }
        }
        else if (_cycleTimer >= (int)TimeOfDay.Dawn * _SinglePhaseLength)
        {
            if (_timeOfDay != TimeOfDay.Dawn)
            {
                InitDawn();
            }
            else
            {
                transform.rotation = Quaternion.Lerp(Quaternion.Euler(_dawnSunXAngle, transform.rotation.y, transform.rotation.z), Quaternion.Euler(_nightSunXAngle, transform.rotation.y, transform.rotation.z), _lerpTimer);
                _sun.intensity = Mathf.Lerp(_dawnLightIntensity, _nightLightIntensity, _lerpTimer);
                RenderSettings.ambientLight = Color.Lerp(_dawnAmbientColor, _nightAmbientColor, _lerpTimer);
                _backgroundMaterial.color = Color.Lerp(_dawnBackgroundColor, _nightBackgroundColor, _lerpTimer);
            }
        }
        else if (_cycleTimer >= (int)TimeOfDay.Day * _SinglePhaseLength)
        {
            if (_timeOfDay != TimeOfDay.Day)
            {
                InitDay();
            }
            else
            {
                transform.rotation = Quaternion.Lerp(Quaternion.Euler(_daySunXAngle, transform.rotation.y, transform.rotation.z), Quaternion.Euler(_dawnSunXAngle, transform.rotation.y, transform.rotation.z), _lerpTimer);
                _sun.intensity = Mathf.Lerp(_dayLightIntensity, _dawnLightIntensity, _lerpTimer);
                RenderSettings.ambientLight = Color.Lerp(_dayAmbientColor, _dawnAmbientColor, _lerpTimer);
                _backgroundMaterial.color = Color.Lerp(_dayBackgroundColor, _dawnBackgroundColor, _lerpTimer);
            }
        }
        else
        {
            if (_timeOfDay != TimeOfDay.Dusk)
            {
                InitDusk();
            }
            else
            {
                transform.rotation = Quaternion.Lerp(Quaternion.Euler(_duskSunXAngle, transform.rotation.y, transform.rotation.z), Quaternion.Euler(_daySunXAngle, transform.rotation.y, transform.rotation.z), _lerpTimer);
                _sun.intensity = Mathf.Lerp(_duskLightIntensity, _dayLightIntensity, _lerpTimer);
                RenderSettings.ambientLight = Color.Lerp(_duskAmbientColor, _dayAmbientColor, _lerpTimer);
                _backgroundMaterial.color = Color.Lerp(_duskBackgroundColor, _dayBackgroundColor, _lerpTimer);
            }
        }
    }

    private void InitDusk()
    {
        _timeOfDay = TimeOfDay.Dusk;
        RenderSettings.ambientLight = _duskAmbientColor;
        _backgroundMaterial.color = _duskBackgroundColor;
        _currentAmbientColor = _duskAmbientColor;
        _sun.intensity = _duskLightIntensity;
        _lerpTimer = 0f;
    }

    private void InitDay()
    {
        _timeOfDay = TimeOfDay.Day;
        RenderSettings.ambientLight = _dayAmbientColor;
        _backgroundMaterial.color = _dayBackgroundColor;
        _currentAmbientColor = _dayAmbientColor;
        _sun.intensity = _dayLightIntensity;
        _lerpTimer = 0f;
    }

    private void InitDawn()
    {
        _timeOfDay = TimeOfDay.Dawn;
        RenderSettings.ambientLight = _dawnAmbientColor;
        _backgroundMaterial.color = _dawnBackgroundColor;
        _currentAmbientColor = _dawnAmbientColor;
        _sun.intensity = _dawnLightIntensity;
        _lerpTimer = 0f;
    }

    private void InitNight()
    {
        _timeOfDay = TimeOfDay.Night;
        RenderSettings.ambientLight = _nightAmbientColor;
        _backgroundMaterial.color = _nightBackgroundColor;
        _currentAmbientColor = _nightAmbientColor;
        _sun.intensity = _nightLightIntensity;
        transform.rotation = Quaternion.Euler(_nightSunXAngle2Dusk, transform.rotation.y, transform.rotation.z);
        _lerpTimer = 0f;
    }



    private float EulerAngleToClockDegree(float angle)
    {
        angle = (angle/360);
        angle = angle - (float) Math.Truncate(angle);
        return angle*360;
    }
}
