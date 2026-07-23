using UnityEngine;

/// <summary>
/// 烽火台 — 存档点/恢复点。
/// 玩家交互后：回满状态、存档、重置敌人。
/// 对应 Hollow Knight 的"长椅"和魂系的"篝火"。
/// </summary>
public class BeaconTower : MonoBehaviour
{
    [Header("Tower Info")]
    [SerializeField] private string beaconName = "无名烽燧";
    [SerializeField] private string sceneName;  // 自动填充

    [Header("Visuals")]
    [SerializeField] private GameObject unlitFlame;
    [SerializeField] private GameObject litFlame;
    [SerializeField] private ParticleSystem lightingEffect;
    [SerializeField] private Color unlitColor = Color.gray;
    [SerializeField] private Color litColor = Color.white;

    [Header("Interaction")]
    [SerializeField] private float interactionRadius = 1.5f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    [Header("Audio")]
    [SerializeField] private AudioClip igniteSound;
    [SerializeField] private AudioClip restSound;

    private bool isLit;
    private bool isPlayerInRange;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (string.IsNullOrEmpty(sceneName))
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        unlitFlame?.SetActive(true);
        litFlame?.SetActive(false);
        spriteRenderer.color = unlitColor;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (playerLayer == (playerLayer | 1 << other.gameObject.layer))
        {
            isPlayerInRange = true;
            ShowInteractionPrompt(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (playerLayer == (playerLayer | 1 << other.gameObject.layer))
        {
            isPlayerInRange = false;
            ShowInteractionPrompt(false);
        }
    }

    private void Update()
    {
        if (isPlayerInRange && Input.GetKeyDown(interactKey))
        {
            Interact();
        }
    }

    private void Interact()
    {
        if (!isLit)
        {
            Ignite();
        }

        Rest();
    }

    /// <summary>
    /// 首次点燃烽火台（只有首次有点燃动画）
    /// </summary>
    private void Ignite()
    {
        isLit = true;

        unlitFlame?.SetActive(false);
        litFlame?.SetActive(true);
        spriteRenderer.color = litColor;

        if (lightingEffect != null)
            lightingEffect.Play();

        if (igniteSound != null)
            AudioManager.Instance?.PlaySFX(igniteSound);

        // 标记为已激活（存档用）
        GameManager.Instance.LastBeaconPosition = transform.position;
        GameManager.Instance.LastBeaconScene = sceneName;
    }

    /// <summary>
    /// 休息（不论是否已点燃均执行）
    /// </summary>
    private void Rest()
    {
        var player = GameManager.Instance.PlayerObject;
        if (player == null) return;

        // 回满状态
        player.GetComponent<PlayerHealth>()?.RestoreAtBeacon();
        player.GetComponent<PlayerQi>()?.Restore();

        // 更新重生点
        GameManager.Instance.LastBeaconPosition = transform.position;
        GameManager.Instance.LastBeaconScene = sceneName;

        // 重置敌人
        EnemyRespawner[] enemies = FindObjectsOfType<EnemyRespawner>();
        foreach (var respawner in enemies)
            respawner.Respawn();

        // 自动存档
        SaveManager.Instance?.SaveGame();

        if (restSound != null)
            AudioManager.Instance?.PlaySFX(restSound);

        // 粒子效果（短暂的篝火上涌）
        Debug.Log($"[烽火台] {beaconName} — 已休息，状态恢复");
    }

    private void ShowInteractionPrompt(bool show)
    {
        // 通过 UI 事件显示"按 F 点燃烽火台"提示
        UIManager.Instance?.ShowInteractionPrompt(show, "点燃烽火台/休息");
    }

    public void ForceLit()
    {
        // 加载存档时调用，恢复已点燃状态
        isLit = true;
        unlitFlame?.SetActive(false);
        litFlame?.SetActive(true);
        spriteRenderer.color = litColor;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
