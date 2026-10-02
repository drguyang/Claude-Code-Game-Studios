# Story 004: 三媒体调度与纸近景模态

> **Epic**: 教学与引导
> **Status**: Ready
> **Layer**: Feature
> **Type**: UI
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/tutorial-and-onboarding.md`(规则二 三种媒体 · 规则六 与 37/13/42/44/6 的边界 · 规则五 不设门 · AC-48-06 [L] / AC-48-15 [L] / AC-48-09 / AC-48-10)
**Requirement**: TR-tutorial-005(纸近景 = `ModalId` 闭集成员,partial ADR-013 —— 枚举已注册,呈现细节待 42 元件回写)· TR-tutorial-006(联机教学演出仲裁 —— **gap**,归 45 GDD 轮,本 story 运行面 BLOCKED-BY-45)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-013(主): 拟物 UI 框架 · ADR-018: 音频架构(口述 = 44 触发,白名单内)· ADR-016 §六(13 示范 = 病人 AI 的一次性片段,42 播)· ADR-020(示范镜头不改玩家相机管线)
**ADR Decision Summary**: 三种媒体:① **口述** = NPC( mentor )声 via 44 cue + **字幕必过 AC-44-15 管线**(教学口述零字幕是禁项);② **示范** = 13 经 42 的一次性片段接口(`actor=mentor + clipKey`),播完即散,不循环;③ **纸上的图** = 世界内静态道具(6 摆放、不可拾取),**走近 → 42 近景模态 `ModalId.PaperCloseup48`**(闭集第七员,ADR-013 §十-B 已注册)—— 摊开 / 合上走焦点单栈门。48 只**调度**,不渲染(42)、不发声(44)、不演动作(13/27 经 42)。与 37 单向:48 读 37 投影,37 零教学分支(AC-48-10)。可无视教学零惩罚(走查 AC-48-07 [L] 归 Story 005)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH
**Engine Notes**: 承重 = 近景模态的焦点单栈与手柄路径(ADR-013 §6.6 假设 6 同源);口述字幕 = AC-44-15 管线若未就位则本 story 的 [L] 走查 BLOCKED-BY 音频 epic,禁借绿。

**Control Manifest Rules (this layer)**:
- Required: 媒体触发全部经既有 DTO 通道(`AudioCueDto` / 42 片段接口 / 近景模态请求);调度器无可变游戏状态(只有媒体意图队列,进程内易失)
- Forbidden: 48 直接持有 / 操作 `AudioSource`、`Animator`、相机 transform;叠加层 HUD 元件(第一铁律,守卫在 Story 005);示范片段循环 / 重播
- Guardrail: 同刻至多一路「纸近景」模态(焦点单栈门);口述可与示范并发,但同 step 的同类媒体不叠播(调度表钉死,时序值归用户)

---

## Acceptance Criteria

*From GDD `design/gdd/tutorial-and-onboarding.md`, scoped to this story:*

- [ ] Fire 意图 → 三媒体调度正确:anchor Fire 播口述(44 cue + 字幕),完成 Fire 按需播示范 / 纸指引;键全部来自 Story 001 的 `.cooked`(cueId / clipKey / paper 条目),无硬编码
- [ ] 口述字幕 = AC-44-15 管线路径(narration text 进字幕队列,**零「无字幕口述」路径**);字幕可见性走查 [L](AC-48-06 子项)
- [ ] 示范 = 13 经 42 的一次性片段:播一次即散,不可被教学状态重播(重燃守住在 Story 003,呈现侧只消费)
- [ ] 纸近景:静态道具(6 摆放,非拾取物)走近 → 请求 `ModalId.PaperCloseup48` 摊开;合上回 Explore 焦点;`IModalState` 可见「有模态摊开」(承 ADR-013 Amendment A 只读口)
- [ ] **AC-48-10**:37 代码面零教学分支(grep + 37 的 AC 回扫,本 story 是联动验证方);48→37 只读单向
- [ ] **AC-48-09**:两玩家投影独立 → 各自媒体互不干扰(P0 单机夹具 = 双 id 两路调度;联机仲裁 **BLOCKED-BY-45 / TR-tutorial-006**,禁借绿)
- [ ] **AC-48-06 [L]**:三媒体可读性走查(口述+字幕成对 / 示范能看懂动作 / 纸图文自含)+ 无显式指令词(词族 = Story 001 扫描器复用)[L]
- [ ] **AC-48-15 [L]**：纸近景手柄走查(走近→摊开→翻页→合上,无指针依赖) + 教学口述复用 AC-44-15 字幕管线(字幕必过 AC-44-15 管线;教学口述零字幕是禁项)

---

## Implementation Notes

*Derived from ADR-013 / ADR-018 Implementation Guidelines:*

1. 调度器 `TutorialDispatcher`:输入 = Story 003 的 Fire 回调(带 step id + phase),输出 = 三类 DTO 意图;内部队列 = 进程内易失,**不落任何持久结构**(守 Story 002 判据)。
2. 口述并发规则(P0 从简):同刻单口述;示范与口述可并发但示范不吞口述(时序旋钮值归用户,调度表留槽)。
3. 纸近景复用 42 的通用近景模态机制(与 39 脉案近景同族不同档):`PaperCloseup48` 的 UXML = 标题 + 正文 + 插图槽(illustrationKey → 图集);**静态图允许**(AC-48-02 禁的是叠加层 Sprite,纸内容是世界内物件纹理)。
4. 走近触发判据 = 表现层距离(不进 sim、不写流;与 ADR-020 §四 表现态纪律一致)—— 用 6 的世界格近邻,不做物理 Trigger 常驻(性能护栏)。
5. 字幕:接 AC-44-15 管线若该 epic 未就绪,本 story 以「字幕接口 + 占位呈现」交付并标 BLOCKED-BY,自动化部分照跑。
6. ⚠️ **数值冻结**:全部节奏 / 延迟 / 并发数时序值归用户手感轮(承 UX Flag ①「三媒体节奏规格缺失」—— 走查前须补 spec)。

---

## Out of Scope

- [Story 003]: 边沿触发引擎(本 story 是其回调消费方)
- [Story 005]: 零叠加层 / 零按键提示守卫 + 无视教学零惩罚走查(AC-48-01/02/05/07/12/17)
- 44 侧音频 cue 表本体(音频 epic)· 13 示范片段资产(AI epic)· 6 的纸道具摆放(世界 epic)
- 联机演出仲裁实现(45 轮,TR-tutorial-006 gap)
- VR 形态(P1a)

---

## QA Test Cases

**[UI story — walkthrough + automated scan]:**

- **AC-1**: 调度表消费正确
  - Given: 夹具依次 Fire 六步 anchor
  - When: 跑调度器
  - Then: 每步出对口述 cue(键 = `.cooked` 值);无未定义键(与 Story 001 引用完整性闭环)
  - Edge cases: Fire 风暴(连边沿)不叠同类(同刻单口述)

- **AC-2**: 纸近景模态生命周期
  - Given: 玩家(手柄焦点)走近纸道具
  - When: 触发摊开 → 翻页 → 合上
  - Then: `ModalId.PaperCloseup48` 置位/清位各恰 1 次;焦点单栈门在摊开期拒其他栈;合上后 Explore 焦点还原 [L](AC-48-15)
  - Edge cases: 摊开期中断(读档)⇒ 模态不留存(易失)

- **AC-3**: 37 零教学分支(AC-48-10)
  - Given: 37 程序集
  - When: 符号扫描 `tutorial|onboarding|guide` 词族 + 37 AC 清单回扫
  - Then: 零命中
  - Edge cases: 48→37 只读接口存在且无反向引用(编译期)

- **AC-4**: 双路独立(AC-48-09 单机子集)
  - Given: p1/p2 两路 Fire 交错
  - Then: 字幕 / cue 意图按路不串
  - Edge cases: 联机运行面 BLOCKED-BY-45

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/tutorial-onboarding/story-004-three-media-walkthrough.md`(AC-48-06/15 [L],含截图 + 主创签核)+ `unity/Assets/Tests/EditMode/Tutorial/tutorial_dispatcher_test.cs`(AC-1/3/4 自动化面) — must exist
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003(Fire 边沿)· Story 001(键空间)· 42 近景模态元件(并行)· 44 字幕管线(AC-44-15;未就位则子项 BLOCKED-BY)· 6 纸道具(并行)
- Unlocks: Story 005(完整流程走查依赖媒体链可跑)· `/ux-review` 48 轮

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*
