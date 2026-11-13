using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// the rules every creature state must follow
// (the StateMachine only knows about this interface, so it can run any state without knowing what it does)
public interface IState
{
    // the state's name (shown in the debug label and the Console logs)
    string Name { get; }

    // called once when the state starts (e.g. set the speed, pick a target)
    void Enter();

    // called every frame while the state is active (e.g. check the senses, move)
    void Tick();

    // called once when the state ends (e.g. clean up, reset values)
    void Exit();
}
