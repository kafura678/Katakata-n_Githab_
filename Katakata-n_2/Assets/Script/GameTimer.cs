using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class GameTimer : MonoBehaviour
{
    [Header("制限時間")]
    [SerializeField] private float timeLimitSeconds = 180f;

    [Header("表示")]
    [SerializeField] private Text textTime;
    [SerializeField] private string prefix = "残り時間：";

    [Header("時間切れ")]
    [SerializeField] private UnityEvent onTimeUp;

    [Header("一時停止")]
    [SerializeField] private bool isPaused = false;   // ★InspectorでON/OFF可能

    private float timeLeft;
    private bool isTimeUp;

    public bool IsPaused => isPaused;
    public bool IsTimeUp => isTimeUp;

    void Start()
    {
        ResetTimer();
        UpdateText();
    }

    void Update()
    {
        // ★ bool判定のみで停止制御
        if (isPaused)
        {
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = 1f;
        }

        if (isTimeUp || isPaused) return;

        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            isTimeUp = true;
            onTimeUp?.Invoke();
        }

        UpdateText();
    }

    public void SetPaused(bool pause)
    {
        isPaused = pause;
    }

    public void ResetTimer()
    {
        timeLeft = Mathf.Max(0f, timeLimitSeconds);
        isTimeUp = false;
    }

    private void UpdateText()
    {
        if (textTime == null) return;

        textTime.text = $"{prefix}{FormatTime(timeLeft)}";
    }

    private string FormatTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);

        if (seconds >= 60f)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return $"{m}:{s:00}";
        }

        return $"{seconds:F1}";
    }

    void OnDestroy()
    {
        Time.timeScale = 1f; // 念のため戻す
    }
}