using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 存档数据结构。只存最小可序列化数据集。
/// </summary>
[System.Serializable]
public class SaveData
{
    // 元数据
    public string saveVersion = "1.0";
    public DateTime saveTime;
    public float playTimeHours;
    public string lastScene;

    // 玩家位置
    public float playerPosX;
    public float playerPosY;

    // 玩家属性
    public int maxHP;
    public int currentHP;
    public int maxQi;
    public int healCharges;
    public int yinCoins;
    public int merits;

    // 能力
    public bool hasAirDash;
    public bool hasInscriptionRead;
    public bool hasShockwave;
    public bool hasSlide;
    public bool hasIllusionSummon;
    public bool hasUnityState;
    public bool hasHeartOfEmperor;

    // Boss 进度
    public bool defeatedHanBoss;
    public bool defeatedZhaoBoss;
    public bool defeatedWeiBoss;
    public bool defeatedChuBoss;
    public bool defeatedYanBoss;
    public bool defeatedQiBoss;

    // 收集
    public int memoryShards;
    public int beaconsLit;
    public List<string> discoveredAreas;
    public List<string> collectedItems;

    // 当前装备
    public string currentWeaponID;
    public List<string> equippedCharms;

    /// <summary>
    /// 从 GameManager 和 Player 组件创建存档
    /// </summary>
    public static SaveData CreateFromCurrentState()
    {
        var data = new SaveData();
        var gm = GameManager.Instance;
        var player = gm?.PlayerObject;

        data.saveTime = DateTime.Now;
        data.lastScene = gm?.LastBeaconScene ?? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (player != null)
        {
            var pos = player.transform.position;
            data.playerPosX = pos.x;
            data.playerPosY = pos.y;

            var health = player.GetComponent<PlayerHealth>();
            if (health != null)
            {
                data.maxHP = health.MaxHP;
                data.currentHP = health.CurrentHP;
                data.healCharges = health.HealCharges;
            }

            var qi = player.GetComponent<PlayerQi>();
            if (qi != null)
            {
                data.maxQi = Mathf.RoundToInt(qi.MaxQi);
            }

            var abilities = player.GetComponent<PlayerAbilities>();
            if (abilities != null)
            {
                data.hasAirDash = abilities.HasAbility(AbilityType.AirDash);
                data.hasInscriptionRead = abilities.HasAbility(AbilityType.InscriptionRead);
                data.hasShockwave = abilities.HasAbility(AbilityType.Shockwave);
                data.hasSlide = abilities.HasAbility(AbilityType.Slide);
                data.hasIllusionSummon = abilities.HasAbility(AbilityType.IllusionSummon);
                data.hasUnityState = abilities.HasAbility(AbilityType.UnityState);
                data.hasHeartOfEmperor = abilities.HasAbility(AbilityType.HeartOfEmperor);
            }
        }

        if (gm != null)
        {
            data.yinCoins = gm.YinCoinBalance;
            data.merits = gm.Merits;
            data.defeatedHanBoss = gm.HasDefeatedHanBoss;
            data.defeatedZhaoBoss = gm.HasDefeatedZhaoBoss;
            data.defeatedWeiBoss = gm.HasDefeatedWeiBoss;
            data.defeatedChuBoss = gm.HasDefeatedChuBoss;
            data.defeatedYanBoss = gm.HasDefeatedYanBoss;
            data.defeatedQiBoss = gm.HasDefeatedQiBoss;
            data.memoryShards = gm.MemoryShardsCollected;
        }

        return data;
    }

    /// <summary>
    /// 将存档数据恢复至游戏状态
    /// </summary>
    public void ApplyToGameState()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        gm.YinCoinBalance = yinCoins;
        gm.Merits = merits;
        gm.HasDefeatedHanBoss = defeatedHanBoss;
        gm.HasDefeatedZhaoBoss = defeatedZhaoBoss;
        gm.HasDefeatedWeiBoss = defeatedWeiBoss;
        gm.HasDefeatedChuBoss = defeatedChuBoss;
        gm.HasDefeatedYanBoss = defeatedYanBoss;
        gm.HasDefeatedQiBoss = defeatedQiBoss;
        gm.MemoryShardsCollected = memoryShards;
        gm.LastBeaconScene = lastScene;
        gm.LastBeaconPosition = new Vector2(playerPosX, playerPosY);
    }
}

/// <summary>
/// 存档管理。处理 JSON 序列化/反序列化。
/// </summary>
public class SaveLoadManager
{
    private const string SaveFileName = "qinshang_save.json";

    public static void Save(SaveData data)
    {
        string json = JsonUtility.ToJson(data, true);
        string path = System.IO.Path.Combine(Application.persistentDataPath, SaveFileName);
        System.IO.File.WriteAllText(path, json);
        Debug.Log($"[Save] 存档已保存至: {path}");
    }

    public static SaveData Load()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, SaveFileName);
        if (!System.IO.File.Exists(path))
        {
            Debug.Log("[Save] 未找到存档文件");
            return null;
        }

        string json = System.IO.File.ReadAllText(path);
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        Debug.Log($"[Save] 存档已加载: {path}");
        return data;
    }

    public static bool HasSaveFile()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, SaveFileName);
        return System.IO.File.Exists(path);
    }

    public static void DeleteSave()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, SaveFileName);
        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
            Debug.Log("[Save] 存档已删除");
        }
    }
}
