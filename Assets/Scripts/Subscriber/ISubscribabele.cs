using UnityEngine;
using System.Collections;

public interface IOSCSubscribable
{
    void Subscribe(IOSCSubscriber subscriber);
    void Unsubscribe(IOSCSubscriber subscriber);
    void NotifyOSCMessage(string message);
}