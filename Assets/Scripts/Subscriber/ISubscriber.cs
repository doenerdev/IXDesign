using UnityEngine;
using System.Collections;

public interface IOSCSubscriber
{
    void OSCMessageUpdate(string message);
    void UpdateTrackingState(bool state);
}

public interface IAvatarControllerSubscriber
{
    void AvatarControllerTrackingState(bool state);
}