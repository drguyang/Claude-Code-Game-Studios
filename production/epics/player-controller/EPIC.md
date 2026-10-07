# Epic: 玩家控制器与移动

> **Layer**: Presentation(控制器与相机 · manifest 该节适用)
> **GDD**: design/gdd/player-controller-and-movement.md
> **Architecture Module**: 表现层(纯表现态位移)+ 世界流唯一投影(跨格事件)
> **Status**: Complete ✅ 2026-10-03(6/6 story Complete;两轮双代理评审的判据缺陷已修复 ⇒ 全 epic **88 例全过 + 3 例显式 NOT-RUN**;评审原件已落盘 `qa/evidence/review-player-controller-{2026-10-03,round2-2026-10-03}.md`)
> ⚠️ 本轮(2026-10-03)修复的判据缺陷:**A3**(`AC-1-06a/b/c` 由「只查 `SpeedWalk > 0`」改为接口形态扫描 + 6 侧差分神谕 + 变异性双向;`06c` 无载体 ⇒ 显式 NOT-RUN)· **A4**(`AC-1-10②` 真约束断言 + 格边长改**读生产同一实体**,消硬编码漂移)· **A5/A7**(`AC-1-07`/`1-23` 调用点与类型白名单真判据)· **#1**(`motor_lease` 白名单遍历改**全部已加载程序集**,消自指空转)· **#4**(`AC-1-18` 改**位置驱动**真跨格夹具)· 另 sync 六份 story 件的状态漂移(story 004/005/006 的 `Pending` 残留、story-001 重复 `Completion Notes` 节、story-002 空占位)。
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
| ADR-025: 契约程序集清单 | `Sim.Contracts`(边界程序集)=`WorldPos` + 七抽象点;1 的引用集白名单判据(AC-1-28)的可执行对像 | LOW |
| ADR-001: 网络 pipe | 第二 QoS 通道 = latest-value 按 `ActorId` 索引;P0 预埋 P1b 实现 | MEDIUM |

**Engine Risk**: **LOW~HIGH 混载**。ADR-020 本体 LOW(`CharacterController` 长期稳定 API);抬到 HIGH 的是 ADR-011 输入面;~~`OQ-1-12`~~ 接地模型 ✅ 2026-09-29 已裁(方案甲),残余 = 斜坡滑向量与 ε 的【桌面】实测(Unity 6.3 collide-and-slide / `isGrounded` 更新时机须实测)。

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

**Complete ✅ 2026-10-03** — **6/6 story Complete**(001–006)。零 ADR-blocked story(全部治理 ADR Accepted;`TR-player-007` 的 partial 不阻塞,见上)。

**转 Complete 的两个前置均已满足**:
1. ✅ **评审报告原件已落盘** —— `production/qa/evidence/review-player-controller-2026-10-03.md` 与 `review-player-controller-round2-2026-10-03.md`(含原判定 → 修复落点 → 验证命令)。
2. ✅ **评审查出的判据缺陷已修复并复跑** —— 详见头部 §Status 的 A3/A4/A5/A7/#1/#4 清单;`AC-1-06c` 无 Roslyn 载体 ⇒ **显式 NOT-RUN**(非静默)。

**Test Evidence(2026-10-03 batchmode 复跑,逐 fixture)**:`PlayerController` **91 例 = 88 Passed + 3 Skipped + 0 Failed**:
`ControllerFoundationTest` 11 · `InputContractTest` 16 · `LocomotionChainTest` 21(+2 skip)· `CellTransitionTest` 18(+1 skip)· `StreamBoundTest` 2 · `HostAuthorityTest` 6 · `MotorLeaseTest` 14。
3 例跳过 = `AC-1-06c`(NOT-RUN,无 Roslyn 载体)· `AC-1-21`(只剩 ε 实测,BLOCKED-BY-PlayMode)· `AC-1-04`(随 `AC-1-17` 接地半边,~~BLOCKED-BY-OQ-1-12~~ ✅ 已解除 / P0 无 VR)。

**仍未闭(登记,不阻塞本 Epic 转 Complete —— 均属 P1a/P1b 或外部主语)**:
- ~~`OQ-1-12`~~ ✅ **已裁 2026-09-29(方案甲:恒定下压 + 归零坡面水平投影),2026-10-07 传导订正** ⇒ `AC-1-17` 解除 BLOCKED-BY;`AC-1-21` 的 ε(静止推挤容差)仍待【桌面】PlayMode 实测
- `O-4` / `O-9`(45 侧登记行 · ADR-015 点名 `slopeLimit`/`stepOffset` 几何值)
- `AC-1-22`(EXTERNAL,主语 = 29/45)
- `AC-1-04`(VR,P1a)

### ⚠️ 状态回填轮记账(2026-10-02)

本 Epic 此前三处状态**互相矛盾且全部落后于实际**,已在本轮对齐:

| 来源 | 回填前 | 回填后 |
|---|---|---|
| 本 EPIC | `In Progress (1/6)` | `In Review (5/6)` |
| `epics/index.md` | `In Progress (2/6)` | `In Review (5/6)` |
| story 004/005/006 | 均 `Ready`,AC 零勾,Completion Notes `待填` | 004/005 Complete · 006 In Review |
| 实际 | 001–006 代码与测试**均已在库** | —— |

**成因**:`0db7830` / `e6be7ee` / `45056e6` 三次提交只落代码与测试,**未同步 story/EPIC/index 三处状态**;随后 `185063f` 又以一次编译未通过的提交自称「全量测试通过」,使后续所有引用该数字的状态记载失去依据(`5572d66` 已修编译并撤销其退化替换)。

### ⚠️ 状态漂移第二轮回刷(2026-10-03)

`2026-10-02` 那轮回填**只改了 story 件头行与 EPIC/index 的汇总计数,未触及文件体的 `Status:` 行与 AC 复选框** ⇒ 遗留三处新的自相矛盾(同一 story 件内,头行 `Complete` 而体 `Pending`)。本轮逐文件订正:

| story | 回填前(文件体) | 回填后 |
|---|---|---|
| 001 | 两个 `## Completion Notes`(前者 `_待填_`)+ 四栏 `_待填_` + :169 陈旧「`Assert.Pass` 简化版」 | 重复节合并为一;四栏填实;:169 标记已闭(真约束断言 + 同源读格边长) |
| 002 | `**Status**: [ ] Pending` + 四栏 `_待填_` | `[x] Done`(16/16)+ 四栏填实 |
| 003 | 头行 22/23(与实跑 21+2 不符)+ Deviations 两条已失效的「完整版需要…」 | 头行与 Criteria 订正;Deviations 重定为 06a/b/c 与 1-18 的实际修法 |
| 004 | `**Status**: [ ] Pending`,**14 条 AC 零勾** | `[x] Done`(18+1/2);14 条 AC 勾选(`AC-1-04` 标 **NOT-RUN**) |
| 005 | `**Status**: [ ] Pending`,AC 零勾 | `[x] Done`(6/6);`AC-1-30` + 联机侧边界勾选(**`AC-1-22` EXTERNAL 保持未勾**) |
| 006 | `**Status**: [ ] Pending`,AC 零勾 | `[x] Done`(14/14);4 条 AC 勾选(`AC-1-29` 标 **NOT-RUN**) |

⚠️ **口径**:AC 勾选 = 「判据已落 / 已登记」,**不**等于「该子条已通过」—— NOT-RUN / EXTERNAL / BLOCKED 的子条一律保持未勾或就地标注,承「不得借绿」纪律。

### 🔴 逐 story 证据缺口(依「不得借绿」登记)

1. ~~**006 两条 BLOCKING 判据有缺陷**~~ ✅ **已闭(2026-10-02 判据修复轮)** —— `AC-1-27` 由字段名黑名单改为**字段类型白名单**(含负向夹具证明非恒真);`AC-1-23` 补 IL 调用点扫描 + UI 零符号引用双判据(各配非空转守卫)。`MotorLeaseTest` 14/14。明细见 `story-006` 的 `Criteria` / `Deviations`。
2. ~~**全部 6 个 story 均无 `production/qa/evidence/` 专项件**~~ ✅ **已闭(2026-10-02 对账轮)** —— `production/qa/evidence/reconciliation-player-controller-2026-10-02.md` 已落,逐 story 含「原判定 → 修复落点 → 实测证据 → 验证命令」,并经**独立复核**。
   ⚠️ **但该对账件揭示一处更深的局限**:双代理评审**报告原件从未落盘**,现存最早记录仅为各修复提交 message 的自述清单 ⇒ 对账件验证的是「自述的修复是否真在代码里」,**不验证「原判定是否完备」**。**后续双代理评审须落报告原件至本目录**,否则同类缺口会再生。
3. **005 的 `O-4`(45 侧登记行)仍悬空** —— 45 无 GDD(P1b),该义务未出现在任何 PR 描述中。
4. **004 的 AC-1-04 NOT-RUN** —— 需 45 联机夹具;已 `Assert.Skip`,非静默。
5. **004 的 codec 绕行未登记为 TODO** —— `CellTransitionDetector` 走 `PayloadRef` 三整数字段,不经 `Sim.Codec`;因 asmdef 未引用 `Sim.Codec`,接线须先加程序集依赖边。

## Key Cross-References

- ~~**`OQ-1-12`(接地 spike)= Story 003 的开工前置**~~ ✅ **2026-09-29 已裁(方案甲),不再是前置** —— 轴 2 进入条件 / EC-1 / 9 / 10 / 11 的**方案已定**,2026-10-07 完成 GDD 正文与 EPIC 的传导回填;Story 003 的开工前置自此**清零**(残余 = `AC-1-34` 装载期断言 + ε / 斜坡滑向量实测)
- **Story 002 依赖系统 2 的 `YawBasis` 交付**(2 的 Epic Story 002 = 本故事的硬前置;1 的 `O-8`)
- **载体纪律**:GDD AC 节明写「判据已定、载体未建」——`tests/` CI 载体由 ADR-012 轮建;此前 AC 只可标「已定义」不得标「已验证」

## Next Step

**本 Epic 已 Complete(2026-10-03)** —— 无剩余实现工作。等待事项均属外部主语或 P1a/P1b:
- ~~`OQ-1-12`~~ ✅ **已裁**(方案甲)⇒ `AC-1-17` 已回填;`AC-1-21` 只剩 ε 实测;新立 `AC-1-34` 守「下压量 > skinWidth ∧ > minMoveDistance」
- `O-9`(ADR-015 §一 点名 `slopeLimit`/`stepOffset` 几何值)⇒ 回填 `AC-1-33②`
- `O-4`(45 侧登记行,P1b)· `AC-1-22`(EXTERNAL,主语 29/45)· `AC-1-04`(VR,P1a)

下一件(Sprint 04 Phase 2 关键路径):`interaction-system`(见 `production/sprints/sprint-04.md`)。
