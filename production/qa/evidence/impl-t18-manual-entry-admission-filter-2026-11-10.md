# T18 —— 手册开屏入口(甲) + 投放资格收成生产件(乙) + 死日候选集前置过滤(丙)

> **范围**:用户 2026-11-10 勾选「甲 + 乙 + 丙」(丙 / 丙′ 互斥,二次追问后选「丙 · 真的改」)。
> **上游**:T17 `fa42ac74` · 证据 `impl-t17-e2e-day-2026-11-10.md`(本批三条都源自它的实测)。
> **产出**:2 个新生产类 · 2 个新夹具文件(6 + 1 条)· 2 处生产装配改动 · 1 条 ADR 窄修订。

---

## 1. 甲 —— 手册终于打得开了

### 1.1 病因

`PaperCloseupPresenter.TryPresent` **生产零调用方**:

```bash
grep -rn "TryPresent" unity/Assets --include=*.cs | grep -v /Tests/   # 改前:只有定义与注释
```

⇒ T10 的宿主 + A4 的七张纸 + `TutorialPaperMissing` 的缺页集,整套东西在游戏里**一次都打不开**。
缺页机制「齐全但不触发」—— 这正是 C1 说的那件事。

### 1.2 落地

| 件 | 内容 |
|---|---|
| `PlayerInputReader.ManualPressed()` | 键盘 `M` / 手柄 `selectButton`(现占用 E / F / R / Esc / 1–4 / WASD,`M` 零冲突) |
| `PaperCloseupToggleDriver`(新) | 三条判据:① 只在**无模态**时开 ② 手册开着时再按 = 关 ③ 只有**上升沿**算一次;开屏被拒**不计入** `OpenCount` |
| `BootRoot.EnsureModalInput()` | 与 `ModalInputDriver` 并列构造(判据不同,故分开两个驱动) |
| `BootRoot.TryPresentManual()` | 驱动的「开」这一面;菜单期 / 无手册内容 ⇒ `false` |
| `BootRoot.Update` | `_manualToggle?.OnFrame();` 紧邻 `_modalInput?.OnFrame()`,同在 `_booted` 早退之前(纯呈现层开关) |

**为什么不用 ESC 那条规则**:ESC 是「有模态就关」(TD-skeuo-03 收敛的单条规则);`M` 是**打开某一样东西**的键 ——
让它「关掉别人再开自己」会在玩家没要求的情况下换掉他正看着的屏。

### 1.3 判据

- EditMode `paper_closeup_toggle_test` **6 条**(边沿 / 再按即关 / 模态互斥 / 拒绝不计数 / 构造期 fail-closed)。
- PlayMode `PaperCloseupWiringTest.test_manualToggle_isWiredToThePresenter` —— 真启动序后
  `_manualToggle` 非 null,且 `TryPresentManual()` 真的把 `CurrentModal` 变成 `PaperCloseup48`。
- ⚠️ **不证按键可用**:`M` 的读面在 batch 下无法注入虚拟键盘 ⇒ 真包走查才是它的判据。

---

## 2. 乙 —— 投放资格收成生产件(消掉 T17 登记的最后一处「夹具照抄生产」)

`CompositionRoot` 里那段内联 λ(钩子配额怎么算 / 等级传什么 / 未绑定时怎么办)收成
**`PatientAdmissionPolicy`**;`CompositionRoot` 与 T17 的端到端夹具此后**调同一个对象**。

判据本体一行未改(仍在 `PatientDrawEligibility.Decide` / `HookCasePolicy` / `HookCaseLedger`),
只是换了落点。夹具那侧的**常量替身**(`hookAllowed: true/false`)随之消失 —— 钩子配额现在走**真账本**。

---

## 3. 丙 —— 投放资格前移到候选集(ADR-039 Amendment A)

### 3.1 改了什么

`EventWeighting.CandidatesOfTier(..., Func<int,bool> keyFilter = null)`;
生产装配传 `key => !requiresPatient(key) || canSpawnPatient(key)`。

⇒ **不可产出的条目根本不进抽池** ⇒ 抽到的必然可产 ⇒ `Ordinal` 照常前进 ⇒ T17 的「死日」**在结构上消失**。
链上的 `canSpawnPatient` / `PatientSpawnUnavailable` **保留为防御分支**(两处同源于 `PatientAdmissionPolicy`,分叉即响亮拒)。

### 3.2 代价(已写进 ADR-039 Amendment A)

1. 当日分布变成**条件分布**(在可产出条目上归一)—— 权重本身一字未改。
2. 候选集现在是 `(档, tick, 流状态)` 的函数,不再只是 `(档, tick)`;仍满足 ADR-016 §一 三源不变量。
3. 该档全部条目被滤掉 ⇒ `NoPool`(**新的可达分支**,语义正确:今天没有一件做得出的事)。

### 3.3 T17 三测的期望值随之改写(这不是回归,是判据跟着设计走)

| 测 | T17 | T18 |
|---|---|---|
| 主测(`0xA11CE`) | 2 抽 / 2 降临 / 1 病人 / `{0,1,11}` | **不变**(当天没有不可产出的条目被抽中) |
| 第二测 | `0x5EED` 配额关 ⇒ **6 眼全 `PatientSpawnUnavailable`、零产出** | 改判为「**配额用尽 ⇒ 2002 退池、日照常产出**」,换种子 `0xA`(见 §4.2) |
| 第三测 | `0x5EED` 配额开 ⇒ 2 病人 + 金鸡纳 | **不变**(钩子配额改走真账本,行为逐项相同) |

> 改后的第二测在 `OpportunityCandidateCount` 上直接断言 **3(不是 4)** —— 那一行就是丙的判据本身。

---

## 4. 实施期抓到的两个**真问题**(都是我自己写错、都由实跑抓出)

### 4.1 `TechniqueUnlockBootstrap.Ensure` 必须在**流为空**时调

首跑第二测红:候选数 **1**(期望 3)。根因:我把「预置钩子病例」写在 `Ensure` **之前** ⇒
流非空 ⇒ `Ensure` 按 D9 判「不是新世界」⇒ **起步集一门都没写** ⇒ 技法 0/1 未解锁 ⇒
所有病种被技法门拒 ⇒ 只剩不产病人的遭遇条目。

生产顺序没有这个问题(`BootRoot` 4.4a 在任何 52 活动之前)。**已把 `Ensure` 提到预置之前并写明理由**。

### 4.2 预置病人与「同 tick 的急召降临」会撞 `ActiveHouseCallQuery` 的 fail-loud

第二跑红:`出诊链接歧义:急召降临 tick 0 处 DiseaseOnset 有 2 条(规则十二 12.3)`。

机制:`ActiveHouseCallQuery` 要求**急召 `EventArrived` 的那一 tick 恰有一条 `DiseaseOnset`**。
我的预置病例落在 tick 0,而种子 `0x5EED` 当日的首眼抽中的是出诊类条目(2004)⇒ 它的降临也在 tick 0 ⇒ 两条。

处置:换成首眼抽中 **2001(遭遇条,不产病人)** 的种子 `0xA`。

> ⚠️ **这条不只是夹具问题,是生产上的潜在抛点**:`PatientAppearedDriver` 有自己的出病人节奏,
> 与 52 的降临**互不协调** —— 若它恰好在一次出诊类 `EventArrived` 的同一 tick 产出一个病人,
> 同一抛点就会在生产上触发。本批**未修**(不在 T18 范围),如实登记,建议另立债。

---

## 5. 证伪轮(5 条变异,全部转红;SHA-256 逐字节还原)

| # | 变异 | 落点 | 结果 |
|---|---|---|---|
| **M18-1** | `PatientAdmissionPolicy.Decide` 恒返回 `Admissible` | 乙 | 1 红(第二测) |
| **M18-2** | 删掉 `CandidatesOfTier` 的 `keyFilter` 判定 | 丙 | 1 红(第二测) |
| **M18-3** | `bool rising = pressed;`(去掉边沿化) | 甲 | 1 红(按住十帧) |
| **M18-4** | 删掉「别的模态 ⇒ 不抢」 | 甲 | 1 红(模态互斥) |
| **M18-5** | 删掉「手册开着 ⇒ 关」 | 甲 | **2 红**(再按即关 + 连按序列) |

还原:`sha256sum -c` 三个文件全部 `成功`。

> ⚠️ **M18-1 / M18-2 各自只红一条测** —— 「资格真的在门」这件事今天只由**一条**夹具钉住
> (第二测)。这是本批最薄的一块覆盖,如实登记;若将来有人动这两处,只有那一行会叫。

---

## 6. 回归

**三元组**:截至 `2026-11-10T03:04:53Z` / `03:06:14Z` · **全量** · `fa42ac74` + 工作树。

| 套件 | 本次 | 对照(T17 定稿) | 差 |
|---|---|---|---|
| EditMode(全量) | **`7364 / 7327 / 0 / 0 / 37`** @ `03:02:16Z`–`03:04:53Z` | `7358 / 7321 / 0 / 0 / 37` @ `2026-11-10T01:49:30Z` · `fa42ac74` | **+6 / +6** |
| PlayMode(全量) | **`374 / 369 / 0 / 2 / 3`** @ `03:05:17Z`–`03:06:14Z` | `373 / 368 / 0 / 2 / 3` @ `2026-11-10T01:50:35Z` · `fa42ac74` | **+1 / +1** |

等式:`7364 − 7358 = 6` = `paper_closeup_toggle_test` 的 6 条;`374 − 373 = 1` = 新增的接线测。
(第二测是**改名 + 改判**,不计入增量;主测与第三测断言未改。)

---

## 7. 未做 / 边界(禁借绿)

1. **`M` 键的真机可用性未测**(batch 虚拟键盘不可用)—— 真包走查才是它的判据。
2. **「走近摊纸」的世界内入口仍不存在**(C1-a 等关卡工具);本批只保证手册**打得开**。
3. **§4.2 的生产潜在抛点未修** —— 建议另立债(`PatientAppearedDriver` 与 52 的降临无协调)。
4. **丙的两处薄覆盖**:M18-1 / M18-2 只被一条夹具钉住(§5 末注)。
5. **`NoPool` 的新可达分支无专打夹具** —— 已有夹具覆盖 `NoPool`(合成空池),但「全档被资格滤空」
   这一形态没有专门造过。

---

## 8. 同批修复:§4.2 的出诊链接抛点(用户「现在修复」)

### 8.1 改的是什么

`ActiveHouseCallQuery.Evaluate` 的链接判据:

| | 改前 | 改后 |
|---|---|---|
| 判据 | 该 tick **恰有**一条 `DiseaseOnset`(0 或多都抛) | **急召那条 `EventArrived` 之后的第一条**同 tick `DiseaseOnset` |
| 同 tick 两条 | **抛**(「链接歧义」) | 取**之后的第一条**,第二条视为别的系统的病人 |
| 该位置没有 onset | 抛 | **仍抛**(断链 fail-loud 保留) |

**为什么「之后的第一条」就是 52 的病人**:链在 `RandomEventSpawnChain` 里
**先 Append `EventArrived`、紧接着 Append 它的 `DiseaseOnset`**(⑧ → ⑨,同一次同步调用),
中间没有别的写者能插进来。别的系统(9 的 `PatientAppearedDriver`)在同一 tick 产的病人
要么在这条降临**之前**(不在扫描区间),要么在它**之后第二**条 —— 两种都不会被误取。

### 8.2 改了哪些件

| 件 | 改动 |
|---|---|
| `Sim/ActiveHouseCallQuery.cs` | 链接判据(见上)+ 文件头 / `remarks` / `exception` 三处口径 |
| `design/gdd/random-events.md` §12.3 | 补一句**消歧**(同 tick 可能有多条 onset;取降临之后第一条)—— GDD 原文只说「9 在降临 tick 创建的病人」,从未要求「恰一条」,是**实现侧收紧**的 |
| `house_call_test` | `test_ac5257_linkDuplicateOnset_throws`(钉「多条即抛」)**改判**为 `test_ac5257_linkTakesFirstOnsetAfterSummons_secondOnsetIsIgnored`;新增 `test_ac5257_onsetBeforeSummonsAtSameTick_isNotTheLink`(判别力:取「任意一条同 tick」会中招) |
| `random_events_end_to_end_day_test` 第二测 | **换回种子 `0x5EED`** —— 它正是改前会抛的那个形态(预置病例与当日首眼降临同落 tick 0)⇒ 本测同时成为**修复的回归钉子** |

> ⚠️ 第二测的「由红转绿」是本次修复的直接判据:**改前它抛**(T18 第一跑的原话:
> `出诊链接歧义:急召降临 tick 0 处 DiseaseOnset 有 2 条`),**改后它绿**且断言 3 位病人。

### 8.3 证伪轮(2 变异,全红;SHA-256 还原)

| # | 变异 | 结果 |
|---|---|---|
| **ML-1** | 扫描起点由 `summonsIndex + 1` 改为 `0`(忽略位置) | 1 红:`onsetBeforeSummonsAtSameTick` |
| **ML-2** | 断链改为返回 inactive(不抛) | **3 红**:`linkMissingOnset` · `onsetAtAdjacentTick` · `writer_brokenLink_propagatesFailLoud` |

### 8.4 回归(修复后)

**三元组**:截至 `2026-11-10T03:36:58Z` / `03:38:09Z` · **全量** · `78d49b27` + 工作树。

| 套件 | 本次 | 对照(T18 提交) | 差 |
|---|---|---|---|
| EditMode(全量) | **`7365 / 7328 / 0 / 0 / 37`** @ `03:34:25Z`–`03:36:58Z` | `7364 / 7327 / 0 / 0 / 37` @ `03:02:16Z` · `78d49b27` | **+1 / +1** |
| PlayMode(全量) | **`374 / 369 / 0 / 2 / 3`** @ `03:37:13Z`–`03:38:09Z` | `374 / 369 / 0 / 2 / 3` @ `03:05:17Z` · `78d49b27` | **零变化** |

等式:`7365 − 7364 = 1`(删 1 条改判测、加 2 条 + 删 1 条 = 净 +1)。

### 8.5 仍未做

- **9 与 52 的出病人节奏仍无协调**:本修复消掉了「同 tick 两病人 ⇒ 抛」这个**故障形态**,
  但没有让两边**知道彼此**。若将来要「同一 tick 至多一个病人」这种更强的语义,那是另一条改动线。
