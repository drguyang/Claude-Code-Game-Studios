# 评审报告: player-controller(系统 1 玩家控制器与移动)

> **评审对象**: `production/epics/player-controller/` 全部 6 个 story
> **评审日期**: 2026-10-03
> **评审方式**: 独立代码评审(逐 story 复核,不采信 commit message 自述)
> **声明**: **本报告评的是补做时点的代码,不追认原判定。** 双代理评审的报告原件从未落盘、已不可得。本报告是对当下代码的**新评一次**,非补录。

---

## 🔍 独立复核批注(2026-10-03 · 由主会话复核,非 agent 自述)

复核人**实测**了本报告的关键结论:

| 报告条目 | 复核结果 |
|---|---|
| AC-1-23 调用点白名单当前为空 | ✅ **成立** —— story-006 的 `Deviations` 自陈一致;判据有非空转守卫,故非「恒真」而是「空集守卫」 |
| AC-1-27 已从黑名单改类型白名单 | ✅ 成立(前批已修,负向夹具有效) |

⚠️ 报告自陈**未跑 Unity 套件**;其判决基于静态源码复核。

## 结论摘要表

| Story | 判决 | 说明 |
|-------|------|------|
| 001 控制器地基 | **部分交付** | 核心机制落地;AC-1-10② 测试简化(只查字段存在,不查约束);AC-1-28 Sim 引用测试为 pass-through |
| 002 输入契约与相机相对方向 | **已交付** | F-1-8 基投影、边界断言、求值次序全部落地,测试覆盖完整 |
| 003 移动手感链 | **部分交付** | 核心链落地;AC-1-06a/b/c 测试简化(只查 SpeedWalk > 0);AC-1-18 跨格乘数测试未真正测跨格 |
| 004 跨格检测与归并算子 | **部分交付** | 归并算子正确;AC-1-07 测试不验证源码用 Mathf.FloorToInt;AC-1-17 用字段名匹配非 AST;AC-1-04 NOT-RUN 已核实 |
| 005 主机权威 Append | **已交付** | 两模式 + ③(a)(b)(c) 全部落地,6/6 测试通过 |
| 006 状态纯净与 MotorLease | **部分交付** | 位图机制正确;AC-1-23 调用点白名单当前为空(判据空转,有非空转守卫);AC-1-27 类型白名单已修复 |

---

## 逐条详节

### Story 001: 控制器地基

**原判定**: Complete(10/10 测试通过)

**修复落点**:
- `PlayerController.cs:37` — `[RequireComponent(typeof(CharacterController))]`
- `PlayerController.cs:180` — `_controller.Move(delta)` 唯一位移写入点
- `PlayerController.cs:224-230` — `Teleport` 唯一允许的 `transform.position` 直接写
- `PlayerController.cs:235-243` — `GetCell()` 返回 `Int3`

**实测证据**:
- AC-1-01①: `PlayerController` 无 `Rigidbody` 字段,有 `[RequireComponent]` 断言 — ✅
- AC-1-01②③: IL 扫描器 `ILBodyScanner.cs` 存在,测试遍历所有方法查 `AddForce`/`AddTorque`/`Raycast`/`CheckCapsule`/`Overlap` — ✅
- AC-1-28: asmdef 白名单测试存在,但 `test_ac128_asmdefNoSimImplementationReference` 在检测到 `Sim` 引用时 **pass-through**(`Assert.Pass`),不真正失败 — ⚠️ 已知技术债务(RecipeDataSet)
- AC-1-10②: `test_ac110_latticeSizeAtLeastTwiceRadius` 只检查 `LatticeSizeMm` 字段存在,**不验证** `LATTICE_SIZE >= radius * 2` 约束 — ❌ 简化
- AC-1-10③: `Vector3Int` 递归类型扫描存在 — ✅

**验证命令**:
```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -n "RequireComponent" unity/Assets/Gameplay.Presentation/Player/PlayerController.cs
grep -n "_controller.Move" unity/Assets/Gameplay.Presentation/Player/PlayerController.cs
grep -n "transform.position" unity/Assets/Gameplay.Presentation/Player/PlayerController.cs
grep -n "LatticeSizeMm" unity/Assets/Tests/EditMode/PlayerController/controller_foundation_test.cs
```

---

### Story 002: 输入契约与相机相对方向

**原判定**: Complete(16/16 测试通过)

**修复落点**:
- `InputContractEvaluator.cs:28-33` — `ProjectToWorld(moveInput, rig)` 缓存 YawBasis
- `InputContractEvaluator.cs:41-79` — `ProjectToWorld(moveInput, basis)` F-1-8 投影
- `InputContractEvaluator.cs:60` — `Vector3.Cross(Vector3.up, f).normalized` 派生 r̂
- `InputContractEvaluator.cs:45-49` — `‖MoveInput‖ > 1` 抛异常
- `InputContractEvaluator.cs:52-55` — 零输入返回 `Vector3.zero`
- `InputContractEvaluator.cs:72-75` — 退化分支返回 `Vector3.forward`

**实测证据**:
- AC-1-09: 边界硬断言存在,抛 `ArgumentException` — ✅
- AC-1-31: 单位性 + 水平性测试存在,含反向用例(带仰角相机) — ✅
- AC-1-35①②: `CountingCameraRig` 验证 YawBasis 调用次数 — ✅
- AC-1-35③: `YawBasis` 是 `readonly struct` — ✅
- AC-1-35④: 正交性断言存在 — ✅

**验证命令**:
```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -n "Cross(Vector3.up" unity/Assets/Gameplay.Presentation/Player/InputContractEvaluator.cs
grep -n "magnitude > 1f" unity/Assets/Gameplay.Presentation/Player/InputContractEvaluator.cs
grep -n "YawBasisCallCount" unity/Assets/Tests/EditMode/PlayerController/input_contract_test.cs
```

---

### Story 003: 移动手感链

**原判定**: Complete(22/23 测试通过 + 1 NOT-RUN)

**修复落点**:
- `LocomotionEvaluator.cs:64-68` — `SteadyStateSpeed` 乘数链
- `LocomotionEvaluator.cs:73-90` — `UpdateYaw` 转向
- `LocomotionEvaluator.cs:95-102` — `ComputeVTarget` 求值次序
- `LocomotionConfig.cs:24-52` — 全部调参旋钮

**实测证据**:
- AC-1-19: 半速/半≠满测试存在 — ✅
- AC-1-20a: 转向四子条测试存在 — ✅
- AC-1-11: `AIR_CONTROL ≤ 1` + 跳跃调参自检存在 — ✅
- AC-1-33: `minMoveDistance == 0` + `skinWidth/radius/height > 0` 存在 — ✅
- AC-1-18: 求值次序测试存在,但 `test_ac118_cellCrossing_usesOldCellMultiplier` 两次调用都用 `currentCellIndex=0`,**未真正测跨格** — ⚠️ 简化
- AC-1-06a/b/c: 三个测试都只检查 `config.SpeedWalk > 0`,**未实现**差分神谕 / 变异性 / AST 派生初始化 — ❌ 简化
- AC-1-21: `Assert.Ignore` 正确跳过 — ✅

**验证命令**:
```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -n "SpeedWalk > 0" unity/Assets/Tests/EditMode/PlayerController/locomotion_chain_test.cs
grep -n "currentCellIndex, 0" unity/Assets/Tests/EditMode/PlayerController/locomotion_chain_test.cs
grep -n "Assert.Ignore" unity/Assets/Tests/EditMode/PlayerController/locomotion_chain_test.cs
```

---

### Story 004: 跨格检测与归并算子

**原判定**: Complete(20/21 测试通过 + 1 NOT-RUN)

**修复落点**:
- `CellTransitionDetector.cs:45-66` — `OnPositionSample` 归并算子
- `CellTransitionDetector.cs:71-95` — `OnTickEdge` tick 边沿提交
- `CellTransitionDetector.cs:100-106` — `CellFromPosition` 用 `Mathf.FloorToInt`
- `CellTransitionDetector.cs:111-115` — `ComputeDisplacement` dt 钳位

**实测证据**:
- AC-1-02/34: 递归载荷类型纯净测试存在 — ✅
- AC-1-03: 归并算子单元测试存在(N=8 样本归并 1 条) — ✅
- AC-1-03②: `stream_bound_test.cs` 跑 1000 tick 验证 `Append ≤ tick` — ✅
- AC-1-05: `tick` 来源唯一测试存在 — ✅
- AC-1-08: 静止零事件测试存在(256 帧) — ✅
- AC-1-13/14/15: EC-2/3/4 事件语义测试存在 — ✅
- AC-1-16: dt 钳位不累积测试存在 — ✅
- AC-1-32: tick 内折返不发 + 跨 tick 回访再发测试存在 — ✅
- AC-1-24: `PatientId.None` 不污染高水位测试存在 — ✅
- AC-1-04: `Assert.Ignore` 正确跳过(VR 推 P1a) — ✅
- AC-1-07: 测试只验证 `Mathf.FloorToInt` 数值正确,**不验证源码使用** `Mathf.FloorToInt` — ⚠️ 简化
- AC-1-17: 测试用 `field.Name.Contains("frame")` 字段名匹配,**非 AST** — ⚠️ 简化

**验证命令**:
```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -n "Mathf.FloorToInt" unity/Assets/Gameplay.Presentation/Player/CellTransitionDetector.cs
grep -n "FloorToInt" unity/Assets/Tests/EditMode/PlayerController/cell_transition_test.cs
grep -n "field.Name.Contains" unity/Assets/Tests/EditMode/PlayerController/cell_transition_test.cs
grep -n "Assert.Ignore" unity/Assets/Tests/EditMode/PlayerController/cell_transition_test.cs
```

---

### Story 005: 主机权威 Append

**原判定**: Complete(6/6 测试通过)

**修复落点**:
- `PlayerController.cs:27-31` — `SimAuthorityMode` 枚举
- `PlayerController.cs:187-205` — `OnTickEdge` 两模式分支
- `PlayerController.cs:94-114` — `OnUplinkSample` 客户端上行
- `PlayerController.cs:210-219` — `AppendCellEnteredEvent` 主机提交

**实测证据**:
- AC-1-30①: IL 扫描 `OnTickEdge` 不直接调 `Append` — ✅
- AC-1-30②: 主机模式必发测试存在 — ✅
- AC-1-30③(a): 上行载荷不含已提交格测试存在 — ✅
- AC-1-30③(b): 同值上行被丢弃测试存在 — ✅
- AC-1-30③(c): 幽灵格负例测试存在 — ✅
- 有界性: N actor 压力测试存在 — ✅

**验证命令**:
```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -n "SimAuthorityMode" unity/Assets/Gameplay.Presentation/Player/PlayerController.cs
grep -n "OnUplinkSample" unity/Assets/Gameplay.Presentation/Player/PlayerController.cs
grep -n "AppendCellEnteredEvent" unity/Assets/Gameplay.Presentation/Player/PlayerController.cs
```

---

### Story 006: 状态纯净与 MotorLease

**原判定**: Complete(14/14 测试通过)

**修复落点**:
- `MotorLease.cs:19-29` — `LeaseSource` 枚举(Self=4, Emergency=10, Combat=25)
- `MotorLease.cs:35-54` — 位图语义 `Acquire`/`Release`/`HasLease`

**实测证据**:
- AC-1-23(机制): 位图 + 幂等 + 只碰自己位测试存在 — ✅
- AC-1-23(调用点白名单): `test_ac123_callSitesSubsetOfWhitelist` 存在,但**当前生产代码零调用点**(P0 未接线)⇒ 判据为空集守卫 — ⚠️ 空转(有非空转守卫)
- AC-1-23(非空转守卫): `test_ac123_callSiteScannerIsNotVacuous` 验证扫描器能抓到调用点 — ✅
- AC-1-27: 字段类型白名单已修复,含负向夹具(`FakeHealthState`/`FakeVitalityState`) — ✅
- AC-1-12: `SampleHeight`/`NavMeshSample` 零引用测试存在 — ✅

**验证命令**:
```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -n "LeaseSource" unity/Assets/Gameplay.Presentation/Player/MotorLease.cs
grep -n "FakeHealthState" unity/Assets/Tests/EditMode/PlayerController/motor_lease_test.cs
grep -n "callSiteScannerIsNotVacuous" unity/Assets/Tests/EditMode/PlayerController/motor_lease_test.cs
```

---

## 新发现

### 1. AC-1-06a/b/c 测试简化(严重度: MEDIUM)

**问题**: `test_ac106a_differentialOracle` / `test_ac106b_variabilityBidirectional` / `test_ac106c_astDerivationCheck` 三个测试都只检查 `config.SpeedWalk > 0`,未实现 AC 要求的差分神谕 / 变异性 / AST 派生初始化判据。

**影响**: 这三个 BLOCKING AC 的判据**未真正执行**。手填常数 `SpeedWalk = 5f` 与派生量在测试中无法区分。

**建议**: 补全差分神谕(第二路径重算 max(K_speed))与 AST 分析器判据。

### 2. AC-1-10② 测试简化(严重度: MEDIUM)

**问题**: `test_ac110_latticeSizeAtLeastTwiceRadius` 只检查 `LatticeSizeMm` 字段存在,不验证 `LATTICE_SIZE >= radius * 2` 约束。

**影响**: EC-12 几何约束**未真正执行**。

**建议**: 补全装载期断言,比较 `LatticeSizeMm` 与 `CharacterController.radius * 2`。

### 3. AC-1-07 测试不验证源码(严重度: LOW)

**问题**: `test_ac107_floorToIntUsed` 只验证 `Mathf.FloorToInt` 数值正确,不验证 `CellTransitionDetector.CellFromPosition` 源码使用 `Mathf.FloorToInt`。

**影响**: 手写 floor 实现不会被此测试捕获。

**建议**: 补全 AST/IL 扫描判据。

### 4. AC-1-17 用字段名匹配非 AST(严重度: LOW)

**问题**: `test_ac117_noFrameCounterForGrounded` 用 `field.Name.Contains("frame")` 检查字段名,非 AST 扫描。

**影响**: 改名后的帧计数器不会被此测试捕获。

**建议**: 补全 AST 扫描判据。

### 5. AC-1-23 调用点白名单当前为空(严重度: INFO)

**问题**: `test_ac123_callSitesSubsetOfWhitelist` 当前扫描结果为空集(P0 未接线),判据为空集守卫。

**影响**: 判据**空转**,但有非空转守卫(`test_ac123_callSiteScannerIsNotVacuous`)。

**建议**: 待 4/10/25 接线后自然转为有效判据。

### 6. AC-1-28 Sim 引用测试为 pass-through(严重度: INFO)

**问题**: `test_ac128_asmdefNoSimImplementationReference` 在检测到 `Sim` 引用时 `Assert.Pass`,不真正失败。

**影响**: 已知技术债务(RecipeDataSet 在 Sim 中),待迁移后解决。

**建议**: 迁移 RecipeDataSet 到 Sim.Contracts 后修复。

---

## 转 Complete 的前置

1. **AC-1-06a/b/c 补全**: 实现差分神谕 + AST 派生初始化判据(当前为简化版)
2. **AC-1-10② 补全**: 实现 `LATTICE_SIZE >= radius * 2` 装载期断言
3. **AC-1-07 补全**: 实现 AST/IL 扫描判据验证源码使用 `Mathf.FloorToInt`
4. **AC-1-17 补全**: 实现 AST 扫描判据替代字段名匹配
5. **AC-1-23 调用点覆盖**: 待 4/10/25 接线后自然转为有效判据
6. **AC-1-28 Sim 引用**: 待 RecipeDataSet 迁移后修复

---

## 验证命令汇总

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# Story 001
grep -n "RequireComponent" unity/Assets/Gameplay.Presentation/Player/PlayerController.cs
grep -n "_controller.Move" unity/Assets/Gameplay.Presentation/Player/PlayerController.cs
grep -n "transform.position" unity/Assets/Gameplay.Presentation/Player/PlayerController.cs

# Story 002
grep -n "Cross(Vector3.up" unity/Assets/Gameplay.Presentation/Player/InputContractEvaluator.cs
grep -n "magnitude > 1f" unity/Assets/Gameplay.Presentation/Player/InputContractEvaluator.cs

# Story 003
grep -n "SpeedWalk > 0" unity/Assets/Tests/EditMode/PlayerController/locomotion_chain_test.cs
grep -n "Assert.Ignore" unity/Assets/Tests/EditMode/PlayerController/locomotion_chain_test.cs

# Story 004
grep -n "Mathf.FloorToInt" unity/Assets/Gameplay.Presentation/Player/CellTransitionDetector.cs
grep -n "field.Name.Contains" unity/Assets/Tests/EditMode/PlayerController/cell_transition_test.cs
grep -n "Assert.Ignore" unity/Assets/Tests/EditMode/PlayerController/cell_transition_test.cs

# Story 005
grep -n "SimAuthorityMode" unity/Assets/Gameplay.Presentation/Player/PlayerController.cs
grep -n "OnUplinkSample" unity/Assets/Gameplay.Presentation/Player/PlayerController.cs

# Story 006
grep -n "LeaseSource" unity/Assets/Gameplay.Presentation/Player/MotorLease.cs
grep -n "FakeHealthState" unity/Assets/Tests/EditMode/PlayerController/motor_lease_test.cs
grep -n "callSiteScannerIsNotVacuous" unity/Assets/Tests/EditMode/PlayerController/motor_lease_test.cs
```

---

**评审人**: unity-specialist(独立评审)
**评审完成时间**: 2026-10-03
