# Story 002: F-53.1 确定性结算(Outcome 枚举)

> **Epic**: 医疗后果与责任
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/medical-consequences.md`(F-53.1 `Outcome = Judge(P,C,D,U)` · 双轴裁定 · 试药史形状 OQ-53-9 已裁 · AC-53-01/02/03/08/15 · 支柱注:病人「走了」= 世界无慈悲,53 永不产出「玩家致死」)
**Requirement**: TR-medcons-002(结算 = 三源纯函数可重建) · TR-medcons-003(U 自算:SplitMix64 盐派生) · TR-medcons-007(零可变态零订阅 31,partial)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-007(主): 掷骰状态可重构 · ADR-012: 跨平台确定性 CI 门 · ADR-005: 确定性 · ADR-008: 判断记录投影
**ADR Decision Summary**: `Outcome = Judge(P, C, D, U)`:P = 判断记录投影@结案(经 salted_key 语义)、C = 处置记录投影(病史流)、D = 9 病程投影@Tick(结案)(`treatable_by` 对因手柄门命中)、U = `SplitMix64(WorldSeed, "case-salt", salted_key)`;salted_key 来自 `PatternRecognizedPayload`,**不含裸 disease_id**(保密 = 防御纵深,ADR-008)。**53 重算、不读 37 的 verdict**(AC-53-08:53 是全案唯一正确性判准,37 无 correctness 字段)。**Outcome = 枚举非分数**(AC-53-01);**零浮点字面量静态检查**(AC-53-03,门 A)。**黄金夹具**:AC-53-02/15 = ADR-012 跨平台逐位一致(IL2CPP vs Mono)+ case-salt 单元夹具 + int64 溢出 F7 spike 口径。**双轴**:结局轴(在/走)由 9 判,53 只读;误诊自愈 = 无痕即无回响。试药史 = 同病人连续 `DrugTreatmentApplied` 无支持性 `JudgmentRecorded`(OQ-53-9 形状已裁、值归数值轮)。31 零引用(AC-53-12 asmdef 引用集断言)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH
**Engine Notes**: 承重 = ADR-012 实测(IL2CPP C++ 有符号溢出 UB;hi/lo 无符号拆分是裁决);本机【超算】可跑 Mono 侧,IL2CPP 格归 CI —— NOT-RUN 如实登记,禁借绿。

**Control Manifest Rules (this layer)**:
- Required: Judge 纯函数(同输入同输出);舍入 `ROUND_HALF_AWAY_FROM_ZERO`;U 的每个输入 ∈ 三源;判定表数据驱动(ADR-014 烘焙)
- Forbidden: 读 37 verdict / 缓存判定于对象字段;float/double 字面量;`UnityEngine.Random` 等第二 RNG;31 的任何符号引用
- Guardrail: Outcome 枚举闭集(值集 = GDD F-53.1 表);致死唯一路径 = 9 手柄门(53 面零死亡写入)

---

## Acceptance Criteria

*From GDD `design/gdd/medical-consequences.md`, scoped to this story:*

- [ ] **AC-53-01**:Judge 输出 ∈ Outcome 枚举闭集;类型面**不存在**连续分数(int 枚举语义;「置信度→后果强度」类连续通道 = 断言红)
- [ ] **AC-53-08**:53 自算正确性,代码面无 37 verdict 读取路径(引用 + 反射双断言);D 输入仅经病程投影 DTO 的 `treatable_by` 命中位
- [ ] **AC-53-03**:53 源文件零浮点字面量(静态检查入构建);`FixParse` 为唯一数值解析入口(旋钮表)
- [ ] U 派生逐位钉:`SplitMix64(WorldSeed, "case-salt", salted_key)` 单元黄金夹具(键序 / 混盐写法);salted_key 缺失(未认出模式)⇒ U 的退化路径按 GDD 定义(不静默用 0 —— 夹具钉死口径)
- [ ] **AC-53-02 / AC-53-15**:同一事件流在 Mono 与 IL2CPP 跑 Judge ⇒ Outcome 逐位一致(黄金夹具族挂 ADR-012 矩阵;IL2CPP 格 CI 执行,本机跑 Mono + 负夹具)
- [ ] 双轴验证:9 判「走」+ 53 判「对因无误」⇒ 无「玩家致死」字样路径;53 的任何 Outcome 不改变病人生死字段(只读断言)
- [ ] 试药史形状(OQ-53-9):连续 run 计数 + 支持性判断的谓词实现,阈值常量留槽(值归数值轮);极性真源 = 9 注册表(53 不判极性)
- [ ] **AC-53-12**:53 asmdef 引用集恰 = {BCL, Sim.Contracts(契约件)};31 符号零命中(编译期)
- [ ] 误诊自愈:无痕(Outcome = 正确/自愈兼容态)⇒ 不产出回响事件(与 Story 003 的发出门联动)

---

## Implementation Notes

*Derived from F-53.1 / ADR-012 Implementation Guidelines:*

1. Judge = 判定表数据(作者态 `assets/data/53_outcome_table.json` → `.cooked`,P/C/D 组合 → Outcome,U 仅按 GDD 表定义的档位参与)+ 纯函数求值器;表值与 U 的具体用法归数值轮,本 story 交**形状 + 烘焙校验 + 夹具跑通**。
2. 判定表构建期校验:① 组合全覆盖(P/C/D 值笛卡尔积无空洞,空洞 ⇒ 显式 default 行)② 值域 ∈ Outcome 枚举 ③ 无浮点 ④ 极性列零(53 不重判)。
3. `salted_key` 读取链:`PatternRecognized` payload → salted_key(整数)→ 混盐;裸 `disease_id` 不进 53 的输入类型(类型面断言,承 AC-53-07 反渗的前置)。
4. F7 spike 口径:U 的 128 位中间乘走 hi/lo(Story 52-002 同原语,复用不重写);溢出边界进黄金夹具输入集。
5. 31 零引用:同时**不订阅**任何 31 事件(53 是水龙头,单向流只到世界流为止 —— OQ-53-7)。
6. ⚠️ **数值冻结**:Outcome 各档的映射行值 / 试药史阈值 / 极性权重全部留槽;夹具用符号值(A1/B2)非平衡值。

---

## Out of Scope

- [Story 001]: 聚合(本 story 消费其 PerCaseInputs)
- [Story 003]: EmitTick 延迟与 `ConsequenceResolved` 写流(本 story 只产 Outcome 枚举)
- [Story 004]: 回响呈现与不可归因走查
- F-53.3 RegionOutcome(P1a,TR-medcons-008)
- 9 的对因手柄门本体(9 epic;本 story 只读其投影位)

---

## QA Test Cases

**[Logic story — automated test specs]:**

- **AC-1**: Outcome 枚举闭集 + 无分数(AC-53-01)
  - Given: 判定表全组合夹具
  - Then: 输出恒 ∈ 枚举;类型反射无 Fix/int 连续量出口
  - Edge cases: 表空洞命中 ⇒ 构建期先红(运行期不可达断言)

- **AC-2**: 逐位一致(Mono 侧可跑子集,AC-53-02/15)
  - Given: 20 例流夹具(含溢出边界 U)
  - When: Mono 跑 + 对照 golden-vN
  - Then: 逐位同;负夹具(有符号右移版)哈希漂移被拒
  - Edge cases: IL2CPP 格 = CI 执行,本机标 NOT-RUN(禁借绿)

- **AC-3**: U 退化与盐链
  - Given: 有/无 `PatternRecognized` 两组
  - Then: 盐路径正确;无识别时退化 = GDD 口径(夹具钉死);裸 disease_id 不出现在任何 53 类型(反射)
  - Edge cases: 同病人二次结案(盐 key 复用规则按表)

- **AC-4**: 只读生死(双轴)
  - Given: 9 判「走」的投影
  - Then: Judge 输入不含「死亡」可控位;输出不写回生命字段(类型面)
  - Edge cases: 「走了」叙事面 = 004;本 story 断言「玩家致死」字符串/枚举不存在

- **AC-5**: 试药史谓词形状
  - Given: run 序列夹具(D,D,J,D 等)
  - Then: 支持性判断打断 run 的形状正确;阈值未裁 ⇒ 常量留槽 + 注释指向数值轮
  - Edge cases: run 跨结案(按 GDD 定义同病人连续,不跨世界日重置 —— 形状照文档)

- **AC-6**: 31 零引用(AC-53-12)
  - Given: asmdef + 符号面
  - Then: 引用集白名单;31 命名空间零命中
  - Edge cases: 经契约件间接看到 31 类型 = 不允许(契约件不含 31)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/MedConsequences/medcons_outcome_judge_test.cs` + `tests/golden/medcons/*`(ADR-012 夹具族,IL2CPP 格 NOT-RUN 标注) — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(PerCaseInputs)· 52-002 SplitMix64 原语(复用)· ADR-014 烘焙基建
- Unlocks: Story 003(Outcome → DelayTable → 发出)· Story 004(呈现消费回响)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*
