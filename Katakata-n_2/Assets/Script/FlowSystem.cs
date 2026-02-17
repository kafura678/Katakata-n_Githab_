using UnityEngine;

public class FlowSystem : MonoBehaviour
{
    [Header("Flow Gauge (0-100)")]
    [SerializeField] private float current = 0f;

    public float Percent => Mathf.Clamp(current, 0f, 100f);

    public void ResetFlow()
    {
        current = 0f;
    }

    public void Add(float amount)
    {
        if (amount <= 0f) return;
        current = Mathf.Min(100f, current + amount);
    }

    public void Sub(float amount)
    {
        if (amount <= 0f) return;
        current = Mathf.Max(0f, current - amount);
    }
}