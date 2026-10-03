# 评审报告(第二轮): player-controller(系统 1 玩家控制器与移动)

> **评审对象**: `production/epics/player-controller/` 全部 6 个 story
> **评审日期**: 2026-10-03
> **本轮为第二轮;评的是修复后的代码**(第一轮 = `review-player-controller-2026-10-03.md`)
> **评审方式**: 独立源码复核(逐条核对修复落点与判据形态,不采信 commit message 自述)
> **声明**: 本报告基于**静态源码复核**,**未跑 Unity 测试套件**。凡未亲自验证者标 `未验证`。

---

## 结论摘要表

| 项 | 原判定 | 本轮判决 | 依据 |
|---|---|---|---|
| **A3** `AC-1-06a` | A 类(空转) | ✅ **真换判据** | 接口形态断言 + 6 侧差分神谕(独立第二路径) |
| **A3** `AC-1-06b` | A 类(空转) | ✅ **真换判据** | 变异性(注入抬上界⇒红)+ 双向(删行⇒绿) |
| **A3** `AC-1-06c` | A 类(空转) | ✅ **真降级 NOT-RUN** | `Assert.Ignore`(方法名 `_notRun`),撤 `[x]` |
| **A4** `AC-1-10②` | A 类(空转) | ✅ **真约束断言 + 负例** | 直径 mm ≥ 格边长 mm 断言 + 可证伪夹具 |
| **A5** `AC-1-17` | A 类(空转) | ✅ **类型面 + 源码面双判据** | 整型字段白名单 + 源码去注释扫帧计数语义 |
| **A7** `AC-1-28` | A 类(pass-through) | ✅ **真断言 + 具名豁免仅 `Sim`** | 失败消息点名豁免与出口条件 |
| **AC-1-23** 调用点白名单空集 | INFO(空转守卫) | ⚠️ **仍为空集;但白名单断言**结构性恒真**(新发现) | 见新发现 #1 |
| **AC-1-04** NOT-RUN | 已核实 | ✅ **仍 `Assert.Ignore`** | `cell_transition_test.cs:413-417` |
| Story 001 | 部分交付 | ⚠️ 代码已修,**story 件文本未同步** | 见新发现 #2 |
| Story 002 | 已交付 | ⚠️ **Test Evidence 行仍 `[ ] Pending`**(状态漂移) | 见新发现 #3 |
| Story 003 | 部分交付 | ⚠️ 06a/b/c 已修;**Deviations 文本陈旧**;AC-1-18 跨格仍未真测 | 见新发现 #4 |
| Story 004 | 部分交付 | ⚠️ AC-1-17 已修;**AC-1-17 的 `[x]` 未勾**(与「14 条全落地」自述矛盾) | 见新发现 #5 |
| Story 005 | 已交付 | ⚠️ Test Evidence 行仍 `[ ] Pending`(状态漂移) | 见新发现 #3 |
| Story 006 | 部分交付 | ⚠️ AC-1-23 判据含结构性恒真分支 | 见新发现 #1 |

---

## 逐条详节

### A3 修复:`locomotion_chain_test.cs` 三条测试判据

**原判定**: 三测**都只查 `config.SpeedWalk > 0`** ⇒ 手填常数与派生量无法区分。

**修复落点**: `unity/Assets/Tests/EditMode/PlayerController/locomotion_chain_test.cs`

**实测证据**:

- **06a** `test_ac106a_differentialOracle`(`:298-340`)— ✅ **真换了判据**。两段:
  1. **接口形态**(`:310-318`):遍历 `LocomotionEvaluator` 的全部字段/属性,断言名称不含 `KTerrainMax`/`KContextMax`/`K_TERRAIN`/`K_CONTEXT` ⇒ 验「1 不自持派生量」(F-1-1a 所有者反转)。
  2. **差分神谕**(`:320-339`):构造 6 侧 `WorldLatticeParams`(表 `{1,3,7}`),用**独立第二路径**(裸 `foreach` 求 max,不复用被测 helper)算出 `oracle=7`,断言 `p.KTerrainMax == oracle`,并验 `SpeedMax == 5×7×2`。
- **06b** `test_ac106b_variabilityBidirectional`(`:344-380`)— ✅ **真换了判据**。基线表 `{1,2}` ⇒ `DoesNotThrow`;注入行 `7` ⇒ `Assert.Throws<ArgumentException>`(变异性);删注入行 ⇒ `DoesNotThrow`(双向,排除永久红)。
- **06c** `test_ac106c_astDerivationCheck_notRun`(`:384-400`)— ✅ **真为 `Assert.Ignore`**(方法名带 `_notRun`),消息点名「载体 = Roslyn,ADR-024 §⑤ 明令不引;判据面在 6 侧 AC-6-07」。story-003 `:49-54` 的 `[x]` 已撤为 `[ ]`。

**逻辑自洽性复核**(我按 `WorldLattice.cs:99-193` 手算):
- 06a 表 `{1,3,7}` ⇒ `KTerrainMax=7` ✓;`SpeedMax=5×7×2=70` ✓。
- 06b 基线 `{1,2}` ⇒ `SpeedMax=20`,`displacement=20×100=2000` ≤ `10000/2=5000` ⇒ 不抛 ✓;
  注入 `{1,2,7}` ⇒ `SpeedMax=70`,`displacement=7000 > 5000` ⇒ 抛 `ArgumentException` ✓。
  ⇒ 两测**非空转、可证伪**。

**验证命令**:
```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -n "test_ac106a_differentialOracle\|test_ac106b_variabilityBidirectional\|test_ac106c_astDerivationCheck" \
  unity/Assets/Tests/EditMode/PlayerController/locomotion_chain_test.cs
grep -n "Assert.Ignore" unity/Assets/Tests/EditMode/PlayerController/locomotion_chain_test.cs
```

⚠️ **保留局限**:06a 的接口形态扫描**只覆盖 `LocomotionEvaluator`**,未覆盖 `LocomotionConfig` / `PlayerController`;若派生量落在别处不会被此测捕获。**未跑测试,断言实际通过性未验证**。

---

### A4 修复:`AC-1-10②` 直径装得进格

**原判定**: `test_ac110_latticeSizeAtLeastTwiceRadius` 只查 `LatticeSizeMm` 字段存在,不验约束。

**修复落点**: `unity/Assets/Tests/EditMode/PlayerController/controller_foundation_test.cs:215-253`

**实测证据**: ✅ **真断「直径装得进格」**。
- 取 `LocomotionConfig.LoadDefault().Radius`,先断言 `Radius > 0`(`:228`);
- 计算 `diameterMm = Radius × 2 × 1000`(`:233`);
- 断言 `CanonicalLatticeMm(1000) >= diameterMm`(`:235`),失败消息点名 EC-12 与公式。
- **负例** `test_ac110_negativeFixture_radiusTooLarge`(`:241-253`):`radius=0.6 ⇒ diameter=1200 > 1000`,先断言夹具前提成立,再断言约束不成立 ⇒ 证明判据能分辨。

⚠️ **保留局限**:用**硬编码** `CanonicalLatticeMm = 1000` 而非读真实 `WorldLatticeParams.LatticeSizeMm`(`:232` 注释自陈「规范值」)⇒ 验的是「1 的 radius 装得进 1 m 格」,不是「装得进**当前配置**的格」。若未来 `LATTICE_SIZE` 改值,本测不会随之更新。**未跑测试。**

**验证命令**:
```bash
grep -n "CanonicalLatticeMm\|diameterMm" unity/Assets/Tests/EditMode/PlayerController/controller_foundation_test.cs
```

---

### A5 修复:`AC-1-17` 类型面 + 源码面

**原判定**: 用 `field.Name.Contains("frame")` 字段名匹配,非 AST。

**修复落点**: `unity/Assets/Tests/EditMode/PlayerController/cell_transition_test.cs:376-408`

**实测证据**: ✅ **真查双面**。
- **① 类型面**(`:384-395`):取 `CellTransitionDetector` 全部 `int`/`long` 私有实例字段,断言 ⊆ 白名单 `{_lastCellX, _lastCellZ, _lastEventTick}`,多出任何整型累计器即红(改名仍被捕获)。
- **② 源码面**(`:397-407`):读 `CellTransitionDetector.cs` 原文,**先去注释**(`Regex.Replace(@"//.*?$", "", Multiline)`),再断言不含 `groundedFrames`/`frameCount`/`Frames++`/`frames++`/`GroundedCount`。
- **交叉验证**:我实读 `CellTransitionDetector.cs:29-34`,其私有字段实为 `_eventSink`/`_tickProvider`/`_lastCommittedCell`/`_pendingCell`/`_hasPending` ⇒ **零 `int` 字段** ⇒ 类型面白名单断言当前**必然通过**(且非空转:白名单机制对改名/新增字段有效)。
  ⚠️ 注意:白名单里的 `_lastCellX`/`_lastCellZ`/`_lastEventTick` 在**当前代码中并不存在** ⇒ 白名单与实现不同步(不致命,但属文档漂移)。

**验证命令**:
```bash
grep -n "allowedIntFields\|groundedFrames" unity/Assets/Tests/EditMode/PlayerController/cell_transition_test.cs
grep -n "private" unity/Assets/Gameplay.Presentation/Player/CellTransitionDetector.cs
```

---

### A7 修复:`AC-1-28` 具名豁免仅 `Sim`

**原判定**: 检测到 `Sim` 引用时 `Assert.Pass` ⇒ pass-through。

**修复落点**: `unity/Assets/Tests/EditMode/PlayerController/controller_foundation_test.cs:113-158`

**实测证据**: ✅ **真断言 + 具名豁免仅 `Sim`**。
- 用 `Regex` 解析 asmdef 的 `"references":[...]` 数组(`:124-131`),非字符串 `Contains`(避免误命中注释/路径)。
- `forbidden = names.Where(x => x == "Sim" || x == "Sim.Codec")`(`:134`)。
- `waiver = new[] { "Sim" }`(`:140`)—— ✅ **只放行 `Sim`**;`Sim.Codec` **不在**豁免内。
- `unexpected = forbidden.Where(f => !waiver.Contains(f))` ⇒ `Assert.IsEmpty(unexpected, ...)`(`:143`)。
- **失败消息点名豁免与出口条件**(`:144-150`):明写「已登记豁免仅 [Sim](= RecipeDataSet 住 Sim 的既有债)」+「任何**新增**的 sim 实现引用…须改走 Sim.Contracts」。
- **交叉验证**:实读 `Gameplay.Presentation.asmdef` ⇒ `references = [Sim.Contracts, Sim, Unity.Addressables, Unity.ResourceManager, UnityEngine]` ⇒ `forbidden = {Sim}` ⇒ `unexpected = {}` ⇒ 当前**通过**。豁免注释 `:139` 写明出口条件 = `RecipeDataSet` 迁出后删豁免。
- ⚠️ 保留:`Sim` 引用**当前确实存在** ⇒ AC-1-28 **部分成立**(非完全成立),测试用 `TestContext.WriteLine`(`:152-157`)如实记账。

**验证命令**:
```bash
grep -n "waiver\|forbidden\|unexpected" unity/Assets/Tests/EditMode/PlayerController/controller_foundation_test.cs
cat unity/Assets/Gameplay.Presentation/Gameplay.Presentation.asmdef
```

---

### AC-1-23 调用点白名单空集

**原判定**: 调用点白名单当前为空集(空转守卫)。

**实测证据**: ⚠️ **仍为空集**(P0 未接线,生产代码零 `MotorLease.Acquire/Release` 调用点)。
- `test_ac123_callSitesSubsetOfWhitelist`(`motor_lease_test.cs:151-181`)遍历 `AllowedCallerAssemblies`(4/10/25),记录调用点;`TestContext.WriteLine` 自陈「当前 0 处,预期 P0 为 0」。
- `test_ac123_callSiteScannerIsNotVacuous`(`:183-195`)对**本测试程序集自身**扫描,断言非空 ⇒ 证明扫描器有效(非空转守卫)。✅ 守卫存在。
- ⚠️ **但白名单断言本身含结构性缺陷**(见新发现 #1)。

---

### AC-1-04 NOT-RUN

**实测证据**: ✅ **仍为 `Assert.Ignore`**(`cell_transition_test.cs:413-417`),消息「NOT-RUN: AC-1-04 VR 零事件待 VR 落地时回升 BLOCKING」。story-004 `:54` 亦登记 ADVISORY + 回升条件。**非静默通过。**

---

## 新发现

### 1. `test_ac123_callSitesSubsetOfWhitelist` 的白名单断言**结构性恒真**(严重度:LOW-MEDIUM)

**问题**: `motor_lease_test.cs:159-171` 的循环体:
```csharp
foreach (string asmName in AllowedCallerAssemblies)   // 只遍历白名单内程序集
{
    ...
    if (!AllowedCallerAssemblies.Contains(asmName))   // ← 恒为 false
        disallowed.Add(site.Caller);
}
Assert.IsEmpty(disallowed, ...);
```
循环**只迭代白名单内**的程序集,故 `disallowed` **不可能非空** ⇒ 白名单成员断言**无法失败**。它只能证明「白名单内程序集**若存在**则其调用被记录」,不能证明「非白名单程序集**不存在**调用」。

**影响**: AC-1-23「调用者 ⊆ {4,10,25}」的**排他半边未被真正执行**。非空转守卫(`callSiteScannerIsNotVacuous`)只证明**扫描器**有效,不修复此逻辑。真正的排他判据须**遍历全部已加载程序集**再过滤白名单外者。

**建议**: 改为 `foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())`,对每个程序集扫调用点,调用者 ∉ 白名单 ⇒ `disallowed`。这与「集合为空」的空集问题**正交**——空集是接线进度,恒真是判据形态缺陷。

### 2. Story 001 story 件文本未同步(严重度:LOW)

`story-001-controller-foundation.md`:
- `:169` Deviations 仍写「**AC-1-10② 测试是简化版(`Assert.Pass`)**」—— 与代码(`:216-253` 已为真断言)**矛盾**。
- `:42` AC-1-28 标 `[x]` 但**未登记豁免**(豁免是本轮新增的事实)。
- `:164-165` 的「测试: 6 条(5 通过 + 1 跳过)」「全量 EditMode: 1452/1480」为**修复前**数字。

### 3. Story 002 / 005 的 Test Evidence 行仍 `[ ] Pending`(严重度:LOW)

- `story-002:123`:`**Status**: [ ] Pending — story not yet implemented`,而头 `:4` 已标 `Complete ✅`。
- `story-005:142`:`[ ] Pending`,而头 `:4` 标 `Complete ✅`。
⇒ **同一 story 件内头尾状态自相矛盾**(与 2026-10-02 状态回填轮登记过的漂移同型,**未彻底闭合**)。

### 4. AC-1-18 跨格测试**仍未真测跨格**(严重度:LOW-MEDIUM,非本轮修复项)

`locomotion_chain_test.cs:273-294` 的 `test_ac118_cellCrossing_usesOldCellMultiplier` 注释称「第二帧: 跨到格 1」,但实际调用 `ComputeVTarget(1f, kTerrainSpeeds, 0, basis)` —— **仍传 `currentCellIndex=0`**(与第一帧相同)。第三帧才传 `1`。⇒ 所谓「跨格那一帧」并未模拟跨格,只是**同一索引调用两次**。story-003 `:193` 的 Deviations「AC-1-18: 完整版需要跨格夹具」**仍成立**。本轮任务未要求修此项,故保留。

### 5. Story 004 的 AC-1-17 复选框未勾(严重度:INFO)

`story-004:52` AC-1-17 仍为 `[ ]`(未勾),而测试已实现(`cell_transition_test.cs:376-408`)。同时 `:189` Completion Notes 称「14 条 AC 全部落地」。⇒ 复选框与自述不一致(偏保守方向,非借绿)。

---

## 转 Complete 的前置

1. **【必做】修复新发现 #1**:`test_ac123_callSitesSubsetOfWhitelist` 的排他半边改为遍历全部程序集 —— 否则 AC-1-23 的白名单断言**永不能失败**。
2. **【必做】同步 story 件文本**:story-001 Deviations(撤「AC-1-10② 简化」)、story-002/005 的 Test Evidence 行、story-003 Deviations(06a/b/c 已重定)、story-004 的 AC-1-17 复选框。
3. **【必做】重跑 EditMode 套件**:四份报告(含本件)均未实跑;任何转 Complete 前须以当前 HEAD 重跑并落数字。
4. **【可选】AC-1-18 补真跨格夹具**;A3-06a 的接口形态扫描扩至 `LocomotionConfig`/`PlayerController`。
5. **A4 硬化**:`CanonicalLatticeMm` 改读真实 `WorldLatticeParams.LatticeSizeMm`,避免硬编码漂移。

---

**评审人**: unity-specialist(独立评审)
**评审完成时间**: 2026-10-03
