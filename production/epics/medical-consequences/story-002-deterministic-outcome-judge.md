# Story 002: F-53.1 确定性结算(Outcome 枚举)

> **Epic**: 医疗后果与责任
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-16

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

**[Sprint 12 增量(S12-9,2026-10-15 · qa-plan-sprint-12)]** —— 补实现路线判据(上列 AC-1…6 为规格面,下列为「先勘察再动手」的依赖面 + 收缩判据):

- **S12-9-AC-a(依赖面勘察先行)**: 动手前先读,再裁
  - Given: `Inputs53` 入向闭集(story-001 已实现:`Inputs53` 五路闭集 + 全序聚合 + 20 条测)
  - When: 读 P(判断记录投影)/ C(处置记录投影)/ D(9 病程投影 @Tick)/ U(SplitMix64)四入向的**既有可复用面**
  - Then: 逐入向标注「已可用 / 需补 / 不存在」三类
  - **收缩判据**:若「需补 / 不存在」≥ 2 路 ⇒ **退回单独立项**,不在 S12-9 硬吃(承 sprint-12 Risk「链深即退」)
- **S12-9-AC-b(首个不变量夹具先于实现)**: 承 `.claude/docs/coding-standards.md`「Write tests first」
  - Given: AC-53-01 闭集反射断言(最小面)
  - When: 实现 `Judge` **之前**
  - Then: 夹具先红(无 `Judge` 符号 ⇒ 编译红或断言红),再实现转绿
  - Edge cases: 不得先写实现再造判据(那正是 S12-4 新规要治的「假绿夹具」同型)
- **S12-9-AC-c(跨平台格 NOT-RUN 登记)**: IL2CPP 三格归 CI
  - Given: AC-53-02/15 黄金夹具族
  - Then: 本机跑 Mono 侧 + 负夹具;**IL2CPP 格如实登记 NOT-RUN(禁借绿)**,挂 ADR-012 矩阵
  - Edge cases: 本机【超算】仅可跑 Mono —— 不得因本机绿就声称「跨平台逐位一致已验证」

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/MedConsequences/medcons_outcome_judge_test.cs` + `tests/golden/medcons/*`(ADR-012 夹具族,IL2CPP 格 NOT-RUN 标注) — must exist and pass
**Status**: [x] Created & passing(2026-10-15,S12-9;2026-10-16 返工 + 重评两轮复核)

- **S12-9 首轮**:23 条全绿;回归 `3444 / 3398 / 0 / 46`(基线 3421/3375/0/46 ⇒ +23/+23 零回归;`unity/Logs/s12-9-final.xml`)。
- **golden 真源**:`tests/golden/medcons/golden_v1_reference.py`(独立 python,**与 C# 零共享代码**)+ `golden-v1.txt`;
  A05 与既有 case-salt golden 交叉验证逐位一致;N01 signed-shift 负夹具钉死。
- **S12-9-AC-b 证据**:夹具先于实现,初跑 24 × CS0246(`unity/Logs/s12-9-red.log`)。
- **IL2CPP 三格(AC-53-02/15)**:**NOT-RUN 如实登记**(本机 Mono;挂 ADR-012 矩阵,CI 执行;禁借绿)。
- **AC-53-03 载体**:`MedConsFloatGate.cs`(注释剥离 token 扫描 + 浮点字面量三形态正则,接 `AssemblyGates.RunAll`)。
- **证据原件**:`production/qa/evidence/impl-s12-9-medcons-outcome-judge-2026-10-15.md`(迭代 8 轮全红如实)。
- **键域裁定**:3×3×2 = 18 行 + default(用户 AskUserQuestion 2026-10-15);U 档列默认 1 档;
  退化路径 = 显式中点 1/2(raw 32768,非静默 0,夹具钉死)。

### 返工轮(2026-10-16 · `/code-review` 裁定 CHANGES REQUIRED)

**原件**:`production/qa/evidence/review-medcons-story-002-2026-10-15.md`
(3 BLOCKING R-1/R-2/R-3 + 10 WARNING/MINOR A–J,含 原判定 → 修复落点 → 验证命令)

**回归**:`3462 / 3416 / 0 / 46`(`unity/Logs/s12-9-rework10.xml`,两批证伪轮突变全部还原后的复跑)——
较 S12-9 首轮 **+18/+18,零回归**。18 条新探针 = 11 条返工探针 + 7 条重评探针。

**S12-4 假绿夹具强制证伪轮(R-2 的结案件)** —— 每条探针「停用 X ⇒ 该夹具转红」的实证:

| # | 夹具 | 对应 | 停用 X | 实证 |
|---|---|---|---|---|
| F1 | `test_bake_cookedHeader_schemaVersionEchoesBound` | R-1 | 头 offset 8 改回硬写 0 | ✅ RED |
| F2 | `test_bake_cookedBytes_deterministic_sameSourceTwice` | A | 排序比较器退回单键 | ✅ RED*(注入非确定项;见下注) |
| F3 | `test_rowKey_noCollision_acrossBands` | A | 行键乘子退回逐行量 `uBand+1` | ✅ RED |
| F4 | `test_table_uBandDiscriminates_whenBandCountGtOne` | B | `Evaluate` 传硬编码 0 | ✅ RED |
| F5 | `test_table_miss_fallsBackToDefault_notLifeAxis` | C | 删显式 default 回落 | ✅ RED |
| F6 | `test_bandOf_bandCountBeyondRawDomain_doesNotDivideByZero` | G | 删 `span == 0` 守护 | ✅ RED |
| F7 | `test_oq539_interleavedPatients_doNotContaminateRun` | D | 退回单游标 run | ✅ RED |
| F8 | `test_ac5301_outcomeAxisNarrowing_tableRejectsLifeAxisRows` | T-2 | 删表构造器结局轴拒收 | ✅ RED |
| F9 | `test_bake_uBandMissingKey_hardFails` | E | `u_band` 退回可选 | ✅ RED |
| F10 | `test_bake_schemaVersionMismatch_hardFails` | R-4 | 删 schema_version 期望值比对 | ✅ RED |
| F11 | `test_gate_floatLiteralPatterns_catchLiterals_notDocReferences` | R-3 | 门字面量判据停用 | ✅ RED |

**纪律**:每条「改生产码 ⇒ 跑 ⇒ 判红 ⇒ 还原 ⇒ 跑 ⇒ 判绿」七步在**同一次 bash 调用内**完成
(每条独立 `trap RETURN` 还原 + 全局快照 `EXIT` 还原作第二保险;跑完
`grep -rn FZMARK unity/Assets/` = 零残留)。完整输出:`unity/Logs/s12-4-falsify.txt`
(单次 filtered EditMode 运行 ≈ 31 s,11 条合计 ≈ 6 min)。

**⚠️ 两处如实记录**(详见原件 §四):
1. **F2 对「非稳定排序」子缺陷无判别力** —— A 修复后行键已互异,任何正确比较排序都定序。
   首轮按字面「退回单键比较器」突变 ⇒ GREEN(未证伪);补跑「向载荷注入非确定项」⇒ RED。
   ⇒ 该夹具守的是 ADR-014 §二**普遍确定性**,「非稳定排序」的判别力由 F3 承担。
   *纪律价值:照抄「停用 X」字面描述就断言通过 = 假绿;必须真的跑。*
2. **F3 首轮 NORUN** —— 突变串漏了原版行尾配对 `)` ⇒ CS1026 ⇒ 无 XML。修正后重跑 RED。
   登记为返工期工具错误,与判据无关。

### 重评轮(2026-10-16 · `/code-review` 第二轮)

**原件**:`production/qa/evidence/review-medcons-story-002-rework-2026-10-16.md`
(引擎 `unity-specialist` + 测试 `qa-tester` 并行重评)

**裁定**:**APPROVED WITH SUGGESTIONS** —— 首轮 14 项判据**全部结清**(J 在首轮只做一半,
本轮补齐);重评新发现 9 条引擎缺陷 + 3 条 QA 补强 + 1 条 BandOf uRaw 域,**全部已修并证伪**。

**重评补强的 7 条新探针**(第二批证伪轮,`unity/Logs/s12-4-falsify2.txt`,7/7 RED):

| # | 夹具 | 修什么 |
|---|---|---|
| F12 | `test_bake_missingWholeRow_coverageValidationFires` | 校验① 覆盖分支**首次真正被执行**(原负例走错分支被 `errors.Count==0` 短路) |
| F13 | `test_careLaneOf_matchedTrue_yieldsAppliedMatched` | C 轴 `matched=true` 分支**零正例载体** |
| F14 | `test_bandOf_uRawBeyondDomain_doesNotReturnNegativeBand` | `BandOf` uRaw 超域 ⇒ unchecked 窄化回绕出**负档序数** |
| F15a | `test_oq539_nullAndEmptyStream_returnsFalse` | 空态分支无载体 |
| F15b | `test_oq539_arrivalOrderPermutation_invariantResult` | 全序键排序分支从未被打乱 |
| F16 | `test_table_uBandOutOfRange_fallsBackToDefault_notAliasToNextCombo` | **N-3**:越界 uBand 静默别名到相邻 (P,C,D) 行 |
| F17 | `test_table_uBandCountZero_rejected_notSilentRowCollapse` | **N-4**:`uBandCount=0` 时全表键坍缩、字典静默丢 17 行 |

**两批合计 18 条证伪探针,全部 RED。**

**⚠️ 两处「夹具本身判别力不足」被实证发现并加固**(均登记,未留恒真式):
1. **F2** 对「非稳定排序」子缺陷无判别力(键互异后任何正确排序都定序)—— 判别力由 F3 承担;
2. **F15b** 首版两组夹具在「排序后 / 到达序」下结论相同 ⇒ 删排序也照样绿 ——
   已改为 `J` 的 tick 夹在中间,使两种序给出**相反**结论。

**重评另修的生产缺陷**:`TryLookup` uBand 域守护(N-3)· `Table` 构造器 `uBandCount ≥ 1`(N-4)·
default 行 `u_band` kind 检查(N-5)· 浮点正则补数字分隔符 `1_000f`(N-6)·
`RunAll`/`ParseRows` 拆方法(N-2)· 删 `literalText` 死局部(N-1)· 测试改显式 `ReadU32Le`(N-8)·
订正「词法层拒绝」注释(N-9)。

**重评登记的剩余 Minor(不阻塞)**:golden `C01_degraded_uraw` 孤儿行 · 5 组合仅测试内镜像兜底 ·
AC-53-03 `FixParse` 半句无载体 · 门构建期注册层检不出「注册被删」 · `Materialize` 碰撞分支无直驱 ·
`Judge.Evaluate(null)` 无测试 · 共享静态池无 teardown · EditMode 真实文件 I/O。
完整 9 条见原件 §五。

### 返工轮登记的三处 AC 缺口(如实登记,不以测试自造断言顶替)

1. **AC-53-08 缺「引用」半边**:原文「代码面无 37 verdict 读取路径(**引用** + 反射双断言)」——
   反射半边在 `test_ac5308` 有载体;**编译期 / 门级的「引用」半边无载体**。
   ⇒ 登记为**跨 story 载体**,002 内不造断言顶替。
2. **「误诊自愈 ⇒ 不产出回响事件」后半句**:回响事件发出归 **story-003**(`EmitTick` /
   `ConsequenceResolved` 写流),002 内无载体。同上登记。
3. **AC-53-08 的 D 输入接线仅为类型面**:`IDiseaseProjection` / `treatable_by` 命中位的
   **端到端装载归装配轮 / story-003**;002 的 `CareLaneOf(…, bool matched)` 只钉「命中位
   由调用方给出、53 不判」的**形状**。测试中的 stub 命中位是形状夹具,**不是**已验证的接线。

### 返工轮登记的两处形状留槽(非缺口,归后续轮)

- **U 判别力载体 = `band_count > 1`**:现网 `u_band_count = 1` ⇒ `UBand` 恒 0,
  U 档对 Outcome 无判别力。这是**数值轮**的决定(键域裁定「U 档列默认 1 档,常量留槽」);
  B 返工已把 `UBand` 接进查表,`test_table_uBandDiscriminates_whenBandCountGtOne`
  以双档表证明档位**可**判别。⇒ 判据已就绪,等数值轮扩档。
- **R-3 残余缺口(不借绿)**:`MedConsFloatGate` 的浮点判据是**源级**(注释剥离 + 字符串剥离
  + 三形态正则),看不见 IL 层的 `conv.r4` / `conv.r8`(常量折叠 / 算术产生的非字面量浮点)。
  本仓更强先例 = `EcozoneIntegerGates`(AC-6-22,Mono.Cecil IL 级)。
  **升级触发条件**:53 数值轮引入任何可能产生浮点的算术时,按该先例扩列。
  53 当前零浮点算术(U = Q16.16 raw long 整数比较)⇒ 源级已覆盖其全部数值路径。

---

## Dependencies

- Depends on: Story 001(PerCaseInputs)· 52-002 SplitMix64 原语(复用)· ADR-014 烘焙基建
- Unlocks: Story 003(Outcome → DelayTable → 发出)· Story 004(呈现消费回响)

---

## Completion Notes
**Completed**: 2026-10-16
**Criteria**: 9/9 passing(无 deferred;「误诊自愈 ⇒ 不回响」后半句归 story-003,已登记)
**Deviations**:
- ADVISORY —— Story 写于 manifest `2026-10-02`,现行 `2026-10-15`;14 项判据已按现行 manifest 逐条重核通过。
- 登记项(非 deviation)—— TR-medcons-007 `partial` 已在 Requirement 行登记(零可变态 ✅ /
  零订阅者归拉模式重放面)。
- 留槽常量 —— `DegradedURaw = 32768` / `TrialRunThreshold = 3` /
  `ConfidenceConfirmedFloor = 2`,按卡面 Note 6「数值冻结」登记,golden 夹具钉形。

**Test Evidence**(Logic):`unity/Assets/Tests/EditMode/MedConsequences/medcons_outcome_judge_test.cs`
—— **41 条 `[Test]`,终轮 41/41 Passed**;账本树 `tests/golden/medcons/*`(Python 独立参照,
与 C# 零共享代码)。终轮回归 `3462 / 3416 / 0 / 46`
(`unity/Logs/s12-9-rework10.xml`),较 S12-9 首轮 **+18/+18,零回归**。

**Falsifiability Gate(S12-4 · BLOCKING 级)**:**PASS** —— 两批共 **18 条证伪探针,18/18 RED**
(`unity/Logs/s12-4-falsify.txt` 11 条 + `unity/Logs/s12-4-falsify2.txt` 7 条),
跑完 `FZMARK`/`FZ2MARK` 零残留。两处「夹具本身判别力不足」被实证抓出并加固
(F2 对非稳定排序无判别力 ⇒ 判别力由 F3 承担;F15b 首版两种序结论相同 ⇒ 改为相反结论)。

**Code Review**:Complete —— 两轮 `/code-review`
- 首轮:`production/qa/evidence/review-medcons-story-002-2026-10-15.md`
  —— 裁定 **CHANGES REQUIRED**(3 BLOCKING R-1/R-2/R-3 + 10 WARNING/MINOR A–J),全部结清。
- 重评:`production/qa/evidence/review-medcons-story-002-rework-2026-10-16.md`
  —— 裁定 **APPROVED WITH SUGGESTIONS**;14 项判据全部结清(**J 首轮只兑现一半,重评补齐**),
  新发现 9 条引擎缺陷 + 3 条 QA 补强全部已修并证伪。

**IL2CPP 三格(AC-53-02/15)**:**NOT-RUN 如实登记** —— 本机 Mono;挂 ADR-012 矩阵,CI 执行;禁借绿。

**剩余 Minor(不阻塞,见重评件 §五 共 9 条)**:golden `C01_degraded_uraw` 孤儿行 ·
5 组合仅测试内镜像兜底 · AC-53-03 `FixParse` 半句无载体 · 门构建期注册层检不出「注册被删」·
`Materialize` 碰撞分支无直驱 · `Judge.Evaluate(null)` 无测试 · 共享静态池无 teardown ·
EditMode 真实文件 I/O · N-7 verbatim/插值字符串不在源级判据面内。
