using UnityEngine;
using System;

/// <summary>
/// Boss 基类。扩展 EnemyBase 增加多阶段管理。
/// </summary>
public class BossBase : EnemyBase
{
    [Header("Boss Info")]
    [SerializeField] protected string bossName = "无名Boss";
    [SerializeField] protected string bossTitle = "";
    [SerializeField] protected int phaseCount = 3;

    [Header("Boss Audio")]
    [SerializeField] protected AudioClip phaseTransitionSound;
    [SerializeField] protected AudioClip bossMusic;

    // Events
    public event Action<int> OnPhaseChanged;  // 传入新阶段编号 (1-based)
    public event Action<string> OnBossDefeated; // 传入Boss ID

    public string BossID => bossName;
    public int CurrentPhase { get; protected set; } = 1;

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
        health.OnDeath += HandleBossDeath;
    }

    /// <summary>
    /// 阶段转换。由 BossPhaseManager 或血量阈值触发。
    /// </summary>
    public virtual void TransitionToPhase(int phase)
    {
        if (phase <= CurrentPhase || phase > phaseCount) return;

        CurrentPhase = phase;

        if (phaseTransitionSound != null)
        {
            AudioManager.Instance?.PlaySFX(phaseTransitionSound);
        }

        // 阶段转换动画/特效
        animator?.SetInteger("Phase", phase);
        animator?.SetTrigger("PhaseTransition");

        // 回复少量血量
        health.Heal(30);

        OnPhaseChanged?.Invoke(phase);

        Debug.Log($"[{bossName}] 进入第 {phase} 阶段");
    }

    /// <summary>
    /// Boss 被击败。标记区域完成。
    /// </summary>
    protected virtual void HandleBossDeath()
    {
        if (IsDead) return;

        // 根据Boss ID 标记区域完成
        switch (bossName)
        {
            case "韩弩将军": GameManager.Instance.HasDefeatedHanBoss = true; break;
            case "赵盾之灵": GameManager.Instance.HasDefeatedZhaoBoss = true; break;
            case "魏武卒魂": GameManager.Instance.HasDefeatedWeiBoss = true; break;
            case "楚巫祭": GameManager.Instance.HasDefeatedChuBoss = true; break;
            case "荆轲执念": GameManager.Instance.HasDefeatedYanBoss = true; break;
            case "稷下魂": GameManager.Instance.HasDefeatedQiBoss = true; break;
            case "嬴政": /* Final boss, handled in ending */ break;
        }

        OnBossDefeated?.Invoke(BossID);
    }

    /// <summary>
    /// 开始 Boss 战（玩家进入触发区域时调用）
    /// </summary>
    public virtual void StartBossFight()
    {
        GameManager.Instance.SetGameState(GameState.BossFight);

        // 锁门
        // 播放 Boss 音乐
        if (bossMusic != null)
        {
            MusicManager.Instance?.PlayBossMusic(bossMusic);
        }

        // 显示 Boss 名称
        UIManager.Instance?.ShowBossName(bossName, bossTitle);
    }
}
