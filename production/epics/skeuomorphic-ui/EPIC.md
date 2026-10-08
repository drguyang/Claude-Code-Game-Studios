# Epic: 拟物 UI 框架

> **Layer**: Foundation
> **GDD**: design/gdd/skeuomorphic-ui.md
> **Architecture Module**: L5 Presentation(PRES)
> **Status**: **Complete ✅ 2026-09-28**(18/18 stories)· ⚠️ **范围**见下方 §范围边界声明
> **Stories**: 18 stories created (2026-09-27) + **019 贴图接入(2026-10-03 补 · ◐ 部分完成:c/d/e/f ✅ · b Blocked)** + **020 M2 形态件解锁链(2026-10-08 补 · Complete ✅ 同日)** + **021 M2 形态件①(2026-10-08 补 · Complete ✅ 同日)** + **022 M2 形态件②墨乾湿两态(2026-10-08 补 · Complete ✅ 同日)** + **023 M2 形态件③急救零数字+可跳过(2026-10-09 补 · Complete ✅ 同日)** + **024 M2 形态件④状态反馈通道(2026-10-09 补 · Complete ✅ 同日)**

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

> ### ⚠️ TR-skeuoui-011 混计两量 —— 登记(2026-10-03 实测)
>
> **本条解释一个乍看矛盾的现象:为何 `TR-skeuoui-011` 是 `partial`,而其对应 story-001 已 `Complete`。**
> 实测发现:该 TR 条目把**两个不同的量**记成了一条 ——
>
> | 量 | 实现在哪 | 状态 |
> |---|---|---|
> | **① 注册表配额**(元件种类上限 / 每元件变体槽上限) | `SkeuoComponentRegistry.cs`(`MaxRegisteredComponents = 16` + `ValidateQuotas()`);AC-42-C3 | **已实现且有测**(story-001 37/37) |
> | **② 图集页数预算**(`Pages_frame` / `PAGES_MAX` + 溢出告警阈值) | **无实现** —— `tr-registry.yaml` 注「具体阈值仍待 spike」 | **未做** |
>
> ⇒ **两者不是同一个量,却共用一个 TR 号。** story-001 兑现的是 ①(所以它 Complete);
> TR 条目因 ② 未做而为 `partial`。**二者不矛盾,但共用一号会误导读者以为 story-001 欠账。**
>
> **处置(登记,不擅自拆号)**:本条只**登记该混计事实**,把 ② 归 **story 019**(与贴图绑定同批 ——
> 二者都要求「图集实际布局」这一前提,分开做会做完又拆)。
> **不拆 `TR-skeuoui-011` 为两条** —— 拆号触及 `tr-registry.yaml` + `traceability-index.md` 计数,
> 属**登记处变更**,须用户裁定;本轮只落「此处知悉两者不同」。
> ⇒ 引用 `TR-skeuoui-011` 时**须指明是哪一半**。

## ⚠️ 范围边界声明(2026-10-03 补 · 实测)

> **本条防的是一个已被实测证实的误读:「元件库 Complete」≠「贴图已接入」。**
> 本 epic 的 18 条 story **全部真做真测**(37/37 · 12/12 等,非假绿),但**无一条**的
> AC 要求「把贴图绑到元素上」—— story 001 的 6 条 AC(C1 九宫格区间 / C2 变量完整 /
> C3 图集配额 / C4 内联变体 lint / C5 硬编码字号 lint / C6 fallback 字体)**全为 USS 结构断言**。
>
> **实测(2026-10-03)**:
> - `unity/Assets/Gameplay.UI/Skeuomorphic/Textures/` 的 **16 张 `*-final.png` 已入库**(真图 0.9–4.6 MB)
> - 但这 16 个 GUID **在全库(非 `.meta`)引用数 = 0**,逐个查证无一例外
> - 全部 `.uss` / `.uxml` 内 `url(` / `background-image` / `resource(` **零命中**
> - 整条链是**按 USS 类名**走的:`Register(SkeuoElement.Paper, "paper", …)` → `element.AddToClassList("paper")`
>
> ⇒ **现状 = 元件库的骨架与护栏已建,皮未贴。** 故:
> **`Status: Complete` 只覆盖「契约 + 渲染通道 + 结构护栏」,不覆盖「贴图/图集绑定」。**
> 绑定工作归 **story 019**(见下),**不因本 epic 标 Complete 而推定已完成**。

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 拟物元件库基础(纸/卷轴/墨迹/印章 · 九宫格 · 主题变量 · 图集) | UI | **Complete ✅ 2026-09-27**(37 测全过) | ADR-013 |
| 002 | 焦点门状态机 + 焦点导航呈现桥 | Logic | **Complete ✅ 2026-09-27**(契约面验证) | ADR-013 + ADR-011 |
| 003 | 焦点导航边界(rank 数据断言 · 焦点悬空回退 · 空行反馈 · 同键双触发禁令) | Logic | **Complete ✅ 2026-09-27**(契约面验证) | ADR-013 + ADR-011 |
| 004 | 数据边界守卫(DTO Guard · 符号禁令 · 调试视图白名单 · IModalState) | Logic | **Complete ✅ 2026-09-27**(12 测全过) | ADR-013 + ADR-005 + ADR-008 |
| 005 | 世界空间锚点面片(敌人读数条 · 黄铜侧 · billboard 面片) | Integration | **Complete ✅ 2026-09-27**(7 测,5 passed + 2 inconclusive) | ADR-013 |
| 006 | 医馆面板渲染 + 刷新延迟契约 | Integration | **Complete ✅ 2026-09-27**(7 测,6 passed + 1 inconclusive) | ADR-013 |
| 007 | 数据边界收尾(设置壳不缓存 · 元件库唯一出口 · 墨龄数据路径) | Logic | **Complete ✅ 2026-09-27**(8 测全过) | ADR-013 + ADR-005 |
| 008 | 无血条替代反馈(纸面物理行为 · 印章 · 页边记号 · 拒绝权降级白名单) | Integration | **Complete ✅ 2026-09-27**(7 测,4 passed + 3 inconclusive) | ADR-013 |
| 009 | 焦点可见样式 + 无障碍钩子接口(字号缩放 / 动效缩放 / 焦点可见样式契约) | UI | **Complete ✅ 2026-09-27**(10 测,4 passed + 6 inconclusive) | ADR-013 |
| 010 | 焦点门时序与过渡(PlayMode 帧探针 · 过渡窗口 ≤1 frame · 不可重入) | Visual/Feel | **Complete ✅ 2026-09-27**(7 测,2 passed + 2 inconclusive + 3 skipped) | ADR-013 |
| 011 | 脉案页渲染 + 焦点导航(五通道区 + 两栏 · 焦点顺序面色→语声→呼吸→触感→病名) | UI | **Complete ✅ 2026-09-27**(11 测,5 passed + 5 inconclusive + 1 skipped) | ADR-013 |
| 012 | 存档位界面渲染 + 焦点 | UI | **Complete ✅ 2026-09-27**(9 测,5 passed + 3 inconclusive + 1 skipped) | ADR-013 |
| 013 | 库存容器界面渲染 + 焦点(翻页制 · ≤12 件/屏 · 器物有重量) | UI | **Complete ✅ 2026-09-27**(10 测,6 passed + 3 inconclusive + 1 skipped) | ADR-013 |
| 014 | 设置界面壳(总线音量 + mono · 条目语义归 44 · 42 不缓存) | UI | **Complete ✅ 2026-09-27**(10 测,6 passed + 3 inconclusive + 1 skipped) | ADR-013 + ADR-018 |
| 015 | 教学界面(纸堆翻页走查 · 零按键提示浮层 · 手柄单机走查) | UI | **Complete ✅ 2026-09-28**(契约面验证) | ADR-013 + ADR-011 |
| 016 | 教学纸近景(ModalId.PaperCloseup48 · 世界内单张纸近景 · 走近摊纸) | UI | **Complete ✅ 2026-09-28**(9 测,6 passed + 2 inconclusive + 1 skipped) | ADR-013 |
| 017 | 敌人读数条完整实现(黄铜面片材质 · 蚀刻刻度 · 淡入淡出 · 六态机映射) | Visual/Feel | **Complete ✅ 2026-09-28**(契约面验证) | ADR-013 |
| 018 | 开发者调试视图(仅 Development Build · 焦点栈/元件库/DTO 绑定结果 · 不显示游戏数值) | UI | **Complete ✅ 2026-09-28**(7 测,5 passed + 1 inconclusive + 1 skipped) | ADR-013 |
| 019 | 贴图接入(16 张 `*-final.png` → USS 元件族 · 九宫格 slice 对齐图集真实切图 · 图集页数实测) | UI | **部分完成 ◐**(019-c/d/e `Complete ✅ 2026-10-05` · **019-f `Complete ✅ 2026-10-08`** · 019-b Blocked(PAGES_MAX spike);承上方 §范围边界声明) | ADR-013 |
| 020 | M2 形态件解锁链(切图冻结 → 黄铜 2px 出图 → 绑定回填) | UI/美术前置 | **Complete ✅ 2026-10-08**(三步全交付:冻结件落盘 + 黄铜 2px 入库 + 绑定回填/C8 门接棒) | ADR-013 + `milestones §三` |
| 021 | M2 形态件①(脉案线格 · 空行等重 · 焦点明度轴) | UI/Visual-Feel | **Complete ✅ 2026-10-08**(三步全交付:格线挂行 + 等重声明层 + 贴图级明度轴 0.266 ≥ 0.12;B2 转正、M2 形态件 1/4) | ADR-013 + `milestones §三` + art-bible G1/G3 |
| 022 | M2 形态件②(墨乾湿两态 —— 主题色明度轴 · 双态接图 · 洇开语义) | UI/Visual-Feel | **Complete ✅ 2026-10-08**(三步全交付:两态主题色 + `.ink-wet`/`.ink-dry` 接冻结图 + 洇开覆盖方向判据;M2 形态件 2/4) | ADR-013 + `milestones §三` + art-bible §4.5/G4 |
| 023 | M2 形态件③(急救零数字 + 可跳过 —— 跳过入口元件 · 零数字角标门 · 焦点落点形态) | UI/Visual-Feel | **Complete ✅ 2026-10-09**(三步全交付:`.skip-entry` 元件 + G2 零数字门 + 焦点落点/属性交集判据;零新变量零新图;M2 形态件 3/4) | ADR-013 + `milestones §三` + art-bible G2 + `O-10-1` |
| 024 | M2 形态件④(一条真实状态反馈通道 —— 体征词条→五通道分发 · 通道值区形态 · 真表端到端) | UI/Visual-Feel | **Complete ✅ 2026-10-09**(三步全交付:`SignChannelBinder` 分发纯函数 + `.channel-reading`/`-negative` 两态类 + 34 行真表端到端;零新图零新变量;M2 形态件 4/4) | ADR-013 + `milestones §三` + diagnosis-system 规则五/F-8.2 |

Counts: 5 Logic · 3 Integration · 5 Visual/Feel · **10 UI** = **24 total (23 Complete + 1 ◐ = story-019)**。
43 条 AC 全覆盖(按子条拆入);全 ADR Accepted ⇒ 零 ADR-blocked story。
⚠️ **story 019 是范围补件,非原 18 条的追加** —— 它填的是「贴图绑定」这个原本**没有任何 story 覆盖**的面
(见上方 §范围边界声明)。**上表 18 条 Complete 不因 019 Ready 而失效** —— 二者覆盖不同的面。
⚠️ **story 020(2026-10-08 补)是 019 分档 + G-1b 的 M2 汇总执行 story** —— 步①=019-f、步③=019-a 余项,
**状态仍归 019 各分档,020 只引用与验收**(双真源防线,见 story-020 §Context)。

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/skeuomorphic-ui.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Next Step

**Epic 全部 18 个 story 已完成**;补件 **story 020(M2 形态件解锁链)Complete ✅ 2026-10-08**、
**story 021(M2 形态件①)Complete ✅ 2026-10-08**、**story 022(M2 形态件②墨乾湿两态)Complete ✅ 2026-10-08**、
**story 023(M2 形态件③急救零数字+可跳过)Complete ✅ 2026-10-09**
(跳过入口元件 + G2 零数字角标门 + 焦点落点形态三步全交付,art-bible G2 转可验)、
**story 024(M2 形态件④状态反馈通道)Complete ✅ 2026-10-09**
(通道分发纯函数 + 通道值区两态类 + 真表端到端三步全交付;**M2 形态件 4/4 齐**)——
补件 **story 019(贴图接入)为 ◐ 部分完成(c/d/e/f ✅ · b Blocked)** ——
019-b 的 `PAGES_MAX` spike 卡 atlas 页数预算(AC-42-C9),**不卡 M2 形态件**;截图签核目视义务仍归 019-d。
下一步 = M2 形态件 4/4 已齐(①②③④ 全交付);余 = 9/数据绑定轮的运行时接线(形态件④的施加点)· 019-b spike · 或跨系统待办(45 联机夹具 / ADR-023 spikes / 七屏走查)。

---

## 里程碑归属

| Story | 里程碑 | 依据 |
|---|---|---|
| 001–018 | —(已 Complete,归 M3 计数) | `production/milestones/README.md` §五 M3 |
| **019** | **M2 Vertical Slice**(硬前置) | `milestones/README.md` §三 Exit Criteria「**五族切图与 atlas 布局冻结**」(⚠️ 2026-10-04 D 订正:原文写「纸/墨/铜三族」为措辞误,实测五族) + 第 5 条「4 项形态件」—— **① 脉案线格/空行/明度轴压在九宫格切图上,② 墨乾湿两态压在墨迹 brush 上**。⇒ **不接图则 ①② 无法交付**,019 是它们的直接前置,非额外美化。◐ **c/d/e/f ✅(2026-10-08 冻结轮收口)** —— 切图冻结件落盘 + 绑定回填,形态件 ①② 的**切图前置已解**;余 019-b(atlas 页数)不卡 M2,见 story 文件 §状态拆分 |
| **020** | **M2 Vertical Slice**(硬前置汇总) | `milestones/README.md` §三 —— 019 分档 + G-1b(黄铜 2px)的 **M2 视角汇总执行 story**;✅ **Complete 2026-10-08**:三步全交付 ⇒ M2「焦点黄铜 2px」条勾、「五族冻结」条切图半勾(余 atlas 半归 019-b) |
| **021** | **M2 形态件①**(脉案线格/空行/明度轴) | `milestones/README.md` §三「4 项形态件」① + art-bible **G1/G3**;✅ **Complete 2026-10-08**:线格 border 零新图 + 空行等重声明层 + 贴图级明度轴 ⇒ ① 交付(形态件 1/4);B5 勾、B2 转正;布局探针半归桌面走查 |
| **022** | **M2 形态件②**(墨乾湿两态) | `milestones/README.md` §三「4 项形态件」② + art-bible **§4.5/G4**(手感本体);✅ **Complete 2026-10-08**:两态主题色明度轴(湿=权威浓墨/干=提案色沉)+ `.ink-wet`/`.ink-dry` 接冻结图(slice 0)+ 洇开覆盖方向判据 ⇒ ② 交付(形态件 2/4);切换时机归数据绑定轮 |
| **023** | **M2 形态件③**(急救零数字+可跳过) | `milestones/README.md` §三「4 项形态件」③ + art-bible **G2**(反数值化)+ `emergency-procedures` 规则六之甲(`O-10-1` 跳过入口 owner=42);✅ **Complete 2026-10-09**:`.skip-entry` 元件形态 + 零数字角标门 + 焦点落点/属性交集判据 ⇒ ③ 交付(形态件 3/4);施加点/Idle 时序归 10 轮 |