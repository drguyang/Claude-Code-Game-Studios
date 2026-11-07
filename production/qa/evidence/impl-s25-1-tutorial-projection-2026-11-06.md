# S25-1 · tutorial-002 只读投影 Proj 与装配护栏 —— 实现与判据(2026-11-06)

| 字段 | 值 |
|---|---|
| **卡** | `production/epics/tutorial-onboarding/story-002-read-only-projection-and-assembly-guards.md` |
| **权威** | 48 GDD 规则四 / 规则七 / F-48.1 / F-48.2 · `AC-48-03` · `AC-48-04` · `AC-48-11` · `AC-48-09` · ADR-009 §一 · ADR-019 §三 · ADR-025 §① |
| **commit** | `91b9a09b` + 工作树 |
| **回归** | EditMode **`6998 / 6961 / 0 / 0 / 37`** @ 2026-11-06 18:2x CST(基线 `6968 / 6931` ⇒ **+30 / +30,零回归**) |

---

## 1. 结论

1. **投影 `Proj` 落地且是纯函数**:六步谓词从「事件只读事实序 + 7a 回执」求值,48 侧**看不见** `SimEvent` / `EventKind` / `Fix` / `IEventSink`。
2. **装配护栏双层**:递归反射(会红的门,**负向夹具证明它真的会红**)+ 编译产物引用集断言。
3. **AC-48-11 是编译期事实,不是纪律**:48 装配引用集**恰 = `{ Gameplay.Presentation }`** ⇒ 写不出 `IEventSink` 这个类型名。
4. **`AC-48-03` 的字面面已补跑**(2026-11-06 18:1x,用户指令「补上」):真 `EventStream` + 真 `SaveComposer.Compose`,投影前后两份存档体**逐字节相同**。
5. ⚠️ **一条判据仍未跑**(见 §7):`AC-48-11` 的「真加引用就编不过」**构建级**负例。

---

## 2. 改动清单

| 文件 | 改了什么 |
|---|---|
| `unity/Assets/Gameplay.Presentation/Tutorial/TutorialProjectionFeed.cs` | **边界层解码面**:`TutorialEventFact`(Kind 名 + 作者玩家 id)· `ITutorialProjectionFeed`(事实序 + 7a 回执)· `TutorialSnapshotBuilder`(唯一解码点,三个带作者轴的 Kind)· `TutorialStreamFeed`(生产适配器,零缓存) |
| `unity/Assets/Gameplay.UI/Tutorial/Gameplay.UI.Tutorial.asmdef` | **新装配**,`references` 恰 = `["Gameplay.Presentation"]`(= AC-48-11 的编译期形态) |
| `unity/Assets/Gameplay.UI/Tutorial/TutorialProjection.cs` | `TutorialProjection.Project` / `TutorialProgressView` / `TutorialStepCompletion` + F-48.1 ③ 的**析取组** |
| `unity/Assets/Editor.Tools.Gates/TutorialSurfaceGate.cs` | 递归反射扫描器 + `RunAll()`(门链入口) |
| `unity/Assets/Editor.Tools.Gates/AssemblyGates.cs` | `RunAll` 接 `TutorialSurfaceGate.RunAll()`;`Manifest` 增 `Gameplay.UI.Tutorial` |
| `unity/Assets/Tests/EditMode/Sim/gate_wiring_manifest_test.cs` | `ClassConsumerManifest` 增 `TutorialSurfaceGate`(Chain / `RunAll`)+ `EntryWiringManifest["RunAll"]` 增一条 |
| `unity/Assets/Tests/EditMode/EditMode.asmdef` | 增引 `Gameplay.UI.Tutorial` |
| `docs/architecture/adr-025-contract-assembly-manifest.md` | §① 增 `Gameplay.UI.Tutorial` 一行(原文不删) |
| `docs/architecture/control-manifest.md` | 程序集清单入口加注(权威 = ADR-025 §①) |
| `docs/architecture/tr-registry.yaml` | `TR-tutorial-003` 加「就近解释已落」注,**status 维持 `partial`** |
| `unity/Assets/Tests/EditMode/Tutorial/tutorial_projection_test.cs` | **19 条** |
| `unity/Assets/Tests/EditMode/Tutorial/tutorial_assembly_guard_test.cs` | **10 条** |

---

## 3. 判据映射(29 条 → AC)

| AC | 判据(测试名) | 结果 |
|---|---|---|
| **AC-1** 纯函数 / 单调 | `test_ac1_sameInput_evaluatesIdenticallyTwice` · `test_ac1_prefixIsMonotoneSubsetOfFull` · `test_ac1_emptyStream_allStepsIncomplete_andNotAllCompleted` · `test_ac1_defaultView_isFailClosed` | ✅ |
| **AC-2 / AC-48-03** 零落盘 | `test_ac4803_saveBytes_areUnchangedByProjection`(**字面面**)· `test_ac4803_projection_doesNotMutateTheStream` · `test_ac4803_streamCountUnchanged` + 引用集断言(§4) | ✅ |
| **AC-3 / AC-48-04** 引用面 | `test_ac48_04_exportedSurface_hasNoSimTypes` + 两条负向夹具 + 正控 + null 守卫 | ✅ |
| **AC-3 / AC-48-11** asmdef 白名单 | `test_ac48_11_asmdefWhitelist_isExactlyPresentation` · `test_ac48_11_compiledReferences_excludeTheEventSinkAssembly` · `test_ac48_11_forbiddenAssembly_isSimContracts` · `test_ac48_11_whyNotGameplayUI_…` | ✅ |
| **AC-4 / AC-48-09** 双玩家独立 | `test_ac4_twoPlayersAuthoredEvents_stayIndependent` + `test_ac5_judgmentByOtherPlayer_…` | ✅ **单机夹具**;联机运行面 `BLOCKED-BY-45` |
| **AC-5** 六谓词映射 | `test_ac5_eachPredicateEvent_completesOnlyItsOwnStep`(六条 iff)· `test_ac5_emergencyAttempt_alsoCompletesTreatStep` · `test_ac5_predicateKindOfEachStep_matchesTheContentTable` · `test_ac5_worldLevelEvent_completesForEveryPlayer` | ✅(用**真 cooked 表**) |
| 判定面 | `test_ac8_fullStream_allSixStepsCompleted` · `test_builder_authorAxis_isDecodedFromPayloads` · `test_builder_emptyStream_returnsEmptyNotThrow` · 四条入口守卫 | ✅ |

---

## 4. 证伪轮(M1–M5,逐个转红 + SHA-256 还原)

| 变异 | 内容 | 红 |
|---|---|---|
| **M1** | 去掉作者轴过滤(`|| true`) | **2 红**:双玩家独立 / 别人的落笔不算我的 |
| **M2** | `archive` 步恒不成立(不读回执) | **2 红**:六谓词 iff / 全六步 |
| **M3** | `AllCompleted` 空集取真空为真 | **1 红**:`default(TutorialProgressView)` 的 fail-closed |
| **M4** | 删掉 F-48.1 ③ 的析取支(只剩 `DrugTreatmentApplied`) | **1 红**:`EmergencyAttempt` 也完成手段步 |
| **M5** | 护栏禁引词表清空 | **2 红**:嵌套泛型 `Fix` / 构造参数 `SimEvent` |

**还原判据 = SHA-256 逐字节**:
`TutorialProjection.cs` `e0e1ec40…138c62` · `TutorialSurfaceGate.cs`(变异时名为 `TutorialSurfaceGuard.cs`)`2c80aa69…866a1a`,两次均 OK,残留串 `grep -c "|| true"` = 0。
⚠️ **如实登记**:证伪轮跑在**改名之前**,文件名当时是 `TutorialSurfaceGuard.cs`;改名是 `sed` 的纯标识符替换(`TutorialSurfaceGuard` → `TutorialSurfaceGate`),**未触及任何被变异的行**。

---

## 5. 回归三元组

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | **`6998 / 6961 / 0 / 0 / 37`** | 2026-11-06 18:2x CST | S25-1 收口全量(含 AC-48-03 字节面) | `91b9a09b` + 工作树 |

**等式判据**:`6998 − 6968 = 30` = 本件新增 30 条(投影 20 + 守卫 10),零回归。

```bash
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('/home/gu/.claude/jobs/6001633c/tmp/s25-1-editmode-full6.xml').getroot();print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
```

---

## 6. 本轮被门抓到三次(如实登记,不粉饰)

| # | 症状 | 根因 | 处置 |
|---|---|---|---|
| ① | CS0104 `Assembly` 二义 | `UnityEditor.Compilation.Assembly` 与本类型同名 | 写全名 `System.Reflection.Assembly` |
| ② | 「声明 Chain 但链上看不见它」 | 我用 `AssemblyGates` 的**私有包装**,而门链 manifest 按 `类名.方法` 抽接线 | 入口上收到 `TutorialSurfaceGate.RunAll()` |
| ③ | 改名后仍红 ①「接线集里没有」 | 解析器 `GateClassNameSuffixes = {"Gate","Gates"}` —— **类名不以 Gate 结尾的调用根本不被认作门** | 类改名 `TutorialSurfaceGate` |

**教训(可复用)**:门链的「类名 = 契约」是**机械门的一部分**,不在任何命名规范文档里;新增门类时,`Gate` 后缀不是风格问题。

---

## 7. 未做(不得借绿)

1. ~~**`AC-48-03` 的字节面**未跑~~ 🔁 **2026-11-06 18:1x 已补跑**(用户指令「补上」):`test_ac4803_saveBytes_areUnchangedByProjection` —— 真 `EventStream`(四类事件)+ 真 `SaveComposer.Compose`,投影两遍(含回执为真)前后两份存档体 `CollectionAssert.AreEqual` 逐字节相同。**原文保留作当时口径。**
   - ⚠️ 强度说明:该夹具的**判别力**来自「投影确实跑了同一份流」——若投影改写了流,`Compose` 的输出会变;若投影自己另写一份存档,则引用集断言(48 引用不到 `Sim.Codec`)会先红。两者合起来闭合该 AC 的字面面。
2. **`AC-48-11` 的负例**:「真加一条 `Sim.Contracts` 引用 ⇒ 编译失败」是**构建级实验**,本轮未做。已跑的是编译产物的引用集断言 + asmdef 白名单逐字相等。语言级依据(C# 无直接引用不能命名类型)成立,但**不是观测**。
3. **联机运行面**:双 `player_id` 的真实联机进度独立 —— `BLOCKED-BY-45`,单机双 id 夹具**不替代**其 closure。
4. **`BuildGate` 探针轮**:门已接进 `RunAll`,但「构建时真的执行」**未跑探针轮** —— 按 CLAUDE.md §BuildGate「构建绿 ≠ 门跑了」口径,不记绿。
5. **7a 的 checkpoint 回执口**:今日**零生产实现**(`TutorialStreamFeed` 缺省 = 恒 false)⇒ `archive` 步在生产上恒不成立。已登记接口需求给 7a,48 不私开第二条通道。
6. **内容表的析取表达力**:`predicateKind` 是单值,F-48.1 ③ 的「或」只能由代码侧的析取组补 ⇒ 数据侧无法表达「只认其中一支」。已在代码注释与 story 里登记,未立债。

---

## 8. 2026-11-07 收口轮 —— AC-48-11 的**构建期强制点**(补 §7-2 / §7-4)

### 8.1 病因(实测,不是预防性洁癖)

§7-2 原文的期望是「真加一条 `Sim.Contracts` 引用 ⇒ **编译失败**」。**该期望本身是错的** —— 本轮实测:

- 往 `Gameplay.UI.Tutorial.asmdef` 的 `references` 加 `Sim.Contracts`,**并真的用上** `IEventSink`
  ⇒ **编译通过**(`0` 条 `error CS`);
- `b2-decl` 的双向差集为**空**(声明 = 使用)⇒ **旧门全绿**;
- 当时 AC-48-11 的唯一执行体是 EditMode 夹具 `test_ac48_11_asmdefWhitelist_isExactlyPresentation`,
  而夹具**不在构建链上** ⇒ 「构建前会拦住」是**假的**(与 `TD-gate-02` 同型)。

⇒ 语言的真实限制是「**无引用 ⇒ 写不出那个类型名**」(§8.4 的 N),不是「有引用编不过」。
AC-48-11 的「构建期」半边此前从未存在。

### 8.2 新门 b2-exact(AC-48-11 的构建期强制点)

| 落点 | 内容 |
|---|---|
| `unity/Assets/Editor.Tools.Gates/AssemblyGates.cs` | `ExactRefWhitelist`(点名装配 → 白名单)+ `CheckExactReferenceWhitelist`,接进 `RunAll()`(= 菜单 / 脚本 reload / `BuildGate` 三处共用);`BuildAsmdefIndex` 由 b2-decl 与 b2-exact 共用(两份索引会让 GUID 解析在其中一处静默失明) |

判据 = 源 asmdef 的 `references` 与白名单**集合相等**,**两向都报**(多一条 / 少一条都是违例)。
读**源 asmdef** 而非编译产物 —— 理由同 b2-decl:源级多一条声明即违例,不必等源码用到它。

### 8.3 探针轮 D1 —— `TutorialSurfaceGate` 在构建期**真的执行**

变异窗口:仅 `unity/Assets/Editor.Tools.Gates/TutorialSurfaceGate.cs`(窗口内未读该文件)。
变异:在 `RunAll()` 首行插一条**无条件**失败项 `[PROBE-S25-1-D1]` 并提前 `return`。

| # | 要素 | 观测(`Logs/s25-1-d1-probe.log`) |
|---|---|---|
| 1 | 探针串出现在**构建日志** | `:1068` `[PROBE-S25-1-D1] TutorialSurfaceGate 构建期执行探针 —— 本条为无条件失败项。` |
| 2 | 栈帧在 `BuildGate.OnPreprocessBuild` | `:1075` / `:1090`(`AssemblyGates.cs:1570` = `Debug.LogError` 行,`:1583` = 抛出处) |
| 3 | 构建**因它失败** | `:1167` `Build Finished, Result: Failure`;错误数 = **1**(= 只有本探针) |

⚠️ 栈帧给的是 `Debug.LogError` 行,不是探针行 —— 承 CLAUDE.md §BuildGate 的同一提醒。
「错误数 = 1」是归因依据:同一轮里没有别的门报错,该条只能来自 `TutorialSurfaceGate`。

还原 = SHA-256 `cd684c4f5cde9f0c5dd3a09e0e8b8b801df48ca59cddc05ca4967f655791eac5` 逐字节一致,
`grep -c "PROBE-S25-1-D1"` = **0**。

### 8.4 P2 真负例(asmdef 加 `Sim.Contracts`)+ N 编译负例

**P2**(`Logs/s25-1-p2-negative.log`):asmdef 加 `Sim.Contracts` + 临时件 `_ProbeUseEventSink.cs`
真的声明 `IEventSink` 字段。

| 观测 | 结果 | 行号 |
|---|---|---|
| 编译 | **通过**(`grep -c "error CS"` = **0**) | — |
| `b2-decl`(旧门) | **零输出** —— 对这种形态**结构性沉默** | — |
| `b2-exact`(新门) | `[b2-exact] 装配「Gameplay.UI.Tutorial」声明了白名单外的引用「Sim.Contracts」—— 该装配的引用集**恰** = { Gameplay.Presentation }(AC-48-11)…` | `:1216` |
| 构建 | `Build Finished, Result: Failure` | `:1271` |

**N**(`Logs/s25-1-n-compile.log`):**只删引用、保留使用** ⇒
`Assets/Gameplay.UI/Tutorial/_ProbeUseEventSink.cs(8,31): error CS0234: The type or namespace name 'Sim' does not exist in the namespace 'DaYiJingCheng' (are you missing an assembly reference?)`,exit=**1**。
⇒ 「无引用 ⇒ 写不出类型名」由**观测**坐实(此前是语言级推断)。

**还原**:asmdef SHA-256 `3a993413518b3afb8ecd373b768acdf5cbcaac96c7f06822563f84333eaa7882` 逐字节一致;
探针源文件与 `.meta` 已删(`ls` 输出只剩 4 个正式件)。

### 8.5 干净构建(还原后)

`Logs/s25-1-close-build-final.log` —— `Build Finished, Result: Success`(`:8779`,起始 `2026-11-07T06:01:17Z`)。
⚠️ 同轮更早一次干净构建(`s25-1-close-build1.log`,加门后的首跑)也是 `Success` ⇒ 新门对**当前树**零误报。

### 8.6 仍未做(禁借绿)

1. **联机运行面** —— `BLOCKED-BY-45`,单机双 id 夹具不替代其 closure(原文 §7-3 不变)。
2. **7a 的 checkpoint 回执口** —— 零生产实现,`archive` 步生产上恒不成立(原文 §7-5 不变)。
3. ~~**`ExactRefWhitelist` 表无独立单元夹具** —— 删表**不会**让任何 EditMode 夹具转红;
   今日的兜底是 §8.4 的构建级 P2 轮(**历史观测**,不是常驻门)。~~
   🔁 **2026-11-07 同轮补上**:新增 `tutorial_assembly_guard_test.test_ac48_11_buildGateWhitelist_matchesTheAsmdefFace`
   —— 期望值写字面量(F3:`不引被测表`),断言 ① 条目在场 ② 白名单**恰 =** `{ Gameplay.Presentation }`
   ③ 门表 ↔ asmdef 源**双向一致**。删表 / 放宽 / 单方面改 asmdef 三种形态都会转红。
   原文保留作当时口径。


---

## 9. 回归三元组(2026-11-07 收口轮)

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | **`7067 / 7030 / 0 / 0 / 37`** | `2026-11-07T06:16:11Z` | S25-1 收口全量(末轮,终树) | `3bc99c6d` + 工作树 |
| PlayMode | **`364 / 360 / 0 / 2 / 2`** | `2026-11-07T06:17:14Z` | 同批(末轮,终树) | `3bc99c6d` + 工作树 |

**等式判据**:`7067 − 7066 = 1` = 本轮新增的 1 条(`test_ac48_11_buildGateWhitelist_matchesTheAsmdefFace`);
PlayMode `364 = 364`(零回归)。⚠️ 首跑 `7066 / 7028 / 1` 的唯一红是
`gate_wiring_manifest_test.test_entryWiring_matchesManifest_bidirectionally` —— **门在正常工作**:
新入口 `CheckExactReferenceWhitelist` 未写进 `EntryWiringManifest`。处置 = 补声明(不是改夹具)。

```bash
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/s25-1-close-editmode3.xml').getroot();print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/s25-1-close-playmode2.xml').getroot();print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
```
