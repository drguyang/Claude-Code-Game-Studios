# Story 003: 混音拓扑与快照纪律

> **Epic**: 音频系统
> **Status**: Ready
> **Layer**: Foundation(系统分类;实现落表现层 L5)
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/gdd/audio-system.md`(§States · §Event Table Schema · §UI Requirements 注册表)
**Requirement**: TR-audio-003(混音拓扑单一出处:七总线 + 快照清单)· TR-audio-004(`DialogueFocus` 非拟物例外通道)

**ADR Governing Implementation**: ADR-018: 音频架构(主:§三 七总线 + 快照禁播报 + Aux/Reverb send)· ADR-020 §七(`AudioListener` 单挂点,引用)
**ADR Decision Summary**: 快照切换一律由玩家行为/场景状态驱动,绝不播报病人状态;混音拓扑单一定义含 reverb send(否则快照只能改增益改不了混响)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(快照×`SetFloat` 交互 6.3 须 spike,挂 `OQ-44-5`)
**Engine Notes**: 快照按定义插值其捕获的全部 mixer 参数 ⇒ **两级组结构纪律**(快照 duck 组 / 玩家音量组,交集=∅)是本 story 的资产级断言对象;`.mixer` 用 Force Text YAML 扫描。

**Control Manifest Rules (this layer)**:
- Required: 快照只由玩家行为驱动(调用点白名单)· 拓扑七总线 + send 单一出处 · 快照只拥有 A/M/Master 电平(2026-09-25 二轮 F7=甲)
- Forbidden: 快照由病程/状态变化触发(负向夹具)· 「抬 Stethoscope」归快照(已废,归贴耳脚本动作)· 玩家 exposed ∩ 快照参数集 ≠ ∅
- Guardrail: 分叉的只有音 —— mixer 资产是共享渲染资源,改动须过资产扫描门

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story:*

- [ ] **AC-44-C1**:`AudioMixerSnapshot.TransitionTo` 全部调用点(IL 扫描)∈ **快照切换调用点白名单**(玩家行为/世界可感知声两类);**负向夹具**:从体征/病程变更 handler 调 `TransitionTo(DialogueFocus)` ⇒ 断言失败;无任何快照由非拟物状态变化触发。
- [ ] **AC-44-E3**:`.mixer` 资产扫描 —— ① 七总线组齐备;② Aux/Reverb send 拓扑清单单一出处(新增 send = 构建失败);③ reverb preset 切换所有者 = 44(触发输入 = 玩家所在房间格,表现层派生)。
- [ ] **注册表 AC②**(§UI Requirements):快照参数集 ∩ 玩家 exposed 集 = ∅(两级组结构;Force Text YAML 断言)。
- [ ] **注册表 AC③**:注册表总线部分 == 七总线(计数 = 7,含 Master —— 原 Tuning 行漏 Master 已补)。

---

## Implementation Notes

*Derived from ADR-018 §三 + GDD §States(2026-09-18 增补 + 2026-09-25 二轮)*:

- **两级组结构**:每总线拆「快照 duck 组」+「玩家音量组」;滑块写玩家组;快照捕获 duck 组 —— 玩家值不被打回(GDD States 注的结构纪律,`AC-44-05` 暴露面与快照面由此分离)。
- 快照五员(Default / StethoscopeFocus / DialogueFocus / Paused / VRComfort[P1b])按 GDD States 表;`StethoscopeFocus` **只压 Ambience/Music 电平**(F7=甲;贴耳抬升/bus 路由 = Story 009 的脚本动作)。
- `DialogueFocus` 仅限玩家主动发起对话(病人自发呻吟/咳嗽不触发 —— 规则三 2026-09-18 修订)。
- 调用点白名单 = 登记集常量(C#);扫描用 `System.Reflection.Metadata`(b4 同格)。
- reverb:预设切换逻辑归本 story 的 44 实现面(触发数据 = 房间格,来自烘焙逻辑层表现侧派生,不进流);`reverb_preset_{indoor,outdoor,cave}` 素材资产随 Story 010 管线。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 009: 贴耳切换的 bus 路由脚本动作与交接窗
- Story 010: reverb 素材资产入库与缺失门
- Story 011: 注册表的壳侧消费([①] 断言的另一半在 42)

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-44-C1**: 调用点白名单。Given: 44 快照切换实现。When: IL 扫描 `TransitionTo` 调用点。Then: 每点 ∈ 登记集;负向:注入状态驱动调用点 ⇒ 红;`DialogueFocus` 调用点仅来自玩家对话入口方法。
- **AC-44-E3**: mixer 资产扫描。Given: `.mixer` 资产(Force Text YAML)。When: 扫描。Then: 七组齐备 + send 清单与登记集逐项相等;新增未登记 send ⇒ 红;reverb 切换方法归属 44 命名空间。
- **注册表 ②**: Given: 快照参数捕获清单 + `settings_visible` 注册表。When: 求交集。Then: = ∅;负向:把 `bus_volume_music` 加入某快照捕获 ⇒ 红。
- **注册表 ③**: Given: 注册表。When: 计数总线条目。Then: = 7(含 master)。

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `tests/integration/audio_system/mixer_topology_test.cs` — must exist and pass(mixer 资产扫描 + 调用点扫描)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(装配边界)
- Unlocks: Story 004(快照是听诊聚焦的载体)· 009 · 011(注册表 ③)
