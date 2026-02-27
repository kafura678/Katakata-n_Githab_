using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

public class flowModeManager : MonoBehaviour
{
    [Header("フローモードの持続時間")]
    [SerializeField] private float flowModeDuration = 10f;

    [Header("参照")]
    [SerializeField] private PauseManager pauseManager;
    [SerializeField] private FlowSystem flowSystem;
    [SerializeField] private InputBuffer inputBuffer;
    [SerializeField] private GameObject[] flowModeDeactivationObjects;

    [Header("UI")]
    [SerializeField] private Text originalChallengeText;
    private List<Text> challengeTexts = new List<Text>();
    [SerializeField] private Text textTyped;

    [Header("お題テキストの配置範囲")]
    [SerializeField] Vector2 maxChallengeTextOffset = new Vector2(500f, 450f);
    [SerializeField] Vector2 minChallengeTextOffset = new Vector2(-500f, 250f);

    private Challenge challenge;
    private Action onFlowModeTransitioned;
    private flowModeTransition transition;
    private flowModeTime time;
    void Awake()
    {
        transition = new flowModeTransition();
        time = new flowModeTime();

        time.SetFlowTime(flowModeDuration);
        transition.setFlowModeActivateEvent(OnFlowModeActivated);
        transition.setFlowModeDeactivateEvent(OnFlowModeDeactivated);
    }

    void Update()
    {
        if (flowModeStatus.state == flowModeState.normal)
        {
            transition.tryFlowMode(flowSystem.Percent);
        }
        else if (flowModeStatus.state == flowModeState.canFlowMode)
        {
            transition.tryFlowModeStart();
        }
        else if (flowModeStatus.state == flowModeState.isFlowModeActive)
        {
            inputBuffer.AcceptInput = true;
            inputBuffer.Tick();
            judgeChallenge();

            time.UpdateFlowMode(Time.deltaTime);
            if (time.GetFlowTimeLeft() <= 0f)
                transition.flowSendModeStart();
        }
        else if (flowModeStatus.state == flowModeState.isFlowSendModeActive)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Destroy(challengeTexts[0].gameObject);
                challengeTexts.RemoveAt(0);
                if (challengeTexts.Count == 0)
                {
                    transition.flowModeDeactivated();
                }
            }
        }
    }

    private void judgeChallenge()
    {
        if (challenge.minCount <= inputBuffer.LetterCount)
        {
            updateChallenge();
        }
    }

    private void updateChallenge()
    {
        if (originalChallengeText != null)
        {
            Text newChallengeText = Instantiate(originalChallengeText, originalChallengeText.transform.parent);
            newChallengeText.transform.localPosition = new Vector2(
                UnityEngine.Random.Range(minChallengeTextOffset.x, maxChallengeTextOffset.x),
                UnityEngine.Random.Range(minChallengeTextOffset.y, maxChallengeTextOffset.y)
            );
            foreach (char cr in inputBuffer.TypedChars) newChallengeText.text += cr;
            challengeTexts.Add(newChallengeText);
        }

        inputBuffer.ClearAll();
        challenge = ChallengeGenerator.Create();
    }

    private void OnFlowModeActivated()
    {
        //フローモード制限時間設定
        time.StartFlowMode();

        //ゲーム制限時間を停止
        pauseManager.PauseGameTimer();

        //オブジェクト非表示
        foreach (GameObject obj in flowModeDeactivationObjects)
        {
            obj.SetActive(false);
        }

        //お題生成
        challenge = ChallengeGenerator.Create();
    }

    private void OnFlowModeDeactivated()
    {
        flowSystem.ResetFlow();

        //ゲーム制限時間を再開
        pauseManager.ResumeGameTimer();
        
        //オブジェクト表示
        foreach (GameObject obj in flowModeDeactivationObjects)
        {
            obj.SetActive(true);
        }

    }

}
