using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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
    // 侵食中心の指定方法
    // ==============================
    public enum CenterMode
    {
        PresetByMissingCorner, // 欠け角の反対側（従来）
        ManualUV,              // InspectorでUV(0..1)指定
        ManualLocal            // Inspectorでローカル座標指定（Rect内でクランプ）
    }

    // ==============================
    // 参照
    // ==============================
    [Header("参照（見た目のImage）")]
    [Tooltip("親子構造なら実際に表示している子Imageを指定")]
    [SerializeField] private Image targetImage;

    [Tooltip("通常はtargetImageのRectTransformでOK")]
    [SerializeField] private RectTransform targetRect;

    [Header("侵食マテリアル（必須）")]
    [Tooltip("SuppressionRadialBlock.shader を使った Material を入れる")]
    [SerializeField] private Material baseMaterial;

    // ==============================
    // 制圧設定
    // ==============================
    [Header("制圧（内部値）")]
    [SerializeField] private float maxSuppression = 100f;

    [Header("じわじわ速度（0〜1/秒）")]
    [Tooltip("表示の制圧率(0〜1)が1秒で最大どれだけ進むか")]
    [SerializeField] private float maxFillSpeedPerSecond = 0.35f;

    [Header("全体ポーズ追従")]
    [Tooltip("ONなら PauseManager.IsPaused 中は侵食の更新を止める")]
    [SerializeField] private bool obeyGlobalPause = true;

    // ==============================
    // 侵食中心
    // ==============================
    [Header("侵食中心")]
    [SerializeField] private CenterMode centerMode = CenterMode.PresetByMissingCorner;

    [Tooltip("欠けている角（取り巻き画像に合わせて設定）")]
    [SerializeField] private MissingCorner missingCorner = MissingCorner.LeftTop;

    [Tooltip("侵食中心を角に寄せる量（0=中央 / 0.5=角）")]
    [SerializeField, Range(0f, 0.5f)]
    private float cornerBias = 0.30f;

    [Tooltip("中心UV（0..1）。ManualUVのとき使用")]
    [SerializeField] private Vector2 manualCenterUV = new Vector2(0.5f, 0.5f);

    [Tooltip("中心Local（RectTransformローカル座標）。ManualLocalのとき使用")]
    [SerializeField] private Vector2 manualCenterLocal = Vector2.zero;

    // ==============================
    // クリック
    // ==============================
    [Header("クリック（透明部分）")]
    [Tooltip("この値以上のアルファ部分のみクリック可能（Readableが必要。不要なら0推奨）")]
    [SerializeField, Range(0f, 1f)]
    private float alphaHitThreshold = 0f;

    // ==============================
    // 内部
    // ==============================
    private float currentSuppression = 0f; // 0..max
    private float target01 = 0f;           // 0..1（目標）
    private float shown01 = 0f;            // 0..1（表示：じわじわ）

    private Material runtimeMaterial;
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
        enemyManager = FindObjectOfType<EnemyManager>();

        // 参照が未設定なら子から探す（親子構造対応）
        if (targetImage == null) targetImage = GetComponentInChildren<Image>(true);
        if (targetRect == null && targetImage != null) targetRect = targetImage.rectTransform;

        if (targetImage == null)
        {
            Debug.LogError($"[EnemyUnit] targetImage が見つかりません: {name}");
            return;
        }

        // 透明部分クリック無効（Readableでないと例外になるため保険）
        if (alphaHitThreshold > 0f)
        {
            try
            {
                targetImage.alphaHitTestMinimumThreshold = alphaHitThreshold;
            }
            catch (System.InvalidOperationException)
            {
                // Read/Write OFF のSpriteでは設定できない
                targetImage.alphaHitTestMinimumThreshold = 0f;
            }
        }

        if (baseMaterial == null)
        {
            Debug.LogError($"[EnemyUnit] baseMaterial が未設定です: {name}");
            return;
        }

        // ★敵ごとに独立したマテリアルを作る（共有で全員変わるのを防ぐ）
        runtimeMaterial = new Material(baseMaterial);
        targetImage.material = runtimeMaterial;

        // 中心を適用（Inspector設定）
        ApplyCenterByMode();

        // 初期反映
        ApplySuppressionImmediate();
    }

    void Update()
    {
        if (runtimeMaterial == null) return;

        if (obeyGlobalPause && PauseManager.IsPaused) return;

        // じわじわ更新（TimeScale非依存）
        float step = Mathf.Max(0f, maxFillSpeedPerSecond) * Time.unscaledDeltaTime;
        shown01 = Mathf.MoveTowards(shown01, target01, step);
        SetSuppression01(shown01);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // 再生中にInspector変更を即反映
        if (!Application.isPlaying) return;
        if (runtimeMaterial == null) return;

        ApplyCenterByMode();
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
        currentSuppression = Mathf.Clamp(currentSuppression + amount, 0f, maxSuppression);
        target01 = Mathf.Clamp01(currentSuppression / Mathf.Max(0.0001f, maxSuppression));
    }

    public void ResetUnit()
    {
        currentSuppression = 0f;
        ApplySuppressionImmediate();
    }

    private void ApplySuppressionImmediate()
    {
        target01 = Mathf.Clamp01(currentSuppression / Mathf.Max(0.0001f, maxSuppression));
        shown01 = target01;
        SetSuppression01(shown01);
    }

    private void SetSuppression01(float v)
    {
        if (runtimeMaterial == null) return;
        if (!runtimeMaterial.HasProperty("_Suppression")) return;

        runtimeMaterial.SetFloat("_Suppression", Mathf.Clamp01(v));
    }

    // ==============================
    // 送信演出（Pulse）
    // ==============================
    public void Pulse(float strength01)
    {
        if (runtimeMaterial == null) return;
        if (!runtimeMaterial.HasProperty("_Pulse")) return;

        runtimeMaterial.SetFloat("_Pulse", Mathf.Clamp01(strength01));
    }

    public void ClearPulse()
    {
        if (runtimeMaterial == null) return;
        if (!runtimeMaterial.HasProperty("_Pulse")) return;

        runtimeMaterial.SetFloat("_Pulse", 0f);
    }

    // ==============================
    // 侵食中心（Inspectorで設定可能）
    // ==============================
    private void ApplyCenterByMode()
    {
        if (runtimeMaterial == null) return;
        if (!runtimeMaterial.HasProperty("_Center")) return;

        switch (centerMode)
        {
            case CenterMode.PresetByMissingCorner:
                ApplyCenterPreset();
                break;

            case CenterMode.ManualUV:
                SetCenterUV(manualCenterUV);
                break;

            case CenterMode.ManualLocal:
                SetCenterLocal(manualCenterLocal);
                break;
        }
    }

    private void ApplyCenterPreset()
    {
        if (targetRect == null) return;

        float w = targetRect.rect.width;
        float h = targetRect.rect.height;
        if (w <= 0f || h <= 0f) return;

        float x = (w * 0.5f) * (1f - 2f * cornerBias);
        float y = (h * 0.5f) * (1f - 2f * cornerBias);

        Vector2 local;
        switch (missingCorner)
        {
            case MissingCorner.LeftTop: local = new Vector2(+x, -y); break; // 右下
            case MissingCorner.LeftBottom: local = new Vector2(+x, +y); break; // 右上
            case MissingCorner.RightTop: local = new Vector2(-x, -y); break; // 左下
            case MissingCorner.RightBottom: local = new Vector2(-x, +y); break; // 左上
            default: local = Vector2.zero; break;
        }

        SetCenterLocal(local);
    }

    private void SetCenterUV(Vector2 uv)
    {
        uv.x = Mathf.Clamp01(uv.x);
        uv.y = Mathf.Clamp01(uv.y);

        runtimeMaterial.SetVector("_Center", new Vector4(uv.x, uv.y, 0f, 0f));
    }

    private void SetCenterLocal(Vector2 localPos)
    {
        if (targetRect == null) return;

        float w = targetRect.rect.width;
        float h = targetRect.rect.height;
        if (w <= 0f || h <= 0f) return;

        float uvX = Mathf.InverseLerp(-w * 0.5f, w * 0.5f, localPos.x);
        float uvY = Mathf.InverseLerp(-h * 0.5f, h * 0.5f, localPos.y);

        SetCenterUV(new Vector2(uvX, uvY));
    }

    // （任意）外部から中心を上書き（クリック位置など）
    public void SetCenterFromScreenPosition(Vector2 screenPos, Camera cam = null)
    {
        if (targetRect == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, screenPos, cam, out var local))
        {
            SetCenterLocal(local);
        }
    }
}