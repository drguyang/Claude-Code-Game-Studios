# Story 011: 设置暴露面与归零机制

> **Epic**: 音频系统
> **Status**: Ready
> **Layer**: Foundation(系统分类;实现落表现层 + 边界层 store)
> **Type**: Config/Data
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/gdd/audio-system.md`(§UI Requirements 设置暴露面注册表 · 规则九 · AC-44-12/13/15/19)
**Requirement**: ⚠ 无专属 TR(无障碍/设置面 GDD AC 直承;七路注册表挂 **TR-audio-003**;字幕挂 **TR-audio-005 族**;registry 见报)
**ADR Governing Implementation**: ADR-018: 音频架构(主:mono/总线音量义务 + 44 只渲染)+ ADR-013(控件呈现归 42)· ADR-014(出厂默认 = 烘焙分区)
**ADR Decision Summary**: 44 拥有闭枚举 `settings_visible` 注册表与出厂默认;壳条目表 = 镜像;归零三步(写默认 → 推 mixer → 落 sidecar)承 OQ-SS-5=甲。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(纯数据 + store 机制;sidecar 承 3 规则四先例)
**Engine Notes**: 字幕呈现归 42(F9=乙:上游发射点扇出,44 不新增出向口);归零控件形态 = 42 元件轮(OQ-SS-7=甲 分工)。

**Control Manifest Rules (this layer)**:
- Required: 注册表 = 单一出处(壳表镜像)· 归零三步缺一无效 · 玩家 exposed ∩ 脚本面 = ∅
- Forbidden: 壳直写非注册表参数 · store 绕过 sidecar 落盘(归源无效)· 44 新增到 42 的出向接口(违只入不出)
- Guardrail: 分叉的只有音 —— 设置值是设备偏好(与存档位解耦,OQ-SS-5=甲),不得进 7a 存档头

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story:*

- [ ] **注册表 AC①**(§UI Requirements):壳消费集 ⊆ `settings_visible` 闭枚举(8 条 = 7 总线音量含 Master + mono;越集 = 断言失败)。
- [ ] **AC-44-13**:提供 **mono 选项**(`mono_enabled` ∈ 注册表,出厂默认入烘焙分区)。
- [ ] **AC-44-15**(双半):[A] 上游语声/口述 cue 发射双投递(cue→44,文本→42);文本本体 = 事件表 `subtitle_text`,**表键集 ⊇ 语声 cue 集**(缺失 = 构建失败;含 48 口述);[L] 字幕存在性听测签署(WCAG 1.2.2)。
- [ ] **AC-44-19**(OQ-SS-7=甲):归源(写默认 → 推 mixer → **落 sidecar**)后 store == 出厂集;**重启后仍 == 出厂集**;负向:跳过 sidecar 落盘 ⇒ 红。
- [ ] **AC-44-12** [L](文档判据):三条具名项在位 —— 视觉替代引 `AC-13-F1` · P0 非目标「隔墙听觉无视觉等价」(a11y L-3 已登)· mono/总线音量/语声字幕三义务(本 story 与 012/013 承接)。

---

## Implementation Notes

*Derived from GDD §UI Requirements 注册表(2026-09-25 新增)+ OQ-SS-3/SS-5/SS-7 三裁(2026-09-25)*:

- **注册表结构**:`settings_visible` 行含参数名/类型/区间(`0–1`,dB −80–0)/出厂默认/`player_group` 字段/题签语义源;物理落点 = 44 侧数据(随事件表或独立注册文件,ADR-014 烘焙)。
- **OQ-SS-3=甲**:数据层恒 7 路;`player_group` 只影响壳呈现分组(归 42 元件轮)。
- **归零三步**与边界层 store:store = 默认值真源消费者 + mixer 推送方 + sidecar 落盘方(44 不持值 —— 只渲染宪法);sidecar 承 3 规则四模式(schema 头)。
- **字幕 F9=乙**:上游(13/48)发射点解析 `subtitle_text` 后扇出两消费方;44 无新出向口(只入不出不破);48 的口述经同通道(`AC-48-15`)。
- 与 42 的分权:控件形态/焦点/手柄输入 = 42 元件轮;本 story 交付数据面 + 机制面。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: `subtitle_text` 字段的 schema 校验([A] 半的表侧)—— 本 story 承消费与键集覆盖断言
- Story 012: E2 性能、18 乐层
- 42 元件轮:归零钮/滑杆的呈现与焦点(OQ-SS-7=甲 分工)

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **注册表 AC①**: Given: 注册表 + 壳行目录。When: 求差集。Then: 消费 ⊆ 注册表;负向:壳引用未登记参数 ⇒ 红。
- **AC-44-13**: Given: 注册表。When: 查 `mono_enabled`。Then: 存在 + 出厂默认 ∈ 烘焙分区 + mixer 暴露映射正确。
- **AC-44-15 [A]**: Given: 语声 cue 全集。When: 校验 `subtitle_text` 键集。Then: ⊇ 语声 + 48 口述 cue 集;缺键 ⇒ 构建失败;[L]:字幕听测签署。
- **AC-44-19**: 归零机制。Given: 被改坏的 store 值。When: 归源。Then: 三步后 store==出厂;**重启进程复验仍==出厂**;负向:跳过第三步 ⇒ 重启后红。
- **AC-44-12** [L]: 文档三件在位核对(a11y L-3 行 + F1 引用 + 三义务 AC 存在)。

## Test Evidence

**Story Type**: Config/Data
**Required evidence**:
- 烘焙/注册表断言测试 + `production/qa/evidence/settings-exposure-evidence.md`([L] 半 + 归零重启复验记录)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 · 002 · 003(E3 七路)· 006(变体表承载字幕文本结构)
- Unlocks: 42 元件轮的壳行镜像可开工;OQ-SS-7 控件形态裁定后归零钮可进架
