# S25-4b · 48 媒体意图 → 44/42 的 Boot 翻译层(2026-11-06)

| 字段 | 值 |
|---|---|
| **对象** | `unity/Assets/Gameplay.Boot/TutorialMediaDriver.cs`(新增) |
| **权威** | `AC-44-15`(文本本体 = 事件表 `subtitle_text`,上游发射点解析)· `AC-48-15`(教学口述零字幕是禁项)· ADR-018 §一/§二 · ADR-013 §9 C3 · ADR-025 §① |
| **回归** | EditMode **`7049 / 7012 / 0 / 0 / 37`** @ 2026-11-06T11:19:52Z(前次 `7039 / 7002 / 0 / 0 / 37` ⇒ **+10 / +10,零回归**;等式 `7049 − 7039 = 10` = 新增判据数)· PlayMode **`359 / 355 / 0 / 2 / 2`** @ 同刻(与前次逐项相同,`failed = 0`,退出码 2 来自 2 条既有 inconclusive) |
| **commit** | 工作树在 `2c22e837` 之后 |

---

## 1. 结论

1. **翻译层落地**:`TutorialMediaIntent`(48 的交付形状)→ `AudioCueDto`(44 入向)+ `SubtitleLineData`(42 入向)。
2. **字幕文本取事件表的 `subtitle_text`,不取意图自带的 `Text`** —— `AC-44-15` 逐字要求「文本本体 = 44 维护的事件表」,取意图文本 = 第二真源。
3. ⚠️ **本层至今零生产调用点**:`Play` 的调用方(48 的链上游:投影 → 触发 → 调度)尚未接。**这是本件最大的 NOT-RUN**,见 §6。
4. ⚠️ **查出一处两表分歧**(不在本层修):同一 cue 在 48 内容表与音频事件表里台词不同 —— 见 §5。

---

## 2. 改动清单

| 文件 | 改了什么 |
|---|---|
| `unity/Assets/Gameplay.Boot/TutorialMediaDriver.cs` | **新增**:`Play(intents, sourceCell) → TutorialPlaybackResult`。口述 → `Emit` + `Show`;示范 / 纸**只计数不消费**(交给 13/42),让「没接的那一半」在返回值里可见 |
| `unity/Assets/Gameplay.Boot/Gameplay.Boot.asmdef` | 引用 +`Gameplay.UI.Tutorial`(见 §3 末) |
| `unity/Assets/Tests/EditMode/Boot/tutorial_media_driver_test.cs` | **新增 10 条** |
| `docs/architecture/adr-025-contract-assembly-manifest.md` | §① `Gameplay.Boot` 行**窄修订**(引用集 9 → 10,注明依据) |

**边界(未越过)**:
- **不排序 / 不去重 / 不计时**:顺序归 `TutorialDispatcher`;「每步只发一次」归 `TutorialTrigger` 的闩。本层再去一次重会让两条去重规则互相掩护(有判据钉住:同一输入调两次 = 播两次)。
- **不演示范、不摊纸**:两种意图只计数。
- **不伪造听众位置**:空间化锚点由调用方给,本层原样透传。

---

## 3. 判据(10 条)

| 面 | 判据 | 结果 |
|---|---|---|
| cue 形状 | `test_narration_emitsCueWithResolvedOrdinal`(ordinal 而非名字 / 强度 255 / `Source = -1` / 非循环 / 格原样透传) | ✅ |
| **单一真源** | `test_narration_showsTableText_notIntentText`(意图里的文本与表里的**刻意不同**,并加一条「两者不得恰好相同」的上限反证) | ✅ |
| 失败面 | `test_narration_unknownCue_throws` · `test_narration_emptySubtitleText_throws`(**不跳过**:跳过与「内容没接上」不可区分) | ✅ |
| 另两种媒体 | `test_demonstrationAndPaper_areCountedNotPlayed`(零音频 / 零字幕 / 计数为 1) | ✅ |
| 次序 / 无状态 | `test_play_preservesGivenOrder` · `test_play_sameInputTwice_playsTwice` · `test_play_empty_doesNothing` | ✅ |
| 入口守卫 | `test_guard_nullIntents_throws` · `test_guard_nullSinks_throwAtConstruction`(四支各造「只有它为 null」的夹具) | ✅ |

**装配边**:48 装配的引用集**一字未改**(`AC-48-11` 仍成立);加的是 `Boot → Gameplay.UI.Tutorial` 的**入向**边 —— 同 2026-10-14 `Boot → Gameplay.Input` 先例,被引方反向零引用。本件首次跑时正是这条边缺失导致 CS0234,已补并回写 ADR-025 §①。

---

## 4. 证伪轮(5/5)

| # | 变异 | 应红 | 实测 |
|---|---|---|---|
| m1 | 字幕文本改成 `intent.Text`(第二真源) | `test_narration_showsTableText_notIntentText`(+2) | ✅ 3 红 |
| m2 | `cue: ordinal` → `cue: 0` | `test_narration_emitsCueWithResolvedOrdinal`(+1) | ✅ 2 红 |
| m3 | `looped: false` → `true` | 同上 | ✅ 1 红 |
| m4 | 解析不到 cue 时**静默 return** 而非抛 | `test_narration_unknownCue_throws` | ✅ 1 红 |
| m5 | 示范计进「已播放口述」 | `test_demonstrationAndPaper_areCountedNotPlayed` | ✅ 1 红 |

每轮还原后 SHA-256 与变异前逐字节相同(`55e67f1a…`)。变异窗口内未读取被变异文件。

---

## 5. ⚠️ 查出的**两表分歧**(内容缺陷,不在本层修)

同一 cue 在两张作者态表里给出了**不同台词**:

| cue | `48_tutorial_content.json` `narration[].text` | `audio_events.json` `rows[].subtitle_text` |
|---|---|---|
| `Narration_TeachObserve_01` | 先看看这个人。气色、神态,都在脸上。 | 喘着气,气色、神态,都在脸上。 |
| `Narration_TeachJudge_01` | 看明白了,就翻本子记一笔。 | 哼了一声,看明白了,就翻本子记一笔。 |
| `Narration_TeachClose_01` | 处置妥当,一个病例才算了结。 | 叹口气,处置妥当,一个病例才算了结。 |
| `Narration_TeachArchive_01` | 要紧的,都写进册子。 | 哼着小曲,要紧的,都写进册子。 |

`AC-48-15` 要的是「字幕内容与口述文本**同源**(同一行),不得两处各写一份」—— 现在是**两份**,且内容不同。
本层按 `AC-44-15` 取**音频事件表**为准(它是「文本本体」的法定出处);48 表的 `text` 字段因此成为**未被消费的重复件**。

**未做的两件事**(需要裁定,不得由本件自裁):
1. **内容对账**:哪一版是定稿?两表对齐到同一措辞,还是把 48 表的 `text` 字段删掉?
2. **机械门**:「两表同源」目前**零判据**,靠人眼。它该落在构建期(烘焙时对账)还是 EditMode 夹具(读两张 `.cooked` 比对),需要定。

---

## 6. ⚠️ **上游未接**:本层零生产调用点

`TutorialMediaDriver.Play` 今天**没有任何生产调用方**。要让它真的出声出字幕,还缺一环 —— 48 的链上游:

```
EventStream.Events → (解码) → TutorialSnapshotBuilder → TutorialProjection.Project
   → TutorialTrigger.Evaluate → TutorialDispatcher.Dispatch → TutorialMediaDriver.Play
```

这一环不是「顺手接线」,有**三个必须先裁的点**:

| # | 决策点 | 现状事实 |
|---|---|---|
| ① | **由谁驱动、什么节奏** | 既有先例是 `OnTickEdge`(tick 边沿,不由渲染帧驱动)。需要一个 `TutorialChainDriver` 住 Boot |
| ② | **哪一名玩家** | 生产有 `PlayerActorRegistry` / `next_player_id`(与 `JudgmentWriter` 同源),但「本机玩家 = 哪一个」的取法需点名 |
| ③ | **口述的空间化锚点给哪一格** | **师父实体今天不存在** ⇒ 没有声源格。可选:玩家所在格(听者=声源 ⇒ 不遮挡)/ 由内容表给固定格 / 先给 `Int3.Zero` 并登记 |
| ④ | **每 tick 重扫全流** | `TutorialProjection.Project` 是纯函数,吃**全量** fact 表;Control Manifest 明写「禁每帧全量重扫三流」⇒ 需要**增量游标**(缓存 fact + 只解新事件) |

未裁之前接线 = 自造策略,故本件停在这里。

---

## 7. 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -c "\[Test\]" unity/Assets/Tests/EditMode/Boot/tutorial_media_driver_test.cs   # 期望 10
grep -rn "TutorialMediaDriver" unity/Assets --include=*.cs | grep -v "/Tests/" | grep -v "TutorialMediaDriver.cs"   # 期望零命中(零生产调用点,如实登记)
grep -n "Gameplay.UI.Tutorial" unity/Assets/Gameplay.Boot/Gameplay.Boot.asmdef      # 期望 1 处
```
