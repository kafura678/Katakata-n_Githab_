using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

public class ChallengeButtonItem : MonoBehaviour, IPointerClickHandler
{
    // ==============================
    // UI
    // ==============================
    [Header("UI")]
    [SerializeField] private Text textMinCount;
    [SerializeField] private Image background;

    [Header("Cover（カーテン）")]
    [SerializeField] private RectTransform coverRect;
    [SerializeField] private Image coverImage;

    [Header("色")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.7f, 1f, 0.7f);
    [SerializeField] private Color readyColor = new Color(0.5f, 1f, 1f);

    // ==============================
    // 内部
    // ==============================
    private Challenge challenge;
    private int index;

    private Action<int> onClick;
    private Action<int> onDoubleClick;

    private bool isSelected = false;
    private bool isRevealing = false;

    private float revealDuration = 0f;
    private float revealTimer = 0f;

    private float fullWidth = 0f;

    private float lastClickTime = 0f;
    private const float doubleClickThreshold = 0.3f;

    // ==============================
    // 公開
    // ==============================
    public bool IsSimulSelectable => !isRevealing;

    // ==============================
    // 初期化
    // ==============================
    public void Setup(int idx, Challenge c, Action<int> clickCallback)
    {
        index = idx;
        challenge = c;
        onClick = clickCallback;

        if (textMinCount != null)
            textMinCount.text = $"最低入力数：{challenge.minInputCount}以上";

        SetSelected(false);
        ResetProgress();
    }

    public void SetDoubleClick(Action<int> doubleClickCallback)
    {
        onDoubleClick = doubleClickCallback;
    }

    // ==============================
    // Reveal開始
    // ==============================
    public void BeginReveal(float duration)
    {
        if (coverRect == null)
            return;

        revealDuration = Mathf.Max(0.01f, duration);
        revealTimer = 0f;
        isRevealing = true;

        fullWidth = coverRect.rect.width;

        if (coverImage != null)
            coverImage.raycastTarget = true;

        coverRect.gameObject.SetActive(true);
    }

    void Update()
    {
        if (!isRevealing) return;

        revealTimer += Time.unscaledDeltaTime;

        float t = Mathf.Clamp01(revealTimer / revealDuration);

        float width = Mathf.Lerp(fullWidth, 0f, t);
        coverRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

        if (t >= 1f)
        {
            isRevealing = false;
            coverRect.gameObject.SetActive(false);

            if (coverImage != null)
                coverImage.raycastTarget = false;
        }
    }

    // ==============================
    // クリック
    // ==============================
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsSimulSelectable)
            return;

        float time = Time.unscaledTime;

        if (time - lastClickTime <= doubleClickThreshold)
        {
            onDoubleClick?.Invoke(index);
            lastClickTime = 0f;
            return;
        }

        lastClickTime = time;
        onClick?.Invoke(index);
    }

    // ==============================
    // 選択
    // ==============================
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateVisualState();
    }

    public void SetExcluded(bool excluded)
    {
        // 新仕様では使用しない
    }

    public void ResetProgress()
    {
        UpdateVisualState();
    }

    public void UpdateProgress(int currentCount, System.Collections.Generic.HashSet<char> typedSet, bool isInputting)
    {
        if (challenge == null) return;
        if (!isSelected) return;

        bool ready = currentCount >= challenge.minInputCount;

        if (background == null) return;

        background.color = ready ? readyColor : selectedColor;
    }

    private void UpdateVisualState()
    {
        if (background == null) return;

        if (!isSelected)
            background.color = normalColor;
        else
            background.color = selectedColor;
    }
}