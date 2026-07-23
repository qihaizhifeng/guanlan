using UnityEngine;

/// <summary>
/// Boss 阶段管理器。根据血量百分比触发阶段转换。
/// PhaseManager 不关心"当前阶段做什么"——那是 BossAI 的事。
/// PhaseManager 只负责"什么时候转到下一阶段"。
/// </summary>
public class BossPhaseManager : MonoBehaviour
{
    [System.Serializable]
    public class PhaseConfig
    {
        public int phaseNumber;                   // 1-based
        [Range(0f, 1f)] public float triggerAtHPRatio;  // 如 0.7 = 70%血量时触发
        public string phaseName;
        [TextArea] public string phaseDescription;
        public bool locksArenaDoors;
        public bool restoresBossHP;
    }

    [SerializeField] private PhaseConfig[] phases;
    [SerializeField] private BossBase boss;

    private int currentPhaseIndex;
    private bool hasBeenTriggered;

    private void Awake()
    {
        if (boss == null) boss = GetComponent<BossBase>();
    }

    private void Start()
    {
        // 按血量降序排序
        System.Array.Sort(phases, (a, b) => b.triggerAtHPRatio.CompareTo(a.triggerAtHPRatio));
    }

    private void Update()
    {
        if (boss == null || boss.IsDead) return;

        float ratio = GetComponent<HealthSystem>()?.HPRatio ?? 1f;

        // 从高到低检查是否满足阶段触发条件
        for (int i = 0; i < phases.Length; i++)
        {
            var phase = phases[i];
            if (phase.phaseNumber > currentPhaseIndex && ratio <= phase.triggerAtHPRatio)
            {
                TriggerPhase(phase);
                currentPhaseIndex = phase.phaseNumber;
                break;
            }
        }
    }

    private void TriggerPhase(PhaseConfig config)
    {
        boss.TransitionToPhase(config.phaseNumber);
        Debug.Log($"[PhaseManager] {boss.name} 进入阶段 {config.phaseNumber}: {config.phaseName}");
    }
}
