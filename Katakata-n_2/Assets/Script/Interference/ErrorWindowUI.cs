using UnityEngine;
using UnityEngine.UI;

public class ErrorWindowUI : MonoBehaviour
{
    [Header("表示（UGUI Text）")]
    [SerializeField] private Text timeText;
    [SerializeField] private string prefix = "残り時間：";

    private GameTimer timer;

    // Interference側から注入する
    public void SetGameTimer(GameTimer gameTimer)
    {
        timer = gameTimer;
        RefreshTimeText();
    }

    void Update()
    {
        // 常に更新（妨害中でも見せたい）
        RefreshTimeText();
    }

    private void RefreshTimeText()
    {
        if (timeText == null || timer == null) return;
        timeText.text = prefix + FormatTime(timer.TimeLeft);
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

    public void Close()
    {
        Destroy(gameObject);
    }
}