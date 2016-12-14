using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkControlState : FSMState
{
    private GameObject _character;
    private AvatarController _controller;

    public NetworkControlState(GameObject character, AvatarController controller)
    {
        stateID = StateID.NetworkControl;
        _character = character;
        _controller = controller;
    }

    public override void Reason()
    {
        if (_controller.IsTracked == false)
        {
            _character.GetComponent<AvatarController>().SetTransition(Transition.NetworkControlEnded);
        }
    }

    public override void Act()
    {
        //do nothing -> the kinect controls the rig now
    }

    public override void DoBeforeEntering()
    {
        _controller.UserControlParticleEffect.Stop();
        _controller.UserControlParticleEffect.Play();
       /* _controller.Animator.enabled = true;
        _controller.IsAnimationRunning = true;

        if (_character.transform.position.x < _targetPosition.x)
        {
            _character.transform.eulerAngles = new Vector3(_character.transform.rotation.eulerAngles.x, 90, _character.transform.rotation.eulerAngles.z);
        }
        else
        {
            _character.transform.eulerAngles = new Vector3(_character.transform.rotation.eulerAngles.x, -90, _character.transform.rotation.eulerAngles.z);
        }*/
    }

    public override void DoBeforeLeaving()
    {
        _controller.UserControlParticleEffect.Stop();
        _controller.UserControlParticleEffect.Play();
        /*_controller.Animator.enabled = false;
        _controller.IsAnimationRunning = false;
        _character.transform.eulerAngles = new Vector3(0, 0, 0);*/
    }
}