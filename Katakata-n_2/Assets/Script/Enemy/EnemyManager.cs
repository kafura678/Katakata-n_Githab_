using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    [Header("敵構成")]
    [SerializeField] private EnemyUnit core;
    [SerializeField] private List<EnemyUnit> minions = new List<EnemyUnit>();

    private EnemyUnit currentTarget;
    public EnemyUnit CurrentTarget => currentTarget;

    void Start()
    {
        SelectFirstAliveMinionOrCore();
    }

    public void SelectTarget(EnemyUnit unit)
    {
        if (unit == null) return;

        // 取り巻きが残っている間はコア選択不可
        if (unit == core && !AllMinionsSuppressed())
            return;

        currentTarget = unit;
    }

    public void ApplyDamageToCurrent(float damage, bool pulse = true)
    {
        if (currentTarget == null) return;

        currentTarget.ApplyDamage(damage);

        // 送信1回の演出（必要なら）
        if (pulse) currentTarget.Pulse(1f);

        AutoRetargetIfNeeded();
    }

    private void AutoRetargetIfNeeded()
    {
        // 全取り巻き制圧 → コアへ
        if (AllMinionsSuppressed())
        {
            if (core != null) currentTarget = core;
            return;
        }

        // 取り巻き制圧済みなら次の未制圧へ
        if (currentTarget != null && currentTarget != core && currentTarget.IsSuppressed)
        {
            var next = FindFirstUnsuppressedMinion();
            if (next != null) currentTarget = next;
        }

        if (currentTarget == null)
            SelectFirstAliveMinionOrCore();
    }

    private bool AllMinionsSuppressed()
    {
        for (int i = 0; i < minions.Count; i++)
        {
            var m = minions[i];
            if (m != null && !m.IsSuppressed) return false;
        }
        return true;
    }

    private EnemyUnit FindFirstUnsuppressedMinion()
    {
        for (int i = 0; i < minions.Count; i++)
        {
            var m = minions[i];
            if (m != null && !m.IsSuppressed) return m;
        }
        return null;
    }

    private void SelectFirstAliveMinionOrCore()
    {
        var first = FindFirstUnsuppressedMinion();
        if (first != null) currentTarget = first;
        else if (core != null) currentTarget = core;
    }
}