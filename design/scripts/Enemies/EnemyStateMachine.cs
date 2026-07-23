using UnityEngine;

/// <summary>
/// 通用AI状态机。支持状态切换和条件触发。
/// </summary>
public class EnemyStateMachine : MonoBehaviour
{
    public EnemyState CurrentState { get; private set; } = EnemyState.Idle;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 6f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float loseInterestRange = 12f;

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolSpeed = 3f;
    [SerializeField] private float waitAtPointDuration = 1.5f;

    [Header("Chase")]
    [SerializeField] private float chaseSpeed = 5f;

    [Header("Attack")]
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private int attackDamage = 10;

    [Header("Retreat")]
    [SerializeField] private float retreatDuration = 1.5f;
    [SerializeField] private float retreatSpeed = 4f;

    private Transform player;
    private EnemyBase enemyBase;
    private HealthSystem health;
    private int currentPatrolIndex;
    private float stateTimer;
    private float attackTimer;
    private Vector2 retreatDirection;
    private Vector2 startPosition;

    public Transform Player => player;

    private void Awake()
    {
        enemyBase = GetComponent<EnemyBase>();
        health = GetComponent<HealthSystem>();
        startPosition = transform.position;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void Start()
    {
        if (patrolPoints.Length > 0)
            ChangeState(EnemyState.Patrol);
        else
            ChangeState(EnemyState.Idle);
    }

    private void Update()
    {
        stateTimer -= Time.deltaTime;
        attackTimer = Mathf.Max(0, attackTimer - Time.deltaTime);

        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        switch (CurrentState)
        {
            case EnemyState.Idle:
                UpdateIdle(distanceToPlayer);
                break;
            case EnemyState.Patrol:
                UpdatePatrol(distanceToPlayer);
                break;
            case EnemyState.Alert:
                UpdateAlert();
                break;
            case EnemyState.Chase:
                UpdateChase(distanceToPlayer);
                break;
            case EnemyState.Attack:
                UpdateAttack(distanceToPlayer);
                break;
            case EnemyState.Stagger:
                UpdateStagger();
                break;
            case EnemyState.Retreat:
                UpdateRetreat(distanceToPlayer);
                break;
            case EnemyState.Death:
                UpdateDeath();
                break;
        }
    }

    private void ChangeState(EnemyState newState)
    {
        CurrentState = newState;
        stateTimer = 0;
    }

    private void UpdateIdle(float distance)
    {
        if (distance < detectionRange)
            ChangeState(EnemyState.Alert);
    }

    private void UpdatePatrol(float distance)
    {
        if (distance < detectionRange)
        {
            ChangeState(EnemyState.Alert);
            return;
        }

        if (patrolPoints.Length == 0) return;

        Transform target = patrolPoints[currentPatrolIndex];
        float dist = Vector2.Distance(transform.position, target.position);

        if (dist < 0.5f)
        {
            if (stateTimer <= 0)
            {
                stateTimer = waitAtPointDuration;
            }
            else if (stateTimer <= waitAtPointDuration - 0.1f)
            {
                // 仍在等待
            }
            else
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            }
        }
        else
        {
            Vector2 dir = ((Vector2)target.position - (Vector2)transform.position).normalized;
            enemyBase.Move(dir * patrolSpeed);
        }
    }

    private void UpdateAlert()
    {
        // 警觉动画播放（0.3-0.5s后进入追击）
        if (stateTimer <= 0) stateTimer = 0.4f;
        if (stateTimer <= 0.1f)
            ChangeState(EnemyState.Chase);
    }

    private void UpdateChase(float distance)
    {
        if (distance > loseInterestRange)
        {
            ChangeState(EnemyState.Retreat);
            retreatDirection = (startPosition - (Vector2)transform.position).normalized;
            return;
        }

        Vector2 dir = ((Vector2)player.position - (Vector2)transform.position).normalized;
        enemyBase.Move(dir * chaseSpeed);

        if (distance < attackRange && attackTimer <= 0)
        {
            ChangeState(EnemyState.Attack);
        }
    }

    private void UpdateAttack(float distance)
    {
        // 执行攻击
        enemyBase.PerformAttack(attackDamage);

        attackTimer = attackCooldown;

        if (distance > attackRange * 1.2f)
            ChangeState(EnemyState.Chase);
        else
            ChangeState(EnemyState.Chase);
    }

    private void UpdateStagger()
    {
        if (stateTimer <= 0)
        {
            stateTimer = 1.0f;
            ChangeState(EnemyState.Chase);
        }
    }

    private void UpdateRetreat(float distance)
    {
        if (distance > loseInterestRange)
        {
            ChangeState(EnemyState.Patrol);
            return;
        }

        enemyBase.Move(retreatDirection * retreatSpeed);
    }

    private void UpdateDeath()
    {
        // 死亡逻辑由 EnemyBase 处理
    }

    /// <summary>
    /// 外部调用：进入硬直
    /// </summary>
    public void OnStaggered()
    {
        ChangeState(EnemyState.Stagger);
        stateTimer = 1.0f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, loseInterestRange);
    }
}
