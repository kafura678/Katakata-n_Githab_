using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    [Header("敵構成")]
    [SerializeField] private EnemyUnit core;
    [SerializeField] private List<EnemyUnit> minions;

    private EnemyUnit currentTarget;

    public EnemyUnit CurrentTarget => currentTarget;

    void Start()
    {
        SelectFirstAliveMinion();
    }

    public void SelectTarget(EnemyUnit unit)
    {
        if (unit == null) return;

        // 取り巻きが残っている間はコア選択不可
        if (unit == core && !AllMinionsSuppressed())
            return;

        currentTarget = unit;

        Debug.Log("選択ターゲット：" + unit.name);
    }

    public void ApplyDamageToCurrent(float damage)
    {
        if (currentTarget == null) return;

        currentTarget.ApplyDamage(damage);
    }

    private bool AllMinionsSuppressed()
    {
        foreach (var m in minions)
        {
            if (!m.IsSuppressed)
                return false;
        }
        return true;
    }

    private void SelectFirstAliveMinion()
    {
        foreach (var m in minions)
        {
            if (!m.IsSuppressed)
            {
                currentTarget = m;
                return;
            }
        }
    }
}