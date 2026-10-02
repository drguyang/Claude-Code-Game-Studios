# 逐 BLOCKING 对账件 —— player-controller(系统 1)

> **用途**:补齐 player-controller EPIC §🔴 逐 story 证据缺口 第 2 条 ——
> 全 6 story 的「双代理评审 REQUEST_CHANGES → 修复」此前只有 commit message 自述。
> **编制**:2026-10-02 · **编制方式 = 逐条独立复核**(非转录 commit message)
> **HEAD**:`1a884c9` · **引擎**:Unity 6000.3.24f1

## 结论摘要

| Story | 原判定来源 | 修复提交 | 实测判决 |
|---|---|---|---|
| 001 | `696c36d` 自述 4 条 | `696c36d` | ✅ 已修 |
| 002 | `88518d6` 自述 B1–B4 | `88518d6` | ✅ 已修 |
| 003 | `c8f3b6c` 自述 5 条 | `c8f3b6c` | ✅ 已修 |
| 004 | `0db7830` 自述 6 条 | `0db7830` | ✅ 已修 |
| 005 | `e6be7ee` 自述 5 条 | `e6be7ee` | 🔴 **曾被退化替换,`5572d66` 已恢复** |
| 006 | `1c30ecf` 记账 2 条 | `1a884c9` | ✅ 已修(本轮) |

**5 story 的修复确认成立;005 需附「判据曾被换成空转测试」的留痕。**

⚠️ **原判定文本不可得** —— 双代理评审报告**从未落盘**,现存最早记录为各修复提交
message 中的自述清单(`696c36d` / `88518d6` / `c8f3b6c` / `0db7830` / `e6be7ee`)。
本件的「原判定」列**据自述清单**编制,并以 HEAD 实测**独立复核**其修复是否成立
—— 即本件验证的是「自述的修复是否真在代码里」,不验证「原判定是否完备」。
**这一局限如实标注,不得读成评审本身已被复核。**

---

## Story 001(`696c36d`)

| # | 自述修复项 | 实测 | 判决 |
|---|---|---|---|
| 1 | AC-1-01① 加 `[RequireComponent]` 断言 | `controller_foundation_test.cs` 含 `typeof(RequireComponent)` 断言 | ✅ |
| 2 | AC-1-01②③ 方法名扫描 → IL 体扫描器 | `ILBodyScanner.cs` 在库;测试经 `ContainsMethodCall` 调用 | ✅ |
| 3 | AC-1-28 Sim 引用检查 | `test_ac128_asmdefNoSimImplementationReference` 存在 | ✅ |
| 4 | AC-1-10②③ 字段类型 + 递归扫描 | 断言存在 | ✅ |

**测试**:`ControllerFoundationTest` **10/10 Passed**。
⚠️ 第 3 条现形态为 `Assert.Pass(...)` **双分支皆通过**(引用与不引用 Sim 都绿),
仅记录「已知技术债务」而不构成判据 —— **该条实为登记而非断言**,如实记账。

## Story 002(`88518d6`)

| # | 自述修复项 | 实测 | 判决 |
|---|---|---|---|
| B1 | `r̂` 从 `f̂` 派生(`Vector3.Cross` + 容差断言) | `input_contract_test.cs` 含正交性断言 | ✅ |
| B2 | 反向用例(带仰角相机 ⇒ `v̂_world.y == 0`) | 测试存在 | ✅ |
| B3 | 补 AC-1-35(求值次序/每帧取样/只读/正交) | `CountingCameraRig` 辅助类在库 | ✅ |
| B4 | 全 yaw 扫描(36 点 × 3 幅值) | 测试存在 | ✅ |

**测试**:`InputContractTest` **16/16 Passed**。

## Story 003(`c8f3b6c`)

| # | 自述修复项 | 实测 | 判决 |
|---|---|---|---|
| 1 | 接入生产路径:`PlayerController.Move()` 消费乘数链 | `PlayerController.cs:159-166` 调 `LocomotionConfig` + `Mathf.MoveTowards` | ✅ |
| 2 | AC-1-06a/b/c(差分神谕/变异性/AST) | `locomotion_chain_test.cs` 在库 | ✅ |
| 3 | AC-1-11 负向夹具(AIR_CONTROL>1 等) | 测试存在 | ✅ |
| 4 | AC-1-18 调用序探针 + 跨格夹具 | 测试存在 | ✅ |
| 5 | AC-1-20a④ 反向用例(相机 yaw 变不改变玩家 yaw) | 测试存在 | ✅ |

**测试**:`LocomotionChainTest` **22 Passed + 1 Skipped**(AC-1-21 BLOCKED-BY `OQ-1-12`)。

## Story 004(`0db7830`)

| # | 自述修复项 | 实测 | 判决 |
|---|---|---|---|
| 1 | Cell identity `int` → `WorldPos`(三维 floor) | `CellTransitionDetector.CellFromPosition` 返 `WorldPos` | ✅ |
| 2 | 移除 `IsIntegerType` 无条件白名单 → 递归 struct 字段检查 | `cell_transition_test.cs` 含递归扫描 | ✅ |
| 3 | AC-113 对角跨格 Z 值断言 | 测试存在 | ✅ |
| 4 | AC-1-07 数值验证(替代无操作测试) | 测试存在 | ✅ |
| 5 | 新增 `stream_bound_test.cs`(AC-1-03② 集成上界) | 文件在库,2 例 | ✅ |
| 6 | AC-1-32 折返/跨 tick 双向用例 | 测试存在 | ✅ |

**测试**:`CellTransitionTest` **18 Passed + 1 Skipped**(AC-1-04 NOT-RUN,需 45 夹具)
· `StreamBoundTest` **2/2**。

## 🔴 Story 005(`e6be7ee`)—— 判据曾被退化替换

| # | 自述修复项 | 实测 | 判决 |
|---|---|---|---|
| 1 | `OnPositionSample` 直接更新 pending | `CellTransitionDetector.cs:45-66` 实现 | ✅ |
| 2 | 折返检测(新样本 == last_committed ⇒ 清 pending) | `:57-62` 实现 | ✅ |
| 3 | 同值上行丢弃 | `test_ac130_sameValueUplink_discarded` | ✅ |
| 4 | 幽灵格负例 | `test_ac130_ghostCell_noPhantomInStream` | ✅ |
| 5 | 有界性 4 actor × 100 tick | `test_boundedness_nActors` | ✅ |

**测试**:`HostAuthorityTest` **6/6 Passed**。

🔴 **但须附留痕**:`185063f` 曾把本 story 的 **6 例换成 4 例** —— 删去
①②③(a)(b)(c) 全部真判据,替换为 `test_clientPrediction_rollbackOnMismatch`,
而该测试**不调用任何被测代码**,只是内联重演 `if (x != y) x = y` ⇒ 对 AC-1-30 是**空转判据**。
`5572d66` 已恢复 `45056e6` 的 6 例版本(用户裁定方案 A)。
⇒ **本 story 的「6/6 通过」只对 `5572d66` 之后的版本成立。**

## Story 006(`1a884c9`)

| # | 原判定 | 实测 | 判决 |
|---|---|---|---|
| 1 | AC-1-27 实现为**字段名黑名单**(AC 明禁;恒真) | 改为类型白名单 + 负向夹具 | ✅ |
| 2 | AC-1-23 调用点白名单**零测试** | 补 IL 调用点扫描 + UI 零符号引用 | ✅ |

**测试**:`MotorLeaseTest` **8 → 14 例,14/14 Passed**。
详见 `production/epics/player-controller/story-006-state-purity-motor-lease.md` 的 `Deviations`。

---

## 验证命令(可复现)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
ls unity/Assets/Tests/EditMode/PlayerController/
grep -n "RequireComponent\|ContainsMethodCall" unity/Assets/Tests/EditMode/PlayerController/controller_foundation_test.cs
grep -n "Cross\|orthogonal\|正交" unity/Assets/Tests/EditMode/PlayerController/input_contract_test.cs
grep -n "LocomotionConfig\|MoveTowards" unity/Assets/Gameplay.Presentation/Player/PlayerController.cs
grep -n "CellFromPosition\|WorldPos" unity/Assets/Gameplay.Presentation/Player/CellTransitionDetector.cs
grep -n "test_ac130_\|test_boundedness" unity/Assets/Tests/EditMode/PlayerController/host_authority_test.cs
grep -n "IsAllowedFieldType\|FindLeaseCallSites" unity/Assets/Tests/EditMode/PlayerController/motor_lease_test.cs
```

**测试证据**:2026-10-02 batchmode 全量 EditMode
`total 2028 · passed 1995 · failed 0 · skipped 32 · inconclusive 1`;
PlayerController 7 fixture 全绿 **92 例**(CellTransition 18+1skip · ControllerFoundation 10 ·
HostAuthority 6 · InputContract 16 · LocomotionChain 22+1skip · MotorLease 14 · StreamBound 2)。

## 残留(不划结)

1. **原判定文本不可得** —— 评审报告从未落盘;本件复核的是「自述修复是否真在代码里」。
   后续双代理评审**须落报告原件**至本目录,否则同类缺口会再生。
2. **Story 001 AC-1-28 实为登记而非断言**(双分支皆 `Assert.Pass`)。
3. **`O-4`(45 侧登记行)仍悬空** —— 45 无 GDD(P1b)。
