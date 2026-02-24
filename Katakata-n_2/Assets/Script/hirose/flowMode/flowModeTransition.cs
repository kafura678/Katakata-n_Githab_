using UnityEngine;
using System;

public sealed class flowModeTransition
{
    private Action flowModeActivateEvent;
    private Action flowModeDeactivateEvent;
    void Awake()
    {
        flowModeStatus.state = flowModeState.normal;
    }
    public void setFlowModeActivateEvent(Action flowModeActivateEvent)
    {
        this.flowModeActivateEvent += flowModeActivateEvent;
    }
    public void setFlowModeDeactivateEvent(Action flowModeDeactivateEvent)
    {
        this.flowModeDeactivateEvent += flowModeDeactivateEvent;
    }
    public void tryFlowMode(float flowAmount)
    {
        if (flowModeStatus.state == flowModeState.canFlowMode) return; // すでにフローモードなら何もしない

        if (flowAmount >= 100f)
        {
            flowModeStatus.state = flowModeState.canFlowMode;
        }
    }

    public void tryFlowModeStart()
    {
        if (flowModeStatus.state == flowModeState.canFlowMode && Input.GetKey(KeyCode.A) && Input.GetKey(KeyCode.LeftShift))
        {
            flowModeStatus.state = flowModeState.isFlowModeActive;
            flowModeActivateEvent?.Invoke();
        }
    }

    public void flowSendModeStart()
    {
        flowModeStatus.state = flowModeState.isFlowSendModeActive;
    }

    public void flowModeDeactivated()
    {
        flowModeDeactivateEvent?.Invoke();
        flowModeStatus.state = flowModeState.normal;
    }
}
