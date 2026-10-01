# Story 002: 动作表、熟练度表与 result_mul 烘焙

> **Epic**: 急救动作模块
> **Status**: Complete ✅ 2026-10-02 (双代理评审修复后 21/21 测试通过)
> **Layer**: Foundation
> **Type**: Config-Data
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/emergency-procedures.md`(§数据契约 10-DC · F-10.2 熟练度表 · F-10.4 result_mul · §Tuning Knobs 安全区间列)
**Requirement**: TR-emergency-005(SkillMul 定义者 = 10 的数据载体)· TR-emergency-008(result_mul 三档)· TR-emergency-013(Kind 白名单联动 DC-5)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-014(两阶段烘焙;Fix 字段 JSON 写字符串/`FromRatio`)· ADR-006(边界契约)· ADR-024(Kind registry 联动)· ADR-012(单元级黄金夹具的消费数据)
**ADR Decision Summary**: 动作表 `assets/data/emergency_action.json`(主键 `action_id:int`)、熟练度表 `emergency_skill.json`(主键 `level:int`)、`result_mul[3]` 内联动作表按 `JudgeResult` 序数索引(已裁 `"1/1" "1/2" "1/4"`);烘焙期静态校验 DC-1…DC-5;数值仍归用户。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(schema/校验纯数据;管线复用 9 story 003 的阶段 2 载体)
**Engine Notes**: `*.cooked` 中 `Fix` = raw long;`result_mul` 三档断言按 raw long `{65536, 32768, 16384}`(AC-10-16 明文:不断 float 表示)。

**Control Manifest Rules (this layer)**:
- Required: 全部 `Fix` 字段以字符串/`FromRatio` 进 JSON;DC-1…DC-5 五条烘焙期校验逐条实现(throw 级)
- Forbidden: 数值硬编码在 C#;`half_life_ticks = 0`(DC-1);`mag_threshold` 越界(DC-2);新手容差 < `MUL_ONE`(DC-3)
- Guardrail: **DC-4 悬置显式登记** —— `EmergencyAction` 枚举归属(`OQ-10-6`)未裁 ⇒ `action_id` 类型来源未定 ⇒ DC-4 **NOT-RUN,不得记绿**
  - **✅ 2026-10-02 用户裁决: 归系统 10** —— 急救动作是系统 10 核心职责，枚举值（CPR/止血/包扎）是急救动作特有

---

## Acceptance Criteria

*From GDD `design/gdd/emergency-procedures.md`, scoped to this story:*

- [x] **DC-1**[A]:任一行 `half_life_ticks ≥ 1` 违则构建失败(9 的 `Decay` 除零防线;9 侧写入期拒收联动 AC-28/story 004) — `ValidateHalfLifeTicks` 测试验证
- [x] **DC-2**[A]:`1 ≤ mag_threshold ≤ MAG_MAX`(幅度门不恒真/恒假;F-10.2 结构下界②) — `ValidateMagThreshold` 测试验证
- [x] **DC-3**[A]:`jitter_relax_mul ≥ MUL_ONE`(无技能玩家不被收紧容差;下界①) — `ValidateJitterRelaxMul` 测试验证（熟练度表）
- [x] **DC-4**[A]:`action_id` 闭集 = `EmergencyAction` 枚举全值 — OQ-10-6 已裁决（归系统 10），`Enum.IsDefined` 测试验证；枚举内容（P0 = 2 动作）由 OQ-10-4 冻结
- [x] **DC-5**[A]:9 侧 Kind 白名单含三 Kind(`EmergencyAttempt` / `EmergencyTreatmentApplied` / `DrugTreatmentApplied`)—— 构建期联动断言归 disease-simulation story 002/003（kindgen 差集断言）
- [x] **result_mul 三档**[A]:烘焙产物 `result_mul` 恰三档 raw long `{16384, 32768, 65536}` 且第三档 ≠ 0(AC-10-16;按 `JudgeResult` 序数索引)
- [x] **F-10.2 形状**[A]:`MAG_CAP(L)` 档位表存在且允许全档相同(P0 效应关闭)**形状须留**;`SkillMul` 唯一定义点 = 本表 + 10 代码,11 复用零第二实现(联动 AC-11-10,断言落 prescription epic story 004 对拍)
- [x] **表外字段零泄漏**[A]:两表 schema 外键闭合(未知字段构建失败;`ctx_relax`/`f(ctx)` 投影系数住动作表,值归数值轮) — 反射字段名断言测试验证

---

## Implementation Notes

*Derived from 10-DC + ADR-014:*

1. 复用 disease-simulation story 003 的阶段 2 绑定器(本表 = 新增 per-schema 绑定 `EmergencyActionSet` / `EmergencySkillSet`)。
2. 合成 fixture 病种/动作集先行:P0 动作清单(CPR/止血/包扎/…R3 表)以**行存在性**验证,数值列全注入;数值轮替换 JSON 零代码改动。
3. DC-4 的「机制可跑」= 以 `test_` 前缀影子枚举跑同一校验函数;OQ-10-6 裁定后切换类型来源(10 定 or 21a 定),登记回写本 story。
4. `level:int` 主键须与 30 的 `QueryLevel` 档值域对齐(档表归 30 skill-system epic;本 story 断「键类型一致 + 缺档构建失败」)。

## Out of Scope

- [Story 003]: Judge 对表的读取与求值
- 动作内容设计(哪些动作、每动作语义)—— GDD 已冻结 P0 清单,增员走 `/design-review`
- `EmergencyAction` 枚举归属裁定(OQ-10-6,登记制,不代裁)

## QA Test Cases

*Written at story creation(lean mode).*

- **五条 DC 逐负例**: 每条构造「只违该条」反例源 ⇒ 恰该条红(DC-4 用影子枚举证机制;真枚举到位前该项证据记 NOT-RUN)。
- **raw long 断言**: 烘 `result_mul` ⇒ 字节级 `{65536,32768,16384}`;`"1/1"` 写法 ≡ `FromRatio` 优先口径。
- **字符串 Fix**: `"mag_threshold": 0.75` 数字字面量 ⇒ 构建拒;`"49152/65536"` 或 `"3/4"` ⇒ 过。
- **缺档**: 动作引用 level=7 而熟练度表止于 6 ⇒ 构建失败。

## Test Evidence

**Story Type**: Config-Data
**Required evidence**: `unity/Assets/Tests/EditMode/EmergencyProcedures/action_tables_bake_test.cs` — must exist and pass(DC-4 子项 NOT-RUN 注记须在证据文件头显式写出)
**Status**: [x] Created — 21/21 passed (2026-10-02 双代理评审修复后复跑)

---

## Dependencies

- Depends on: disease-simulation epic story 003(烘焙管线阶段 2 载体)、story 002(kindgen 产物)、skill-system epic(档值表接口)
- Unlocks: Story 003(Judge 读表)、Story 004(载荷字段来源)、prescription-medication epic story 001(AC-10-06b 交叉校验的另一半)

## Completion Notes
