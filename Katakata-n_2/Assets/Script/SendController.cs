using UnityEngine;

public class SendController : MonoBehaviour
{
    public enum SendResult
    {
        None,
        Success,
        Fail
    }

    // ==============================
    // 参照
    // ==============================
    [Header("参照")]
    [SerializeField] private EnemyManager enemyManager;
    [SerializeField] private EnemyUISuppressionView enemyView; // 任意：送信演出
    [SerializeField] private FlowSystem flow;

    [Header("SE")]
    [SerializeField] private AudioSource seSource;
    [SerializeField] private AudioClip seSuccess;
    [SerializeField] private AudioClip seFail;
    [SerializeField, Range(0f, 1f)] private float seVolume = 1f;

    // ==============================
    // 調整：基本値
    // ==============================
    [Header("調整：基本値")]
    [Tooltip("送信成功1回の基本ダメージ（倍率前）")]
    [SerializeField] private float baseDamagePerSend = 10f;

    [Tooltip("送信成功1回の基本FLOW増加（倍率前）")]
    [SerializeField] private float baseFlowGainPerSend = 10f;

    [Tooltip("送信失敗時のFLOW減少")]
    [SerializeField] private float flowLossOnFail = 5f;

    // ==============================
    // 調整：入力数倍率
    // ==============================
    [Header("調整：入力数倍率")]
    [Tooltip("最低入力数を超えた1文字ごとの倍率加算（例：0.10=+10%/文字）")]
    [SerializeField] private float bonusPerExtraInput = 0.10f;

    [Tooltip("入力数倍率の上限（例：3=最大3倍）")]
    [SerializeField] private float maxInputMultiplier = 3.0f;

    // ==============================
    // 調整：同時打ちボーナス（任意）
    // ==============================
    [Header("調整：同時打ちボーナス（任意）")]
    [Tooltip("追加1個ごとにダメージ倍率が増える量（例:0.25=+25%）。不要なら0")]
    [SerializeField] private float damageBonusPerAdditional = 0.25f;

    [Tooltip("追加1個ごとにFLOW倍率が増える量（例:0.25=+25%）。不要なら0")]
    [SerializeField] private float flowBonusPerAdditional = 0.25f;

    [Tooltip("同時打ちダメージ倍率の上限")]
    [SerializeField] private float maxDamageSimulMultiplier = 5.0f;

    [Tooltip("同時打ちFLOW倍率の上限")]
    [SerializeField] private float maxFlowSimulMultiplier = 5.0f;

    // ==============================
    // 外部から呼ぶ
    // ==============================
    public SendResult TrySend(InputBuffer inputBuffer, ChallengeController challengeController, float elapsedSeconds)
    {
        if (inputBuffer == null || challengeController == null)
            return SendResult.None;

        if (!challengeController.HasSelection)
            return SendResult.None;

        bool ok = challengeController.IsSendableForAllSelected(inputBuffer);

        if (!ok)
        {
            PlayFailSE();

            if (flow != null)
                flow.Sub(flowLossOnFail);

            inputBuffer.ClearAll();
            return SendResult.Fail;
        }

        // =========================
        // 成功：倍率計算
        // =========================
        int inputCount = inputBuffer.LetterCount;

        // 「全選択を満たす最低入力数」（= 選択中minの最大）
        int requiredMin = Mathf.Max(0, challengeController.RequiredMinForAllSelected);

        int extra = Mathf.Max(0, inputCount - requiredMin);

        float inputMul = 1f + extra * Mathf.Max(0f, bonusPerExtraInput);
        inputMul = Mathf.Clamp(inputMul, 1f, Mathf.Max(1f, maxInputMultiplier));

        int k = challengeController.SelectedCount;

        float dmgSimulMul = CalcSimulMultiplier(k, damageBonusPerAdditional, maxDamageSimulMultiplier);
        float flowSimulMul = CalcSimulMultiplier(k, flowBonusPerAdditional, maxFlowSimulMultiplier);

        float finalDamage = baseDamagePerSend * inputMul * dmgSimulMul;
        float finalFlow = baseFlowGainPerSend * inputMul * flowSimulMul;

        // =========================
        // 反映
        // =========================
        if (enemyManager != null)
            enemyManager.ApplyDamageToCurrent(finalDamage);

        enemyView?.AnimateOneSend();

        if (flow != null)
            flow.Add(finalFlow);

        PlaySuccessSE();

        // 成功後：お題置換（中で選択解除される）
        challengeController.ReplaceSelectedWithReveal();

        // 入力リセット
        inputBuffer.ClearAll();

        return SendResult.Success;
    }

    // GameManagerから「同時打ち不可」などで鳴らしたい用
    public void PlayFailSE()
    {
        PlaySE(seFail);
    }

    public void PlaySuccessSE()
    {
        PlaySE(seSuccess);
    }

    // ==============================
    // 内部：同時打ち倍率
    // ==============================
    private float CalcSimulMultiplier(int selectedCount, float bonusPerAdditional, float maxMul)
    {
        int add = Mathf.Max(0, selectedCount - 1);
        float mul = 1f + add * Mathf.Max(0f, bonusPerAdditional);
        return Mathf.Min(mul, Mathf.Max(1f, maxMul));
    }

    // ==============================
    // 内部：SE
    // ==============================
    private void PlaySE(AudioClip clip)
    {
        if (clip == null) return;

        if (seSource == null)
            seSource = GetComponent<AudioSource>();

        if (seSource == null) return;

        seSource.PlayOneShot(clip, seVolume);
    }
}