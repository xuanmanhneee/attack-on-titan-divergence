using System;
using UnityEngine;

// Máu dùng chung cho người chơi và titan.
public class Health : MonoBehaviour
{
    public float maxHealth = 100f;

    public float Current { get; private set; }
    public bool IsDead => Current <= 0f;
    public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;

    public event Action<float> Damaged; // tham số: lượng sát thương
    public event Action Died;

    void Awake()
    {
        if (Current <= 0f) Current = maxHealth;
    }

    // Gọi ngay sau AddComponent để đặt máu tối đa.
    public void Init(float max)
    {
        maxHealth = max;
        Current = max;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;
        Current = Mathf.Max(0f, Current - amount);
        Damaged?.Invoke(amount);
        if (Current <= 0f) Died?.Invoke();
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        Current = Mathf.Min(maxHealth, Current + amount);
    }
}
