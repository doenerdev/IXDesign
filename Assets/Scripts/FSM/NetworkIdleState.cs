using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkIdleState : FSMState
{
    private GameObject _character;
    private AvatarController _controller;

    public NetworkIdleState(GameObject character, AvatarController controller)
    {
        stateID = StateID.NetworkIdle;
        _character = character;
        _controller = controller;
    }

    public override void Reason()
    {
        if (_controller.IsTracked)
        {
            _character.GetComponent<AvatarController>().SetTransition(Transition.NetworkControlStarted);
        }
    }

    public override void Act()
    {
        //do nothin yet -> play the idle animation
    }

    /*public override void DoBeforeEntering()
    {
        _controller.Animator.enabled = true;
        _controller.IsAnimationRunning = true;

        if (_character.transform.position.x < _targetPosition.x)
        {
            _character.transform.eulerAngles = new Vector3(_character.transform.rotation.eulerAngles.x, 90, _character.transform.rotation.eulerAngles.z);
        }
        else
        {
            _character.transform.eulerAngles = new Vector3(_character.transform.rotation.eulerAngles.x, -90, _character.transform.rotation.eulerAngles.z);
        }
    }

    public override void DoBeforeLeaving()
    {
        _controller.Animator.enabled = false;
        _controller.IsAnimationRunning = false;
        _character.transform.eulerAngles = new Vector3(0, 0, 0);
    }*/
}