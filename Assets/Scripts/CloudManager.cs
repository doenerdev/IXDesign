using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CloudDirection
{
    Left,
    Right
}

public class CloudManager : MonoBehaviour
{

    [SerializeField] private GameObject[] _cloudTempaltes;
    [SerializeField] private GameObject _cloudWrapper;
    [SerializeField] private CloudDirection _cloudDirection;
    [SerializeField] [Range(0f, 2f)] private float _cloudMovementSpeed;
    [SerializeField] private Vector3 _lowerLimits;
    [SerializeField] private Vector3 _upperLimits;
    [SerializeField] private int _minInitalCloudQty;
    [SerializeField] private int _maxInitalCloudQty;

    private List<GameObject> _clouds;

    private void Awake()
    {
        _clouds = new List<GameObject>();
    }

	private void Start ()
	{
	    int cloudQty = Random.Range(_minInitalCloudQty, _maxInitalCloudQty+1);
	    for (int i = 0; i < cloudQty; i++)
	    {
	        InitializeCloud();
	    }
	}


    private void InitializeCloud()
    {
        float posX = _cloudDirection == CloudDirection.Left ? _lowerLimits.x : _upperLimits.x;
        Vector3 initPosition = new Vector3(posX, Random.Range(_lowerLimits.y, _upperLimits.y), Random.Range(_lowerLimits.z, _upperLimits.z));
        GameObject cloud = GameObject.Instantiate(_cloudTempaltes[Random.Range(0, _cloudTempaltes.Length-1)], initPosition, Quaternion.identity);
        cloud.transform.SetParent(_cloudWrapper.transform);
         _clouds.Add(cloud);
    }

    private void MoveClouds()
    {
        int direction = _cloudDirection == CloudDirection.Left ? 1 : -1;
        foreach (GameObject cloud in _clouds)
        {
            cloud.transform.position = new Vector3(transform.position.x + _cloudMovementSpeed * direction, transform.position.y, transform.position.z);
        }
    }

    private void RemoveClouds()
    {
        float limitX = _cloudDirection == CloudDirection.Left ? _upperLimits.x : _lowerLimits.x;
        foreach (GameObject cloud in _clouds)
        {
            if (_cloudDirection == CloudDirection.Left && cloud.transform.position.x > _upperLimits.x)
            {
                _clouds.Remove(cloud);
                Destroy(cloud);
            }
            else if (_cloudDirection == CloudDirection.Right && cloud.transform.position.x < _lowerLimits.x)
            {
                _clouds.Remove(cloud);
                Destroy(cloud);
            }
        }
    }

	void Update () {
		MoveClouds();
	    RemoveClouds();
	}
}
