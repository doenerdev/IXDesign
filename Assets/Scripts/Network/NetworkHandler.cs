using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

/// <summary>
/// Deals with sending and receiving data over the network
/// via OSC/UDP. Can be accessed as a singleton.
/// </summary>
public class NetworkHandler : MonoBehaviour, IOSCSubscribable
{

    private static NetworkHandler _Instance = null;
    private List<IOSCSubscriber> _subscriber = new List<IOSCSubscriber>();
    private Dictionary<string, ClientLog> _clients;
    private Dictionary<string, ServerLog> _servers;
    private int _qtyOSCMessages = 0;
    private long _lastMessageTimeStamp;
    private int _lastMessageID = -999999;

    [SerializeField] private string _ListenerServerName;
    [SerializeField] private string _TargetClientName;
    [SerializeField] private string _TargetClientIP;
    [Range(1025, 9999)][SerializeField] private uint _SenderPort;
    [Range(1025, 9999)][SerializeField] private uint _ListenerPort;

    public string TargetClientName
    {
        get { return _TargetClientName; }
    }
    public string TargetClientIP
    {
        get { return _TargetClientIP; }
    }
    public uint SenderPort
    {
        get { return _SenderPort; }
    }
    public uint ListenerPort
    {
        get { return _ListenerPort; }
    }
    public string ListenerServerName
    {
        get { return _ListenerServerName; }
    }
    // makes the class a singleton instance
    public static NetworkHandler Instance
    {
        get
        {
            _Instance = Object.FindObjectOfType<NetworkHandler>();
            if (_Instance == null)
            {
                _Instance = Camera.main.gameObject.AddComponent<NetworkHandler>();
            }
            return _Instance;
        }
    } 

	private void Start ()
	{
        //Call the OSCHandler to create a client and server for all osc actions
        OSCHandler.Instance.Init();
    }

	private void Update () {
        CheckForOSCMessages();
    }

    /// <summary>
    /// Checks for new osc message and delegates the latest message all subscribers
    /// </summary>
    private void CheckForOSCMessages()
    {
        OSCHandler.Instance.UpdateLogs();
        _servers = OSCHandler.Instance.Servers;

        foreach (KeyValuePair<string, ServerLog> item in _servers)
        {
            //Debug.Log("OSC Count:" + _qtyOSCMessages + "  array count:" + item.Value.packets.Count);
            if (item.Value.packets.Count > 0)
            {
                int messageID;
                string adress = item.Value.packets[item.Value.packets.Count - 1].Address;
                bool isNumeric = int.TryParse(adress, out messageID);
                if (isNumeric == false)
                {
                    if (adress == "TrackingFlag")
                    {
                        int iflag;
                        bool isFlagNumeric = int.TryParse(item.Value.packets[item.Value.packets.Count - 1].Data[0].ToString(), out iflag);
                        if (isFlagNumeric)
                        {
                            UpdateSubscriberTracking(iflag == 1 ? true : false);
                        }
                        continue;
                    }
                }
                if (messageID == _lastMessageID)
                {
                    //UpdateSubscriberTracking(false);
                    continue;
                }
                else
                {
                    _lastMessageID = messageID;
                }

                
                var messageData = item.Value.packets[item.Value.packets.Count - 1].Data[0].ToString();
               
                NotifyOSCMessage(messageData);
                _qtyOSCMessages++;
                _lastMessageTimeStamp = item.Value.packets[item.Value.packets.Count - 1].TimeStamp;
                 
            }
        }
    }

    /// <summary>
    /// Sends data via OSC. The data needs to be formatted as a json string.
    /// </summary>
    public void SendOSCKinectData(string data, int messageID)
    {
        OSCHandler.Instance.SendMessageToClient(NetworkHandler.Instance.TargetClientName, messageID.ToString(), data);
    }

    public void SendOSCTrackingFlag(bool flag)
    {
        int iflag = flag == true ? 1 : 0;
        OSCHandler.Instance.SendMessageToClient(NetworkHandler.Instance.TargetClientName, "TrackingFlag", iflag.ToString());
    }

    public void Subscribe(IOSCSubscriber subscriber)
    {
        this._subscriber.Add(subscriber);
    }

    public void Unsubscribe(IOSCSubscriber subscriber)
    {
        this._subscriber.Remove(subscriber);
    }

    /// <summary>
    /// Sends data to each subscriber
    /// </summary>
    public void NotifyOSCMessage(string message)
    {
        foreach(var sub in _subscriber )
        { 
            sub.OSCMessageUpdate(message);
        }
    }

    private void UpdateSubscriberTracking(bool state)
    {
        foreach (var sub in _subscriber)
        {
            sub.UpdateTrackingState(state);
        }
    }



}
