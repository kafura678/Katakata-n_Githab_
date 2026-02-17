using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class EnemyUISuppressionView : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] EnemySystem enemy;

    [Header("中心設定")]
    [SerializeField] bool useRandomCenter = false;
    [SerializeField] Vector2 manualCenter = new(0.5f, 0.5f);
    [SerializeField, Range(0f, 0.5f)] float centerRandomRange = 0.3f;

    [Header("ブロック設定（Shaderと合わせる）")]
    [SerializeField, Range(4, 256)] int blocksPerAxis = 64;
    [SerializeField] bool snapCenterToBlock = true;

    [Header("送信1回あたりの演出")]
    [Tooltip("1回の送信で変化が終わる時間（秒）")]
    [SerializeField, Range(0.05f, 2f)] float perSendDuration = 0.45f;

    [Header("イーズ（加速→減速）")]
    [Tooltip("0=線形, 1=強いイーズ")]
    [SerializeField, Range(0f, 1f)] float easeStrength = 1f;

    [Tooltip("より自然なイーズ（おすすめ）")]
    [SerializeField] bool useSmoothStepEase = true;

    [Header("ブロック感")]
    [SerializeField] bool quantizeProgressToBlocks = true;
    [SerializeField, Range(1, 512)] int progressSteps = 128;

    [Header("成功時に侵入口を変える")]
    [SerializeField] bool changeCenterOnSuccess = false;

    Image image;
    Material runtimeMat;

    float shownProgress01;
    Coroutine animCo;

    static readonly int PropProgress = Shader.PropertyToID("_Progress");
    static readonly int PropCenter = Shader.PropertyToID("_Center");
    static readonly int PropBlocks = Shader.PropertyToID("_Blocks");

    void Awake()
    {
        image = GetComponent<Image>();

        runtimeMat = Instantiate(image.material);
        image.material = runtimeMat;

        runtimeMat.SetFloat(PropBlocks, blocksPerAxis);

        ApplyCenter();

        shownProgress01 = GetEnemyProgress01();
        ApplyProgress(shownProgress01);
    }

    /// <summary>
    /// 送信成功など「1回のイベント」で、現在表示→最新制圧率までをじわじわ反映
    /// </summary>
    public void AnimateOneSend()
    {
        if (enemy == null || runtimeMat == null) return;

        if (changeCenterOnSuccess)
        {
            if (useRandomCenter) manualCenter = GetRandomCenter();
            ApplyCenter();
        }

        float target = GetEnemyProgress01();

        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(AnimateProgress(shownProgress01, target, perSendDuration));
    }

    IEnumerator AnimateProgress(float from, float to, float duration)
    {
        from = Mathf.Clamp01(from);
        to = Mathf.Clamp01(to);

        if (Mathf.Approximately(from, to) || duration <= 0f)
        {
            shownProgress01 = to;
            ApplyProgress(shownProgress01);
            animCo = null;
            yield break;
        }

        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / duration);

            // ★ イーズ適用
            float eased = Ease(a);

            // 補間
            shownProgress01 = Mathf.Lerp(from, to, eased);
            ApplyProgress(shownProgress01);

            yield return null;
        }

        // ★最後は必ずピタッと目標に合わせる
        shownProgress01 = to;
        ApplyProgress(shownProgress01);
        animCo = null;
    }

    float Ease(float a01)
    {
        a01 = Mathf.Clamp01(a01);

        // ベースのイーズ
        float baseEase;
        if (useSmoothStepEase)
        {
            // SmoothStep: 加速→減速（自然）
            baseEase = a01 * a01 * (3f - 2f * a01);
        }
        else
        {
            // SmootherStep: さらに滑らか（ややぬるい）
            baseEase = a01 * a01 * a01 * (a01 * (6f * a01 - 15f) + 10f);
        }

        // 0=線形, 1=イーズ にブレンド
        return Mathf.Lerp(a01, baseEase, easeStrength);
    }

    void ApplyProgress(float p01)
    {
        if (runtimeMat == null) return;

        float p = Mathf.Clamp01(p01);

        if (quantizeProgressToBlocks)
        {
            int steps = Mathf.Max(1, progressSteps);
            p = Mathf.Floor(p * steps) / steps;
        }

        runtimeMat.SetFloat(PropProgress, p);
    }

    float GetEnemyProgress01()
    {
        if (enemy == null) return 0f;
        return Mathf.Clamp01(enemy.SuppressionPercent / 100f);
    }

    void ApplyCenter()
    {
        if (runtimeMat == null) return;

        Vector2 c = useRandomCenter ? GetRandomCenter() : manualCenter;

        if (snapCenterToBlock)
            c = SnapToBlockCenter(c, Mathf.Max(1, blocksPerAxis));

        runtimeMat.SetVector(PropCenter, c);
    }

    Vector2 GetRandomCenter()
    {
        float cx = 0.5f + Random.Range(-centerRandomRange, centerRandomRange);
        float cy = 0.5f + Random.Range(-centerRandomRange, centerRandomRange);
        return new Vector2(Mathf.Clamp01(cx), Mathf.Clamp01(cy));
    }

    static Vector2 SnapToBlockCenter(Vector2 uv, int blocks)
    {
        float x = (Mathf.Floor(uv.x * blocks) + 0.5f) / blocks;
        float y = (Mathf.Floor(uv.y * blocks) + 0.5f) / blocks;
        return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying) return;
        if (runtimeMat == null) return;

        runtimeMat.SetFloat(PropBlocks, blocksPerAxis);
        ApplyCenter();
        ApplyProgress(shownProgress01);
    }
#endif
}