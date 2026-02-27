using UnityEngine;
using UnityEngine.UI;

public class ErrorWindowUI : MonoBehaviour
{
    [Header("表示（UGUI Text）")]
    [SerializeField] private Text timeText;
    [SerializeField] private string prefix = "残り時間：";

    private GameTimer timer;

    public void SetGameTimer(GameTimer gameTimer)
    {
        timer = gameTimer;
        RefreshTimeText();
    }

    void Update()
    {
        RefreshTimeText();
    }

    private void RefreshTimeText()
    {
        if (timeText == null || timer == null) return;
        timeText.text = prefix + FormatTime(timer.TimeLeft);
    }

    // ★ 常に小数第1位（例：12.3）
    private string FormatTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        return seconds.ToString("F1");
    }

    public void Close()
    {
        Destroy(gameObject);
    }
}