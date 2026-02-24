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
    [SerializeField] private GameTimer gameTimer;                 // ★制限時間（分離）
    [SerializeField] private EnemyUISuppressionView enemyView;
    [SerializeField] private flowModeManager flowModeManager; // ★フローモード管理（分離）

    [Header("敵")]
    [SerializeField] private EnemySystem enemy;
    [SerializeField] private Text textEnemySuppression;           // 制圧率：12.3%

    [Header("フロー")]
    [SerializeField] private FlowSystem flow;
    [SerializeField] private Text textFlowPercent;                // FLOW：12.3%

    [Header("SE")]
    [SerializeField] private AudioSource seSource;
    [SerializeField] private AudioClip seSuccess;
    [SerializeField] private AudioClip seFail;
    [SerializeField, Range(0f, 1f)] private float seVolume = 1f;

    // ==============================
    // UI
    // ==============================
    [Header("UI")]
    [SerializeField] private Text textCount;                      // 入力数
    [SerializeField] private Text textTyped;                      // 入力履歴（RichText推奨）
    [SerializeField] private Text textStatus;                     // 状態表示

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
    [Tooltip("送信成功／失敗などの表示時間（秒）")]
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
    [Tooltip("お題ボタン出現演出の時間（秒）")]
    [SerializeField] private float challengeRevealSeconds = 0.35f;

    [Header("時間切れ表示")]
    [SerializeField] private string timeUpMessage = "時間切れ";

    // ==============================
    // 内部状態
    // ==============================
    private readonly List<Challenge> challenges = new List<Challenge>();
    private readonly List<ChallengeButtonItem> challengeButtons = new List<ChallengeButtonItem>();

    // 複数選択（Shift同時打ち）
    private readonly HashSet<int> selectedSet = new HashSet<int>();

    // 状態固定表示（成功/失敗/選択不可/時間切れ など）
    private string forcedStatusText = null;
    private Color forcedStatusColor = Color.white;
    private float forcedStatusUntil = 0f;

    //フローモード中に成功したお題の数
    private int flowModeSuccessCount = 0;

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
        ClearSelection();
        RefreshUI();

        //フローモード遷移のイベント登録
        if (flowModeManager != null)
        {
            flowModeManager.setOnFlowModeTransitioned(GenerateChallenges);
            flowModeManager.setOnFlowModeTransitioned(BuildChallengeListUI);
            flowModeManager.setOnFlowModeTransitioned(ClearSelection);
            flowModeManager.setOnFlowModeTransitioned(RefreshUI);
        }
    }

    void Update()
    {
        // ===== タイムアップ後は停止（制限時間は GameTimer 側）=====
        if (gameTimer != null && gameTimer.IsTimeUp)
        {
            inputBuffer.AcceptInput = false;
            inputBuffer.ClearAll();
            ClearSelection();
            RefreshUI();
            return;
        }

        // ===== フローモード送信 =====
        if (flowModeStatus.state == flowModeState.isFlowSendModeActive)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                successExection();
                flowModeSuccessCount--;
                if (flowModeSuccessCount <= 0)
                {
                    flowModeManager.OnFlowModeDeactivate();
                }
            }

            RefreshUI();
            return;
        }

        // ===== 入力更新 =====
        inputBuffer.Tick();

        // ===== フローモード中は常に成功扱い（ただし送信しない） =====
        if (flowModeStatus.state == flowModeState.isFlowModeActive)
        {
            if (IsSendableForAllSelected())
            {
                challengeRefresh();
                flowModeSuccessCount++;

                RefreshUI();
                return;
            }
        }

        // 選択が無ければ送信しない
        if (selectedSet.Count == 0)
        {
            RefreshUI();
            return;
        }

        // ===== Enter送信 =====
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            bool ok = IsSendableForAllSelected();

            if (ok)
            {
                OnSendSuccess();
            }
            else
            {
                OnSendFail();
            }
        }

        RefreshUI();
    }

    // ==============================
    // GameTimer から呼ぶ（UnityEvent推奨）
    // ==============================
    public void HandleTimeUp()
    {
        inputBuffer.AcceptInput = false;
        inputBuffer.ClearAll();

        ClearSelection();
        ForceStatus(timeUpMessage, statusFailColor);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        RefreshUI();
    }

    // ==============================
    // 送信処理
    // ==============================
    private void OnSendSuccess()
    {
        successExection();
        challengeRefresh();
    }

    private void successExection()
    {
        ForceStatus("送信成功", statusSuccessColor);
        PlaySE(seSuccess);

        int k = selectedSet.Count;

        float dmgMult = CalcSimulMultiplier(k, damageBonusPerAdditional, maxDamageSimulMultiplier);
        float flowMult = CalcSimulMultiplier(k, flowBonusPerAdditional, maxFlowSimulMultiplier);

        if (enemy != null)
            enemy.ApplyDamage(damagePerSuccess * dmgMult);

        enemyView?.AnimateOneSend();

        if (flow != null)
        {
            float elapsed = hasStartedTiming ? (Time.time - challengeStartTime) : 0f;
            float timeMult = CalcTimeMultiplier(elapsed);
            flow.Add(flowGainOnSuccess * timeMult * flowMult);
        }
    }

    private void challengeRefresh()
    {
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

    private void OnSendFail()
    {
        ForceStatus("送信失敗", statusFailColor);
        PlaySE(seFail);

        if (flow != null)
            flow.Sub(flowLossOnFail);

        inputBuffer.ClearAll();
    }

    // ==============================
    // お題生成・UI生成
    // ==============================
    private void GenerateChallenges()
    {
        challenges.Clear();
        for (int i = 0; i < challengeSlots; i++)
        {
            challenges.Add(ChallengeGenerator.Create());

            if (flowModeStatus.state == flowModeState.isFlowModeActive)
            {
                // フローモード中は必要キーなしにする
                var c = challenges[i];
                c.requiredKeys.Clear();
            }
        }
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
            challengeButtons.Add(item);

            // ★開始時も出現演出をする場合（不要なら削除）
            item.BeginReveal(challengeRevealSeconds);
        }
    }

    private void OnClickChallenge(int index)
    {
        // 時間切れなら無視
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
            if (index < 0 || index >= challengeButtons.Count) return;

            var btn = challengeButtons[index];

            // ① 出現中などで選択不可
            if (btn == null || !btn.IsSimulSelectable)
            {
                ForceStatus("選択不可", statusFailColor);
                PlaySE(seFail);
                return;
            }

            // ② レンジ不一致（キー数が同時に満たせない）
            if (selectedSet.Count > 0)
            {
                GetSelectedRangeIntersection(out int selMin, out int selMax);
                if (!IsSimulSelectableByRange(index, selMin, selMax))
                {
                    ForceStatus("選択不可", statusFailColor);
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
        challengeStartTime = Time.time;
        hasStartedTiming = true;
    }

    private void ClearSelection()
    {
        selectedSet.Clear();
        inputBuffer.AcceptInput = false;

        // 見た目解除
        for (int i = 0; i < challengeButtons.Count; i++)
        {
            challengeButtons[i].SetSelected(false);
            challengeButtons[i].SetExcluded(false);
        }

        if (flowModeStatus.state == flowModeState.isFlowModeActive)
        {
            //フローモード中はランダムに選択
            int idx = Random.Range(0, challenges.Count);
            selectedSet.Add(idx);
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
        bool timeUp = (gameTimer != null && gameTimer.IsTimeUp);

        inputBuffer.AcceptInput = (!timeUp && selectedSet.Count > 0);

        if (enemy != null && textEnemySuppression != null)
            textEnemySuppression.text = $"制圧率：{enemy.SuppressionPercent:F1}%";

        if (flow != null && textFlowPercent != null)
            textFlowPercent.text = $"FLOW：{flow.Percent:F1}%";

        // 状態（文字＋色）
        if (textStatus != null)
        {
            textStatus.text = BuildStatusText();

            if (!string.IsNullOrEmpty(forcedStatusText) && Time.time < forcedStatusUntil)
                textStatus.color = forcedStatusColor;
            else
                textStatus.color = statusNormalColor;
        }

        if (textCount != null)
            textCount.text = $"入力数: {inputBuffer.LetterCount}";

        // 未選択時
        if (selectedSet.Count == 0)
        {
            if (textTyped != null)
                textTyped.text = "入力履歴:";

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

        // 進捗更新用
        var typedSet = new HashSet<char>(inputBuffer.TypedChars);
        int currentCount = inputBuffer.LetterCount;
        bool isInputting = currentCount > 0;

        // 選択中レンジの共通部分
        GetSelectedRangeIntersection(out int selMin, out int selMax);

        // ボタン更新（選択色の上書き防止：sel==true には SetExcluded を呼ばない）
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
                btn.ResetProgress();
            }
            else
            {
                btn.UpdateProgress(currentCount, typedSet, isInputting);
            }
        }
    }

    private string BuildStatusText()
    {
        if (!string.IsNullOrEmpty(forcedStatusText) && Time.time < forcedStatusUntil)
            return forcedStatusText;

        forcedStatusText = null;

        bool timeUp = (gameTimer != null && gameTimer.IsTimeUp);
        if (timeUp) return timeUpMessage;

        if (selectedSet.Count == 0) return "選択中";
        if (inputBuffer.LetterCount == 0) return "待機中";
        return "入力中";
    }

    private void ForceStatus(string statusText, Color color)
    {
        forcedStatusText = statusText;
        forcedStatusColor = color;
        forcedStatusUntil = Time.time + sendResultHoldSeconds;
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
        var list = new List<int>(selectedSet);
        list.Sort();

        foreach (int index in list)
        {
            if (index < 0 || index >= challengeButtons.Count) continue;

            // お題更新
            challenges[index] = ChallengeGenerator.Create();
            if (flowModeStatus.state == flowModeState.isFlowModeActive)
            {
                // フローモード中は必要キーなしにする
                var c = challenges[index];
                c.requiredKeys.Clear();
            }

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

            // 100%生成されるまで選択不可（ChallengeButtonItem側で管理）
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