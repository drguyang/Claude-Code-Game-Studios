# Story 004: 反幻想三条「绝不」的能力面守卫

> **Epic**: 脉案
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/casebook.md`(规则四 三条绝不 · AC-39-05 / AC-39-06 / AC-39-08 / AC-39-09 · `disease_id` 不进呈现层)
**Requirement**: TR-casebook-006(防间接泄漏的**能力面** —— 39 的 API 不暴露任何「按病种聚合 / 计数 / 检索」形状,partial:45 侧残)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-008(主): 病例流 · ADR-013: 拟物 UI 框架 · ADR-009: 世界状态事件化边界(`disease_id` 保密语义 = 防御纵深)
**ADR Decision Summary**: 39 是 `AC-37-15` 类型面守卫(`PresentationDtoGuard` 递归反射)之外的**能力面**落点:① 绝不显示未结病例计数 / 红点 / 提醒;② 绝不按病种分组、排序、检索;③ 绝不提示同源(不暗示「这些病例像同一个病」)。归纳是玩家的动作,不是界面的功能(AC-39-08)。实现载体 = 构建期扫描(反射断言 + USS 文本黑名单),不是散文约定 —— 同 ADR-017 门 A 白名单「把约定升为构建失败」的先例形制。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 守卫本体 = 纯 C# 反射 + 文本扫描;被扫对象含 UI Toolkit USS/UXML 资产(读文件文本,非引擎 API)。

**Control Manifest Rules (this layer)**:
- Required: 判据 = 可执行断言(构建期 / EditMode),非 grep 单点;闭集扫描方向 = 「白名单外即失败」
- Forbidden: 以「运行时不显示」替代「能力面不存在」(API 里能数 ⇒ 泄漏路径已打开);用红点/角标/计数文本表达任何聚合
- Guardrail: 与 8 的词表联动 —— 呈现词经 ADR-014 烘焙,词表**不含**病种分组维度(闭集扫描的输入)

---

## Acceptance Criteria

*From GDD `design/gdd/casebook.md`, scoped to this story:*

- [ ] **AC-39-05 能力面**:39 公开 API 与投影 DTO 上不存在「按 `disease_id`/病名 分组 / 排序 / 检索 / 计数」的任何方法或参数(反射断言:公开签名扫描,标识符黑名单 ∈ {GroupByDisease, SortByDisease, SearchDisease, CountBy*, CountUnopened, UnopenedCount…})
- [ ] **AC-39-06a 本地化键闭集 + USS 文本黑名单**:脉案用到的全部文本键 ∈ 闭集(「读数 / 落笔 / 翻页 / 合上」类),扫描不出「同源 / 相似 / 同症 / 未结 N / 提醒」类字符串;USS/UXML 里 `text=` 直写 = 黑名单命中即构建失败
- [ ] **AC-39-08 零归纳辅助**:39 无任何「跨病例聚合视图」(同一投影不做两次以上病例间的比较;`J(c,p)` 仅病例内)
- [ ] **`disease_id` 类型面**:39 消费的全部 DTO 过 `PresentationDtoGuard` 递归扫描子集(与 AC-37-15 同判据、共享扫描器),零命中(= AC-39-01 的本 story 侧落点)
- [ ] **AC-39-09 [L]** 「病名列空白、零催促」人工走查项**建立走查脚本与证据模板**(本 story 交付走查用例文档,执行与主创签核归 QA 轮)[L]
- [ ] 「未结」状态词仅作为**单病例**的 37 状态读出(已处置/未处置的呈现权归 37 判据),39 不做**跨病例**计数

---

## Implementation Notes

*Derived from ADR-013 / ADR-008 Implementation Guidelines:*

1. 扫描器落 EditMode 测试 + 构建期 hook 双跑(形制照抄 `PresentationDtoGuard` 的「递归反射扫描」:公开方法参数名/返回类型/委托签名三面)。
2. 本地化键闭集 = 作者态数据(ADR-014 管线 `assets/data/casebook_text.json` → `.cooked`);扫描输入用烘焙产物的键集,运行期零 JSON 解析。
3. USS 文本黑名单扫 `text=` / `-unity-text` 属性与 UXML 内联字符串;命中表与 AC-39-06a 词族一一对应(词族表进测试夹具,数值/词表扩充归用户/文案轮)。
4. 「能力面」与「呈现面」分离:本 story 只管 **API 里做不到**;「就算有人偷加,UI 也不显示」不是达标 —— 记入 story 评审检查单。
5. 与 42 的边界:焦点引擎、元件库归 42;本 story 扫的是 **39 程序集** + 39 独占的 UXML 片段,不判 42 全域。

---

## Out of Scope

- [Story 005]: 空行的可聚焦与压痕声(呈现面,42 元件)
- [Story 006]: 落笔/相机路径
- AC-39-06b / 09 / 11 / 12 / 14 的**执行走查**([L] 类,归 QA 轮 + 主创签核;本 story 只立守卫与走查脚本)
- 44 侧零状态播报音(联合 AC-44-09,归音频 epic)

---

## QA Test Cases

**[Logic story — automated test specs]:**

- **AC-1**: 公开签名黑名单扫描
  - Given: 39 程序集全部 public 类型
  - When: 反射枚举方法/属性/字段,匹配标识符黑名单
  - Then: 零命中(命中 = 构建失败)
  - Edge cases: 内部实验性方法标 `[Obsolete]` 也算命中(扫描面 = 全 public,无豁免)

- **AC-2**: DTO 无病种维度
  - Given: 39 消费的全部投影 DTO
  - When: `PresentationDtoGuard` 递归扫描 + 字段名黑名单
  - Then: 无 `disease_id`/病名分组结构;`disease_id` 零出现
  - Edge cases: 嵌套集合元素类型也被递归(列表泛型参数扫描)

- **AC-3**: 文本键闭集
  - Given: `.cooked` 文本表 + 39 UXML/USS 资产
  - When: 构建期扫描:① 39 引用的键 ⊆ 闭集;② `text=` 直写 = 0;③ 黑名单词族(同源/相似/未结N/提醒)= 0
  - Then: 三项全零命中
  - Edge cases: 注释与测试夹具排除在扫描集外(白名单目录表)

- **AC-4**: 跨病例比较不存在(AC-39-08)
  - Given: 投影 API 面
  - When: 评审 + 断言:无任何函数接受「≥2 病例」输入对并返回相似/同源判定
  - Then: 零命中;单病例状态词读出(37 的已处置)合法
  - Edge cases: 串接组「叠」是同一**病人**的分组(允许),不是病种分组(禁止)—— 判据写进断言注释

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/Casebook/casebook_anti_fantasy_guard_test.cs` + `production/qa/evidence/casebook/story-004-l-walkthrough-scripts.md`([L] 走查脚本模板,执行留 QA 轮) — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002 / 003(API 面成形后才有可扫对象)
- Unlocks: Story 005(呈现层在「做不到」约束内设计,不再返工)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*
