using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WalkToOriginState : FSMState
{
    private GameObject _character;
    private AvatarController _controller;
    private bool _isNetworkCharacter;
    private Vector3 _targetPosition;

    public WalkToOriginState(GameObject character, AvatarController controller, bool isNetworkCharacter)
    {
        stateID = StateID.WalkingToOrigin;
        _character = character;
        _controller = controller;
        _isNetworkCharacter = isNetworkCharacter;

        if (_isNetworkCharacter)
        {
            _targetPosition = new Vector3(GameManager.Instance.NetworkCharacterOrigin.position.x,
                GameManager.Instance.YPlanePosition, GameManager.Instance.ZPlanePosition);
        }
        else
        {
            _targetPosition = new Vector3(GameManager.Instance.LocalCharacterOrigin.position.x,
                GameManager.Instance.YPlanePosition, GameManager.Instance.ZPlanePosition);
        }
    }

    public override void Reason()
    {
        if (_character.transform.position == _targetPosition)
        {
            _character.GetComponent<AvatarController>().SetTransition(Transition.ArrivedAtOrigin);
        }
    }

    public override void Act()
    {
        _character.transform.position = Vector3.MoveTowards(_character.transform.position, _targetPosition, _controller.OriginReturnSpeed);
    }

    public override void DoBeforeEntering()
    {
        //_controller.Animator.enabled = true;
        //_controller.IsAnimationRunning = true;

        //flag for the walk animation needs to be set here later

        /*if (_character.transform.position.x < _targetPosition.x)
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
        //_controller.Animator.enabled = false;
        //_controller.IsAnimationRunning = false;
       // _character.transform.eulerAngles = new Vector3(0, 0, 0);
    }

    private void SetOpacity(float from, float to, float t)
    {
        Renderer renderer = _controller.GetComponent<Renderer>();

        foreach (Material mat in renderer.materials)
        {
            //Material has to have Rendering Mode set to 'Fade'
            mat.color = new Vector4(mat.color.r, mat.color.g, mat.color.b, Mathf.SmoothStep(from, to, t));
        }
    }
}