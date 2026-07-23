using UnityEngine;

/// <summary>
/// 秦殇 — 独立版玩家控制器。
/// 不依赖 Unity Input System Package，仅用 Input.GetKey。
/// 新建 Unity 2D 项目后，把此文件拖进 Assets/Scripts/ 即可运行。
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerController_Standalone : MonoBehaviour
{
    [Header("=== 移动参数 ===")]
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private float jumpForce = 22f;
    [SerializeField] private float fallGravityMultiplier = 2.5f;

    [Header("=== 冲刺参数 ===")]
    [SerializeField] private float dashDistance = 5f;
    [SerializeField] private float dashDuration = 0.3f;
    [SerializeField] private int dashQiCost = 15;

    [Header("=== 攻击参数 ===")]
    [SerializeField] private int lightDamage = 15;
    [SerializeField] private int heavyDamage = 30;
    [SerializeField] private int lightQiCost = 12;
    [SerializeField] private int heavyQiCost = 30;
    [SerializeField] private float lightDuration = 0.25f;
    [SerializeField] private float heavyDuration = 0.6f;
    [SerializeField] private int blockQiCostPerHit = 5;
    [SerializeField] private float blockDamageReduction = 0.7f;
    [SerializeField] private int maxHealCharges = 3;
    [SerializeField] private int healAmount = 35;

    [Header("=== 气力 ===")]
    [SerializeField] private int maxQi = 100;
    [SerializeField] private float qiRecoveryRate = 25f;
    [SerializeField] private float qiRecoveryDelay = 0.5f;

    [Header("=== 碰撞检测 ===")]
    [SerializeField] private LayerMask groundLayer = 1;
    [SerializeField] private LayerMask enemyLayer = 1 << 6;
    [SerializeField] private float groundCheckDistance = 0.15f;
    [SerializeField] private float attackRange = 2f;

    //--- 运行时状态 ---
    private Rigidbody2D rb;
    private SpriteRenderer sprite;
    private Vector2 moveInput;

    // 战斗状态
    private int currentHP = 80;
    private int maxHP = 80;
    private float currentQi;
    private int healCharges;

    private bool isAttacking;
    private float attackTimer;
    private int comboStep;
    private float lastAttackTime;

    private bool isBlocking;
    private bool isDashing;
    private bool canDash = true;
    private bool isGrounded;
    private bool isDead;
    private float invincibleTimer;
    private float qiRecoveryTimer;

    //--- Unity 生命周期 ---
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        if (sprite == null) sprite = gameObject.AddComponent<SpriteRenderer>();

        currentHP = maxHP;
        currentQi = maxQi;
        healCharges = maxHealCharges;
    }

    private void Update()
    {
        if (isDead) return;
        HandleInput();
        UpdateTimers();
        UpdateGravity();
        ClampPosition();
        UpdateDebugDisplay();
    }

    private void FixedUpdate()
    {
        if (isDead) return;
        CheckGrounded();
        ApplyMovement();
        RecoverQi();
    }

    //--- 输入处理 ---
    private void HandleInput()
    {
        // 移动
        float h = Input.GetAxisRaw("Horizontal");
        moveInput = new Vector2(h, 0);

        // 面朝方向
        if (Mathf.Abs(h) > 0.01f)
            transform.localScale = new Vector3(Mathf.Sign(h), 1, 1);

        // 跳跃
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && !isDashing)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false;
        }

        // 冲刺
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash && !isGrounded && TryConsumeQi(dashQiCost))
        {
            StartDash();
        }

        // 轻攻击
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Z))
        {
            TryLightAttack();
        }

        // 重攻击
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.X))
        {
            TryHeavyAttack();
        }

        // 格挡（按住/松开）
        if (Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl))
        {
            if (!isBlocking && !isAttacking && !isDead)
            {
                isBlocking = true;
                moveSpeed = 6f; // 格挡时减速
            }
        }
        else
        {
            if (isBlocking)
            {
                isBlocking = false;
                moveSpeed = 12f;
            }
        }

        // 丹药
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Q))
        {
            TryHeal();
        }

        // 暂停
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("[暂停] 游戏暂停 (待实现菜单)");
        }
    }

    //--- 攻击 ---
    private void TryLightAttack()
    {
        if (isAttacking || isBlocking || isDead) return;
        if (!TryConsumeQi(lightQiCost)) return;

        isAttacking = true;
        attackTimer = lightDuration;
        comboStep = (comboStep % 3) + 1;
        lastAttackTime = Time.time;

        int dmg = lightDamage * comboStep;
        PerformHit(dmg, 0.3f, false);

        Debug.Log($"[攻击] 轻击{comboStep}段 — 伤害:{dmg} 气力:{currentQi:F0}");
    }

    private void TryHeavyAttack()
    {
        if (isAttacking || isBlocking || isDead) return;
        if (!TryConsumeQi(heavyQiCost)) return;

        isAttacking = true;
        attackTimer = heavyDuration;
        lastAttackTime = Time.time;

        PerformHit(heavyDamage, 0.7f, true);
        Debug.Log($"[攻击] 重击 — 伤害:{heavyDamage} 气力:{currentQi:F0}");
    }

    private void PerformHit(int damage, float knockbackForce, bool isHeavy)
    {
        Vector2 origin = (Vector2)transform.position + Vector2.right * transform.localScale.x * 1f;
        Vector2 size = new Vector2(isHeavy ? 2.5f : 1.8f, 1.2f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(origin, size, 0, enemyLayer);

        foreach (var hit in hits)
        {
            var enemy = hit.GetComponent<Enemy_Standalone>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                Vector2 kb = new Vector2(transform.localScale.x * knockbackForce * 3f, 1f);
                enemy.ApplyKnockback(kb);
            }
        }
    }

    //--- 格挡 ---
    public int BlockDamage(int incomingDamage)
    {
        if (!isBlocking) return incomingDamage;

        int reduced = Mathf.RoundToInt(incomingDamage * (1 - blockDamageReduction));
        TryConsumeQi(blockQiCostPerHit);
        return reduced;
    }

    //--- 冲刺 ---
    private void StartDash()
    {
        isDashing = true;
        rb.gravityScale = 0;
        rb.linearVelocity = new Vector2(transform.localScale.x * dashDistance, 0);
        invincibleTimer = dashDuration;
        Invoke(nameof(EndDash), dashDuration);
    }

    private void EndDash()
    {
        isDashing = false;
    }

    //--- 丹药 ---
    private void TryHeal()
    {
        if (healCharges <= 0 || currentHP >= maxHP || isDead) return;

        healCharges--;
        currentHP = Mathf.Min(maxHP, currentHP + healAmount);
        Debug.Log($"[丹药] +{healAmount}HP → {currentHP}/{maxHP} 剩余:{healCharges}");
    }

    //--- 气力 ---
    private bool TryConsumeQi(int amount)
    {
        if (currentQi < amount) return false;
        currentQi -= amount;
        qiRecoveryTimer = qiRecoveryDelay;
        return true;
    }

    private void RecoverQi()
    {
        if (qiRecoveryTimer > 0)
        {
            qiRecoveryTimer -= Time.fixedDeltaTime;
            return;
        }
        if (currentQi < maxQi)
            currentQi = Mathf.Min(maxQi, currentQi + qiRecoveryRate * Time.fixedDeltaTime);
    }

    //--- 受伤 ---
    public void TakeDamage(int amount)
    {
        if (invincibleTimer > 0 || isDead) return;

        if (isBlocking)
            amount = BlockDamage(amount);

        currentHP = Mathf.Max(0, currentHP - amount);
        invincibleTimer = 1.0f;

        // 闪白
        if (sprite != null)
            StartCoroutine(DamageFlash());

        Debug.Log($"[受伤] -{amount}HP → {currentHP}/{maxHP}");

        if (currentHP <= 0)
            Die();
    }

    private void Die()
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        sprite.color = Color.red;

        Debug.Log("[死亡] 5秒后复活");
        Invoke(nameof(Respawn), 5f);
    }

    private void Respawn()
    {
        isDead = false;
        rb.simulated = true;
        currentHP = maxHP;
        currentQi = maxQi;
        healCharges = maxHealCharges;
        sprite.color = Color.white;
        transform.position = Vector2.zero;
        Debug.Log("[复活] 状态回满");
    }

    //--- 物理 ---
    private void CheckGrounded()
    {
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position, Vector2.down, groundCheckDistance, groundLayer);
        isGrounded = hit.collider != null;
        if (isGrounded) canDash = true;
    }

    private void ApplyMovement()
    {
        if (isDashing || isAttacking) return;
        float targetVelX = moveInput.x * moveSpeed;
        rb.linearVelocity = new Vector2(
            Mathf.MoveTowards(rb.linearVelocity.x, targetVelX, 60 * Time.fixedDeltaTime),
            rb.linearVelocity.y);
    }

    private void UpdateGravity()
    {
        if (!isDashing && rb.linearVelocity.y < 0)
            rb.gravityScale = fallGravityMultiplier;
        else if (!isDashing)
            rb.gravityScale = 1f;
    }

    private void UpdateTimers()
    {
        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0) isAttacking = false;
        }
        if (Time.time - lastAttackTime > 0.5f) comboStep = 0;
        if (invincibleTimer > 0) invincibleTimer -= Time.deltaTime;
    }

    private void ClampPosition()
    {
        // 防止掉出世界边界
        Vector3 pos = transform.position;
        if (pos.y < -50f)
        {
            currentHP = 0;
            Die();
        }
    }

    private System.Collections.IEnumerator DamageFlash()
    {
        sprite.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        sprite.color = Color.white;
    }

    //--- HUD 文字 ---
    private void UpdateDebugDisplay()
    {
        // 在 Scene 窗口顶部显示状态（运行时能看到）
        string status = $"HP:{currentHP}/{maxHP} QI:{currentQi:F0}/{maxQi} 丹:{healCharges}";
        if (isBlocking) status += " [格挡]";
        if (isDashing) status += " [冲刺]";
        if (isDead) status += " [死亡]";
    }

    private void OnGUI()
    {
        // 运行时屏幕左上显示状态
        GUIStyle style = new GUIStyle();
        style.fontSize = 18;
        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Bold;

        GUI.Label(new Rect(20, 20, 400, 30),
            $"气血:{currentHP}/{maxHP}  气力:{currentQi:F0}/{maxQi}  丹药:{healCharges}");

        GUI.Label(new Rect(20, 45, 400, 30),
            $"    [W跳] [左/右键攻击] [C格挡] [Shift冲刺] [E恢复]");

        if (isBlocking)
            GUI.Label(new Rect(20, 70, 200, 30), "<< 格挡中 >>");

        if (invincibleTimer > 0 && !isBlocking)
            GUI.Label(new Rect(20, 70, 200, 30), "<< 无敌帧 >>");

        if (isDead)
        {
            style.fontSize = 48;
            style.normal.textColor = Color.red;
            GUI.Label(new Rect(Screen.width/2-100, Screen.height/2-30, 400, 60), "魂归");
        }
    }

    private void OnDrawGizmosSelected()
    {
        // 可视化攻击判定框
        Gizmos.color = Color.red;
        Vector2 origin = (Vector2)transform.position + Vector2.right * transform.localScale.x * 1f;
        Gizmos.DrawWireCube(origin, new Vector2(1.8f, 1.2f));

        // 地面检测
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * groundCheckDistance);
    }
}
