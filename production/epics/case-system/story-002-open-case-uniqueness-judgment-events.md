# Story 002: 立案两路径 · 开案唯一 · 判断记录事件化

> **Epic**: 病例系统
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28
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
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001;系统 13 `IPresentPatients`(既有契约);52 注入信号、8/10 动作事件(⚠️ D-8-12 master 行未落 —— 桩先行,联调不记绿)
- Unlocks: Story 003(有开案才有结案)、Story 004(案池来源)

---

## Completion Notes

*(留空 — story 关闭时回填)*
