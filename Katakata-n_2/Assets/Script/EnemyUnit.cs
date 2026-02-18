using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(RectTransform))]
public class EnemyUnit : MonoBehaviour
{
    // ==============================
    // 設定
    // ==============================

    [Header("制圧設定")]
    [SerializeField] private float maxSuppression = 100f;

    [Header("じわじわ速度")]
    [SerializeField] private float lerpSpeed = 4f;

    [Header("侵食中心（ローカル座標）")]
    [Tooltip("RectTransform中心(0,0)基準のローカル座標")]
    [SerializeField] private Vector2 centerLocalPosition = Vector2.zero;

    // ==============================
    // 内部状態
    // ==============================

    private float currentSuppression = 0f;
    private float targetNormalized = 0f;
    private float displayedNormalized = 0f;

    private Material runtimeMaterial;
    private RectTransform rectTransform;

    // ==============================
    // 公開プロパティ
    // ==============================

    public float SuppressionPercent => currentSuppression;
    public bool IsSuppressed => currentSuppression >= maxSuppression;

    // ==============================
    // Unity
    // ==============================

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        var img = GetComponent<Image>();

        // ★ マテリアル個別化（UI共有回避）
        runtimeMaterial = Instantiate(img.material);
        img.material = runtimeMaterial;

        ApplyCenterFromLocal(centerLocalPosition);
        UpdateShaderImmediate();
    }

    void Update()
    {
        // じわじわ補間
        displayedNormalized = Mathf.Lerp(
            displayedNormalized,
            targetNormalized,
            Time.deltaTime * lerpSpeed
        );

        runtimeMaterial.SetFloat("_Suppression", displayedNormalized);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying && runtimeMaterial != null)
        {
            ApplyCenterFromLocal(centerLocalPosition);
        }
    }
#endif

    // ==============================
    // ダメージ処理
    // ==============================

    public void ApplyDamage(float amount)
    {
        currentSuppression += amount;
        currentSuppression = Mathf.Clamp(currentSuppression, 0f, maxSuppression);

        targetNormalized = currentSuppression / maxSuppression;
    }

    public void ResetUnit()
    {
        currentSuppression = 0f;
        UpdateShaderImmediate();
    }

    private void UpdateShaderImmediate()
    {
        targetNormalized = currentSuppression / maxSuppression;
        displayedNormalized = targetNormalized;

        if (runtimeMaterial != null)
            runtimeMaterial.SetFloat("_Suppression", displayedNormalized);
    }

    // ==============================
    // 侵食中心制御（歪形対応）
    // ==============================

    /// <summary>
    /// ローカル座標から侵食中心を設定
    /// </summary>
    public void SetCenterFromLocalPosition(Vector2 localPos)
    {
        centerLocalPosition = localPos;
        ApplyCenterFromLocal(localPos);
    }

    /// <summary>
    /// クリック位置から侵食中心を設定
    /// </summary>
    public void SetCenterFromScreenPosition(Vector2 screenPos, Camera cam = null)
    {
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            screenPos,
            cam,
            out local))
        {
            SetCenterFromLocalPosition(local);
        }
    }

    /// <summary>
    /// ローカル座標 → UV変換 → シェーダーへ反映
    /// </summary>
    private void ApplyCenterFromLocal(Vector2 localPos)
    {
        if (runtimeMaterial == null || rectTransform == null)
            return;

        float width = rectTransform.rect.width;
        float height = rectTransform.rect.height;

        if (width <= 0f || height <= 0f)
            return;

        // ローカル座標 → 0〜1 UV変換
        float uvX = Mathf.InverseLerp(-width * 0.5f, width * 0.5f, localPos.x);
        float uvY = Mathf.InverseLerp(-height * 0.5f, height * 0.5f, localPos.y);

        uvX = Mathf.Clamp01(uvX);
        uvY = Mathf.Clamp01(uvY);

        runtimeMaterial.SetVector("_Center", new Vector4(uvX, uvY, 0f, 0f));
    }
}