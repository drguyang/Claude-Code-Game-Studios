# Epic: 输入与设备

> **Layer**: Foundation
> **GDD**: design/gdd/input-system.md
> **Architecture Module**: L4 边界层(呈现侧 · EDGE)
> **Status**: Ready
> **Stories**: 13 stories created (2026-09-25)

## Overview

输入与设备(3)是全案唯一「玩家的手」与「游戏世界」之间的转换层,职责恰好四条:① 把物理输入映射为动作(全案唯一一套 `.inputactions` 动作资产,K&M + Gamepad + OpenXR 三套绑重同表共存);② 把动作持久化为绑重 overrides(输入层唯一的落盘职责,不进 ADR-010 存档、不进三流);③ 把「手感量」与「判定量」分开 —— 急救动作走独立直读通道(`InputSystem.onAfterUpdate` 内直读,`L_input→pixel ≤ 50 ms`,不穿过 42 UI 事件栈),直读浮点值是手感层永不进流,交出的是全整数 `EmergencyReading`(tick 计持时 + 边沿);④ 把导航动作映射为 `FocusNavigationIntent` 单向只读视图 —— 3 只出意图观测,焦点移动的唯一真源 = 官方桥,归 42(D-A 裁定:意图不驱动移动)。玩家侧 3 的全部设计目标是「零存在感」:无按键提示浮层、无输入延迟可被察觉、无手柄焦点路径断头。本 Epic 覆盖架构 §5.3 的 **L4 边界层(EDGE)**:承重 ADR 定接口与通道形状,逐条动作词表与档位数值归 GDD(数值用户自己调)。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-011: 输入架构 | action-based 官方路线(动作资产 + 三套绑重 + overrides 持久化);急救动作独立直读通道(<50 ms 不穿 42 UI 栈);焦点导航接口归 3 · 呈现归 R-6;Amendment A「不加宽 IEmergencyInput,defer to 10」· Amendment B 联机急救改判 C 路(主机权威) | HIGH |
| ADR-013: 拟物 UI 框架 | UI Toolkit 主 + UGUI 补 world/XR;焦点单栈门;官方桥 NavigationMoveEvent 为焦点移动唯一真源(`TR-input-009`/`020` 消费) | HIGH |
| ADR-018: 音频架构 | 44 只触发 / 只渲染;`AudioCueDto` 整数语义;震动触觉语义归 8/10,3 只拥通道(`TR-input-015` 消费) | MEDIUM |
| ADR-010: 7a 持久化与存档格式 | 全二进制 codec;绑重 overrides 持久化**刻意排除**在存档之外(输入层唯一落盘职责,`TR-input-003` 消费) | MEDIUM |
| ADR-006: 定点域边界数据契约 | 运行期无 float→Fix 路径;直读 float = 手感层永不进流,判定结果全整数直接构造 SimEvent(`TR-input-007` 消费) | MEDIUM |
| ADR-009: 世界状态事件化边界 | Amendment I 追加 `EmergencyAttempt` / `EmergencyTreatmentApplied`;三段式意图事件 + 主机当下判距(`TR-input-008` 消费) | MEDIUM |

**Engine Risk**: **HIGH**(ADR-011 / ADR-013 并列最高)。ADR-011 的 Input System 6.3 具体行为为 post-cutoff、须实测(接口层为纯 C# 契约不受影响);ADR-013 的焦点桥 / UI Toolkit 运行时手柄导航为半可信假设,原型 spike 为前置。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-input-*`,Manifest Version 2026-09-21)。
> 计数(2026-09-23 Epic-3 对症复核批后实测):**21 条 = 14 covered + 3 partial + 1 gap + 3 no-adr-by-design**;
> **GDD Requirements Covered by ADRs = 17 / 21**(covered+partial);**Untraced = None**。
> 3 条 ◆(017/019/021)为 2026-09-23 Epic-3 对症复核批裁定降级 —— 归属件落在已 Approved 的 10 GDD(`emergency-procedures.md`),ADR 段结构上不承载,不计缺口、不阻塞 story。
> 唯一 gap `TR-input-016` **不是 ADR 缺口**(`adr: ADR-011` 已挂)—— 是**实测前置**(最低目标硬件 + 可跑 player),绑 `/test-setup`,story 不因此 blocked-by-ADR。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-input-001 | 全案恰有一个动作资产;InputSystemUIInputModule 与 3 的输入服务引用同一对象 | ADR-011 ✅ |
| TR-input-002 | 三套绑重同表共存;OpenXR 绑通用 XRController 布局 | ADR-011 ✅ |
| TR-input-003 | 绑重 overrides 持久化 = 输入层唯一落盘职责,不进存档不进三流 | ADR-011 + ADR-010 ✅ |
| TR-input-004 | 资产重建致 GUID 失效 → schema hash 失配 → 优雅清空,不静默采用错位 overrides | ADR-011 ✅ |
| TR-input-005 | 同机多玩家每玩家 Instantiate() 一份动作资产(克隆不继承 overrides、不得启用 UI map) | ADR-011 ✅ |
| TR-input-006 | 急救直读通道:onAfterUpdate 内直读,不穿 42 UI 栈,L_input→pixel ≤ 50 ms | ADR-011 ✅ |
| TR-input-007 | 直读 float = 手感层永不进流;判定结果全整数直接构造 SimEvent | ADR-011 + ADR-006 ✅ |
| TR-input-008 | 联机急救判定 = 主机权威(C 路):EmergencyAttempt 上行 → 主机 Judge+Append+Seq;本地预表现 | ADR-011 + ADR-009 ✅ |
| TR-input-009 | 导航动作 → FocusNavigationIntent 单向只读视图;3 不实现焦点移动(归 42) | ADR-011 + ADR-013 ✅ |
| TR-input-010 | 无同键双触发:同一物理输入不得同时绑官方桥 Navigation 与自实现路径 | ADR-011 ✅ |
| TR-input-011 | 设备态 Mixed 以最近一次有效输入为准;设备移除/失焦 → 合成 release | ADR-011 ✅ |
| TR-input-012 | 预缓存 InputAction 引用;零每帧字符串查找 | ADR-011 ✅ |
| TR-input-013 | QueryBinding → {device, bindingPath, iconKey};3 只给键名不给素材 | ADR-011 ⚠️ partial |
| TR-input-014 | P0 不交付改键 UI,只交付契约 + overrides 读写 + 默认三套绑重 | ADR-011 ✅ |
| TR-input-015 | 震动触觉:3 拥有通道,不拥有语义(何时震/多强归 8/10) | ADR-011 + ADR-018 ⚠️ partial |
| TR-input-016 | L_render / L_poll **实测**(均值达标 + 抖动有上界两条义务) | ADR-011 ❌ gap(实测前置,非 ADR 缺口) |
| TR-input-017 | EmergencyAction 枚举基数(P0 = 2 个急救动作) | ◆ no-adr-by-design |
| TR-input-018 | OpenInventory 的 P0 归属 —— 由 20 库存与物品提供 | — ✅(GDD 所有权:20) |
| TR-input-019 | Emergency 表达力:力道须有可采集通道(magnitude: int) | ◆ no-adr-by-design |
| TR-input-020 | 反幻想守门(42+48 联合):零按键提示浮层 + 手柄可走查纸页 | ADR-011 + ADR-013 ⚠️ partial |
| TR-input-021 | 跳过路径归 10;3 只提供通道形状,不实现跳过语义 | ◆ no-adr-by-design |

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 动作资产 · 单实例纪律与 Legacy 零引用门 | Integration | Ready | ADR-011 |
| 002 | F-3.1 轴处理算术性质与装载期断言 | Logic | Ready | ADR-006 + ADR-014 |
| 003 | 绑重 overrides sidecar 持久化 | Integration | Ready | ADR-011 + ADR-010 |
| 004 | 绑重 schema hash 稳定性(F-3.5) | Logic | Ready | ADR-011 |
| 005 | schema hash 失配优雅清空与跨版本备份 | Integration | Ready | ADR-011 |
| 006 | 意图边界与交出物(零 SimEvent · 全整数 · 聚合上行) | Logic | Complete | ADR-011 + ADR-006 + ADR-009 |
| 007 | 急救直读通道与输入更新相位 | Integration | Ready | ADR-011 |
| 008 | 设备态与呈现契约(QueryBinding · 合成 release · Mixed 迟滞) | Integration | Ready | ADR-011 |
| 009 | 焦点导航意图视图与单一真源 | Logic | Ready | ADR-011 + ADR-013 |
| 010 | 热路径零成本(预缓存 · Idle 零调用 · 零分配) | Logic | Ready | ADR-011 |
| 011 | L_input→pixel 延迟实测与 L_render/L_poll 分解 | Visual/Feel | Ready | ADR-011 |
| 012 | 开发者调试视图与构建剥离 | UI | Ready | ADR: N/A — GDD §UI 二纯呈现侧开发工具规格,无架构裁决需求(E2② CI 载体归 ADR-012,非治理件) |
| 013 | 反幻想守门登记面(P0 / 后阶段) | UI | Ready | ADR-011 + ADR-013 |

Counts: 5 Logic · 5 Integration · 1 Visual/Feel · 2 UI = 13 total.
29 条 AC 全覆盖(A4/A9/B1b/B2/E2/E3/F1 按子条拆入);全 ADR Accepted ⇒ 零 ADR-blocked story。

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/input-system.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Next Step

Run `/story-readiness production/epics/input-system/story-001-action-asset-identity.md` to validate before starting, then `/dev-story` to implement. Stories work in dependency order — each story's `Depends on:` field gates its start。
