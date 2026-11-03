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
| **构建期探针轮**(证明门在建构建时真的被调用) | **NOT-RUN** | 接线在源码里:`AssemblyGates.cs:229` 内一行 `errs.AddRange(TimeConstantSingleDefinitionGate.RunAll("."))`,由 `gate_wiring_manifest_test.cs` 的 `EntryWiringManifest` / `ClassConsumerManifest` 双向钉住。但按 `CLAUDE.md` §构建前门,**构建绿 ≠ 门跑了** —— 未做「插无条件失败项 ⇒ 要求 `Build Failure`」的探针轮 ⇒ **不声称已证构建时生效** |
