using UnityEngine;
using System;

/// <summary>
/// 敌人基类。所有敌人（包括Boss）从此派生。
/// 提供 Move, PerformAttack, TakeDamage 接口，
/// 具体行为由子类 override 或通过 EnemyStateMachine 驱动。
/// </summary>
[RequireComponent(typeof(HealthSystem), typeof(Rigidbody2D))]
public class EnemyBase : MonoBehaviour
{
    [Header("Core Stats")]
    [SerializeField] protected string enemyName = "无名之魂";
    [SerializeField] protected int scoreValue = 10;      // 击杀死后获得的功德

    [Header("Components")]
    protected HealthSystem health;
    protected Rigidbody2D rb;
    protected Animator animator;
    protected SpriteRenderer spriteRenderer;

    [Header("State")]
    public bool IsExecutable { get; protected set; }
    public bool IsDead { get; protected set; }

    // Events
    public event Action OnDeath;

    protected virtual void Awake()
    {
        health = GetComponent<HealthSystem>();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    protected virtual void Start()
    {
        health.OnDeath += HandleDeath;
        health.OnStaggered += HandleStaggered;
    }

    /// <summary>
    /// 移动（由状态机或AI驱动）
    /// </summary>
    public virtual void Move(Vector2 velocity)
    {
        rb.linearVelocity = new Vector2(velocity.x, rb.linearVelocity.y);
        FaceDirection(velocity.x);
    }

    /// <summary>
    /// 执行攻击（由状态机调用）
    /// </summary>
    public virtual void PerformAttack(int damage)
    {
        animator?.SetTrigger("Attack");
        // 由 Animation Event 触发 HitBox
    }

    /// <summary>
    /// 受击。由 PlayerCombat 或弹反系统调用。
    /// </summary>
    public virtual void TakeDamage(int damage, GameObject source)
    {
        health.TakeDamage(damage, source);
        HitStop.Stop(0.08f);
        StartCoroutine(DamageFlash());
    }

    /// <summary>
    /// 设置可处决状态（通常由韧性归零触发）
    /// </summary>
    public virtual void SetExecutable(bool value)
    {
        IsExecutable = value;
        if (value)
        {
            spriteRenderer.color = Color.yellow; // 提示可处决
        }
        else
        {
            spriteRenderer.color = Color.white;
        }
    }

    /// <summary>
    /// 被处决时的特殊响应
    /// </summary>
    public virtual void OnExecuted()
    {
        // 由 PlayerCombat.Execute 调用，子类可 override 特殊动画
        health.TakeDamage(9999, null);
    }

    protected virtual void HandleDeath()
    {
        if (IsDead) return;
        IsDead = true;

        // 功德奖励
        GameManager.Instance.Merits += scoreValue;

        animator?.SetTrigger("Death");
        OnDeath?.Invoke();

        // 碰撞体禁用
        GetComponent<Collider2D>().enabled = false;
        rb.simulated = false;

        Destroy(gameObject, 2f);
    }

    protected virtual void HandleStaggered()
    {
        animator?.SetTrigger("Stagger");
        SetExecutable(true);
        Invoke(nameof(ClearExecutable), 2f);
    }

    private void ClearExecutable()
    {
        SetExecutable(false);
    }

    protected void FaceDirection(float xVelocity)
    {
        if (Mathf.Abs(xVelocity) > 0.1f)
        {
            transform.localScale = new Vector3(Mathf.Sign(xVelocity), 1, 1);
        }
        else if (GameManager.Instance?.PlayerObject != null)
        {
            // 面朝玩家
            float dir = GameManager.Instance.PlayerObject.transform.position.x - transform.position.x;
            if (Mathf.Abs(dir) > 0.1f)
                transform.localScale = new Vector3(Mathf.Sign(dir), 1, 1);
        }
    }

    private System.Collections.IEnumerator DamageFlash()
    {
        spriteRenderer.color = Color.white;
        yield return new WaitForSeconds(0.1f);
        if (!IsDead) spriteRenderer.color = Color.white;
    }
}
