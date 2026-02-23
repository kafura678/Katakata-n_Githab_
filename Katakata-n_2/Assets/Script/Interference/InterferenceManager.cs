using System.Collections.Generic;
using UnityEngine;

public class InterferenceManager : MonoBehaviour
{
    [Header("発動間隔（秒）")]
    [SerializeField] private Vector2 intervalRangeSeconds = new Vector2(8f, 14f);

    [Header("妨害候補（同じGameObject上のコンポーネント参照）")]
    [SerializeField] private MonoBehaviour[] interferenceComponents;

    [Header("同時発動")]
    [SerializeField] private bool allowOverlap = false;

    [Header("ポーズ追従")]
    [SerializeField] private bool obeyGlobalPause = true;

    // ★外部（GameManager等）から参照する
    public bool IsBlockingInput => AnyRunningBlocking();

    private readonly List<IInterference> pool = new List<IInterference>();
    private float nextTime = 0f;

    void Awake()
    {
        pool.Clear();
        foreach (var mb in interferenceComponents)
        {
            if (mb == null) continue;
            if (mb is IInterference itf) pool.Add(itf);
        }
        ScheduleNext();
    }

    void Update()
    {
        if (obeyGlobalPause && PauseManager.IsPaused) return;

        // 実行中妨害のTick
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].IsRunning) pool[i].Tick();
        }

        // ブロック中でも「Enterで閉じる」など妨害側Tickは動いているのでOK
        if (Time.unscaledTime < nextTime) return;
        if (pool.Count == 0) { ScheduleNext(); return; }

        if (!allowOverlap && AnyRunning())
        {
            nextTime = Time.unscaledTime + 0.5f;
            return;
        }

        var candidates = GetAvailable();
        if (candidates.Count == 0) { ScheduleNext(); return; }

        int pick = Random.Range(0, candidates.Count);
        candidates[pick].Begin();
        ScheduleNext();
    }

    private void ScheduleNext()
    {
        float a = Mathf.Max(0.1f, intervalRangeSeconds.x);
        float b = Mathf.Max(a, intervalRangeSeconds.y);
        nextTime = Time.unscaledTime + Random.Range(a, b);
    }

    private bool AnyRunning()
    {
        for (int i = 0; i < pool.Count; i++)
            if (pool[i].IsRunning) return true;
        return false;
    }

    private bool AnyRunningBlocking()
    {
        for (int i = 0; i < pool.Count; i++)
            if (pool[i].IsRunning && pool[i].BlocksInput) return true;
        return false;
    }

    private List<IInterference> GetAvailable()
    {
        var list = new List<IInterference>();
        for (int i = 0; i < pool.Count; i++)
            if (!pool[i].IsRunning) list.Add(pool[i]);
        return list;
    }
}