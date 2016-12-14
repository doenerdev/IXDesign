using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour
{

    private static GameManager _Instance = null;

    [SerializeField] private float _zPlanePosition;
    [SerializeField] private float _yPlanePosition;
    [SerializeField] private Transform _networkCharacterOrigin;
    [SerializeField] private Transform _localCharacterOrigin;

    // makes the class a singleton instance
    public static GameManager Instance
    {
        get
        {
            _Instance = Object.FindObjectOfType<GameManager>();
            if (_Instance == null)
            {
                _Instance = Camera.main.gameObject.AddComponent<GameManager>();
            }
            return _Instance;
        }
    }
    public float ZPlanePosition
    {
        get
        {
            return _zPlanePosition;
        }
    }
    public float YPlanePosition
    {
        get
        {
            return _yPlanePosition;
        }
    }
    public Transform NetworkCharacterOrigin
    {
        get
        {
            return _networkCharacterOrigin;
        }
    }
    public Transform LocalCharacterOrigin
    {
        get
        {
            return _localCharacterOrigin;
        }
    }
}
