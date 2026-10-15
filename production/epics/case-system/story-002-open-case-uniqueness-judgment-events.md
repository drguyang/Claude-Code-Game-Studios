# Story 002: 立案两路径 · 开案唯一 · 判断记录事件化

> **Epic**: 病例系统
> **Status**: In Review ⚠️ 2026-10-15 —— 补评门不通过(原 `Complete ✅ 2026-10-06(c4e3708)` 判定不成立;原件为补评件)
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-15
## Context

**GDD**: `design/gdd/case-system.md`(规则二 立案 A/B 两路径 + 因果订正 + 开案唯一 · 规则三 串接 · 规则四 判断记录/存新值/Judgment 形状 · 药材短缺不立案)
**Requirement**: TR-case-012(立案两路径,原 gap —— 本 story 兑现机制层) · TR-case-033(同一病人同时至多一个开案) · TR-case-010/011(JudgmentRecorded/JudgmentRevised 事件化) · TR-case-036(就诊动作语义归 8/10 词表,37 只订阅 —— partial 的 37 半边)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-008(主): 病例流;ADR-009: 三问判据
**ADR Decision Summary**: 立案进流的充要条件 = ADR-009 §二「模拟态」判据(有真源/有权威/可感知),**与触发者无关** —— 路径 A(52 注入的急召出诊/原型疫情,玩家抵达+就诊交互时刻立案)与路径 B(13 自然就诊)入流义务完全相同;37 **不定义就诊动作**,只订阅「动作已开始」信号(经 13 的 `IPresentPatients` + 8/10 动作事件,以其 Tick 为立案时刻 —— 载体欠债在 8 的 D-8-12 master 行);`(state=开 ∧ patient_id=p)` 恒 ≤ 1,第二次交互 = 回到已开那例(**AC-37-26**);药材短缺**不立案**(无病人);判断记录落病例流**不违反 8 铁律②**;`JudgmentRevised` **存新值**(完整快照,兑现 AC-51-B3),`Judgment = {lexicon_id(u16 ordinal), confidence(u8 档), freehand_text(纯呈现,永不进判定)}`。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 订阅/写入均纯 C# 契约;`IPresentPatients` 为 ADR-016 §六 已定形状。

**Control Manifest Rules (this layer)**:
- Required: CaseOpened 载荷 `{patient_id, disease_snapshot}`(立案 tick = 事件 Tick);唯一性判据 = 流前缀纯函数;串接键 = patient_id,展示排序键 = `(patient_id, CaseOpened 的 (Tick,Seq))`
- Forbidden: 为「非玩家触发」开豁免不入流;为药材短缺硬造病人;37 定义就诊动作语义;freehand_text 进任何判定
- Guardrail: 37 读 13、13 不引用 37(单向无环);13 的 InTreatment 是表现态约束,与 37 的模拟态唯一性**互相印证但不同源,不得互相推导**

---

## Acceptance Criteria

*From GDD `design/gdd/case-system.md`, scoped to this story:*

- [ ] AC-37-30:路径 A 与路径 B 各产生 CaseOpened 入病例流 —— **入流义务不因触发者不同而不同**
- [ ] AC-37-26:`#{c : c.state=开 ∧ c.patient_id=p} ≤ 1` 恒成立;第二次就诊交互不产生新 CaseOpened,返回已开例
- [ ] 药材短缺注入 ⇒ 零 CaseOpened(不立案的负断言,点名 fixture)
- [ ] AC-37-05(判断半边,`[I]`):落笔/改写各产生一条事件进病例流,无内存私有副本;存新值:`J(c)` = 全序最后一条判断事件(改写两次后取末值,非差分合成)。**处置半边**:处置事件由 10/11/25 写病史流(非 37 写)—— 本 story 只集成断言「处置发生后 37 跨流可查到、无第二副本」,写入本体不在 37
- [ ] 改写史全序列在病例流保留(AC-37-23 的事件面;「永不进计分」归 story-005/003 的断言面)
- [ ] `Judgment` 类型形状:lexicon_id/confidence/freehand_text 三字段,判定路径只读前两者(引用扫描:freehand_text 零判定消费点)

---

## Implementation Notes

*Derived from ADR-008/009 Implementation Guidelines:*

1. 立案入口单一:`OnTreatmentStarted(patientId, tick, sourceKind ∈ {A,B})` —— sourceKind 只作记录不改变入流义务(因果订正的执行面)。
2. 唯一性 = 查流前缀:`OpenCaseOf(p) := 最近 CaseOpened 且无其后 CaseClosed`;存在 ⇒ 返回其 case_id,不存在 ⇒ Append。
3. 52 注入侧对接 = 医疗事件模板的降临信号(52 定何时);就诊交互 master 行(8 的 D-8-12)未闭合 ⇒ AC-4-15 等同源断言维持 NOT-RUN,37 以信号桩先行。
4. `disease_snapshot` 在立案时的取值口径 = 该病人当前病程 overlap 病种集快照(结案快照在 story-003,两处不同事实,注释钉死)。
5. 串接(规则三)的**数据面** = SortKey/ThreadKey 暴露给 39;拟物「线订」呈现归 39/42(Out of Scope)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: Kind/载荷形状(本 story 消费)
- [Story 003]: 结案与处置证据窗口
- [Story 004]: 同源检测
- [Story 005]: 词表烘焙/守密门(lexicon_id 的取值合法性校验在彼)
- 系统 8/10:就诊动作词表 master;系统 39/42:脉案串接呈现与 [V] 走查(AC-37-17)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(两路径同义务)**: A/B 都入流
  - Given: fixture A = 52 注入+抵达交互;fixture B = 13 自然就诊
  - When: 各触发一次立案
  - Then: 病例流各得一条 CaseOpened(载荷字段齐);无任何「B 路径可跳过」分支
  - Edge cases: 同 tick 一 A 一 B(不同病人)→ 两条,case_id 不同(承 story-001 AC-37-02)
- **AC-2(开案唯一)**: 连点两次
  - Given: p 已有开案
  - When: 第二次就诊交互
  - Then: 零新事件;返回值 = 原 case_id
  - Edge cases: 结案后再交互 → 新案(复诊,合法);跨病人不受限
- **AC-3(存新值)**: 两次改写后取值
  - Given: J 序列 = 落笔(甲) → 改写(乙) → 改写(丙)
  - When: 求 `J(c)`
  - Then: = 丙的完整快照;重放逐位同;改写史三行都在流中可回看
  - Edge cases: 空判断合法(lexicon_id=0 ∧ freehand_text="")

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/CaseSystem/case_open_and_judgment_test.cs` — must exist and pass(就诊动作 master 未闭合 ⇒ AC-4-15 侧联动维持 NOT-RUN 标注)
**Status**: [ ] In Review — 测试文件存在且 **25/25 绿**(2026-10-15 S11-7 后;原 23/23),但补评判定 **Test Evidence 门不通过**(见下方登记块)

> ⚠️ **2026-10-15 补评件登记(S11-2 后续批 · QA 核查)** —— 原评审原件缺失(承 `review-case-system-001-004-gap-audit-2026-10-15.md` §二:002 确证产出过 BLOCKING 级判定),按义务第 3 条**补做评审(评当下)**,不追认 `c4e3708` 的历史判定。
> **补评原件**:`production/qa/evidence/review-case-system-story-002-retro-2026-10-15.md`
> **判定(⚠️ 已按 §七 勘误修正 —— 见下)**:原判「代码面 4 BLOCKING + 测试面 10 BLOCKING」,
> **复核后收窄**:① **`LastJudgmentOf` 的 `caseId` 形参零引用**(真实现缺陷;有生产实害链:
> `CompositionRoot.cs:245` 传全局混合流 ⇒ `CasebookVitalsSource.InkAgeTicks` 会取到别的病人的
> 判断事件;**新发现的 `Boot/ink_age_test.cs` 4 测全为单案夹具 ⇒ 跨病人混淆仍无覆盖**);
> ② 声明件 `case_open_and_judgment_test.cs` 自身的**断言形态**问题(`decide(A)==decide(B)` 恒真 /
> 测试自造 J(c) / 只查路由表 / 重复断言)。
> ❌ **已推翻**(§七·三):AC-37-30 入流义务、AC-37-05 判断半边、AC-37-05 处置半边、
> AC-37-23 不折叠 —— **均有真载体**,只是不在本 story 声明的那一个文件里:
> `CaseSystem/case_open_writer_test.cs`(真组合根+真流)· `Boot/judgment_writer_test.cs` ·
> `Boot/casebook_command_router_test.cs` · `PlayMode/case_opened_wiring_test.cs`(真启动序)·
> `CaseSystem/case_replay_persistence_test.cs`(noFold 两测)。
> ⇒ 真问题是**声明证据与 AC 错配**,**不是「AC 无证据」**。
> 架构纪律全绿(纯函数 · 门 A · ADR-029 · 零 float);commit `c4e3708` 三条自述 **B1 部分成立 / B2 未兑现 AC 本体 / B3 与其点名的测试内容不符**。
> **Test Evidence 门:不通过。** 修复归**新一轮**;状态**已裁定(2026-10-15,用户)转 `In Review`**。⚠️ 另:AC-37-05/23 在 GDD 为 `[I]` 集成,而 `tests/integration/case-system/` 目录是否存在集成件**未查证完**,登记待查。
>
> ✅ **该「未查证」已于 2026-10-15 结清**(§七 勘误轮):`tests/integration/case-system/` **确实不存在**
> —— 但该目录是**账本约定**树,非唯一证据面;实际集成件在 `PlayMode/case_opened_wiring_test.cs`
> (真启动序)与 `EditMode/CaseSystem/case_open_writer_test.cs`(真组合根 + 真流)等处,已计入 §七 推翻面。
>
> 📌 **修复排期(2026-10-15 用户裁定「并入 sprint-11 · 必须项优先」)**:
> - **本轮取**:① `LastJudgmentOf` 以 `caseId` 过滤 + `CompositionRoot.cs:245-246` 改传**该案子集**
>   + `CasebookVitalsSource.cs:214` 注释对齐 + 补跨病人交错夹具 = **S11-7**;
> - **顺延 sprint-12**(须带状态+归属):声明件的 5 条**断言形态**(BLK-2 纯常量比较 / BLK-3 测试自造 `J(c)` /
>   BLK-5 重复断言 / BLK-9 负断言未按 AC 契约构造 / BLK-1 的恒真半边)+ ③ 把
>   `CaseOpenWriterTest` / `JudgmentWriterTest` / PlayMode 接线件**补进 §Test Evidence 的引用面**(AC→载体映射表)。

> 🟢 **BLK-8 已闭合 —— 2026-10-15 · sprint-11 S11-7**(证据原件:`production/qa/evidence/fix-s11-7-case-system-story-002-blk8-2026-10-15.md`)
>
> **缺陷的准确性质(修复中厘清)**:不是「调用方传错」,而是**接口本身失守** —— 旧契约要求
> 「调用方传入该案事件子集」,但该契约**在生产路径上不可能被满足**:唯一调用方传
> `EventStream.Events`(全部混合流),且**没有任何一层能合法切片**(`SimEvent.Payload` 是
> `PayloadRef`,解码归 `Sim.Codec`,而 `Sim` 引用集恰 = {BCL, `Sim.Contracts`} —— ADR-025 §①)
> ⇒ 契约把责任推给了一个**做不到的调用方**。**与 S11-8 同一结构约束**。
>
> **改动**:
> ① `LastJudgmentOf` 归属判据 = **流序关联**(跟踪该病人 `CaseOpened` 三元组确定「当下开案」,
>   至 `CaseClosed` 前为止)—— **与既有正确范式 `OpenCaseOf` 同构**;
> ② `CasebookVitalsSource.cs:211`/`:214` 注释对齐(原「该病人最近一条」正是缺陷的自我描述);
> ③ 测试:**改写 3 条旧夹具**(原夹具**不含 `CaseOpened`** = 「调用方已切片」形状,
>   正是 caseId 死参逃过测试面的直接原因;与 S11-10 那条反向断言的旧测同型)+ **新增 3 条判别夹具**。
>
> ⚠️ **计划口径订正(已报用户)**:计划写「`CompositionRoot.cs:245-246` **改传该案子集**」——
> **实测该处方基于错误前提**:传整条流本就是正确用法,且「该案子集」构造不出来
> (切片只能靠装配层自己解码 ⇒ 把 37 的查询语义复制到装配层,是更差的架构)。
> ⇒ **修复落在被调用方内部,调用点一字未改**。**属「计划了一个做不到、也不需要做的形状」,非范围缩减。**
>
> **可证伪判据(证伪链双信号一致)**:
> | 状态 | CaseOpenAndJudgmentTest | InkAgeTest |
> |---|---|---|
> | **回退旧实现** | 退出码 **2** · 25 测 23/**2 红** | 退出码 **2** · 5 测 4/**1 红** |
> | **修复后** | 退出码 **0** · **25/25** | 退出码 **0** · **5/5** |
>
> 三条红讯的失败消息**逐字命中** BLK-8 的实害描述,其中 `test_inkAge_crossPatient_*`
> 打印「病人 A:5 − 3 = 2;旧实现取全流末条(病人 B 的 tick=4)⇒ 1 —— **墨龄取错病人**」
> —— 这是**端到端**复现实害链的判据(走 `CompositionRoot` 全生产装配)。
> **新增夹具不可互相替代**:跨病人夹具与同病人复诊夹具的 `Patient` 相同与否不同,
> 只有后者能抓「按 Patient 匹配」与「按 case_id 匹配」的分界。
>
> **零回归**:EditMode `3337/3291/0 failed`(较 S11-11 后 **+3**,恰为本轮新增);
> PlayMode `186/186/0`(**逐位等于 M2 基线**)。引用集一字未动(无 asmdef 改动)。
>
> **未清**:仍余 5 条**断言形态**(含 `test_ac3705_storeNewValue_judgmentIsLastEvent` 仍自造
> `J(c)` 不调生产函数 —— 与本件同一缺口的两半)+ BLK-9 / BLK-10,顺延 sprint-12。

---

## Dependencies

- Depends on: Story 001;系统 13 `IPresentPatients`(既有契约);52 注入信号、8/10 动作事件(⚠️ D-8-12 master 行未落 —— 桩先行,联调不记绿)
- Unlocks: Story 003(有开案才有结案)、Story 004(案池来源)

---

## Completion Notes

*(留空 — story 关闭时回填)*
