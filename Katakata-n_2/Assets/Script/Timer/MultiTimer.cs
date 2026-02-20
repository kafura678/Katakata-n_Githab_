using UnityEngine;
using UnityEngine.Events;

public class MultiTimer : MonoBehaviour
{
    [Header("時間")]
    [SerializeField] private float durationSeconds = 10f;

    [Tooltip("Startで自動開始する")]
    [SerializeField] private bool autoStart = true;

    [Header("停止制御")]
    [Tooltip("全体ポーズ(PauseManager)の影響を受ける")]
    [SerializeField] private bool obeyGlobalPause = true;

    [Tooltip("このタイマー自身の一時停止（個別停止）")]
    [SerializeField] private bool localPaused = false;

    [Header("イベント")]
    [SerializeField] private UnityEvent onFinished;

    private float timeLeft;
    private bool isFinished;
    private bool isRunning;

    public float DurationSeconds => durationSeconds;
    public float TimeLeft => timeLeft;
    public bool IsFinished => isFinished;
    public bool IsRunning => isRunning;
    public bool LocalPaused => localPaused;

    void Start()
    {
        ResetTimer();
        if (autoStart) StartTimer();
        else StopTimer();
    }

    void Update()
    {
        if (!isRunning || isFinished) return;

        if (obeyGlobalPause && PauseManager.IsPaused) return;
        if (localPaused) return;

        // ★ Time.timeScale に影響されない時間
        timeLeft -= Time.unscaledDeltaTime;

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            isFinished = true;
            isRunning = false;
            onFinished?.Invoke();
        }
    }

    // ==============================
    // 操作API
    // ==============================
    public void ResetTimer()
    {
        timeLeft = Mathf.Max(0f, durationSeconds);
        isFinished = false;
    }

    public void StartTimer()
    {
        isRunning = true;
    }

    public void StopTimer()
    {
        isRunning = false;
    }

    public void SetLocalPaused(bool paused)
    {
        localPaused = paused;
    }

    public void ToggleLocalPaused()
    {
        localPaused = !localPaused;
    }

    public void SetDuration(float seconds, bool reset = true)
    {
        durationSeconds = Mathf.Max(0f, seconds);
        if (reset) ResetTimer();
    }
}