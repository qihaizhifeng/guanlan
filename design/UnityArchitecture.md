# 秦殇 — Unity 架构设计指南

## 项目结构

```
Assets/
  Scenes/          — 场景文件
  Scripts/         — C# 脚本
  Art/             — 美术资源
  Audio/           — 音频
  Prefabs/         — 预制体
  ScriptableObjects/ — 数据配置
  Tilemaps/        — 瓦片地图
```

## C# 脚本架构总览

```
Scripts/
  Core/             — 全局管理
    GameManager.cs
    SaveManager.cs
    SceneLoader.cs

  Player/           — 玩家组件
    PlayerController.cs   — 移动/跳跃/冲刺
    PlayerCombat.cs       — 攻击/格挡/处决
    PlayerHealth.cs       — 气血/受伤/无敌帧
    PlayerQi.cs           — 气力管理
    PlayerAbilities.cs    — 能力管理
    PlayerInventory.cs    — 武器/护符/道具

  Combat/           — 战斗通用
    HealthSystem.cs       — 通用生命值
    DamageCalculator.cs   — 伤害计算(含韧性)
    HitBox.cs             — 攻击判定框
    HitStop.cs            — 打击停顿
    ParrySystem.cs        — 格挡/弹反

  Enemies/          — 敌人
    EnemyBase.cs          — 基类
    EnemyStateMachine.cs  — AI状态机
    EnemySpawner.cs       — 生成器

  Boss/             — Boss
    BossBase.cs           — Boss基类
    BossPhaseManager.cs   — 阶段管理

  Abilities/        — 能力系统
    AbilityBase.cs
    AirDash.cs
    InscriptionRead.cs
    Shockwave.cs
    Slide.cs
    IllusionSummon.cs
    UnityState.cs

  World/            — 世界物件
    BeaconTower.cs        — 烽火台(存档点)
    ShortcutDoor.cs       — 捷径门
    TrapBase.cs           — 陷阱基类
    BreakableObject.cs    — 可破坏物件
    MovingPlatform.cs     — 移动平台

  UI/               — 界面
    HUDController.cs
    InventoryUI.cs
    MapUI.cs
    DeathScreen.cs
    PauseMenu.cs

  Camera/           — 摄像机
    CameraFollow.cs
    CameraShake.cs
    CameraBounds.cs

  Audio/            — 音频
    AudioManager.cs
    MusicManager.cs

  Save/             — 存档
    SaveData.cs
    SaveLoadManager.cs
```

## 核心接口与基类

### IHealth （通用生命值接口）

```csharp
public interface IHealth {
    int CurrentHP { get; }
    int MaxHP { get; }
    bool IsInvincible { get; }
    void TakeDamage(int amount, GameObject source);
    void Heal(int amount);
    void Die();
    event System.Action OnDamaged;
    event System.Action OnDeath;
}
```

### IPlayerState （玩家状态接口）

```csharp
public interface IPlayerState {
    bool IsGrounded { get; }
    bool IsBlocking { get; }
    bool IsStaggered { get; }
    bool HasAbility(AbilityType ability);
    WeaponData CurrentWeapon { get; }
}
```

### AbilityType （能力枚举）

```csharp
public enum AbilityType {
    AirDash,
    InscriptionRead,
    Shockwave,
    Slide,
    IllusionSummon,
    UnityState,
    HeartOfEmperor
}
```

### EnemyState （AI状态枚举）

```csharp
public enum EnemyState {
    Idle,
    Patrol,
    Alert,
    Chase,
    Attack,
    Stagger,
    Retreat,
    Death
}
```

## 组件职责边界

每个 Player 组件只做一件事：

- **PlayerController**: 读取输入 → 驱动移动/跳跃/冲刺物理 → 播出动画参数
- **PlayerCombat**: 监听攻击输入 → 触发 HitBox → 消耗气力 → 处理命中/硬直
- **PlayerHealth**: 接收伤害 → 扣血→ 触发无敌帧 → 调用死亡逻辑
- **PlayerQi**: 气力增减 → 自然恢复计时 → 不足时阻止动作
- **PlayerAbilities**: 持有能力位标记 → 被其他系统查询

## ScriptableObject 数据设计

### WeaponData

```csharp
[CreateAssetMenu(fileName = "NewWeapon", menuName = "秦殇/武器数据")]
public class WeaponData : ScriptableObject {
    public string weaponName;
    [TextArea] public string description;
    public Sprite icon;
    public int lightDamage;
    public int heavyDamage;
    public float lightSpeed;      // 攻速倍率
    public float qiCostMultiplier;
    public float reachMultiplier;
    public string specialEffect;  // 效果描述，代码中 switch 处理
}
```

### EnemyData

```csharp
[CreateAssetMenu(fileName = "NewEnemy", menuName = "秦殇/敌人数据")]
public class EnemyData : ScriptableObject {
    public string enemyName;
    public int maxHP;
    public int damage;
    public float moveSpeed;
    public float detectionRange;
    public float attackRange;
    public int scoreValue;        // 击杀功德值
}
```

### CharmData （护符）

```csharp
[CreateAssetMenu(fileName = "NewCharm", menuName = "秦殇/护符数据")]
public class CharmData : ScriptableObject {
    public string charmName;
    [TextArea] public string description;
    public Sprite icon;
    public int slotCost;
    public CharmEffect effectType;
    public float effectValue;     // 数值参数
}

public enum CharmEffect {
    MaxHP_Flat,
    AttackPercent,
    BlockPercent,
    QiRecoverSpeed,
    CoinPercent,
    BlockNoQiCost,
    AirHover,
    IllusionDuration,
    HealPercent,
    UnityDuration
}
```

## 设计原则摘要

1. **GameManager 是唯一全局单例**，场景切换不销毁。其他系统通过它访问存档、玩家引用、游戏状态。
2. **所有战斗相关数值走 ScriptableObject**，不在代码里硬编码。方便调参不需要碰代码。
3. **Boss 阶段管理独立于 Boss 行为** — PhaseManager 负责触发条件（血量百分比、时间、玩家动作），BossAI 只关心"当前阶段下怎么做"。
4. **事件驱动的跨系统通信** — 如 PlayerHealth.OnDamaged 触发 CameraShake 和 HUD 更新，PlayerCombat 不关心谁在听。
5. **玩家状态数据与渲染分离** — Health/Qi/Abilities 是纯数据，UI 和动画是它们的监听者。
6. **存档存的是可序列化的最小数据集** — 不存 Transform 位置以外的 Unity 对象引用。
