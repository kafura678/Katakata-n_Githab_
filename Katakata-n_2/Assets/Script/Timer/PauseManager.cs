using System;
using UnityEngine;

public class PauseManager : MonoBehaviour
{
    // ==============================
    // 全体ポーズ
    // ==============================
    public static bool IsPaused { get; private set; }
    public static event Action<bool> OnPauseChanged;

    [Header("制限時間参照")]
    [SerializeField] private GameTimer gameTimer;

    [Header("初期状態")]
    [SerializeField] private bool startPaused = false;

    void Awake()
    {
        Time.timeScale = 1f; // 旧実装対策
        SetPaused(startPaused);
    }

    // ==============================
    // 全体ポーズ制御
    // ==============================
    public void SetPaused(bool paused)
    {
        if (IsPaused == paused) return;

        IsPaused = paused;
        OnPauseChanged?.Invoke(IsPaused);
    }

    public void TogglePause()
    {
        SetPaused(!IsPaused);
    }

    // ==============================
    // 制限時間だけ停止
    // ==============================
    public void PauseGameTimer()
    {
        if (gameTimer == null) return;
        gameTimer.SetLocalPaused(true);
    }

    public void ResumeGameTimer()
    {
        if (gameTimer == null) return;
        gameTimer.SetLocalPaused(false);
    }

    public void ToggleGameTimerPause()
    {
        if (gameTimer == null) return;
        gameTimer.ToggleLocalPaused();
    }

    public bool IsGameTimerPaused()
    {
        if (gameTimer == null) return false;
        return gameTimer.LocalPaused;
    }
}