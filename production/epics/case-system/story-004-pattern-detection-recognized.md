# Story 004: F-37.1 同源检测与 PatternRecognized(含 F-37.1b 脚本链)

> **Epic**: 病例系统
> **Status**: In Review ⚠️ 2026-10-15 —— 补评门不通过(原 `Complete ✅ 2026-10-06(8a693fd)` 判定不成立;原件为补评件)。**CB-2/TB-5 已于 2026-10-15 S11-9 闭合**;余 6 条顺延 sprint-12
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 7h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-15
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
- [x] AC-37-29:脚本三案链 —— 链外同 D 病例**不消耗名额** + **一次性**。⚠️ 原「非 ScriptedChain 半边先落 / 联调半边 BLOCKED-BY-W-1」的形状**已作废**:W-1 会签 2026-10-15 结案 ⇒ 免责前提消失 ⇒ **全条兑现**(**S11-9 交付 2026-10-15**;`ChainEligible` / `ScriptedChain` 从全库零命中转为有实现 + 有测试)
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

> ⚠️ **`PatternRecognized` 写者延后 —— 正式登记(2026-10-15 用户裁定)**
> 写者未落,由 `Editor.Tools.Gates/WriterExistenceGate.cs:88` 登记为**具名豁免债**
> (原文:「37 同源检测写者未落 —— `PatternDetector` 现为纯函数,盐派生 + 冻结三元组写入归 37 实现轮(2026-10-10)」)。
> 裁定 = **接受延后并正式登记**,不复述为「已完成」:
> - **影响面**:补评判定 **CB-1 / TB-4(发射语义无生产载体)** 由此成立 ——
>   `Evaluate` 返回的是**前缀属性**(`PatternDetector.cs:108/:128`),**不表达「本 tick 新发射几条」**;
>   「已发过不再发」只由 `IsFired` 守卫,而 `IsFired` **同样零生产调用者**。
>   ⇒ AC-37-08/09 的「恰发一次 / 第 4 例不重发」在实现里**不由 `PatternDetector` 保证**,
>   由「调用方先问 `IsFired`」这一**未写、未测**的契约保证。
> - **证据面订正(AC-37-08 / AC-37-09)**:两条 AC 的**发射语义**半边**不再计入本 story 的已交付面**;
>   ⚠️ 但 **AC-37-09 的另一半「`MemberSet` 冻结于前 3 例」不受写者缺失影响** ——
>   它是 `PatternDetector` 自身输出的性质,已排补断言(见下)。
> - **本 story 自陈与延后的冲突(须如实登记)**:story §Acceptance Criteria / §Test Evidence /
>   §Out of Scope **原先均未登记该延后**(Out of Scope 只列 003/005/006 与 53),
>   而 Implementation Note 3(`:51`)明写「先按 `salted_key` 升序**再 Append**」—— 即 story 自认含写入步。
>   本次登记即消解该悬空。
> - ⚠️ **不因延后豁免的两项**:① **AC-37-27 定序判别力为零**(TB-1/CB-3,实现侧 `memberSet` 按
>   `Dictionary` 迭代序 + 夹具期望序 ≡ 插入序)② **AC-37-29 零覆盖**(CB-2/TB-5)—— 二者与写者无关,
>   其中 ② **已于 2026-10-15 S11-9 闭合**(见下方 🟢 块);① 仍归 sprint-12。

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
**Required evidence**: `unity/Assets/Tests/EditMode/CaseSystem/case_pattern_detection_test.cs` — must exist and pass
（⚠️ 原附加语「AC-37-29 的 ScriptedChain 联调半边 BLOCKED-BY-W-1 不记绿」**已于 2026-10-15 失效** —— W-1 会签结案 + S11-9 已交付,该免责前提不再存在;就地订正见下）
**Status**: [ ] In Review — 测试文件存在且 **30/30 绿**(2026-10-15 S11-9 后;原 23/23),但补评判定 **Test Evidence 门不通过**(见下方登记块)

> ⚠️ **2026-10-15 补评件登记(S11-2 后续批 · QA 核查)** —— 原评审原件缺失(承 `review-case-system-001-004-gap-audit-2026-10-15.md` §二:004 判定分级未能确证),按义务第 3 条**补做评审(评当下)**,不追认 `8a693fd` 的历史判定。
> **补评原件**:`production/qa/evidence/review-case-system-story-004-retro-2026-10-15.md`
> **判定**:代码面 **3 BLOCKING** + 测试面 **5 BLOCKING**;去重后核心 4 类 ——
> ① **AC-37-27 定序判别力为零**:实现 `memberSet` 按 `Dictionary` 迭代序拼装(`PatternDetector.cs:104`),而测试夹具的**期望序 ≡ 插入序**(代理逐位复刻 `SplitMix64` + 黄金自校验证得)⇒ **删掉 `:121-126` 的 Sort,断言仍绿**(可捕获性判定 = **不可捕获**);
> ② **AC-37-09/10 成员身份与冻结内容不可证伪**(只断 `MemberSet.Count`,`:105`/`:126` ⇒ 取「后 3 例」的变异同样绿);
> ③ **AC-37-29 零覆盖 + `ChainEligible`/`ScriptedChain` 全库 `.cs` 零命中**,而 W-1 已于 2026-10-15 会签结案 ⇒ **免责前提当日失效**,夹具今日即可写;
> ④ **发射语义无生产载体(借绿)**:`PatternRecognized` 写者未落(`WriterExistenceGate.cs:88` 具名豁免)、`IsFired` 零生产调用者,「不重发」由一份**未写、未测**的调用方契约保证。
> 正面:阈值 `>=` 边界成对夹住 ✅ · 去重算子 ✅ · **AC-37-34 是全文件唯一「断内容」的真判别用例** ✅ · 盐函数有全案级黄金夹具 ✅。
> **Test Evidence 门:不通过。** 修复归**新一轮**;状态**已裁定(2026-10-15,用户)转 `In Review`**。
>
> 🔓 **BLOCKED-BY-W-1 解除(2026-10-15)**:AC-37-29 的脚本链半边阻塞理由已消失 ——
> `case-system.md:610-627` 记 W-1 会签结案(裁① = 三案各挂 `DIS_DYSENTERY`,user + game-designer 双签,提交 `4565dc6`);
> 裁决明写「`AC-37-12` 原样不动(三案从此**不是**纯伤情案)」⇒ `CandidateSeq` 非空的机制路径已定。
> ⚠️ 但**谓词实现 `ChainEligible` 本身也未落**(story `:40` 自认「先落」的那半边同样零命中),故解除的是**夹具的免责前提**,不是交付本身。

> 🟢 **CB-2 / TB-5 已闭合 —— 2026-10-15 · sprint-11 S11-9**(证据原件:`production/qa/evidence/fix-s11-9-case-system-story-004-cb2-tb5-2026-10-15.md`)
>
> **交付**:`ScriptedChain` / `ChainEligible` 从**全库 `.cs` 零命中**转为**有实现 + 有测试**
> (GDD F-37.1b 逐字定义兑现;测数 **23 → 30**,新增 7 条夹具)。
>
> **改动**:
> ① **`ScriptedChain`(纯机制载体,不含任何具体 `patient_id`)** —— GDD F-37.1b 逐字
>   「具名角色 → 数字 `patient_id` 的绑定**仍属内容值**……**本 GDD 只登记机制与判定域,不落数值**」
>   ⇒ P0 无该数据 ⇒ 值由调用方 / ADR-014 烘焙**注入**。**这不是范围裁剪,是 GDD 的明文归属划分。**
> ② **`ChainEligible.IsEligible` / `TryChainOf`** —— 逐字兑现 `ChainEligible(D) := 病例 c 满足 c.Patient ∈ ScriptedChain(D)`。
> ③ **`Evaluate` 定义域收窄(逐 D 二选一)** —— 命中链的病种:`CandidateSeq(D)` 只留链成员
>   (链外病例**既不计数也不占名额**);未命中链的病种:维持 F-37.1 原算式。
>   GDD 逐字「成员判定域 = 显式指定,不是『全局首次三例』(旧算法的行为保留给**非脚本**的同源检测)」。
>   抽出 `ComputeCandidateSeq` / `SortBySaltedKey` 使新增入口共用**单一定序源**;原 4 参重载转发 `null` ⇒ 现行为逐位不变。
> ④ **`NextEmissions` + `PatternEmission`(发射决策层)** —— 见下方「两层语义」条。
>
> **⚠️ 一处语义厘清(须如实登记,非判定推翻)**:补评件 TB-4/CB-1 记「发射语义无生产载体」——
> 该观察**成立**,但指向应收正为「**缺一层**」,不是「`Evaluate` 有问题」:
> - `Evaluate` 返回**前缀属性**(达标后第 4/5/6 例仍 `Fired=true`)。**这本身不是缺陷** ——
>   AC-37-07/08 测的就是这一层,现有 `test_ac3709` 断言「第 4 例仍触发」**是对的**。
> - 缺的是「**本 tick 真正应发射几条**」(AC-37-09/29 的「不重发」属该层)⇒ 由 `NextEmissions` 补上。
> - 新增夹具 `test_ac3729_firesOnce_neverRefires` **末段保留对照断言**(`Evaluate` 仍返 `true`)
>   把两层边界**钉死**,防后人把两者重新合并。
>
> **⚠️ 写者仍未落 —— 不得误读**:`NextEmissions` **不触 `IEventSink` / 不 `Append` / 不发号 `Seq`**,
> 只到「该做什么」。写者按 **2026-10-15 用户裁定「接受延后并正式登记」** 仍缺(具名豁免 `WriterExistenceGate.cs:88`)。
> **本件不得被引为「写者已落」。** 同理 **CB-3 未触及**(`MemberSet` 仍扁平、`Dictionary` 迭代序仍非契约 ⇒ 归 sprint-12);
> `NextEmissions` 自带逐 D 归属是**本件所需**,不是对 CB-3 的修复。
>
> **两处用户裁定(2026-10-15)**:① 「一次性」的载体 = **新增纯函数决策入口** ——
> 若让夹具自己写 `if (!IsFired(...))`,那**正是 TB-4 所斥的「未写、未测的调用方契约」的复制** ⇒ 判据不可证伪;
> ② 链内**声明序不消费** —— GDD 同句「触发仍走 F-37.1 的**同一算式**……**不需要第二套公式**」,
> 而该算式按全序键定序 ⇒ 「有序」只作内容语义(叙事先后 甲→乙→丙),以夹具钉死。
> ⚠️ 该张力(GDD `ScriptedChain(D) := …【有序】…` vs「同一算式」)**未在 GDD 就地加注**(改设计件未授权)。
>
> **可证伪判据(证伪三步 · 双信号一致)**:
> | 状态 | PatternDetectionTest(30 测) |
> |---|---|
> | **反证① 停用定义域收窄** | 退出码 0 · XML **`failed=3`** — `chainQuota` / `chainComplete` / `chainAndNaturalCoexist` |
> | **反证② 停用「一次性」门** | 退出码 0 · XML **`failed=1`** — `firesOnce_neverRefires` |
> | **修复后** | 退出码 **0** · **30/30** |
>
> **零回归**:EditMode **`3344 / 3298 / 0 failed`**(较 S11-7 后 **+7**,恰为本轮新增;S11-7 后为 3337)·
> PlayMode **`186 / 186 / 0`**(**逐位等于 M2 基线**)· **无 asmdef 改动**(门 A / b2 门 / ADR-025 §① / ADR-029 全成立)。
>
> **⚠️ 证伪轮抓出本件自身的一条假绿夹具(已修正,故在此登记)**:夹具
> `test_ac3729_chainAndNaturalCoexist_perDisease` **首版**里 D=0 的**链外病例数 = 0**
> ⇒ 收窄与否结果相同 ⇒ **对「收窄」这一变异毫无判别力**(首轮反证①只红 2 条才暴露)。
> 已补入链外病例 20/21/22 修正,反证①**恰红 3 条**。**它若不经证伪轮,会以「绿」留下却什么都没测** ——
> 与补评件 **TA-6** 元规则同型。
>
> **未清**:① 写者仍缺(承裁定,见上);② CB-3 归 sprint-12;
> ③ 本 story 余 **6 条**顺延 sprint-12(TB-1 定序判别力 / TB-2 成员冻结 / TB-3 真空夹具 / CB-1 实现侧 / TA-6 元规则);
> ④ **GDD 两处待裁**:F-37.1b「有序」张力加注 · `:738`/`:718` 行号漂移(S11-10 登记);
> ⑤ **脚本链内容数据仍不存在**(`patient_id` 绑定归内容轮 + ADR-014 烘焙 —— 本条**不是缺口**,防「`ScriptedChain` 已落 = 三案链可跑」的误读);
> ⑥ GDD F-37.1b 注块的「先触发歧义」仍写「归 37 实现轮」—— 本件的逐 D 二选一**已消除其一半**(有链的 D 不再走自然检测),与 GDD 给出的裁法一致;注块是否更新**待裁**(改 GDD 未授权)。

---

## Dependencies

- Depends on: Story 001(salted_key 字段/哨兵)、Story 003(CaseClosed);SplitMix64 黄金夹具(ADR-012 既有);`WorldSeed` 存档头(7a)
- Unlocks: Story 006(重放);支柱四 P0 落点验证(待 playtest)

---

## Completion Notes

*(留空 — story 关闭时回填)*
