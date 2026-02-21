using UnityEngine;

public class EnemySystem : MonoBehaviour
{
    [Header("Enemy HP (Suppression)")]
    [SerializeField] private float maxHP = 100f;
    [SerializeField] private float currentHP = 100f;

    // 0% = 未制圧、100% = 制圧完了
    public float SuppressionPercent
    {
        get
        {
            if (maxHP <= 0f) return 100f;
            float ratio = 1f - (currentHP / maxHP);
            return Mathf.Clamp01(ratio) * 100f;
        }
    }

    public void ResetHP()
    {
        currentHP = maxHP;
    }

    public void ApplyDamage(float damage)
    {
        if (damage <= 0f) return;
        currentHP = Mathf.Max(0f, currentHP - damage);
    }
}