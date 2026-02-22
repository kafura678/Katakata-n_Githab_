using UnityEngine;
using System;

public sealed class flowModeTransition
{
    private bool canFlowMode = false;
    public bool isFlowModeActive { get; private set; } = false;
    private Action flowModeEvent;
    public void setFlowModeEvent(Action flowModeEvent)
    {
        this.flowModeEvent += flowModeEvent;
    }
    public void tryFlowMode(float flowAmount)
    {
        if (canFlowMode) return; // すでにフローモードなら何もしない

        if (flowAmount >= 100f)
        {
            canFlowMode = true;
        }
    }

    public void tryFlowModeStart()
    {
        if (canFlowMode && Input.GetKey(KeyCode.A) && Input.GetKey(KeyCode.LeftShift))
        {
            flowModeEvent?.Invoke();

            isFlowModeActive = true;
            canFlowMode = false;
        }
    }
}
