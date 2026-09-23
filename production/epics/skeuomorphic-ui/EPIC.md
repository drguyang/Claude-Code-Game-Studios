# Epic: 拟物 UI 框架

> **Layer**: Foundation
> **GDD**: design/gdd/skeuomorphic-ui.md
> **Architecture Module**: L5 Presentation(PRES)
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories skeuomorphic-ui`

## Overview

拟物 UI 框架(42)是全案**唯一被允许画界面的系统**,交出两样东西:① 一套拟物元件库 —— 纸面 / 卷轴 / 墨迹 / 印章四种元件 + 九宫格切片 + 主题变量,构成「一本书 / 一张纸 / 一个出诊箱」的视觉语汇,任何界面都从这套元件拼出;② 一条焦点呈现通道 —— 手柄没有指针而全案手持界面「全是纸」,42 用**官方桥**把导航动作翻成焦点移动事件,由引擎自己的焦点系统选邻居(42 不重复实现焦点算法),「同一时刻只有一个界面接收导航」由**焦点单栈门**保证。框架两栈(ADR-013):平面拟物 UI(脉案 / 存档位 / 纸质地图 / 教学)走 **UI Toolkit**(UXML + USS 元件库);世界空间与 VR 走 **UGUI world canvas**(VR 必须 World Space;VR 推 P1a,不在 P0 关键路径)。玩家侧目标是「物生于理」:纸就是纸、墨就是墨、手翻页就是手翻页 —— 零按键提示浮层。42 **只渲染、永不持有游戏状态**(符号级禁写,ADR-013 §9 C3 呈现三件套之首)。GDD Approved(2026-09-17,MAJOR REVISION 当日修订,免二轮);TR 12 = 8 covered + 4 partial(004/005/010/011 挂 spike / OQ 残余,禁借绿)。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-013: 拟物 UI 框架 | UI Toolkit 主 + UGUI 补 world/XR;P0 定两栈接口与元件库契约,VR 推 P1a;拟物视觉 = 自建 USS 元件库 + UXML;焦点单栈门 + 官方桥 NavigationMoveEvent(禁同键双触发);IModalState + ModalId 闭集(2026-09-21 扩 7 员);42 只渲染不持状态 | HIGH |
| ADR-011: 输入架构 | 焦点导航接口归 3(action → FocusNavigationIntent),呈现归 42(官方桥);无同键双触发(TR-input-009/010 消费侧) | HIGH |
| ADR-025: 契约程序集清单 | Gameplay.UI 装配 = 焦点单栈门的编译期表达;42 = 唯一 UI 程序集(TR-skeuoui-003/005 消费) | LOW |
| ADR-005: 确定性模拟与状态同步模型 | 墨龄 = ITickProvider 的纯函数,禁墙钟(TR-skeuoui-010 消费);表现层 float 经 IVitalsQuery → VitalsDto 隔离 | HIGH |
| ADR-008: 病例事件流 | disease_id 不进呈现层;PresentationDtoGuard 递归反射扫描(TR-skeuoui-006 消费) | HIGH |
| ADR-009: 世界状态事件化边界 | 表现态与模拟态分离;42 只读 VitalsDto | MEDIUM |
| ADR-010: 7a 持久化与存档格式 | 7b 存档位 UI 契约(Depends On) | MEDIUM |

**Engine Risk**: **HIGH**(ADR-013 / ADR-011 / ADR-005 / ADR-008 并列最高)。ADR-013 的 UI Toolkit 6.3 运行时焦点导航 / 官方桥 / 自定义材质 / 图集 / 无障碍均 post-cutoff,须 spike(§6.6 假设 6「运行时手柄焦点导航可用」半可信 —— R-A spike 未跑,`TR-skeuoui-004` 降级是否触发未知)。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-skeuoui-*`,Manifest Version 2026-09-21)。
> 计数(2026-09-23 实测):**12 条 = 8 covered + 4 partial + 0 gap + 0 no-adr-by-design**;
> **GDD Requirements Covered by ADRs = 12 / 12**(covered+partial);**Untraced = None**。
> 4 条 partial 全为**禁借绿**登记:004 焦点桥 spike 未跑(R-A)、005「51 经 42」登记面欠账、
> 010 墨龄 OQ-42-14 未裁、011 图集阈值 PAGES_MAX 待 spike —— 均不阻塞 epic 建置,story 按
> BLOCKED-BY 处理。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-skeuoui-001 | 全案恰一个 EventSystem,UI Toolkit 与 UGUI 两栈共用 | ADR-013 ✅ |
| TR-skeuoui-002 | z 序 = 域内序表(跨域不得互压,域划分单一定义处) | ADR-013 ✅ |
| TR-skeuoui-003 | 焦点单栈门:同一时刻仅一栈接收导航意图流 | ADR-013 ✅ |
| TR-skeuoui-004 | 焦点移动 = 官方桥 NavigationMoveEvent+FocusController(不自实现);降级路径受 AC-42-B4 类型面约束 | ADR-013 ⚠️ partial(R-A spike 未跑) |
| TR-skeuoui-005 | 42 = 唯一 UI 程序集;51 调试视图经 42 渲染(不直画) | ADR-025 + ADR-013 ⚠️ partial |
| TR-skeuoui-006 | PresentationDtoGuard 递归反射扫描:disease_id 等语义禁入呈现 DTO | ADR-013 + ADR-008 ✅ |
| TR-skeuoui-007 | 42 只渲染永不持有游戏状态(符号级禁写) | ADR-013 ✅ |
| TR-skeuoui-008 | 世界锚点体征面片:P0 最小实现 = UGUI world canvas | ADR-013 ✅ |
| TR-skeuoui-009 | IModalState 契约:模态闭集(7 员,2026-09-21 起)与栈语义 | ADR-013 ✅ |
| TR-skeuoui-010 | 墨龄(纸张老化呈现)= ITickProvider 的纯函数(禁墙钟) | ADR-005 ⚠️ partial(OQ-42-14 未裁) |
| TR-skeuoui-011 | 图集护栏:Pages_frame 预算与溢出告警阈值 | ADR-013 ⚠️ partial(PAGES_MAX 待 spike) |
| TR-skeuoui-012 | 无障碍四钩子:字号缩放/高对比/焦点指示/减少动效在元件库级内建 | ADR-013 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/skeuomorphic-ui.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Next Step

Run `/create-stories skeuomorphic-ui` to break this epic into implementable stories.