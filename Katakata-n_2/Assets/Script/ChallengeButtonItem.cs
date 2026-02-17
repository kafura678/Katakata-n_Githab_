using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ChallengeButtonItem : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button button;
    [SerializeField] private Image background;

    [SerializeField] private Text textKeyCount; // キー数: min〜max（部分色付け）
    [SerializeField] private Text textKeys;     // 必要キー: A K（入力済みだけ緑）

    [Header("Selection Colors")]
    [SerializeField] private Color normalBgColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color selectedBgColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Header("Simul Highlight Colors")]
    [SerializeField] private Color excludedBgColor = new Color(0.12f, 0.12f, 0.12f, 1f); // 同時打ちに追加できない色

    [Header("Spawn Reveal")]
    [Tooltip("出現演出用の覆い（Prefab子のCover）※アンカー/ピボットで方向が変わる")]
    [SerializeField] private RectTransform coverRect;

    [Tooltip("出現演出が完了するまでクリック不可にする")]
    [SerializeField] private CanvasGroup canvasGroup;

    private int index;
    private System.Action<int> onClick;
    private Challenge challenge;

    private Coroutine revealCo;

    // 出現中など「同時選択に追加できるか」判定（button.interactable を基準）
    public bool IsSimulSelectable => (button != null && button.interactable);

    public void Setup(int index, Challenge c, System.Action<int> onClick)
    {
        this.index = index;
        this.challenge = c;
        this.onClick = onClick;

        if (textKeyCount != null) textKeyCount.text = $"キー数: {c.minCount}〜{c.maxCount}";
        if (textKeys != null) textKeys.text = $"必要キー: {FormatKeysPlain(c.requiredKeys)}";

        if (background != null) background.color = normalBgColor;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => this.onClick?.Invoke(this.index));
        }

        SetInteractable(true);
        HideCover();
    }

    // 選択色（最優先）
    public void SetSelected(bool selected)
    {
        if (background == null) return;
        background.color = selected ? selectedBgColor : normalBgColor;
    }

    // 除外色（選択中以外で使う想定）
    public void SetExcluded(bool excluded)
    {
        if (background == null) return;
        background.color = excluded ? excludedBgColor : normalBgColor;
    }

    public void UpdateProgress(int nowCount, HashSet<char> typedSet)
    {
        if (challenge == null) return;

        if (textKeyCount != null)
        {
            int min = challenge.minCount;
            int max = challenge.maxCount;

            // 下限：未達なら赤、達成なら緑
            string minStr = (nowCount < min) ? $"<color=red>{min}</color>"
                                             : $"<color=green>{min}</color>";

            // 上限：超過なら赤、範囲内なら緑
            string maxStr = (nowCount > max) ? $"<color=red>{max}</color>"
                                             : $"<color=green>{max}</color>";

            textKeyCount.text = $"キー数: {minStr}〜{maxStr}";
        }

        if (textKeys != null)
        {
            textKeys.text = $"必要キー: {FormatKeysColored(challenge.requiredKeys, typedSet)}";
        }
    }

    public void ResetProgress()
    {
        if (challenge == null) return;

        if (textKeyCount != null) textKeyCount.text = $"キー数: {challenge.minCount}〜{challenge.maxCount}";
        if (textKeys != null) textKeys.text = $"必要キー: {FormatKeysPlain(challenge.requiredKeys)}";
    }

    // =========================
    // 出現演出
    // =========================
    public void BeginReveal(float duration)
    {
        if (revealCo != null) StopCoroutine(revealCo);
        revealCo = StartCoroutine(CoReveal(duration));
    }

    private IEnumerator CoReveal(float duration)
    {
        // 100%になるまで選択できない
        SetInteractable(false);

        if (coverRect == null)
        {
            SetInteractable(true);
            yield break;
        }

        coverRect.gameObject.SetActive(true);

        var selfRect = transform as RectTransform;
        float fullW = selfRect != null ? selfRect.rect.width : 0f;
        if (fullW <= 0f) fullW = 300f;

        SetCoverWidth(fullW);

        if (duration <= 0f)
        {
            SetCoverWidth(0f);
            HideCover();
            SetInteractable(true);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / duration);
            float w = Mathf.Lerp(fullW, 0f, a);
            SetCoverWidth(w);
            yield return null;
        }

        SetCoverWidth(0f);
        HideCover();
        SetInteractable(true);
        revealCo = null;
    }

    private void SetCoverWidth(float width)
    {
        if (coverRect == null) return;
        var size = coverRect.sizeDelta;
        size.x = width;
        coverRect.sizeDelta = size;
    }

    private void HideCover()
    {
        if (coverRect != null)
            coverRect.gameObject.SetActive(false);
    }

    private void SetInteractable(bool on)
    {
        if (button != null) button.interactable = on;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = on;
    }

    // =========================
    // 表示整形
    // =========================
    private string FormatKeysPlain(List<char> keys)
    {
        if (keys == null || keys.Count == 0) return "なし";
        return string.Join(" ", keys);
    }

    private string FormatKeysColored(List<char> keys, HashSet<char> typedSet)
    {
        if (keys == null || keys.Count == 0) return "なし";

        var sb = new StringBuilder();
        for (int i = 0; i < keys.Count; i++)
        {
            char k = char.ToUpperInvariant(keys[i]);
            bool done = typedSet != null && typedSet.Contains(k);

            if (done) sb.Append($"<color=green>{k}</color>");
            else sb.Append(k);

            if (i < keys.Count - 1) sb.Append(" ");
        }
        return sb.ToString();
    }
}