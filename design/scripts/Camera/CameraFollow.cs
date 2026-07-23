using UnityEngine;

/// <summary>
/// 摄像机跟随。支持平滑跟随、LookAhead、边界限制、震动。
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 offset = new Vector2(0, 0.5f);

    [Header("Follow")]
    [SerializeField] private float smoothSpeed = 8f;
    [SerializeField] private float lookAheadX = 1.5f;
    [SerializeField] private float lookAheadSpeed = 3f;

    [Header("Bounds")]
    [SerializeField] private bool useBounds;
    [SerializeField] private Vector2 minBounds;
    [SerializeField] private Vector2 maxBounds;

    [Header("Shake")]
    private float shakeIntensity;
    private float shakeDuration;
    private Vector3 shakeOffset;

    private float currentLookAheadX;
    private float targetLookAheadX;

    private void LateUpdate()
    {
        if (target == null) return;

        // LookAhead: 根据玩家朝向和速度提前偏移
        var playerController = target.GetComponent<PlayerController>();
        if (playerController != null)
        {
            float moveDir = playerController.MoveDirection.x;
            targetLookAheadX = Mathf.Lerp(0, lookAheadX * Mathf.Sign(moveDir), 
                Mathf.Abs(moveDir));
        }
        currentLookAheadX = Mathf.Lerp(currentLookAheadX, targetLookAheadX,
            lookAheadSpeed * Time.deltaTime);

        // 目标位置
        Vector3 desiredPos = target.position + (Vector3)offset + 
            new Vector3(currentLookAheadX, 0, 0);
        desiredPos.z = -10; // 固定Z

        // 平滑跟随
        Vector3 smoothedPos = Vector3.Lerp(transform.position, desiredPos, 
            smoothSpeed * Time.deltaTime);

        // 边界限制
        if (useBounds)
        {
            smoothedPos.x = Mathf.Clamp(smoothedPos.x, minBounds.x, maxBounds.x);
            smoothedPos.y = Mathf.Clamp(smoothedPos.y, minBounds.y, maxBounds.y);
        }

        // 震动
        if (shakeDuration > 0)
        {
            shakeOffset = Random.insideUnitSphere * shakeIntensity;
            shakeOffset.z = 0;
            shakeDuration -= Time.deltaTime;
        }
        else
        {
            shakeOffset = Vector3.zero;
        }

        transform.position = smoothedPos + shakeOffset;
    }

    /// <summary>
    /// 触发摄像机震动
    /// </summary>
    public void Shake(float intensity, float duration)
    {
        shakeIntensity = intensity;
        shakeDuration = duration;
    }

    public void SetBounds(Vector2 min, Vector2 max)
    {
        minBounds = min;
        maxBounds = max;
        useBounds = true;
    }

    public void DisableBounds()
    {
        useBounds = false;
    }
}

// CameraShake — 方便外部调用的静态方法
public static class CameraShake
{
    public static void Trigger(float intensity = 0.3f, float duration = 0.2f)
    {
        CameraFollow cam = Camera.main?.GetComponent<CameraFollow>();
        cam?.Shake(intensity, duration);
    }
}
