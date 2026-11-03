# AC-5-05 门(`TICKS_PER_DAY` 单一定义点)· 证据原件

**日期**: 2026-11-03
**触发**: 用户「补 AC-5-05 的门」
**权威来源**: `design/gdd/time-and-weather.md:184`(判据句)· `:668`(AC-5-05 的 GIVEN/WHEN/THEN)· `:497`(裁定「单一定义点在 5」)

---

## 一、立门缘由(不是「补个文档」—— 本轮事故是肇事现场)

我在同一轮里新造 `Sim/Time/WorldTimeConstants.cs`(`TicksPerDay = 36000`),
而 `unity/Assets/Sim/World/TimeBase.cs` **早就存在**,自带 `TICKS_PER_DAY = 480L`
与**整套昼夜公式**。我建之前**没搜过既有实现**。

⇒ 全仓同时存在:**两个定义点 · 两套昼夜公式 · 两个互相矛盾的日长(480 vs 36000)**,
而**两边各自都有绿灯夹具** —— 没有任何东西会红。这就是 AC-5-05 要防的形态,而**我是肇事者**。

处置已在 `impl-f51-day-night-2026-11-03.md` §七 登记(收编到 `TimeBase`,删除 `Sim/Time/`)。
**本件只记「从此不再允许第二次」的机械执行体。**

## 二、判据(代码侧 3 条 + 数据侧 1 条)

| # | 判据 | 红向证据 |
|---|---|---|
| ① | 全仓**恰好存在**一条声明(零条 ⇒ 红) | `test_gate_detectsZeroDeclarations` |
| ② | 那一条必须落在 `Assets/Sim/World/TimeBase.cs`(别处 ⇒ 红) | `test_gate_detectsSecondDefinitionOutsideAllowedFile` |
| ③ | 允许文件内**也只能有一条** | `test_gate_detectsDuplicateInsideAllowedFile` |
| ④ | `assets/data/**/*.json` **零「日长」标识**(AC-5-05 的 `THEN` 第二半) | `test_gateDataSet_detectsLiteralCopy` / `_detectsAllNameForms` |

**①是空洞正控的堵口**:没有它,「把常量删光」也能让门绿(`F4` 形态)。

## 三、首跑实测逼出的一次返工(如实登记)

第一次真树跑:**门在自己的工程上恒红** —— 它把
- 门自己的头注释 `` //   · **注释里的声明行**:如 `// const int TICKS_PER_DAY = 480;` ``,与
- 夹具里作为**字符串实参**的 `"public const long TICKS_PER_DAY = 36000L;"`

双双判成「第二定义点」,一次报 5+ 条假红。

**修法**:匹配前先经 `StripCommentsAndLiterals` 抹掉注释 / 字符串 / 字符字面量的内容
(**换行原样保留**,故行号不漂)。这是判据的一部分,不是优化 ——
**恒红的门与恒绿的门同为空洞**,两者都等于没有门。

因此夹具由 7 条扩到 **15 条**,其中 ⑦–⑩ 专钉剥离面、⑪–⑭ 专钉数据侧。

## 四、证伪轮(6 条变异,全部命中;SHA-256 逐字节还原)

**被变异文件**(唯一):`unity/Assets/Editor.Tools.Gates/TimeConstantSingleDefinitionGate.cs`
**改前 SHA-256**:`b4d5c3d53537f7a03f4d1f292a5f165263da1d79cd09c2357f0a4dfe371d2850`
**还原后 SHA-256**:同值(逐条还原后复核,见下每行)

| 变异 | 改什么 | 期望红 | 实测 |
|---|---|---|---|
| **M1** | 正控守卫 `if (hits.Count == 0)` → `if (false && …)` | `test_gate_detectsZeroDeclarations` | ✅ 命中(恰 1 红) |
| **M2** | 位置判据 `if (!string.Equals(path, Allowed))` → `if (false && …)` | `test_gate_detectsSecondDefinitionOutsideAllowedFile` | ✅ 命中(2 红:另含行号测) |
| **M3** | 文件内重复判据 `if (allowedCount > 1)` → `if (false && …)` | `test_gate_detectsDuplicateInsideAllowedFile` | ✅ 命中(1 红) |
| **M4** | 移除剥离:`StripCommentsAndLiterals(text)` → `text` | `test_gate_greenOnRealTree` | ✅ 命中(**4 红**:真树 + 三条注释/字面量测) |
| **M5(首枪)** | `if (!IsMatch(l)) continue;` → `if (false) continue;` | 期望 `detectsLiteralCopy` | ❌ **打错靶**:该变异使「每行都判红」,靶测反而通过;已**作废登记** |
| **M5b(重打)** | 正则本体 `@"ticks[_\-\s]?per[_\-\s]?day"` → `@"__never_matches__"` | `detectsLiteralCopy` + `detectsAllNameForms` | ✅ 命中(2 红) |
| **M6** | 数据面缺失守卫 `if (!Directory.Exists(dataRoot))` → `if (false && …)` | `test_gateDataSet_redWhenDataRootMissing` | ✅ 命中(2 红:另含 `redWhenScanRootMissing`) |

⚠️ **M5 首枪作废的理由**:变异必须让**被测属性失效**,而不是让它**过激**。
「每行都判红」把靶测的断言(「必须有红」)喂饱了 ⇒ 假存活。这正是证伪轮八种假存活里的
**「变异打错靶」**。重打后靶点(正则)被直接废掉,靶测才转红。
⚠️ M2 / M4 / M6 的**连带红**是预期附带产物(同一门的不同测试共用一条判据链),不算弱化。

## 五、真树复算(命令自包含)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# ① 真正的代码声明点 —— 期望**恰 1 行**(TimeBase.cs:56)
grep -rnE "\b(const|static[[:space:]]+readonly)[[:space:]]+(int|long|uint|ulong)[[:space:]]+(TICKS_PER_DAY|TicksPerDay)\b" \
  unity/Assets --include=*.cs | grep -v "^unity/Assets/Tests/" | grep -v "Editor.Tools.Gates/"
# 实跑输出:
#   unity/Assets/Sim/World/TimeBase.cs:56:        public const long TICKS_PER_DAY = 36000L;
# 行数: 1

# ② 数据侧(AC-5-05 第二半)—— 期望 0
grep -rniE "ticks[_-]?per[_-]?day" assets/data --include=*.json | wc -l    # 实跑: 0
grep -rn "36000" assets/data --include=*.json | wc -l                      # 实跑: 0(仅事实记录,门不判值)

# ③ 夹具条数
grep -c "\[Test\]" unity/Assets/Tests/EditMode/Sim/time_constant_gate_test.cs   # 实跑: 15
```

⚠️ ①**不能**去掉后两个 `grep -v`:裸跑此模式会命中夹具里的字符串实参与门的头注释
(共 13 行)—— 那正是 §三 的返工现场,**不是**第二定义点。

## 六、回归三元组

| 项 | 读数 |
|---|---|
| EditMode | `6685 / 6648 / 0 / 0 / 37`(total / passed / failed / inconclusive / skipped) |
| PlayMode | `324 / 320 / 0 / 2 / 2`(退出码 2 为本仓 PlayMode 常态) |
| 编译警告 | **0**(全量重编译口径:`touch` 全部 **1323** 个 `.cs` 后跑) |
| 截至时刻 · 批次 · commit | **2026-11-03 10:39 CST** · AC-5-05 立门轮 · **`1df0646b` + 工作树** |

⚠️ **两轮读数**:10:35 的全量重编译轮(警告计数取此轮)与 10:39 的复跑轮(**两条纯注释改动之后**)
读数**完全相同**(`6685/6648/0/0/37` · `324/320/0/2/2`)。以 10:39 为三元组,
警告 `0` 归 10:35 的全量重编译轮(`touch` 1323 个 `.cs`)。

**等式**:`6685 − 6670 = 15` = 新夹具 `time_constant_gate_test.cs` 的 `[Test]` 数。
(`6670` = F-5.1 轮基线,见 `impl-f51-day-night-2026-11-03.md` §四;
`f51` / `time_base` 两件为**改名与断言更新**,净增 0。)

## 七、未做 / 盲区(逐项,不借绿)

| 项 | 状态 | 说明 |
|---|---|---|
| AC-5-05 `THEN` 的「**引用解析后与 5 同值**」 | **NOT DONE** | 52 今日无数据集;「跨表引用」在 ADR-014 管线里**尚未定义** ⇒ 无可解析之物。门只覆盖「零字面副本」那半 |
| 数据侧判据的**当日有效性** | **空真** | `assets/data/` 47 件对日长零命中 ⇒ **今天没有可判对象**。门证明的是「一出现就红」,不是「52 的数据集正确」 |
| 换名同值(`{"day_length": 36000}`) | **结构性盲区** | 门按**名字**判。按值判会把一切恰好等于 36000 的 JSON 判红,噪音不可接受 ⇒ 不采用,登记 |
| 大小写变体(`ticksPerDay` 在 `.cs` 里) | **刻意不管** | 现存唯一一例是夹具值 `event_budget_defer_test.cs:99`(`1200`);判红它会迫使夹具去读生产常量(= 自指判据,`F3`) |
| 注释/字符串里的声明 | **已不误报** | 由 `StripCommentsAndLiterals` 处理,⑦–⑩ 钉住 |
| **构建期探针轮**(证明门在建构建时真的被调用) | ~~**NOT-RUN**~~ → ✅ **2026-11-03 已跑(见 §九)** | 接线在源码里:`AssemblyGates.cs:229` 内一行 `errs.AddRange(TimeConstantSingleDefinitionGate.RunAll("."))`,由 `gate_wiring_manifest_test.cs` 的 `EntryWiringManifest` / `ClassConsumerManifest` 双向钉住。~~但按 `CLAUDE.md` §构建前门,**构建绿 ≠ 门跑了** —— 未做探针轮 ⇒ 不声称已证构建时生效~~ ⇒ **§九 已补探针轮:干净构建 `Success`;插无条件失败项后 `Result: Failure`,探针串在日志、栈帧指向 `BuildGate.OnPreprocessBuild`,utp 在 `Preprocess Player` 即止** |

---

## 八、2026-11-03 后续:AC-5-05 的 `THEN` 第二半已落地(本节**订正** §七的前两行)

§七写「引用解析后与 5 同值 = **NOT DONE**」「数据侧 **空真**」—— 那是**当时**的事实。
同日 ADR-038 实现轮把两半都补上了(证据原件:`impl-adr038-time-params-2026-11-03.md`):

| §七 的项 | 现在 | 载体 |
|---|---|---|
| 「引用解析后与 5 同值」 | **已落地** | `assets/data/52_random_events.json` 写 `{"$ref": "5_time.ticks_per_day"}`;`RandomEventsBinder` 解析,`RandomEventsBaker` 做逐位相等断言 —— 判据句从「无可判对象」变成「有可判对象且绿」 |
| 数据侧「空真」 | **不再空真** | 门现在**能**读到 52 那份文件:它是 `$ref`(对象)⇒ 不算字面副本;把 `$ref` 换成数字 ⇒ `test_dataSide_redWhenOtherFileHasLiteralCopy` 覆盖的正是这个形态 |

**未变的两件事(不得借绿)**:
① §七最后一行的**构建期探针轮**仍是 `NOT-RUN`;
② 门的**换名同值盲区**仍在(按名字判,不看值)。52 今天是**唯一**的跨表引用方,
若将来出现第三份数据集复制日长,只要它用了 `TICKS_PER_DAY` 之外的键名,门照不到 —— 仍登记为盲区。

---

## 九、构建期探针轮(2026-11-03 · 结清 §七 最后一行)

**判据来源**:`CLAUDE.md` §构建前门(BuildGate)——「绿 = 没报错,**不等于**门跑了」。
**载体**:`Editor.Tools.Bake/Cli/CostTableProbeBuildCli.Build`(全仓唯一 `BuildPipeline.BuildPlayer` 入口)。

| 轮 | 变异 | 结果 | 证据 |
|---|---|---|---|
| 干净轮 | 无 | `Build Finished, Result: Success` · CLI `Result: Succeeded` | `unity/Logs/probe-ac505-clean.log` |
| 探针轮 | `TimeConstantSingleDefinitionGate.RunAll` 首行插**无条件**失败项(`[PROBE][AC-5-05] …`) | `Build Failed` · `Result: Failure` · 退出码 **1** | `unity/Logs/probe-ac505-probe.log:1013` |

**归因三件套(逐项,缺一不算证明)**:

1. **探针串在构建日志里** —— `probe-ac505-probe.log:1013`
   `[PROBE][AC-5-05] 构建前门探针:本串出现 = 门在构建时被执行。`
   该串**只存在于** `TimeConstantSingleDefinitionGate.RunAll` 的变异版里
   ⇒ 它出现本身就证明该方法被执行(不是「可能被调用」的推断)。
2. **`Debug.LogError` 的栈帧** —— 同一日志 `:1020`
   `DaYiJingCheng.EditorTools.Gates.AssemblyGates/BuildGate:OnPreprocessBuild (…) (at Assets/Editor.Tools.Gates/AssemblyGates.cs:1501)`
   ⚠️ 按 `CLAUDE.md` 的提醒:栈帧给的是 **`Debug.LogError` 所在行**(1501),不是接线行;
   接线(`errs.AddRange(TimeConstantSingleDefinitionGate.RunAll("."))`)在 `:229` ——
   **本条是论证,不是直接观测**。直接观测是第 1 条(探针串唯一来源即该门)。
3. **构建因它失败** —— `BuildFailedException: [AssemblyGates] 构建前门失败 1 条(…)`
   (`AssemblyGates.cs:1502`)· `Build Finished, Result: Failure.` · utp 行:
   `##utp:{…,"steps":[{"description":"Preprocess Player","duration":3143}],"duration":3143}`
   ⇒ 步骤在 **Preprocess Player 即止**,未进入后续构建阶段。
   报错总数 = **2**(探针行 1 条 + `BuildFailedException` 1 条)。

**还原判据 = SHA-256 逐字节**:
```
探测前 2b612e08d47ed2e2c4cf06ae5225393e14a710e5c52d76cda541f49195b29d5c
探测后 2b612e08d47ed2e2c4cf06ae5225393e14a710e5c52d76cda541f49195b29d5c
```
**构建派生改动(② 正常派生,已还原)**:成功轮与探针轮都让
`unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` 的 `m_RuntimeSettings`
从 16 条 rid 变 `[]`(`OnBeforeSerialize` 在 `isBuildingPlayer` 时 `Clear()` + `PerformStripping`)。
按 `.claude/docs/coding-standards.md` §工作树卫生:该文件是**混合文件**,不能整文件忽略 ⇒
本轮构建结束后 `git checkout --` 还原回 `HEAD`,工作树归零。
⚠️ 这与 2026-10-31 的「成功构建后 `git status` 干净」**不一致** —— 本轮成功轮确实留下了改动。
如实登记:**「成功构建后干净」不是稳定事实**,判读时按派生组 ① 处理。
