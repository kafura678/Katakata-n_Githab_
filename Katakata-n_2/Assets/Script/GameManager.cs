using System;
using System.Collections.Generic;
using System.Text;
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

    [Header("敵")]
    [SerializeField] private EnemyManager enemyManager;
    [SerializeField] private EnemyUISuppressionView enemyView; // 送信時のPulse等（任意）
    [SerializeField] private Text textEnemySuppression; // 制圧率：12.3%

    [Header("フロー")]
    [SerializeField] private FlowSystem flow;
    [SerializeField] private Text textFlowPercent; // FLOW：12.3%

    [Header("SE")]
    [SerializeField] private AudioSource seSource;
    [SerializeField] private AudioClip seSuccess;
    [SerializeField] private AudioClip seFail;
    [SerializeField, Range(0f, 1f)] private float seVolume = 1f;

    // ==============================
    // UI
    // ==============================
    [Header("UI")]
    [SerializeField] private Text textCount;   // 入力数
    [SerializeField] private Text textTyped;   // 入力履歴（RichText ON推奨）
    [SerializeField] private Text textStatus;  // 状態表示
    [SerializeField] private Text textTime;    // 残り時間（GameTimerが別表示なら未使用でもOK）

    [Header("状態表示の色")]
    [SerializeField] private Color statusNormalColor = Color.white;
    [SerializeField] private Color statusSuccessColor = Color.green;
    [SerializeField] private Color statusFailColor = Color.red;

    [Header("状態表示の固定時間（秒）")]
    [SerializeField] private float sendResultHoldSeconds = 1.0f;

    // ==============================
    // お題ボタン
    // ==============================
    [Header("お題ボタン")]
    [SerializeField] private Transform challengeListParent;
    [SerializeField] private ChallengeButtonItem challengeButtonPrefab;

    [Header("調整：お題スロット数（3×2なら6）")]
    [SerializeField] private int challengeSlots = 6;

    [Header("演出：お題ボタン生成")]
    [Tooltip("お題ボタンの出現演出時間（BeginRevealに渡す秒数）")]
    [SerializeField] private float challengeRevealSeconds = 0.35f;

    // ==============================
    // 調整：敵ダメージ / フロー増減
    // ==============================
    [Header("調整：敵ダメージ / フロー増減")]
    [Tooltip("送信成功1回で敵に与えるダメージ（同時打ち倍率の前）")]
    [SerializeField] private float damagePerSuccess = 10f;

    [Tooltip("送信成功時のフロー増加量（時間倍率・同時打ち倍率の前）")]
    [SerializeField] private float flowGainOnSuccess = 10f;

    [Tooltip("送信失敗時のフロー減少量")]
    [SerializeField] private float flowLossOnFail = 5f;

    // ==============================
    // 調整：入力時間による倍率
    // ==============================
    [Header("調整：入力時間による倍率")]
    [Tooltip("ONの場合、お題を選択した瞬間から入力時間を計測")]
    [SerializeField] private bool startTimerOnSelect = true;

    [Tooltip("この秒数以下なら最速入力扱い")]
    [SerializeField] private float fastTimeSeconds = 2.0f;

    [Tooltip("この秒数以上なら最遅入力扱い")]
    [SerializeField] private float slowTimeSeconds = 8.0f;

    [Tooltip("入力が遅いときの倍率下限")]
    [SerializeField] private float minTimeMultiplier = 0.5f;

    [Tooltip("入力が速いときの倍率上限")]
    [SerializeField] private float maxTimeMultiplier = 2.0f;

    // ==============================
    // 調整：同時打ちボーナス
    // ==============================
    [Header("調整：同時打ちボーナス")]
    [Tooltip("追加1個ごとにダメージ倍率が増える量（例:0.25=+25%）")]
    [SerializeField] private float damageBonusPerAdditional = 0.25f;

    [Tooltip("追加1個ごとにFLOW倍率が増える量（例:0.25=+25%）")]
    [SerializeField] private float flowBonusPerAdditional = 0.25f;

    [Tooltip("ダメージ倍率の上限（暴走防止）")]
    [SerializeField] private float maxDamageSimulMultiplier = 5.0f;

    [Tooltip("FLOW倍率の上限（暴走防止）")]
    [SerializeField] private float maxFlowSimulMultiplier = 5.0f;

    // ==============================
    // 内部状態
    // ==============================
    private readonly List<Challenge> challenges = new List<Challenge>();
    private readonly List<ChallengeButtonItem> challengeButtons = new List<ChallengeButtonItem>();

    // 複数選択（Shift同時打ち）
    private readonly HashSet<int> selectedSet = new HashSet<int>();

    // 状態固定表示（成功/失敗など）
    private string forcedStatusText = null;
    private Color forcedStatusColor = Color.white;
    private float forcedStatusUntil = 0f;

    // 入力時間計測
    private float challengeStartTime = 0f;
    private bool hasStartedTiming = false;

    // タイムアップ
    private bool isTimeUp = false;

    // ==============================
    // 状態（テキスト生成を1か所へ）
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

        GenerateChallenges();
        BuildChallengeListUI();
        ClearSelectionVisualOnly();
        RefreshUI();
    }

    void Update()
    {
        // ---- タイムアップ後は停止（UI更新だけ） ----
        if (isTimeUp)
        {
            inputBuffer.AcceptInput = false;
            RefreshUI();
            return;
        }

        // ---- 妨害中（ウィンドウがある等）なら、他操作は全部無効化 ----
        // 妨害側がEnterを使うので、GameManager側はEnterを絶対に処理しない
        if (interferenceManager != null && interferenceManager.IsBlockingInput)
        {
            inputBuffer.AcceptInput = false;
            ForceStatusAuto(Status.Interference, statusFailColor);
            RefreshUI();
            return;
        }

        // ---- ポーズ（全体停止）中の挙動は任意：ここでは入力を止めるだけ ----
        if (PauseManager.IsPaused)
        {
            inputBuffer.AcceptInput = false;
            RefreshUI();
            return;
        }

        // ---- 入力更新 ----
        inputBuffer.AcceptInput = (selectedSet.Count > 0);
        inputBuffer.Tick();

        // ---- Enter送信（選択が無ければ送らない） ----
        if (selectedSet.Count > 0 &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            TrySend();
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

        inputBuffer.AcceptInput = false;
        inputBuffer.ClearAll();

        ClearSelection();
        ForceStatusAuto(Status.TimeUp, statusFailColor);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    // ==============================
    // 送信処理
    // ==============================
    private void TrySend()
    {
        bool ok = IsSendableForAllSelected();

        if (ok)
        {
            ForceStatusAuto(Status.Success, statusSuccessColor);
            PlaySE(seSuccess);

            int k = selectedSet.Count;

            float dmgMult = CalcSimulMultiplier(k, damageBonusPerAdditional, maxDamageSimulMultiplier);
            float flowMult = CalcSimulMultiplier(k, flowBonusPerAdditional, maxFlowSimulMultiplier);

            // 敵へダメージ（現在ターゲットへ）
            if (enemyManager != null)
                enemyManager.ApplyDamageToCurrent(damagePerSuccess * dmgMult);

            // 送信演出（任意）
            enemyView?.AnimateOneSend();

            // フロー増加（時間倍率×同時打ち倍率）
            if (flow != null)
            {
                float elapsed = hasStartedTiming ? (Time.unscaledTime - challengeStartTime) : 0f;
                float timeMult = CalcTimeMultiplier(elapsed);
                flow.Add(flowGainOnSuccess * timeMult * flowMult);
            }

            // 成功したお題を置換（出現演出で選択不可→可）
            ReplaceSelectedChallengesWithReveal();

            // 入力・計測リセット
            inputBuffer.ClearAll();
            hasStartedTiming = false;

            // 成功後は選び直し
            ClearSelection();

            // EnterのUI事故防止
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
        else
        {
            ForceStatusAuto(Status.Fail, statusFailColor);
            PlaySE(seFail);

            if (flow != null)
                flow.Sub(flowLossOnFail);

            inputBuffer.ClearAll();

            // 失敗時は「選択は維持」したいなら ClearSelection() はしない
        }
    }

    // ==============================
    // お題生成・UI生成
    // ==============================
    private void GenerateChallenges()
    {
        challenges.Clear();
        for (int i = 0; i < challengeSlots; i++)
            challenges.Add(ChallengeGenerator.Create());
    }

    private void BuildChallengeListUI()
    {
        for (int i = challengeListParent.childCount - 1; i >= 0; i--)
            Destroy(challengeListParent.GetChild(i).gameObject);

        challengeButtons.Clear();

        for (int i = 0; i < challenges.Count; i++)
        {
            var item = Instantiate(challengeButtonPrefab, challengeListParent);
            item.Setup(i, challenges[i], OnClickChallenge);
            item.SetSelected(false);
            item.SetExcluded(false);
            item.ResetProgress();

            challengeButtons.Add(item);

            // ★開始時にも出現演出を動かしたい場合
            item.BeginReveal(challengeRevealSeconds);
        }
    }

    private void OnClickChallenge(int index)
    {
        if (isTimeUp) return;
        if (PauseManager.IsPaused) return;
        if (interferenceManager != null && interferenceManager.IsBlockingInput) return;

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (!shift)
        {
            // 単独選択
            selectedSet.Clear();
            selectedSet.Add(index);

            inputBuffer.ClearAll();
            StartTimingIfNeeded();

            ClearForcedStatus();

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            RefreshUI();
            return;
        }

        // Shift：同時選択トグル（追加時に可否チェック）
        if (selectedSet.Contains(index))
        {
            selectedSet.Remove(index);
        }
        else
        {
            // 出現中などで選択不可
            if (index < 0 || index >= challengeButtons.Count) return;

            var btn = challengeButtons[index];
            if (btn == null || !btn.IsSimulSelectable)
            {
                ForceStatusAuto(Status.SimulNotAllowed, statusFailColor);
                PlaySE(seFail);
                return;
            }

            // レンジ不一致（キー数が同時に満たせない）
            if (selectedSet.Count > 0)
            {
                GetSelectedRangeIntersection(out int selMin, out int selMax);
                if (!IsSimulSelectableByRange(index, selMin, selMax))
                {
                    ForceStatusAuto(Status.SimulNotAllowed, statusFailColor);
                    PlaySE(seFail);
                    return;
                }
            }

            bool wasEmpty = (selectedSet.Count == 0);
            selectedSet.Add(index);

            if (wasEmpty)
            {
                inputBuffer.ClearAll();
                StartTimingIfNeeded();
            }
        }

        // 0件になったら入力不可
        if (selectedSet.Count == 0)
        {
            inputBuffer.ClearAll();
            hasStartedTiming = false;
        }

        ClearForcedStatus();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        RefreshUI();
    }

    private void StartTimingIfNeeded()
    {
        if (!startTimerOnSelect) return;

        challengeStartTime = Time.unscaledTime;
        hasStartedTiming = true;
    }

    private void ClearSelection()
    {
        selectedSet.Clear();
        inputBuffer.AcceptInput = false;

        ClearSelectionVisualOnly();
    }

    private void ClearSelectionVisualOnly()
    {
        for (int i = 0; i < challengeButtons.Count; i++)
        {
            if (challengeButtons[i] == null) continue;
            challengeButtons[i].SetSelected(false);
            challengeButtons[i].SetExcluded(false);
            challengeButtons[i].ResetProgress();
        }
    }

    private void ClearForcedStatus()
    {
        forcedStatusText = null;
        forcedStatusUntil = 0f;
    }

    // ==============================
    // UI更新
    // ==============================
    private void RefreshUI()
    {
        // 入力受付（妨害/ポーズ/タイムアップはUpdate側で止める）
        inputBuffer.AcceptInput = (!isTimeUp &&
                                  (interferenceManager == null || !interferenceManager.IsBlockingInput) &&
                                  !PauseManager.IsPaused &&
                                  selectedSet.Count > 0);

        // 残り時間表示（GameTimerがTextを持っているなら未使用でもOK）
        if (textTime != null && gameTimer != null)
            textTime.text = "残り時間：" + FormatTime(gameTimer.TimeLeft);

        // 敵・フロー表示
        if (enemyManager != null && textEnemySuppression != null)
        {
            // EnemyManager側に「現在ターゲットの制圧率」表示を持たせるならそちらに寄せてもOK
            // ここでは EnemyUISuppressionView 等が別で表示しているケースが多いので未使用でも可
        }

        if (flow != null && textFlowPercent != null)
            textFlowPercent.text = $"FLOW：{flow.Percent:F1}%";

        // 入力数
        if (textCount != null)
            textCount.text = $"入力数: {inputBuffer.LetterCount}";

        // 状態（文字＋色）
        if (textStatus != null)
        {
            Status st = GetCurrentStatus();
            textStatus.text = BuildStatusText(st);

            // 成功/失敗などの固定表示が有効ならその色
            if (!string.IsNullOrEmpty(forcedStatusText) && Time.unscaledTime < forcedStatusUntil)
                textStatus.color = forcedStatusColor;
            else
                textStatus.color = statusNormalColor;
        }

        // 未選択時：履歴/進捗をリセット
        if (selectedSet.Count == 0)
        {
            if (textTyped != null)
                textTyped.text = "入力履歴:";

            for (int i = 0; i < challengeButtons.Count; i++)
            {
                if (challengeButtons[i] == null) continue;
                challengeButtons[i].SetSelected(false);
                challengeButtons[i].SetExcluded(false);
                challengeButtons[i].ResetProgress();
            }
            return;
        }

        // 入力履歴（必要キーの最初の1回だけ緑）
        if (textTyped != null)
            textTyped.text = BuildTypedHistoryColoredTextForSelected();

        // ボタン進捗更新
        var typedSet = new HashSet<char>(inputBuffer.TypedChars);
        int now = inputBuffer.LetterCount;
        bool isInputting = (now > 0);

        // 選択中レンジの共通部分
        GetSelectedRangeIntersection(out int selMin, out int selMax);

        for (int i = 0; i < challengeButtons.Count; i++)
        {
            var btn = challengeButtons[i];
            if (btn == null) continue;

            bool sel = selectedSet.Contains(i);
            btn.SetSelected(sel);

            // ★選択色の上書き防止：sel==true のボタンには SetExcluded を呼ばない
            if (!sel)
            {
                bool interactable = btn.IsSimulSelectable;
                bool rangeOk = IsSimulSelectableByRange(i, selMin, selMax);
                btn.SetExcluded((!interactable) || (!rangeOk));
                btn.ResetProgress();
            }
            else
            {
                // 選択中の進捗（キー数/必要キーの色更新はChallengeButtonItem側）
                btn.UpdateProgress(now, typedSet, isInputting);
            }
        }
    }

    private Status GetCurrentStatus()
    {
        // 固定表示優先（成功/失敗/時間切れ/妨害など）
        if (!string.IsNullOrEmpty(forcedStatusText) && Time.unscaledTime < forcedStatusUntil)
        {
            // forcedStatusText の内容に依存せず、表示は forcedStatusText を使う
            return Status.Selecting;
        }

        if (isTimeUp) return Status.TimeUp;

        if (interferenceManager != null && interferenceManager.IsBlockingInput)
            return Status.Interference;

        if (selectedSet.Count == 0)
            return Status.Selecting;

        if (inputBuffer.LetterCount == 0)
            return Status.Waiting;

        return Status.Inputting;
    }

    private string BuildStatusText(Status st)
    {
        // forced中は forcedStatusText を優先
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

        // 1分未満は 59.9 表示
        return $"{seconds:F1}";
    }

    // ==============================
    // 同時打ち「可否」：レンジ交差判定
    // ==============================
    private void GetSelectedRangeIntersection(out int selMin, out int selMax)
    {
        selMin = int.MinValue;
        selMax = int.MaxValue;

        bool first = true;

        foreach (int idx in selectedSet)
        {
            if (idx < 0 || idx >= challenges.Count) continue;
            var c = challenges[idx];

            if (first)
            {
                selMin = c.minCount;
                selMax = c.maxCount;
                first = false;
            }
            else
            {
                if (c.minCount > selMin) selMin = c.minCount;
                if (c.maxCount < selMax) selMax = c.maxCount;
            }
        }
    }

    private bool IsSimulSelectableByRange(int otherIndex, int selMin, int selMax)
    {
        if (otherIndex < 0 || otherIndex >= challenges.Count) return false;
        var other = challenges[otherIndex];

        // overlap: other.min <= selMax AND other.max >= selMin
        return (other.minCount <= selMax) && (other.maxCount >= selMin);
    }

    // ==============================
    // 送信判定（全選択を満たす）
    // ==============================
    private bool IsSendableForAllSelected()
    {
        foreach (int idx in selectedSet)
        {
            if (idx < 0 || idx >= challenges.Count) return false;
            if (!IsSendableNow(challenges[idx])) return false;
        }
        return true;
    }

    private bool IsSendableNow(Challenge c)
    {
        int n = inputBuffer.LetterCount;

        if (n < c.minCount || n > c.maxCount) return false;
        if (!inputBuffer.ContainsAllRequired(c.requiredKeys)) return false;

        return true;
    }

    // ==============================
    // 倍率計算
    // ==============================
    private float CalcTimeMultiplier(float elapsed)
    {
        float fast = Mathf.Max(0.01f, fastTimeSeconds);
        float slow = Mathf.Max(fast + 0.01f, slowTimeSeconds);

        float k = Mathf.InverseLerp(slow, fast, elapsed); // 速いほど1に近い
        float m = Mathf.Lerp(minTimeMultiplier, maxTimeMultiplier, k);
        return Mathf.Clamp(m, minTimeMultiplier, maxTimeMultiplier);
    }

    private float CalcSimulMultiplier(int selectedCount, float bonusPerAdditional, float maxMul)
    {
        int add = Mathf.Max(0, selectedCount - 1);
        float mul = 1f + add * Mathf.Max(0f, bonusPerAdditional);
        return Mathf.Min(mul, Mathf.Max(1f, maxMul));
    }

    // ==============================
    // 成功お題を置換（消して出現）
    // ==============================
    private void ReplaceSelectedChallengesWithReveal()
    {
        var list = new List<int>(selectedSet);
        list.Sort();

        foreach (int index in list)
        {
            if (index < 0 || index >= challengeButtons.Count) continue;

            // 新お題生成
            challenges[index] = ChallengeGenerator.Create();

            // 旧ボタン破棄 → 新ボタン生成
            var oldItem = challengeButtons[index];
            int sibling = oldItem != null ? oldItem.transform.GetSiblingIndex() : index;

            if (oldItem != null)
                Destroy(oldItem.gameObject);

            var newItem = Instantiate(challengeButtonPrefab, challengeListParent);
            newItem.transform.SetSiblingIndex(sibling);

            newItem.Setup(index, challenges[index], OnClickChallenge);
            newItem.SetSelected(false);
            newItem.SetExcluded(false);
            newItem.ResetProgress();

            challengeButtons[index] = newItem;

            // 100%生成されるまで選択不可（ChallengeButtonItem側で制御）
            newItem.BeginReveal(challengeRevealSeconds);
        }
    }

    // ==============================
    // 入力履歴（必要キーの最初の1回だけ緑）
    // ==============================
    private string BuildTypedHistoryColoredTextForSelected()
    {
        if (inputBuffer.TypedChars.Count == 0)
            return "入力履歴:";

        // 選択中お題の必要キーの集合
        var requiredSet = new HashSet<char>();
        foreach (int idx in selectedSet)
        {
            if (idx < 0 || idx >= challenges.Count) continue;
            var keys = challenges[idx].requiredKeys;
            if (keys == null) continue;

            foreach (var k in keys)
                requiredSet.Add(char.ToUpperInvariant(k));
        }

        var consumed = new HashSet<char>();
        var sb = new StringBuilder("入力履歴: ");

        foreach (char ch in inputBuffer.TypedChars)
        {
            char u = char.ToUpperInvariant(ch);

            if (!requiredSet.Contains(u))
            {
                sb.Append(u);
            }
            else if (!consumed.Contains(u))
            {
                sb.Append($"<color=green>{u}</color>");
                consumed.Add(u);
            }
            else
            {
                sb.Append(u);
            }
        }

        return sb.ToString();
    }

    // ==============================
    // SE
    // ==============================
    private void PlaySE(AudioClip clip)
    {
        if (clip == null) return;

        if (seSource == null)
            seSource = GetComponent<AudioSource>();

        if (seSource == null) return;

        seSource.PlayOneShot(clip, seVolume);
    }
}