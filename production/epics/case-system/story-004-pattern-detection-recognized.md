# Story 004: F-37.1 同源检测与 PatternRecognized(含 F-37.1b 脚本链)

> **Epic**: 病例系统
> **Status**: Complete ✅ 2026-10-15 —— 补评门不通过 → sprint-11 就地修复 → **复评通过**
> (原件:`review-…-004-retro` + `review-…-004-postfix`;核心 4 类 + 4 条附加,去重后 **7 条 BLOCKING 全 CLOSED**)
> ⚠️ **原 `Complete ✅ 2026-10-06(8a693fd)` 判定确不成立**(补评件已证),本状态是**修复后重新评**的结果,**非追认**
> ⚠️ **5 条 ADVISORY 未动**(TA-1/2/5/7/8),均**非门阻塞**,已逐条登记;另 **1 项待裁**见 §Test Evidence
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
  - Then: 两条按 salted_key 升序,Seq=0,1;打乱 disease_set 构造序 重跑 → 流逐位同
  - Edge cases: salted_key 相等不可能(逐 D 哈希碰撞 → 以 ordinal 次级定序,登记测试观察)

---

**[Sprint 12 增量(S12-6,2026-10-15 · qa-plan-sprint-12)]** —— 补齐 AC-37-29 的**写者侧**:
上列 AC-1/AC-2 覆盖检测器侧判定;下列补 `PatternRecognitionWriter` 发射面(两者都绿才闭合 AC-37-29)。

- **S12-6-AC-a(同 tick 一次性批量跨阈)**: 发射数恒 1
  - Given: 同一 tick,N 例同 D 结案,N > PATTERN_THRESHOLD(含恰 threshold / threshold+1 / 远大于三档)
  - When: 写者 Append
  - Then: `PatternRecognized` **恰一条**(判据真源 = GDD AC-37-29 `:1143`「恰好触发一次」)
  - Falsifiability:删「一次性」门 ⇒ 发射数 1 → N ⇒ 红(承 S11-8 反证范式)
- **S12-6-AC-b(不重发)**: 同 tick 后续病例零发射
  - Given: 已触发后,同 tick 再结案更多同 D 病例
  - Then: 零新发射(AC-37-29「再让更大的同 D 病例结案 ⇒ 永不重发」)
- **S12-6-AC-c(链外不占名额)**: 脚本三案链指定三例时,链外同 D 病例不消耗名额
  - Given: ScriptedChain(D) 已显式指定三 patient_id,另有两个同 D 随机病例先结案
  - Then: MemberSet = 指定的那三例(随机病例不占名额)
  - Edge cases: 链外病例数 > 指定数仍不抢发
- **S12-6-AC-d(阈值守卫边界值 2)**: `patternThreshold` 边界两格
  - Given: `= 2` 与 `= 1` 各一
  - Then: `= 2` 放行,`= 1` 抛 `ArgumentOutOfRangeException`(`PatternRecognitionWriter.cs:127-128` 已建 `< 2` 闸)
  - Edge cases: 教训承 S11-17 —— 阈值守卫差一格即假绿,故边界值必须两格都测
- **分层边界(防重复劳动)**: 上列 AC-1/2 + S12-6-a/b/c 分别属**检测器侧 / 写者侧**,不得互相顶替;
  TA-7 已覆盖检测器侧(S11-9 交付),本批不重造。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/CaseSystem/case_pattern_detection_test.cs` — must exist and pass
（⚠️ 原附加语「AC-37-29 的 ScriptedChain 联调半边 BLOCKED-BY-W-1 不记绿」**已于 2026-10-15 失效** —— W-1 会签结案 + S11-9 已交付,该免责前提不再存在;就地订正见下）
**Status**: [x] Complete — 测试文件存在且 **36/36 绿**(2026-10-15 ADVISORY 清理批后;断言形态批 34;S11-9 后曾记 30,再前 23)。**Test Evidence 门:通过**(补评不通过 → 修复 → 复评通过,见下方登记块)。
> ⚠️ **本行原记「30/30」是陈旧值**(2026-10-15 postfix 评审核出 —— 同 story `:195` 已写「30 → 34」,**同一文件内自相矛盾**),已就地订正为 34;2026-10-15 **ADVISORY 清理批再 +2 → 36**(新增 `test_ac3734_sameTickFourthCase_membersAreFirstThreeInFullOrder` + `test_sortBySaltedKey_keyCollision_ordinalTieBreak`)。
> ⚠️ **下文各批登记块里的「30 测」/「30 → 34」/「34 测不变」是各批当时的快照,非现值** —— 现值以本行为准(同理,`case_pattern_detection_test.cs` 内各注释的行号是快照不是指针)。

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

> 🟢 **TB-1 / TB-2 / TB-3 / CB-3 / TA-6 + CB-1/TB-4 + ⑥ 全部闭合 —— 2026-10-15 · sprint-11 就地闭合**
> (证据原件:`production/qa/evidence/fix-s11-15-case-system-story-004-003-assertions-2026-10-15.md`)
>
> **改动面**:`unity/Assets/Sim/PatternDetector.cs`(**产品代码** —— 仅 CB-3 一处)+
> `unity/Assets/Tests/EditMode/CaseSystem/case_pattern_detection_test.cs`。
> **测试数 30 → 34**(+4 新增 · 0 删除 · 4 条重命名)。
> 重命名 4 条:`test_ac3709_fourthCase_noRefire` → `..._membersFrozenAtFirstThree` ·
> `test_ac3710_sameTickThreeCases_firesOnce` → `..._shuffledStreamOrder_membersInFullOrder` ·
> `test_ac3712_pureInjuryCase_notCounted` → `..._pureInjuryCases_neverCounted` ·
> `test_ac3727_sameTickMultipleDiseases_saltedKeyOrder` → `..._saltedKeyOrderNotInsertionOrder`。
> (S11-9 证据件点名的 5 个名字 `chainQuota` / `chainComplete` / `firesOnce_neverRefires` /
> `chainAndNaturalCoexist` / `declarationOrder` **全部未变**。)
>
> | ID | 缺陷 | 修复形态 | 变异探针(实测) |
> |---|---|---|---|
> | **TB-1** AC-37-27 定序判别力 | 夹具**期望序 ≡ 插入序** ⇒ 删掉排序断言仍绿 | 三处改用**插入序 ≠ 期望序**的夹具(如 `D ∈ {1,2}` 令提取器返 `[2,1]`)+ 所引 AC 前后句各一测 | M1 停用 `SortBySaltedKey` ⇒ **红** ✅ · M6 改按 ordinal 排 ⇒ **红** ✅ |
> | **TB-2** AC-37-09/10 成员身份与冻结 | 只断 `MemberSet.Count` ⇒「前 3」与「后 3」不可分 | 一律改断**逐位内容 + 次序**;AC-37-09 用逐 tick 递增令前 3 ≠ 后 3;AC-37-10 用**同 tick** 令流内序 ≠ 全序 | M2 成员取后 N 例 ⇒ **红** ✅ · M3 删 `candidates.Sort` ⇒ **红** ✅ · M5 删一次性门 ⇒ **红** ✅ |
> | **TB-3** AC-37-12 真空夹具 | 只有 2 例(< 阈值)⇒ `Fired==false` 由**数据不足**蕴含,与「伤情案是否进计数」无因果 | 改**恰达阈值数**的 3 例纯伤情案 + 「2 真病例 + 3 伤情案」混合夹具 | M4 空 `disease_set` 也计入 D0 ⇒ **红** ✅ |
> | **CB-3** 扁平 `memberSet` | 按 `Dictionary` 迭代序拼装(**违 AC-37-27**)+ 结构扁平 | **删除** `PatternDetectionResult.MemberSet` 字段与其拼装循环 —— 消费者调查确认**全仓零生产消费者**(生产侧唯一调用点 `PatternRecognitionWriter` 只读 `.Fired`);逐 D 归属从此**只有** `PatternEmission.MemberSet` 一条路径 | 产品改动,由全量测覆盖 |
> | **TA-6** 元规则 | 全文件「输入序 == 期望序」⇒ 定序类断言**系统性**丧失判别力 | 文件头写入**元规则**(定序类夹具须至少一处「输入序 ≠ 期望序」;判别力判据固定为「删掉对应实现 ⇒ 该测试转红」)+ 逐用例清单(满足 / 不适用的理由) | 结构性 |
> | **CB-1 / TB-4** 发射语义无生产载体 | `PatternRecognized` 写者未落 · `IsFired` 零生产调用者 | 用户裁定「**解冻写者**」⇒ `Sim/PatternRecognitionWriter.cs` 落地(9 测) | 见批 1 |
> | **⑥** F-37.1b 注块「先触发歧义」悬垂 | GDD 注块仍写「归 37 实现轮」 | **GDD 就地订正** —— 该裁法**确已实现**(`PatternDetector.ComputeCandidateSeq` 3b 步:命中链的病种候选只留链成员),注块更新为「已闭合、不再悬垂」 | 见 GDD |
>
> **变异探针口径(如实)**:6 个变异体(M1–M6)以 **dotnet 行为探针**复刻夹具并**直接调生产**
> `PatternDetector`,与基线逐行 diff —— **6/6 全部至少翻红一条断言**。
> ⚠️ 探针是**独立复刻 + 直接调生产**,**不是** UTF 执行;「34/34 绿」由本轮全量测确认。
> ⚠️ **额外补的一处(超出字面清单,同族同病)**:新增
> `test_ac3727_saltedOrderIsNotOrdinalOrder`(夹具 `D ∈ {0,1,5}`,期望序 `[5,0,1]` ≠ ordinal 序 `[0,1,5]`)
> —— 原 TB-1 修法只能证「有排序 vs 无排序」,**不能**证「排序**键**是 `salted_key` 而非 `ordinal`」。
>
> ⚠️ **CB-3 的历史登记就地订正**:`PatternDetector.cs` 原注释写「CB-3 **未触及**,归 sprint-12」——
> **已订正为「已修」**(文件 Header `:18-20` / `:37` 与 `:275` 注释同批更新)。
>
> ⚠️ **一处越权改动已回退(如实登记)**:本批执行轮曾自行把 `SortBySaltedKey` 由**偏序改全序**
> (主键 `salted_key`,相等时以 **ordinal 次级定序**),自陈引 GDD F-37.1 **逐字要求**。
> **引据经复核不成立**(GDD `:828-830` 只写「按 `salted_key` 的整数升序依次写入」,全文无 ordinal 次级键),
> 且该改动**无夹具可证伪**、**超出本批 20 条清单** ⇒ 按用户裁定 2026-10-15 **回退**
> (`PatternDetector.cs` 该函数与 HEAD **逐字一致**)。**回退前后双平台测数逐字不变**
> ⇒ 实证该改动在全部夹具上**行为不可观测**。
>
> ⚠️ **一个未处理的相邻 ADVISORY,如实登记(不冒充已决)**:`SortBySaltedKey` 用 `List.Sort`
> (**不稳定**排序),主键相等(64 位哈希碰撞,概率极低但非零)时次序**未定义** ⇒ 理论上与
> AC-37-27 后半句「换一个插入序重跑,事件流逐位相同」有张力。**是否需要次级键是设计决策**,
> 须用户 / ADR 裁 —— 本轮**不自行加**。归**待裁项**。
>
> **门验证**:EditMode `3387/3341/0/46` · PlayMode `186/186/0`(双平台退出码 0)。

> 🔵 **postfix 复评 —— 2026-10-15 · 本轮第二次新评审(评当下状态)**
> (报告原件:`production/qa/evidence/review-case-system-story-004-postfix-2026-10-15.md`)
>
> **门判定:通过。** 补评核心 4 类 + 4 条附加(**去重后 7 条 BLOCKING**)复评**全部 CLOSED**,
> 且多条经**独立实证** —— 复评以 python **逐位复刻 `SplitMix64.HashTagged`** 并用
> `golden_hash_v1_test.cs` 的黄金向量(`h(0xABCD,"case-salt",3)==0x9104567A4D5A819C`)自校通过,
> 独立算出 `salted(1) < salted(2)`,与夹具前提自检**逐位一致** ⇒ 「插入序 ≠ 期望序」成立。
> **回退洁净性**亦经复评核实:`SortBySaltedKey` 与 `HEAD` **逐字一致**,全仓 grep 失实引据 **零残留**。
>
> ⚠️ **复评登记的 5 条 ADVISORY(未动,均非门阻塞)**:`TA-1`(AC-37-11 夹具与 AC-37-08 逐字重复)·
> `TA-2`(`IsFired` 三用例把 `salted_key` 塞进 `SimEvent.Seq` 自造载体 —— ⚠️ 新增写者测试已用**真载体**
> **部分覆盖**)· `TA-5`(AC-37-14 负断言按**字段名**判)· `TA-7`(同 tick 边界 —— 已由新增
> `sameTickFourCases` **部分缓解**)· `TA-8`(**阈值「2/3 两档」缺失**;复评实测写者 ctor 只校验 `≥1`,
> Guardrail 的「≥2」**未强制**)。
>
> ⚠️ **复评新发现的一处待裁(已登记)**:`SortBySaltedKey` 用 `List.Sort`(**不稳定**),
> 64 位哈希碰撞时次序未定义 —— 与 AC-37-27 后半句有理论张力(**是否需次级键 = 设计决策**,归用户 / ADR)。
> 与本块上方同源登记,**同一项**,非两处。
>
> **本轮门验证(断言形态批后)**:EditMode `3386/3340/0/46` · PlayMode `186/186/0`
> (双平台退出码 0)。本 story 的测试件在断言形态批**未增删**(34 测不变)。

> 🟢 **ADVISORY 全清 —— 2026-10-15 · sprint-11 S11-17**
> (证据原件:`production/qa/evidence/fix-s11-17-case-system-advisories-2026-10-15.md`)
>
> **门判定:通过。** 复评登记的 5 条 ADVISORY(TA-1/2/5/7/8)+ 复评新发现的 2 条
> (元规则清单措辞 · 同 tick 批量 `anchor_case` 语义)**逐条处置**。**测试数 34 → 36**。
>
> | ID | 缺陷 | 处置 | 可捕获的变异 |
> |---|---|---|---|
> | **TA-1** | AC-37-11 夹具与 AC-37-08 **逐字重复**,且断言是其**真子集** ⇒ 零新增判别力 | 重写为**两腿**:① **接口面**(`Evaluate` 两重载形参表无病名 / 文本入口)② **行为面**(流内塞空 / 错名判断噪声,三例同 D 仍**恰触发一次**且成员集**逐位**) | 加 `string diseaseName` 形参 / 判断噪声被计入候选 ⇒ **红** |
> | **TA-2** | `IsFired` 三例把 `salted_key` 塞进 `SimEvent.Seq` 作**自造载体** | 改**真载体**:`MakePatternRecognized` 经 `PayloadEncoder` 编码 + `SaltedKeyOfReal` 经 `PayloadCodec` 解码;`Emit` 的盐提取器同批换真载体 | 载荷编解码错位 / `IsFired` 比较恒真 ⇒ **红** |
> | **TA-5** | AC-37-14 负断言按**字段名**判,防不住「换名携带」 | 改**闭集相等**(字段名集**恰等于**白名单 5 元) | 加任一字段(含 `DiseaseOrdinal` / `RawId` 等换名)⇒ **红** |
> | **TA-7** | AC-37-34「第 4 例**同 tick**」边界未覆盖 | 新增 `test_ac3734_sameTickFourthCase_membersAreFirstThreeInFullOrder`(同 tick 四例,插入序**逆**全序) | 成员取后 3 例 / 删 `candidates.Sort` ⇒ **红** |
> | **TA-8** | 阈值「≥ 2」Guardrail **未强制**(ctor 只拒 `< 1`) | `PatternRecognitionWriter.cs` 改 `< 2`(真源 = GDD `case-system.md:1004` 旋钮安全范围);测试补**阈值 1** 一格 | 守卫改回 `< 1` ⇒ **红** |
> | 元规则清单措辞 | `test_ac3709` 被列在「满足『输入序 ≠ 期望序』」组,而其夹具**输入序 == 期望序** | 文件头拆为**两组**(满足该纪律 / **满足另一条轴**),`test_ac3709` 移入后者 | 结构性 |
> | 同 tick 批量 `anchor_case` | 写者 9 测**均逐 tick 递增** ⇒「同 tick 跨阈值」语义未被直接证伪 | 由 TA-7 的新增夹具覆盖**检测器侧**该维;**写者侧**仍无同 tick 批量夹具 ⇒ **如实登记为残余**(低置信、非阻塞) | — |
>
> 🟢 **该残余已销项 —— 2026-10-15 · sprint-12 S12-6**(证据原件:`production/qa/evidence/fix-s12-6-writer-same-tick-batch-2026-10-15.md`)
>
> **交付**:写者侧补 **7 条**同 tick 夹具(测数 **36 → 43**,全量 EditMode `3394/3348/0/46` → `3401/3355/0/46`,**+7/+7 零回归**)。
> **判别力**:插入序刻意逆全序(测 1 插入 `[3,5,1,4,2]` vs 全序 `[1..5]`);链外病例 `patient_id` 取更小值 ⇒ 收窄失效即抢名额。
> **证伪轮(S12-4 新规)三处独立反证,均成立**:① 停 `IsFired` 一次性门 ⇒ 6 红(含本件 2 条)② 停 `candidates.Sort` ⇒ 4 红(含本件 1 条)③ 守卫 `<2`→`<3` ⇒ 1 红(本件)。三次「改→跑→还原」均字节相同、探针零残留。
> **产品码改动 = 零**。**AC-37-29 证据面自此两层齐备**(检测器侧 TA-7 + 写者侧本件)。
> ⚠️ **生产触发点仍未接线**(意图源尚不存在),承 `PatternRecognitionWriter.cs:41` 既有登记,归 37 接线轮 —— **本销项不改变该状态**。
>
> **⚠️ 次级键裁定更新(推翻本 story 上方「已回退」记录 —— 同一项,非两处)**
>
> 上方 `⚠️ 一处越权改动已回退` 段记:上一批自行加的 ordinal 次级键因三条被用户裁定**回退** ——
> **① 引据失实 · ② 无夹具可证伪 · ③ 超出清单**。本轮 **2026-10-15 用户明确裁定「加次级键」**:
>
> - **② 已消解(硬)** —— `SortBySaltedKey` 改**可注入键形** `(List<int>, Func<int, ulong> keyOf)`,
>   测试可喂**人为碰撞键**(`d => (ulong)(d % 2)`)⇒ 次级键**有了可证伪夹具**
>   (`test_sortBySaltedKey_keyCollision_ordinalTieBreak`:删次级键 ⇒ 等键组保持插入序(**降序**)⇒ **必红**)。
>   生产侧保留 `(List<int>, long worldSeed)` 私有重载 ⇒ 两个调用点**逐字未改**。
> - **① 仍未消解,如实登记** —— GDD `:828-830` **仍无** ordinal 次级键的明文要求(本轮**未改 GDD**;
>   改设计件需单独授权)。故本改动的定性是「**确定性加固,非 GDD 明文兑现**」:它在 **2⁶⁴ 键碰撞**
>   这一 **P0 不可构造**的场景下才可观测 ⇒ **不改变任何可达行为**(「回退前后双平台测数逐字不变」
>   已由上批实证,与本批「+2 全来自新增测」互相印证)。
> - **③** 本轮不再适用(ADVISORY 清理是用户裁定的独立批次,该改动**在清单内**)。
>
> ⇒ **不得**把本改动引为「GDD F-37.1 逐字要求 ordinal 次级键」—— **该引据仍失实**。
>
> **门验证**:EditMode **`3388/3342/0/46`** · PlayMode **`186/186/0`**(双平台退出码 0 · `error CS` **0**)。
> **零回归**:较断言形态批基线 `3386/3340/0/46` **+2**,恰为本批**新增 2 测**、**零删除**;
> PlayMode **逐位不变**。无 asmdef 改动(门 A / b2 门 / ADR-025 §① / ADR-029 全成立)。

---

## Dependencies

- Depends on: Story 001(salted_key 字段/哨兵)、Story 003(CaseClosed);SplitMix64 黄金夹具(ADR-012 既有);`WorldSeed` 存档头(7a)
- Unlocks: Story 006(重放);支柱四 P0 落点验证(待 playtest)

---

## Completion Notes

*(留空 — story 关闭时回填)*
