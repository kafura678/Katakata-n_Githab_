using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChallengeButtonItem : MonoBehaviour
{
    // ===============================
    // 参照
    // ===============================
    [Header("参照")]
    [SerializeField] private Button button;
    [SerializeField] private Image background;

    [Header("表示テキスト（UI.Text）")]
    [SerializeField] private Text textKeyCount;     // キー数：10〜15
    [SerializeField] private Text textRequired;     // 必要キー：A B C

    [Header("カバー（Reveal用）")]
    [SerializeField] private RectTransform cover;   // 上に被せるImage

    // ===============================
    // 色設定
    // ===============================
    [Header("色設定")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.6f, 1f, 0.6f, 1f);
    [SerializeField] private Color excludedColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    // ===============================
    // 内部状態
    // ===============================
    private bool isSelected;
    private bool isExcluded;
    private Challenge challengeData;

    public bool IsSimulSelectable { get; private set; } = true;

    // ===============================
    // 初期化
    // ===============================
    void Reset()
    {
        button = GetComponent<Button>();
        background = GetComponent<Image>();
    }

    public void Setup(int index, Challenge data, System.Action<int> onClick)
    {
        challengeData = data;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(index));
        }

        UpdateBaseText();
        ApplyVisual();
    }

    private void UpdateBaseText()
    {
        if (challengeData == null) return;

        if (textKeyCount != null)
            textKeyCount.text = $"キー数：{challengeData.minCount}〜{challengeData.maxCount}";

        if (textRequired != null)
            textRequired.text = $"必要キー：{string.Join(" ", challengeData.requiredKeys)}";
    }

    // ===============================
    // 状態管理
    // ===============================
    public void SetSelected(bool value)
    {
        isSelected = value;
        ApplyVisual();
    }

    public void SetExcluded(bool value)
    {
        isExcluded = value;

        if (button != null)
            button.interactable = !isExcluded;

        ApplyVisual();
    }

    public void SetSimulSelectable(bool value)
    {
        IsSimulSelectable = value;
        SetExcluded(!value);
    }

    private void ApplyVisual()
    {
        if (background == null) return;

        if (isExcluded)
            background.color = excludedColor;
        else if (isSelected)
            background.color = selectedColor;
        else
            background.color = normalColor;
    }

    // ===============================
    // GameManager互換（空でOK）
    // ===============================
    public void UpdateProgress(int currentCount, HashSet<char> typedSet, bool isInputting)
    {
        if (challengeData == null) return;

        // ===============================
        // キー数表示（下限〜上限）
        // ===============================
        if (textKeyCount != null)
        {
            int min = challengeData.minCount;
            int max = challengeData.maxCount;

            // 下限判定
            string minColor = (currentCount >= min) ? "green" : "red";

            // 上限判定
            string maxColor = (currentCount <= max) ? "green" : "red";

            textKeyCount.text =
                $"キー数：<color={minColor}>{min}</color>〜<color={maxColor}>{max}</color>";
        }

        // ===============================
        // 必要キー表示
        // ===============================
        if (textRequired != null)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("必要キー：");

            foreach (char k in challengeData.requiredKeys)
            {
                char u = char.ToUpperInvariant(k);

                bool contains = typedSet.Contains(u);

                if (contains)
                    sb.Append($"<color=green>{u}</color> ");
                else
                    sb.Append($"<color=red>{u}</color> ");
            }

            textRequired.text = sb.ToString();
        }
    }

    public void ResetProgress()
    {
        if (challengeData == null) return;

        // キー数を通常表示に戻す
        if (textKeyCount != null)
        {
            textKeyCount.text =
                $"キー数：{challengeData.minCount}〜{challengeData.maxCount}";
        }

        // 必要キーを通常表示に戻す
        if (textRequired != null)
        {
            textRequired.text =
                $"必要キー：{string.Join(" ", challengeData.requiredKeys)}";
        }
    }

    // ===============================
    // カバー方式 Reveal
    // ===============================
    public void BeginReveal(float duration)
    {
        StartCoroutine(RevealRoutine(duration));
    }

    private IEnumerator RevealRoutine(float duration)
    {
        if (cover == null)
            yield break;

        float t = 0f;

        // クリック防止
        if (button != null)
            button.interactable = false;

        float fullWidth = cover.rect.width;

        // 最初は全面カバー
        cover.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fullWidth);

        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / duration);

            float width = fullWidth * (1f - a);
            cover.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

            yield return null;
        }

        // 完全に開く
        cover.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);

        if (button != null)
            button.interactable = true;
    }
}