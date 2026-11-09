# T10 手册近景的内容宿主 —— 实施与证据(2026-11-09)

> **范围**:`technique-unlock-map-2026-11-09.md` §5.2 的「缺页可见」——R2 三条反馈里的第二条。
> **用户裁定**:「按甲」(三候选里唯一不需要新裁法则的一条)。
> **上游**:T5a(钩子病例判定)· T6(`17067116` 缺页元件)· T8(`06c23e3a` 缺页判定)· T9(`153c4ce6`)。
> **本件交付「宿主」,不交付「可达」** —— 见 §6。

---

## 1. 断点在哪(可复算)

2026-11-09 侦察查出**三处零生产消费者**:

```bash
grep -rn "SetMissingPaperIds\|TutorialPaperMissing" unity/Assets --include=*.cs | grep -v /Tests/
# 期望:只有定义与注释,零调用点
```

| 缝 | 状态 |
|---|---|
| `PaperCloseupScreen.Bind(papers)` | 零生产调用方 |
| `PaperCloseupScreen.SetMissingPaperIds(...)` | 零生产调用方 |
| `TutorialPaperMissing.Compute(...)` | 零生产调用方 |
| `ModalId.PaperCloseup48` | **没有任何东西打开它** |

⇒ T6 会画、T8 会判、48 有表 —— **三件之间没有一根线**。本件就是那根线。

另有一条同族:`PatientDrawDecision.MissingTechniques`(T5a 的「缺哪门技法」)**只进了一行
`Debug.Log`**(`PatientAppearedDriver.cs:196-198`),没有任何呈现面。本件**刻意不接它** —— 见 §3。

---

## 2. 改动清单

| # | 文件 | 层 | 内容 |
|---|---|---|---|
| 1 | `Gameplay.Boot/PaperCloseupPresenter.cs` | **新** | `PaperCloseupSnapshot`(值快照)+ `PaperCloseupPresenter`(`Build` 纯算 · `TryPresent` 开屏喂屏) |
| 2 | `Gameplay.Boot/TutorialChainSeams.cs` | 缝 | 新增 `public Content => _content` —— 手册近景读**同一个** 48 表实例,不另装载 |
| 3 | `Gameplay.Boot/CompositionRootServices.cs` | 服务袋 | 新增 `PaperCloseups` 槽 |
| 4 | `Gameplay.Boot/BootRoot.cs` | 装配 | 第 **6.1c** 步构造(`_uiDriver` 已建之后) |
| 5 | `Tests/EditMode/Tutorial/paper_closeup_presenter_test.cs` | **新** | **11 条**判据 |
| 6 | `Tests/PlayMode/PaperCloseupWiringTest.cs` | **新** | **1 条**生产接线判据 |
| 7 | `Tests/PlayMode/TechniqueUnlockWiringTest.cs` | 修(T9 遗留) | `GameObject.Find` → `FindObjectsByType(Include)` —— 见 §5.2 |

**零新增 Kind、零 registry、零存档格式、零 ADR 改动、零烘焙产物。**
`Sim` / `Sim.Contracts` 引用集一字未动。

---

## 3. 为什么「缺页」只有一个判据源

钩子病例侧也有一个「缺哪些技法」(`PatientDrawDecision.MissingTechniques`),**不接进来**:

- `MissingTechniques` = 「病种 `treatable_by` 里玩家还没解锁的技法」;
- `TutorialPaperMissing` = 「教会某门技法、而该技法还没解锁的页」。

**前者是后者的子集**(没解锁的技法若有教学页,那一页必缺)。接两个源 = 同一事实两个口径,
迟早一个说缺一个说不缺 —— 与 ADR-024 消灭「Kind 白名单三处登记」同一类毛病。

钩子病例「指出**哪一门**」的细分(要高亮其中一页)今日**没有呈现载体**:屏的缺页态是
二值的撕口(`paper-missing` 类 + 一条撕口线)。**不造第三种状态** —— 如实登记为未做(§6)。

---

## 4. 判据

### 4.1 EditMode 11 条

| # | 判据 | 判别力 |
|---|---|---|
| 1 | 未解锁技法页 ⇒ 缺 | 判据没接上 ⇒ 期望集为空 ⇒ 红 |
| 2 | 已解锁 ⇒ 不缺 | 删「已解锁跳过」⇒ 红 |
| 3 | 纯知识页永不缺 | 把 `-1` 当「缺」⇒ 整本手册全白 |
| 4 | 只含被锁的、按输入序 | 顺序写反 ⇒ 红 |
| 5 | **换 actor ⇒ 换缺页集** | 缓存 / 忽略 actorId ⇒ 两条断言同一个集合 ⇒ 红 |
| 6 | **两次 `Build` 之间查询变了 ⇒ 结果跟着变** | 加缓存 ⇒ 红(承 AC-53-06 零跨 tick 态) |
| 7 | 查询为 null ⇒ **构造期**抛,点名校验形参 | 默认「全未解锁」= 整本被撕光;默认「全已解锁」= 少一页没人发现 |
| 8 | 玩家 id 未绑定(-1)⇒ `Build` 抛且**报文含该值** | 只说「未绑定」会让人找绑定点而不是找调用时序 |
| 9 | 纸条目为 null ⇒ 构造期抛 | 空纸冒充「还没读到」⇒ 红 |
| 10 | 空纸 ⇒ 空缺页集,不抛 | 空集上其余判据恒绿,故单列一条 |
| 11 | `PaperCount` 数的是纸,不是缺页 | 两个量混用 ⇒ 红 |

### 4.2 PlayMode 1 条(五段真启动序断言)

① `services.PaperCloseups` 非 null · ② `TryPresent` 返回 true 且 `CurrentModal == PaperCloseup48` ·
③ 屏是 `PaperCloseupScreen` 且 `PageCount ≥ 1`、标题非空(开了屏没喂数据 = 一本白纸)·
④ 缺页集与 `Build()` 现算的一致 · ⑤ `screen.IsCurrentPageMissing` 与快照一致(判据真的落到屏上)。

> ⚠️ **本测不证明可达**:「走近摊纸」的世界交互不存在,玩家点不开这本手册(§6)。

---

## 5. 回归

**三元组**:`2026-11-09T15:58:38Z`(EditMode 结束)/ `15:59:54Z`(PlayMode 结束) · **全量** · `153c4ce6` + 工作树。

| 套件 | 本次 | 对照基线(T9 后) | 差 |
|---|---|---|---|
| EditMode(全量) | **`7296 / 7259 / 0 / 0 / 37`** | `7285 / 7248 / 0 / 0 / 37` @ `2026-11-09T14:35:18Z` · T9 批 · `153c4ce6` | **+11 / +11** |
| PlayMode(全量) | **`373 / 368 / 0 / 2 / 3`** | `372 / 367 / 0 / 2 / 3` @ `2026-11-09T14:39:41Z` · T9 批 · `153c4ce6` | **+1 / +1** |

**等式核对**:EditMode 增量 **11** = `paper_closeup_presenter_test` 的 `[Test]` 条数 ·
PlayMode 增量 **1** = `PaperCloseupWiringTest` 的 `[UnityTest]` 条数。

### 5.1 首轮 2 红:门抓到了 T9 的一个遗留(如实登记)

首轮全量 `7296 / 7257 / 2 / 0 / 37`,两条红是**同一个根因**:

- `AssemblyGateRunAllTest.test_assemblyGates_runAll_zeroErrors_b3_b2_b6_b7`
- `PayloadDecoderTest.test_adr031_v4_b5GateStillPassesOnRealTree`(它就是跑同一套 `RunAll`)

门报文(`pm-teardown-find`)点名**两份**文件:

```
Assets/Tests/PlayMode/PaperCloseupWiringTest.cs:51
Assets/Tests/PlayMode/TechniqueUnlockWiringTest.cs:43
  TearDown 内用 `GameObject.Find(` —— 它只查 active 对象,而被启动序 SetActive(false) 的预摆
  恰是 inactive ⇒ 清场静默失效 ⇒ 同名件跨测累积 ⇒ 后续 BootRoot.FindInactiveInclusive
  按不稳定枚举序摘错 listener(AC-ADR020-10 偶发红)。
```

**根因不是「门太严」**,是**时间窗**:
`TechniqueUnlockWiringTest.cs` 写于 T9 的 EditMode 全量跑**之后**(14:38 vs 14:35),
而 T9 那一批只跑了 PlayMode。⇒ **T9 已入库(`153c4ce6`)的那个文件带着这条违例,
第一次被全量 EditMode 扫到是在本轮。** 说明「PlayMode 夹具写完只跑 PlayMode」是个缺口
—— 静态门的扫描面覆盖 PlaysMode 树,但只有 EditMode 套件会跑它。

**处置**:两份都改成
`FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)` 再按名过滤。
未放宽任何门。

---

## 6. 未做(不得借绿)

| 项 | 状态 | 理由 |
|---|---|---|
| **开屏入口**(走近摊纸) | **不存在** | 需要 4 的交互点 + 关卡布置(ADR-022 唯一作者)⇒ 玩家**今天打不开这本手册**。宿主就位 ≠ 可达,承 ADR-044 §D7 同款口径 |
| **哪一页教哪门技法**(内容) | **未填** | 与 T8 同一未结项:48 的 `papers[]` 仍是 1 条纯知识页,`teachTechnique` 全库一处未填 ⇒ **缺页集恒空** |
| 钩子病例「指出**哪一门**」的高亮 | **无载体** | 屏的缺页态是二值撕口;做「高亮其中一页」要新状态,须先裁 |
| `MissingTechniques` 的呈现 | **未接** | 刻意不接(§3):它是缺页集的子集,接进来就成第二口径 |
| 手册页内容本身 | 归内容轮 | `papers` 仍只有 1 条 |

> **一句话**:T6 会画、T8 会判、48 有表,本轮把三者接上了;**但手册仍然打不开,而且里面还没有字**。
> 这两个缺口分属交互轮与内容轮,不是本步的实现缺陷。

---

## 7. 装配位置与「同一个实例」

`TutorialChainSeams.Content` 是本轮新开的公开读面。理由:手册近景与教学链要读**同一张** 48 表。
若宿主另走一条装载路径(再 `Load<TutorialContentTable>()` 一次),同一份数据就有两条来路
——一条烘焙产物、一条随便什么,迟早分叉。这与 ADR-024「Kind 白名单只有一个登记真源」同型。

⚠️ 教学链未装配(无 data provider / 无音频字幕口)⇒ `TutorialSeams == null` ⇒
宿主**不建**,`PaperCloseups` 留 null。语义 = **本局没有手册内容**,不是「宿主坏了」。
