using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(RectTransform))]
public class EnemyUnit : MonoBehaviour, IPointerClickHandler
{
    public enum MissingCorner { LeftTop, LeftBottom, RightTop, RightBottom }

    [Header("侵食マテリアル（必須）")]
    [Tooltip("SuppressionRadialBlock.shader を使った Material をここに入れる（ImageのMaterial欄は空でOK）")]
    [SerializeField] private Material baseMaterial;

    [Header("制圧設定")]
    [SerializeField] private float maxSuppression = 100f;

    [Header("じわじわ速度（0〜1/秒）")]
    [SerializeField] private float maxFillSpeedPerSecond = 0.35f;

    [Header("全体ポーズ追従")]
    [SerializeField] private bool obeyGlobalPause = true;

    [Header("形状プリセット")]
    [SerializeField] private MissingCorner missingCorner = MissingCorner.LeftTop;

    [SerializeField, Range(0f, 0.5f)]
    private float cornerBias = 0.30f;

    [Header("クリック")]
    [SerializeField, Range(0f, 1f)]
    private float alphaHitThreshold = 0.1f;

    [Header("デバッグ")]
    [SerializeField] private bool debugPingPong = false;   // ONで強制アニメ（動作確認）
    [SerializeField] private bool debugLogOnce = true;

    private float currentSuppression = 0f;
    private float target01 = 0f;
    private float shown01 = 0f;

    private Material runtimeMaterial;
    private RectTransform rectTransform;
    private Image image;
    private EnemyManager enemyManager;

    public float SuppressionPercent => currentSuppression;
    public bool IsSuppressed => currentSuppression >= maxSuppression;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();

        // 透明部分クリック無効
        image.alphaHitTestMinimumThreshold = alphaHitThreshold;

        enemyManager = FindObjectOfType<EnemyManager>();

        // ★ Materialを必ず個別化（baseMaterial → runtimeMaterial）
        if (baseMaterial == null)
        {
            Debug.LogError($"[EnemyUnit] baseMaterial が未設定です: {name}");
            return;
        }

        runtimeMaterial = new Material(baseMaterial); // ←Instantiate(null)問題を回避
        image.material = runtimeMaterial;

        // 侵食中心セット
        ApplyCenterPreset();

        // 初期反映
        ApplySuppressionImmediate();
    }

    void Update()
    {
        if (runtimeMaterial == null) return;

        if (debugLogOnce)
        {
            debugLogOnce = false;
            Debug.Log($"[EnemyUnit] {name} shader={runtimeMaterial.shader.name} Has _Suppression={runtimeMaterial.HasProperty("_Suppression")} Has _Center={runtimeMaterial.HasProperty("_Center")}");
        }

        if (obeyGlobalPause && PauseManager.IsPaused) return;

        if (debugPingPong)
        {
            float v = Mathf.PingPong(Time.unscaledTime * 0.25f, 1f);
            runtimeMaterial.SetFloat("_Suppression", v);
            return;
        }

        float step = Mathf.Max(0f, maxFillSpeedPerSecond) * Time.unscaledDeltaTime;
        shown01 = Mathf.MoveTowards(shown01, target01, step);

        runtimeMaterial.SetFloat("_Suppression", shown01);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        enemyManager?.SelectTarget(this);
    }

    public void ApplyDamage(float amount)
    {
        currentSuppression = Mathf.Clamp(currentSuppression + amount, 0f, maxSuppression);
        target01 = Mathf.Clamp01(currentSuppression / maxSuppression);
    }

    public void ResetUnit()
    {
        currentSuppression = 0f;
        ApplySuppressionImmediate();
    }

    public void Pulse(float strength01)
    {
        if (runtimeMaterial != null && runtimeMaterial.HasProperty("_Pulse"))
            runtimeMaterial.SetFloat("_Pulse", Mathf.Clamp01(strength01));
    }

    public void ClearPulse()
    {
        if (runtimeMaterial != null && runtimeMaterial.HasProperty("_Pulse"))
            runtimeMaterial.SetFloat("_Pulse", 0f);
    }

    private void ApplySuppressionImmediate()
    {
        target01 = Mathf.Clamp01(currentSuppression / maxSuppression);
        shown01 = target01;
        if (runtimeMaterial != null) runtimeMaterial.SetFloat("_Suppression", shown01);
    }

    private void ApplyCenterPreset()
    {
        if (runtimeMaterial == null || rectTransform == null) return;
        if (!runtimeMaterial.HasProperty("_Center")) return;

        float w = rectTransform.rect.width;
        float h = rectTransform.rect.height;
        if (w <= 0f || h <= 0f) return;

        float x = (w * 0.5f) * (1f - 2f * cornerBias);
        float y = (h * 0.5f) * (1f - 2f * cornerBias);

        Vector2 local = Vector2.zero;
        switch (missingCorner)
        {
            case MissingCorner.LeftTop: local = new Vector2(+x, -y); break; // 右下
            case MissingCorner.LeftBottom: local = new Vector2(+x, +y); break; // 右上
            case MissingCorner.RightTop: local = new Vector2(-x, -y); break; // 左下
            case MissingCorner.RightBottom: local = new Vector2(-x, +y); break; // 左上
        }

        float uvX = Mathf.InverseLerp(-w * 0.5f, w * 0.5f, local.x);
        float uvY = Mathf.InverseLerp(-h * 0.5f, h * 0.5f, local.y);

        runtimeMaterial.SetVector("_Center", new Vector4(Mathf.Clamp01(uvX), Mathf.Clamp01(uvY), 0f, 0f));
    }
}