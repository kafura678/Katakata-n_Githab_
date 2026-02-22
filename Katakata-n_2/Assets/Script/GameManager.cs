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

    [Header("敵（UI Image侵食）")]
    [SerializeField] private EnemyManager enemyManager;
    [SerializeField] private EnemyUISuppressionView enemyView;
    [SerializeField] private Text textEnemySuppression; // 制圧率：12.3%

    [Header("フロー")]
    [SerializeField] private FlowSystem flow;
    [SerializeField] private Text textFlowPercent; // FLOW：12.3%

    [Header("制限時間（GameTimer）")]
    [SerializeField] private GameTimer gameTimer;
    [SerializeField] private Text textTime; // GameTimer側で表示しているなら未使用でもOK

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

    [Header("お題ボタン")]
    [SerializeField] private Transform challengeListParent;
    [SerializeField] private ChallengeButtonItem challengeButtonPrefab;

    // ==============================
    // 調整（日本語）
    // ==============================
    [Header("調整：お題")]
    [Tooltip("同時に表示するお題の数（3×2なら6）")]
    [SerializeField] private int challengeSlots = 6;

    [Header("調整：状態表示")]
    [Tooltip("送信成功／失敗の表示時間（秒）")]
    [SerializeField] private float sendResultHoldSeconds = 1.0f;

    [Header("調整：状態表示の色")]
    [SerializeField] private Color statusNormalColor = Color.white;
    [SerializeField] private Color statusSuccessColor = Color.green;
    [SerializeField] private Color statusFailColor = Color.red;

    [Header("調整：敵ダメージ / フロー増減")]
    [Tooltip("送信成功1回で敵に与えるダメージ（同時打ち倍率の前）")]
    [SerializeField] private float damagePerSuccess = 10f;

    [Tooltip("送信成功時のフロー増加量（時間倍率・同時打ち倍率の前）")]
    [SerializeField] private float flowGainOnSuccess = 10f;

    [Tooltip("送信失敗時のフロー減少量")]
    [SerializeField] private float flowLossOnFail = 5f;

    [Header("調整：入力時間によるフロー倍率")]
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

    [Header("調整：同時打ち（複数お題成功）ボーナス")]
    [Tooltip("追加1個ごとにダメージ倍率が増える量（例:0.25=+25%）")]
    [SerializeField] private float damageBonusPerAdditional = 0.25f;

    [Tooltip("追加1個ごとにFLOW倍率が増える量（例:0.25=+25%）")]
    [SerializeField] private float flowBonusPerAdditional = 0.25f;

    [Tooltip("ダメージ倍率の上限（暴走防止）")]
    [SerializeField] private float maxDamageSimulMultiplier = 5.0f;

    [Tooltip("FLOW倍率の上限（暴走防止）")]
    [SerializeField] private float maxFlowSimulMultiplier = 5.0f;

    [Header("演出：お題ボタン生成")]
    [Tooltip("お題ボタンが出現する演出の時間（秒）")]
    [SerializeField] private float challengeRevealSeconds = 0.35f;

    // ==============================
    // 内部状態
    // ==============================
    private readonly List<Challenge> challenges = new List<Challenge>();
    private readonly List<ChallengeButtonItem> challengeButtons = new List<ChallengeButtonItem>();

    // 複数選択（Shift同時打ち）
    private readonly HashSet<int> selectedSet = new HashSet<int>();

    // 状態固定表示
    private string forcedStatusText = null;
    private Color forcedStatusColor = Color.white;
    private float forcedStatusUntil = 0f;

    // 入力時間計測
    private float challengeStartTime = 0f;
    private bool hasStartedTiming = false;

    // ==============================
    // Unity
    // ==============================
    void Start()
    {
        GenerateChallenges();
        BuildChallengeListUI();

        ClearSelectionVisualOnly();
        ClearSelectionLogic();
        RefreshUI();
    }

    void Update()
    {
        // 全体ポーズ（必要なら）
        if (PauseManager.IsPaused)
            return;

        // 時間切れ（GameTimer側で管理）
        if (gameTimer != null && gameTimer.IsTimeUp)
        {
            ForceStatus("時間切れ", statusFailColor);
            inputBuffer.AcceptInput = false;
            RefreshUI();
            return;
        }

        // 入力更新
        inputBuffer.Tick();

        // 選択がなければ送信しない
        if (selectedSet.Count == 0)
        {
            RefreshUI();
            return;
        }

        // Enter送信
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            bool ok = IsSendableForAllSelected();

            if (ok)
            {
                // =========================
                // 成功
                // =========================
                ForceStatus("送信成功", statusSuccessColor);
                PlaySE(seSuccess);

                int k = selectedSet.Count;

                // 同時打ち倍率
                float dmgMult = CalcSimulMultiplier(k, damageBonusPerAdditional, maxDamageSimulMultiplier);
                float flowMult = CalcSimulMultiplier(k, flowBonusPerAdditional, maxFlowSimulMultiplier);

                // 敵へダメージ（★ここが重要：EnemyManagerへ）
                if (enemyManager != null)
                {
                    float dmg = damagePerSuccess * dmgMult;
                    enemyManager.ApplyDamageToCurrent(dmg);
                }

                // 侵食演出（Pulse）
                enemyView?.AnimateOneSend();

                // FLOW増加（時間倍率×同時打ち倍率）
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
                ClearSelectionLogic();
                ClearSelectionVisualOnly();

                // EnterのUI事故防止
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
            }
            else
            {
                // =========================
                // 失敗
                // =========================
                ForceStatus("送信失敗", statusFailColor);
                PlaySE(seFail);

                if (flow != null)
                    flow.Sub(flowLossOnFail);

                inputBuffer.ClearAll();
            }
        }

        RefreshUI();
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
        // 既存削除
        for (int i = challengeListParent.childCount - 1; i >= 0; i--)
            Destroy(challengeListParent.GetChild(i).gameObject);

        challengeButtons.Clear();

        for (int i = 0; i < challenges.Count; i++)
        {
            var item = Instantiate(challengeButtonPrefab, challengeListParent);
            item.Setup(i, challenges[i], OnClickChallenge);

            // 初期状態
            item.SetSelected(false);
            item.SetExcluded(false);

            // 初回表示でも出現演出（必要なら）
            // ※ ChallengeButtonItemに BeginReveal がある前提
            item.BeginReveal(challengeRevealSeconds);

            challengeButtons.Add(item);
        }
    }

    // ==============================
    // クリック（単独 / Shift同時選択）
    // ==============================
    private void OnClickChallenge(int index)
    {
        // 時間切れ中は不可
        if (gameTimer != null && gameTimer.IsTimeUp) return;

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

        // Shift：同時選択トグル
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
                ForceStatus("同時打ち不可", statusFailColor);
                PlaySE(seFail);
                return;
            }

            // レンジ不一致（キー数が同時に満たせない）
            if (selectedSet.Count > 0)
            {
                GetSelectedRangeIntersection(out int selMin, out int selMax);
                if (!IsSimulSelectableByRange(index, selMin, selMax))
                {
                    ForceStatus("同時打ち不可", statusFailColor);
                    PlaySE(seFail);
                    return;
                }
            }

            // 追加OK
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

    // ==============================
    // 選択解除（ロジック/見た目）
    // ==============================
    private void ClearSelectionLogic()
    {
        selectedSet.Clear();
        inputBuffer.AcceptInput = false;
    }

    private void ClearSelectionVisualOnly()
    {
        for (int i = 0; i < challengeButtons.Count; i++)
        {
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
        // 入力受付
        inputBuffer.AcceptInput = ((gameTimer == null || !gameTimer.IsTimeUp) && selectedSet.Count > 0);

        // 制圧率表示（任意）
        if (enemyManager != null && textEnemySuppression != null && enemyManager.CurrentTarget != null)
        {
            // EnemyUnit側は「現在値%」を返している想定
            // 表示はターゲットの値を例示（必要なら総合%に変更）
            textEnemySuppression.text = $"制圧率：{enemyManager.CurrentTarget.SuppressionPercent:F1}%";
        }

        // Flow表示
        if (flow != null && textFlowPercent != null)
            textFlowPercent.text = $"FLOW：{flow.Percent:F1}%";

        // 状態（文字＋色）
        if (textStatus != null)
        {
            textStatus.text = BuildStatusText();

            if (!string.IsNullOrEmpty(forcedStatusText) && Time.unscaledTime < forcedStatusUntil)
                textStatus.color = forcedStatusColor;
            else
                textStatus.color = statusNormalColor;
        }

        // 入力数
        if (textCount != null)
            textCount.text = $"入力数: {inputBuffer.LetterCount}";

        // 未選択時
        if (selectedSet.Count == 0)
        {
            if (textTyped != null)
                textTyped.text = "入力履歴:";

            // 選択色などを完全リセット
            for (int i = 0; i < challengeButtons.Count; i++)
            {
                challengeButtons[i].SetSelected(false);
                challengeButtons[i].SetExcluded(false);
                challengeButtons[i].ResetProgress();
            }
            return;
        }

        // 入力履歴
        if (textTyped != null)
            textTyped.text = BuildTypedHistoryColoredTextForSelected();

        var typedSet = new HashSet<char>(inputBuffer.TypedChars);
        int nowCount = inputBuffer.LetterCount;
        bool isInputting = nowCount > 0;

        // 選択中レンジの共通部分
        GetSelectedRangeIntersection(out int selMin, out int selMax);

        // ボタン更新
        for (int i = 0; i < challengeButtons.Count; i++)
        {
            bool sel = selectedSet.Contains(i);
            var btn = challengeButtons[i];

            btn.SetSelected(sel);

            if (!sel)
            {
                bool interactable = btn.IsSimulSelectable;
                bool rangeOk = IsSimulSelectableByRange(i, selMin, selMax);

                bool excluded = (!interactable) || (!rangeOk);
                btn.SetExcluded(excluded);
            }

            // ★ 必須：今の入力数/入力中を渡す
            if (sel) btn.UpdateProgress(nowCount, typedSet, isInputting);
            else btn.ResetProgress();
        }
    }

    private string BuildStatusText()
    {
        // 強制表示が優先
        if (!string.IsNullOrEmpty(forcedStatusText) && Time.unscaledTime < forcedStatusUntil)
            return forcedStatusText;

        forcedStatusText = null;

        if (gameTimer != null && gameTimer.IsTimeUp) return "時間切れ";
        if (selectedSet.Count == 0) return "選択中";
        if (inputBuffer.LetterCount == 0) return "待機中";
        return "入力中";
    }

    private void ForceStatus(string statusText, Color color)
    {
        forcedStatusText = statusText;
        forcedStatusColor = color;
        forcedStatusUntil = Time.unscaledTime + sendResultHoldSeconds;
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

        // 文字数レンジ
        if (n < c.minCount || n > c.maxCount) return false;

        // 必要キー
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

        // elapsed が小さいほど 1 に近づく（速いほど上げる）
        float k = Mathf.InverseLerp(slow, fast, elapsed);
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
        // 選択中のindexを昇順で処理（見た目が安定）
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

        // 選択中お題の必要キーの「集合」
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

            // 必要キーでない文字：通常色
            if (!requiredSet.Contains(u))
            {
                sb.Append(u);
                continue;
            }

            // 必要キー：最初の1回だけ緑、それ以降は通常色
            if (!consumed.Contains(u))
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