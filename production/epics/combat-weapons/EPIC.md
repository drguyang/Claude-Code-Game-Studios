# Epic: 格斗与武器线

> **Layer**: Core
> **GDD**: design/gdd/combat-and-weapon-lines.md
> **Architecture Module**: L2 Sim(门 A 侧 · SIM)+ Tooling(烘焙断言)+ Gameplay.Presentation(表现契约,另批)
> **Status**: Ready
> **Stories**: 6 stories — see table below

## Overview

格斗与武器线(25)是「暴力→医学」的翻译层:它拥有**命中判定**(sim 整数格距离 + 范围 + tick 冷却)、**伤情注入**(onset 事件写入)、**归零转译入口**三件事;它**不拥有**伤情语义、生命/昏迷真相、HurtLevel 折叠、体征读数(全归 9)。承重裁决:病人伤情落**病史流** `InjuryOnset`、敌人伤情落**世界流** `EnemyInjuryOnset`(ADR-009/016,一支 Kind 无法同时落两条流 ⇒ 两支并存,写者均为 25);敌人生命归零 = `INJ_COMA`(非致命模型,9 执行);`CombatPower` 以 `Fix` 从 30 单点传入(ADR-026);`magnitude = clamp(MAG_FLOOR + base_step × CP/CP_MAX, floor, cap)` 全程 Q16.16 定点,敌/兽 CP:=0 退化式(不引第二公式);PS 单位使 9 的 F1 入口 `SCALE_d` 对消(R11)。规则〇的时序纪律:意图不排队(无缓冲)、tick 内次序 = `ActorCellEntered` → 攻击求值 → onset `Append`;占用门 `Occupied(a)` 为事件流求值的纯函数,压制不占用、Natural 占用、占用不发事件。F-25-8 构建期平衡断言(逐动作/逐 bar × CP∈{0,CP_MAX})是数值轮的验收门。**全部逐值(OQ-25-7)冻结归用户**;78 条 AC 中 34 [U] / 6 [P] / 30 [B] / 8 [V],本 epic 覆盖 [U]/[B] 主体,[V] 表现项走查移交 42/44/27 各 epic;`AC-25-6-08`(玩家致死路径)BLOCKED-BY-9(`SelfLimited` 未落地)禁记绿。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-016: AI 架构(分层确定性) | 27 的 Engage 意图入向、零数值出向;敌人复用 9 伤情模型,id 经 IIdAuthority 与病人共用空间;敌人伤情落世界流 | MEDIUM |
| ADR-009: 世界状态事件化边界 | `InjuryOnset`(病史)/ `EnemyInjuryOnset`(世界)的流别归属;三流全序键 | MEDIUM |
| ADR-026: 技能成长定点化 | `CombatPower = (格斗等级 × WeaponMultiplier) × (1 + 医术修正)` 以 Fix 传入 25(单一交接点) | LOW |
| ADR-005: 确定性模拟 | magnitude / d2 / 冷却全整数定点;占用态由事件流+cooldown 重放推出;主机唯一 Step | HIGH |
| ADR-006: 定点域边界 | base_step 等 Fix 字段 JSON 写字符串经 FixParse;禁浮点入流 | MEDIUM |
| ADR-014: 数据管线 | `assets/data/combat_actions.json` 两阶段烘焙;构建期 schema/闭集/溢出断言 | MEDIUM |
| ADR-024: Kind 单一登记真源 | `entities.yaml` 三支 Kind 登记(stream/author/payload_schema);kindgen 生成路由 | LOW |
| ADR-020: 玩家控制器 | MotorSuppressed 唯三调用者 {4,10,25};压制只锁走位/Jump 不锁攻击 | LOW |
| ADR-011: 输入架构 | Attack = 普通动作回调路径(非急救直读通道);3 的动作表已回填补 `Attack` 行(R19b) | HIGH |
| ADR-012: 跨平台确定性 CI 门 | 乘加中间量 hi/lo 纪律;黄金夹具矩阵(另批) | HIGH |

**Engine Risk**: **HIGH**(挂 ADR-005/011/012 的 IL2CPP 逐位实测与 Input System 6.3 行为前置;但这些均**不阻塞机制实现** —— 接口层为纯 C# 契约,矩阵对拍另批复跑,禁借绿)。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-combat-*`,共 24 条)。
> 计数(2026-09-28 实测):**24 条 = 17 covered + 6 partial + 1 gap**(TR-combat-024 玩家致死形态 OQ-25-1 未裁)。partial 主因 = 「裁定 ≠ 验收」降级(TD C4)与待二轮评审,不阻塞实现轮;gap 项对应 story-006 的 BLOCKED-BY-9 面。

| TR-ID | Requirement(摘要) | Status |
|-------|--------------------|--------|
| TR-combat-001 | 非致命模型:归零 = INJ_COMA 投影,敌/兽永不 death | covered |
| TR-combat-002 | 命中判定全在 sim 整数域,可逐位重放 | covered |
| TR-combat-003 | magnitude 为 Fix(PS 单位),clamp 带,MAG_FLOOR>0 构建硬拒 | covered |
| TR-combat-004 | 敌/兽 CP:=0 退化式,无 enemy_cp 旋钮 | partial(待二轮) |
| TR-combat-005 | onset 三元组 Kind 登记(病史/世界两支 + InjuryStateChanged=9) | covered |
| TR-combat-006 | onset 去重五元组键,dose_seq 为流派生,禁可变计数器 | covered |
| TR-combat-007 | 占用门 = 事件流纯函数;三边界(压制不占用/Natural 占用/不发事件不排队) | covered |
| TR-combat-008 | IsSuppressed 公开只读查询;禁轮询;判据分侧 | partial(待二轮) |
| TR-combat-009 | 25 = MotorSuppressed 唯三调用者之一;只锁走位 | covered |
| TR-combat-010 | combat_actions.json 唯一真源;21a inflicts_injury 降级集合约束 | covered |
| TR-combat-011 | 战斗效能公式定义权归 30,25 只消费输出档 | partial(30 已修订,复验转实现轮) |
| TR-combat-012 | 战伤入 9 的 F1 加性阶跃入口;TRAUMA_CAP 单侧 clamp | covered |
| TR-combat-013 | 致死判定 = 逐实体门 LethalFor | covered |
| TR-combat-014 | HurtLevel 折叠归 9 + QueryHurtLevel;25 载荷不带档位 | partial(借绿降级) |
| TR-combat-015 | InjuryStateChanged emit 时机 = Step 求出越阈 tick;CatchUp 序列一致 | covered |
| TR-combat-016 | 击退 = 纯表现,零注入权 | covered |
| TR-combat-017 | 战斗乐层 = ADR-018 白名单第 5 类 + G1–G3 护栏 | covered |
| TR-combat-018 | 冷却判据 cd_eff = min 逐动作 + 逐 bar 上界断言;F-25-8 乘法形式 | covered |
| TR-combat-019 | 伤口通道第六条感知;INJ_BLUNT 绝不出血构建断言 | partial(借绿降级) |
| TR-combat-020 | 三时钟总图 T1/T2/T3;禁按动画碰撞帧发伤害 | partial |
| TR-combat-021 | 表现层可订阅 onset 做持续读法;禁入决策 | covered |
| TR-combat-022 | 27→25 有入向意图、无出向数值;规格依赖非运行接口 | covered |
| TR-combat-023 | tick 频率标定(OQ-25-8 = 20 Hz 已裁) | covered |
| TR-combat-024 | 玩家致死形态(OQ-25-1)+ Down 覆盖(OQ-25-3) | gap —— story-006 BLOCKED-BY-9 |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- AC-25-x-xx 的 [U]/[B] 面全部有通过测试;[P] PlayMode 6 条实现并有交互记录
- [V] 表现项 8 条(三时钟走查/无三段式/血渍读法等)移交 42/44/27 epic 走查闭环,本 epic 登记移交不代跑
- `AC-25-6-08` 维持 **BLOCKED-BY-9**(SelfLimited 未落地),不得记绿;OQ-25-1/25-2/25-3 结案后回补
- 全部数值旋钮保持「值归用户数值轮」标注,F-25-8 构建断言以符号化夹具验证门本身

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 动作表 schema 与烘焙构建门 | Config-Data | **Complete ✅ 2026-09-30** | ADR-014/006/021 |
| 002 | 攻击求值点:意图通道 · 占用门 · tick 内次序 | Logic | **Complete ✅ 2026-09-30** | ADR-005/016/011 |
| 003 | 命中判定 F-25-1 | Logic | **Complete ✅ 2026-09-30** | ADR-015/005 |
| 004 | magnitude 装配 F-25-2 与 CombatPower 交接 | Logic | **Complete ✅ 2026-09-30** | ADR-026/006 |
| 005 | 冷却 / 切换 / 压制(S-1/S-2/S-3 与 IsSuppressed) | Logic | **Complete ✅ 2026-09-30** | ADR-020/005 |
| 006 | onset 事件流 · dose_seq 派生 · F-25-8 平衡门 | Integration | **Complete ✅ 2026-09-30** | ADR-009/024/016 |
