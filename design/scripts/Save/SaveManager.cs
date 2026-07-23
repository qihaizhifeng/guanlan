using UnityEngine;
using System;

/// <summary>
/// 单例存档管理器。
/// 协调 GameManager / Player / SaveData 之间的存档/读档流程。
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("Save Info")]
    [SerializeField] private bool autoSaveOnBeacon = true;
    [SerializeField] private bool autoSaveOnSceneChange = true;

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

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// 保存游戏
    /// </summary>
    public void SaveGame()
    {
        SaveData data = SaveData.CreateFromCurrentState();
        SaveLoadManager.Save(data);
        Debug.Log("[SaveManager] 游戏已保存");
    }

    /// <summary>
    /// 加载游戏。返回是否找到有效存档。
    /// </summary>
    public bool LoadGame()
    {
        SaveData data = SaveLoadManager.Load();
        if (data == null) return false;

        data.ApplyToGameState();
        Debug.Log("[SaveManager] 游戏已加载");
        return true;
    }

    /// <summary>
    /// 检查是否有存档
    /// </summary>
    public bool HasSaveData()
    {
        return SaveLoadManager.HasSaveFile();
    }

    /// <summary>
    /// 删除存档
    /// </summary>
    public void DeleteSave()
    {
        SaveLoadManager.DeleteSave();
    }

    /// <summary>
    /// 场景加载后的处理
    /// </summary>
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, 
        UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (autoSaveOnSceneChange && scene.name != "Menu" && scene.name != "Bootstrap")
        {
            SaveGame();
        }
    }

    /// <summary>
    /// 新建游戏（重置所有状态）
    /// </summary>
    public void NewGame()
    {
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.YinCoinBalance = 0;
            gm.Merits = 0;
            gm.HasDefeatedHanBoss = false;
            gm.HasDefeatedZhaoBoss = false;
            gm.HasDefeatedWeiBoss = false;
            gm.HasDefeatedChuBoss = false;
            gm.HasDefeatedYanBoss = false;
            gm.HasDefeatedQiBoss = false;
            gm.MemoryShardsCollected = 0;
        }

        DeleteSave();
        Debug.Log("[SaveManager] 新游戏已开始");
    }
}
