using UnityEngine;

public sealed class flowModeTime
{
    private float flowTime = 10f; // フローモードの持続時間（秒）
    private float timeLeft = 0f; // フローモードの残り時間

    public void SetFlowTime(float seconds)
    {
        flowTime = Mathf.Max(0f, seconds);
    }
    public void StartFlowMode()
    {
        timeLeft = flowTime;
    }

    public void UpdateFlowMode(float deltaTime)
    {
        if (timeLeft > 0f)
        {
            timeLeft -= deltaTime;
        }
    }

    public float GetFlowTimeLeft()
    {
        return Mathf.Max(0f, timeLeft);
    }
}
