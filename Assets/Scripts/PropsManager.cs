using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PropsManager : MonoBehaviour
{
    public GameObject MainCamera;

    [Tooltip("Reference external templates, which are not children of PropsManager")]
    public List<GameObject> Templates;

    private KinectManager kinectManager;
    private static PropsManager instance;
    private Dictionary<long, PropController> dictUserIdToPropCtrl = new Dictionary<long, PropController>();

    /// <summary>
    /// Gets the single PropsManager instance.
    /// </summary>
    /// <value>The PropsManager instance.</value>
    public static PropsManager Instance
    {
        get
        {
            return instance;
        }
    }

    void Awake()
    {
        instance = this;
    }

    // Use this for initialization
    void Start()
    {
        kinectManager = KinectManager.Instance;
        GetChildTemplates();
    }

    // Update is called once per frame
    void Update()
    {
        if (!kinectManager || !kinectManager.IsInitialized()) return;

        foreach (var item in dictUserIdToPropCtrl)
        {
            var userId = item.Key;
            var propCtrl = item.Value;
            Vector3 userPos = kinectManager.GetUserPosition(userId);

            if ((!propCtrl.ApplyUserX && !propCtrl.ApplyUserY && !propCtrl.ApplyUserZ) || userPos == Vector3.zero) continue;

            userPos = new Vector3(-userPos.x, userPos.y, userPos.z + MainCamera.transform.position.z);
            Vector3 propPos = propCtrl.transform.position;

            propCtrl.transform.position = new Vector3(
                propCtrl.ApplyUserX ? userPos.x : propPos.x,
                propCtrl.ApplyUserY ? userPos.y : propPos.y,
                propCtrl.ApplyUserZ ? userPos.z : propPos.z);

            if (!propCtrl.gameObject.activeSelf)
            {
                propCtrl.gameObject.SetActive(true);
            }
        }
    }

    public void AddProp(long userId)
    {
        if (dictUserIdToPropCtrl.ContainsKey(userId)) return;

        GameObject template = SelectTemplate(userId);
        GameObject prop = (GameObject)Instantiate(template, transform.position, Quaternion.identity);
        prop.transform.SetParent(transform);
        dictUserIdToPropCtrl[userId] = prop.GetComponent<PropController>();
        dictUserIdToPropCtrl[userId].Init(userId, instance);
    }

    private GameObject SelectTemplate(long userId)
    {
        Vector3 userPos = kinectManager.GetUserPosition(userId);
        userPos = new Vector3(-userPos.x, userPos.y, userPos.z + MainCamera.transform.position.z);

        //TODO Wähle Template abhängig von UserPosition

        return Templates[0];
    }

    private void GetChildTemplates()
    {
        //Get templates parented to this GameObject
        foreach (Transform child in transform)
        {
            Templates.Add(child.gameObject);
        }
    }

    public void FadeOutProp(long userId)
    {
        if (IsProp(userId))
        {
            dictUserIdToPropCtrl[userId].FadeOut();
        }
        else
        {
            Debug.Log(string.Format("User {0} is NOT a prop. Couldn't fade out prop.", userId));
        }
    }

    public void RemoveProp(long userId)
    {
        if (IsProp(userId))
        {
            Destroy(dictUserIdToPropCtrl[userId].gameObject);
            dictUserIdToPropCtrl.Remove(userId);
        }
        else
        {
            Debug.Log(string.Format("User {0} is NOT a prop. Couldn't remove prop.", userId));
        }
    }

    public bool IsProp(long userId)
    {
        return dictUserIdToPropCtrl.ContainsKey(userId);
    }

    public bool IsPropFadingOut(long userId)
    {
        if (IsProp(userId))
        {
            return dictUserIdToPropCtrl[userId].IsFadingOut();
        }

        Debug.Log(string.Format("User {0} is NOT a prop.", userId));
        return false;
    }

    void OnDestroy()
    {
        instance = null;
    }

}