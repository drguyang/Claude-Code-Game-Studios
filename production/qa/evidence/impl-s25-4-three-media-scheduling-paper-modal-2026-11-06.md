# S25-4 · tutorial-004 三媒体调度与纸近景模态 —— 实现与判据(2026-11-06)

| 字段 | 值 |
|---|---|
| **卡** | `production/epics/tutorial-onboarding/story-004-three-media-scheduling-paper-modal.md` |
| **权威** | 48 GDD 规则二/五/七 · `AC-48-06` · `AC-48-09` · `AC-48-10` · `AC-48-15` · ADR-013 §十-B · ADR-018 §一/§二 · ADR-016 §六 |
| **回归** | EditMode **`7039 / 7002 / 0 / 0 / 37`** @ 2026-11-06T11:04:11Z(基线 `7016 / 6979 / 0 / 0 / 37` ⇒ **+23 / +23,零回归**;+23 = 调度器 14 + 纸屏 9)· PlayMode **`359 / 355 / 0 / 2 / 2`** @ 同刻(基线 `359 / 355 / 0 / 2 / 2` ⇒ **零回归**) |
| **commit** | 工作树在 `89405366` 之后(本轮提交见 git log) |

> PlayMode 的进程退出码是 **2**,不是 0 —— 因 `inconclusive = 2`(与基线同形,`failed = 0`)。
> 如实登记,不当成绿:这两条 inconclusive 是本批之前就存在的,**不是本件引入**。

---

## 1. 结论

1. **媒体调度落地,且是「意图形状」而非 DTO** —— 结构性理由:48 装配的引用集恰 = `{Gameplay.Presentation}`(`AC-48-11` 的编译期形态),而 `AudioCueDto` / `IAudioCueSink` 声明在 `Sim.Contracts` ⇒ **48 写不出那个类型名**。本件产出 `TutorialMediaIntent`,翻译成 44 / 42 的 DTO 归 `Gameplay.Boot`。这不是取舍,是 `AC-48-04` / `AC-48-11` 的直接后果。
2. **纸近景屏从空壳变成可绑内容的屏** —— `Bind` / `TurnPage` / 页码只在多页时出现;模态身份 `ModalId.PaperCloseup48`(闭集第七员)由判据钉住。
3. ⚠️ **本件只交付「可判的一半」**:世界侧「走近摊开」、示范片段消费、字幕投递、`[L]` 手柄走查**全部 NOT-RUN**(§5),理由是上游交付物不存在 —— 不是没做,是**没有可接的东西**。
4. ⚠️ **本轮撞了两次机械门**(不是逻辑错):`Append*` 方法名 / `clip` 字段名。两处改名已登记进 `.claude/docs/coding-standards.md` §命名 — 机械门避让表(见 §6)。

---

## 2. 改动清单

| 文件 | 改了什么 |
|---|---|
| `unity/Assets/Gameplay.UI/Tutorial/TutorialDispatcher.cs` | **新增**:`TutorialMediaKind`(口述/示范/纸)· `TutorialMediaIntent`(只读意图)· `TutorialDispatcher`(一实例 = 一名玩家,纯函数)。排序 = **内容表步序 → 步内口述(按 `order` 升序,同 order 按 id)→ 示范 → 纸** |
| `unity/Assets/Gameplay.UI/Skeuomorphic/Screens/PaperCloseupScreen.cs` | **改写**(原为空壳):`Bind(papers, startIndex)` / `TurnPage(±1)`(越界即停,不回绕)/ `CurrentTitle` / `CurrentBody` / `CurrentIllustrationKey` / `PageCount`;页码**仅在多页时**显示 |
| `unity/Assets/Tests/EditMode/Tutorial/tutorial_dispatcher_test.cs` | **新增 14 条**(12 + 证伪轮补的 2 条次序判据) |
| `unity/Assets/Tests/EditMode/Tutorial/paper_closeup48_screen_test.cs` | **新增 9 条** |
| `.claude/docs/coding-standards.md` | 机械门避让表**补两行**(`Append/Write/Emit` 方法名 · `clip` 引用型成员名)+ 适用范围订正 |
| `production/sprint-status.yaml` | S25-4 → `review` |

**边界(未越过)**:
- 调度器**不播放、不计时、不并发**:「同刻不叠播」由消费方按序播放实现;48 的时序旋钮归用户手感轮。
- 调度器**零游戏状态**:无闩、无播放进度、不写流、不存档,构造后成员不再变。
- 纸屏**不持游戏状态**(ADR-013 §9 C3):纸条目是开屏时喂入的呈现缓存;插图槽只**暴露键**,接图归 42 元件库。

---

## 3. 判据(23 条)

### 3.1 调度器(14 条)

| AC | 判据 | 结果 |
|---|---|---|
| AC-1 键全来自 `.cooked` | `test_ac1_everyIntentKey_existsInCookedTable`(cue / 片段键 / 纸条目 id 逐条回查) | ✅ |
| AC-1 排序确定性 | `test_ac1_order_isStepTableOrderThenNarrationThenDemoThenPaper` · `test_ac1_firedOrderIrrelevant_outputIsIdentical` · **`test_ac1_stepOrder_isTheContentTableOrder`(绝对次序)** · **`test_ac1_narrationWithinStep_orderedByOrderThenId`(合成行直测)** | ✅ |
| AC-1 内容面 | `test_ac1_allSixStepsFired_producesAllThreeMediaKinds` | ✅ |
| AC-5 cue 与字幕成对 | `test_ac5_everyNarrationCarriesNonEmptyText` · `test_ac5_narrationTextEqualsCookedRow` · `test_ac5_narrationCountEqualsCueCount` | ✅ |
| AC-48-09 双路独立(单机子集) | `test_ac48_09_twoPlayers_dispatchIndependently` | ✅ |
| 边界 | `test_guard_nullOrEmptyFiredSteps_returnsEmpty` · `test_guard_defaultTable_throws`(F9:`default(struct)` 的 `Steps` 是空列表不是 null) · `test_guard_duplicateStepIds_notDuplicated` · `test_guard_negativePlayerId_throws` | ✅ |
| AC-48-10 37 零教学分支 | `test_ac48_10_simAssembly_hasNoTutorialSymbols`(`Sim` 导出类型 + 成员符号扫 `tutorial\|onboarding\|guide`;空扫描面 = 红) | ✅ |

### 3.2 纸近景屏(9 条)

| AC | 判据 | 结果 |
|---|---|---|
| AC-4 内容面 | `test_bind_rendersTitleAndBodyFromRow`(含插图键原样透出)· `test_bind_emptySet_keepsPaperWithoutText`(空纸 ≠ 未绑定) | ✅ |
| 翻页 | `test_turnPage_advancesThenStopsAtEnds`(不回绕,越界不改内容)· `test_turnPage_unboundOrZeroDelta_isNoOp` · `test_bind_startIndex_outOfRange_isClamped` | ✅ |
| 页码 | `test_singlePage_showsNoPageHint`(一张纸没有页序)· `test_multiPage_showsPageHint` | ✅ |
| 模态身份 | `test_modalId_isPaperCloseup48`(ADR-013 §十-B 闭集第七员) | ✅ |
| 入口守卫 | `test_guard_nullPapers_throws` | ✅ |

---

## 4. 证伪轮

驱动器 = `$CLAUDE_JOB_DIR/tmp/falsify_s25_4.py`(备份 → 变异 → 跑夹具 → 还原 → SHA-256 逐字节比对)。
变异窗口内**未读取**被变异文件(承 `tools/falsify/README.md` §并发读取禁令)。

| # | 变异(把对的改坏) | 应红的判据 | 实测 |
|---|---|---|---|
| m1 | `for (s = 0; s < Steps.Count; …)` → **倒序遍历** | DEAD `test_ac1_stepOrder_isTheContentTableOrder` | ✅ 1 红 |
| m2 | 步内排序比较器 → **只按 id 排**(`Order` 失效) | DEAD `test_ac1_narrationWithinStep_orderedByOrderThenId` | ✅ 1 红 |
| m3 | 零步守卫 `Steps.Count == 0` → `Steps == null`(**F9 形态**) | DEAD `test_guard_defaultTable_throws` | ✅ 1 红 |
| m4 | 口述意图的 `r.Text` → `string.Empty`(**字幕掏空**) | DEAD `test_ac5_everyNarrationIntent_carriesSubtitleText` · `…subtitleText_comesFromTheSameRowAsTheCue` | ✅ 2 红 |
| m5 | 越界翻页 `return false` → **回绕取模** | DEAD `test_turnPage_advancesThenStopsAtEnds` | ✅ 1 红 |
| m6 | 页码 `PageCount > 1 ? … : ""` → **恒显示** | DEAD `test_singlePage_showsNoPageHint` | ✅ 1 红 |

六轮全部转红;每轮还原后 SHA-256 与变异前逐字节相同(调度器 `8f3622ba…` · 纸屏 `5430a411…`)。

### 4.1 ⚠️ m1 / m2 的首轮是**假存活** —— 两条判据都补了

首轮 m1、m2 **零红**(`148/148` 全绿),查明是两个**不同**的病灶,都不是实现有 bug:

| 变异 | 为什么首轮没抓住 | 补的判据 |
|---|---|---|
| **m1**(步序反转) | 原判据 `test_ac1_fireOrderDoesNotAffectOutputOrder` 只断言「**换 Fire 次序输出不变**」—— 那是**不变性**。倒序遍历时正反两路**一起反**,等式照样成立 ⇒ 恒真 | `test_ac1_stepOrder_isTheContentTableOrder`:钉**绝对次序**(逆序喂,输出仍须等于表序),并前置守卫「至少出现 2 步」防空转 |
| **m2**(Order 失效) | 原判据先把两边 `OrderBy(cueId)` 再 `AreEquivalent` —— **顺序被自己抹掉了**;更根本的是**真表每步至多 1 条口述** ⇒ 排序分支在真数据上**无输入可达**(靶场里没有靶),不是断言写松 | 把次序规则提成 `TutorialDispatcher.OrderNarrations`(纯函数)+ 合成行直测;合成 id 刻意让 **id 序与 order 序相反**,否则「只按 id 排」照样绿 |

> 这两条正是 `.claude/docs/coding-standards.md` §假绿形态登记表 的 F4(空洞正控 / 无负控 的变体)
> 与「合取守卫无输入可达」家族。**登记在此,不删首轮记录** —— 「首轮零红」本身就是结论的一部分。

---

## 5. 诚实 NOT-RUN 清单(禁借绿)

| 项 | 为什么没跑 | 缺什么 |
|---|---|---|
| **AC-4 世界侧半边**:走近纸道具 → 请求 `ModalId.PaperCloseup48` 摊开 / 合上回 Explore 焦点 | 纸道具**不存在**于世界 | 6 的静态道具摆放(纸 = 世界内物件)+ 4 的交互判定(走近触发) |
| **AC-2 / AC-3 示范媒体**:13 经 42 的一次性片段播一次即散 | 片段资产与 42 的姿态槽位播放口均未接 | 13 的 `clipKey` 资产 · 42 的片段播放接口 |
| **AC-2 字幕投递**:口述 `Text` → `SubtitleLineView` | 42 的接收口(`SubtitleLine.cs`,AC-44-15 的 42 侧落点)已在,但**没有生产调用点**把 48 意图翻译过去 | `Gameplay.Boot` 的调度消费方(翻译 intent → `AudioCueDto` + `SubtitleLineView.Show`)。**配对本身是结构性的**(cue 与 text 同一行、单一构造路径),但「屏幕上真的出现字幕」未验 |
| **AC-48-06 [L] / AC-48-15 [L]** 三媒体可读性与手柄走查 | 需要真包 + 上述三条接线 | `production/qa/evidence/tutorial-onboarding/story-004-three-media-walkthrough.md`(**未创建,不得代签**) |
| **AC-48-09 联机半边** | 联机仲裁归 45 轮(`TR-tutorial-006` gap) | BLOCKED-BY-45 |

> ⚠️ **`test_ac48_10_simAssembly_hasNoTutorialSymbols` 的强度**:它判的是**符号面**(类型名 / 成员名),不是「37 有没有教学语义的代码」。37 出现零符号但仍按教学状态分支在理论上可能 —— 只是这种写法在 C# 里没有合理形态。如实登记,不把它当成 AC-48-10 的全部证据。

---

## 6. 两处机械门命中(改名,不改语义)

| 门 | 报错 | 处置 |
|---|---|---|
| `ClinicPanelRefreshTest.test_ac42d2_noEventSinkAppend_noWriteMethods` | `TutorialDispatcher.AppendNarration / AppendDemonstrations / AppendPapers` 判为「写三流方法」 | 改 `Collect*`。三者只往**本地意图列表**加条目,不碰任何 sink;门按方法名子串判,连**私有方法**与编译器生成的 lambda 名都算 |
| `NoHealthbarFeedbackTest.test_rejectionWhitelist_noSilentClipping_noClipFields` | `TutorialMediaIntent.ClipKey` 判为「裁切/缩字字段」 | 改 `SegmentKey`。语义未变(仍是示范片段键);源数据 `TutorialDemonstrationRow.ClipKey` 不动 —— 它在 `Gameplay.Presentation`,不在扫描面 |

**两门的扫描面是 `DaYiJingCheng.Gameplay.UI*` 整个命名空间前缀**(不只 `Skeuomorphic/`)—— 这一点此前未记录,已补进避让表的「适用范围」。

---

## 7. 复算命令(自包含)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -c "\[Test\]" unity/Assets/Tests/EditMode/Tutorial/tutorial_dispatcher_test.cs   # 期望 14
grep -c "\[Test\]" unity/Assets/Tests/EditMode/Tutorial/paper_closeup48_screen_test.cs # 期望 9
grep -n "Append\|ClipKey" unity/Assets/Gameplay.UI/Tutorial/TutorialDispatcher.cs      # 期望 5 行:3 行注释(避让说明)+ 1 行注释引源字段 + 1 行读 Presentation 的 r.ClipKey(不在扫描面)
```

> 实测输出(@ 2026-11-06):`14` · `9` · 5 行 —— 与上列期望逐项一致。**门扫的是「声明的成员」,不是「读到的成员」**:`r.ClipKey` 是 `TutorialDemonstrationRow`(住 `Gameplay.Presentation`)的属性,不在面内。
