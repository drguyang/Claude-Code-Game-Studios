# Story 011: 跨平台确定性黄金夹具

> **Epic**: 物品与配方数据库
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/gdd/item-database.md`
**Requirement**: TR-itemdb-022(AC-21a-29 跨平台确定性;ADR-012 双级黄金夹具 = 执行载体,F7 spike 前置)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-012(主): 跨平台确定性 CI 门 · ADR-005(次): 确定性模拟与状态同步模型
**ADR Decision Summary**: ADR-012 规定**双级黄金夹具**(单元级哈希:`Fix` 四则 / 负值右移 / 定点 `Exp` / `SplitMix64` / `CDF walk` / 编码器往返;集成级字节:ADR-010 存档字节流)+ **三格全免费常驻矩阵**(Linux-x64-Mono + Linux-x64-IL2CPP + Linux-ARM64-IL2CPP;qemu 否决)+ **版本化刷新**(`golden-vN` + 变更日志 + 全体平台同时重签 + 旧版保留回归对比,禁单平台独签)。**BLOCKING spike(F7)**:C# `int64` 溢出为定义性回绕,IL2CPP 生成的 C++ 有符号溢出为 UB —— `SplitMix64` 与 Q16.16 中间乘正踩此线。ADR-005 规定全部模拟数学在整数定点域,存储中不出现任何 float。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH
**Engine Notes**: ADR-012 Engine Knowledge Risk **HIGH** —— 矩阵实现须 spike,判据本身为纯规格;IL2CPP 逐位性与 F7 溢出风险线是本故事的**实测前置**,未经实测不得签署 Determinism 结论。

**Control Manifest Rules (this layer)**:
- Required: 双级黄金夹具(单元级哈希 + 集成级字节);三格常驻矩阵 + 发版前两格;金标准 `golden-vN` 版本化,全体平台同时重签、旧版保留回归对比;金标准不得由首次运行自动生成(独立参考实现或手算产出 + 评审签字)
- Forbidden: 用 Mono 单侧结果冒充跨平台已验证(AC-29 禁借绿);qemu softfloat 对拍;单平台独签;`Math.Round` / `(float)` / `Math.Exp`(AC-30 静态扫描面)
- Guardrail: AC-29 当前 **BLOCKED-BY-实测**(IL2CPP player + ADR-012 F7 spike 未跑),不得记绿;`UNITY_LICENSE` 未配时 CI 红是预期态非测试缺陷

---

## Acceptance Criteria

*From GDD `design/gdd/item-database.md`, scoped to this story:*

- [ ] **AC-21a-28**: 参数表逐条枚举边界组合(所有 cap 取 0/取满、ENV_MOD 取 MIN/MAX、quality 取 1/MAX_QUALITY、EFF 取 EFF_MIN/EFF_MAX、outputs 取 m=1/m>1),F1/F2/F5 求解,每组合 SplitMix64 哈希与 `tests/unit/item_database/golden/` 下已提交且经人工核对的金标准逐位相同。⚠️ 金标准不得由首次运行自动生成(自指 ⇒ 恒过);必须由独立参考实现或手算产出、经评审签字后提交
- [ ] **AC-21a-29** [I]: 同一组参数,Editor(Mono)与 IL2CPP 独立玩家构建中求解,哈希逐位相同。⚠️ **当前 UNVERIFIED —— BLOCKED-BY-实测**:IL2CPP player 尚未构建(ADR-012 F7 spike 未跑:SplitMix64 与 Q16.16 中间乘踩 C# 有符号溢出定义性回绕 vs IL2CPP C++ UB 风险线);ADR-005 自述 IL2CPP 逐位性「需实测」。**在实测通过前,21a 不得签署 Determinism 结论;本 spec 一律记 BLOCKED-BY-实测,不得以 Mono 单侧结果借绿**
- [ ] **AC-21a-30**: F1…F5 全部中间变量静态扫描 ⇒ 除 facade 的 `ToFloat()` 外无任何 float/double:无 `Math.Round`、无 `(float)` 转型、无 `Math.Exp`(需手写定点版)—— ADR-005「Storage 中不出现任何 float」的验证

---

## Implementation Notes

*Derived from ADR-012 (primary) / ADR-005 (secondary):*

- **双级黄金夹具**:单元级哈希(`Fix` 四则 / 负值右移 / 定点 `Exp` / `SplitMix64` / `CDF walk` / 编码器往返)+ 集成级字节(ADR-010 存档字节流);字节错位可下钻到单元级算子 —— ADR-012 §裁决 ①
- **三格全免费常驻矩阵**:Linux-x64-Mono + Linux-x64-IL2CPP + Linux-ARM64-IL2CPP;Windows-x64-IL2CPP 与 Apple Silicon 列**发版前必跑**;**qemu 否决**(softfloat 不可信为逐位判据)—— ADR-012 §裁决 ②
- **版本化刷新**:`golden-vN` + 变更日志 + 全体平台同时重签 + 旧版保留回归对比;禁单平台独签 —— ADR-012 §裁决 ③
- **工具链修正**:`unity-test-runner@v4` 只在 Editor(Mono)跑 UTF —— IL2CPP 对拍须 `unity-builder@v4` 出 player + 独立 job(ADR-012 工具链修正;AC-29 的 Edge cases 已注明 unity-test-runner 不覆盖 IL2CPP)
- **BLOCKING spike(F7)**:C# `int64` 溢出为定义性回绕,IL2CPP 生成的 C++ 有符号溢出为 UB —— `SplitMix64` 与 Q16.16 中间乘正踩此线;AC-29 的 Edge cases 要求 F7 溢出用例须包含中间乘峰值参数 —— ADR-012 §BLOCKING spike(F7)
- **AC-30 静态扫描**:白名单 = facade 的 `ToFloat()` 调用点,且该白名单本身按 ADR-025 QQ-03 甲案做**构建期调用点断言**(`Sim` 内调用 `ToFloat` = 构建失败)—— ADR-025 §② QQ-03 甲案
- ADR-005 §Storage 禁 float 是本 AC 的上位判据;`Math.Exp` / `Math.Pow` / `Math.Sqrt` 同拒(ADR-026 `FixPow` / `FixSqrt` 纪律同族,libm 依赖即逐位性风险)
- 执法体 = 构建期断言(AC-30 静态扫描红即构建失败)+ CI 矩阵 job(AC-28/29)

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: `FixParse` / `ROUND_HALF_AWAY_FROM_ZERO` 单元测试(本故事只消费其输出做哈希)
- Story 003–007: 各业务逻辑的单元测试(本故事是横切的确定性验证层)
- Story 010: 存档字节流的业务正确性(本故事只在 AC-28 集成级引用其作为哈希输入)
- Story 012: 呈现层合规走查

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-21a-28**: 参数表逐条枚举边界组合(所有 cap 取 0/取满、ENV_MOD 取 MIN/MAX、quality 取 1/MAX_QUALITY、EFF 取 EFF_MIN/EFF_MAX、outputs 取 m=1/m>1),F1/F2/F5 求解,每组合 SplitMix64 哈希与 `tests/unit/item_database/golden/` 下已提交且经人工核对的金标准逐位相同。⚠️ 金标准不得由首次运行自动生成(自指 ⇒ 恒过);必须由独立参考实现或手算产出、经评审签字后提交。
  - Given: 边界组合笛卡尔积参数表(全枚举,确定性、无随机种子、无墙钟);`golden/` 下已提交金标准(`golden-vN` 版本化)。
  - When: 逐组合跑 F1/F2/F5,取输出 SplitMix64 哈希。
  - Then: 每组合哈希与金标准逐位相等;金标准文件带「独立参考实现/手算产出 + 评审签字」元数据,且提交历史证明非首跑自动冻结(评审检查项)。
  - Edge cases: cap 恰 0(全项退化);ENV_MOD_MIN 与 MAX 同时叠加其余满 cap(clamp 触发两极);EFF 两极;m=1 与 m>1;金标准刷新须全体平台同时重签、禁单平台独签、旧版保留回归对比(ADR-012,版本升 `golden-vN+1`);字节错位可下钻到单元级算子(双级夹具)。
  - Negative fixture: 无(夹具即金标准;篡改任一位应红——可作冒烟反证步骤)。
  - Suggested test path: `tests/unit/item_database/determinism_golden_fixtures_test.cs` + 金标准目录 `tests/unit/item_database/golden/`

- **AC-21a-29** [I]: 同一组参数,Editor(Mono)与 IL2CPP 独立玩家构建中求解,哈希逐位相同。⚠️ **当前 UNVERIFIED —— BLOCKED-BY-实测**:IL2CPP player 尚未构建(ADR-012 F7 spike 未跑:SplitMix64 与 Q16.16 中间乘踩 C# 有符号溢出定义性回绕 vs IL2CPP C++ UB 风险线);ADR-005 自述 IL2CPP 逐位性「需实测」。**在实测通过前,21a 不得签署 Determinism 结论;本 spec 一律记 BLOCKED-BY-实测,不得以 Mono 单侧结果借绿。**
  - Given: 同一组边界参数表(同 AC-28);Linux-x64-Mono(常驻 UTF)+ Linux-x64-IL2CPP / Linux-ARM64-IL2CPP(`unity-builder@v4` 出 player,独立 job)—— 注意 `unity-test-runner` 只在 Editor Mono 跑,IL2CPP 对拍须 player 路径。
  - When: 两侧分别求解并出哈希;发版前加 Windows-x64-IL2CPP、Apple Silicon 两格。
  - Then: 哈希逐位相同 ⇒ 才可转 VERIFIED;当前状态 = NOT-RUN/BLOCKED-BY-实测。
  - Edge cases: qemu 合成跑对拍(禁——ADR-012 明文 softfloat 不可信);`UNITY_LICENSE` 未配时 CI 三格红是预期态非测试缺陷;F7 溢出用例须包含中间乘峰值参数;单平台独签金标准(违规)。
  - Negative fixture: 无。
  - **状态:UNVERIFIED / BLOCKED-BY-实测(IL2CPP player 构建 + F7 spike)。禁记绿。**
  - Suggested test path: `tests/integration/item_database/determinism_golden_fixtures_test.cs`(Mono 侧)+ CI 独立 IL2CPP job(ADR-012 矩阵);证据落 `production/qa/evidence/` 待实测

- **AC-21a-30**: F1…F5 全部中间变量静态扫描 ⇒ 除 facade 的 `ToFloat()` 外无任何 float/double:无 `Math.Round`、无 `(float)` 转型、无 `Math.Exp`(需手写定点版)—— ADR-005「Storage 中不出现任何 float」的验证。
  - Given: src 中 F1–F5 实现及全部中间变量(SkillMod/QualityMod/EquipMod/EnvMod/QtyMultiplier/Retain/EFF/Axis_effective 等)。
  - When: 语法级静态扫描(编译单元,排除注释/字符串)。
  - Then: 无 float/double 类型声明、无 float 字面量、无 `Math.Round`、无 `(float)`/`(double)` 显式转型、无 `Math.Exp`/`Math.Pow`/`Math.Sqrt`(libm 依赖,ADR-026 FixPow/FixSqrt 纪律同族);白名单 = facade 的 `ToFloat()` 调用点,且该白名单本身按 ADR-025 QQ-03 甲案做构建期调用点断言(`Sim` 内调用 `ToFloat` = 构建失败)。
  - Edge cases: 中间量声明为 `var` 但推断为 double(须类型推断级扫描,非文本 grep);`decimal` 混入(同拒——非 Fix);测试代码与 Editor 工具豁免(扫描范围 = 门 A 侧运行期程序集);`1.0` 字面量出现在比较(拒)。
  - Negative fixture: 无(代码构造:注入一个含 `(float)` 的临时中间文件应红)。
  - Suggested test path: `tests/unit/item_database/determinism_golden_fixtures_test.cs`(静态扫描子用例)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/item_database/determinism_golden_fixtures_test.cs` — must exist and pass
- Integration (AC-29): `tests/integration/item_database/determinism_golden_fixtures_test.cs` + CI IL2CPP job — **当前 BLOCKED-BY-实测,禁记绿**;证据待 F7 spike 后落 `production/qa/evidence/`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001, 003, 004, 005, 010(F1/F2/F5 求解器 + Fix 编码器全部就位,才有东西可哈希)
- Unlocks: None(确定性验证是横切收口层,不单独解锁其他故事)
