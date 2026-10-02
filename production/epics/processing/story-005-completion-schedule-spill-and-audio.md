# Story 005: 完成调度、起货溢出与等待期音频护栏

> **Epic**: 炮制
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/processing.md`(规则八 R-18-D[甲]② 起货溢出 · 等待期可读性 · 规则十一 音频 BL-10 · UI-18 · AC-18-21/09/22/24/08)
**Requirement**: TR-processing-012(完成时点派生的执行侧)· TR-processing-001(F1 出参原样交 20/落地的下游收口)· TR-audio 族联动(44 消费 18 的 cue 发射;sfx_process_light/complete/work,总线 SFX)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-018(主): 音频架构 —— 44 只触发、无提示音铁律(AC-44-09 白名单),18 = cue 发射方 · ADR-009(次): 溢出落地 = 世界流 `DropSpawned`(身份进流/位置表现)· ADR-013(次): 42 只渲染永不持状态(进度不画、器物状态语义归 18)· ADR-005(次): 完成 tick 到达 = `ITickProvider` 驱动,禁墙钟/帧对齐
**ADR Decision Summary**: 起货溢出 R-18-D[甲]②:完成 tick 当场**本格 `DropSpawned`**,由 20 从 `Craft` 派生折叠(O-18-R4 回 20),18 不写流(落地事件发出者 = 20 结算路径),不延迟不中断;等待期可读性:42 不画进度条,器物状态语义归 18(OQ-18-9 具名渲染形态未裁 ⇒ 只断 18 侧输出:调度落点 + cue 发射,AC-18-21);炉火声床禁 sting / 倒数感(用耳朵数 tick = 失效模式,AC-44-09 白名单**看不见**的听测面独立成 AC-18-09)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 完成调度为纯 tick 逻辑(LOW);cue 发射经 `AudioCueDto` 边界(整数语义,ADR-018),44 侧 AudioMixer 6.3 post-cutoff 行为归 audio-system epic,本故事不触混音实现;[L] 听测/盲测为人工面。

**Control Manifest Rules (this layer)**:
- Required: 完成时刻 = tick 谓词命中(`tick == CompleteTickOf`)当拍调度;`sfx_process_complete` 发射与器物状态变化同源(1 tick 误差内可判起货);溢出路径 = 20 的标准入库失败 ⇒ 本格 `DropSpawned`(成品不消失、不延迟);cue 载荷 ⊆ 整数、无品质维(承 ADR-018 §六)
- Forbidden: 墙钟/`Time.*`/帧对齐作发射条件(Visual/Audio 护栏 1);18 直写世界流落地事件(发出者 = 20,18 只触发结算);进度百分比入 DTO;节奏化加密声床(= 播报剩余时间,违 AC-44-09 精神)
- Guardrail: OQ-18-9(器物状态具名渲染形态)未裁 ⇒ AC-18-21 只断 18 侧两输出,渲染形态半边留给 42;AC-18-08(零数字走查)与 AC-18-24(仅手柄全流程)执行依赖 42 面板/焦点导航落地 ⇒ 走查前保持未勾

---

## Acceptance Criteria

*From GDD `design/gdd/processing.md`, scoped to this story:*

- [ ] **AC-18-21** [I]: 在场观察者,时间到达 `CompleteTick` ⇒ 1 tick 误差内器物状态变化可判起货 + `sfx_process_complete` 发射 —— 只断 18 侧输出(调度落点 + cue 发射,OQ-18-9 具名渲染形态不影响本条);禁以墙钟/帧对齐为发射条件
- [ ] **AC-18-09** [L]: 全部炮制音频听测收窄为 AC-44-09 白名单**看不见**者:等待期声床无节奏化加密(= 用耳朵数 tick)、无「还剩 N 秒」式边界音形。证据归档 `production/qa/evidence/processing-09-<date>.md`(镜像 AC-5-10:混入缺陷样本,分不出即过)
- [ ] **AC-18-22** [L]: 去标注盲测:含多次完整炮制的录音,受试者仅凭听觉可指出起货时刻(±1 tick);失败判据 = 报出「倒数感」或把起货声判为播报。与 44 联合验收
- [ ] **AC-18-24** [L, ADVISORY]: 仅手柄(无指针)走查选料→点火→起货全流程,每步有焦点导航路径可达。18 侧义务 = 谓词与调度设备中立(已由 AC-18-20 机器化);本条端到端佐证,主验收在 42/43(TR-concept-008)
- [ ] **AC-18-08** [L]: 一次完整炮制走查逐屏零数字(无转化率/成功率/进度百分比),成色由质地/药签措辞可读。证据归档 `production/qa/evidence/processing-08-<date>.md`(主创签核页)

---

## Implementation Notes

*Derived from ADR-018 §Decision(主)/ ADR-009 §六 扩展:*

1. 完成调度:每 tick(经 `ITickProvider` 步相位,承 tick 裁定 ③)对「在烧」谓词命中 `tick == CompleteTick` 的炉,触发 20 的结算入口(`ApplyOutput`):容量足 ⇒ 成品入 `InventoryOf`;不足 ⇒ 20 走本格 `DropSpawned`(R-18-D[甲]②,不延迟不中断)。18 提供触发与时机,**不持有落地事件写权**(发出者 = 20,同 Story 003 铸造的下游)。
2. `Craft` 载荷的 `output_instance_ids[]` 已在点火铸造 ⇒ 完成落地只引用既有 id(`DropSpawned{instance_id = 已铸 id, spawn_anchor = tool_cell, ...}` 形状对齐,承 ADR-024 载荷)—— 无第二铸造点。
3. cue 发射:`sfx_process_light`(点火)/`sfx_process_work`(等待期声床,无进度语义)/`sfx_process_complete`(起货)三 cue,经 `AudioCueDto` 边界给 44;发射条件 = tick 事件,禁墙钟(AC-18-21 判据);声床素材的「非播报性」归 44 白名单断言(AC-44-09)+ 本故事 [L] 听测。
4. 等待期可读性接缝:器物状态语义(炉盖/音色等)具名形态 = OQ-18-9 未裁 ⇒ 本故事只交「状态变化时刻 + cue 时刻」两个整数时间点给 42,不定义渲染物。
5. 溢出路径的 spy 测试:AC-18-12(点火时点)与本条(完成时点)共享 spy-sink 基建;断「成品永不消失」= 折后 `InventoryOf ∪ 地面 drop` 含全部产出实例。
6. [L] 三条(08/09/22/24)证据目录 `production/qa/evidence/` 在走查轮生成;目录从未建立的历史事实(item-database story-003 先例)⇒ 登记不擅建,建目录属项目级决定。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003/004:载荷、铸造、时序谓词(本故事的输入)
- inventory-items epic:20 结算/入库/`DropSpawned` 发出本体(O-18-R4 的回写落点)
- audio-system epic:44 混音总线、白名单断言执行体(AC-44-09)、素材管线
- skeuomorphic-ui(42)epic:炮制面板、进度禁画、焦点导航、OQ-18-9 渲染形态
- ADR-012 / persistence:重放逐位与存档(Story 004 已钉)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-18-21**: 调度落点 + cue 发射。
  - Given: 夹具炉一,`duration_ticks = 20`;tick 驱动逐拍推进;cue spy。
  - When: 到达 `CompleteTick`。
  - Then: 当拍(误差 0,谓词等号)状态变化时间点输出 + `sfx_process_complete` 恰 1 条;无墙钟符号参与发射条件(扫描复用 AC-18-13 件)。
  - Edge cases: 完成 tick 与存档恢复交叉(重放后同 tick 同结果);两玩家同 tick 完成 ⇒ 各 1 cue 不合并。
- **溢出路径**: 完成时点容量不足。
  - Given: 20 剩余容量 < 产出总体重。
  - When: 完成调度。
  - Then: 成品以 `DropSpawned` 落 `tool_cell` 格,id = 点火已铸 id;fold 后实例恒在(不静默吞、不延迟);无「半成品」中间态。
  - Edge cases: 部分容纳 ⇒ 整批落地(与拾取整件拒绝同纪律,20 规则六);落地后玩家可拾(承 inventory 拾取链)。
- **AC-18-09 / 22** [L]: 声床听测 + 盲测。
  - Given: 44 素材接入后的录音样本(含故意混入的加密声床缺陷样例,镜像 AC-5-10 形式)。
  - When: 人工听测 / 受试者仅凭听觉报起货时刻。
  - Then: 无倒数感/播报判读;起货时刻可指出(±1 tick);证据 + 与 44 联合签核。
  - Edge cases: 走查未跑 ⇒ 未勾,禁借 AC-18-21 机器绿。
- **AC-18-24** [L, ADVISORY]: 仅手柄全流程。
  - Given: 手柄独占输入构建(42 焦点导航落地后)。
  - When: 选料→点火→起货走查。
  - Then: 每步可达、无指针依赖;ADVISORY 级(主验收在 42/43)。
  - Edge cases: 药箱模态与炉交互的焦点单栈门(ADR-013)交接点。
- **AC-18-08** [L]: 零数字走查。
  - Given: 42 面板落地后的完整炮制操作序列。
  - When: 逐屏记录。
  - Then: 零转化率/成功率/百分比;成色由质地/药签措辞可读;主创签核页归档。
  - Edge cases: 42 未交付 ⇒ 保持 EXTERNAL 标记未勾(机制侧供数无数字已由 Story 002 保证,不代走查)。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `unity/Assets/Tests/PlayMode/process_completion_and_spill_test.cs` — must exist and pass
- [L]: `production/qa/evidence/processing-{08,09,22,24}-<date>.md`(走查/听测轮,与 44/42 联合)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004(`CompleteTick` 谓词),Story 003(已铸 output ids),inventory-items Story 001/004(20 结算与 `DropSpawned` 发出),audio-system epic(cue 消费端,已 Complete 件直连),42(走查三条的呈现前置,EXTERNAL 半边)
- Unlocks: 炮制 epic 收尾走查轮;OQ-18-9 裁定后的渲染形态故事(另开);O-18-R4 回写完成确认

---

## Completion Notes

*(placeholder — to be filled at story completion)*
