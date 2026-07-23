using UnityEngine;

/// <summary>
/// 秦殇 — 独立版敌人。
/// 一个巡逻的韩弩兵。朝玩家行进，近战攻击。
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Enemy_Standalone : MonoBehaviour
{
    [Header("=== 属性 ===")]
    [SerializeField] private int maxHP = 60;
    [SerializeField] private int damage = 12;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float detectionRange = 6f;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackCooldown = 1.2f;

    [Header("=== 巡逻 ===")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float waitAtPoint = 1.5f;

    [Header("=== 视觉 ===")]
    [SerializeField] private Color aliveColor = Color.gray;
    [SerializeField] private Color hurtColor = Color.red;

    [Header("=== 掉落 ===")]
    [SerializeField] private int coinDrop = 10;

    public bool IsDead { get; private set; }

    private Rigidbody2D rb;
    private SpriteRenderer sprite;
    private Transform player;
    private int currentHP;

    private enum AIState { Patrol, Chase, Attack, Stagger, Death }
    private AIState state = AIState.Patrol;
    private int currentPatrolIndex;
    private float stateTimer;
    private float attackTimer;
    private float staggerTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        if (sprite == null)
        {
            sprite = gameObject.AddComponent<SpriteRenderer>();
            sprite.color = aliveColor;
        }
        currentHP = maxHP;
    }

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void Update()
    {
        if (IsDead) return;

        stateTimer -= Time.deltaTime;
        attackTimer = Mathf.Max(0, attackTimer - Time.deltaTime);
        staggerTimer = Mathf.Max(0, staggerTimer - Time.deltaTime);

        if (staggerTimer > 0) return; // 硬直中

        float distToPlayer = player != null ?
            Vector2.Distance(transform.position, player.position) : 999;

        switch (state)
        {
            case AIState.Patrol: UpdatePatrol(distToPlayer); break;
            case AIState.Chase: UpdateChase(distToPlayer); break;
            case AIState.Attack: UpdateAttack(distToPlayer); break;
        }

        // 面朝目标
        if (state == AIState.Chase || state == AIState.Attack)
        {
            float dir = player.position.x - transform.position.x;
            if (Mathf.Abs(dir) > 0.1f)
                transform.localScale = new Vector3(Mathf.Sign(dir), 1, 1);
        }
    }

    private void UpdatePatrol(float distToPlayer)
    {
        if (distToPlayer < detectionRange)
        {
            state = AIState.Chase;
            return;
        }

        if (patrolPoints == null || patrolPoints.Length == 0) return;

        Transform target = patrolPoints[currentPatrolIndex];
        float dist = Vector2.Distance(transform.position, target.position);

        if (dist < 0.5f)
        {
            if (stateTimer <= 0)
                stateTimer = waitAtPoint;
            else if (stateTimer <= waitAtPoint - 0.1f)
                return;
            else
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        }
        else
        {
            Vector2 dir = ((Vector2)target.position - (Vector2)transform.position).normalized;
            rb.linearVelocity = new Vector2(dir.x * patrolSpeed, rb.linearVelocity.y);
            transform.localScale = new Vector3(Mathf.Sign(dir.x), 1, 1);
        }
    }

    private void UpdateChase(float distToPlayer)
    {
        if (player == null) return;

        Vector2 dir = ((Vector2)player.position - (Vector2)transform.position).normalized;
        rb.linearVelocity = new Vector2(dir.x * moveSpeed, rb.linearVelocity.y);

        if (distToPlayer < attackRange && attackTimer <= 0)
        {
            state = AIState.Attack;
        }
    }

    private void UpdateAttack(float distToPlayer)
    {
        if (player == null) return;

        // 执行攻击
        var playerCtrl = player.GetComponent<PlayerController_Standalone>();
        if (playerCtrl != null)
        {
            playerCtrl.TakeDamage(damage);
            Debug.Log($"[敌人] 攻击玩家 — 伤害:{damage}");
        }

        attackTimer = attackCooldown;
        state = AIState.Chase;
    }

    //--- 受击 ---
    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        currentHP = Mathf.Max(0, currentHP - amount);
        HitStopEffect();
        StartCoroutine(DamageFlash());

        // 击退后进入短暂硬直
        state = AIState.Stagger;
        staggerTimer = 0.5f;
        rb.linearVelocity = Vector2.zero;
        Invoke(nameof(ReturnToChase), staggerTimer);

        Debug.Log($"[敌人] 受击 -{amount}HP → {currentHP}/{maxHP}");

        if (currentHP <= 0)
        {
            Die();
        }
    }

    public void ApplyKnockback(Vector2 force)
    {
        rb.linearVelocity = force;
    }

    private void ReturnToChase()
    {
        if (!IsDead) state = AIState.Chase;
    }

    private void Die()
    {
        IsDead = true;
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        sprite.color = Color.black;

        // 掉落阴钱
        var playerCtrl = player?.GetComponent<PlayerController_Standalone>();
        Debug.Log($"[敌人] 击败! +{coinDrop}阴钱");

        Destroy(gameObject, 1.5f);
    }

    private void HitStopEffect()
    {
        // 简单 hit-stop: 暂停动画 0.08 秒
        // 这里用协程模拟
        StartCoroutine(HitStopCoroutine());
    }

    private System.Collections.IEnumerator HitStopCoroutine()
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(0.08f);
        Time.timeScale = 1f;
    }

    private System.Collections.IEnumerator DamageFlash()
    {
        sprite.color = hurtColor;
        yield return new WaitForSeconds(0.1f);
        sprite.color = aliveColor;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (patrolPoints != null)
        {
            Gizmos.color = Color.cyan;
            foreach (var p in patrolPoints)
                if (p != null) Gizmos.DrawSphere(p.position, 0.2f);
        }
    }
}
