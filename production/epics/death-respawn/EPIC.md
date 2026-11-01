# Epic: 死亡与复活

> **Layer**: Core
> **GDD**: design/gdd/death-and-respawn.md
> **Architecture Module**: L2 Sim(门 A 侧 · SIM)+ Sim.Contracts(传送命令半)+ Gameplay.Presentation(过渡呈现,另批)
> **Status**: **6/7 Complete**(2026-11-02 —— 新立 story-007 `Ready ⬜`;原 2026-11-01 口径 6/6 —— UX 硬前置已由 `design/ux/death-transition-29.md` Approved 兑现;余项 = [L] 走查/听测待桌面批 + 42 侧视觉装配归表现资产批)
> **Stories**: 7 stories(001–006 Complete + **007 Ready ⬜**)—— see table below

## Overview

死亡与复活(29)只管**玩家**的死亡与归来:病人之死归 9+53。触发输入 = 9 的 `DeathCandidate`(经 `SelfLimited(entity,d)` 逐实体门,OQ-25-1 路甲),29 **不自行判死**(TR-death-001)。P0 代价 = 两件套:**掉落**(从死亡格回捡)+ **技能掉级**(`Level_new = (Level × 19) / 20` 的 C# int 截断除法 —— B2 定点陷阱明令禁 `Fix.Div`/floor_Mul;计时归 29、公式归 30,同 tick 运行期调用零重复实现);**无压力轴**(47 P1a)、无存档回滚、无尸体玩法。`death_cell` = 世界流中该玩家**最后一条 `ActorCellEntered`** 的格(禁读实时物理,B4 订正);掉落清单由流派生(`InventoryOf`)、位置由 7a 快照(格锚点 + 确定性格内偏移);29 零铸造 `instance_id`(归 20/`IIdAuthority`)。复活落点 = 医馆床具格(24 提供,选择规则欠 24 下轮)/ 兜底世界出生点;29 调 1 的 `TeleportTo(cell)`(经 `ITeleportCommandSink` 整数命令半,ADR-025 全案唯一跨门调用点),**29 自身 Append 计数 = 0**(AC-29-09)。死亡冷却 `DeathAllowed ⟺ t − last_death_tick ≥ DEATH_COOLDOWN`,且 `last_death_tick` **只从世界流 PlayerDied 重算**(零计数器,AC-29-15)。呈现铁律:无 Game Over 屏/文字/统计面板/失败音 —— 白名单 {过渡动画, 环境音, 42 拟物元件}(AC-29-06),DTO 递归扫描零代价数值字段(AC-29-07)。⚠️ 9 侧 `SelfLimited` 未落地 ⇒ AC-29-01/02/05/08/10/17 **BLOCKED-BY-9**、AC-29-16 ~~**BLOCKED-BY OQ-7a-9**(SkillGrown 折叠豁免)~~ **BLOCKED-BY-7a 折叠实现**(〔2026-10-20 订正:`OQ-7a-9` 已于 `ADR-026 §⑦` 2026-09-23 结案 = `SkillGrown` 豁免折叠;阻塞性质由「裁决待定」改为「实现待落」,实测 `Folded(p)` 生产谓词全库零命中〕)—— 一律禁记绿。`DEATH_COOLDOWN` 值未定(OQ-29-5,须 >0);判断技能豁免口径待裁(2026-09-20 R-4-W-6),**不得当既有规则实现**。 🔁 **2026-10-21 订正(原文不删,就地加注)** —— 本句**一半已过期**:`OQ-29-5` **已于 2026-10-06 代拍裁定 = `2400` tick(= 120 s @20 Hz,值可调)**,见 `design/gdd/death-and-respawn.md:280` 与 `:535` 结案行 ⇒ 「值未定」不再成立(**「须 > 0」硬约束仍有效**)。**未过期的另一半** = 判断技能豁免口径(`R-4-W-6`)**确仍未裁**(`death-and-respawn.md:333` / `:520`),该项继续「不得当既有规则实现」。⚠️ 两半同句,**不得整句作废**。

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
| TR-death-005 | 掉级 = fold 投影(SkillGrown 参与重放折叠) | partial(**豁免面已裁** = `ADR-026 §⑦` 案 1,`SkillGrown` 不随折叠抹除;剩「`Folded(p)` 实现未落」—— 2026-10-20 订正) |
| TR-death-006 | 复活传送走 ITeleportCommandSink 整数命令半 | covered(ADR-025) |
| TR-death-007 | 死亡线三半边落点:判定 Sim / 命令 Sim.Contracts / 呈现 Presentation | covered(ADR-025) |
| TR-death-008 | 死亡呈现白名单 + DTO 只读投影 | partial(ADR-013,实现轮) |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- AC-29-01…17 中非 BLOCKED 条目全部有通过证据;BLOCKED-BY-9 / **BLOCKED-BY-7a 折叠实现**(原 BLOCKED-BY-OQ-7a-9,2026-10-20 订正)条目维持显式标注,禁借绿
- AC-29-11/12 的 [L] 人工走查在证据目录留档(承 GDD 原标注)
- UX Flag:全屏过渡经 `/ux-design` spec 先于本 epic 的呈现故事关闭(29 的 epics 前置,GDD 明写)
- `DEATH_COOLDOWN` / 判断技能豁免等未裁项保持「待裁」标注,数值归用户数值轮 🔁 **2026-10-21 订正(原文不删,就地加注)** —— **`DEATH_COOLDOWN` 已不在「未裁项」之列**:`OQ-29-5` 已于 2026-10-06 代拍裁定 = `2400` tick(120 s @20 Hz,值可调;`death-and-respawn.md:280` / `:535`)⇒ 本条现**只剩「判断技能豁免口径」一项**保持待裁(`R-4-W-6`,2026-09-20,`death-and-respawn.md:333` / `:520`)。🔁 **2026-11-01 结案(原文不删)**:R-4-W-6 已由 **2026-09-29 用户裁定**解决(豁免撤销,全部技能统一掉级;S23-3 勘察漏检误标待裁,已如实登记)⇒ 本 epic **零未裁项**。数值轮的适用范围随之收窄为「`DEATH_COOLDOWN` 的**再调值**」,**不是「首次定值」**。

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | PlayerDied 登记与死亡结算入口 | Integration | **Complete**(2026-11-01 · S23-1;AC-29-01 全链绿随 G-1 结案) | ADR-009/024/005 |
| 002 | 掉落清单流派生与回捡 | Integration | **Complete**(2026-11-01 · S23-2) | ADR-009/010/020 |
| 003 | 技能掉级 F-29-1(整数截断)与调 30 | Logic | **Complete**(2026-11-01 · S23-3;30 权威面订正同批) | ADR-026/006 |
| 004 | 死亡冷却 F-29-2(从流重算) | Logic | **Complete**(2026-10-24 · 谓词半边;AC-29-08/10 全链 BLOCKED-BY-9 → **该阻塞已随 G-1 结案于 2026-11-01 闭**) | ADR-005/009 |
| 005 | 复活落点与传送命令 | Integration | **Complete**(2026-11-01 · S23-5 接口先行;**S23-7 生产接线已兑付** —— 1 侧 `TeleportCommandSink` 实装 + 装配,AC-29-10 实发数有真链(PM 实测落格事件);床格端口仍 BLOCKED-BY-24 = 实现待落) | ADR-025/020/024(24 联动) |
| 006 | 呈现白名单与零状态播报 | UI | **Complete**(2026-11-01;[L] 两条 AC-29-11/12 待桌面批走查/听测;**S23-7 补 42 侧水墨视觉装配** —— 相位机 + 呈现器 + 程序化水墨纹理(正式素材归美术资产批)) | ADR-013/018/020 |
| 007 | 29 的档位请求生产载体 —— `Downed` / `Explore`(requesterId 29)+ `TR-camera-007` 补登记 | Integration | **Ready ⬜** —— 2026-11-02 立件(§14 按 epic 扫描查出的**缺口 A**:相机闭包 `a5f766f6` 已进 `BootRoot`,但本 epic 内零 story 提它 ⇒ 改接线无 AC 会红;**另查出 GDD 的 29 两行无 TR**) | ADR-020 Amd C ①② / §五 |

---

## 🔁 2026-11-02 跨 epic 登记:29 的两段式倒下相机**自此才真正生效**

**事实**:`CameraRig.Tick` 此前**零生产调用点**(由 `emergency-procedures/story-010` 查出),
⇒ `CameraModeMachine.Settle` 从未执行 ⇒ 29 发出的 `Downed` 请求**只是入队,永不结算** ——
相机不会下沉、不会微倾,`CameraDownedDriver` 的相位机也从不推进。
`sprint-24`(ADR-020 Amendment C 那轮)交付的 `Downed` 档位、`GetLookTarget` 微倾、双参 `SetMode`
**都已实现且单测覆盖,但生产上是死路**。

**处置**:用户 2026-11-02 裁定由 `story-010` 顺带接上 `BootRoot.Update` 里的
`_cameraRig.Tick(...)`。

**⚠️ 本 epic 侧须承接的行为面**:
- `AC-29-11` / `AC-29-12`(`[L]`)的**真机走查**自此才有意义 —— 此前它们即使跑也测不到东西。
  状态**维持 `NOT-RUN`**(本批未跑,batch 模式结构性不可判图形面)。
- 若真机发现倒塌两段式的时长/幅度不合手感,归本 epic 或机位轮,数值在 `CameraDownedPhases`
  (已由用户数值轮裁定:`SagSeconds 1.2` / `SagDepth 0.45` / `SagTiltDegrees 10`)。
