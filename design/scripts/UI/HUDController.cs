using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD 控制器。管理游戏内所有 UI 元素。
/// 通过事件监听玩家状态变化，不主动轮询。
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Qi / Stamina")]
    [SerializeField] private Slider qiSlider;
    [SerializeField] private TextMeshProUGUI qiText;

    [Header("Items")]
    [SerializeField] private TextMeshProUGUI healChargesText;
    [SerializeField] private TextMeshProUGUI yinCoinText;

    [Header("Boss HP")]
    [SerializeField] private GameObject bossHPContainer;
    [SerializeField] private Slider bossHPSlider;
    [SerializeField] private TextMeshProUGUI bossNameText;

    [Header("Interaction")]
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private TextMeshProUGUI interactionText;

    [Header("Notifications")]
    [SerializeField] private GameObject notificationPrefab;
    [SerializeField] private Transform notificationParent;

    // Listeners
    private PlayerHealth playerHealth;
    private PlayerQi playerQi;

    private void Awake()
    {
        // 隐藏 Boss HP
        if (bossHPContainer != null)
            bossHPContainer.SetActive(false);

        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);
    }

    private void Start()
    {
        var player = GameManager.Instance?.PlayerObject;
        if (player == null) return;

        playerHealth = player.GetComponent<PlayerHealth>();
        playerQi = player.GetComponent<PlayerQi>();

        // 注册事件
        if (playerHealth != null)
        {
            playerHealth.OnHPChanged += UpdateHealthUI;
            playerHealth.OnHealChargesChanged += UpdateHealChargesUI;
            UpdateHealthUI(playerHealth.CurrentHP, playerHealth.MaxHP);
            UpdateHealChargesUI();
        }

        if (playerQi != null)
        {
            playerQi.OnQiChanged += UpdateQiUI;
            UpdateQiUI(playerQi.CurrentQi, playerQi.MaxQi);
        }

        // 阴钱更新
        UpdateYinCoinsUI();
    }

    private void UpdateHealthUI(int current, int max)
    {
        if (healthSlider != null)
            healthSlider.value = (float)current / max;

        if (healthText != null)
            healthText.text = $"{current}/{max}";
    }

    private void UpdateQiUI(float current, float max)
    {
        if (qiSlider != null)
            qiSlider.value = current / max;

        if (qiText != null)
            qiText.text = $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(max)}";
    }

    private void UpdateHealChargesUI()
    {
        if (healChargesText != null && playerHealth != null)
            healChargesText.text = playerHealth.HealCharges.ToString();
    }

    public void UpdateYinCoinsUI()
    {
        if (yinCoinText != null && GameManager.Instance != null)
            yinCoinText.text = GameManager.Instance.YinCoinBalance.ToString();
    }

    /// <summary>
    /// 显示/隐藏交互提示
    /// </summary>
    public void ShowInteractionPrompt(bool show, string text = "")
    {
        if (interactionPrompt == null) return;
        interactionPrompt.SetActive(show);
        if (interactionText != null)
            interactionText.text = $"[F] {text}";
    }

    /// <summary>
    /// 显示 Boss 名称和 HP
    /// </summary>
    public void ShowBossHP(string bossName, int currentHP, int maxHP)
    {
        if (bossHPContainer == null) return;
        bossHPContainer.SetActive(true);
        if (bossNameText != null) bossNameText.text = bossName;

        if (bossHPSlider != null)
            bossHPSlider.value = (float)currentHP / maxHP;
    }

    public void HideBossHP()
    {
        if (bossHPContainer != null)
            bossHPContainer.SetActive(false);
    }

    /// <summary>
    /// 显示获得新能力的通知
    /// </summary>
    public void ShowAbilityNotification(string abilityName)
    {
        if (notificationPrefab == null) return;

        var notif = Instantiate(notificationPrefab, notificationParent);
        var text = notif.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
            text.text = $"习得能力: {abilityName}";

        Destroy(notif, 3f);
    }
}

/// <summary>
/// 简易 UIManager 单例，供其他系统调用。
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] private HUDController hud;
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private GameObject deathScreen;
    [SerializeField] private GameObject endingScreen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void ShowInteractionPrompt(bool show, string text = "")
    {
        hud?.ShowInteractionPrompt(show, text);
    }

    public void ShowBossName(string name, string title = "")
    {
        string display = string.IsNullOrEmpty(title) ? name : $"{name}\n{title}";
        hud?.ShowBossHP(display, 100, 100); // actual HP from HealthSystem
    }

    public void ShowAbilityNotification(string abilityName)
    {
        hud?.ShowAbilityNotification(abilityName);
    }

    public void ShowDeathScreen()
    {
        if (deathScreen != null)
            deathScreen.SetActive(true);
    }

    public void ShowPauseMenu()
    {
        if (pauseMenu != null)
            pauseMenu.SetActive(true);
    }

    public void HidePauseMenu()
    {
        if (pauseMenu != null)
            pauseMenu.SetActive(false);
    }
}
