using UnityEngine;

/// <summary>
/// 全局游戏状态管理者。单一入口，不随场景加载销毁。
/// 所有子系统通过它访问存档、玩家引用、游戏状态。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    public GameState State { get; private set; } = GameState.Menu;

    [Header("Player State")]
    public bool HasDefeatedHanBoss { get; set; }
    public bool HasDefeatedZhaoBoss { get; set; }
    public bool HasDefeatedWeiBoss { get; set; }
    public bool HasDefeatedChuBoss { get; set; }
    public bool HasDefeatedYanBoss { get; set; }
    public bool HasDefeatedQiBoss { get; set; }

    // 记忆碎片收集情况（结局二触发条件）
    public int MemoryShardsCollected { get; set; }
    public const int TotalMemoryShards = 12;

    [Header("Player Reference")]
    public GameObject PlayerObject { get; private set; }

    [Header("Respawn")]
    public Vector2 LastBeaconPosition { get; set; }
    public string LastBeaconScene { get; set; }

    [Header("Currency")]
    public int YinCoinBalance { get; set; } // 阴钱
    public int Merits { get; set; }         // 功德（经验值）

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void RegisterPlayer(GameObject player)
    {
        PlayerObject = player;
    }

    public void SetGameState(GameState newState)
    {
        State = newState;
        Debug.Log($"[GameManager] State changed to: {newState}");
    }

    public bool CanTriggerMemoryShard()
    {
        return MemoryShardsCollected < TotalMemoryShards;
    }

    public void CollectMemoryShard()
    {
        MemoryShardsCollected = Mathf.Min(MemoryShardsCollected + 1, TotalMemoryShards);
    }

    public int TotalBossesDefeated()
    {
        int count = 0;
        if (HasDefeatedHanBoss) count++;
        if (HasDefeatedZhaoBoss) count++;
        if (HasDefeatedWeiBoss) count++;
        if (HasDefeatedChuBoss) count++;
        if (HasDefeatedYanBoss) count++;
        if (HasDefeatedQiBoss) count++;
        return count;
    }
}

public enum GameState
{
    Menu,
    Exploring,
    BossFight,
    Paused,
    Death,
    Cutscene,
    Ending
}
