# Story 002: 病例列表排序纯函数(F-39.1)

> **Epic**: 脉案
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-10-15  <!-- 原 `2026-10-02` —— 2026-10-21 /dev-story Phase 2 逐件判定取 [A]（升级并按现行规则实施）；原文保留 -->
> **Last Updated**: 2026-10-21

## Context

**GDD**: `design/gdd/casebook.md`(规则二 病例列表组织 · F-39.1 `SortKey(c)` = `(patient_id, Tick(CaseOpened), Seq(CaseOpened))` 升序)
**Requirement**: TR-casebook-005(`SortKey` 读时派生零落盘,排序不写存档)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-008(主): 病例事件流 · ADR-010: 持久化 · ADR-024: Kind 单一登记真源
**ADR Decision Summary**: 病例流是独立于病史流的第二条逻辑流,跨流全序键 `(Tick, StreamPriority, Patient, Seq)`;39 的列表顺序 = **读时从 `CaseOpened` 事件派生**的纯函数,零落盘(落盘 = 第二真源,ADR-010 义务表已把该派生排除在存档之外);同 tick 并列由 `Seq` 打破 —— `Seq` 由主机在 `Append` 时发号(ADR-024 registry 具名 Kind)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯整数比较排序,BCL `Comparer`/`Array.Sort` 即可(比较器为全序 ⇒ 排序不稳定亦不影响结果);零引擎 API。

**Control Manifest Rules (this layer)**:
- Required: 排序键三元组全整数;读时计算,函数签名 `OrderedList(events) → case[]`(投影自病例流,不新增可变状态)
- Forbidden: 在可序列化类型里出现「显示顺序」int 字段(AC-39-03 反渗判据);按病种 / 日期 / 状态重排(那是 Story 004 的禁项,也是本函数的禁项)
- Guardrail: 该函数是**全序**——任意两病例可比、无并列残留(否则不同平台 `Sort` 稳定化差异会显形)

---

## Acceptance Criteria

*From GDD `design/gdd/casebook.md`, scoped to this story:*

- [x] `SortKey(c) = (patient_id, Tick(CaseOpened), Seq(CaseOpened))` 三元组升序,纯函数、零落盘
- [x] **全序性**(AC-39-02):对任意事件集合,`OrderedList` 输出为确定的全序;同病人多病例按开案 tick 分先后;同 tick 开案按 `Seq` 分先后
- [x] **流前缀一致性**(AC-39-03):同一病例流取任意两个前缀 A ⊂ B,`OrderedList(A)` 是 `OrderedList(B)` 的前缀(新事件不重排旧项)—— 补算/重放不炸列
- [x] 可序列化类型中**零**「display order / index」类 int 字段(反射断言)
- [x] 串接组(同一病人的多病例)作为「线订成叠」呈现**单元**由本函数输出分组标记,组内顺序同样由三元组决定
- [x] 病例流为空 ⇒ 返回空列表,不报错(与「未结 N 计数」无关 —— 计数是 Story 004 禁项)

> 🔁 **2026-10-21 就地加注 —— AC-39-03 补前提(原文不删,判定收窄)**
>
> 原文「取**任意**两个前缀 A ⊂ B」是**无条件**表述,**数学上不可满足**:`OrderedList` 是
> 全量重排,若追加项的三元组小于既有某键,它就会插到前面,`OrderedList(A)` 不再是
> `OrderedList(B)` 的前缀。反例已单独成测:
> `test_orderedList_appendedSmallerKey_relativeOrderKeptButNotPrefix`(`casebook_sort_test.cs`)。
>
> **现行判据(收窄后)**:AC-39-03 成立的前提 = **追加键三元组 ≥ 该流既有全部键**。
> 该前提由两件事共同保证,且**均不在本函数判据面内**:
> ① `IIdAuthority` 单调发号(ADR-006 Amendment B,计数器永不复位);
> ② 新案开案 tick 严格大于既有开案 tick(开案由 37 在主机侧发事件,ADR-005 唯一真源)。
>
> ⇒ 本函数只被要求:**给定满足前提的追加**,不重排旧项。**前提的守护归上游写者**
> (37 开案路径),不是本函数的义务。本 AC 勾选时按此口径判,**不得**记成无条件全绿。
>
> 依据:`production/qa/evidence/review-sprint17-batch1-2026-10-21.md` §三 B-1。

---

## Implementation Notes

*Derived from ADR-008 / ADR-010 Implementation Guidelines:*

1. 落点 `Gameplay.UI`(39 侧)—— 输入 = 边界层给已加载病例流的只读投影(39 不自己摸 `IEventSink`;`CasesOf` 具名接口 = TR-casebook-008 gap,本 story 以「事件列表参数」形态解耦,待 37 修订轮回写接口名,**不得记绿**)。
2. 比较器:`a.patient_id != b.patient_id ? a.pid - b.pid : (a.tick != b.tick ? Compare(a.tick,b.tick) : Compare(a.seq,b.seq))` —— 全 int64 比较,无减法溢出用 `Math.Sign` 风格写法(禁 `a-b` 直比较,防 int64 溢出)。
3. 每个病例的 `CaseOpened` 引用在投影时**一次性缓存于返回列表的不可达局部**(栈上/只读结构),不写入任何持久结构。
4. 「线订成叠」组标记 = 相邻同 `patient_id` 的连续段;组序仍由段首三元组决定,不重排。
5. ⚠️ **数值冻结**:每页病例数 / 分册纸页容量等呈现值归用户数值轮;本 story 只出全序列与分组。

---

## Out of Scope

- [Story 003]: 分册正文与改写痕投影(J(c,p) 折叠)
- [Story 004]: 按病种分组/排序/检索的**禁用**守卫(能力面扫描)
- [Story 005]: 列表的 UXML 呈现与焦点进入顺序
- 37 侧病例生命周期(`CaseOpened/CaseClosed` 的产出与判定)—— 39 只读

---

## QA Test Cases

**[Logic story — automated test specs]:**

- **AC-1**: 全序性总序断言(AC-39-02)
  - Given: 随机(固定夹具,禁运行时随机)生成 200 例事件集,含同 pid 同 tick 不同 Seq 的并列
  - When: `OrderedList` 两次独立调用 + 乱序输入各一次
  - Then: 四次输出逐位相同;任意相邻两项比较器 < 0
  - Edge cases: 单病例 / 两病例同 pid 同 tick(靠 Seq 分序)

- **AC-2**: 流前缀一致性(AC-39-03)
  - Given: 事件流 E 与截断前缀 E' ⊂ E,**且 E \ E' 的每个键三元组 ≥ E' 的全部键**
    (🔁 2026-10-21 补前提 —— 见上方 AC 列表后的加注;无条件版不可满足)
  - When: 分别 `OrderedList`
  - Then: `OrderedList(E')` 是 `OrderedList(E)` 的前缀
  - Edge cases: E' = ∅ ⇒ 空列表是任意列表前缀;**反例守卫** = 追加更小键时相对序保持但**非**前缀
    (`test_orderedList_appendedSmallerKey_relativeOrderKeptButNotPrefix`,如实登记,不记为失败)

- **AC-3**: 零落盘 / 零显示顺序字段
  - Given: 排序跑完后检查 39 全部可序列化类型
  - When: 反射扫描
  - Then: 无 `int order/index/sortKey` 类字段;存档字节流与未排序直接对比 = 不含顺序残留
  - Edge cases: 「旧」态标记是 8/37 的判断记录量,不是 39 的落盘字段

- **AC-4**: 串接组标记
  - Given: 病人 #3 有 3 例、#1 有 1 例、#2 有 2 例(交错开案)
  - When: `OrderedList`
  - Then: 组按段首三元组分块;组内升序;组输出带连续段标记供 42 装订
  - Edge cases: 同病人病例被其他病人病例在**开案序**上分隔时,「叠」按 GDD 规则二归组(以 patient_id 为主键聚叠)

---

> ### 🔁 QA plan 回填(2026-10-21 · `production/qa/qa-plan-sprint-17-2026-10-21.md`)
>
> 上列规格**原文保留**。**如实登记:本件的既有规格已相当完整,回填增量只有 1 条测试判据 + 2 条边界登记**
> —— 不padding,没新增的不写。**估计测试数 ~12**。
>
> | # | 增量 | 形态 |
> |---|---|---|
> | 1 | **`tick` 取 int64 近极值**(`MinValue` / `MaxValue`)时比较仍正确 | 测试判据 —— 直接钉住 `Implementation Notes` ② 「**禁 `a-b` 直比较**(防 int64 溢出)」;上列 AC-1 的 200 例夹具用常规值,**碰不到**这条 |
> | 2 | **端到端「加载后列表」不在本件 AC 面** | 边界登记 —— `casebook-001`(加载路径)现 `Status: Ready` **全未实现**;本件 `Implementation Notes` ① 明写以「事件列表参数」形态解耦 ⇒ 依赖为**名义**。**不得**因此扣绿,**也不得**声称已验证加载路径 |
> | 3 | **「未结 N 计数」不是本件判据** | 边界登记 —— 空流返回空列表(**AC 第 6 条**),但「未结 N」归 `casebook-004` 禁项;测试**不得**顺手断言计数,否则越界 |
>
> ⚠️ 上列 AC-4 的「同病人病例被其他病人分隔」在本件判定下**不可能出现**(按 `patient_id` 为主键聚叠后无分隔)——
> 该 Edge case 描述的是**输入侧**的交错,测试须先按聚叠语义构造,再断言分组。

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/Casebook/casebook_sort_test.cs` — must exist and pass
**Status**: ~~[ ] Not yet created~~ **[x] Created & passing — 16 个测试函数**
> 🔁 **2026-10-21 订正(QA 签核轮)**:本行为**建 story 时的占位**,与事实相反 ——
> 文件早已存在且全绿,本文件 §Completion Notes(`:172`)自己也记了「16 测」,**同一文件内自相矛盾**。
> 实测:`grep -c '^\s*\[Test\]' unity/Assets/Tests/EditMode/Casebook/casebook_sort_test.cs` = **16**。
> 原文加删除线保留(承「原文不删」)。
> 溯源:`2026-10-21 16:51 CST` · 批次 `sprint-17 QA 签核轮全量 EditMode` · `e0ad379`+工作树 · `4350/4314/0/36`。

---

## Dependencies

- Depends on: Story 001(病例流只读投影的加载路径就位)
- Unlocks: Story 005(脉案页列表渲染消费本全序列)

---

## Completion Notes
**Completed**: 2026-10-21
**Criteria**: 6/6 passing(AC 第 3 条按**收窄后口径**,见下)
**Deviations**:
- 🔁 **AC 第 3 条已收窄**(2026-10-21 用户裁定):原「任意两个前缀」数学上不可满足 —— 实现是全量重排。
  现行判据加前提「追加键三元组 ≥ 该流既有全部键」;前提由 `IIdAuthority` 单调发号(ADR-006 Amendment B)
  + 新案开案 tick 更大共同保证,**归上游写者(37 开案路径)**,不在本函数判据面内。
  反例 `test_orderedList_appendedSmallerKey_relativeOrderKeptButNotPrefix` 保留且**不记为失败**。
- ADVISORY — 零 display-order 的反射断言过滤掉自动属性背衬 ⇒ 属性式 `DisplayOrder` 可逃逸;
  词表有 `sortindex` 但漏 `sortkey`;类型限 `int`/`long`,`byte`/`short` 逃逸(评审 C4)。
- ADVISORY — `Casebook.Tests.asmdef` 引用 `Sim` 与 `Gameplay.Presentation` 但零使用,且缺 `autoReferenced: false`(评审 C9)。
**Test Evidence**: Logic — `unity/Assets/Tests/EditMode/Casebook/casebook_sort_test.cs`(16 测)
  + 证伪 F3(`CompareTo` → `(int)(a-b)` ⇒ 2 条红)
**Code Review**: Complete — 同上原件(0 BLOCKING)
