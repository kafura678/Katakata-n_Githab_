using System;
using UnityEngine;

public class PauseManager : MonoBehaviour
{
    // ===== 全体ポーズ状態（ゲーム側が参照する）=====
    public static bool IsPaused { get; private set; }

    // （任意）全体ポーズ変更通知が欲しい場合
    public static event Action<bool> OnPauseChanged;

    [Header("参照（制限時間だけ止める用）")]
    [SerializeField] private GameTimer gameTimer;

    [Header("初期状態")]
    [SerializeField] private bool startPaused = false;

    void Awake()
    {
        SetPaused(startPaused);
    }

    // ==============================
    // 全体ポーズ
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
    // 制限時間だけ停止/再開
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

    // （任意）コード側から参照を差し替えたい時
    public void SetGameTimer(GameTimer timer)
    {
        gameTimer = timer;
    }
}