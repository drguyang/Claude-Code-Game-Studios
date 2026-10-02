# Epic: 玩家控制器与移动

> **Layer**: Presentation(控制器与相机 · manifest 该节适用)
> **GDD**: design/gdd/player-controller-and-movement.md
> **Architecture Module**: 表现层(纯表现态位移)+ 世界流唯一投影(跨格事件)
> **Status**: In Review (6/6 stories Complete;EPIC 未转 Complete 仅因 **evidence 对账件缺口**,见下)
> **Stories**: 6 stories created (2026-09-28)

## Overview

玩家控制器与移动(系统 1)是全案「位移 = 纯表现态」裁决的执行者:移动由 `CharacterController`(kinematic,不参与 PhysX 求解)驱动,连续位置 / 速度 / 朝向**永不进三流**,其在 sim 中的唯一投影 = **跨格世界流事件 `ActorCellEntered`**(载荷只有整数格)。本 Epic 覆盖:① 移动程序集的边界纪律(引用集恰 = 边界程序集 + BCL、载荷类型递归纯净、1 不持有游戏状态);② 手感链(二维 `MoveInput` 经相机 yaw 基投影为世界方向 → 目标速度合成 → 加减速 → 转向 → 跳跃 → 地貌/情境乘数);③ 跨格检测与归并算子(事件率上界 = tick 频率,与帧率无关);④ 联机权责(Append 权 = 主机唯一,客户端经第二 QoS 上行其格)与 `MotorSuppressed` per-source lease 调用面。P0 只有一个移动档位(Walk,冲刺整档推 P1a,R7);接地模型的三命题不相容已裁「先 spike 再定」(`OQ-1-12`,P0 开工前须裁决)。**全部数值留白归用户**(AC-20-11),本 Epic 只交付形状与判据。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-020: 玩家控制器与相机 | 移动 = `CharacterController`(kinematic,AC-20-01);玩家位移 = 纯表现态,唯一投影 = 跨格世界流事件(AC-20-03 BLOCKING,判据 = 反射断言非 grep);Amendment B:**Append 权 = 主机唯一**,`ActorCellEntered` 走第二 QoS 不在可靠通道;全部手感数值留白(AC-20-11) | LOW |
| ADR-005: 确定性模拟 | `ITickProvider` / `IEventSink` 抽象点;事件率与 tick 挂钩不挂帧;载荷整数域 | HIGH(逐位性须实测,裁决不依赖) |
| ADR-006: 定点边界契约 | 载荷禁 float;`PatientId.None` 不污染高水位(Amendment B) | MEDIUM |
| ADR-009: 世界流边界 | `ActorCellEntered` 属世界流;有界性论证依赖「事件率上界 = tick 频率」 | MEDIUM |
| ADR-015: 世界几何 | 单一整数格 `WorldPos`;`LATTICE_SIZE` 单一装载常量;`slopeLimit` 与逻辑层同源;可走性 = 烘焙整数查表,禁视觉地形采样 | LOW |
| ADR-011: 输入架构 | `MoveInput` / `Jump` 动作映射供给(1 不消费 `Look`,朝向自动面向移动方向) | HIGH |
| ADR-025: 契约程序集清单 | `Sim.Contracts`(边界程序集)=`WorldPos` + 六抽象点;1 的引用集白名单判据(AC-1-28)的可执行对像 | LOW |
| ADR-001: 网络 pipe | 第二 QoS 通道 = latest-value 按 `ActorId` 索引;P0 预埋 P1b 实现 | MEDIUM |

**Engine Risk**: **LOW~HIGH 混载**。ADR-020 本体 LOW(`CharacterController` 长期稳定 API);抬到 HIGH 的是 ADR-011 输入面与 `OQ-1-12` 接地 spike(Unity 6.3 collide-and-slide / `isGrounded` 更新时机须实测)。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-player-*`)。
> 计数(实测):**7 条 = 6 covered + 1 partial**;**Untraced = None**。
> `TR-player-007` partial 的根因 = ADR-015 §一 尚未点名 `slopeLimit`/`stepOffset` 的几何取值(登记义务 `O-9`,落点 = ADR-015 · 6),**不阻塞 story**——形状与判据先落地,取值填入即被 `AC-1-33` 守住。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-player-001 | 移动模型 = `CharacterController`(不参与 PhysX 求解;无 Rigidbody 驱动);判据三条并列(`AC-1-01`) | ADR-020 ✅ |
| TR-player-002 | 玩家位移 = 纯表现态;唯一投影 = 跨格世界流事件(载荷纯净 + 事件率上界,`AC-1-02/03`) | ADR-020 ✅ |
| TR-player-003 | 移动 / 旋转轴由 ADR-011 动作映射供给;1 不消费 `Look`;`‖MoveInput‖` 是因子非开关(边界断言 `≤ 1`) | ADR-011 + ADR-020 ✅ |
| TR-player-004 | 移动手感旋钮的形状与归属(六组 Tuning Knobs;全部数值留白归用户) | ADR-020 ✅ |
| TR-player-005 | 二维 `MoveInput` → 世界方向的基 = 相机相对(读 2 的 `YawBasis`,F-1-8) | ADR-020 ✅ |
| TR-player-006 | 跨格事件 `Append` 权 = 主机唯一;客户端经第二 QoS 上行其格 | ADR-020 ✅ |
| TR-player-007 | `CharacterController` 参数契约(`slopeLimit`/`stepOffset`/`minMoveDistance`/`skinWidth`)的命名与归属 | ADR-020 ⚠️ partial(根因 = `O-9` 取值未点名,形状已定) |

## Stories

| # | Story | Type | Status | Layer | ADR |
|---|-------|------|--------|-------|-----|
| 001 | 控制器地基:`CharacterController` 唯一位移写入点 + 程序集边界白名单 | Integration | Complete ✅ 2026-10-02 | Foundation | ADR-020 + ADR-025 + ADR-015 |
| 002 | 输入契约与相机相对方向(`MoveInput` 边界断言 + F-1-8 基投影) | Logic | Complete ✅ 2026-10-02 | Foundation | ADR-011 + ADR-020 |
| 003 | 移动手感链:目标速度合成 / 加减速 / 转向 / 跳跃 / 地貌情境乘数 + 求值次序 | Logic | Complete ✅ 2026-10-02 | Core | ADR-020 + ADR-005 |
| 004 | 跨格检测与归并算子:整数化、事件语义全表(F-1-1b / EC-1…8) | Logic | Complete ✅ 2026-10-02 | Core | ADR-020 + ADR-009 + ADR-015 |
| 005 | 主机权威 Append 与第二 QoS 上行(客户端零 Append + 提交态归主机) | Integration | Complete ✅ 2026-10-02 | Core | ADR-020 + ADR-001 |
| 006 | 状态纯净与 `MotorSuppressed` per-source lease(零游戏状态反射断言 + 调用面) | Logic | Complete ✅ 2026-10-02 | Feature | ADR-020 + ADR-013 |

Counts: 4 Logic · 2 Integration = 6 total.
38 个 AC 条目(35 编号,`06` 拆三 / `20` 拆二)全覆盖:BLOCKING 32 全部落 story;ADVISORY 5(`04`/`20b`/`25`/`26`/`29`)落 story 003/004/006 的签核面;EXTERNAL 1(`22`)不进本 Epic 验收面(主语 = 29/45 的 GDD),在 story 005 登记挂账。

## Epic Status

**In Review** — **6/6 story Complete**(001–006)。零 ADR-blocked story(全部治理 ADR Accepted;`TR-player-007` 的 partial 不阻塞,见上)。

**EPIC 不转 Complete 的原因 = 评审报告原件缺口(见下 §🔴 第 2 条)** —— 6 个 story 的 AC 均已落地且测试全绿(全 epic 92 例),逐 BLOCKING 对账件已于 2026-10-02 补齐。**但双代理评审的报告原件从未落盘** ⇒ 「原判定是否完备」无法复核。**补落报告原件后即可转 Complete。**

### ⚠️ 状态回填轮记账(2026-10-02)

本 Epic 此前三处状态**互相矛盾且全部落后于实际**,已在本轮对齐:

| 来源 | 回填前 | 回填后 |
|---|---|---|
| 本 EPIC | `In Progress (1/6)` | `In Review (5/6)` |
| `epics/index.md` | `In Progress (2/6)` | `In Review (5/6)` |
| story 004/005/006 | 均 `Ready`,AC 零勾,Completion Notes `待填` | 004/005 Complete · 006 In Review |
| 实际 | 001–006 代码与测试**均已在库** | —— |

**成因**:`0db7830` / `e6be7ee` / `45056e6` 三次提交只落代码与测试,**未同步 story/EPIC/index 三处状态**;随后 `185063f` 又以一次编译未通过的提交自称「全量测试通过」,使后续所有引用该数字的状态记载失去依据(`5572d66` 已修编译并撤销其退化替换)。

### 🔴 逐 story 证据缺口(依「不得借绿」登记)

1. ~~**006 两条 BLOCKING 判据有缺陷**~~ ✅ **已闭(2026-10-02 判据修复轮)** —— `AC-1-27` 由字段名黑名单改为**字段类型白名单**(含负向夹具证明非恒真);`AC-1-23` 补 IL 调用点扫描 + UI 零符号引用双判据(各配非空转守卫)。`MotorLeaseTest` 14/14。明细见 `story-006` 的 `Criteria` / `Deviations`。
2. ~~**全部 6 个 story 均无 `production/qa/evidence/` 专项件**~~ ✅ **已闭(2026-10-02 对账轮)** —— `production/qa/evidence/reconciliation-player-controller-2026-10-02.md` 已落,逐 story 含「原判定 → 修复落点 → 实测证据 → 验证命令」,并经**独立复核**。
   ⚠️ **但该对账件揭示一处更深的局限**:双代理评审**报告原件从未落盘**,现存最早记录仅为各修复提交 message 的自述清单 ⇒ 对账件验证的是「自述的修复是否真在代码里」,**不验证「原判定是否完备」**。**后续双代理评审须落报告原件至本目录**,否则同类缺口会再生。
3. **005 的 `O-4`(45 侧登记行)仍悬空** —— 45 无 GDD(P1b),该义务未出现在任何 PR 描述中。
4. **004 的 AC-1-04 NOT-RUN** —— 需 45 联机夹具;已 `Assert.Skip`,非静默。
5. **004 的 codec 绕行未登记为 TODO** —— `CellTransitionDetector` 走 `PayloadRef` 三整数字段,不经 `Sim.Codec`;因 asmdef 未引用 `Sim.Codec`,接线须先加程序集依赖边。

## Key Cross-References

- **`OQ-1-12`(接地 spike)= Story 003 的开工前置**(用户裁定 P0 开工前须裁决;轴 2 进入条件、EC-1/9/10/11 共用该上游裁定)
- **Story 002 依赖系统 2 的 `YawBasis` 交付**(2 的 Epic Story 002 = 本故事的硬前置;1 的 `O-8`)
- **载体纪律**:GDD AC 节明写「判据已定、载体未建」——`tests/` CI 载体由 ADR-012 轮建;此前 AC 只可标「已定义」不得标「已验证」

## Next Step

Story 001(地基)先行;002 与 2 的 Epic B 组咬合后推进;003 等 `OQ-1-12` spike 结果回填接地判据。
