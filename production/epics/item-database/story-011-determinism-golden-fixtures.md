# Story 011: 跨平台确定性黄金夹具

> **Epic**: 物品与配方数据库
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-24(AC-29 实测补跑完成 —— 三 AC 全 VERIFIED;矩阵建设残余另账)

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
- Guardrail: ~~AC-29 当前 BLOCKED-BY-实测~~ **✅ 2026-09-24 解除** —— IL2CPP player + F7 峰值向量已实测(证据 `production/qa/evidence/ac-29-il2cpp-crosscheck-2026-09-24.md`,19/19 逐位);**禁借绿条款持续有效**:ARM64/发版前/CI 矩阵格未跑就是未跑,不得以本批结果冒充;`UNITY_LICENSE` 未配时 CI 红是预期态非测试缺陷

---

## Acceptance Criteria

*From GDD `design/gdd/item-database.md`, scoped to this story:*

- [x] **AC-21a-28**: 参数表逐条枚举边界组合(所有 cap 取 0/取满、ENV_MOD 取 MIN/MAX、quality 取 1/MAX_QUALITY、EFF 取 EFF_MIN/EFF_MAX、outputs 取 m=1/m>1),F1/F2/F5 求解,每组合 SplitMix64 哈希与 `tests/unit/item_database/golden/` 下已提交且经人工核对的金标准逐位相同。⚠️ 金标准不得由首次运行自动生成(自指 ⇒ 恒过);必须由独立参考实现或手算产出、经评审签字后提交。**✅ 2026-09-24:19 条(12 S 场景 + 4 F5 + 3 codec 字节样本)逐位比对全绿 + 篡改一位必红反证 + 出处元数据断言三测通过 —— 金标准由独立 Python 参考实现产出(与 C# 零共享代码),C# 侧只读不写(防自指)。**
- [x] **AC-21a-29** [I]: 同一组参数,Editor(Mono)与 IL2CPP 独立玩家构建中求解,哈希逐位相同。⚠️ **✅ 2026-09-24 VERIFIED(Linux-x64 两后端)**:Mono 腿 = 编辑器 PlayMode 3/3 绿(`backend=Mono`,platform=LinuxEditor);IL2CPP 腿 = StandaloneLinux64 UTF player 独立构建 3/3 绿(`backend=IL2CPP`,platform=LinuxPlayer,`_APPDOMAIN=IL2CPP Root Domain`,退出码 0);外部 diff 三方(golden / Mono / IL2CPP)**19/19 逐位相同** + F7 峰值向量(SplitMix64 回绕 / Fix.MulRaw 2^32×2^32 / long.MinValue 量积 / half-away ties / 域外抛)player 侧已知值断言全过 —— 证据 `production/qa/evidence/ac-29-il2cpp-crosscheck-2026-09-24.md`(两腿输出文件 + 命令 + 订正记录)。**残余挂账(不在本 AC 判据内)**:ARM64 交叉格 / 发版前两格 / CI 三格常驻矩阵 / F7 反汇编验 FMA —— 见证据文件「残余」表,归矩阵建设轮。
- [x] **AC-21a-30**: F1…F5 全部中间变量静态扫描 ⇒ 除 facade 的 `ToFloat()` 外无任何 float/double:无 `Math.Round`、无 `(float)` 转型、无 `Math.Exp`(需手写定点版)—— ADR-005「Storage 中不出现任何 float」的验证。**✅ 2026-09-24:全 `Assets/Sim` 目录(门 A 运行期程序集,测试/Editor 工具豁免)六类 token 扫描零命中(剥注释/字符串;`Sim.Contracts` 的 facade 定义在扫描面外 = AC 白名单),扫描器自证测(九类注入全捕获 + 干净探针零误报)通过;三处语法级盲区登记 Completion Notes Deviations(ADVISOORY)。**

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
  - **状态(2026-09-24 复核)**: ✅ 勾选 —— 三测全绿,金标准出处/刷新纪律元数据在文件头,提交历史可证非首跑冻结。

- **AC-21a-29** [I]: 同一组参数,Editor(Mono)与 IL2CPP 独立玩家构建中求解,哈希逐位相同。⚠️ **当前 UNVERIFIED —— BLOCKED-BY-实测**:IL2CPP player 尚未构建(ADR-012 F7 spike 未跑:SplitMix64 与 Q16.16 中间乘踩 C# 有符号溢出定义性回绕 vs IL2CPP C++ UB 风险线);ADR-005 自述 IL2CPP 逐位性「需实测」。**在实测通过前,21a 不得签署 Determinism 结论;本 spec 一律记 BLOCKED-BY-实测,不得以 Mono 单侧结果借绿。**
  - Given: 同一组边界参数表(同 AC-28);Linux-x64-Mono(常驻 UTF)+ Linux-x64-IL2CPP / Linux-ARM64-IL2CPP(`unity-builder@v4` 出 player,独立 job)—— 注意 `unity-test-runner` 只在 Editor Mono 跑,IL2CPP 对拍须 player 路径。
  - When: 两侧分别求解并出哈希;发版前加 Windows-x64-IL2CPP、Apple Silicon 两格。
  - Then: 哈希逐位相同 ⇒ 才可转 VERIFIED;当前状态 = NOT-RUN/BLOCKED-BY-实测。
  - Edge cases: qemu 合成跑对拍(禁——ADR-012 明文 softfloat 不可信);`UNITY_LICENSE` 未配时 CI 三格红是预期态非测试缺陷;F7 溢出用例须包含中间乘峰值参数;单平台独签金标准(违规)。
  - Negative fixture: 无。
  - **状态: ✅ VERIFIED 2026-09-24(Linux-x64 两后端实测)** —— Mono 腿(编辑器 PlayMode)与 IL2CPP 腿(StandaloneLinux64 UTF player 独立构建)各 3/3 绿,外部 diff golden/Mono/IL2CPP **19/19 逐位相同**,F7 峰值向量 player 侧全过;证据 = `production/qa/evidence/ac-29-il2cpp-crosscheck-2026-09-24.md`。**残余(未跑,禁冒充)**:ARM64 交叉格 / 发版前两格 / CI 常驻矩阵 / F7 反汇编 —— 归矩阵建设轮,见证据文件残余表。
  - Suggested test path: `unity/Assets/Tests/PlayMode/determinism_golden_crossplatform_test.cs`(双腿真身;账本 `tests/integration/item_database/determinism_golden_fixtures_test.cs` 仍不建 —— Unity 不编译仓库根,落点说明见 `tests/integration/item_database/README.md`)+ 证据 `production/qa/evidence/ac-29-*`

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
- Integration (AC-29): `tests/integration/item_database/determinism_golden_fixtures_test.cs`(账本路径,**不建文件** —— Unity 不编译仓库根,真身 = PlayMode 测试)+ 证据落 `production/qa/evidence/ac-29-il2cpp-crosscheck-2026-09-24.md` —— **✅ 已建**(2026-09-24)

**Status**: [x] Created —— 两件三物:独立 Python 参考实现
`tests/unit/item_database/golden/golden_v1_reference.py`(**与 C# 零共享代码**,按 GDD F1/F2/F5
+ ADR-006 舍入 + ADR-010 codec 布局手写)→ 金标准 `golden/golden-v1.txt`(19 条 = 12 S 场景
+ 4 F5 + 3 codec 字节样本,头载出处/向量序/刷新纪律元数据)→ 测试真身
`unity/Assets/Tests/EditMode/ItemDatabase/determinism_golden_fixtures_test.cs`(5 测:AC-28
逐位比对/篡改反证/出处元数据 + AC-30 扫描/扫描器自证;账本落点表见
`tests/unit/item_database/README.md` §Story 011)。
**执行 ✅ VERIFIED 2026-09-24 桌面 batch** —— EditMode **463 全绿**(前批 458 + 本批 5),
**一跑收敛**(规格保真修正后 golden 重新生成逐字节相同,先行验证)。
**AC-29 ✅ 同日补跑 VERIFIED** —— 双腿 PlayMode 测试真身
`unity/Assets/Tests/PlayMode/determinism_golden_crossplatform_test.cs`(Mono 腿 3/3 + IL2CPP 腿 3/3)+
外部三方 diff 19/19 逐位相同;全量 PlayMode 回归 **15/15 绿**(原 12 + 新 3)。
证据 = `production/qa/evidence/ac-29-il2cpp-crosscheck-2026-09-24.md`(含两腿输出文件、
后端标记、订正记录、残余表)。

---

## Completion Notes
**Completed**: 2026-09-24(AC-29 实测同日补跑收口)
**Criteria**: 3/3 已勾选(AC-21a-28 / AC-21a-29 / AC-21a-30)—— AC-29 由双腿实测 + 三方 diff 19/19 逐位背书,非借绿
**Deviations(ADVISORY)**: ① AC-30 扫描为**语法级正则**(剥 `//`、`/* */`、字符串/字符字面量后扫关键词/字面量/libm 家族/`.ToFloat(`),非完整编译单元类型推断 —— QA 条款「`var` 推断为 double」的正例靠 `double` 关键词与浮点字面量两路兜住,插值串内嵌表达式是已登记盲区(注入自证测覆盖九类 token);② AC-28 集成级字节样本取自真实 C# codec 产物(PayloadCodec/ItemInstanceCodec/SimEventCodec),Python 侧按 ADR-010 tag 布局手写对拍 —— 首跑即逐位相同,无偏差待修。两者均非规格偏离,登记备查。
**范围边界**: CI 三格常驻矩阵 / ARM64 交叉格 / 发版前两格 / F7 反汇编验 FMA = **矩阵建设轮**(AC-29 判据本体已在 Linux-x64 两后端实测,矩阵是把该判据常驻化的 CI 基建 —— 残余表见证据文件);金标准升 golden-v2 重签 = 未来公式/codec 变更时的刷新义务;`CDF walk` / 定点 `Exp` 等 ADR-012 单元级清单里非 21a 面的算子归 9/52 各自故事
**Test Evidence**: Logic —— ① AC-28/30:真身 `unity/Assets/Tests/EditMode/ItemDatabase/determinism_golden_fixtures_test.cs`(5 测);② AC-29:真身 `unity/Assets/Tests/PlayMode/determinism_golden_crossplatform_test.cs`(3 测,双腿)+ 证据 `production/qa/evidence/ac-29-il2cpp-crosscheck-2026-09-24.md`(含 `ac29-hashes-{mono,il2cpp}.txt`);金标准 `tests/unit/item_database/golden/golden-v1.txt`(19 条)+ 参考实现 `golden_v1_reference.py`;落点表 = `tests/unit/item_database/README.md` §Story 011 + `tests/integration/item_database/README.md` §Story 011
**Code Review**: Skipped(lean 模式,承 Story 001–010 先例;桌面批次无 `/code-review` 记录)
**执行状态**: ✅ **VERIFIED 2026-09-24 桌面** —— EditMode **463 全绿**(AC-28/30)+ PlayMode 双腿 3/3 × 2 + 全量 PlayMode **15/15 绿** + 三方 diff **19/19 逐位相同**(AC-29,Linux-x64 Mono↔IL2CPP)。**三 AC 全 VERIFIED**。Determinism 结论就 Linux-x64 两后端已签署;矩阵建设残余(ARM64 / 发版前 / CI 常驻)另账挂证据文件残余表,不随本故事冒充已跑。

---

## Dependencies

- Depends on: Story 001, 003, 004, 005, 010(F1/F2/F5 求解器 + Fix 编码器全部就位,才有东西可哈希)
- Unlocks: None(确定性验证是横切收口层,不单独解锁其他故事)
- **2026-09-24 陈旧性复核**: Depends on 未变(字节样本额外消费 Story 009 的 Craft 载荷结构与 b1b 的 SimEventCodec,但二者是前置已就位件、非本故事新增依赖边);Unlocks 仍为 None —— 无陈旧
