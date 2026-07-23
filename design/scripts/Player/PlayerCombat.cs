using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家战斗系统：轻击、重击、格挡、处决。
/// 不掌管气力——通过 PlayerQi.TryConsume 请求消耗。
/// </summary>
[RequireComponent(typeof(PlayerController), typeof(PlayerQi), typeof(PlayerHealth))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Attack Stats")]
    [SerializeField] private int lightDamage = 15;
    [SerializeField] private int heavyDamage = 30;
    [SerializeField] private int lightQiCost = 12;
    [SerializeField] private int heavyQiCost = 30;
    [SerializeField] private float lightAttackDuration = 0.25f;
    [SerializeField] private float heavyAttackDuration = 0.6f;

    [Header("Combo")]
    [SerializeField] private int maxLightCombo = 3;
    private int currentComboStep;

    [Header("Block")]
    [SerializeField] private int blockQiCostPerHit = 5;
    [SerializeField] private float blockDamageReduction = 0.7f;
    [SerializeField] private float perfectParryWindow = 0.15f;

    [Header("Executions")]
    [SerializeField] private int executionDamage = 999; // fixed damage
    [SerializeField] private int executionHealAmount = 10;

    [Header("Components")]
    private PlayerController controller;
    private PlayerQi qi;
    private PlayerHealth health;
    private PlayerAbilities abilities;
    private Animator animator;
    private PlayerInputActions inputActions;

    // State
    private bool isAttacking;
    private bool isBlocking;
    private float attackTimer;
    private float lastAttackTime;
    private EnemyBase lockTarget;

    public bool IsAttacking => isAttacking;
    public int CurrentComboStep => currentComboStep;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        qi = GetComponent<PlayerQi>();
        health = GetComponent<PlayerHealth>();
        abilities = GetComponent<PlayerAbilities>();
        animator = GetComponent<Animator>();

        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Gameplay.LightAttack.performed += _ => TryLightAttack();
        inputActions.Gameplay.HeavyAttack.performed += _ => TryHeavyAttack();
        inputActions.Gameplay.Block.performed += _ => StartBlock();
        inputActions.Gameplay.Block.canceled += _ => EndBlock();
        inputActions.Gameplay.Heal.performed += _ => TryHeal();
        inputActions.Gameplay.Enable();
    }

    private void OnDisable()
    {
        inputActions.Gameplay.LightAttack.performed -= _ => TryLightAttack();
        inputActions.Gameplay.HeavyAttack.performed -= _ => TryHeavyAttack();
        inputActions.Gameplay.Block.performed -= _ => StartBlock();
        inputActions.Gameplay.Block.canceled -= _ => EndBlock();
        inputActions.Gameplay.Heal.performed -= _ => TryHeal();
        inputActions.Gameplay.Disable();
    }

    private void Update()
    {
        // 攻击计时
        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
            {
                isAttacking = false;
            }
        }

        // 连击超时重置
        if (Time.time - lastAttackTime > 0.5f)
        {
            currentComboStep = 0;
        }
    }

    private void TryLightAttack()
    {
        if (isAttacking || isBlocking || health.IsDead) return;
        if (!qi.TryConsume(lightQiCost)) return;

        isAttacking = true;
        attackTimer = lightAttackDuration;
        currentComboStep = (currentComboStep % maxLightCombo) + 1;
        lastAttackTime = Time.time;

        animator?.SetTrigger("LightAttack");
        animator?.SetInteger("ComboStep", currentComboStep);

        // 触发 HitBox
        PerformHit(lightDamage * currentComboStep);
    }

    private void TryHeavyAttack()
    {
        if (isAttacking || isBlocking || health.IsDead) return;
        if (!qi.TryConsume(heavyQiCost)) return;

        isAttacking = true;
        attackTimer = heavyAttackDuration;
        lastAttackTime = Time.time;

        animator?.SetTrigger("HeavyAttack");

        PerformHit(heavyDamage, knockback: true);
    }

    private void StartBlock()
    {
        if (health.IsDead) return;
        isBlocking = true;
        controller.enabled = false;
        animator?.SetBool("isBlocking", true);
    }

    private void EndBlock()
    {
        isBlocking = false;
        controller.enabled = true;
        animator?.SetBool("isBlocking", false);
    }

    /// <summary>
    /// 格挡伤害。由外部（敌人攻击）调用。
    /// 返回实际受到的伤害（格挡后）。
    /// </summary>
    public int BlockDamage(int incomingDamage)
    {
        if (!isBlocking) return incomingDamage;

        int reduced = Mathf.RoundToInt(incomingDamage * (1 - blockDamageReduction));
        qi.TryConsume(blockQiCostPerHit);

        animator?.SetTrigger("BlockHit");
        return reduced;
    }

    private void TryHeal()
    {
        if (isAttacking || health.IsDead) return;
        health.Heal();
    }

    /// <summary>
    /// 执行处决（仅敌人处于可处决状态时调用）
    /// </summary>
    public void Execute(EnemyBase enemy)
    {
        if (!enemy.IsExecutable) return;

        enemy.TakeDamage(executionDamage, gameObject);
        health.Heal(); // 处决回复固定气血

        animator?.SetTrigger("Execute");
    }

    private void PerformHit(int damage, bool knockback = false)
    {
        // HitBox 碰撞逻辑由单独的 HitBox 组件或动画事件驱动
        // 此处触发事件，让 HitBox 系统处理判定
        // 实际实现中，可发射射线检测前方敌人
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position + (Vector3)GetAttackOffset(),
            Vector2.right * transform.localScale.x,
            2f,
            LayerMask.GetMask("Enemy"));

        if (hit.collider != null)
        {
            var enemy = hit.collider.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, gameObject);
                if (knockback)
                    controller.ApplyKnockback(new Vector2(-hit.normal.x * 3f, 1f));
            }
        }
    }

    private Vector2 GetAttackOffset()
    {
        return new Vector2(transform.localScale.x * 0.8f, 0.2f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector2 origin = Application.isPlaying ?
            (Vector2)transform.position + GetAttackOffset() :
            (Vector2)transform.position + new Vector2(1, 0.2f);
        Gizmos.DrawRay(origin, Vector2.right * 2f);
    }
}
