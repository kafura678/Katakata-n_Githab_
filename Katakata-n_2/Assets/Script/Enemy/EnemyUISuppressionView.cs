using System.Collections;
using UnityEngine;

public class EnemyUISuppressionView : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private EnemyManager enemyManager;

    [Header("送信演出（侵食の“境界”を一瞬荒らす）")]
    [Tooltip("送信1回でPulseをどこまで上げるか（0〜1）")]
    [SerializeField, Range(0f, 1f)] private float pulsePeak = 1f;

    [Tooltip("Pulseを0に戻すまでの時間（秒）")]
    [SerializeField] private float pulseDuration = 0.25f;

    [Tooltip("全体ポーズ中はPulse更新を止める（任意）")]
    [SerializeField] private bool obeyGlobalPause = false;

    private Coroutine pulseCo;

    /// <summary>
    /// GameManagerの「送信成功時」に呼ぶ
    /// </summary>
    public void AnimateOneSend()
    {
        if (enemyManager == null) return;

        var target = enemyManager.CurrentTarget;
        if (target == null) return;

        if (pulseCo != null) StopCoroutine(pulseCo);
        pulseCo = StartCoroutine(PulseRoutine(target));
    }

    /// <summary>
    /// 外部から明示的にターゲットを指定して鳴らしたい場合
    /// </summary>
    public void AnimateOneSend(EnemyUnit target)
    {
        if (target == null) return;

        if (pulseCo != null) StopCoroutine(pulseCo);
        pulseCo = StartCoroutine(PulseRoutine(target));
    }

    private IEnumerator PulseRoutine(EnemyUnit target)
    {
        // 開始時にピークまで上げる
        target.Pulse(pulsePeak);

        float dur = Mathf.Max(0.001f, pulseDuration);
        float t = 0f;

        while (t < dur)
        {
            if (obeyGlobalPause && PauseManager.IsPaused)
            {
                yield return null;
                continue;
            }

            // 1 -> 0 へ減衰
            float k = 1f - (t / dur);
            target.Pulse(pulsePeak * k);

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        target.ClearPulse();
        pulseCo = null;
    }
}