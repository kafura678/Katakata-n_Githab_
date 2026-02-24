using UnityEngine;
using System;

public class flowModeManager : MonoBehaviour
{
    [Header("フローモードの持続時間")]
    [SerializeField] private float flowModeDuration = 10f;

    [Header("参照")]
    [SerializeField] private PauseManager pauseManager;
    [SerializeField] private FlowSystem flowSystem;

    private Action onFlowModeTransitioned;
    private flowModeTransition transition;
    private flowModeTime time;
    void Awake()
    {
        transition = new flowModeTransition();
        time = new flowModeTime();

        time.SetFlowTime(flowModeDuration);
        transition.setFlowModeActivateEvent(OnFlowModeActivated);
        transition.setFlowModeDeactivateEvent(OnFlowModeDeactivated);
    }

    void Update()
    {
        if (flowModeStatus.state == flowModeState.normal)
        {
            transition.tryFlowMode(flowSystem.Percent);
        }
        else if (flowModeStatus.state == flowModeState.canFlowMode)
        {
            transition.tryFlowModeStart();
        }
        else if (flowModeStatus.state == flowModeState.isFlowModeActive)
        {
            time.UpdateFlowMode(Time.deltaTime);
            if (time.GetFlowTimeLeft() <= 0f)
                transition.flowSendModeStart();
        }
    }

    private void OnFlowModeActivated()
    {
        //フローモード制限時間設定
        time.StartFlowMode();

        //ゲーム制限時間を停止
        pauseManager.PauseGameTimer();

        //フローモード遷移イベント
        onFlowModeTransitioned?.Invoke();
    }

    private void OnFlowModeDeactivated()
    {
        //ゲーム制限時間を再開
        pauseManager.ResumeGameTimer();

        //フローモード遷移イベント
        onFlowModeTransitioned?.Invoke();
    }

    public void OnFlowModeDeactivate()
    {
        transition.flowModeDeactivated();
    }

    public void setOnFlowModeTransitioned(Action onFlowModeTransitioned)
    {
        this.onFlowModeTransitioned += onFlowModeTransitioned;
    }
}
