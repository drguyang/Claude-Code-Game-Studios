# Story 005: 守密纪律 —— DTO 守卫 · 词表烘焙 · 零奖励断言

> **Epic**: 病例系统
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28
## Context

**GDD**: `design/gdd/case-system.md`(规则九 守密 · 第六泄漏面 · F-37.4 词表 · 规则十 不拥有清单 · AC-37-15/24/35 批)
**Requirement**: TR-case-015(disease_id 不进呈现 DTO) · TR-case-024(排序键枚举禁自书病名) · TR-case-035(词表烘焙与非一一对应) · TR-case-020(规则十:不拥有清单,原 gap —— 本 story 兑现断言批) · TR-case-031/032 的守密半边
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-013(主): PresentationDtoGuard;ADR-014: 词表烘焙;ADR-006: 整数计数移出 Fix 解析集
**ADR Decision Summary**: 保密语义 = **player 不可见**,非 client 不可知(ADR-008 收窄);`disease_id` 类型层面不得进 39/42/48 的 DTO —— 机制 = `PresentationDtoGuard` **递归**反射扫描(AC-37-15 是 37 全 36 条 AC 中此铁律的**唯一落点**;判据是反射断言非 grep);第六泄漏面 = 玩家自书病名的**分组/词表一一对应**会把病种身份从侧信道漏出 ⇒ 判定枚举排序键禁 disease_id/自书文本(AC-37-24),`case_judgment_lexicon` 烘焙期做**非一一对应校验**(词表项与病种不得构成可反推的双射,ordinal append-only 基线门承 ADR-014);`Judgment.freehand_text` 纯呈现永不进判定(引用扫描);**P0 零数值奖励** —— 37 不发 potency/解锁/成长,反射断言「无任何写向奖励通道的代码路径」(AC-37-32);37 不拥有清单(规则十):不拥有病情/处置/后果/词表语义定义权,只订阅。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: PresentationDtoGuard 扫描对象是 UI 程序集(`Gameplay.UI`)的 DTO 类型,须 EditMode 反射测试;词表烘焙走 ADR-014 两阶段工具链(阶段 2 校验器扩展非一一对应项)。

**Control Manifest Rules (this layer)**:
- Required: DTO 闭集点名 {39 脉案, 42 呈现元件, 48 教学} 全部纳入守卫扫描根;lexicon ordinal = 整数计数(D-21-17 同类,禁 Fix);quill_tick 进判定记录 DTO(只读呈现量,TR-case-034)
- Forbidden: 以 grep 替代反射断言;把 freehand_text 读进任何判定/分组/排序;词表项删改后复用旧 ordinal(append-only 违例 = 构建失败);37 内定义后果或处置内容
- Guardrail: 负存在断言按「闭集点名 + 载体是否已建」执行 —— 载体(39/42/48 DTO 类)未建齐时记 **NOT-RUN** 禁空集绿(禁借绿铁律)

---

## Acceptance Criteria

*From GDD `design/gdd/case-system.md`, scoped to this story:*

- [ ] AC-37-15:对 39/42/48 全部 DTO 类型跑 `PresentationDtoGuard` 递归扫描 ⇒ 零 `disease_id`/病种名成员;故意塞入一个 ⇒ 断言失败(正负夹具各一)
- [ ] AC-37-24:判定枚举/排序键的输入集 = 可读呈现量(读数态/quill_tick)+ 不含 disease_id、不含自书文本(类型面断言)
- [ ] AC-37-35:词表烘焙门 —— ①ordinal append-only 基线校验 ②非一一对应(双射)检查落构建期(阶段 2 校验器) ③`lexicon_id(u16)`/`confidence(u8)` 不属 Fix 解析集(fixture 验)
- [ ] AC-37-32:反射扫描 37 全部出站写路径 ⇒ 零数值奖励通道(无 potency/解锁/成长写入;P0 铁律)
- [ ] AC-37-31:「处置对不对」在 37 类型面无表示(无 correctness 字段/谓词;判定输入只有 state 与快照)
- [ ] AC-37-20(不拥有清单断言批):37 不定义 后果(→53)/处置内容(→10/11)/病情演进(→9)/词表语义(→8/内容)—— 每条目一个「越权符号不存在」或「只读接口」断言;⚠️ 53/8 载体未建齐的条目记 NOT-RUN
- [ ] quill_tick 只读进判定 DTO(呈现「何时落笔」,不回写判定)

---

## Implementation Notes

*Derived from ADR-013/014 Implementation Guidelines:*

1. 守卫复用 ADR-013 的 `PresentationDtoGuard` 既有实现,**增扫描根**(39/42/48 的 Case 相关 DTO 包),不复制第二套守卫。
2. 词表非一一对应校验 = 构建期代数检查(|词表项| vs |可关联病种| 的映射矩阵不得存在完美配对),落 ADR-014 阶段 2 校验器新增项;失败 = `throw` 级(硬失败先例 = ADR-022 C 类检查)。
3. freehand_text 的「零判定消费点」= 对 `Judgment` 字段引用做符号扫描(判定路径集白名单:落笔/改写/呈现三处,text 只出现在第三处)。
4. 零奖励断言落点:扫描 37 程序集的出站调用 ⇒ 白名单 ∈ {IEventSink.Append(五 Kind), 只读查询};任何 `Reward`/`Potency`/`Unlock` 类型出现即失败。
5. AC-37-16/17/19 的 [V] 走查不在本 story(移交登记见 story-006);本 story 只做**类型面**自动化半边。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: `Judgment`/载荷字段形状与 ordinal 编码(本 story 校验其取值合法性)
- [Story 002]: 判断事件写入(本 story 只断言其守密性质)
- [Story 004]: salted_key 的**计算**(本 story 断言其「防御纵深非保密层」的定位不再扩权)
- [Story 006]: [V] 走查移交与 53 侧后果
- 系统 42/48:DTO 类的**实现**(未建 ⇒ 相应断言 NOT-RUN,本 story 交付夹具与增员机制)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(守卫增员回归)**: 塞入即红
  - Given: 一个含 `int disease_id` 成员的假 DTO 类置于扫描根
  - When: 运行 PresentationDtoGuard
  - Then: 失败点名该类;移除后全绿;夹具本身留库(下轮走查复用)
  - Edge cases: 嵌套泛型成员递归可见(先例:AC-37-15 递归口径)
- **AC-2(词表双射反例)**: 一一对应夹具
  - Given: 手工烘焙 fixture 词表 = 病种数相同且完美配对
  - When: 阶段 2 校验器
  - Then: 构建失败(非告警);正常词表(多对一)通过
  - Edge cases: ordinal 复用(删项后旧号再指新项)⇒ append-only 门拒
- **AC-3(零奖励全扫)**: 出站白名单
  - Given: 37 程序集全类型反射
  - When: 扫描写调用
  - Then: 仅五 Kind Append 与只读查询;注入一个假 `SkillGrown` 调用 ⇒ 断言失败
  - Edge cases: PATTERN_THRESHOLD 调参 ≠ 奖励(数值轮路径,白名单不因此扩)

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/CaseSystem/case_secrecy_discipline_test.cs` + 构建期词表校验夹具 `unity/Assets/Tests/EditMode/CaseSystem/Fixtures/lexicon_bijection_fail.json` — must exist and pass(39/42/48 DTO 载体未建齐的条目维持 NOT-RUN,禁空集绿)
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(ordinal/字段形状);ADR-013 `PresentationDtoGuard` 既有件;ADR-014 阶段 2 校验器(增一项)
- Unlocks: Story 006(守密面重放不变量);39/42 epic 的 DTO 走查批(消费本 story 夹具)

---

## Completion Notes

*(留空 — story 关闭时回填)*
