# Story 005: 采集动作原子性、失败收敛与呈现/浮点反射门

> **Epic**: 采集
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/foraging.md`(规则六 · Edge Cases 失败收敛 · UI Requirements · AC B/C/D 组走查行)
**Requirement**: TR-foraging-001(三 Kind 原子的动作侧收口)· TR-foraging-008(联机采集意图上行须 ADR-001 窄修订,❌ gap —— P1b 前硬前置,本故事只钉单机主机终裁形状,联机半边不外纳)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011(主,Amendment B): 输入架构 —— 客户端聚合意图上行、主机终裁、本地判定降级为预表现 · ADR-018(次): 音频只触发、无提示音铁律 · ADR-013(次): 42 只渲染永不持状态 · ADR-006(次): 浮点边界
**ADR Decision Summary**: 采集 = 世界动作:经 3(输入 action 映射)+ 4(F-4.1 目标选择,「脚与身位即光标」,**不经过 42 焦点栈**);判定归主机(单机 P0 = 本地主机),客户端预答仅呈现;失败三类(满载 / 余量不足 / 超时未裁决)**各自具名**、非弹窗非文字;`AudioCueDto` 载荷禁品质维(ADR-018 §六 无提示音铁律的视觉/听觉侧同源);呈现 DTO 禁「原始品级 / 本可采到」字段(反幻想硬约束)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 输入侧触 Input System 6.3(post-cutoff,须实测但走 ADR-011 已裁的 action 资产路线,本故事只消费 3 的既有 action);反射/Roslyn 扫描为纯工具面(LOW);[L] 走查项依赖 42 呈现规格(未认领 ⇒ EXTERNAL 标注原样保留)。

**Control Manifest Rules (this layer)**:
- Required: 三类失败各有具名枚举/分支;中途打断 = 零效果(无事件、余量不变);DTO 递归 type-surface 反射无「原始品级/本可采到」字段;`AudioCueDto` 载荷类型不含品质维;sim 求值路径 Roslyn 扫描零 `float`/`double`(注意 `FixParse` 在烘焙期不在运行期,扫描口径按 ADR-014 收窄)
- Forbidden: 采集交互走 42 焦点导航(世界动作经 3+4);准星/描边高亮载体(4 已废除);单一「采集失败」可选反馈糊弄三类收敛;向 45 索 ack(禁负确认,同 20 AC-20-25 纪律)
- Guardrail: 超时未裁决分支在 OQ-17-6 裁定前**只立枚举与分支不接网络**(单机不可达路径,静态检查仍过;禁为「凑绿」伪造超时夹具)

---

## Acceptance Criteria

*From GDD `design/gdd/foraging.md`, scoped to this story:*

- [ ] **AC-17-07**: `GIVEN` 库存满载,`WHEN` 采集,`THEN` 失败且不消耗余量、不发成长(EC 满载行)
- [ ] **AC-17-07c**: `GIVEN` 17 的失败收敛路径,`WHEN` 静态检查,`THEN` 三类失败各有具名枚举/分支,非单一可选反馈(no hand-waving)
- [ ] **AC-17-08**: `GIVEN` 采集中途被打断,`WHEN` 动作终止,`THEN` 零效果(无事件、余量不变)(规则六)
- [ ] **AC-17-09**: `GIVEN` 全部呈现层 DTO,`WHEN` 递归 type-surface 反射扫描,`THEN` 无任何「原始品级 / 本可采到」字段(AC-37-15 同型,非按名 grep)
- [ ] **AC-17-09b**: `GIVEN` 采集成功/失败音的 `AudioCueDto`,`WHEN` 反射断载荷字段类型,`THEN` 载荷不含品质维(ADR-018 §六)
- [ ] **AC-17-10**: `GIVEN` F-17-1/2/3 求值路径,`WHEN` Roslyn 字面量扫描 + 类型签名断言,`THEN` sim 字段零 float/double、零浮点字面量(FixParse 在烘焙期)
- [ ] **AC-17-07b** [L]: 三类失败反馈可区分且各自可见(玩家走查)。⚠️ 载体 = 42 的「三类失败反馈」UX Flag ③,**未交付前记 EXTERNAL 不得记绿**
- [ ] **AC-17-12** [L, ADVISORY]: P0 三品种采集前后摇 + 打断窗口手感人工走查,证据存档 `production/qa/evidence/`
- [ ] **AC-17-13b** [L, ADVISORY]: 差分结果去标注盲测可觉察。**⚠️ EXTERNAL(由 42 交付)+ BLOCKED-BY-OQ-17-2**(差分值未裁)—— 双挂标,禁记绿

---

## Implementation Notes

*Derived from ADR-011 Amendment B(主)/ ADR-018 / ADR-013:*

1. 动作链(单机 P0,主机=本地):3 的采集 action 触发 → 4 的 F-4.1 按 `(格距, 种类优先级, 稳定 id)` 选目标节点 → 17 裁决(Story 004 采前闸 → Story 002 掷骰 → Story 003 落流)→ 结果出参三态:成功 / `FailFullInventory` / `FailInsufficientRemaining`(+ `FailTimeoutUnresolved` 枚举位,P0 单机不可达,见护栏)。
2. 「库存满载」判定 = 调 20 的 `CanCarry`(整件拒绝语义,AC-20-06 同构 —— 17 不自判容量,零第二容量真值);满载失败 ⇒ 零事件零消耗(裁决在 Append 之前短路)。
3. 打断 = 前后摇窗口内动作终止(4/3 的表现态事件,17 侧无状态可回滚 —— 因为事件只在终裁成功时发,结构性零效果)。
4. 呈现 DTO 面:`GatherAffordanceDto`(可采提示,整数格邻域布尔/节点类,无余量数字)与 `AudioCueDto`(cue 无品质维)走 `PresentationDtoGuard` 递归反射(与 case-system AC-37-15 同一扫描器,零第二实现)。
5. Roslyn 扫描范围 = 17 的 sim 程序集求值路径(装载校验与呈现换算按 processing AC-18-02 同款边界豁免);复用 item-database story-011 已建的扫描件。
6. UX Flag 三项(采前线索 / 可采载体 / 三类失败载体)均 = 42 阻断呈现规格,17 只供整数输入;本故事实现输入侧,走查 AC 挂 EXTERNAL 不擅绿。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003:成功路径的事件与铸造(本故事只接失败短路与打断)
- Story 004:`remaining` 求值(本故事调用其闸接口)
- inventory-items epic:`CanCarry` 本体与满载判定(20 规则六)
- interaction-system(4)/ input-system(3)epics:身位光标与 action 映射本体(本故事为消费者)
- 42 的呈现规格与走查执行(UX Flag 三项,AC-17-07b/13b 的 EXTERNAL 半边)
- ADR-001 窄修订 / 45 轮:联机意图上行(TR-foraging-008 gap,P1b)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-17-07**: 满载零消耗。
  - Given: 20 侧 `CanCarry == false`(合成临界载重);一次采集意图。
  - When: 裁决。
  - Then: 返回具名 `FailFullInventory`;spy-sink 零事件;`remaining` 与 `gather_seq` 逐位不变;零成长。
  - Edge cases: 同 tick 先丢弃一格再采(满载解除)⇒ 成功;部分容纳 ⇒ 整件拒绝(禁「采半件」)。
- **AC-17-07c**: 三类具名分支。
  - Given: 17 的失败收敛枚举与裁决路径源码。
  - When: 静态检查(反射枚举成员 + Roslyn switch 完备性)。
  - Then: {Full, InsufficientRemaining, TimeoutUnresolved} 三支各独立具名、各归各的反馈钩子;不存在「单一 bool 失败」。
  - Edge cases: `TimeoutUnresolved` 分支存在但 P0 不可达(测试以直接调用枚举归一函数覆盖,不伪造网络时序)。
- **AC-17-08**: 打断零效果。
  - Given: 采集前后摇进行中,在打断窗口注入终止。
  - When: 动作结束。
  - Then: 零事件、余量不变、零成长;再采成功 ⇒ `gather_seq` 仍从原值 +1(打断不烧计数)。
  - Edge cases: 打断恰落在裁决帧边界 ⇒ 要么全成(三事件齐)要么全败(零事件),禁「有二无一」。
- **AC-17-09 / 09b**: 反射门。
  - Given: 17 输出 DTO 全集 + cue 载荷类型表。
  - When: `PresentationDtoGuard` 递归扫描 / 字段类型断言。
  - Then: 零原始品级/本可采到字段;`AudioCueDto` 载荷无品质维。
  - Edge cases: 嵌套集合/基类字段递归覆盖(承 AC-37-15 递归口径);新增 DTO 未挂守卫 ⇒ 守卫遍历程序集自动纳入(按类型发现,非登记清单)。
- **AC-17-10**: 零浮点扫描。
  - Given: 17 sim 程序集。
  - When: Roslyn 字面量 + 类型签名扫描。
  - Then: 求值路径零 float/double、零浮点字面量;`FixParse` 符号在运行期 17 代码里零出现(烘焙期义务不外溢)。
  - Edge cases: 注释/字符串内浮点词不计;呈现换算(如格距显示)不在扫描路径。
- **[L] 三条(07b / 12 / 13b)**: 人工走查/听测。
  - Given: 可玩构建 + 42 呈现规格落地后。
  - When: 玩家走查(三类失败可区分)/ 手感走查 / 去标注盲测。
  - Then: 证据 `production/qa/evidence/foraging-<ac>-<date>.md` + 主创签核;42 未交付前保持 EXTERNAL 未勾。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `unity/Assets/Tests/PlayMode/foraging_action_atomicity_test.cs` — must exist and pass
- Logic(反射/Roslyn 门): `unity/Assets/Tests/EditMode/Foraging/foraging_presentation_guard_test.cs`
- [L]: `production/qa/evidence/foraging-{07b,12,13b}-<date>.md`(走查轮,ADVISORY/BLOCKING 按 GDD 级别)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003(成功路径事件件),Story 004(采前闸),inventory-items Story 003(`CanCarry` 容量裁决),input-system / interaction-system epics(action 与目标选择,已有 Complete 件则直连)
- Unlocks: 数值轮差分夹具真值回填(AC-17-13 翻绿路径);42 走查轮(三项 UX Flag);OQ-17-6 裁定后的联机意图半边(经 ADR-001 窄修订,另开故事)

---

## Completion Notes

*(placeholder — to be filled at story completion)*
