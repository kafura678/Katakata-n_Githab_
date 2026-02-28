using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GameManager : MonoBehaviour
{
    // ==============================
    // 参照
    // ==============================
    [Header("参照")]
    [SerializeField] private InputBuffer inputBuffer;

    [Header("妨害（Interference）")]
    [Tooltip("妨害中は他操作を無効化する")]
    [SerializeField] private InterferenceManager interferenceManager;

    [Header("制限時間（GameTimer）")]
    [SerializeField] private GameTimer gameTimer;

    [Header("ポーズ（任意）")]
    [SerializeField] private PauseManager pauseManager; // 参照が欲しい場合（なくてもOK）

    [Header("お題管理（分離）")]
    [SerializeField] private ChallengeController challengeController;

    [Header("送信処理（分離）")]
    [SerializeField] private SendController sendController;

    [Header("ターミナルウィンドウ（分離）")]
    [SerializeField] private TerminalWindowManager terminalWindowManager;

    [Header("フロー表示（任意）")]
    [SerializeField] private FlowSystem flow; // UI表示に必要なら参照（SendController側で更新される想定）

    // ==============================
    // UI
    // ==============================
    [Header("UI")]
    [SerializeField] private Text textCount;        // 入力数
    [SerializeField] private Text textStatus;       // 状態表示
    [SerializeField] private Text textTime;         // 残り時間（GameTimerが別表示なら未使用でもOK）
    [SerializeField] private Text textFlowPercent;  // FLOW：12.3%

    [Header("状態表示の色")]
    [SerializeField] private Color statusNormalColor = Color.white;
    [SerializeField] private Color statusSuccessColor = Color.green;
    [SerializeField] private Color statusFailColor = Color.red;

    [Header("状態表示の固定時間（秒）")]
    [SerializeField] private float sendResultHoldSeconds = 1.0f;

    // ==============================
    // 調整：入力時間計測
    // ==============================
    [Header("調整：入力時間計測")]
    [Tooltip("ONの場合、お題を選択した瞬間から入力時間を計測")]
    [SerializeField] private bool startTimerOnSelect = true;

    // ==============================
    // 内部状態
    // ==============================
    private bool isTimeUp = false;

    // 固定表示（成功/失敗/妨害/同時打ち不可など）
    private string forcedStatusText = null;
    private Color forcedStatusColor = Color.white;
    private float forcedStatusUntil = 0f;

    // 入力時間計測（SendControllerへ elapsed を渡す）
    private float challengeStartTime = 0f;
    private bool hasStartedTiming = false;

    // ターミナルへ「増えた入力数」だけ流す差分
    private int prevLetterCount = 0;

    // ==============================
    // 状態
    // ==============================
    private enum Status
    {
        Selecting,
        Waiting,
        Inputting,
        Success,
        Fail,
        Interference,
        TimeUp,
        SimulNotAllowed
    }

    // ==============================
    // Unity
    // ==============================
    void Start()
    {
        isTimeUp = false;

        // お題管理 初期化（ChallengeController 側でボタン生成）
        if (challengeController != null)
        {
            challengeController.Initialize(OnClickChallenge, OnDoubleClickChallenge);
            challengeController.OnSelectionChanged += OnSelectionChanged;
        }

        // 入力差分初期化
        prevLetterCount = (inputBuffer != null) ? inputBuffer.LetterCount : 0;

        RefreshUI();
    }

    void Update()
    {
        // ---- タイムアップ後は停止（UI更新だけ） ----
        if (isTimeUp)
        {
            if (inputBuffer != null) inputBuffer.AcceptInput = false;
            RefreshUI();
            return;
        }

        // ---- 妨害中は他操作無効（Enterも処理しない） ----
        if (interferenceManager != null && interferenceManager.IsBlockingInput)
        {
            if (inputBuffer != null) inputBuffer.AcceptInput = false;
            ForceStatusAuto(Status.Interference, statusFailColor);
            RefreshUI();
            return;
        }

        // ---- 全体ポーズ中（入力止める） ----
        if (PauseManager.IsPaused)
        {
            if (inputBuffer != null) inputBuffer.AcceptInput = false;
            RefreshUI();
            return;
        }

        // ---- 入力更新 ----
        bool hasSelection = (challengeController != null && challengeController.HasSelection);

        if (inputBuffer != null)
        {
            inputBuffer.AcceptInput = hasSelection;
            inputBuffer.Tick();
        }

        // ---- ターミナルへ「増えた入力数」だけ流す（ウィンドウが開いている時だけ） ----
        UpdateTerminalFromInputDelta();

        // ---- Enter送信 ----
        if (hasSelection &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            SendByController();
        }

        RefreshUI();
    }

    // ==============================
    // GameTimer から呼ぶ（UnityEventで登録）
    // ==============================
    public void OnTimeUp()
    {
        if (isTimeUp) return;

        isTimeUp = true;

        if (inputBuffer != null)
        {
            inputBuffer.AcceptInput = false;
            inputBuffer.ClearAll();
        }

        challengeController?.ClearSelection();

        ForceStatusAuto(Status.TimeUp, statusFailColor);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        prevLetterCount = 0;
        hasStartedTiming = false;
    }

    // ==============================
    // お題クリック（ChallengeController がボタンから呼ぶ）
    // ==============================
    private void OnClickChallenge(int index)
    {
        if (isTimeUp) return;
        if (PauseManager.IsPaused) return;
        if (interferenceManager != null && interferenceManager.IsBlockingInput) return;
        if (challengeController == null) return;

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        bool hadSelectionBefore = challengeController.HasSelection;

        if (!challengeController.HandleClick(index, shift, out string failReason))
        {
            ForceStatusAuto(Status.SimulNotAllowed, statusFailColor);
            sendController?.PlayFailSE(); // 同時打ち不可SE（SendController側にメソッド追加が必要）
            return;
        }

        bool hasSelectionAfter = challengeController.HasSelection;

        // 「選択が0→1になった瞬間」だけ時間計測開始（毎クリックでリセットしない）
        if (startTimerOnSelect && !hadSelectionBefore && hasSelectionAfter)
        {
            challengeStartTime = Time.unscaledTime;
            hasStartedTiming = true;
        }

        // 選択変更時の入力リセット（既存仕様）
        if (inputBuffer != null) inputBuffer.ClearAll();
        prevLetterCount = 0;

        // 成功/失敗などの固定表示は解除
        ClearForcedStatus();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        RefreshUI();
    }

    // ==============================
    // お題ダブルクリック → ターミナルウィンドウを開く
    // ==============================
    private void OnDoubleClickChallenge(int index)
    {
        if (isTimeUp) return;
        if (PauseManager.IsPaused) return;
        if (interferenceManager != null && interferenceManager.IsBlockingInput) return;

        terminalWindowManager?.Open();
    }

    private void OnSelectionChanged()
    {
        // 拡張ポイント（今は空）
    }

    // ==============================
    // 送信（SendControllerへ委譲）
    // ==============================
    private void SendByController()
    {
        if (sendController == null || inputBuffer == null || challengeController == null)
            return;

        float elapsed = hasStartedTiming ? (Time.unscaledTime - challengeStartTime) : 0f;

        var result = sendController.TrySend(inputBuffer, challengeController, elapsed);

        if (result == SendController.SendResult.Success)
        {
            ForceStatusAuto(Status.Success, statusSuccessColor);

            hasStartedTiming = false;
            prevLetterCount = 0;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
        else if (result == SendController.SendResult.Fail)
        {
            ForceStatusAuto(Status.Fail, statusFailColor);
            prevLetterCount = 0;
        }
    }

    // ==============================
    // ターミナル：入力増分だけ流す（ウィンドウがある時のみ）
    // ==============================
    private void UpdateTerminalFromInputDelta()
    {
        if (terminalWindowManager == null || !terminalWindowManager.HasWindow) return;
        if (inputBuffer == null) return;

        int now = inputBuffer.LetterCount;
        int delta = now - prevLetterCount;

        if (delta > 0)
            terminalWindowManager.AddCharacters(delta);

        prevLetterCount = now;
    }

    // ==============================
    // UI更新
    // ==============================
    private void RefreshUI()
    {
        // 入力受付（妨害/ポーズ/タイムアップはUpdate側で止める）
        if (inputBuffer != null)
        {
            inputBuffer.AcceptInput = (!isTimeUp &&
                                      (interferenceManager == null || !interferenceManager.IsBlockingInput) &&
                                      !PauseManager.IsPaused &&
                                      challengeController != null &&
                                      challengeController.HasSelection);
        }

        // 残り時間（GameTimerが別Text持ってるなら未使用でもOK）
        if (textTime != null && gameTimer != null)
            textTime.text = "残り時間：" + FormatTime(gameTimer.TimeLeft);

        // 入力数
        if (textCount != null && inputBuffer != null)
            textCount.text = $"入力数: {inputBuffer.LetterCount}";

        // FLOW表示（任意）
        if (flow != null && textFlowPercent != null)
            textFlowPercent.text = $"FLOW：{flow.Percent:F1}%";

        // 状態（文字＋色）
        if (textStatus != null)
        {
            Status st = GetCurrentStatus();
            textStatus.text = BuildStatusText(st);

            if (!string.IsNullOrEmpty(forcedStatusText) && Time.unscaledTime < forcedStatusUntil)
                textStatus.color = forcedStatusColor;
            else
                textStatus.color = statusNormalColor;
        }

        // お題ボタン表示更新（ChallengeControllerへ委譲）
        if (challengeController != null && inputBuffer != null)
        {
            var typedSet = new HashSet<char>(inputBuffer.TypedChars);
            int now = inputBuffer.LetterCount;
            bool isInputting = (now > 0);

            challengeController.RefreshButtons(now, typedSet, isInputting);
        }
    }

    private Status GetCurrentStatus()
    {
        // forced優先
        if (!string.IsNullOrEmpty(forcedStatusText) && Time.unscaledTime < forcedStatusUntil)
            return Status.Selecting;

        if (isTimeUp) return Status.TimeUp;

        if (interferenceManager != null && interferenceManager.IsBlockingInput)
            return Status.Interference;

        bool hasSelection = (challengeController != null && challengeController.HasSelection);
        if (!hasSelection) return Status.Selecting;

        if (inputBuffer == null || inputBuffer.LetterCount == 0) return Status.Waiting;

        return Status.Inputting;
    }

    private string BuildStatusText(Status st)
    {
        if (!string.IsNullOrEmpty(forcedStatusText) && Time.unscaledTime < forcedStatusUntil)
            return forcedStatusText;

        forcedStatusText = null;

        switch (st)
        {
            case Status.TimeUp: return "時間切れ";
            case Status.Interference: return "妨害中";
            case Status.Selecting: return "選択中";
            case Status.Waiting: return "待機中";
            case Status.Inputting: return "入力中";
            default: return "選択中";
        }
    }

    private void ForceStatusAuto(Status st, Color color)
    {
        forcedStatusText = BuildForcedText(st);
        forcedStatusColor = color;
        forcedStatusUntil = Time.unscaledTime + sendResultHoldSeconds;
    }

    private void ClearForcedStatus()
    {
        forcedStatusText = null;
        forcedStatusUntil = 0f;
    }

    private string BuildForcedText(Status st)
    {
        switch (st)
        {
            case Status.Success: return "送信成功";
            case Status.Fail: return "送信失敗";
            case Status.TimeUp: return "時間切れ";
            case Status.Interference: return "妨害中";
            case Status.SimulNotAllowed: return "同時打ち不可";
            default: return "選択中";
        }
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
}