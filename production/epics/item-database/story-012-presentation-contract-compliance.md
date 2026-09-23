# Story 012: 呈现契约合规走查

> **Epic**: 物品与配方数据库
> **Status**: Ready
> **Layer**: Foundation
> **Type**: UI
> **Estimate**: 2h
> **Manifest Version**: 2026-09-21
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/gdd/item-database.md`
**Requirement**: 无 TR —— 内容合规与呈现问句(命名规范保留词表 / 品级拟物呈现禁数字刻度),归属 GDD 自身,不挂 `TR-itemdb-*`。
*(This story covers presentation-facing acceptance questions rather than traceable requirements; no `TR-itemdb-*` ID applies.)*

**ADR Governing Implementation**: ADR: N/A — 呈现契约为 GDD §UI Requirements(U-1…U-6)直接规定,非架构裁决;走查执行体归 Epic 42(拟物 UI 框架),21a 只提供验收问句与数据侧前置(AC-45/46/54/55 的执行与签字均归 42)。
**ADR Decision Summary**: N/A —— 本故事不消费任何架构裁决;呈现框架本身归 ADR-013(UI Toolkit 主 + UGUI 补)与 Epic 42,不在本故事范围。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 本故事为文档合规 + 手工走查,不引入引擎 API;执行体归 Epic 42,其 Engine Risk 见 ADR-013(HIGH,焦点桥 / world-space / UI Toolkit 自定义材质须 spike)—— 与本故事无关,仅注明依赖 42 的可玩构建存在。

**Control Manifest Rules (this layer)**:
- Required: 走查证据落 `production/qa/evidence/`;BLOCKED-BY-21b 的 AC(43/44)在判据(保留词表)落盘前**不执行、不记账、禁记绿**;归 Epic 42 执行的 AC(45/46/54/55)21a 侧只登记问句,证据与签字由 42 承载
- Forbidden: 在 `tcm_reserved_terms.yaml`(D-21-12)落盘前自行臆造保留词表判据;把归 42 的走查执行体在 21a 侧代签
- Guardrail: UI 走查为 ADVISORY 门(Visual/Feel / UI 类证据),不入 BLOCKING 确定性套件

---

## Acceptance Criteria

*From GDD `design/gdd/item-database.md`, scoped to this story:*

- [ ] **AC-21a-43** [D]: 21b 的全部 P0 物品名逐条对照 §命名规范与 21b 的保留词表 ⇒ 无任何被保留词表命中的词。**状态:BLOCKED-BY-21b** —— 命名规范与保留词表(`tcm_reserved_terms.yaml`,D-21-12,须带药典版次/豁免/逐条出处)未落盘,判据不存在,本 spec 不可执行、禁记绿
- [ ] **AC-21a-44** [D]: P0 可玩版本,玩家查看任何物品名 ⇒ 不出现被保留词表命中的词。**状态:BLOCKED-BY-21b**(同上,且依赖 21b 内容 + 42 呈现已存在)
- [ ] **AC-21a-45** [U]: 42 的物品呈现 UI,走查任意物品 ⇒ 不出现数字品级、不出现线性刻度条(U-1/U-2)。**执行体归 Epic 42**(skeuomorphic-ui);21a 只提供验收问句,证据由 42 走查文档承载
- [ ] **AC-21a-46** [U]: 同一 base_id 的生药与炮制品并排走查 ⇒ 视觉可区分(U-4)。**执行体归 Epic 42**
- [ ] **AC-21a-54** [U]: 42 的物品呈现 UI,走查任一有 quality_character[] 的物品 ⇒ 品级以「外观/药签措辞」呈现(U-5),不得退化为数字品级或线性刻度(U-1/U-2)。**执行体归 Epic 42**
- [ ] **AC-21a-55** [U]: 42 的物品呈现 UI,走查任一有 drug_quality_character[] 的成药 ⇒ 品级以「药签措辞/外观」呈现(U-6),不得数字/线性刻度 —— 成药侧与原料侧各一条通道,互为补充不可互替。**执行体归 Epic 42**

**状态注记**: 43/44 = BLOCKED-BY-21b(判据不存在);45/46/54/55 = 执行体归 Epic 42,21a 侧仅登记问句、不代签证据。

---

## Implementation Notes

*Derived from GDD §UI Requirements 直接规定(非 ADR):*

- 本故事的「Implementation」是**组织与跟踪**动作,不是写代码:登记 21b 落盘与 42 走查的依赖关系,确认 21a 数据侧已提供 `quality_character[]` / `drug_quality_character[]` / `legal_transitions` 等呈现所需字段(字段本身由 Story 002/004 校验,本故事只确认它们可被 42 消费)
- **BLOCKED-BY-21b 的判据** = `tcm_reserved_terms.yaml`(D-21-12),须带药典版次/豁免/逐条出处;在它落盘前,AC-43/44 不可执行(不是「执行了没通过」,是「判据不存在」)—— 承本项目禁借绿纪律:记 BLOCKED-BY-Rn 不得记绿
- **执行体归 Epic 42** 的四条(45/46/54/55):21a 只提供验收问句(已写入本文件 Acceptance Criteria),证据截图与主美/lead 签字由 42 的走查文档承载;21a 侧不重复建证据文件
- UI 走查 = ADVISORY 门(coding-standards:UI 类证据为 Manual walkthrough doc / interaction test,不入 BLOCKING)

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002 / 004 / 007: `quality_character[]` / `drug_quality_character[]` 字段的长度与地板校验(AC-50/50b/62/37/61)—— 本故事只消费字段做呈现,不校验数据
- Story 006 / 007: 其余负向夹具
- Epic 42(拟物 UI 框架): AC-45/46/54/55 的实际 UI 实现与走查执行、证据签字
- 21b(物品内容,尚未立项): AC-43/44 的命名规范与保留词表本体

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-21a-43** [D]: 21b 的全部 P0 物品名逐条对照 §命名规范与 21b 的保留词表 ⇒ 无任何被保留词表命中的词。**状态:BLOCKED-BY-21b** —— 命名规范与保留词表(`tcm_reserved_terms.yaml`,D-21-12,须带药典版次/豁免/逐条出处)未落盘,判据不存在,本 spec 不可执行、禁记绿。
  - Setup: 待 21b 落盘后:取得全量 P0 物品 display_name 清单 + 保留词表(带出处)。
  - Verify: 逐名逐词匹配保留词表(匹配规则按 21b 定义的判定法);命中即列违例。
  - Pass condition: 违例数 = 0。**当前:BLOCKED-BY-21b(21b 未立项/词表未落盘),不执行、不记账。**

- **AC-21a-44** [D]: P0 可玩版本,玩家查看任何物品名 ⇒ 不出现被保留词表命中的词。**状态:BLOCKED-BY-21b**(同上,且依赖 21b 内容 + 42 呈现已存在)。
  - Setup: 待 21b + 可玩构建后:游戏内遍历全部可显示物品名(图鉴/库存/配方/药签入口)。
  - Verify: 任一界面显示的物品名对照保留词表。
  - Pass condition: 全部显示名零命中。**当前:BLOCKED-BY-21b,不执行、不记账。**

- **AC-21a-45** [U]: 42 的物品呈现 UI,走查任意物品 ⇒ 不出现数字品级、不出现线性刻度条(U-1/U-2)。**执行体归 Epic 42**(skeuomorphic-ui);21a 只提供验收问句,证据由 42 走查文档承载。
  - Setup: 进入 42 实现的物品呈现界面(库存/图鉴/药签),对任意带 quality 的物品展开详情。
  - Verify: 界面无「品级 3」「品级:75%」类数字,无 progress-bar/线性刻度条表示品级;等级信息仅能以外观/措辞差异感知。
  - Pass condition: 所查物品详情中品级维度零数字、零线性刻度;违例截图归档 `production/qa/evidence/`。执行人:Epic 42;21a 侧仅登记问句。

- **AC-21a-46** [U]: 同一 base_id 的生药与炮制品并排走查 ⇒ 视觉可区分(U-4)。**执行体归 Epic 42**。
  - Setup: 42 UI 中同 base 的 raw 态与 dried/extracted 等态并排(同屏或对照截图)。
  - Verify: 两态在图标/底纹/签条/形态上肉眼可区分,不需读文字也能分辨(文字标签辅助亦可,但视觉层必须有差)。
  - Pass condition: 对照截图中两态可即时区分,主美/lead 签字;证据落 `production/qa/evidence/`。执行人:Epic 42。

- **AC-21a-54** [U]: 42 的物品呈现 UI,走查任一有 quality_character[] 的物品 ⇒ 品级以「外观/药签措辞」呈现(U-5),不得退化为数字品级或线性刻度(U-1/U-2)。**执行体归 Epic 42**。
  - Setup: 取 gather_profile.quality_character[] 非空的原料,在 42 UI 切换不同 quality 档实例查看。
  - Verify: 每档显示对应定性修饰措辞(如 GDD 示例语义:新采带露/干燥/陈放/虫蛀类措辞)与外观差;无数字、无刻度。
  - Pass condition: 各档呈现 = 该档 quality_character 字符串语义 + 视觉差,零数字零刻度;证据归档。执行人:Epic 42。

- **AC-21a-55** [U]: 42 的物品呈现 UI,走查任一有 drug_quality_character[] 的成药 ⇒ 品级以「药签措辞/外观」呈现(U-6),不得数字/线性刻度 —— 成药侧与原料侧各一条通道,互为补充不可互替。**执行体归 Epic 42**。
  - Setup: 取 drug_quality_character[] 非空的成药,不同 quality 档实例在 42 UI 查看。
  - Verify: 成药签显示该档 drug_quality_character 措辞(工艺质地类:炮制得法/火候稍欠等语义)+ 外观差;不与原料侧 quality_character 混用通道;无数字无刻度。
  - Pass condition: 成药侧通道独立生效、措辞与档位一一对应(长度 = MAX_QUALITY 已由 AC-62 数据侧保证)、零数字零刻度;证据归档。执行人:Epic 42。

---

## Test Evidence

**Story Type**: UI
**Required evidence**:
- UI: `production/qa/evidence/` — **执行体归 Epic 42**,21a 侧不重复建档;43/44 = BLOCKED-BY-21b 不执行、不记账

**Status**: [ ] Not yet created(归 Epic 42 的走查证据)

---

## Dependencies

- Depends on: Story 002 / 004(`quality_character[]` / `drug_quality_character[]` 字段就位)· Epic 42(可走查 UI 实现)· 21b(保留词表本体,尚未立项)
- Unlocks: None(呈现层合规是收口走查,不单独解锁其他故事)
