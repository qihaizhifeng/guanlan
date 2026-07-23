using UnityEngine;
using System;

/// <summary>
/// 通用生命值组件（敌人、Boss、可破坏物件通用）。
/// 实现 IHealth 接口。
/// </summary>
public class HealthSystem : MonoBehaviour, IHealth
{
    [SerializeField] private int maxHP = 60;
    private int currentHP;

    // 韧性系统（仅Boss/精英适用）
    [Header("Poise")]
    [SerializeField] private int maxPoise = 30;
    [SerializeField] private float poiseRecoveryTime = 2f;
    private int currentPoise;
    private float poiseTimer;

    public event Action<int, int> OnHPChanged;  // current, max
    public event Action OnDamaged;
    public event Action OnDeath;
    public event Action OnStaggered; // 韧性归零

    // IHealth
    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public bool IsInvincible { get; private set; }

    private void Awake()
    {
        currentHP = maxHP;
        currentPoise = maxPoise;
    }

    private void Update()
    {
        if (poiseTimer > 0)
        {
            poiseTimer -= Time.deltaTime;
            if (poiseTimer <= 0)
            {
                currentPoise = Mathf.Min(maxPoise, currentPoise + 1);
            }
        }
    }

    public void TakeDamage(int amount, GameObject source)
    {
        if (IsInvincible || currentHP <= 0) return;

        currentHP = Mathf.Max(0, currentHP - amount);
        OnHPChanged?.Invoke(currentHP, maxHP);
        OnDamaged?.Invoke();

        // 韧性处理
        currentPoise -= amount;
        poiseTimer = poiseRecoveryTime;
        if (currentPoise <= 0)
        {
            currentPoise = maxPoise;
            OnStaggered?.Invoke();
        }

        if (currentHP <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    public void SetInvincible(float duration)
    {
        IsInvincible = true;
        Invoke(nameof(ClearInvincible), duration);
    }

    private void ClearInvincible()
    {
        IsInvincible = false;
    }

    public void Die()
    {
        OnDeath?.Invoke();
        // 可选：播放死亡动画后销毁
    }

    /// <summary>
    /// 获取血量百分比 (0.0 - 1.0)
    /// </summary>
    public float HPRatio => (float)currentHP / maxHP;
}

public interface IHealth
{
    int CurrentHP { get; }
    int MaxHP { get; }
    bool IsInvincible { get; }
    void TakeDamage(int amount, GameObject source);
    void Heal(int amount);
    void Die();
    event Action OnDamaged;
    event Action OnDeath;
}
