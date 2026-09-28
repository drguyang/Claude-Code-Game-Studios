# Epic: 死亡与复活

> **Layer**: Core
> **GDD**: design/gdd/death-and-respawn.md
> **Architecture Module**: L2 Sim(门 A 侧 · SIM)+ Sim.Contracts(传送命令半)+ Gameplay.Presentation(过渡呈现,另批)
> **Status**: Ready
> **Stories**: 6 stories — see table below

## Overview

死亡与复活(29)只管**玩家**的死亡与归来:病人之死归 9+53。触发输入 = 9 的 `DeathCandidate`(经 `SelfLimited(entity,d)` 逐实体门,OQ-25-1 路甲),29 **不自行判死**(TR-death-001)。P0 代价 = 两件套:**掉落**(从死亡格回捡)+ **技能掉级**(`Level_new = (Level × 19) / 20` 的 C# int 截断除法 —— B2 定点陷阱明令禁 `Fix.Div`/floor_Mul;计时归 29、公式归 30,同 tick 运行期调用零重复实现);**无压力轴**(47 P1a)、无存档回滚、无尸体玩法。`death_cell` = 世界流中该玩家**最后一条 `ActorCellEntered`** 的格(禁读实时物理,B4 订正);掉落清单由流派生(`InventoryOf`)、位置由 7a 快照(格锚点 + 确定性格内偏移);29 零铸造 `instance_id`(归 20/`IIdAuthority`)。复活落点 = 医馆床具格(24 提供,选择规则欠 24 下轮)/ 兜底世界出生点;29 调 1 的 `TeleportTo(cell)`(经 `ITeleportCommandSink` 整数命令半,ADR-025 全案唯一跨门调用点),**29 自身 Append 计数 = 0**(AC-29-09)。死亡冷却 `DeathAllowed ⟺ t − last_death_tick ≥ DEATH_COOLDOWN`,且 `last_death_tick` **只从世界流 PlayerDied 重算**(零计数器,AC-29-15)。呈现铁律:无 Game Over 屏/文字/统计面板/失败音 —— 白名单 {过渡动画, 环境音, 42 拟物元件}(AC-29-06),DTO 递归扫描零代价数值字段(AC-29-07)。⚠️ 9 侧 `SelfLimited` 未落地 ⇒ AC-29-01/02/05/08/10/17 **BLOCKED-BY-9**、AC-29-16 **BLOCKED-BY OQ-7a-9**(SkillGrown 折叠豁免)—— 一律禁记绿。`DEATH_COOLDOWN` 值未定(OQ-29-5,须 >0);判断技能豁免口径待裁(2026-09-20 R-4-W-6),**不得当既有规则实现**。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-009: 世界状态事件化边界 | `PlayerDied` 落世界流(不落病史流);掉落实体身份进流/位置表现 | MEDIUM |
| ADR-020: 玩家控制器与相机 | 位移纯表现;跨格 `ActorCellEntered` 为 death_cell 唯一真源;移动禁用 = 1 读 PlayerDied(29 不在 MotorSuppressed 白名单) | LOW |
| ADR-025: 契约程序集清单 | 传送拆两半:整数命令 `ITeleportCommandSink.RequestTeleport(int, WorldPos)` 进 Sim.Contracts;Vector3 连续半留 Gameplay.Presentation | LOW |
| ADR-026: 技能成长定点化 | 掉级 = 对 30 §4.3 的同 tick 调用;SkillGrown 折叠豁免(案 1)决定掉级在重放中的存续 | LOW |
| ADR-006: 定点域边界 | `Level_new` 用 int 截断除法(非 Fix);存档禁 float;Seq 复用 | MEDIUM |
| ADR-010: 持久化与存档格式 | 位置不快照(格锚点+性格内偏移重建);三流序列化与折叠谓词 Folded(p) | MEDIUM |
| ADR-013: 拟物 UI 框架 | 呈现白名单 + DTO 递归扫描(代价数值不进呈现层);过渡元件归 42 | HIGH |
| ADR-024: Kind 单一登记真源 | `PlayerDied`/`DropSpawned`/`DropClaimed` 经 entities.yaml + kindgen | LOW |

**Engine Risk**: **MEDIUM**(主体纯 C#;悬置面 = ADR-013 DTO 守卫细节与 9 侧未落地接口 —— 后者是 BLOCKED 清单的根因,非引擎风险)。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-death-*`,共 8 条)。
> 计数(2026-09-28 实测):**8 条 = 6 covered + 2 partial**;无 gap。

| TR-ID | Requirement(摘要) | Status |
|-------|--------------------|--------|
| TR-death-001 | 29 只消费 DeathCandidate,不自行判死 | partial(9 侧 SelfLimited 未落) |
| TR-death-002 | PlayerDied 世界流事件 | covered(ADR-009) |
| TR-death-003 | death_cell 由 sim 整数格派生,禁读表现态 | covered(ADR-020+009) |
| TR-death-004 | 复活清单/掉落回收 = 事件流重放重建;位置不快照 | covered(ADR-009+023) |
| TR-death-005 | 掉级 = fold 投影(SkillGrown 参与重放折叠) | partial(OQ-7a-9 豁免面) |
| TR-death-006 | 复活传送走 ITeleportCommandSink 整数命令半 | covered(ADR-025) |
| TR-death-007 | 死亡线三半边落点:判定 Sim / 命令 Sim.Contracts / 呈现 Presentation | covered(ADR-025) |
| TR-death-008 | 死亡呈现白名单 + DTO 只读投影 | partial(ADR-013,实现轮) |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- AC-29-01…17 中非 BLOCKED 条目全部有通过证据;BLOCKED-BY-9 / BLOCKED-BY-OQ-7a-9 条目维持显式标注,禁借绿
- AC-29-11/12 的 [L] 人工走查在证据目录留档(承 GDD 原标注)
- UX Flag:全屏过渡经 `/ux-design` spec 先于本 epic 的呈现故事关闭(29 的 epics 前置,GDD 明写)
- `DEATH_COOLDOWN` / 判断技能豁免等未裁项保持「待裁」标注,数值归用户数值轮

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | PlayerDied 登记与死亡结算入口 | Integration | Ready | ADR-009/024/005 |
| 002 | 掉落清单流派生与回捡 | Integration | Ready | ADR-009/010/020 |
| 003 | 技能掉级 F-29-1(整数截断)与调 30 | Logic | Ready | ADR-026/006 |
| 004 | 死亡冷却 F-29-2(从流重算) | Logic | Ready | ADR-005/009 |
| 005 | 复活落点与传送命令 | Integration | Ready | ADR-025/020/024(24 联动) |
| 006 | 呈现白名单与零状态播报 | UI | Ready | ADR-013/018/020 |
