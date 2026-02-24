using UnityEngine;
using System;
[Serializable]
public enum flowModeState
{
    normal,
    canFlowMode,
    isFlowModeActive,
    isFlowSendModeActive
}

public static class flowModeStatus
{
    public static flowModeState state;
}
