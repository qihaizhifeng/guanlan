using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家移动/跳跃/冲刺控制器。
/// 只负责输入驱动的物理运动，不处理战斗逻辑。
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(PlayerHealth), typeof(PlayerQi))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private float acceleration = 60f;
    [SerializeField] private float groundDeceleration = 40f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 22f;
    [SerializeField] private float fallGravityMultiplier = 2.5f;
    [SerializeField] private float groundCheckDistance = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Dash")]
    [SerializeField] private float dashDistance = 5f;
    [SerializeField] private float dashDuration = 0.3f;
    [SerializeField] private int dashQiCost = 15;

    [Header("Components")]
    private Rigidbody2D rb;
    private PlayerHealth health;
    private PlayerQi qi;
    private PlayerCombat combat;
    private Animator animator;

    // State
    private Vector2 moveInput;
    private bool isDashing;
    private bool isBlocking;
    private bool isGrounded;
    private bool canDash = true;

    // Input cache
    private PlayerInputActions inputActions;

    public bool IsGrounded => isGrounded;
    public bool IsDashing => isDashing;
    public bool IsBlocking => isBlocking;
    public Vector2 MoveDirection => moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<PlayerHealth>();
        qi = GetComponent<PlayerQi>();
        combat = GetComponent<PlayerCombat>();
        animator = GetComponent<Animator>();

        // 初始化 Input System
        inputActions = new PlayerInputActions();
        inputActions.Gameplay.Enable();
    }

    private void OnEnable()
    {
        inputActions?.Gameplay.Enable();
    }

    private void OnDisable()
    {
        inputActions?.Gameplay.Disable();
    }

    private void Update()
    {
        ReadInput();
        UpdateAnimationState();
    }

    private void FixedUpdate()
    {
        CheckGrounded();
        ApplyMovement();
        ApplyFallGravity();
    }

    private void ReadInput()
    {
        // 移动
        moveInput = inputActions.Gameplay.Move.ReadValue<Vector2>();

        // 跳跃
        if (inputActions.Gameplay.Jump.WasPressedThisFrame() && isGrounded && !isDashing)
        {
            Jump();
        }

        // 冲刺
        if (inputActions.Gameplay.Dash.WasPressedThisFrame() && canDash && !isGrounded && qi.TryConsume(dashQiCost))
        {
            StartDash();
        }
    }

    private void CheckGrounded()
    {
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position, Vector2.down, groundCheckDistance, groundLayer);
        isGrounded = hit.collider != null;

        if (isGrounded) canDash = true;
    }

    private void ApplyMovement()
    {
        if (isDashing || combat.IsAttacking) return;

        float targetVelX = moveInput.x * moveSpeed;
        float accel = Mathf.Abs(moveInput.x) > 0.1f ? acceleration : groundDeceleration;

        rb.linearVelocity = new Vector2(
            Mathf.MoveTowards(rb.linearVelocity.x, targetVelX, accel * Time.fixedDeltaTime),
            rb.linearVelocity.y);
    }

    private void ApplyFallGravity()
    {
        if (rb.linearVelocity.y < 0)
        {
            rb.gravityScale = fallGravityMultiplier;
        }
        else
        {
            rb.gravityScale = 1f;
        }
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    private void StartDash()
    {
        isDashing = true;
        rb.gravityScale = 0;
        rb.linearVelocity = new Vector2(
            (moveInput.x != 0 ? Mathf.Sign(moveInput.x) : transform.localScale.x) * dashDistance,
            0);

        // 无敌帧
        health.SetInvincible(dashDuration);

        Invoke(nameof(EndDash), dashDuration);
    }

    private void EndDash()
    {
        isDashing = false;
    }

    private void UpdateAnimationState()
    {
        if (animator == null) return;

        animator.SetBool("isGrounded", isGrounded);
        animator.SetBool("isMoving", Mathf.Abs(moveInput.x) > 0.1f);
        animator.SetBool("isDashing", isDashing);
        animator.SetFloat("verticalSpeed", rb.linearVelocity.y);

        // 面朝方向
        if (Mathf.Abs(moveInput.x) > 0.1f)
        {
            transform.localScale = new Vector3(Mathf.Sign(moveInput.x), 1, 1);
        }
    }

    /// <summary>
    /// 被击退。由 CombatSystem 或受击时调用。
    /// </summary>
    public void ApplyKnockback(Vector2 force)
    {
        rb.linearVelocity = force;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * groundCheckDistance);
    }
}
