using UnityEngine;
using System;

/// <summary>
/// 气力（类魂体力条）管理。消耗后自动恢复，不足时阻止动作。
/// </summary>
public class PlayerQi : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private int maxQi = 100;
    [SerializeField] private float currentQi;
    [SerializeField] private float recoveryRate = 25f;    // 每秒恢复
    [SerializeField] private float recoveryDelay = 0.5f;  // 停止消耗后的延迟

    private float recoveryTimer;

    public event Action<float, float> OnQiChanged; // current, max
    public event Action OnQiDepleted;              // 气力耗尽时触发

    public float CurrentQi => currentQi;
    public float MaxQi => maxQi;
    public float Ratio => currentQi / maxQi;

    private void Awake()
    {
        currentQi = maxQi;
    }

    private void Update()
    {
        if (recoveryTimer > 0)
        {
            recoveryTimer -= Time.deltaTime;
        }
        else if (currentQi < maxQi)
        {
            currentQi = Mathf.Min(maxQi, currentQi + recoveryRate * Time.deltaTime);
            OnQiChanged?.Invoke(currentQi, maxQi);
        }
    }

    /// <summary>
    /// 尝试消耗气力。返回是否成功。
    /// </summary>
    public bool TryConsume(int amount)
    {
        if (currentQi < amount) return false;

        currentQi -= amount;
        recoveryTimer = recoveryDelay;

        OnQiChanged?.Invoke(currentQi, maxQi);

        if (currentQi <= 0)
            OnQiDepleted?.Invoke();

        return true;
    }

    public void IncreaseMaxQi(int amount)
    {
        maxQi += amount;
        currentQi = Mathf.Min(currentQi + amount, maxQi);
        OnQiChanged?.Invoke(currentQi, maxQi);
    }

    public void Restore()
    {
        currentQi = maxQi;
        OnQiChanged?.Invoke(currentQi, maxQi);
    }
}
