using UnityEngine;

public class flowModeManager : MonoBehaviour
{
    [Header("フローモードの持続時間")]
    [SerializeField] private float flowModeDuration = 10f;

    [Header("参照")]
    [SerializeField] private PauseManager pauseManager;
    [SerializeField] private FlowSystem flowSystem;

    private flowModeTransition transition;
    private flowModeTime time;
    void Awake()
    {
        transition = new flowModeTransition();
        time = new flowModeTime();

        time.SetFlowTime(flowModeDuration);
        transition.setFlowModeEvent(OnFlowModeActivated);
    }

    void Update()
    {
        if (transition.isFlowModeActive)
        {
            time.UpdateFlowMode(Time.deltaTime);
        }
        else
        {
            transition.tryFlowMode(flowSystem.Percent);
            transition.tryFlowModeStart();
        }
    }

    private void OnFlowModeActivated()
    {
        //フローモード制限時間設定
        time.StartFlowMode();

        //ゲーム制限時間を停止
        pauseManager.PauseGameTimer();
    }
}
