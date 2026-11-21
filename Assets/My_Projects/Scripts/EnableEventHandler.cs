using UnityEngine;
using UnityEngine.Events;

public class EnableEventHandler : MonoBehaviour
{
    public UnityEvent onEnableEvent;
    public UnityEvent onDisableEvent;

    private void OnEnable()
    {
        onEnableEvent?.Invoke();
    }

    private void OnDisable()
    {
        onDisableEvent?.Invoke();
    }

}
