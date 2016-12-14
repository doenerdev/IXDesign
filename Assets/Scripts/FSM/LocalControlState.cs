using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LocalControlState : FSMState
{
    private GameObject _character;
    private AvatarController _controller;

    public LocalControlState(GameObject character, AvatarController controller)
    {
        stateID = StateID.LocalControl;
        _character = character;
        _controller = controller;
    }

    public override void Reason()
    {
        if (_controller.IsTracked == false)
        {
            _character.GetComponent<AvatarController>().SetTransition(Transition.LocalControlEnded);
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
    }
    

    public override void DoBeforeLeaving()
    {
        _controller.UserControlParticleEffect.Stop();
        _controller.UserControlParticleEffect.Play();
        
    }

    
}