# 秦殇 — 15分钟跑通战斗测试

> 从新建 Unity 项目到按 Play 打木桩的完整步骤。
> 不需要任何美术资源，所有角色用白色方块代替。

---

## 准备工作

把以下 4 个文件复制到 Unity 项目中：
```
design/prototype/
├── PlayerController_Standalone.cs    →  Assets/Scripts/
├── Enemy_Standalone.cs               →  Assets/Scripts/
├── PlayerInputActions.inputactions   →  Assets/Settings/ (可选)
└── SETUP_GUIDE.md                    →  项目根目录 (参考用)
```

---

## Step 1: 新建 Unity 项目 (2分钟)

1. 打开 Unity Hub → 新建项目
2. 选择 **2D (URP)** 模板
   - 如果用 Universal Render Pipeline 模板，光照和渲染管线已预配好
3. 项目名: `QinShang_Prototype`
4. 位置: 任意
5. 点击 **创建**

---

## Step 2: 导入脚本 (1分钟)

1. 在 Unity 的 Project 窗口中，右键 `Assets/` → 新建文件夹 → `Scripts/`
2. 把 `PlayerController_Standalone.cs` 和 `Enemy_Standalone.cs` 拖进 `Scripts/` 目录
3. 等待编译完成（右下角转圈结束）

---

## Step 3: 创建测试场景 (5分钟)

### 3.1 创建场景

1. File → New Scene → 选择 **Basic (URP)** → Create
2. 保存: Ctrl+S → 命名 `Prototype_Battle` → 保存到 `Assets/Scenes/`

### 3.2 创建地面

1. Hierarchy 右键 → 2D Object → Sprite → **Square**
   - 命名为 `Ground`
   - Inspector 中:
     - **Position**: X=0, Y=-2, Z=0
     - **Scale**: X=20, Y=1, Z=1
   - Sprite Renderer → Color: 深灰色 (#444444)
2. 给 Ground 添加 **BoxCollider2D**（自动适配）
3. 在 Layer 下拉菜单 → 添加 Layer → 命名为 `Ground`
4. Ground 的 Layer 设为 **Ground**

### 3.3 创建 Player

1. Hierarchy 右键 → 2D Object → Sprite → **Square**
   - 命名为 `Player`
   - Inspector:
     - **Position**: X=0, Y=0, Z=0
     - **Scale**: X=0.8, Y=1.2, Z=1
   - Sprite Renderer → Color: **纯白色** (#FFFFFF)
2. 添加组件:
   - **Rigidbody2D** — 设为：
     - Body Type: Dynamic
     - Linear Drag: 0
     - Angular Drag: 0
     - Gravity Scale: 1
     - Freeze Rotation: Z (勾选)
     - Collision Detection: Continuous
   - **BoxCollider2D** — Size: X=0.8, Y=1.2
   - **PlayerController_Standalone** (Script) — 全部用默认值
3. **关键：给 Player 加 Tag**
   - Inspector 顶部 Tag 下拉 → Add Tag → 新建 `Player` 标签
   - 把 Player 的 Tag 设为 **Player**

### 3.4 创建 Enemy (木桩/敌人)

1. Hierarchy 右键 → Create Empty → 命名为 `Enemy`
2. Inspector:
   - Position: X=5, Y=-1, Z=0
3. 添加 Component:
   - **SpriteRenderer** → Color: **浅灰色** (#888888)
     - 点击 Sprite 选择器 → 选 **Square** (UISprite)
   - **Rigidbody2D** — 同 Player 设置
   - **BoxCollider2D** — Size: X=0.8, Y=1.2
   - **Enemy_Standalone** (Script)
4. **关键：给 Enemy 设 Layer**
   - Layer 下拉 → 添加 Layer → 命名为 `Enemy` (建议放到 User Layer 6)
   - Enemy 的 Layer 设为 **Enemy**
5. 回到 `PlayerController_Standalone` 检查 **Enemy Layer** 字段：
   - 如果用了 Layer 6，Inspector 中 EnemyLayer 选 **Enemy**
   - 如果选了别的编号，对应选上

### 3.5 加一个平台 (可选)

在 Player 上方 4 格处加一个小平台，用来测试跳跃+下落攻击：
- 新建一个 Sprite → Square
- Position: X=3, Y=4
- Scale: X=3, Y=0.3
- Layer: Ground
- 加 BoxCollider2D

---

## Step 4: 配置层碰撞 (1分钟)

1. Edit → Project Settings → **Physics 2D**
2. 找到 **Layer Collision Matrix**
3. 确保：
   - Ground 层 ✓ 和 Ground 层 ✓
   - Ground 层 ✓ 和 Default 层 ✓
   - Enemy 层 ✓ 和 Default (Player) 层 ✓
   - Enemy 层 □ 和 Enemy 层 □ (取消勾选，敌人不互相碰撞)
   - Player 所在的 Default 层 ✓ 和 Ground 层 ✓

---

## Step 5: 按 Play (1分钟)

操作说明（按 Play 后直接可用）：

| 操作 | 按键 | 说明 |
|---|---|---|
| 左右移动 | A / D 或 ←/→ | 平滑移动 |
| 跳跃 | W 或 Space | 仅地面可跳 |
| 轻击 | Z 或 鼠标左键 | 三段连击，伤害递增 |
| 重击 | X 或 鼠标右键 | 高伤慢速，范围更大 |
| 格挡 | 按住 C 或 左Ctrl | 减伤70%，减速50% |
| 冲刺 | 左Shift（空中） | 0.3s无敌帧，耗气力 |
| 丹药恢复 | E 或 Q | +35HP，最多3次 |
| 暂停 | Escape | (仅log) |

**屏幕左上角**会实时显示：
```
气血:80/80  气力:100/100  丹药:3
    [W跳] [左/右键攻击] [C格挡] [Shift冲刺] [E恢复]
```

---

## Step 6: 验证

按 Play 后依次测试：

- [ ] 按 A/D 移动 → Player 左右滑动
- [ ] 按 Space/W → 跳跃，落地后可以再次跳
- [ ] 走到 Enemy 附近 (5格内) → Enemy 开始朝你移动
- [ ] 按 Z 轻击 → Enemy 闪红+后退 (HitStop触发)
- [ ] 按 X 重击 → Enemy 被击退更远 (+击退力)
- [ ] 按住 C → Player 出现 "格挡中" 文字 → Enemy 攻击伤害降低
- [ ] 接近 Enemy 被攻击 → 闪红 + 无敌帧1秒
- [ ] 空中按 左Shift → 空中冲刺一段距离
- [ ] 按 E → 回复HP，丹药-1
- [ ] 全部完成 → 战斗手感调参

---

## 调参指南

所有参数都在 `PlayerController_Standalone` 的 Inspector 中公开可调。
按 Play 后**实时修改**数值会立即生效（前提是脚本没有使用 `Awake` 缓存）。

### 优先调的参数

```
移动:
  Move Speed       12    → 快了就降，慢了就升
  Jump Force       22    → 太高/太低？
  Fall Gravity     2.5   → 落地感够不够重

攻击:
  Light Damage     15    → 打一次敌人掉多少血 (敌人默认60HP)
  Heavy Damage     30    → 两次重击能打死一个敌人
  Light Duration   0.25  → 攻击前摇时长 (秒)
  Heavy Duration   0.6   → 重击前摇

气力:
  Max Qi           100   → 够打8次轻击+1次冲刺
  Qi Recovery Rate 25    → 每秒恢复量
  Qi Recovery Delay 0.5  → 停手后多久开始恢复

冲刺:
  Dash Distance    5     → 够穿越一个敌人？
  Dash Duration    0.3   → 无敌帧持续时间
```

### 敌人调参

在 `Enemy_Standalone` 中：
```
Max HP            60    → 玩家打几次能杀死
Damage            12    → 打玩家掉多少 (玩家默认80HP)
Detection Range   6     → 多远开始追玩家
Attack Range      1.8   → 多近开始攻击
Attack Cooldown   1.2   → 两次攻击间隔
Move Speed        3     → 追击速度
```

---

## 常见问题

### Player 掉出世界

在 `PlayerController_Standalone.cs` 中，`ClampPosition()` 会在 Y < -50 时自动复活。如果掉出去了等 5 秒即可。

### Enemy 不移动

检查：
1. Enemy 的 Rigidbody2D 是 Dynamic
2. Player 的 Tag 是 "Player"
3. Enemy 的 Collider 和 Enemy Layer 正确

### 攻击没反应

检查：
1. PlayerController 的 Enemy Layer 字段是否选了正确的层
2. Enemy 确实在 Enemy Layer 上

### Sprite 显示为问号/方块

新建项目默认 Sprite 列表中包含 White Square。如果找不到：
1. Assets → Create → Sprite → Square
2. 或者用 GameObject → 2D Object → Sprites → Square

---

## 下一步

跑通后，如果想继续深入：

1. **加攻击动画**：给 Player 加 Animator Controller，把 placeholder 方块替换为正式精灵
2. **加烽火台**：参考 `design/scripts/World/BeaconTower.cs`，拖进场景
3. **铺残韩之廊**：参考 `design/HanCorridorLayout.md`，用 Tilemap 开始画房间
4. **加韩弩将军**：参考 `design/BossDesign.md` + `design/BossAI.md`，用 `BossBase.cs` 实现

