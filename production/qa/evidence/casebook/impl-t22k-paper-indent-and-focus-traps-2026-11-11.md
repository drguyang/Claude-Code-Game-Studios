# T22 批十一 —— 脉案续链:焦点两坑修复 + 纸面反馈声(压痕 / 翻页)+ 翻页淡入

- **日期**:2026-11-11
- **批次**:T22 批十一(sprint-26 `S26-4` 续链;用户裁定「甲 · 脉案页」)
- **commit 基线**:`7b56ea96` + 工作树
- **范围**:`TD-casebook-04` 两个焦点坑 + `AC-39-12` 第一半(纸的物理感)的**实现与可判部分**
- **产物**:`unity/builds/Linux-x64/大医精诚.x86_64`(2026-11-11 14:54:02 CST 重建,`ConfigVersion = 0x8484C950` · 21 行)

---

## 1. 改了什么(按判据归属)

| # | 改动 | 落点 |
|---|---|---|
| 1 | **落笔重建后焦点回到刚用的条目**(`TD-casebook-04` 坑①)| `CasebookScreen.RefreshLexiconBook` 记 `FocusedLexiconId()` → 重建后 `RestoreLexiconFocus(id)`(推迟一帧,同开册落焦的理由) |
| 2 | **册条目插在「折边」之前**(坑②:病名栏下一格不再落折边)| `RefreshLexiconBook` 的容器挂载:`host.Insert(host.IndexOf(foldEdge), _lexiconBook)` |
| 3 | **42 的呈现反馈声请求面**(42 只请求,不持声源)| `CasebookBinding.ICasebookCueSink`(`RequestPaperIndent` / `RequestPageTurn`)· `SkeuoRuntimeDriver.CueSink` · `CasebookScreen.BindCueSink` |
| 4 | **空行落焦请求压痕声**(GDD `casebook.md:224`) | `CasebookScreen.AddChannel` 的 `FocusInEvent` ⇒ `OnChannelFocusIn`:有 `.channel-reading*` 子元素 ⇒ 不发 |
| 5 | **翻页请求翻页声**(前案 / 后案)| `SubmitPageNext` / `SubmitPagePrev` |
| 6 | **翻页淡入**(不再瞬切;逻辑切换不变)| `ApplyPageVisibility` → `FadeInPage`;时长 = `0.12 s × MotionScaleHook.Scale`,`Scale = 0` ⇒ 瞬切 |
| 7 | **装配翻译器**(语义请求 → `AudioCueDto`)| `Gameplay.Boot/CasebookCueRouter`:压痕 / 翻页 ordinal、强度 255、`Source = -1`、空间锚 = **玩家当前格** |
| 8 | **素材 + 表行 + 需求 + 门 + 重烘** | `unity/Assets/Audio/ui_paper_indent_01.wav`(0.843 s · 44.1 kHz 单声道 · `loadType 0`)· `audio_events.json` 末位追加 `UI_Paper_Indent`(bus `UICue` · `PlayerExamAction`)· `audio_requirements.json` **SFX-11** · `AudioEventTableGates.RequiredCues` 加名 · `RequiredRequirements` / `RequiredRequirementIds` 加项 · `audio-material-manifest.md` 冻结表加行 · 重烘 21 行 `0x8484C950` |

---

## 2. 判据(自动化)

| 套件 | 结果 | 时刻(UTC) |
|---|---|---|
| PlayMode `casebook_lexicon_book_test`(含新增 ⑧a–⑧f)| **18 / 18** | 06:18:05Z 起 |
| EditMode `casebook_cue_router_test`(新,6 条)| **6 / 6** | 06:19 前后定向轮 |
| EditMode 全量 | `7453 / 7416 / 0 / 0 / 37` | 06:50:22Z–06:52:30Z |
| PlayMode 全量 | `386 / 381 / 0 / 2 / 3`(exit 2 = 含 2 inconclusive)| 06:53:23Z–06:53:45Z |

- **新增测试数等式**:EditMode `7453 − 7443 = 10`(6 router + 4 `paper_indent_cue_test`);PlayMode `386 − 380 = 6`(⑧a–⑧f)。两边都对得上,无借绿。
- EditMode 全量在把 SFX-11 同步到四处(数据件 / 常量 / 权威件 / 夹具)之前曾红 **11 条**(9 条是非法夹具缺必需 cue 行、2 条是「起货行末位」旧断言),逐项修完转绿;过程见 §5。

### 2.1 证伪轮(8 枪,各自恰好打中一条;还原 SHA-256 逐字节)

| 枪 | 变异 | 红 |
|---|---|---|
| M1 | 删 `RestoreLexiconFocus(focusedLexiconId)` | `test_pointIdentify_keepsFocusOnUsedEntry` |
| M2 | 册容器改回 `host.Add`(排在折边之后)| `test_openBook_entriesPrecedeFoldEdge_inFocusOrder` |
| M3 | 删 `row.RegisterCallback<FocusInEvent>` | `test_emptyRowFocus_requestsPaperIndentOnce` |
| M4 | 空行判定放宽(`return` 改成丢弃)| `test_nonEmptyRowFocus_doesNotRequestIndent` |
| M5 | 删两处 `RequestPageTurn` | `test_pageTurn_requestsCue_andSwitchesImmediately` |
| M6 | `PageTurnFadeSeconds = 0f` | `test_pageTurn_fadeDuration_scalesAndDegradesToInstant` |
| M7 | 压痕请求发成翻页 ordinal | 2 条(`..._emitsIndentOrdinal_withUiShape` · `..._unresolvedOrdinal_emitsNothing_andCountIsHonest`)|
| M8 | 未解析 ordinal 也发(`ordinal = 0`)| `..._unresolvedOrdinal_emitsNothing_andCountIsHonest` |

还原:`CasebookScreen.cs` `6f3bfcc2…` · `CasebookCueRouter.cs` `616164ec…`(变异前后逐字节一致)。

---

## 3. 真包走查(可判部分)

构建:`player-build-20261111T065402Z.log` = Success(27.4 s);`data-core` / `audio` 组均重建。

| 观察 | 证据 |
|---|---|
| **空行落焦压痕声** | 脉案内逐行导航共 **16 次** `oneshot cue=20 clip=ui_paper_indent_01 … group=UICue … playing=True`,空间锚 = 玩家格 `(1.45,0,15.45)`(不是原点) |
| **翻页声** | **3 次** `oneshot cue=11 clip=ui_page_turn_fwd_01 … group=UICue`(前案 / 后案各触发) |
| **实听电平** | sink monitor 录制 `t22k-audio2.raw`:峰值 **16359**(−5 dBFS)· RMS 5734 —— 与批十修复后的量级一致,不再是 −27 dB 的「听不见」 |
| **屏上形态** | `shots/t22k/25-book-open.png`:脉案纸面 + 病名 / ？/ 折;`24-disease-name.png`:右走可达病名栏 |
| **翻页淡入** | 仅**单测**判据(`transitionDuration > 0`,Scale=0 退化瞬切);实拍抓帧间隔 **0.27 s/png** > 淡入 0.12 s ⇒ **未拍到中途帧**,人眼判读待主创 |

### 3.1 本轮真包**没跑到**的部分(如实登记)

- **改判 / 焦点保持的人工腿**:本局两名病人 `disease=14` / `16`,判断词表无覆盖 ⇒ 病名册 `pointable=0`、册内零条目 ⇒ 无法在真包做「落笔 → 改判」。两条判据由 PlayMode ⑧a / ⑧b 承载;`TD-casebook-04` 的**人工解除判据仍未跑**。
- **淡入的肉眼判读**:见上,抓帧太慢。
- **主创签核**:`AC-39-12` 的签核要件,未跑。

---

## 4. 残留 / 不得借绿

- `AC-39-12` **仍不勾**:第一半里的「翻页惯性」只有单测 + 用户待看;第二半(旧 / 新同屏)是批三的既有证据。签核未做。
- `TD-casebook-04` **维持 `open` / P0 面 N**:两个坑的实现已落,但**解除判据是人工腿**;本轮被「病种不可指认」挡住(不是代码问题)。
- 新增 cue 的 `RequiredCues` 是**构建期常量**:删表行 ⇒ 构建红;但「路由器常量 ↔ 表行名」由 `paper_indent_cue_test` ① 守(改任一处 = 该测红)。
- `UI_Paper_Indent` 的声学判定由走查人试听后批准搬入(E5 谓词第 21 条,`status_words: null`);**不是** `AC-44-03 [L]` 的替代。

---

## 5. 同批踩到的连带面(逐项同步,不是「顺手」)

| 面 | 现象 | 处置 |
|---|---|---|
| 非法夹具 | 8 份 `invalid_*.json` 缺 `UI_Paper_Indent` ⇒ `ValidateRequiredCues` 多报一条,压掉各自「恰 N 条」断言(9 红)| 各夹具补同一行 |
| 起货末位判据 | 批九的「`sfx_process_complete` 在末位」随追加失效(2 红)| 改成「ordinal 恒 19 + 最新追加行在末位」;测试改名 `…_atItsAppendOrdinal` |
| A 锁常量 | `RequiredRequirements` 未含 SFX-11 ⇒ `[E1 · A 锁] 数据件多出未登记项`(1 红)| 常量 + `RequiredRequirementIds` 同批加项(9 → 10 项) |
| 权威件 B 锁 | `audio-material-manifest.md` 冻结表未含 cue / 文件(1 红)| 表内加 `[SFX #11]` 行 |

---

## 6. 复算(自包含)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# 表行与行数
python3 -c "import json;print(len(json.load(open('assets/data/audio_events.json'))['rows']))"          # 21
# 需求项
python3 -c "import json;print([r['id'] for r in json.load(open('assets/data/audio_requirements.json'))['requirements']])"
# 权威件冻结表
grep -c 'UI_Paper_Indent' design/assets/specs/audio-material-manifest.md                                # ≥1
# 真包 cue 发射(需 -tutorialdiag 的走查日志)
grep -ac 'oneshot cue=20' /home/gu/.claude/jobs/6001633c/tmp/t22k-walk.log                             # 16
grep -ac 'oneshot cue=11' /home/gu/.claude/jobs/6001633c/tmp/t22k-walk.log                             # 3
```