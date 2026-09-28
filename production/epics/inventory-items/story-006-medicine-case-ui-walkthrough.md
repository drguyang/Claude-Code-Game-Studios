# Story 006: 药箱容器摆放、翻页遍历与满溢四档呈现

> **Epic**: 库存与物品
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/inventory-and-items.md`(规则四 UI-20.3 读数 · 规则十 容器与翻页 · 音效护栏 · AC-20-10/11/20/23)
**Requirement**: TR-inventory-011(载重呈现 = 药箱满溢感四档定性,零负重条/百分比/网格/排序/自动整理 —— ADR-013)· TR-inventory-016(联机同步粒度 ❌ gap,P1b 归 45,本故事单机不阻塞)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-013(主,含 Amendment B): UI Toolkit 主栈;ModalId 闭集 7 员含 `InventoryContainer`(模态 ③ 药箱);42 只渲染永不持有游戏状态;焦点单栈门 + 两栈共用同一 EventSystem;42 元件库「器具」第三材质档(木/皮/铜)已裁 · ADR-018(次): 音效零状态播报(AC-44-09 白名单)· BL-27 分工:`slack = cap − carry_load` 由 42 自算,四档 = slack 单调不增函数,阈值归 42(R9)
**ADR Decision Summary**: 药箱 = UI Toolkit **平面拟物面板**(BL-7① 语义读法,非 world-space);摆放 = `instance_id` 升序确定性函数落二维位(col/row,Story 003 提供序函数,`COLUMNS`/`ROWS_PER_PAGE` 归 42);超一屏走 `PagePrev`/`PageNext`(R8 已回写,20 为翻页的消费者)+ 方向键遍历(AC-20-20:同一箱内容 + 同一方向键 ⇒ 同一焦点目标,稳定可达);满溢四档(空/半满/近满/满合盖受阻)禁数字禁条;色盲走查须存在非颜色通道承载品级差异(AC-20-23);库存音效零状态播报(无拾取 jingle / 无满箱警告,AC-20-11 与 AC-44-09 联合)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH
**Engine Notes**: UI Toolkit 6.3 运行时 + gamepad 焦点桥(官方 `NavigationMoveEvent`/`FocusController`,ADR-013 §6.6 **假设 6 = ⚠️ 半可信,原型 spike 为前置** —— S5 spike 未跑);`InventoryContainer` 模态接线承 skeuomorphic-ui epic 元件库;走查证据面(四档可读性)不可自动化,按 UI 类 ADVISORY 处置。

**Control Manifest Rules (this layer)**:
- Required: 界面只读 Story 001 投影 + Story 004 `CarryRatioDto`,状态变更一律经意图(拾/放/用)回主机/fold,零本地乐观状态;四档 = slack 的单调不增函数(阈值 42 侧旋钮);焦点路径完整覆盖(仅手柄可选中);`ModalId.InventoryContainer` 走闭集既有成员(零新增枚举)
- Forbidden: 负重条/百分比/数字(AC-20-10);网格背包形态(42 规则五);排序/自动整理按钮;第二容量真值/在 42 侧判载重(谓词归 20,BL-27 分工);模态期间 20 被绕过直接写流(写权 = 主机/20 事务);拾取 jingle / 满箱警告音(AC-20-11)
- Guardrail: AC-20-20 走查**BLOCKED-BY-R9**(页可见集归 42 未完全定 + 二维几何焦点在「同一箱内容+同一方向键 ⇒ 同一目标」的稳定性依赖 R9 ①② 子项)—— 前提未回写前不得记绿;R8(翻页消费者)已回写可用

---

## Acceptance Criteria

*From GDD `design/gdd/inventory-and-items.md`, scoped to this story:*

- [ ] **AC-20-10** [L]: `GIVEN` 药箱从空到满,`WHEN` 人工走查,`THEN` 满溢感可读(不看数字也知能否再装),且无任何负重条/百分比(主创签核)
- [ ] **AC-20-11** [L]: `GIVEN` 全部库存音效,`WHEN` 人工听测,`THEN` 零状态播报音(无拾取 jingle / 无满箱警告)—— 与 AC-44-09 联合
- [ ] **AC-20-20** [I] ⚠️ **BLOCKED-BY-R9**: `GIVEN` 箱内器物超一屏,`WHEN` 手柄仅用 `PagePrev/PageNext` + 方向键,`THEN` 可遍历全部器物,且同一箱内容 + 同一方向键 ⇒ 同一焦点目标(稳定且可达);序 = `instance_id` 升序(「入箱序」废止);`COLUMNS`/`ROWS_PER_PAGE` 归 42
- [ ] **AC-20-23** [L]: `GIVEN` 色盲模拟 + 仅手柄走查药箱,`WHEN` 区分两品级同种器物,`THEN` 存在非颜色通道(形状/药签文字)承载品级差异(记号具体形态归 42)
- [ ] **读数契约(本故事可自证的半边)**: 42 侧四档由 `slack = cap − carry_load` 相减得出;断言 20/42 接缝中 `CARRY_CAP` 唯一来源、四档切换对 `carry_load` 单调不增(承 AC-20-24 的 20 侧已证 + 本故事出分档函数的形状测试)

---

## Implementation Notes

*Derived from ADR-013 §Decision(主)/ BL-27 分工:*

1. 屏 = 模态 ③ `InventoryContainer`(闭集既有员,ADR-013 Amendment B 七员口径);UXML/USS 走 42 元件库,器具质感第三材质档(木/皮/铜)已裁(任务规则钉)—— 药箱外观 = 拟物器具,禁背包格视觉。
2. 数据流:`InventoryOf` 只读快照 → 摆放序(Story 003 纯函数)→ 落位(col/row,`COLUMNS` 为 42 旋钮);翻页 = 可见集切分(R9 的页可见集子项未完全定 ⇒ 先按「稳定序 + 页容量」最小实现,BLOCKED-BY-R9 标注保留)。
3. 焦点:UI Toolkit 官方导航桥(`NavigationMoveEvent`),焦点单栈门(药箱模态开 ⇒ 世界动作栈闭,承 `IModalState` 只读契约);键鼠与手柄双导航等价可达(AC-20-20 手柄半边 + AC-20-23)。
4. 四档呈现:合盖受阻 = 第四档的拟物语义(满时盖不上的视觉/动效),零数字;分档函数单调不增断言以整数序列穷举(EditMode 可测部分)。
5. 音效接线:库存相关触发只经 cue 白名单(44 侧断言),18/20 无 jingle;本故事只提供「无播报音源」的素材清单核对(与 audio-system epic 联合)。
6. 品级非颜色通道:同种异品级用 形状/药签文字 区分(AC-20-22 的呈现对偶,记号形态 = 42 自家题);色盲模拟走查用 Unity 内置 + 屏幕设置(人工面)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003:摆放序纯函数 / 堆叠语义(本件消费)
- Story 004:`CarryRatioDto` 与谓词(本件消费;四档函数是 42 侧实现)
- Story 005:意图事务与无负确认(本件被拒反馈 = 重读投影,零信令 UI)
- skeuomorphic-ui(42)epic:元件库材质档实装、模态管理、R9 完整回写、`AC-42-F1` 走查轮
- audio-system(44)epic:混音、白名单断言执行体(AC-44-09)
- ADR-013 假设 6 / S5 spike:手柄焦点导航可行性(桌面 Unity 侧,另行安排 —— 本故事 BLOCKED-BY 链的一环)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-20-10** [L]: 满溢感走查。
  - Given: 可玩构建;从空箱逐件添至满。
  - When: 人工走查(禁提示数字条件下判断「能否再装」)。
  - Then: 四档可读(空/半满/近满/合盖受阻);零负重条/百分比;主创签核页归档 `production/qa/evidence/inventory-10-<date>.md`。
  - Edge cases: 恰临界(`cap − W_MIN` 带,与机制谓词 AC-20-18 对照同一状态)。
- **四档单调(可自动化半边)**。
  - Given: `carry_load` 0..cap 全整数扫描;合成阈值组。
  - When: 求档。
  - Then: 档号随 carry_load 单调不增(松弛度视角)且只由 slack 决定(无第二输入);接缝 cap 值 = 20 的 `CARRY_CAP`(对象同一)。
  - Edge cases: 阈值相邻平段(档不变);W_MIN>1 临界带不误判「满」。
- **AC-20-20** [I](BLOCKED-BY-R9,机制半边先测)。
  - Given: 超一屏夹具(>COLUMNS×ROWS 件);手柄独占。
  - When: PageNext + 方向键遍历。
  - Then: 全部器物可达、无死角;同内容同按键序列 ⇒ 同焦点目标(两次遍历路径逐位比);顺序 = instance_id 升序可见。R9 未回写 ⇒ 走查判据半边未勾(禁借形状绿)。
  - Edge cases: 中途增删一件(投影变化)⇒ 焦点重定位确定性;末页回翻。
- **AC-20-11 / 23** [L]: 音效听测 + 色盲手柄走查。
  - Given: 录音样本 / 色盲模拟 + 两品级同种器物对。
  - When: 听测 / 走查。
  - Then: 零播报音(无 jingle/警告);存在非颜色通道区分品级;证据归档,与 44/42 联合签核。
  - Edge cases: 走查未跑 ⇒ 未勾,禁借 EditMode 绿。

---

## Test Evidence

**Story Type**: UI
**Required evidence**:
- Manual walkthrough doc + interaction test: `production/qa/evidence/inventory-{10,11,20,23}-<date>.md`(ADVISORY/UI 门)
- Logic(四档单调/接缝半边): `unity/Assets/Tests/EditMode/Inventory/carry_tier_monotonicity_test.cs`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003(摆放序),Story 004(读数契约),Story 005(意图事务),skeuomorphic-ui epic(元件库材质档 + ModalId 接线,既有 Complete 件直连),R9 完整回写(AC-20-20 走查半边前提,部分未回写 ⇒ BLOCKED-BY 保留),S5/ADR-013 假设 6 spike(桌面侧)
- Unlocks: `AC-42-F1` 走查轮的库存屏项;数值轮满档体验校正

---

## Completion Notes

*(placeholder — to be filled at story completion)*
