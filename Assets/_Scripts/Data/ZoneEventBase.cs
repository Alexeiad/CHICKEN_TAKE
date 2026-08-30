using UnityEngine;
using UnityEngine.Events;

public abstract class ZoneEventBase : ScriptableObject
{
    public UnityAction OnEnter;
    public UnityAction OnExit;

    public void Enter() => OnEnter?.Invoke();
    public void Exit() => OnExit?.Invoke();
}