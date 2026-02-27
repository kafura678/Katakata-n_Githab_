using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ErrorWindowInterference : MonoBehaviour, IInterference
{
    [Header("表示名")]
    [SerializeField] private string displayName = "エラーウィンドウ大量発生";
    public string DisplayName => displayName;

    // 妨害中は他操作を止める
    public bool BlocksInput => true;

    [SerializeField] private GameTimer gameTimer;

    [Header("生成")]
    [SerializeField] private RectTransform spawnParent; // Canvas配下（Panelなど）
    [SerializeField] private ErrorWindowUI windowPrefab;

    [Header("枚数")]
    [SerializeField] private int spawnCount = 20;

    [Header("1枚ずつ出現（秒）")]
    [SerializeField] private bool randomizeInterval = true;
    [SerializeField] private float spawnIntervalSeconds = 0.03f;
    [SerializeField] private Vector2 spawnIntervalRangeSeconds = new Vector2(0.02f, 0.06f);

    [Header("配置範囲（親のRect内）")]
    [SerializeField] private Vector2 margin = new Vector2(40f, 40f);

    [Header("Enterで消す")]
    [SerializeField] private bool closeOnEnter = true;

    [Header("ポーズ追従")]
    [SerializeField] private bool obeyGlobalPause = true;

    private readonly List<ErrorWindowUI> alive = new List<ErrorWindowUI>(); // 末尾が最前面（最後に出た）
    public bool IsRunning { get; private set; }

    private Coroutine spawnRoutine;
    private bool isSpawning = false; // ★生成中フラグ（生成完了まで消せない）

    public void Begin()
    {
        if (IsRunning) return;

        if (spawnParent == null || windowPrefab == null)
        {
            Debug.LogError("[ErrorWindowInterference] spawnParent / windowPrefab が未設定です");
            return;
        }

        IsRunning = true;
        alive.Clear();

        // 生成中止（念のため）
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        isSpawning = true;
        spawnRoutine = StartCoroutine(SpawnWindowsOneByOne());
    }

    public void End()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        isSpawning = false;

        for (int i = alive.Count - 1; i >= 0; i--)
        {
            if (alive[i] != null) alive[i].Close();
        }
        alive.Clear();

        IsRunning = false;
    }

    public void Tick()
    {
        if (!IsRunning) return;
        if (obeyGlobalPause && PauseManager.IsPaused) return;

        // 参照切れ掃除
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            if (alive[i] == null) alive.RemoveAt(i);
        }

        // ★生成が終わるまで消せない
        if (!isSpawning && closeOnEnter &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            CloseOneFromTop();
        }

        // 生成が終わっていて、全部消えたら終了
        if (!isSpawning && alive.Count == 0)
        {
            IsRunning = false;
        }
    }

    // ==============================
    // 1枚ずつ生成（前面に積む）
    // ==============================
    private IEnumerator SpawnWindowsOneByOne()
    {
        int count = Mathf.Max(0, spawnCount);

        for (int i = 0; i < count; i++)
        {
            // ポーズ中は生成を止める
            if (obeyGlobalPause)
            {
                while (PauseManager.IsPaused)
                    yield return null;
            }

            var w = Instantiate(windowPrefab, spawnParent);

            w.SetGameTimer(gameTimer);

            var rt = w.GetComponent<RectTransform>();
            RandomPlace(rt);

            // ★前面へ（最後の兄弟＝最前面）
            w.transform.SetAsLastSibling();

            // ★末尾が「最後に出た」ウィンドウ
            alive.Add(w);

            // 次まで待つ（unscaled）
            float wait = GetSpawnInterval();
            float t = 0f;
            while (t < wait)
            {
                if (obeyGlobalPause && PauseManager.IsPaused)
                {
                    yield return null;
                    continue;
                }

                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // 生成完了
        isSpawning = false;
        spawnRoutine = null;
    }

    private float GetSpawnInterval()
    {
        if (!randomizeInterval)
            return Mathf.Max(0f, spawnIntervalSeconds);

        float a = Mathf.Max(0f, spawnIntervalRangeSeconds.x);
        float b = Mathf.Max(a, spawnIntervalRangeSeconds.y);
        return Random.Range(a, b);
    }

    // ==============================
    // 最後に出たウィンドウから消す（LIFO）
    // ==============================
    private void CloseOneFromTop()
    {
        if (alive.Count == 0) return;

        int last = alive.Count - 1;
        var w = alive[last];
        alive.RemoveAt(last);

        if (w != null) w.Close();
    }

    // ==============================
    // ランダム配置（親Rect内）
    // ==============================
    private void RandomPlace(RectTransform rt)
    {
        if (rt == null || spawnParent == null) return;

        var parent = spawnParent.rect;

        float xMin = parent.xMin + margin.x;
        float xMax = parent.xMax - margin.x;
        float yMin = parent.yMin + margin.y;
        float yMax = parent.yMax - margin.y;

        float x = Random.Range(xMin, xMax);
        float y = Random.Range(yMin, yMax);

        rt.anchoredPosition = new Vector2(x, y);
    }
}