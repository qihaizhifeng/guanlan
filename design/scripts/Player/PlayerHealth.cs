using UnityEngine;
using System;

/// <summary>
/// 玩家生命值系统。管理气血、受伤、无敌帧、死亡。
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private int maxHP = 80;
    [SerializeField] private int currentHP;
    [SerializeField] private float invincibilityDuration = 1.0f;
    [SerializeField] private int healAmount = 35;
    [SerializeField] private int maxHealCharges = 3;

    [Header("State")]
    private int healCharges;
    private float invincibilityTimer;
    private bool isDead;

    // Events
    public event Action<int, int> OnHPChanged;   // current, max
    public event Action OnDamaged;
    public event Action OnHealed;
    public event Action OnDeath;
    public event Action OnHealChargesChanged;

    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public int HealCharges => healCharges;
    public bool IsDead => isDead;
    public bool IsInvincible => invincibilityTimer > 0;

    private void Awake()
    {
        currentHP = maxHP;
        healCharges = maxHealCharges;
    }

    private void Update()
    {
        if (invincibilityTimer > 0)
            invincibilityTimer -= Time.deltaTime;
    }

    public void TakeDamage(int amount, GameObject source)
    {
        if (IsInvincible || isDead) return;

        currentHP = Mathf.Max(0, currentHP - amount);
        invincibilityTimer = invincibilityDuration;

        OnHPChanged?.Invoke(currentHP, maxHP);
        OnDamaged?.Invoke();

        if (currentHP <= 0)
        {
            Die();
        }
    }

    public void Heal()
    {
        if (healCharges <= 0 || currentHP >= maxHP || isDead) return;

        healCharges--;
        currentHP = Mathf.Min(maxHP, currentHP + healAmount);

        OnHPChanged?.Invoke(currentHP, maxHP);
        OnHealed?.Invoke();
        OnHealChargesChanged?.Invoke();
    }

    public void RestoreAtBeacon()
    {
        currentHP = maxHP;
        healCharges = maxHealCharges;
        isDead = false;
        OnHPChanged?.Invoke(currentHP, maxHP);
        OnHealChargesChanged?.Invoke();
    }

    public void SetInvincible(float duration)
    {
        invincibilityTimer = Mathf.Max(invincibilityTimer, duration);
    }

    public void IncreaseMaxHP(int amount)
    {
        maxHP += amount;
        currentHP = Mathf.Min(currentHP + amount, maxHP);
        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    private void Die()
    {
        isDead = true;
        OnDeath?.Invoke();
    }
}
