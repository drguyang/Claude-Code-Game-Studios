# Story 004: 三媒体调度与纸近景模态

> **Epic**: 教学与引导
> **Status**: Complete
> **Layer**: Feature
> **Type**: UI
> **Estimate**: 6h
> **Manifest Version**: 2026-11-02
> **Last Updated**: 2026-11-07

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

- [x] Fire 意图 → 三媒体调度正确:anchor Fire 播口述(44 cue + 字幕),完成 Fire 按需播示范 / 纸指引;键全部来自 Story 001 的 `.cooked`(cueId / clipKey / paper 条目),无硬编码 —— ✅ **调度面已落**(`tutorial_dispatcher_test` 14 条:键逐条回查 `.cooked`、绝对次序、字幕成对);⚠️ **播放面** NOT-RUN(见 Completion Notes)
- [x] 口述字幕 = AC-44-15 管线路径(narration text 进字幕队列,**零「无字幕口述」路径**);字幕可见性走查 [L](AC-48-06 子项)
  - ✅ **2026-11-07 收口轮勾上**:生产调用点存在(`TutorialMediaDriver.cs:180` `_subtitles.Show(...)`,
    链上游 = `TutorialChainDriver` → `TutorialMediaDriver`);cue/字幕**成对是结构性的**(同一行、单一构造路径,3 条);
    **真包实测**:屏幕上出现 `【师父】喘着气,气色、神态,都在脸上。` = **事件表** `subtitle_text`
    (`walkthrough-s25-4c…§8.2` 判据 2),同时段声学的归一化互相关 0.6032(同 §8.3)
    ⇒ 「有字幕、字幕与配音同源」两头都**是观测不是意图**。
  - ⚠️ 上一版之所以未勾,是因为当时 `SubtitleLine` 没有生产调用点(S25-4b 之后才有)。
- [ ] 示范 = 13 经 42 的一次性片段:播一次即散,不可被教学状态重播(重燃守住在 Story 003,呈现侧只消费) —— ⚠️ **未勾 · `BLOCKED-BY` 13(AI epic)的片段资产 + 42 的播放口**:调度器只产出 `SegmentKey` 意图,`TutorialMediaDriver` 显式**不消费**演示意图(`:30`),生产上**没有任何东西会演一段示范**。重燃守卫本身在 Story 003 已证
- [ ] 纸近景:静态道具(6 摆放,非拾取物)走近 → 请求 `ModalId.PaperCloseup48` 摊开;合上回 Explore 焦点;`IModalState` 可见「有模态摊开」(承 ADR-013 Amendment A 只读口) —— ⚠️ **未勾 · `BLOCKED-BY` 6(纸道具)/ 4(交互判定)**:内容面已落(`paper_closeup48_screen_test` 9 条:绑定 / 翻页 / 页码 / 模态身份);**但生产上没有第二条路径能打开这块屏** —— `TutorialMediaDriver` 不消费纸意图(`:140` 只计数),`SkeuoRuntimeDriver` 有屏工厂却无人请求 ⇒ 「走近摊开」整条腿**端到端不可达**(2026-11-07 复核)
- [x] **AC-48-10**:37 代码面零教学分支(grep + 37 的 AC 回扫,本 story 是联动验证方);48→37 只读单向 —— ✅ 符号面扫 `Sim` 装配(`tutorial|onboarding|guide`),扫描面为空即红
- [x] **AC-48-09**:两玩家投影独立 → 各自媒体互不干扰(P0 单机夹具 = 双 id 两路调度;联机仲裁 **BLOCKED-BY-45 / TR-tutorial-006**,禁借绿) —— ✅ P0 单机双 id 夹具已落;联机仲裁仍 **BLOCKED-BY-45**
- [ ] **AC-48-06 [L]**:三媒体可读性走查(口述+字幕成对 / 示范能看懂动作 / 纸图文自含)+ 无显式指令词(词族 = Story 001 扫描器复用)[L] —— ⚠️ **部分**:① 口述+字幕成对**已有真包观测**(`walkthrough-s25-4c…§8.2` 判据 2);② 示范 / 纸两腿**走查对象不存在**(见上两条 `BLOCKED-BY`)⇒ 该 AC **不可能整体跑通**,须待 13 / 6 落地后重排;③ 无显式指令词是 Story 001 扫描器的自动化面
- [ ] **AC-48-15 [L]**：纸近景手柄走查(走近→摊开→翻页→合上,无指针依赖) + 教学口述复用 AC-44-15 字幕管线(字幕必过 AC-44-15 管线;教学口述零字幕是禁项) —— ⚠️ **半边成立/半边不可达**:字幕半边**已有真包观测**(见第一条 AC);纸近景手柄走查**结构上做不了** —— 该屏今日无生产开口(见 AC-2 的 `BLOCKED-BY`),走查只能靠测试直调,那不是走查

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

- **AC-5**: 口述零字幕路径不存在(🔁 **2026-11-06 sprint-25 `/qa-plan` 轮补** —— 原 AC-1 只测「cue 键来自 `.cooked`」,
  而卡面 `AC-48-15` 的禁项是「**教学口述零字幕是禁项**」,原卡无对应用例)
  - Given: 全部口述 cue 的调度路径
  - When: 逐条追「cue 意图 → 字幕队列」的成对关系
  - Then: **每一条口述 cue 都带字幕文本**,零「只发声音不发字幕」的分支;
    字幕内容与口述文本同源(同一 `.cooked` 行),不得两处各写一份
  - Edge cases: 字幕管线(`AC-44-15`)未就位 ⇒ 以「字幕接口 + 占位呈现」交付并标 `BLOCKED-BY`,
    **自动化部分照跑**(占位路径同样必须存在字幕意图)

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/tutorial-onboarding/story-004-three-media-walkthrough.md`(AC-48-06/15 [L],含截图 + 主创签核)+ `unity/Assets/Tests/EditMode/Tutorial/tutorial_dispatcher_test.cs`(AC-1/3/4 自动化面) — must exist
**Status**: [x] 自动化面已落(`tutorial_dispatcher_test.cs` 14 条 + `paper_closeup48_screen_test.cs` 9 条 + 翻译层 10 + 链驱动 10);[ ] **[L] 走查未做**(`…-three-media-walkthrough.md` 未创建,**不得代签**;且纸 / 示范两腿的走查对象今天不存在,见 AC 注)

---

## Dependencies

- Depends on: Story 003(Fire 边沿)· Story 001(键空间)· 42 近景模态元件(并行)· 44 字幕管线(AC-44-15;未就位则子项 BLOCKED-BY)· 6 纸道具(并行)
- Unlocks: Story 005(完整流程走查依赖媒体链可跑)· `/ux-review` 48 轮

---

## Completion Notes

**2026-11-06 · S25-4 实现轮**(证据原件 = `production/qa/evidence/impl-s25-4-three-media-scheduling-paper-modal-2026-11-06.md`)

**落了什么**
- `TutorialDispatcher`(住 `Gameplay.UI.Tutorial`):`Dispatch(firedStepIds)` → **有序**媒体意图。次序 = 内容表步序 → 步内口述(`Order` 升序,同 `Order` 按 id)→ 示范 → 纸。纯函数,零游戏状态。
- **为什么产出 `TutorialMediaIntent` 而不是 `AudioCueDto`**(结构性,不是取舍):`AudioCueDto` 声明在 `Sim.Contracts`,而 48 装配的引用集恰 = `{Gameplay.Presentation}`(`AC-48-11`)⇒ 48 写不出那个类型名。翻译成 44 / 42 的 DTO 归 `Gameplay.Boot`。
- `PaperCloseupScreen` 由空壳改为内容绑定屏:`Bind` / `TurnPage`(越界即停,不回绕)/ 页码**仅多页时**显示 / `ModalId.PaperCloseup48`。
- 判据 **23 条**(调度器 14 + 纸屏 9);回归 **EditMode `7039 / 7002 / 0 / 0 / 37`** · **PlayMode `359 / 355 / 0 / 2 / 2`**(基线 `7016 / 6979` / `359 / 355` ⇒ 零回归)。

**证伪轮 6/6 转红。** ⚠️ 但**首轮 m1 / m2 是假存活**(全绿逃逸),两条判据当场补齐:
- m1(步序反转):原判据只断言「换 Fire 次序输出不变」= **不变性**,正反两路一起反 ⇒ 恒真。补 `test_ac1_stepOrder_isTheContentTableOrder` 钉**绝对次序**。
- m2(`Order` 失效):原判据先 `OrderBy(cueId)` 再 `AreEquivalent` ⇒ 顺序被自己抹掉;更根本的是**真表每步至多 1 条口述** ⇒ 排序分支**无输入可达**。把规则提成 `TutorialDispatcher.OrderNarrations`(纯函数)并用**合成行**直测(合成 id 刻意让 id 序与 order 序相反)。

**两处机械门命中(改名,不改语义)**:`Append*` 私有方法名被 `ClinicPanelRefreshTest.test_ac42d2_…` 判成「写三流」⇒ 改 `Collect*`;`TutorialMediaIntent.ClipKey` 被 `NoHealthbarFeedbackTest.test_rejectionWhitelist_noSilentClipping_…` 判成裁切字段 ⇒ 改 `SegmentKey`。两条已补进 `.claude/docs/coding-standards.md` §命名 — 机械门避让表(**扫描面是 `Gameplay.UI*` 整个前缀,不只 `Skeuomorphic/`**)。

**⚠️ 未做 / NOT-RUN(禁借绿)**
| 项 | 缺什么 |
|---|---|
| AC-2 世界侧:走近纸道具 → 摊开 | 6 的纸道具摆放 + 4 的交互判定 |
| AC-3 示范媒体真的播一次 | 13 的片段资产 + 42 的播放口 |
| AC-2 字幕真的上屏 | `Gameplay.Boot` 里把意图翻成 `AudioCueDto` + `SubtitleLineView.Show` 的那层(42 的接收口已在) |
| AC-48-06 / AC-48-15 [L] | 真包手柄走查;`…-three-media-walkthrough.md` **未创建** |
| AC-48-09 联机半边 | BLOCKED-BY-45(`TR-tutorial-006`) |

**2026-11-06 · S25-4b 翻译层**(证据 = `production/qa/evidence/impl-s25-4b-tutorial-media-translation-2026-11-06.md`)

- `Gameplay.Boot/TutorialMediaDriver`:`TutorialMediaIntent` → `AudioCueDto`(44 入向)+ `SubtitleLineData`(42 入向)。字幕文本取**事件表 `subtitle_text`**(`AC-44-15`),不取意图自带的 `Text`。判据 10 条,证伪 5/5 转红。
- 加边 `Boot → Gameplay.UI.Tutorial`(ADR-025 §① 窄修订);48 自身的引用集一字未改,`AC-48-11` 仍成立。
- ⚠️ **仍有零生产调用点**:链上游(投影 → 触发 → 调度 → 本层)未接,四个待裁点见证据 §6。**本件不把「能翻译」记成「已播出」。**
- ⚠️ **查出两表分歧**:同一 cue 的台词在 48 内容表与音频事件表里**不同**(四条全不同)。`AC-48-15` 要的是「同一行、不得两处各写一份」,现在是两份。内容对账与「同源」机械门**未做**,见证据 §5。

**2026-11-06 · S25-4c 生产接线**(证据 = `production/qa/evidence/impl-s25-4c-tutorial-chain-wiring-2026-11-06.md`)

- 整条链首次有生产驱动:`TutorialChainDriver`(事件流 → 投影 → 边沿 → 调度 → 投递),`TutorialChainSeams` 在玩家生成后 `Attach`;接线三处 = `CompositionRoot:2137-2165`(装配)/ `BootRoot:845`(绑定玩家)/ `BootRoot:2795`(tick 边沿末尾)。
- **播种在构造期**(读档进来的已完成步不重播);**增量游标 + 换流指纹**(换世界/读档 ⇒ 重建 + 重新播种)。判据 10 条,证伪 4/4 转红。
- ⚠️ **`archive` 步今天永不完结**:7a 没有「已存过档」读面,`checkpointWritten` 传 `null`(不伪造恒真回执)。
- ⚠️ **生产装配路径无自动化判据**:`TutorialSeams` 只在 `dataProvider is AddressablesDataProvider` 时装配,EditMode 夹具走不到该分支 ⇒ 「真接上了」只有代码面证据,须真包走查确认(`[L]`)。

**2026-11-07 收口轮(sprint-25 S25-4)—— 口述一路闭环,另两路的生产触发面不存在**

> ⚠️ **本件 `Complete` 的口径**:它指 **本 story 自身范围**(调度 / 翻译 / 投递 / 纸屏内容面)完成。
> **三媒体里今天只有「口述」这一路真的能播**;「示范」与「纸」两路的**生产触发面不存在**,
> 分别 `BLOCKED-BY` 13(AI epic 的片段资产 + 42 播放口)与 6/4(纸道具 + 交互判定)。
> AC 复选框**保留未勾**,那才是机器可读的事实 —— 不得把本条读成「三媒体全通」。

- **口述一路闭环(实测,非意图)**:`TutorialChainDriver` → `TutorialDispatcher` → `TutorialMediaDriver`
  (`:180` `_subtitles.Show` / 44 `CueRenderSink.Emit`)在**生产装配**下跑通;真包屏幕上出现
  `【师父】喘着气,气色、神态,都在脸上。`(= **事件表** `subtitle_text`),同一时段音频归一化互相关 **0.6032**
  (负控 ≈ 0)。⇒ **字幕可见 + 出声**两头都是观测。
- **`AC-48-06` / `AC-48-15` 拆开看**:字幕那半边成立(上文);走查那半边**结构上做不了**
  —— 纸近景屏今日**无生产开口**(`SkeuoRuntimeDriver` 有屏工厂,但无人请求;`TutorialMediaDriver`
  只对纸意图计数、不摊屏)⇒ 手柄走查的对象不存在。**不代签 `[L]`**。
- ~~**新立债 `TD-tutorial-01`**~~ 🔁 **同日已结案(用户裁定甲案)**:四条文案对齐到**事件表**
  (`48_tutorial_content.json` → 逐字同值,重烘 1904 B),并补**构建期跨表门**
  `TutorialNarrationSubtitleCrossRefGate`(C1/C2/C3 + G1/G2/G3,接进 `AssemblyGates.RunAll`),
  判据 9 条。**真负例**证明它抓得住原 bug:把 JSON 还原成 `HEAD` 版重烘 ⇒ 夹具红 + **构建 `Failure`**;
  证伪 M1/M2 各自单红;数据与产物 SHA-256 逐字节还原。
  证据 = `production/qa/evidence/impl-s25-4d-narration-subtitle-crossref-2026-11-07.md`。
- **回归(终树)**:EditMode `7076 / 7039 / 0 / 0 / 37` @ `2026-11-07T07:20:04Z` ·
  PlayMode `364 / 360 / 0 / 2 / 2` @ `07:21:12Z` · `cdbefccc` + 工作树。
  等式 `7076 − 7067 = 9` = 本轮新增的跨表门夹具,零回归。
- **仍未做 / NOT-RUN**:① 示范播放(`BLOCKED-BY` 13 + 42)· ② 纸近景走近摊开(`BLOCKED-BY` 6 + 4)·
  ③ `AC-48-06` / `AC-48-15` 的 `[L]` 走查(对象不全)· ④ 联机演出仲裁(`BLOCKED-BY-45` / TR-tutorial-006)·
  ⑤ `three-media-walkthrough.md` **未创建**(不得代签)。
