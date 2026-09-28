# Story 004: F-37.1 同源检测与 PatternRecognized(含 F-37.1b 脚本链)

> **Epic**: 病例系统
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 7h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28
## Context

**GDD**: `design/gdd/case-system.md`(F-37.1 全式 + 两处订正 · F-37.1b ScriptedChain + W-1 注块 · F-37.3 基数 · 规则八 · 承重账定序/悬垂两框)
**Requirement**: TR-case-018(规则八:同源检测算法,原 gap —— 本 story 兑现机制层) · TR-case-021(fires-once) · TR-case-022(MemberSet 互异) · TR-case-025(salted_key 逐 D 派生) · TR-case-032(ScriptedChain 显式成员 + 一次性,⚠️ W-1 未会签)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-008(主): 盐契约与载荷;ADR-007: 哨兵
**ADR Decision Summary**: `FirstPerPatient(D)`(每病人取全序最早结案例,互异**结构性成立** —— 2026-09-17 由旁注升为筛选算子)→ `CandidateSeq(D)` 全序升序 → `PatternFired(D)@t` 为**存在量化谓词**(非序列≤标量)→ 达 `PATTERN_THRESHOLD`(P0=3,进 ConfigVersion 覆盖集,非拒载)时发**一条**世界级 `PatternRecognized`:`PatientId.None=-1`,载荷 `{salted_key = SplitMix64(WorldSeed,"case-salt",ordinal(D)) 逐 D, anchor_case, member_set(冻结于触发 tick,不随第 4 例增长)}`;**fires-once 永不重发**;纯伤情案(病种空集)不入检测;共病一案进多组;**同 tick 多 D 按 salted_key 整数升序写**(禁 disease_set 迭代序,AC-37-27);53 消费须按 Tick 非到达序(哨兵 -1 前向悬垂,AC-37-28 归 53);F-37.1b:脚本链成员 = `ScriptedChain(D)` 显式 patient_id 集(《他回来了》甲乙丙),走同一算式只是定义域收窄,**不新增 Kind、无第二公式**。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: SplitMix64/整数排序纯 BCL;IL2CPP 溢出纪律承 ADR-012 F7(SplitMix64 黄金夹具已有先例)。

**Control Manifest Rules (this layer)**:
- Required: FiredSet/MemberSet 全部**直接读流**(流派生,不存储;触发事实钉在 PatternRecognized 上);判据 ≥ 非 ==(同 tick 批量 2→4 也触发);成员互异由筛选保证
- Forbidden: 存储式 fired 标志/计数器;disease_set 集合迭代序参与定序;把「诊断正确/病名」读进判定(输入只有 state 与结案快照);本 story 内实现 ScriptedChain 联调(**W-1 未裁:三案纯伤情 vs D 域口径须 9+37 会签,「不裁不得实现」**)
- Guardrail: PATTERN_THRESHOLD 经配置注入(int ≥2,<2 构建拒);旧存档在新阈值下「历史照旧、将来分叉」(ADR-010 §七 语义,不拒载)

---

## Acceptance Criteria

*From GDD `design/gdd/case-system.md`, scoped to this story:*

- [ ] AC-37-07/08/09:长度 < 阈值不触发;恰达标发**一次**(哨兵行);第 4 例不重发、不进 MemberSet(冻结)
- [ ] AC-37-10:同 D 三例同 tick ⇒ 触发一次,成员取全序前 3,无平局(全序键含 Patient)
- [ ] AC-37-11:三互异病人、两空名一错名 ⇒ 照样触发(公式不看病名栏)
- [ ] AC-37-12/13:纯伤情案(disease_set 空)不进任何计数;共病一案进两组各计一次
- [ ] AC-37-22/34:A、B、A 三例同 D ⇒ FirstPerPatient 只留 A₁ ⇒ 长度 2 不触发;补 C 例后触发且 MemberSet = {A₁,B₁,C₁}(旧式反例回归哨兵)
- [ ] AC-37-27:同 CaseClosed 令 D₁/D₂ 同时达标 ⇒ 两条 PatternRecognized 按 salted_key 整数升序(Seq 0,1);换插入序重跑逐位同
- [ ] AC-37-14:载荷正向合取 —— **含** salted_key、**不含**裸 disease_id/病种名(结构在 story-001,语义计算在此)
- [ ] AC-37-29(非 ScriptedChain 半边,⚠️ 联调半边 BLOCKED-BY-W-1):随机自然病例不消耗脚本链名额的谓词实现先落(`ChainEligible` 判定域),「指定三例触发 + 永不重发」的夹具待 9+37 会签后补
- [ ] 无时间窗口下界:三例任意跨度结案可触发(性质 2 夹具)

---

## Implementation Notes

*Derived from ADR-008/007 Implementation Guidelines:*

1. 评估时机 = 每条 CaseClosed 入流后的同 tick 结算(主机),输入 = 已完成流前缀纯函数;Step/CatchUp 同路径。
2. `salted_key(D) = SplitMix64(WorldSeed, "case-salt", ordinal(D))` —— 逐 D(全局单值旧写法作废);WorldSeed 读存档头(ADR-007 §二)。
3. 定序:`candidates` 按 (Tick,Patient,Seq) 升序;触发循环对「本 tick 新达标的 D 集」先按 salted_key 升序再 Append。
4. ScriptedChain 的**数据形状**在此(content 条目显式 patient_id 三元组 → 烘焙/he-returns.md 正典),评估域切换用同一算式;W-1 双口径(伤情入不入 D)未裁 ⇒ 分支代码禁止实现,接口位留空并注明 D 项。
5. PATTERN_THRESHOLD/阈值语义测试用符号化小值(2/3 两档);三案链内容值归 35/内容轮。
6. 53 侧两条义务(按 Tick 聚合 AC-37-28、延迟+不可归因 AC-37-25)不在本 story —— 在 story-006 登记移交,载体在 53 epic。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 003]: CaseClosed 的产生与快照(本 story 的输入)
- [Story 005]: salted_key 是防御纵深非保密层的守密断言面(AC-37-15 侧)
- [Story 006]: 53 消费秩序与重放对拍
- 系统 53:世界变化(延迟/不可归因);playtest:图样可辨识度(TR-case-027,出本 epic)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(复诊污染反例)**: A、B、A 形状
  - Given: 三例同 D 结案,顺序 t₁<t₂<t₃,病人 A、B、A
  - When: 达标判定
  - Then: 不触发(CandidateSeq 长度 2);加互异 C ⇒ 触发且成员 {A₁,B₁,C₁}
  - Edge cases: A 的第二例更早结案不可能(FirstPerPatient 取最早)
- **AC-2(fires-once 与冻结)**: 第 4 例
  - Given: D 已触发(MemberSet 3 例)
  - When: 第 4 例结案(含同 tick 批量情形计数 2→4)
  - Then: 零新聚合事件;MemberSet 不变;批量情形恰在达标 tick 一次
  - Edge cases: 阈值改 4 的存档重读:历史 FiredSet 照旧(读流),后续用新值
- **AC-3(同 tick 多 D 定序)**: 共病双达标
  - Given: 一个 CaseClosed 的 disease_set = {D₁,D₂} 且两者同 tick 达标
  - When: Append
  - Then: 两条按 salted_key 升序,Seq=0,1;打乱 disease_set 构造序重跑 → 流逐位同
  - Edge cases: salted_key 相等不可能(逐 D 哈希碰撞 → 以 ordinal 次级定序,登记测试观察)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/CaseSystem/case_pattern_detection_test.cs` — must exist and pass(AC-37-29 的 ScriptedChain 联调半边 BLOCKED-BY-W-1 不记绿)
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(salted_key 字段/哨兵)、Story 003(CaseClosed);SplitMix64 黄金夹具(ADR-012 既有);`WorldSeed` 存档头(7a)
- Unlocks: Story 006(重放);支柱四 P0 落点验证(待 playtest)

---

## Completion Notes

*(留空 — story 关闭时回填)*
