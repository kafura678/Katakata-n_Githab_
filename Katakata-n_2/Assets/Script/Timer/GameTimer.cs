using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(MultiTimer))]
public class GameTimer : MonoBehaviour
{
    [Header("表示")]
    [SerializeField] private Text textTime; // UGUI(Text)
    [SerializeField] private string prefix = "残り時間：";

    [Header("時間切れ")]
    [SerializeField] private UnityEvent onTimeUp;

    private MultiTimer timer;
    private bool firedTimeUp = false;

    public bool IsTimeUp => (timer != null && timer.IsFinished);
    public float TimeLeft => (timer != null ? timer.TimeLeft : 0f);
    public bool LocalPaused => (timer != null && timer.LocalPaused);

    void Awake()
    {
        timer = GetComponent<MultiTimer>();
    }

    void Update()
    {
        if (timer == null) return;

        // 表示更新（ポーズ中でも表示したいので常に更新）
        if (textTime != null)
            textTime.text = prefix + FormatTime(timer.TimeLeft);

        // 時間切れイベント（1回だけ）
        if (!firedTimeUp && timer.IsFinished)
        {
            firedTimeUp = true;
            onTimeUp?.Invoke();
        }
    }

    // ==============================
    // 外部操作
    // ==============================
    public void SetLocalPaused(bool paused)
    {
        if (timer == null) return;
        timer.SetLocalPaused(paused);
    }

    public void ToggleLocalPaused()
    {
        if (timer == null) return;
        timer.ToggleLocalPaused();
    }

    public void ResetAndStart()
    {
        if (timer == null) return;
        firedTimeUp = false;
        timer.ResetTimer();
        timer.StartTimer();
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

        // 1分未満は 59.9 表示
        return $"{seconds:F1}";
    }
}