# Story 001: 病例流 Kind 登记与病例身份

> **Epic**: 病例系统
> **Status**: Complete ✅ 2026-10-15 —— 补评门不通过 → sprint-11 就地修复 → **复评 + 窄审双通过**
> (原件三件:`review-…-001-retro` · `review-…-001-postfix` · `review-…-001-b1-postfix`;4 条 BLOCKING 全闭)
> ⚠️ **原 `Complete ✅ 2026-10-06(e64efad)` 判定确不成立**(补评件已证),本状态是**修复后重新评**的结果,**非追认**
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-15
## Context

**GDD**: `design/gdd/case-system.md`(F-37.2 身份/全序 · 承重账五 Kind · §规则一 记录≠实体)
**Requirement**: TR-case-001(case_id 三元组可从事件流重构) · TR-case-002(病例事件落独立第二条逻辑流) · TR-case-004(跨流全序键) · TR-case-005/006/008/009(载荷契约与哨兵)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-008(主): 病例事件流;ADR-024: Kind 单一登记真源
**ADR Decision Summary**: 五 Kind 全部落**病例流**:`CaseOpened {patient_id, disease_snapshot}` / `CaseClosed {disease_set, treated}` / `PatternRecognized {PatientId.None, anchor_case, salted_key, member_set}` / `JudgmentRecorded` / `JudgmentRevised`;`entities.yaml` 为唯一真源(stream/author/payload_schema 三必填,载荷字段只允许整数域),kindgen 生成 `StreamRouting.g.cs` + 断言 A1–A5;`case_id = (Tick,Patient,Seq)`(Seq 仅同 `(Tick,Patient)` 内单调,单独取会撞号);**`opened_tick` 从载荷删除**(与 `SimEvent.Tick` 是同一事实的别名 —— ADR-006「少一份可失步的状态」);跨流全序 `(Tick, StreamPriority, Patient, Seq)`,病史流 < 病例流;世界级事件 `PatientId.None = -1` 不污染 `max(patient_id)` 高水位(ADR-007 §四,高水位扫三流并集)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 数据结构与路由为纯 C#;不触 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 五 Kind 在 `design/registry/entities.yaml` 具名(stream: case);三元组判等 = 全序无平局;`DiseaseId`/`DiseaseIdSet` = 版本化整数 ordinal(**禁 Fix** —— FixSet 之错已裁)
- Forbidden: 载荷携带 `opened_tick`/浮点/字符串判定字段(freehand_text 例外已登记 ADR-006 Amendment A,纯呈现语义);37 侧自造第七抽象点或新计数器
- Guardrail: 病例流不参与终态物理折叠(ADR-008 §六);kindgen A5 双向差集归零才算过

---

## Acceptance Criteria

*From GDD `design/gdd/case-system.md`, scoped to this story:*

- [ ] AC-37-01:病例流任意 `CaseOpened` 的 `(Tick,Patient,Seq)` 全流唯一;任何被引用的 `case_id` 等于某条 CaseOpened 三元组
- [ ] AC-37-02:两病人同 tick 各立案 ⇒ 两个 case_id 不同(两人各有 Seq=0,Patient 区分,不撞号)
- [ ] kindgen 断言批:五 Kind 的 A1 唯一流别 / A2 载荷 ∈ 整数域 / A3 无重名 / A4 author=37 / A5 双向差集 全过
- [ ] 载荷形状断言:`CaseOpened`/`CaseClosed` 载荷中**无** `opened_tick` 字段(别名纪律);`PatternRecognized` 载荷**含** `salted_key`(AC-37-14 的载体半边在此建)
- [ ] 跨流全序复验:构造病史/病例交错流,`(Tick,StreamPriority,Patient,Seq)` 排序确定且无平局(哨兵 -1 行可排序不崩)
- [ ] 高水位纯净:含哨兵行的流上 `max(patient_id)` 重构不受 -1 污染(ADR-007 §四 消费面)

---

## Implementation Notes

*Derived from ADR-008/024 Implementation Guidelines:*

1. `entities.yaml` 增五行后跑 kindgen;`StreamRouting.g.cs` 为生成物不手改(承 ADR-009 §三 降级为路由注记的口径)。
2. `CaseId` 结构体 = readonly 三元组 + 比较器(比较序显式:Tick→Patient→Seq);全库禁以 `Seq` 单独为键。
3. ordinal 类型(`DiseaseId`/`LexiconId`)编码按 int/u16 走 ADR-006 §五 自定义编码器;`FixParse` 解析集**不含**它们(D-21-17 同类纪律)。
4. 宿主迁移场景的病例流序列化归 7a(ADR-010 义务表),本 story 只交付事件形状。
5. `Judgment` 结构(`lexicon_id/confidence/freehand_text`)的形状在此钉,判定语义与词表门在 story-005。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 002]: 立案触发与唯一性
- [Story 003]: 结案前置谓词
- [Story 004]: 同源检测与 salted_key 计算
- [Story 005]: DTO 守密与词表烘焙门
- [Story 006]: 重放/持久化联调与跨系统边界

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(三元组唯一)**: 撞号反例
  - Given: 同 (Tick,Patient) 两条 CaseOpened(Seq 0/1)+ 另一 Patient 同 Tick(Seq 0)
  - When: 全流建 case_id 集
  - Then: 三者两两不同;任何 Seq 单独取值会撞(断言测试故意展示)
  - Edge cases: 跨主机迁移重放后同流同键(纯函数)
- **AC-2(kindgen 门)**: 登记完整性
  - Given: entities.yaml 五 Case Kind 行
  - When: 运行生成器
  - Then: A1–A5 全绿;故意删 author ⇒ A4 构建失败
  - Edge cases: 载荷塞 float 字段 ⇒ A2 拒(白名单非 grep)
- **AC-3(别名纪律)**: 载荷字段扫描
  - Given: CaseOpenedPayload/CaseClosedPayload 类型
  - When: 反射扫描
  - Then: 无 opened_tick 成员;tick 只在 SimEvent.Tick 一处
  - Edge cases: 读取便捷属性 `CaseOpened.Tick` 合法(别名非存储)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs` — must exist and pass
**Status**: [x] Complete — 测试文件存在且 **9 测全绿**(2026-10-15 断言形态批后;原记 13,其中 4 条 kindgen 复刻已随 B-4 载体唯一化删除)。**Test Evidence 门:通过**(补评不通过 → 修复 → 复评 + 窄审双通过,见下方登记块)。
> **2026-10-15 更新(两轮)**:S11-11 闭合 **B-3**(全文件去号,含头/注释/方法名共 25 处);
> 断言形态批闭合 **B-2**(kindgen A2 空跑)· **B-4**(kindgen 载体借绿)· **B-1 测试半边**(高水位测试改打生产载体)。
> ⇒ 4 条 BLOCKING **全部已闭**;门判定见下方 **postfix 复评**。
> ⚠️ **本行原记「13/13 绿」是陈旧值**(postfix 评审核出)—— B-4 删除 4 条测试后实为 9,本行已就地订正。
>
> 🚧 **未测半边显式登记 —— `BLOCKED-BY-7a/45`(2026-10-15 · S12-7')**:
> **AC-37-06 的完整命题**(「含已结案病例的存档,主机迁移并重放 ⇒ 开/结状态与 `FiredSet`/`MemberSet` 逐位一致」)
> **前置 = 7a 持久化 + 45 主机迁移,两者 P0 均未实现** ⇒ **该半边当前无载体,记 `BLOCKED-BY-7a/45`**。
> **本件（story-001）只覆盖收窄后的可测半边** —— 生产 `IdAuthority.ReconstructPatientHighWater`
> 由**事件流**重构病人 id 高水位(纯流函数,不依赖 7a/45),三态用例见
> `case_identity_routing_test.cs` 的 `test_highWaterMark_sentinelMinusOne_noPollution`
> (① 真 id 累加 ② 全哨兵流 `{-1,-1}` 不回退 ③ 号空间耗尽硬失败 —— **以方法名为准**;
> ⚠️ 行号在测试件内是快照非指针,见 `story-004` 同款纪律)。
> ⚠️ **禁借绿**:上列三态**不构成**「迁移重放逐位一致」的证据;该半边的 `status` 维持 **未覆盖**,
> 待迁移链可测后**重评**(依据 = `design/gdd/case-system.md:1121` 的四步收窄裁定,2026-10-15 用户授权)。

> ⚠️ **2026-10-15 补评件登记(S11-2 后续批 · QA 核查)** —— 原评审原件缺失(承 `production/qa/evidence/review-case-system-001-004-gap-audit-2026-10-15.md` §二:001 确证产出过 BLOCKING 级判定),按义务第 3 条**补做评审(评当下)**,不追认 `e64efad` 的历史判定。
> **补评原件**:`production/qa/evidence/review-case-system-story-001-retro-2026-10-15.md`
> **判定**:代码面 **无 BLOCKING**(无 ADR/GDD 冲突 · 注册链条严整 · 产品代码无须返工);测试面 **4 BLOCKING** —— **story 自定判据**(高水位纯净 —— ⚠️ **无 GDD AC 号**,见文件头「编号纪律」;本行初稿误记为「AC-37-30/06」,2026-10-15 postfix 评审核出并就地订正)断言形态错误(`ReconstructPatientHighWater` 生产载体**零调用** · 自造 `Where().Select().Max()` **恒真**)· AC-37-03 A2 **空跑** · kindgen 载体**借绿** · 测试头 **AC 号一号两义**(与 GDD 权威编号冲突)。
> **Test Evidence 门:不通过。** 修复归**新一轮**;状态**已裁定(2026-10-15,用户)转 `In Review`**。
>
> 📌 **修复排期(2026-10-15 用户裁定「并入 sprint-11 · 必须项优先」)**:
> - **本轮取**:B-3(测试头自填 AC 号一号两义,零成本)= **S11-11**;
> - **顺延 sprint-12**(须带状态+归属,禁借绿):**B-1**(AC-37-06 判据**算术上不可证伪** ⇒ 须**退设计重述**,仅加大输入无效)·
>   **B-2**(kindgen A2 空跑)· **B-4**(kindgen 载体借绿 —— 「删除 kindgen,13 条测试照样全绿」)。
>   B-2/B-4 是同一缺口两半,归 kindgen 侧(「CI 跑 `kindgen --check`」为 AC-37-03 的唯一载体)。
>
> 🔴 **本行已过期 —— 2026-10-15 · sprint-12 S12-7' 订正(原文不删留证)**:
> 上列三项**当日即在 sprint-11 就地闭合**(S11-12 / 断言形态批,用户裁定「不带入下个 sprint」):
> - **B-1** 的「须退设计重述」**前提已消解** —— 设计侧收窄裁定已由 **`GDD:1121` 于 2026-10-15 裁毕**
>   (完整命题前置 = 7a + 45,当前不可测;可测半边 = 生产 `ReconstructPatientHighWater`;未测半边记 `BLOCKED-BY-7a/45`),
>   测试侧亦已改打生产载体。⇒ **「重述设计」这一半无事可做**,真残余 = **文书回填**(本注即其兑现)。
> - **B-2 / B-4** 已由断言形态批闭合(见下方 B-1/B-2/B-4 闭合块)。
> ⇒ **sprint-12 的 S12-7' 只做回填,不重述设计**(若按旧前提去「退设计重述」= 推翻已 Accepted 的收窄裁定 = 范围外破坏)。

> 🟢 **B-3 已闭合 —— 2026-10-15 · sprint-11 S11-11**(证据原件:`production/qa/evidence/fix-s11-11-case-system-story-001-b3-2026-10-15.md`)
>
> **改动**(`case_identity_routing_test.cs`,**纯测试件,零产品代码**):
> ① 测试头去号并**分组**(有号者归 GDD / 无号者自成「story 自定判据」一组);
> ② 文件体 **14 处**注释标去号,改 `[story 判据] <bullet 名>` 前缀;
> ③ **方法名 10 处**去号(按 bullet 归组,如 `test_ac3703_a1_…` → `test_kindgenA1_…`)。
> **保留**:`AC-37-01`/`AC-37-02`(与 GDD 一致)与 `AC-37-14`(GDD `:1110`,本文件是其载体半边)。
>
> ⚠️ **范围订正(已报用户并获批「全文件去号」)**:retro 只点名**测试头 `:5-8`**,实测误编号
> **有三层**(头 4 + 体注释 14 + **方法名 10**,共 **25 处**)。**只改头等于没改** ——
> 按 AC 号的覆盖矩阵会扫**文件任意位置**(含方法名),重复计数照旧。
>
> **可证伪判据**(本 AC = 「不再重复计数」,是覆盖声明属性 ⇒ 判据为 grep 而非跑测):
> 同号前缀下的方法数 —— `test_ac3703` **7 → 2** · `test_ac3704` **4 → 2** ·
> `test_ac3705` **5 → 3** · `test_ac3706` **1 → 0**。修复前 `test_ac3703` 下 **7 个方法分属两种
> 互不相干的语义**(本文件 5 个 = kindgen 断言批;close 文件 2 个 = 幂等拒收)⇒ retro 的
> 「重复计数」逐字成立。**反向**:把任一处号改回,对应行即时回升。
>
> **验证**:`13/13 绿 · 退出码 0 · error CS = 0`(**测数 13 → 13,改名未增未减**);
> 全仓 grep 确认被改名方法**零外部引用**;`git diff --name-only` 仅本测试件。
>
> **附带观察(如实登记,非本件缺陷)**:修复后 `test_ac3706` 方法名命中 = 0,因 AC-37-06 的真实主人
> `case_replay_persistence_test.cs` **不在方法名里编 AC 号** ⇒ 暴露的是**命名惯例覆盖不均**;
> 修复前那 1 个命中恰是**错的那个**。是否统一惯例归 **sprint-12 断言形态批**。

> 🟢 **B-1 / B-2 / B-4 已闭合 —— 2026-10-15 · sprint-11 断言形态批就地闭合**
> (证据原件:`production/qa/evidence/fix-s11-12-case-system-story-001-b2-b4-2026-10-15.md`)
>
> **B-2(kindgen A2 空跑)** —— 根因两层:① 断言体挂在内层
> `for m in re.finditer(r"[Ff]loat|[Dd]ouble", ps)`,而 `ps` 是 registry **散文**。
> 实测 35 支 Kind 全库命中仅 4 次,**4/4 全落「无 / 禁」否定语境被豁免**,case 流命中 **0**
> ⇒ **内层循环体执行 0 次,结构上不可证伪**。
> **修复**:A2 改**严格形** —— 扫 `Sim.Contracts` 全树的载荷 struct **字段类型**白名单
> (8 种整数宽度 + bool),原散文扫描降级为 `A2b`;并加**非空跑守卫**(扫得 0 个字段 = FAIL)。
> **实测**:`python3 tools/kindgen/kindgen.py --check` →
> `A1–A5 全过 · A2 严格形:39 个载荷 struct / 158 个字段类型受判(非空跑)`,**EXIT=0**;
> 断言体**结构性**(`stats['fields']` 在扫描循环体内累加,非打印硬编码 —— 已独立复核)。
> **变异矩阵 7 种**(int→float / bool→string / 未登记 struct 内含 double / 未登记类型 /
> 改 struct 名 / 抽空字段 / registry 散文注入非否定 float)**全部 EXIT=1**。
> ⚠️ 该矩阵为**沙箱一次性验证,未固化为 CI 步骤**;CI 亦**未在真 GitHub runner 上跑过**。
>
> **B-4(测试从不运行 kindgen)** —— `.github/workflows/tests.yml` 新增 job **`kindgen-assertions`**:
> 跑 `--check`(非零即红)+ 重生成后 `git diff --quiet` 产物一致性校验。
> **载体唯一化(本轮回刷)**:`case_identity_routing_test.cs` 内**手搓 `entities.yaml` 解析器**
> (4 个私有静态方法)与其唯一的 4 条消费者测试(A2–A5 的**测试内复刻**)**已删除**
> ⇒ **AC-37-03 的载体仅剩 CI**。**测试数 13 → 9**
> (`git diff --stat` = 11 insertions / 191 deletions;被删方法全仓零悬空引用 —— 已复核)。
> **保留** `test_kindgenA1_fiveCaseKinds_routeToCaseStream`(`:152`):它打的是**生成物**
> `StreamRouting.Of(...)`(生产码),**不是** kindgen 扫描的复刻 —— CI 的「重生成 + diff」
> 只证明产物**忠于生成器**,证明不了**路由正确** ⇒ 两者**互补而非重复**。
>
> **B-1(AC-37-06 判据算术上不可证伪)** —— **两半**:
> **(a) 设计侧**(S11-12 已做,按用户授权**收窄 GDD 文本**):`case-system.md` AC-37-06 行内加注
> (原文一字保留)—— 真实前置 = 7a + 45,**当前不可测**;本轮可测半边 =
> `ReconstructPatientHighWater` 从事件流重构高水位;未测半边登记 `BLOCKED-BY-7a/45`。
> **(b) 测试侧**(2026-10-15 断言形态批**补做**,postfix 评审核出 (a) 只做了半边):
> `case_identity_routing_test.cs` 的 `test_highWaterMark_sentinelMinusOne_noPollution`
> **原稿逐字是**测试内自造 `events.Where(...).Select(...).Max()` —— 生产
> `ReconstructPatientHighWater` 在 story-001 面内**零调用**(真实调用点原只在 21a 的
> `id_authority.cs`)。**已改写为打生产载体**。
> ⚠️ **判别力如实计数(2026-10-15 第三次窄审订正)** —— 本稿 **只有 ① 与 ③ 两处**可被
> **单行**变异打红,**不是三处**(本行初稿误记「三处可证伪断言」,已被窄审实测推翻):
> ① 真 id 累加(删 `id+1 > candidate` 累加 ⇒ 红;哨兵序已前置 ⇒ 把 `id<0 continue` 改 `break`
> 也 ⇒ 红)③ 溢出硬失败(删 `OverflowException` ⇒ 红;⚠️ 隐性前提 = 算术非受检,实测成立)。
> **② 全哨兵不回退是结构性保留、无单行捕获面** —— 「只升不降」的真正来源是 `candidate` 的初值
> `= _nextPatient` 与 `if (id+1 > candidate)` 的**取 max 形式**,**不是**末尾
> `if (candidate > _nextPatient)` 那道守卫(它**行为冗余**)。⇒ 同删 `id<0 continue` 与该守卫,
> `candidate` 仍 ≥ 初值,**② 仍绿**(窄审矩阵 M4 实测)。只有整函数重写才会红。
> ⚠️ **另一处不可证伪面**:「哨兵不污染 max」**单独**在算术上不可证伪(`-1` 是下界)。
> 以上均逐字写进测试 docstring,**不冒充判别力**。
>
> ⚠️ **一处「自述 ≠ 代码」须记(本件自身)**:GDD `:1121` ② 写「已由 story-001 的测试载体承接」,
> 而该句**写出时测试尚未改** —— 是**先写声明、后补代码**。postfix 复评据此判「GDD 承接声明与代码不符」。
> **本批补上代码后该句才成立**。此处留痕,不作「一直成立」的追溯粉饰。
>
> ⚠️ **同型号复刻在他处存在,本件未动(登记为系统性观察)**:
> `unity/Assets/Tests/EditMode/Combat/combat_actions_schema_test.cs:121`
> `test_kindgen_registeredKinds` 是同型「测试内反射复查 registry 登记面」,对象是 Combat 3 支 Kind、归系统 25。
>
> ⚠️ **上一条「归 sprint-12 断言形态批」的惯例统一**:该批已**就地闭合**,不复存在 ⇒ 该观察项**悬空**,
> 归**命名惯例**的独立轮次(非本 story 的阻塞项)。
>
> **门验证**:EditMode `3387/3341/0/46` · PlayMode `186/186/0`(双平台退出码 0,`error CS` = 0)。

> 🔵 **postfix 复评 —— 2026-10-15 · 本轮第二次新评审(评当下状态)**
> (报告原件:`production/qa/evidence/review-case-system-story-001-postfix-2026-10-15.md`)
>
> **门判定:不通过**(B-2 / B-3 / B-4 **已闭且经独立实证**;**B-1 只闭一半** —— 残余唯一 =
> 高水位那条测试仍自造 `Max()`)。**该残余已于本批补上**(见上方 B-1(b) 块)⇒ 四条 BLOCKING **全部闭合**。
>
> **B-1 测试半边的收口实证**(本批新增):
> | 状态 | `test_highWaterMark_sentinelMinusOne_noPollution` |
> |---|---|
> | 改前(自造 `Max()`) | 生产函数**零调用** ⇒ 断言在**任何**实现下为真 |
> | 改后(打生产载体) | **两处**单行可捕获(① 累加 / ③ 溢出);② 结构性、无单行捕获面 |
> ⚠️ 上表初稿写「三处可证伪断言;复合删两守卫 ⇒ 红」—— **经窄审实测为误**,已订正(见 B-1(b) 块)。
>
> 🔵 **第三次窄口径评审 —— 2026-10-15**(报告原件:
> `production/qa/evidence/review-case-system-story-001-b1-postfix-2026-10-15.md`)
> **门判定:通过**(判定边界 = **只覆盖「B-1 测试半边已闭」**;B-2/B-3/B-4 前轮已 CLOSED,该件不复核)。
> 复核方式:三处调用点逐行核对 + `/tmp` 副本上的**变异矩阵**(算术转录复算)+ 独立证明
> 「删 `id<0 continue` 是**行为等价**的」(由 `candidate ≥ 0` 恒成立推出)。
>
> ⚠️ **窄审抓到本件一处「自述 ≠ 代码」并已订正(方向朝上 = 虚报判别力)**:见上表与 B-1(b) 块。
> 这正是本 story 一路在修的同一病灶 —— **在修它的时候又犯了一次**。留痕不删。
>
> ⚠️ **窄审登记的效力边界(如实)**:变异矩阵是**算术转录复算**(证明判据算术,**不**证明
> Unity 编译产物行为);XML **不含源码哈希** ⇒ **不能**证明该次运行确已重新编译本文件;
> 全部套件 `asserts="0"`(UTF 已知现象)⇒ 只能据 `result="Passed"` 判绿。
>
> ⚠️ **复评登记的另两项未闭(如实,均非门阻塞)**:
> ① **CI job 从未在真 runner 跑过**(修复件已自陈,复评复核属实)—— 「失败即红」是**设计判据**,
> 非**已观测行为**;② **变异矩阵无常驻执行体**(A2 判别力由复评**独立重做其中 2 个变异**背书,
> 但「持续受检」无载体)。两者均**不得**读成「已持续守护」。
>
> ⚠️ **复评留的标准问题(本件不裁)**:「CI job(纯 python)能否算 story 的 Test Evidence 载体」——
> 该判据是补评件自己开的处方,复评按处方判其成立,但**标准本身归用户 / 流程裁**。
>
> **本轮门验证(断言形态批后)**:EditMode `3386/3340/0/46` · PlayMode `186/186/0`
> (双平台退出码 0,`error CS` = 0)。⚠️ 较 S11-12 后 `3387/3341` **−1** —— 构成见 story-003 块
> (删 2 条恒真 + 增 1 条真装配;本文件的改写为 1 换 1,计数不变)。

---

## Dependencies

- Depends on: kindgen(ADR-024 既有件);`IEventSink`/`SimEvent` 抽象点(ADR-005 既有)
- Unlocks: Story 002/003/004(全部写流故事的形状前提)、Story 006(序列化联调)

---

## Completion Notes

*(留空 — story 关闭时回填)*
