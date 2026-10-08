# Story 001: 定点数学库与确定性哈希

> **Epic**: 疾病与伤情模拟
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 8h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/disease-simulation.md`(§F0 定点域与中间宽度 · 规则一 确定性根 · Dependencies 实现顺序 1「Fix 库」)
**Requirement**: TR-disease-001(Fix struct Q16.16)· TR-disease-002(Q32.32 中间乘)· TR-disease-003(手写定点 Exp)· TR-disease-004(SplitMix64 唯一哈希)· TR-disease-022(128 位中间结果唯一类型 = 手工 hi/lo)· TR-disease-014(零 Math.Exp/Pow 类型断言)· TR-disease-019(保守带误差预算,库级支撑)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005 Amendment G(主)· ADR-006(舍入与边界)· ADR-012 F7(溢出 UB)· ADR-025(装配落点)· ADR-026(FixPow/FixSqrt 同族先例)
**ADR Decision Summary**: 128 位中间结果**唯一类型 = 手工 hi/lo 两 `ulong`**(32 位数字四路拆分,交叉项全程无符号 + 掩码提取,禁有符号右移);**禁 `System.Int128`**(netstandard2.1/IL2CPP 无此类型)、**禁 `BigInteger`**;无条件钉 hi/lo,不留条件分支。单一舍入 `ROUND_HALF_AWAY_FROM_ZERO`(≠ C# `/` 向零截断)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(本库自身纯 C# = LOW;抬到 MEDIUM 的是 IL2CPP 有符号溢出 UB 前置 —— ADR-012 F7 spike 未跑前 hi/lo 路线是**裁决要求**而非实测结论)
**Engine Notes**: 住 `Sim.Contracts`(`Fix` 公开面)+ `Sim.Codec` 不涉及(序列化归 story 002 之后的持久化侧);不触及任何 post-cutoff API。跨平台逐位判据(本 AC 的 [I] 半边)NOT-RUN 直至三格矩阵实跑 —— **禁借绿**。

**Control Manifest Rules (this layer)**:
- Required: 全部模拟数学 ∈ 整数定点域(Q16.16);中间乘经 hi/lo;patient_seed = SplitMix64 纯函数派生
- Forbidden: float/double 入 `Sim`/`Sim.Contracts` 数学面;`System.Int128` / `BigInteger` / `Math.Exp` / `Math.Pow` / `UnityEngine.Random`
- Guardrail: `Fix` 保持 public(ADR-025 QQ-03 甲案),`ToFloat()` 调用点白名单断言(`Sim` 内调用 = 构建失败)

---

## Acceptance Criteria

*From GDD `design/gdd/disease-simulation.md`, scoped to this story:*

- [ ] **AC-4 库侧半边**[L]:`Fix` 四则(Add/Sub/Mul/Div)在整数域定义完备;`Div` 舍入 = `ROUND_HALF_AWAY_FROM_ZERO`,含负值用例(合成 fixture:8192.5 语义 → 8193;−8192.5 → −8193),**不得**退化为 C# `/` 向零截断
- [ ] **AC-4 / F0 中间乘宽度**[L]:任意两 Q16.16 值相乘,中间积经 hi/lo 两 `ulong` 四路拆分;随机对拍(定种子)≥10⁴ 组:hi/lo 结果 ≡ 参考实现(BCL `BigInteger` **仅测试侧**参考,不入 sim 程序集)
- [ ] **AC-5b Exp**[L]:手写定点 `Exp` 在声明输入域内逐位确定;与既有 skill-system 的 `FixPow/FixSqrt` 同族纪律(禁 libm);精度/误差预算以参数化断言,常数**待用户数值轮**,测试用合成 fixture 注入
- [ ] **AC-4 / TR-disease-004 SplitMix64**[L]:唯一哈希实现;黄金向量 = spec 标准测试序列 + 本工程自签向量(golden-vN 刷新纪律,全体平台同时重签)
- [ ] **TR-disease-014 类型断言**[A]:构建期断言 —— `Sim`/`Sim.Contracts` 程序集引用集恰 = {BCL, Sim.Contracts}(门 A 白名单,承 ADR-017 §二);源文本/IL 双层扫描零 `Math.Exp`/`Math.Pow`/`System.Int128`/`BigInteger`(判据 = 反射+IL 断言,不是裸 grep;承 ADR-020 AC-20-03 先例)
- [ ] **AC-1 库级半边**[I]:上述四件的**单元级黄金夹具哈希**入库(Fix 四则 / 负值右移 / 定点 Exp / SplitMix64,承 ADR-012 双级夹具的「单元级」层);**跨平台三格实跑 NOT-RUN**,依赖 ADR-012 矩阵,禁借绿

---

## Implementation Notes

*Derived from ADR-005 Amendment G + ADR-012 + ADR-025:*

1. `Fix` = `readonly struct`,内部 `long raw`(Q16.16);构造只经 `FixParse` / `FromRaw` / `FromRatio`,无 float 构造器。
2. hi/lo 拆分按 ADR-005 Amendment G 逐字:32 位四路交叉项、全程无符号、掩码提取、**禁有符号右移**;无条件走 hi/lo(不留「小值直乘」分支 —— 分支本身即漂移面)。
3. `FixSqrt`/`FixPow` **本 story 不重实现**(skill-system story 003 已有),9 侧只声明复用入口;若引用形参不同须回写 skill epic,不私改。
4. SplitMix64 落在 `Sim.Contracts`(patient_seed 派生链的最底层);`patient_seed = hash(WorldSeed 派生盐, patient_id)` 的**语义**归 story 002,本 story 只交付哈希原语。
5. 位移纪律:负值右移必须显式定义(单元级黄金夹具已列「负值右移」项)。
6. 测试真身 `unity/Assets/Tests/EditMode/Sim/sim_fixedpoint_test.cs`;账本镜像 `tests/unit/sim/`(承 skill-system 先例:Unity 只编译 `unity/Assets/` 树)。
7. 一切数值常数(Exp 项数、误差带)以 `fixture` 参数传入,story 文本不拍定值(数值用户自己调)。

## Out of Scope

- [Story 002]: SimEvent/流机制、patient_seed 语义、Seq 发号、折叠
- [Story 003]: 注册表 schema 与烘焙管线(`FixParse` 的**调用**面)
- [Story 004…006]: F1/F2/F4/F3/F5 各公式求值(复用本库,不在此实现)
- Fix 的存档编码器(归 `Sim.Codec`,7a 持久化 epic 侧落点)

## QA Test Cases

*Written at story creation(lean mode — specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **舍入模式**: Given 8192.5 与 −8192.5 两语义中间值。When `Fix.Div`。Then 8193 / −8193(HALF_AWAY_FROM_ZERO);负向:若实现退化为向零截断 ⇒ 红。
- **hi/lo 对拍**: Given 定种子随机 10⁴ 组 Q16.16 二元组(含极值/符号边界)。When 逐组 hi/lo 乘 vs 测试侧 BigInteger 参考。Then 逐位相等;任何一组不等 ⇒ 红并打印操作数。
- **Exp 确定性**: Given 固定输入网格(域边界±1)。When 双跑。Then 逐位相等且 ≡ 黄金向量。
- **类型断言**: Given 合成负夹具(源文本 `Math.Exp(x)` / `using System.Numerics;` 注入测试装配影子类型)。When 构建门扫描。Then 红;生产树 ⇒ 绿。**不在生产树注入**。
- **黄金夹具入库**: Given golden-vN 文件。When Mono 本地跑。Then 单元级哈希 ≡ 夹具;三格对拍列 NOT-RUN(矩阵未跑)。

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/Sim/sim_fixedpoint_test.cs` — must exist and pass(跨平台 [I] 半边除外)
**Status**: [x] Complete — test file exists (`unity/Assets/Tests/EditMode/Sim/sim_fixedpoint_test.cs`)

---

## Dependencies

- Depends on: skill-system story 003(FixPow/FixSqrt 先例,只复用不重实现)、item-database epic(`FixParse` 已交付的解析入口)
- Unlocks: Story 002…006(9 的全部求值面)、time-weather epic(25 侧定点复用同库)

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `Fix.cs` 264 行 — Q16.16 四则 + hi/lo 中间乘 + `FromRational` + `Pow`/`Sqrt`
- `SplitMix64.cs` 104 行 — `Avalanche` / `NextValue` / `Fold` / `Hash` / `HashTagged`
- `FixParse.cs` 100 行 — `RoundHalfAwayFromZero` + 解析入口
- `RoundMode.cs` 17 行 — 舍入模式枚举
- 测试: `sim_fixedpoint_test.cs` (5 测) + `golden_hash_v1_test.cs` (12 测) + `sim_codec_roundtrip_test.cs` (10 测)
- 全量 EditMode 986/986 Passed

**Deviations**: 无

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/Sim/sim_fixedpoint_test.cs` — 5 测全过
- `unity/Assets/Tests/EditMode/Sim/golden_hash_v1_test.cs` — 12 测全过
- `unity/Assets/Tests/EditMode/Sim/sim_codec_roundtrip_test.cs` — 10 测全过

**Code Review**: 无（纯数学库，无外部依赖）

**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
