# S25-4c · 48 教学链生产接线(2026-11-06)

| 字段 | 值 |
|---|---|
| **对象** | `unity/Assets/Gameplay.Boot/TutorialChainDriver.cs` · `TutorialChainSeams.cs`(新增)+ `CompositionRoot` / `CompositionRootServices` / `BootRoot` 接线 |
| **权威** | 48 GDD F-48.1 / 规则四 · ADR-009 §一 Q1(进度 = 派生态)· ADR-016 §三 · ADR-019 §三 · ADR-025 §① |
| **回归** | EditMode **`7059 / 7022 / 0 / 0 / 37`** @ 2026-11-06T11:49:39Z(前次 `7049 / 7012 / 0 / 0 / 37` ⇒ **+10 / +10,零回归**;等式 `7059 − 7049 = 10` = 本件新增判据数)· PlayMode **`359 / 355 / 0 / 2 / 2`** @ 同刻(与前次逐项相同,`failed = 0`;退出码 2 来自 2 条既有 inconclusive) |
| **commit** | 工作树在 `2c22e837` 之后 |

---

## 1. 结论

1. **整条链首次有生产驱动**:`EventStream.Events → 解码 → Project → Evaluate → Dispatch → Play`。此前五段**各自零生产调用点**(只有夹具)。
2. **播种在构造期**,不在首个 tick —— 读档进来的已完成步因此**不重播**;而「tick 0 内写入的事件」也不会被误当成既有进度吞掉。
3. **增量游标 + 换流指纹**:每 tick 只解新事件(控制清单禁「每帧全量重扫三流」);流被整体替换(换世界 / 读档)时**检出并重建 + 重新播种**,旧世界进度不会串进新世界。
4. ⚠️ **`archive` 步今天永不完结**:7a **没有**「已存过档」读面,`checkpointWritten` 传 `null`。这是**如实登记**,不是伪造一个恒真回执(那会让「还没存过」直接算学会)。
5. ⚠️ **两个执行环境不同**:`TutorialSeams` 只在 `dataProvider is AddressablesDataProvider && audio != null && subtitleSink != null` 时装配 ⇒ 62 处 EditMode 夹具**照旧不装配**(零行为变化),生产 Boot 才接。

---

## 2. 改动清单

| 文件 | 改了什么 |
|---|---|
| `unity/Assets/Gameplay.Boot/TutorialChainDriver.cs` | **新增**:链驱动。`OnTickEdge(tick)` 返回 `TutorialChainTick`(发火的步 / 投递条数 / 是否检出换流) |
| `unity/Assets/Gameplay.Boot/TutorialChainSeams.cs` | **新增**:装配缝(表 + 翻译层 + 锚点),`Attach(stream, decoder, playerId)` 绑定玩家 —— 玩家 id 在 `BootRoot` 第 4 步才存在,同 `JudgmentWriter.SetAuthor` 的两段式 |
| `CompositionRootServices` | 新增 `TutorialSeams` / `TutorialChain` 两个属性,文档写明 `null` 的语义 |
| `CompositionRoot.Assemble` | 装配缝构造(**三件缺一不可**)+ `using …Presentation.Tutorial` |
| `BootRoot` | ① 第 4.7 步 `Attach`(actorId 已定)+ 未装配**显式告警** ② tick 边沿序列**末尾**调 `OnTickEdge` |
| `unity/Assets/Tests/EditMode/Tutorial/tutorial_chain_driver_test.cs` | **新增 10 条** |

---

## 3. 判据(10 条)

| 面 | 判据 | 结果 |
|---|---|---|
| 边沿 → 投递 | `test_caseOpened_firesObserveStep_andPlaysNarration`(真表 `observe.predicateKind = CaseOpened`;口述 1 条、字幕 1 条、锚点来自装配缝) | ✅ |
| 只发一次 | `test_sameCompletion_doesNotRefireOnLaterTicks` | ✅ |
| 增量 | `test_incremental_newEventFiresOnlyItsOwnStep`(CaseOpened → observe;CaseClosed → close;不重发 observe) | ✅ |
| 事实表不重复 | `test_incrementalCursor_doesNotDuplicateFacts` | ✅ |
| **读档不重播** | `test_seed_fromLoadedStream_doesNotReplay`(流里先有 CaseOpened ⇒ 构造期播种 ⇒ 边沿零 Fire、零投递) | ✅ |
| Checkpoint 缝 | `test_checkpointSeam_whenNull_archiveNeverCompletes` · `…whenWired_archiveFiresOnce` | ✅ |
| 换流 | `test_streamReplacement_isDetectedAndReseeded`(Clear + 换病人 ⇒ `StreamWasReplaced`、零投递) | ✅ |
| 守卫 | `test_seams_emptyContent_isRejected` · `test_guard_nullArguments_throw` | ✅ |

---

## 4. 证伪轮(4/4)

| # | 变异 | 实测转红 |
|---|---|---|
| m1 | 播种前先清空事实表(投影读到空表) | ✅ `test_seed_fromLoadedStream_doesNotReplay` |
| m2 | 构造期完全不播种 | ✅ 同上 |
| m3 | 永不检出换流 | ✅ `test_streamReplacement_isDetectedAndReseeded` |
| m4 | 游标不推进 | ✅ `test_incremental_newEventFiresOnlyItsOwnStep` |

每轮还原后 SHA-256 逐字节一致(`a938f881…`)。

⚠️ **m4 的转红判据与预期不同**(如实登记):预期由 `test_incrementalCursor_doesNotDuplicateFacts` 抓,实测由 `test_incremental_newEventFiresOnlyItsOwnStep` 抓 —— 因为游标不推进时,`DetectStreamReplacement` 会**每 tick 误判换流**(`_cursor-1` 指向不存在的下标),于是在「发火」那侧先炸。两条判据各自仍有效,但**「事实重复」这条在当前变异下没有被单独点亮**;要单独点亮需要一个只动累积、不动指纹的变异,本轮未做。

---

## 5. 接线面与**未接面**

**接上的**(三处,全部有 `file:line`):
1. `CompositionRoot.cs:2137-2165` — 装配缝(内容表 / 翻译层 / 锚点),条件 `dataProvider is AddressablesDataProvider && audio != null && subtitleSink != null`;
2. `BootRoot.cs:839-848` — 第 4.7 步 `Attach`(`:845`,玩家 id 已定)+ 未装配 `Debug.LogWarning`;
3. `BootRoot.cs:2790-2796` — tick 边沿序列**末尾** `OnTickEdge`(`:2795`,排在所有事件生产者之后,否则晚一拍看到本 tick 的 `CaseOpened`)。

**仍未接的**(都**不是**本件能做主的):
| 项 | 为什么 |
|---|---|
| `archive` 步完结 | 7a 无「已存过档」读面 ⇒ 传 `null` |
| 示范片段真的播 | 13 的 `clipKey` 资产 + 42 的播放口均不在(调度器只出 `SegmentKey` 意图) |
| 纸指引真的摊开 | 6 的纸道具 + 4 的走近判定不在 |
| 联机仲裁 | `TR-tutorial-006` gap,BLOCKED-BY-45 |
| `[L]` 走查 | 需要真包 + 上述三项 |

---

## 6. 一条**未验证**的风险(如实登记)

`TutorialSeams` 的装配条件是 `dataProvider is AddressablesDataProvider` —— 这一支**在 EditMode 夹具里走不到**(夹具的 provider 都不是该类),因此「生产装配路径真的接通」只有**代码面**证据,**没有**自动化判据。真包启动序里是否真的装上,须走**真包走查**确认(`[L]` 那一半);在此之前不得按「已接通」计。

---

## 7. 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -c "\[Test\]" unity/Assets/Tests/EditMode/Tutorial/tutorial_chain_driver_test.cs    # 期望 10
grep -n "TutorialChain" unity/Assets/Gameplay.Boot/BootRoot.cs                            # 期望 3 处(Attach / 告警 / tick)
grep -n "TutorialSeams" unity/Assets/Gameplay.Boot/CompositionRoot.cs                     # 期望 1 处(装配)
```
