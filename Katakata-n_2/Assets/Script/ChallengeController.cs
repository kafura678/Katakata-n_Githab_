using System;
using System.Collections.Generic;
using UnityEngine;

public class ChallengeController : MonoBehaviour
{
    // ==============================
    // 参照
    // ==============================
    [Header("UI")]
    [SerializeField] private Transform challengeListParent;
    [SerializeField] private ChallengeButtonItem challengeButtonPrefab;

    [Header("設定")]
    [SerializeField] private int challengeSlots = 6;
    [SerializeField] private float revealSeconds = 0.35f;

    // ==============================
    // 内部データ
    // ==============================
    private readonly List<Challenge> challenges = new List<Challenge>();
    private readonly List<ChallengeButtonItem> buttons = new List<ChallengeButtonItem>();

    // 複数選択（Shift）
    private readonly HashSet<int> selectedSet = new HashSet<int>();

    // コールバック
    private Action<int> onClickFromButton;
    private Action<int> onDoubleClickFromButton;

    public event Action OnSelectionChanged;

    // ==============================
    // 公開
    // ==============================
    public bool HasSelection => selectedSet.Count > 0;
    public int SelectedCount => selectedSet.Count;

    /// <summary>
    /// 選択中すべてを満たすために必要な最低入力数（= 選択中のminInputCountの最大）
    /// </summary>
    public int RequiredMinForAllSelected
    {
        get
        {
            if (!HasSelection) return 0;

            int req = 0;
            foreach (int idx in selectedSet)
            {
                if (idx < 0 || idx >= challenges.Count) continue;
                req = Mathf.Max(req, challenges[idx].minInputCount);
            }
            return req;
        }
    }

    // ==============================
    // 初期化
    // ==============================
    public void Initialize(Action<int> onClick, Action<int> onDoubleClick)
    {
        onClickFromButton = onClick;
        onDoubleClickFromButton = onDoubleClick;

        GenerateChallenges();
        BuildUI();

        ClearSelection();
        RefreshButtons(0, new HashSet<char>(), false);
    }

    // ==============================
    // 生成
    // ==============================
    private void GenerateChallenges()
    {
        challenges.Clear();
        for (int i = 0; i < challengeSlots; i++)
            challenges.Add(ChallengeGenerator.Create());
    }

    private void BuildUI()
    {
        for (int i = challengeListParent.childCount - 1; i >= 0; i--)
            Destroy(challengeListParent.GetChild(i).gameObject);

        buttons.Clear();

        for (int i = 0; i < challenges.Count; i++)
        {
            var item = Instantiate(challengeButtonPrefab, challengeListParent);

            item.Setup(i, challenges[i], OnClickInternal);
            item.SetDoubleClick(OnDoubleClickInternal);

            item.SetSelected(false);
            item.SetExcluded(false);
            item.ResetProgress();

            buttons.Add(item);

            // 出現演出
            item.BeginReveal(revealSeconds);
        }
    }

    // ==============================
    // ボタンイベント（内部→GameManagerへ）
    // ==============================
    private void OnClickInternal(int index)
    {
        onClickFromButton?.Invoke(index);
    }

    private void OnDoubleClickInternal(int index)
    {
        onDoubleClickFromButton?.Invoke(index);
    }

    // ==============================
    // クリック処理（GameManagerから呼ばれる）
    // ==============================
    public bool HandleClick(int index, bool shift, out string failReason)
    {
        failReason = null;

        if (index < 0 || index >= buttons.Count)
            return false;

        var btn = buttons[index];
        if (btn == null || !btn.IsSimulSelectable)
        {
            failReason = "reveal";
            return false;
        }

        if (!shift)
        {
            // 単独選択
            selectedSet.Clear();
            selectedSet.Add(index);
            OnSelectionChanged?.Invoke();
            return true;
        }

        // Shift：トグル（新仕様ではレンジ制約が無いので、reveal中でなければ全部OK）
        if (selectedSet.Contains(index))
            selectedSet.Remove(index);
        else
            selectedSet.Add(index);

        OnSelectionChanged?.Invoke();
        return true;
    }

    // ==============================
    // 送信判定（全選択を満たす）
    // ==============================
    public bool IsSendableForAllSelected(InputBuffer buffer)
    {
        if (buffer == null) return false;
        if (!HasSelection) return false;

        int n = buffer.LetterCount;

        foreach (int idx in selectedSet)
        {
            if (idx < 0 || idx >= challenges.Count)
                return false;

            if (n < challenges[idx].minInputCount)
                return false;
        }

        return true;
    }

    // ==============================
    // 成功時：置換（出現演出）→ 選択解除
    // ==============================
    public void ReplaceSelectedWithReveal()
    {
        var list = new List<int>(selectedSet);
        list.Sort();

        foreach (int index in list)
        {
            if (index < 0 || index >= buttons.Count)
                continue;

            challenges[index] = ChallengeGenerator.Create();

            var oldItem = buttons[index];
            int sibling = oldItem != null ? oldItem.transform.GetSiblingIndex() : index;

            if (oldItem != null)
                Destroy(oldItem.gameObject);

            var newItem = Instantiate(challengeButtonPrefab, challengeListParent);
            newItem.transform.SetSiblingIndex(sibling);

            newItem.Setup(index, challenges[index], OnClickInternal);
            newItem.SetDoubleClick(OnDoubleClickInternal);

            newItem.SetSelected(false);
            newItem.SetExcluded(false);
            newItem.ResetProgress();

            buttons[index] = newItem;

            newItem.BeginReveal(revealSeconds);
        }

        ClearSelection();
    }

    // ==============================
    // 選択解除
    // ==============================
    public void ClearSelection()
    {
        selectedSet.Clear();

        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] == null) continue;
            buttons[i].SetSelected(false);
            buttons[i].SetExcluded(false);
            buttons[i].ResetProgress();
        }

        OnSelectionChanged?.Invoke();
    }

    // ==============================
    // 表示更新（GameManagerから呼ばれる）
    // ==============================
    public void RefreshButtons(int currentCount, HashSet<char> typedSet, bool isInputting)
    {
        // typedSet/isInputting は旧仕様互換（今は未使用でOK）
        for (int i = 0; i < buttons.Count; i++)
        {
            var btn = buttons[i];
            if (btn == null) continue;

            bool sel = selectedSet.Contains(i);
            btn.SetSelected(sel);

            // 新仕様：同時打ち不可の概念が無いので excluded は基本 false
            if (!sel)
            {
                btn.SetExcluded(false);
                btn.ResetProgress();
            }
            else
            {
                btn.UpdateProgress(currentCount, typedSet, isInputting);
            }
        }
    }
}