# Story 002: 病例列表排序纯函数(F-39.1)

> **Epic**: 脉案
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

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

- [ ] `SortKey(c) = (patient_id, Tick(CaseOpened), Seq(CaseOpened))` 三元组升序,纯函数、零落盘
- [ ] **全序性**(AC-39-02):对任意事件集合,`OrderedList` 输出为确定的全序;同病人多病例按开案 tick 分先后;同 tick 开案按 `Seq` 分先后
- [ ] **流前缀一致性**(AC-39-03):同一病例流取任意两个前缀 A ⊂ B,`OrderedList(A)` 是 `OrderedList(B)` 的前缀(新事件不重排旧项)—— 补算/重放不炸列
- [ ] 可序列化类型中**零**「display order / index」类 int 字段(反射断言)
- [ ] 串接组(同一病人的多病例)作为「线订成叠」呈现**单元**由本函数输出分组标记,组内顺序同样由三元组决定
- [ ] 病例流为空 ⇒ 返回空列表,不报错(与「未结 N 计数」无关 —— 计数是 Story 004 禁项)

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
  - Given: 事件流 E 与截断前缀 E' ⊂ E
  - When: 分别 `OrderedList`
  - Then: `OrderedList(E')` 是 `OrderedList(E)` 的前缀
  - Edge cases: E' = ∅ ⇒ 空列表是任意列表前缀

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

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/Casebook/casebook_sort_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(病例流只读投影的加载路径就位)
- Unlocks: Story 005(脉案页列表渲染消费本全序列)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*
