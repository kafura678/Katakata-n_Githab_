using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(RectTransform))]
public class EnemyUnit : MonoBehaviour, IPointerClickHandler
{
    // ==============================
    // 欠け角プリセット
    // ==============================
    public enum MissingCorner
    {
        LeftTop,
        LeftBottom,
        RightTop,
        RightBottom
    }

    // ==============================
    // 設定
    // ==============================
    [Header("制圧設定")]
    [SerializeField] private float maxSuppression = 100f;

    [Header("じわじわ（最大変化速度）")]
    [Tooltip("表示の制圧率(0〜1)が1秒で最大どれだけ進むか（0〜1/秒）")]
    [SerializeField] private float maxFillSpeedPerSecond = 0.35f;

    [Header("形状プリセット")]
    [Tooltip("欠けている角（取り巻き画像に合わせて設定）")]
    [SerializeField] private MissingCorner missingCorner = MissingCorner.LeftTop;

    [Tooltip("侵食中心を角に寄せる量（0=中央 / 0.5=角）")]
    [SerializeField, Range(0f, 0.5f)]
    private float cornerBias = 0.30f; // 0.25〜0.35推奨

    [Header("クリック設定")]
    [Tooltip("この値以上のアルファ部分のみクリック可能")]
    [SerializeField, Range(0f, 1f)]
    private float alphaHitThreshold = 0.1f;

    // ==============================
    // 内部
    // ==============================
    private float currentSuppression = 0f;   // 0〜maxSuppression（内部値）
    private float targetNormalized = 0f;     // 0〜1（目標）
    private float displayedNormalized = 0f;  // 0〜1（表示：じわじわ）

    private Material runtimeMaterial;
    private RectTransform rectTransform;
    private Image image;
    private EnemyManager enemyManager;

    // ==============================
    // 公開
    // ==============================
    public float SuppressionPercent => currentSuppression;
    public bool IsSuppressed => currentSuppression >= maxSuppression;

    // ==============================
    // Unity
    // ==============================
    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();

        // ★ マテリアル個別化（共有で全敵が同時に変わるのを防ぐ）
        runtimeMaterial = Instantiate(image.material);
        image.material = runtimeMaterial;

        // ★ 透明部分クリック無効（不透明部分だけ反応）
        image.alphaHitTestMinimumThreshold = alphaHitThreshold;

        // EnemyManager 参照（SelectTarget(EnemyUnit unit) を呼ぶ）
        enemyManager = FindObjectOfType<EnemyManager>();

        // ★ 欠け角の反対側に侵食中心を置く
        Vector2 centerLocal = GetPresetCenterLocal();
        ApplyCenterFromLocal(centerLocal);

        // 初期反映
        UpdateShaderImmediate();
    }

    void Update()
    {
        if (runtimeMaterial == null) return;

        // ★ じわじわを「速度制限」で保証（急に一気に変わるのを防ぐ）
        float step = Mathf.Max(0f, maxFillSpeedPerSecond) * Time.deltaTime;
        displayedNormalized = Mathf.MoveTowards(displayedNormalized, targetNormalized, step);

        runtimeMaterial.SetFloat("_Suppression", displayedNormalized);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // 再生中のみ反映（編集時は material が無いことが多い）
        if (!Application.isPlaying) return;
        if (runtimeMaterial == null || rectTransform == null) return;

        Vector2 centerLocal = GetPresetCenterLocal();
        ApplyCenterFromLocal(centerLocal);

        // 速度0は不自然なので保険
        if (maxFillSpeedPerSecond < 0f) maxFillSpeedPerSecond = 0f;
    }
#endif

    // ==============================
    // クリック
    // ==============================
    public void OnPointerClick(PointerEventData eventData)
    {
        if (enemyManager != null)
            enemyManager.SelectTarget(this);
    }

    // ==============================
    // 制圧（ダメージ）
    // ==============================
    public void ApplyDamage(float amount)
    {
        currentSuppression += amount;
        currentSuppression = Mathf.Clamp(currentSuppression, 0f, maxSuppression);

        targetNormalized = currentSuppression / maxSuppression;
        targetNormalized = Mathf.Clamp01(targetNormalized);
    }

    public void ResetUnit()
    {
        currentSuppression = 0f;
        UpdateShaderImmediate();
    }

    private void UpdateShaderImmediate()
    {
        targetNormalized = Mathf.Clamp01(currentSuppression / maxSuppression);
        displayedNormalized = targetNormalized;

        if (runtimeMaterial != null)
            runtimeMaterial.SetFloat("_Suppression", displayedNormalized);
    }

    // ==============================
    // 中心制御（欠け角の反対側）
    // ==============================
    private Vector2 GetPresetCenterLocal()
    {
        if (rectTransform == null) return Vector2.zero;

        float w = rectTransform.rect.width;
        float h = rectTransform.rect.height;

        if (w <= 0f || h <= 0f) return Vector2.zero;

        // RectTransform中心(0,0)から角方向に寄せる距離
        // cornerBias=0.30 → halfの(1 - 0.60)=0.40倍
        float x = (w * 0.5f) * (1f - 2f * cornerBias);
        float y = (h * 0.5f) * (1f - 2f * cornerBias);

        // 欠け角の「反対角」に中心を置く
        switch (missingCorner)
        {
            case MissingCorner.LeftTop: return new Vector2(+x, -y); // 右下
            case MissingCorner.LeftBottom: return new Vector2(+x, +y); // 右上
            case MissingCorner.RightTop: return new Vector2(-x, -y); // 左下
            case MissingCorner.RightBottom: return new Vector2(-x, +y); // 左上
            default: return Vector2.zero;
        }
    }

    private void ApplyCenterFromLocal(Vector2 localPos)
    {
        if (runtimeMaterial == null || rectTransform == null)
            return;

        float width = rectTransform.rect.width;
        float height = rectTransform.rect.height;

        if (width <= 0f || height <= 0f)
            return;

        // ローカル座標（-w/2..w/2, -h/2..h/2）→ UV（0..1）
        float uvX = Mathf.InverseLerp(-width * 0.5f, width * 0.5f, localPos.x);
        float uvY = Mathf.InverseLerp(-height * 0.5f, height * 0.5f, localPos.y);

        // 範囲外防止
        uvX = Mathf.Clamp01(uvX);
        uvY = Mathf.Clamp01(uvY);

        runtimeMaterial.SetVector("_Center", new Vector4(uvX, uvY, 0f, 0f));
    }

    // ==============================
    // （任意）外部から中心を上書きしたい場合
    // ==============================
    public void SetCenterFromLocalPosition(Vector2 localPos)
    {
        ApplyCenterFromLocal(localPos);
    }

    public void SetCenterFromScreenPosition(Vector2 screenPos, Camera cam = null)
    {
        if (rectTransform == null) return;

        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPos, cam, out local))
        {
            ApplyCenterFromLocal(local);
        }
    }
}