# 秦殇 — Boss AI 状态机设计

> 每个 Boss 的状态机用 标准层级状态机 (Hierarchical State Machine) 实现。
> 语法说明:
>   ENTER → 进入状态时的行为
>   UPDATE → 每帧更新
>   EXIT → 退出状态时的行为
>   → 表示状态转换
>   [条件] → 转换条件

---

## 1. 韩弩将军 (入门Boss)

### 状态机总图

```
                    ┌─────────────┐
                    │   INIT      │ Boss醒来动画
                    └──────┬──────┘
                           ↓
                    ┌─────────────┐
          ┌────────→│ PHASE1_IDLE │←────────┐
          │         └──────┬──────┘         │
          │                │ 选择攻击        │
          │                ↓                │
          │         ┌──────────────┐        │
          │         │ SELECT_ATTACK │        │
          │         └──────┬───────┘        │
          │                │ 执行            │
          │                ↓                │
          │         ┌──────────────┐        │
          │         │ EXECUTE_ATTACK│        │
          │         └──────┬───────┘        │
          │                │ 攻击完成        │
          │                ↓                │
          │         ┌──────────────┐        │
          │         │ COOLDOWN     │────────┘
          │         └──────────────┘  [HP>50%]
          │
          │         [HP≤50%]
          │         ↓
          │         ┌─────────────┐
          │         │ TRANSITION  │ 破碎动画+屏幕震
          │         └──────┬──────┘
          │                ↓
          │         ┌─────────────┐
          │         │ PHASE2_IDLE │
          │         └──────┬──────┘
          │                │
          │                ↓ 同 P1 但攻击池变化
          │         ┌──────────────┐
          │         │ PHASE2_ATTACK│
          │         └──────┬───────┘
          │                │
          └────────────────┘    [HP>0]
                               [HP=0] → DEATH
```

### 攻击选择逻辑 (伪代码)

```
// 韩弩将军 - Phase 1 攻击选择
function SelectAttackPhase1():
    if playerDistance > 5:
        priority = [
            (crossbowShot,     weight: 40),
            (tripleShot,       weight: 30),
            (arrowRain,        weight: 20),
            (sweepAttack,      weight: 10),  // 距离远时不会横扫
        ]
    elif playerDistance > 2:
        priority = [
            (crossbowShot,     weight: 30),
            (tripleShot,       weight: 30),
            (sweepAttack,      weight: 25),
            (arrowRain,        weight: 15),
        ]
    else:  // 近身
        priority = [
            (sweepAttack,      weight: 45),
            (crossbowShot,     weight: 20),  // 近身仍可能射
            (tripleShot,       weight: 20),
            (arrowRain,        weight: 15),
        ]
    
    // 连出惩罚：连续3次同类攻击则权重-50%
    if consecutiveAttacks >= 3:
        priority[currentType] *= 0.5
    
    return weightedRandomSelect(priority)


// Phase 2 的增强
function SelectAttackPhase2():
    // 攻击池中 crossbowShot → quickShot (前摇减半)
    // tripleShot → pentaShot (5发散射)
    // sweepAttack → doubleSweep (连续两次)
    // 新增: spinShot (旋转散射)
    
    if playerDistance > 5:
        priority = [
            (quickShot,        weight: 35),
            (pentaShot,        weight: 25),
            (spinShot,         weight: 25),
            (arrowRain,        weight: 15),
        ]
    else:
        priority = [
            (doubleSweep,      weight: 35),
            (quickShot,        weight: 25),
            (spinShot,         weight: 25),
            (pentaShot,        weight: 10),
            (arrowRain,        weight: 5),
        ]


// 韩弩将军核心行为
STATE CrossbowShot:
    ENTER:
        playAnimation("crossbow_charge")
        playSound("crossbow_windup")
        aimDirection = (playerPosition - bossPosition).normalized
        chargeTimer = phase == 1 ? 0.8s : 0.5s
    
    UPDATE:
        chargeTimer -= deltaTime
        if chargeTimer <= 0:
            // 发射弩箭
            projectile = spawnProjectile(crossbowBolt, aimDirection)
            projectile.speed = phase == 1 ? 15 : 20
            playAnimation("crossbow_fire")
            ChangeState(COOLDOWN)
    
    EXIT:
        consecutiveAttacks++


STATE SweepAttack:
    ENTER:
        playAnimation("arm_windup")
        windupTimer = 0.6s
        hitboxActivated = false
    
    UPDATE:
        windupTimer -= deltaTime
        if windupTimer <= 0 and !hitboxActivated:
            hitboxActivated = true
            enableHitbox("left_arm", duration: 0.3s)
            playSound("sweep_swing")
            knockbackDirection = (playerPosition - bossPosition).normalized * 5
            // 如果 Phase 2: 连续两次
            if phase == 2 and !secondSweep:
                secondSweep = true
                windupTimer = 0.4s
                return
            ChangeState(COOLDOWN)
```

---

## 2. 赵盾之灵 (防守反击型)

### 状态机总图

```
         ┌─────────────┐
         │ SHIELD_WALK │  ← 默认状态，举盾缓步向前
         └──────┬──────┘
                │ 根据距离和随机选择
          ┌─────┼──────┬──────┐
          ↓     ↓      ↓      ↓
    ┌──────┐ ┌────┐ ┌────┐ ┌──────┐
    │SHIELD│ │EDGE │ │CALL │ │STAGGER│
    │BASH  │ │SLASH│ │SOULS│ │(被弹反)│
    └──┬───┘ └──┬─┘ └──┬─┘ └──┬───┘
       │        │      │       │
       └────────┴──────┴───────┘
                │ 攻击结束
                ↓
         ┌─────────────┐
         │ SHIELD_WALK │
         └─────────────┘

         [HP≤66%] → PHASE_TRANSITION
         [HP≤33%] → PHASE_TRANSITION
```

### 核心行为：格挡窗口教学

```
// 赵盾之灵 - 核心教学机制
// 盾牌猛击 (Shield Bash) 是这场战斗最重要的招式
// 设计意图：教会玩家"精准格挡创造攻击窗口"

STATE ShieldBash:
    ENTER:
        playAnimation("shield_raise")     // 高举盾牌
        playSound("shield_powerup")       // 低沉的蓄力音
        chargeTimer = 1.0s
        isPerfectParryable = true         // 此招可被精准格挡
        groundCrackEffect = true          // 地面裂纹视觉提示
    
    UPDATE:
        chargeTimer -= deltaTime
        
        // 地面裂纹逐渐扩大
        if chargeTimer < 0.5f:
            spawnGroundCrack(effectScale: 1 - chargeTimer/0.5)
        
        // 砸下
        if chargeTimer <= 0:
            playAnimation("shield_slam")
            screenShake(intensity: 0.4, duration: 0.15)
            
            // 检测精准格挡窗口 (0.12s)
            if playerIsBlocking and player.isPerfectParry:
                // ↓ 精准格挡成功 → 大硬直
                ChangeState(STAGGERED_LONG)
            else:
                // 普通格挡或未格挡
                hitbox = createHitbox(center: boss, radius: 2.0)
                hitbox.damage = 22
                if player.isBlocking:
                    hitbox.damage = 7   // 70%减伤后
                ChangeState(SHIELD_WALK)
    
    EXIT:
        cleanup()

// ↓ 精准格挡成功后的奖励状态

STATE STAGGERED_LONG:
    ENTER:
        playAnimation("shield_stuck_in_ground")
        // 盾牌插入地面，Boss 无法移动
        staggerTimer = 2.0s
        exposeBack = true       // 背部暴露
    
    UPDATE:
        staggerTimer -= deltaTime
        if staggerTimer <= 0:
            playAnimation("shield_pull_out")
            ChangeState(SHIELD_WALK)
    
    // 玩家在此时绕到背后输出
    // 背部攻击伤害 1.5x
    // 允许 3-4 次轻击 或 2 次重击
```

### Phase 转换

```
// Phase 1 → 2 (HP≤66%)
function PhaseTransition1To2():
    playAnimation("shield_break")
    screenShake(intensity: 0.6, duration: 0.3)
    // 盾牌分裂为两半
    shieldSplit = true
    moveSpeed *= 1.2
    // 新增攻击: 左右连击, 盾旋风, 盾投
    attackPool.unlock("doubleSlash")
    attackPool.unlock("shieldSpin")
    attackPool.unlock("shieldToss")

// Phase 2 → 3 (HP≤33%)
function PhaseTransition2To3():
    playAnimation("shield_recombine")
    // 盾牌重新合并，表面浮现哀嚎面孔
    shieldMerged = true
    // 新增终极招式: 千魂冲撞
    attackPool.unlock("soulCharge")
    // 军旗燃烧效果
    setFlagsOnFire()
```

---

## 3. 魏武卒魂·统帅 (重甲型)

### 核心机制：架势值

```
// 魏武卒魂的独特机制：隐藏的 POISE 值
// 非Boss敌人也有Poise，但统帅的Poise机制是游戏的核心教学点

// Poise 系统
poise = 120
poiseMax = 120
poiseRecoveryDelay = 3.0s
poiseRecoveryRate = 5/s

每击中Boss一次 → poise -= damage
poise归零时 → Boss单膝跪地3s → 背后攻击窗口

// Phases
Phase 1: 标准重甲模式，移动极慢
Phase 2: 装甲碎裂，移动速度+30%，攻击模式更激进
```

### 状态机

```
         ┌───────────┐
         │ HEAVY_WALK│  ← 缓慢向玩家前进 (速度 = 玩家步行 × 0.4)
         └─────┬─────┘
               │ 距离判定
          ┌────┼────┐
          ↓    ↓    ↓
    ┌──────┐ ┌──┐ ┌──────┐
    │GORGE │ │STOMP │WATER │
    │SWING │ │      │SLASH │
    └──┬───┘ └──┘ └──┬───┘
       │              │
       ↓              ↓
   ┌───────┐    ┌─────────┐
   │POISE  │    │POISE    │
   │CHECK  │    │CHECK    │
   └──┬────┘    └──┬──────┘
      │            │
      └─────┬──────┘
            ↓
      ┌──────────┐
      │ POISE >0 │────→ CONTINUE
      └────┬─────┘
           ↓ POISE ≤0
      ┌──────────┐
      │ STAGGER  │ (3秒大硬直)
      │ KNELT    │
      └────┬─────┘
           ↓ 3s后/提前受击
      ┌──────────────┐
      │ STAND_UP     │ (0.8s起身动画)
      └──────┬───────┘
             ↓
      ┌──────────┐
      │HEAVY_WALK│
      └──────────┘
```

### 背面判定

```
// 魏武卒魂的核心策略：绕背
// 正面攻击伤害减半 (damage *= 0.5)
// 侧面攻击正常 (damage *= 1.0)  
// 背面攻击 1.5x (damage *= 1.5)
//
// Boss 转向速度: 每秒旋转 90°
// 窗口: 攻击硬直时转向暂停

STATE HeavyWalk:
    UPDATE:
        // 面朝玩家
        targetAngle = angleFromTo(bossPosition, playerPosition)
        currentAngle = lerpAngle(currentAngle, targetAngle, 
            turnSpeed * deltaTime)  // turnSpeed = 90°/s，攻击时=0
        
        // 缓慢接近
        if distanceToPlayer > attackRange:
            moveToward(playerPosition, speed: 1.2)
        
        // 攻击判定
        if distanceToPlayer <= attackRange and attackCooldown <= 0:
            ChangeState(SelectAttack())
```

---

## 4. 楚巫祭·灵均 (法系型)

### 核心机制：漂浮

```
// 楚巫祭在空中漂浮 (地面以上4格)
// 玩家地面攻击无法触及
// 需要跳跃攻击或利用二层平台
// 这也意味着：玩家需要在下落/跳跃中同时处理弹幕

STATE Float:
    ENTER:
        floatHeight = 4.0
        floatAmplitude = 0.3      // 小幅上下浮动
        floatSpeed = 1.0          // 浮动频率
    
    UPDATE:
        // 空中漂浮运动
        yOffset = sin(time * floatSpeed) * floatAmplitude
        position.y = floatHeight + yOffset
        
        // 水平缓慢移动
        if phase == 1:
            moveSpeed = 1.5
        elif phase == 2:
            moveSpeed = 2.0
        else:
            moveSpeed = 2.5 + sin(time * 0.5) * 1.0  // 不规则运动
        
        patrolDirection = (patrolCenter - position).normalized
        moveToward(patrolCenter + patrolDirection * patrolRadius, moveSpeed)
```

### 弹幕生成

```
// 楚巫祭的弹幕模式（按优先级排列）

function GenerateBulletPattern():
    switch selectedPattern:
        case PHOSPHORUS_ORB:
            // 磷光弹：缓慢追踪
            orb = spawnProjectile("phosphorus_orb")
            orb.tracking = true
            orb.trackingStrength = 0.3  // 轻微追踪，非制导
            orb.speed = 3.0
            orb.lifetime = 5.0
            // 特性和玩家攻击可打散
            orb.hittable = true
            orb.hitHP = 3
            break
        
        case WATER_WEED_GRAB:
            // 水草缠绕：从毒沼伸出触手
            for i in 0..3:
                weedSpawnPoint = randomPointInSwamp(avoid: playerPosition, radius: 2.0)
                weed = spawnEnemy("water_weed", weedSpawnPoint)
                weed.grabDuration = 1.5s
                weed.escapeMethod = "tapAnyKey"  // QTE连点挣脱
            break
        
        case GHOST_FIRE_RING:
            // 鬼火圈：玩家周围形成收缩圈
            center = playerPosition
            for angle in 0..360 step 60:
                spawnX = center.x + cos(angle) * 3.0
                spawnY = center.y + sin(angle) * 3.0
                fire = spawnProjectile("ghost_fire", (spawnX, spawnY))
                fire.moveToward(center, speed: 2.0)
            break
        
        case SUMMON_SHADES:
            // 召唤幻影
            playAnimation("chant")
            playSound("chant_loop")
            chantTimer = 2.0s
            // 吟唱期间可被打断
            isInterruptible = true
            if chantTimer <= 0 and !interrupted:
                for i in 0..2:
                    shade = spawnEnemy("chu_shade", randomEdgePosition())
                    shade.HP = 30
                    shade.damage = 8
            break
```

### Phase 3：离骚模式

```
// HP≤33%时进入
// 视觉特征：屏幕变暗，Boss 半透明化
// 新机制：每15s吟唱诗句，诗句决定弹幕模式

STATE LamentChant:
    ENTER:
        isInvincible = true  // 吟唱中无敌
        chantDuration = 3.0s
        poems = [
            "路漫漫其修远兮",   // → 连续光柱扫射
            "吾将上下而求索",   // → 四向交替弹幕
            "长太息以掩涕兮",   // → 全屏泪滴
            "虽九死其犹未悔",   // → 瞬移三次+范围弹幕
        ]
        currentPoem = randomSelect(poems)
        screenText = showLargeText(currentPoem, duration: chantDuration)
    
    UPDATE:
        chantDuration -= deltaTime
        if chantDuration <= 0:
            isInvincible = false
            GeneratePatternForPoem(currentPoem)
            ChangeState(FLOAT)
```

---

## 5. 荆轲执念 (速度型)

### 状态机总图

```
// 荆轲可能是最复杂的 AI——它大部分时间不在"战斗状态"
// 它在"准备袭击"和"袭击完成"之间快速切换

              ┌──────────────┐
              │  PREPARE     │ 消失/移动到攻击位置
              └──────┬───────┘
                     ↓
              ┌──────────────┐
        ┌────→│  ATTACK      │ 发动袭击 (0.3-0.5s)
        │     └──────┬───────┘
        │            ↓
        │     ┌──────────────┐
        │     │  AFTERMATH   │ 攻击完成后短暂可见 (0.3s窗口)
        │     └──────┬───────┘
        │            ↓
        │     ┌──────────────┐
        │     │  VANISH      │ 消失 → 移动到新位置
        │     └──────┬───────┘
        │            ↓
        └────────────┘  循环

// Phase 2: 增加双影刺杀 (双倍压力)
// Phase 3: 增加碎片收集系统
```

### 袭击选择

```
// 荆轲的攻击选择逻辑 (每次袭击从以下选择)

function SelectAssassination():
    if phase == 1:
        moves = [
            (frontStab,    weight: 40),   // 正面突刺
            (sideStab,     weight: 30),   // 侧面突袭
            (overheadDive, weight: 30),   // 空翻背刺
        ]
    
    elif phase == 2:
        moves = [
            (frontStab,    weight: 30),   // 加速版
            (dualShadow,   weight: 30),   // 双影刺杀 (新增，不可格挡)
            (iceDive,      weight: 25),   // 冰遁突袭 (新增)
            (tripleStab,   weight: 15),   // 三连冲刺 (新增)
        ]
    
    else: // phase 3
        moves = [
            (frontStab,    weight: 25),   // 更快
            (dualShadow,   weight: 25),
            (iceDive,      weight: 20),
            (tripleStab,   weight: 15),
            (desperateLunge, weight: 15), // 最终一击 (新增)
        ]
    
    // 避免重复：同一种攻击不会连续出现两次
    return weightedRandomSelect(moves, exclude: lastAttackType)
```

### 核心行为：ATTACK 状态

```
// 以正面突刺为例

STATE FrontStab:
    ENTER:
        // 出现在玩家正面 5-8 格距离处
        appearPosition = playerPosition + playerDirection * 6
        instantTeleport(appearPosition)
        
        // 匕首反光效果（预警提示）
        playSound("dagger_gleam")
        spawnEffect("blade_flash", duration: 0.1s)
        
        // 前摇计时
        windupTimer = 0.3s
        isParryable = true
        parryWindow = 0.1s  // 极短窗口
    
    UPDATE:
        windupTimer -= deltaTime
        
        if windupTimer <= 0:
            // 冲刺攻击
            chargeSpeed = 25
            chargeDirection = (playerPosition - bossPosition).normalized
            rb.velocity = chargeDirection * chargeSpeed
            
            // 命中判定
            if hitPlayer:
                // 精准格挡判定：
                if player.isPerfectParrying and parryWindow > 0:
                    // 弹反成功！
                    ChangeState(PARRY_STAGGER)
                else:
                    // 造成伤害
                    player.TakeDamage(phase >= 2 ? 20 : 15)
                    // 穿到玩家身后
                    teleportBehind(player, distance: 3)
                    ChangeState(AFTERMATH)
            else:
                // 未命中，穿到远端
                ChangeState(AFTERMATH)
        
        // 格挡窗口计时
        if parryWindow > 0:
            parryWindow -= deltaTime
    
    EXIT:
        rb.velocity = Vector2.zero
```

### Phase 3 碎片机制

```
// 荆轲的匕首碎裂，留下碎片
// 每次攻击后，在地面生成碎片
// 碎片: 持续 5s，收集后 +5 气力 + 0.1s 前摇增加

STATE Aftermath:
    ENTER:
        visibleTimer = 0.3s  // 攻击后短暂可见
    
    UPDATE:
        visibleTimer -= deltaTime
        if visibleTimer <= 0:
            // 生成碎片
            if phase == 3:
                shard = spawnItem("dagger_shard", at: attackPosition)
                shard.lifetime = 5.0
            
            // 消失
            ChangeState(VANISH)
    
    // 碎片收集
    onPlayerCollectShard:
        player.restoreQi(5)
        jingKe.nextAttack.windupModifier -= 0.1s  // 前摇更长
        if shardsCollected >= 5:
            // 进入慢动作状态 5s
            Time.timeScale = 0.3
            playEffect("slow_motion")
            // 这是唯一的长时间输出窗口
```

---

## 6. 稷下魂·辩论 (规则型)

### 核心机制：规则系统

```
// 稷下魂不靠血量阶段决定行为
// 它靠"规则切换"控制战斗节奏
// 每次规则切换时，场上会出现发光书简堆

// 规则循环：
// 阶段1: A→B→C (40s/个)
// 阶段2: A→B→C→D (30s/个)
// 阶段3: B→D→E (25s/个)
// 阶段4: C→E→F (20s/个)

STATE RuleController:
    ENTER:
        currentRule = SelectRuleForPhase()
        ruleTimer = GetRuleDuration()
        displayRuleBanner(currentRule.name)
        activateBookPile(currentRule.bookPileIndex)
    
    UPDATE:
        ruleTimer -= deltaTime
        
        // 规则效果生效
        ApplyRuleEffect(currentRule)
        
        // 时间到 → 切换
        if ruleTimer <= 0:
            nextRule = GetNextRuleInCycle()
            ChangeRule(nextRule)
        
        // 玩家触碰发光书简堆
        if playerTouchesActivatedBookPile:
            // 玩家选择：提前结束此规则
            RemoveRuleEffect(currentRule, gradual: false)
            nextRule = GetNextRuleInCycle()
            ChangeRule(nextRule)
            // 奖励：回复 30 HP
            player.Heal(30)
```

### 规则实现

```
// 每条规则修改游戏参数

function ApplyRuleEffect(Rule rule):
    switch rule.id:
        case "法家·严刑":
            player.lightAttackDamage *= 1.5
            player.incomingDamage *= 1.5
            break
        
        case "道家·无为":
            player.attackQiCost = 0       // 不耗气
            player.qiRecoveryRate = 0     // 不恢复
            boss.moveSpeed *= 0.7
            break
        
        case "墨家·兼爱":
            player.attackRange *= 2.0     // 双倍攻击范围
            player.healAmount *= 0.5      // 治疗减半
            // Boss 每 10s 召唤纸兵
            StartCoroutine(SpawnPaperSoldiers(interval: 10s))
            break
        
        case "名家·诡辩":
            // 30%概率交换位置 (攻击命中/受击时)
            player.onHit += maybeSwapPosition(0.3)
            player.onDamaged += maybeSwapPosition(0.3)
            break
        
        case "阴阳家·五行":
            // 场地属性每 10s 轮转
            elements = [金,木,水,火,土]
            currentElement = elements[0]
            StartCoroutine(RotateElements(interval: 10s))
            boss.resistCurrentElement = true
            boss.weakToCounterElement = true
            break
        
        case "儒家·礼乐":
            player.canDash = false
            player.blockDamageReduction = 0.9  // 90%减伤
            boss.attackSpeed *= 0.8
            break
```

---

## 7. 嬴政·肇始 (最终Boss)

### 三阶段总图

```
Phase 1: 王座之间 (HP 800)
  ↓ Boss 击退 → 地板碎裂 → 空间扭曲
Phase 2: 七国熔炉 (HP 600)
  ↓ Boss 击退 → 空间崩塌 → 纯白虚空
Phase 3: 太一之辩 (HP 400)
  ↓ "理解度"判定 → 结局分支
```

### Phase 2：模仿系统

```
// 嬴政在 Phase 2 轮流使用前六个 Boss 的风格
// 每个风格持续 30s 或直到特定条件
// 顺序固定：韩→赵→魏→楚→燕→齐→融合

STATE Phase2Imitation:
    ENTER:
        currentStyleIndex = 0
        styleTimer = 30.0
        styleList = [HAN, ZHAO, WEI, CHU, YAN, QI, FUSION]
        
        // 首次显示当前模仿风格
        showStyleName(styleList[0].name)
    
    UPDATE:
        attackCooldown -= deltaTime
        styleTimer -= deltaTime
        
        // 执行当前风格的攻击
        if attackCooldown <= 0:
            ExecuteCurrentStyleAttack()
        
        // 风格切换
        if styleTimer <= 0 or bossHP ≤ nextThreshold:
            currentStyleIndex++
            if currentStyleIndex ≥ len(styleList):
                currentStyleIndex = len(styleList) - 1  // 保持在融合模式
            
            showStyleName(styleList[currentStyleIndex].name)
            styleTimer = 30.0
            
            // 切换特效
            playEffect("style_transition")
            screenShake(0.3, 0.3)

// 风格攻击示例
function ExecuteCurrentStyleAttack():
    switch currentStyle:
        case HAN:  // 柯弩将军 - 金色光矢版
            attackPool = [goldenBolt, fiveBoltSpread, decreeExplosion]
            speedMult = 1.2
        case ZHAO:  // 赵盾之灵 - 光盾版
            attackPool = [lightShieldBash, edgeSlash, soulShield]
            speedMult = 1.3
        case WEI:  // 魏武卒魂 - 重力版
            attackPool = [gravitySwing, earthStomp, tidalWave]
            speedMult = 1.0
        case CHU:  // 楚巫祭 - 星光版
            attackPool = [starOrbs, voidWeeds, galaxyRing]
            speedMult = 1.4
        case YAN:  // 荆轲执念 - 瞬移版
            attackPool = [voidStab, dualIllusion, blinkStrike]
            speedMult = 1.3
        case QI:  // 稷下魂 - 规则版
            attackPool = [ruleInscription, scholarSummon]
            speedMult = 1.0
            // 规则每 8s 切换一次
        
        case FUSION:  // 全部混合
            attackPool = 全部
            speedMult = 1.5
            // 每 3 次攻击换一个风格
    
    selectedAttack = weightedRandomSelect(attackPool)
    ExecuteWithSpeed(selectedAttack, speedMult)
```

### Phase 3：理解度系统

```
// 这不是要杀死嬴政的阶段
// 目标不是打空 HP 条，而是填满"理解度"条

理解度增加方式:
  - 精准格挡嬴政的攻击: +10%/次
  - 用相同风格还击: +15%/次  
     (如嬴政用韩弩招式 → 玩家也用远程/冲刺攻击回应)
  - 连续格挡5次不攻击: +20% (一次性奖励)
  - 在嬴政面前不动3s: +5%/次
  - "迟来的刺"特殊触发: +30% (一次性隐藏)

理解度减少方式:
  - 主动攻击嬴政: -5%/次
  - 使用丹药: -10%/次 (在嬴政看来这是"你还未放下")

结局分支 (在嬴政 Phase 3 HP ≤ 0 时判定):
  - 理解度 ≥ 80% → 结局二: 不朽者
  - 理解度 < 40% → 结局一: 统一者
  - 40-80% → 默认为统一者

// 嬴政在此阶段的攻击不致命
// 伤害极低 (3-5/击)，速度慢
// 他的动作更像一次"演示"而不是"攻击"
// 连续被击中不会死亡——HP降到1后会锁血

STATE Phase3Demonstration:
    ENTER:
        // 嬴政松开武器
        // 他的姿态从战斗变为演示
        playAnimation("relax_grip")
        musicCrossfade("qin_sad_theme", duration: 2.0s)
        
        // 所有攻击变为慢速教学版
        attackSpeed = 0.3  // 所有前摇变为 3x
        
        // 开始无休止的"演示循环"
        demonstrationCycle = [
            "han_crossbow",   // 演示远程
            "zhao_shield",    // 演示防守
            "wei_heavy",      // 演示重击
            "chu_magic",      // 演示弹幕
            "yan_assassin",   // 演示速度
            "qi_rules",       // 演示规则
            "repeat"
        ]
        currentDemonstration = 0
        understanding = 0.0
        HP_is_locked = true
        minHP = 1
    
    UPDATE:
        // 执行演示动作
        ExecuteDemonstration(demonstrationCycle[currentDemonstration])
        
        // 检测玩家行为 → 更新理解度
        CheckPlayerResponse()
        
        // 演示完成 + 玩家无响应 → 下一演示
        if demonstrationComplete:
            currentDemonstration = (currentDemonstration + 1) % 6
            // 第7次循环时，如果理解度>80%，触发结局2
            if cycleCount >= 6 and understanding >= 80:
                TriggerEnding(2)
        
        // 如果理解度始终很低，嬴政会慢慢消散
        if elapsed > 120 and understanding < 40:
            TriggerEnding(1)

// 特殊隐藏：荆轲的回应
function CheckSpecialTrigger():
    // 在嬴政演示 yan_assassin (燕刺客) 动作时
    if currentDemonstration == "yan_assassin" and player_did_not_evade_and_did_not_block:
        // 玩家不做任何操作，站着让嬴政的剑刺中
        // 但实际上剑在喉咙前 0.1 寸停住
        playSpecialCutscene("jingke_appears")
        // 荆轲的幻影显现
        jingkeGhost.spawn()
        jingkeGhost.say("……")
        // 剑停在嬴政自己喉咙前
        understanding += 30
        player.HP = 1  // 锁血效果在此
```

---

## AI 实现通用原则

### 攻击冷却 (Cooldown)

```csharp
// 所有 Boss 共享的冷却管理
attackCooldown = baseCooldown * Random.Range(0.8f, 1.2f)
// 根据阶段调整
if phase >= 2: attackCooldown *= 0.85f
if phase >= 3: attackCooldown *= 0.75f
```

### 玩家距离感知

```csharp
// 所有 Boss 的通用距离分类
enum DistanceZone {
    CLOSE_RANGE,    // 0-2 格: 近战范围
    MID_RANGE,      // 2-5 格: 中距离
    FAR_RANGE,      // 5-8 格: 远程范围
    OUT_OF_RANGE    // 8+ 格: 失锁范围
}
```

### 连出惩罚

```csharp
// 避免 Boss 反复出同一招
// 连续两次相同攻击 → 权重 * 0.5
// 连续三次相同攻击 → 强制切换
consecutiveCounters[attackType]++
if consecutive >= 2:
    attackWeight *= 0.5
if consecutive >= 3:
    forcedNextType = random different type
```

### 挑衅行为

```csharp
// 当玩家长时间不攻击（超过10s）
// Boss 进入"挑衅"状态
// 降低防御 + 增加破绽
// 鼓励玩家主动攻击而非全程龟缩
if timeSinceLastPlayerAttack > 10.0:
    boss.defense *= 0.8
    boss.parryWindow *= 0.5
    boss.staggerTime *= 1.3
    playAnimation("taunt")
```

