# QA Sign-Off Report: sprint-17

**日期**:2026-10-21
**sprint**:17 — clinic 三件 + tutorial schema + casebook 全序 + 台账回写(6 件)
**阶段**:Pre-Production
**QA Plan**:`production/qa/qa-plan-sprint-17-2026-10-21.md`(317 行;S17-6 段与实测对账节为签核轮就地增补)
**Smoke Check**:`production/qa/smoke-2026-10-21-sprint17.md`(Verdict = **PASS**;5 PASS + 1 N/A)
**Review Mode**:`lean`
**基线溯源三元组(强制 · S12-0)**:截至 `2026-10-21 16:51 CST` · 批次 `sprint-17 QA 签核轮全量 EditMode` ·
commit `e0ad379` + 工作树 · XML `unity/Logs/s17-qa-full-editmode.xml`

---

## 0. 本件的判读边界(先读)

本件是 **sprint-17** 的 QA 签核。sprint-17 的六件**全部是 Logic / Config-Data 面**,
零 UI、零 Visual-Feel、零播放面改动。因此本件的 PASS **含义与常规含播放面的 sprint 签核不同**,
以下三条为硬边界,承 `qa-signoff-sprint-16-2026-10-21.md` §0 同款:

| # | 边界 | 含义 |
|---|---|---|
| **①** | **本 PASS 只覆盖 sprint-17 的 6 件** | 不覆盖 M3,不覆盖其它 sprint 的任何 story,不覆盖 M3 Exit 的任一条判据 |
| **②** | **禁止把 `failed 0` 读作「M3 有进展」** | 未闭 P0 面 = **73**(债册七订后值,`docs/tech-debt-register.md:210-211`),一位未动。本 sprint 六件**没有一件**清偿 P0 面债条 |
| **③** | **人工面依赖集为空,不是「已验」** | 六件零 Presentation / UI / Visual-Feel / 存档 / 性能面交付物 ⇒ **拿不到任何可人工观察的对象**。本件的人工面结论是「无可验对象」,**不得**被任何 AC 引作人工验收证据 |

> ⚠️ **②的由来**:本仓已混过一次「**判据可求值**」与「**判据已满足**」(`qa-signoff-sprint-16` §0 的加注)。
> `failed 0` 是**测试面**的性质;P0 面债条是**交付面**的性质,两者无蕴含关系。本件不得重犯。
>
> ⚠️ **①的补注**:本 sprint 唯一与「M3 进展」沾边的动作是 `TD-building-01` 的**立债**(不是清债)——
> 它使未闭 P0 面 `72 → 73`、epic 分布 `12/31 → 13/31`。**方向是反的**。

---

## 1. Test Coverage Summary

| Story | 类型 | 要求的证据形态 | 实际载体(路径 + 条数) | 结果 |
|---|---|---|---|---|
| **S17-1** `clinic-002` 房间析出与连通性 | **Logic** | EditMode 单测,必须存在且通过 | `unity/Assets/Tests/EditMode/ClinicMachine/clinic_room_extraction_test.cs` — **23** `[Test]` | **PASS** |
| **S17-2** `clinic-003` 乘子求值(F-24-1/F-24-2) | **Logic** | EditMode 单测,必须存在且通过 | `.../ClinicMachine/clinic_modifier_eval_test.cs` — **28 用例位**(`[Test]=24` + `[TestCase]=4`) | **PASS**(~~附 1 条 NOT-RUN 口径~~ 🔁 2026-10-21 CA-M3 补跑后 **EXACT**,无 NOT-RUN) |
| **S17-3** `tutorial-001` 内容 schema + 六项构建期校验 | **Logic** | EditMode 单测,必须存在且通过 | `.../Tutorial/tutorial_content_validation_test.cs` — **43** `[Test]`(该目录四件共 **73**) | **PARTIAL** ⚠️(AC 第 2 条 ② 半边 NOT-RUN) |
| **S17-4** `casebook-002` 病例列表全序纯函数 | **Logic** | EditMode 单测,必须存在且通过 | `.../Casebook/casebook_sort_test.cs` — **16** `[Test]` | **PASS** |
| **S17-5** `retro-14 A5` 台账回写 | **Config/Data** | 纸面;判据 = 两处 `grep` 同值 | `docs/tech-debt-register.md:301` + `production/retrospectives/retro-sprint-14-2026-10-18.md:213` | **PASS** |
| **S17-6** `clinic-006` 房间类型匹配(AC-24-03) | **Logic** | EditMode 单测 + PlayMode 集成 | `.../ClinicMachine/clinic_room_matcher_test.cs` — **32** `[Test]` · `Tests/PlayMode/ClinicMachine/clinic_room_matching_player_test.cs` — **5** | **PASS** |

**Summary**:**4 PASS · 1 PASS(~~附 NOT-RUN 口径~~ 🔁 2026-10-21 CA-M3 补跑后无 NOT-RUN)· 1 PARTIAL · 0 FAIL**。
> 🔁 **2026-10-21 补跑后**:S17-2 的 NOT-RUN 口径已消解 ⇒ 现为 **5 PASS · 1 PARTIAL · 0 FAIL**。
**无 S1 / S2 / S3 / S4 未闭缺陷**(`production/qa/bugs/` 仅 `README.md`,本轮实测)。

### 1.1 全量回归对账(本轮实跑,非沿用)

| 时点 | 批次 | commit | XML | total / passed / failed / skipped |
|---|---|---|---|---|
| 基线 | `sprint-17 末轮全量` | `e0ad379`+工作树 | `unity/Logs/s17-6x-full-editmode.xml` | **4347 / 4311 / 0 / 36** |
| 本件 | `sprint-17 QA 签核轮全量 EditMode` | `e0ad379`+工作树 | `unity/Logs/s17-qa-full-editmode.xml` | **4350 / 4314 / 0 / 36** |

`inconclusive` 两轮均 **0**。**集合差独立复算**(按 `fullname`,非只看计数):

```
baseline uniq 4686   new uniq 4689
added 3   removed 0
  + ClinicModifierEvalTest.test_clinic_envMod_sumExactlyMax_doesNotThrow
  + ClinicModifierEvalTest.test_clinic_envMod_sumOverflowLower_throws
  + ClinicModifierEvalTest.test_clinic_envMod_sumOverflowUpper_throws
```

**等式 `Δtotal(3) = Δpassed(3) = 新增条数(3)` 成立**;「**消失 0**」为独立判据,
排除「新增 3 同时坏 3、净增恰好对得上」的掩盖形态。
三条新增全部是 S17-2 `CheckedAdd` 守卫的载体 —— 与草稿增量轮 D-01/D-06 的判定闭环。

> ⚠️ **草稿 K-19 的 `4347/4311/0/36` 是「上一轮基线」,不是「含新测的全量」**。
> 本件以 `4350/4314/0/36` 为**含新测的全量**,两者差恰 3,等式已复算。

---

## 2. 逐件判定理由

### S17-1 `clinic-002` — PASS

载体 `unity/Assets/Tests/EditMode/ClinicMachine/clinic_room_extraction_test.cs`,**23** `[Test]`
(本轮 `grep -c '^\s*\[Test\]'` 实测)。23 条覆盖 6 条 AC;AC 第 6 条(只读视图不暴露写路径)
有真载体 `test_read_view_exposes_no_write_path`。
**证伪**:F1(对角并查)与 M3(instance-as-cell)两例在册,见 `review-sprint17-batch1-2026-10-21.md` §六。
壳格夹具的历史缺陷(A3-3/T1「夹具里无壳格」)已修 —— 现以「空足迹实例」表达并与无壳格基线逐项比对。
第 23 条 `test_countComponents_xAtIntMax_noPhantomUnionFromWraparound` 是 `int` 回绕守卫的载体。
**判 PASS 理由**:6/6 AC 均有载体,证伪有反证,评审 0 BLOCKING。

### S17-2 `clinic-003` — PASS(~~附 1 条 NOT-RUN 口径~~ 🔁 2026-10-21 补跑后无 NOT-RUN)

载体 `.../ClinicMachine/clinic_modifier_eval_test.cs`,**28 用例位**(`[Test]=24` + `[TestCase]=4`)。
性质测试 512 + 256 组已补(承草稿 K-04 的 1 BLOCKING,补后 M1 实测 7 红)。
`CheckedAdd` 守卫的**上界 `throw` / 下界 `throw` / `>`vs`>=` 边界**三条均有独立载体,
证伪 **CA-M1 / CA-M2 各 EXACT**(各轮 `total=28 passed=27 failed=1`,失败名恰为预期名)。
三条新测落在 `ClinicEnvModOf` 的 `Σ e_env` 路径(`ClinicModifierEvaluator.cs:282-289`),
**不是** clamp 夹具 —— 这是草稿 K-05 / 缺口 ① 的关闭依据。
**两条口径限制(须随判定一起读)**:① 输入是 `EEnv = ±MaxValue/2` 的**域外输入**(文件头自陈),
证的是「守卫会响」,不是「域内可达溢出」;② 只走到 `:115` 一个调用点,另三处未各自成载体
(同一函数的同一分支,不构成新缺口)。
**证伪第三变异 CA-M3**:~~未跑(`unity/Logs/` 下无任何 `ca-m3-*` 文件)⇒「守卫写宽一格」的变异反证空缺~~
<br>🔁 **2026-10-21 补跑 ⇒ EXACT**:把 `:284` 的 `>` 改成 `>=`,重跑该 fixture ⇒
**28 / 27 / 1**,红的恰是 `test_clinic_envMod_sumExactlyMax_doesNotThrow`;
还原后复跑 **28/28/0**;还原 `sha256` 逐字节一致(`33032f88…`)。
失败原文 = `OverflowException: 4611686018427387904 + 4611686018427387903 越出 int64 上界`,
两数之和恰为 `long.MaxValue` —— 正是被 `>=` 误判的那个边界。**详见 §4.1。**
⇒ S17-2 的 `>`/`>=` 边界守卫**可以**记「已由变异反证」。

### S17-3 `tutorial-001` — **PARTIAL**(AC 第 2 条)

载体 `.../Tutorial/tutorial_content_validation_test.cs` — **43** `[Test]`(目录四件共 **73**:
validation 43 · runtime table 10 · provider load 9 · disease names 11)。
AC 第 4 条(病名闭集扫描)的「未定项」已解除:`assets/data/disease_names.json`(8 病 / 12 显示名)
+ `TutorialDiseaseNameSource` 落地,`AllSixChecksExecuted == true`,④ 真跑且 Clean。
AC-5「无解析器」断言带 `Count > 0` 守卫(QA plan 增量 4 已兑现)。

**AC 第 2 条记 `PARTIAL`,不是 covered。**
- ② 外部半边(`cueId` / `clipKey`)**NOT-RUN**:`audio_events.json` 无 `teach_*` 键,全库无 13 示范片段表
  ⇒ `CueIdTableReady` / `ClipKeyTableReady` = **false**。
- 已由 `test_seed_content_withAllTablesInjected_passesAllSix` 证明「表就位后可过 6 项」——
  **这证明的是校验器正确,不是内容就位**。
- 这是**内容缺口,不是代码缺口**;补表须同时产出音频素材本体会撞 D6 素材存在性门。
- **具名归属:`cue + wav = 系统 44`;片段表 = `系统 13`**(`story-001:151-155` 原文)。

**⇒ 不得因其余五项全绿而把 AC 第 2 条记全条 covered。** 该条的状态是「代码半边已证、内容半边 NOT-RUN」。

### S17-4 `casebook-002` — PASS

载体 `.../Casebook/casebook_sort_test.cs`,**16** `[Test]`。覆盖 int64 极值、前缀、反射三面。
`AC-39-03` 已按用户裁定**收窄**(无条件版数学不可满足),反例测保留且**不记为失败**,前提归上游 37 开案路径
(`story-002(casebook):46-62`)。
**证伪**:F3(`CompareTo` → `(int)(a-b)`)⇒ 2 条红。评审 0 BLOCKING。

### S17-5 `retro-14 A5` — PASS

判据双侧同值(本轮 `grep` 实测):
`docs/tech-debt-register.md:301` = `| TD-player-01 | player-controller | closed | **Y** | R1 |`;
`production/retrospectives/retro-sprint-14-2026-10-18.md:213` 行动项行含结案注。
**未解除的两半如实保留**(不是本件判定面,见 §4)。

### S17-6 `clinic-006` — PASS

载体 `.../ClinicMachine/clinic_room_matcher_test.cs` — **32** `[Test]`;PlayMode
`Tests/PlayMode/ClinicMachine/clinic_room_matching_player_test.cs` — **5**,IL2CPP 真跑 `5/5/0/0`。
评审合并裁定 **CHANGES REQUIRED 但 0 BLOCKING**,W1–W3 + B-1..B-11 双列(Landed ✅ / Proven ✅)全结清。
**证伪 7/7 恰如预期**(实现轮)。
`OQ-24-7`(多匹配裁决)已由 **2026-10-21 补齐轮用户裁定「转正:声明序首个匹配」**:
哨兵 `MultiMatchPolicyIsProvisional => false`(`ClinicRoomMatcher.cs:120`,本轮实读),
哨兵测已翻转(`clinic_room_matcher_test.cs:445` 断言 `Is.False`),另配双向等价断言(`:479`)。
⚠️ **残留记账漂移(非 bug)**:`story-006:54` 与 `:92` 仍写「登记待裁」,与哨兵 `false` 矛盾
⇒ 见 §3 残余面 3。

---

## 3. Bugs Found

**零未闭 bug。** `production/qa/bugs/` 本轮实测仅 `README.md` ⇒ 无 S1 / S2 / S3 / S4 在册。

**「发现」的是残余验证面(不是 bug,不归本 sprint 修)**:

| # | 残余面 | 性质 | 具名归属 |
|---|---|---|---|
| 1 | **S17-3 AC 第 2 条 ② 半边** —— `audio_events.json` 缺 `teach_*` 键;全库无 13 示范片段表;`Assets/Audio/` 37 个 wav 零 `teach_*` | **内容缺口**(代码半边已由 `withAllTablesInjected` 测证明) | **cue + wav = 44 · 片段表 = 13** |
| 2 | **S17-6 `TD-building-01` 的 `World` 半边** —— 布局来源接 `World` 一侧未验;`IsEffectivelyWalkable` 端到端未核 | 债条 `open` / `P0 面 = Y` | 已入债册(六订);卡面自陈未验 |
| 3 | **记账漂移(第三处)** —— `story-006:54` / `:92` 仍写 `OQ-24-7`「登记待裁」,与哨兵 `false` 矛盾;`EPIC.md:70` 的「24 测」是实现轮历史快照(现实 32),可保留但**不得引作当前条数** | 记账,不影响门 | 卡面维护轮 |
| 4 | **`OQ-24-7`** —— **已裁**(2026-10-21 补齐轮),**不是未裁**。本件据实测登记:哨兵 `false` + 哨兵测翻转 + 双向等价断言 | 已结案 | — |

> ⚠️ **第 4 行是对派单口径的订正,不是借绿**:派单描述「`OQ-24-7` 未裁」已过期。
> 本件以 `ClinicRoomMatcher.cs:120` 与 `clinic_room_matcher_test.cs:445/:479` 的**源码实读**为准;
> 若下游仍按「未裁」表述,须以本行为对账点。
>
> **为什么 1–3 不定 S3/S4 缺陷**:三项均无运行时风险、无数据面风险、**无伪绿**(不存在「报告说验了实际没验」)。
> 缺的是**内容产物**与**记账一致性**,属「未完成面」而非「缺陷」。

---

## 4. NOT-RUN 清单(禁借绿)

以下各项**本轮未跑 / 未核**,逐条如实登记。**任何一项都不得被读作通过**:

| # | 项 | 状态 | 依据 |
|---|---|---|---|
| 1 | **CA-M3 证伪轮**(S17-2 `CheckedAdd` 的第三变异) | ~~**NOT-RUN**~~ ✅ **已跑 · EXACT** | ~~`unity/Logs/` 下无 `ca-m3-*` 任何文件(本轮 `ls \| grep -i ca-m3` 实测空)。**不得**用 CA-M1/M2 的绿推断其结论~~<br>🔁 **2026-10-21 补跑(见下方专节)** —— 结果 **EXACT**(恰 1 红,命中预登记名) |
| 2 | **IL2CPP 三格矩阵** | **非齐** | Linux-x64-IL2CPP 单格 ✅(`s17-6s-il2cpp-player.xml` = `5/5/0/0`,仅 5 测);**ARM64 目标不存在**(pinned 工具链无 arm BuildTarget,用户裁定长期未达);Windows-x64 / Apple Silicon 归发版前,不进常驻 |
| 3 | **PlayMode 全量套件** | **NOT-RUN** | 本轮只取到 `s17-6s-il2cpp-player.xml`(5)与 `s17-6q-playmode-editor.xml`(5/5/0/0),**均为 5 测的子集**,不是 PlayMode 全量(上一全量为 `smoke-s16-playmode.xml` 197 条) |
| 4 | **跨平台 CI 常驻 job** | **从未跑** | job 已建但缺 `UNITY_CLIENT_ID` / `UNITY_CLIENT_SECRET`(承 sprint-16 §6 第 5 行的 2026-10-21 订正) |
| 5 | **本报告未独立复核的项** | **未核** | ① GDD 逐行比对(AC 转写完整性依赖评审原件,本轮未独立复核 —— S17-2 曾因此丢「性质测试」);② S17-5 两条未解除面(`AC-1-33②` slopeLimit/stepOffset 卡 `O-9`;`AC-1-34` 的 `DOWNWARD_PRESSURE` 全库零命中),沿用债册自述;③ S17-6 `TD-building-01` 的 `World` 半边 / `IsEffectivelyWalkable` 端到端;④ `:119` / `:151` / `:278` 三个调用点的实际可达性(仅论证其与 `:115` 共享同一函数);⑤ S17-3 ④ 病名扫描的 `StringComparison.Ordinal` 语义未逐字复核 |

### 4.1 CA-M3 补跑记录(2026-10-21 · C-2 结清件)

**预登记(跑之前写下,非事后抄录)**:

| 项 | 值 |
|---|---|
| 变异 | `ClinicModifierEvaluator.CheckedAdd` 上溢判据 `a > long.MaxValue - b` → `a >= long.MaxValue - b` |
| 靶 | `test_clinic_envMod_sumExactlyMax_doesNotThrow`(断言「恰好等于上界属合法」,写 `>=` 后必抛) |
| 预期 | **恰 1 红**,且红的就是它;Upper / Lower 两条**必须仍绿** |
| 红 >1 | 变异波及别的判据 ⇒ 记 MISMATCH,不追认 |
| 红 =0 | 夹具对该变异无判别力 ⇒ **假绿,BLOCKING** |

**实测**:

| 轮次 | XML | total / passed / failed | 红的是 |
|---|---|---|---|
| 变异轮 | `unity/Logs/ca-m3.xml` | **28 / 27 / 1** | `test_clinic_envMod_sumExactlyMax_doesNotThrow` —— **恰 1 条,与预登记一致** |
| 还原后复跑 | `unity/Logs/ca-m3-restored.xml` | **28 / 28 / 0** | 无 |

**失败原文(XML `<failure>`)**:
```
System.OverflowException : env_score 的 Σ e_env:4611686018427387904 + 4611686018427387903
                            越出 int64 上界 —— 拒收(ADR-012 F7)
  at ClinicModifierEvaluator.CheckedAdd(...)  ClinicModifierEvaluator.cs:288
  at ClinicModifierEvaluator.ClinicEnvModOf(...)
```
两数之和 = `long.MaxValue`(`9223372036854775807`)—— 正是被 `>=` 误判成溢出的那个边界值。
⇒ 该测**真的**钉住了 `>` 而非 `>=`。

⚠️ **栈帧落在 `:288`(throw 行)而非 `:284`(判据行)** —— 这是 C# 栈帧的固有形态
(帧指向**抛出**处),不是变异打偏。判据行的变异由 sha256 变化(`33032f88…` → `6055c3a7…`)与结果差异共同坐实。

**还原判据 = SHA-256 逐字节**(不是 grep):
`sha256sum -c /tmp/CME-m3.sha256` ⇒ **成功**;现值 `33032f88dcfb684e2963c3b5ce96579d74bd2e597d8cc9dbfd923467821e3101`。
`grep -c "FALSIFY\|>= long.MaxValue - b"` = **0**。

**⇒ C-2 已结。** S17-2 的 `>`/`>=` 边界守卫自此可记「**已由变异反证**」,不再是「仅载体在册且绿」。

---

**已消除的旧 NOT-RUN(供对账,勿混读)**:
- 草稿「全量 EditMode 尚未产出」→ **本轮已跑**(`4350/4314/0/36`)。
- 草稿 K-05「`CheckedAdd` 守卫零载体」→ **已关闭**(三条载体 + CA-M1/M2 各 EXACT)。
- 草稿 K-19 的 `4347/4311/0/36` → **是基线,不是含新测的全量**。

---

## 5. Verdict: **APPROVED WITH CONDITIONS**

**逐条对规则**:

- **非 NOT APPROVED**:无 S1 / S2 缺陷(`bugs/` 仅 `README.md`);全量 EditMode `failed 0` / `inconclusive 0`;
  回归等式成立且「消失 0」;Smoke check PASS;**无伪绿**(不存在「报告说验了实际没验」)。
- **非 APPROVED**:S17-3 的 **AC 第 2 条明确 `PARTIAL`**(② 内容半边 NOT-RUN)。
  skill 规则:全部 PASS 或 PASS WITH NOTES + 无 S1/S2 ⇒ APPROVED;**存在一条 PARTIAL ⇒ 取 WITH CONDITIONS**。

> **为什么 S17-3 的 PARTIAL 足以把 verdict 从 APPROVED 拉到 WITH CONDITIONS**:
> 它的缺项**不在测试面**,而在**被校验对象的输入面**(cue 表 / 片段表)。
> 这意味着「六项校验全绿」这句话在当前仓库状态下**只覆盖 5 项 + 1 项的代码半边**。
> 若签成纯 APPROVED,下游会把 `AllSixChecksExecuted == true` 误读成「六项内容均就位」——
> 这正是本仓反复出现的「判据可求值 ≠ 判据已满足」形态。**拉一档是防误读,不是判失败。**

### Conditions

| # | 条件 | 为什么不能算已满足 | 归属 |
|---|---|---|---|
| **C-1** | **S17-3 AC 第 2 条转 covered 的前提**:`audio_events.json` 补 `teach_*` 键 + 对应 wav 素材;13 的示范片段表落地。在此之前,该条**只能写「代码半边已证 / 内容半边 NOT-RUN」**,不得在任何下游件写成「AC-48 六条全过」 | `CueIdTableReady` / `ClipKeyTableReady` 现为 `false`;`Assets/Audio/` 37 wav 零 `teach_*` | **cue + wav = 44 · 片段表 = 13** |
| **C-2** | ~~**跑 CA-M3 证伪轮**并落 XML;在此之前,S17-2 的 `>`/`>=` 边界守卫只能记「载体在册且绿」,**不得记「已由变异反证」**~~<br>✅ **2026-10-21 已结** | ~~第三变异无 XML;M1/M2 只覆盖上/下界两条 `throw`~~<br>已跑:**EXACT**(28/27/1,命中预登记名);还原后复跑 28/28/0;还原 sha256 逐字节一致。详见 §4.1 | ~~QA(下一轮自跑,成本 1 次 filter 运行)~~ **已兑现** |
| **C-3** | **`OQ-24-7` 的卡面表述订正** —— `story-006:54` / `:92` 的「登记待裁」须加注为「已裁(声明序首个匹配)」;`EPIC.md:70` 的「24 测」加注为历史快照 | 哨兵已 `false`、哨兵测已翻转,卡面仍写待裁 ⇒ 同一文件内自相矛盾(与 S17-4 `Test Evidence` 行的历史形态同型) | 卡面维护轮 |
| | ✅ **2026-10-21 本件写盘后即已兑现** | 见下方 🔁 加注 —— **C-3 已结,不再是条件** | — |
| **C-4** | **PlayMode 全量套件** 与 **IL2CPP 三格矩阵**须在 M3 前补齐;本 sprint 只证了「Linux-x64-IL2CPP 下这 5 条通过」 | 见 §4 第 2/3 行 | CI / 发版前 |

**⇒ 四条条件中 ~~C-1 / C-2~~ **C-1** 是 verdict 拉档的直接原因(写盘时含 C-2;C-2 已于同日补跑结清);C-3 是记账(已结);C-4 是矩阵级、非本件个体判据。**

> 🔁 **2026-10-21 补跑后复核 verdict**:C-2 结清**不改变 verdict**。
> 拉档的直接原因是 **C-1**(S17-3 AC 第 2 条内容半边 NOT-RUN)—— 它是**内容缺口**,不因证伪轮补齐而消解。
> C-2 结清只把 S17-2 的一处表述从「载体在册且绿」升级为「已由变异反证」,**是证据强度提升,不是条件消解**。
> ⇒ **APPROVED WITH CONDITIONS 维持不变。**

> 🔁 **2026-10-21 写盘后加注(禁借绿:以下均为本件写盘**之后**发生的动作,不是写盘时的事实)**
>
> | 条件 | 现状 | 实测依据 |
> |---|---|---|
> | **C-3** | ✅ **已结** | `story-006:54` / `:78-80` / `:81-86` / `:103-104` / `:131` / `:187-188` 六处已加注「`OQ-24-7` 已结案」,原文加删除线保留;`EPIC.md:71` 的「24 测」已加注为历史快照并标 32。**全部 append-only,原文未删** |
> | **C-2** | ✅ **已结(2026-10-21 补跑)** | ~~本轮**仍未跑** CA-M3 —— 中断所致~~<br>已补跑:**EXACT**(28/27/1,恰红 `test_clinic_envMod_sumExactlyMax_doesNotThrow`);还原后复跑 **28/28/0**;还原 sha256 逐字节一致(`33032f88…`)。详见 §4.1 |
> | **C-1 / C-4** | ⏳ 仍开 | 归 44 / 13 内容轮 与 CI / 发版前,与本轮无关 |
>
> ⚠️ **C-3 / C-2 的结清都不改变 verdict** —— verdict 拉档的直接原因现为 **C-1**(见上句),
> C-3 是记账、C-2 是证据强度。**APPROVED WITH CONDITIONS 维持不变。**

---

## 6. Next Step

### 6.1 sprint-17 可以收口

六件状态:`S17-1 / 2 / 4 / 5 / 6` = **done**;`S17-3` = **done,但带 C-1 限定语**
(「AC 第 2 条 = 代码半边已证、内容半边 NOT-RUN」)。**DoD 面**:
smoke check ✅ · QA sign-off ✅(本件)· 每件附可复算命令与输出 ✅。
⚠️ `S17-5` 无独立代码评审原件,`smoke-2026-10-21-sprint17.md` 已声明不覆盖 DoD 第 10 条 —— 本件**不代其结论**。
> 🔁 **2026-10-21 写盘后订正**:上句**已过期** —— S17-5 的独立评审原件**已于同日落盘**:
> `production/qa/evidence/review-s17-5-retro-backfill-2026-10-21.md`(APPROVED WITH SUGGESTIONS,2 MINOR)。
> ⇒ **DoD 第 10 条已兑现**。原文保留作沿革。

### 6.2 建议动作顺序

1. ~~**C-2 优先**~~ ✅ **已完成(2026-10-21)**:CA-M3 跑毕,EXACT,还原 sha256 逐字节一致。见 §4.1。
2. ~~**C-3 与 C-2 同批**~~ ✅ **均已完成(2026-10-21)**。
3. **C-1 归 44 / 13 的内容轮** —— **现为唯一拉档条件**,也是唯一会**持续被误读**的一条
   (六项校验的绿会一直被引作「内容就位」)。**这是下一步唯一必须做的事。**
4. **C-4 归发版前 / CI 常驻轮**,与 ADR-012 的矩阵账一并处理。

### 6.3 不得据此推进的事

- ❌ 不得声明 M3 达成(未闭 P0 面 = **73**,本 sprint 方向是 `72 → 73`)
- ❌ 不得把本件 PASS 引作任何 story / epic 之外的验收证据
- ❌ 不得把 S17-3 写成「六条 AC 全过」(AC 第 2 条 = PARTIAL)
- ❌ 不得把 `failed 0` 引作「播放面已验」(本 sprint 零播放面改动)

---

## 7. 溯源三元组(每个数值的出处)

| 数值 / 事实 | 截至时刻 | 批次 | commit | 出处 |
|---|---|---|---|---|
| `4350 / 4314 / 0 / 36` | `2026-10-21 16:51 CST` | `sprint-17 QA 签核轮全量 EditMode` | `e0ad379`+工作树 | `unity/Logs/s17-qa-full-editmode.xml`(根属性 `testcasecount="4350" total="4350" passed="4314" failed="0" inconclusive="0" skipped="36"`) |
| 基线 `4347 / 4311 / 0 / 36` | 同上(引用) | `sprint-17 末轮全量` | `e0ad379`+工作树 | `unity/Logs/s17-6x-full-editmode.xml` |
| 集合差 `+3 / −0` | `2026-10-21`(本件复算) | `sprint-17 QA 签核轮` | `e0ad379`+工作树 | 按 `fullname` 集合差脚本,本件实跑 |
| S17-1 **23** 测 | 同上 | 同上 | 同上 | `grep -c '^\s*\[Test\]' unity/Assets/Tests/EditMode/ClinicMachine/clinic_room_extraction_test.cs` |
| S17-2 **28** 用例位(`[Test]=24` + `[TestCase]=4`) | 同上 | 同上 | 同上 | 同法实测 + `unity/Logs/ca-m1-164351.xml` 的 `testcasecount="28"`(两条独立来源同值) |
| S17-3 validation **43** / 目录 **73** | 同上 | 同上 | 同上 | `grep -c` 四件 = 43 + 10 + 9 + 11;`story-001:157` 同值 |
| S17-4 **16** 测 | 同上 | 同上 | 同上 | `grep -c` `casebook_sort_test.cs`;`story-002(casebook):131,172` 同值 |
| S17-6 **32** 测 | 同上 | 同上 | 同上 | `grep -c` `clinic_room_matcher_test.cs`;`story-006:182`、`smoke:161` 订正后同值 |
| S17-6 PlayMode **5/5/0/0** | 引用自实现轮 | `S17-6s IL2CPP player` | `e0ad379` 之前 | `unity/Logs/s17-6s-il2cpp-player.xml`;另有 `s17-6q-playmode-editor.xml` = `5/5/0/0` |
| CA-M1 / CA-M2 各 `28/27/0/1` EXACT | `2026-10-21 16:43` 前后 | `CA-M1` / `CA-M2` | 同树 | `unity/Logs/ca-m1-164351.xml` · `ca-m2-fixed.xml`(草稿增量轮 D-06 实读) |
| ~~**CA-M3 = NOT-RUN**~~ **CA-M3 = EXACT**(28/27/1) | `2026-10-21 17:09 CST` | `CA-M3 补跑轮` | `e0ad379`+工作树 | `unity/Logs/ca-m3.xml`(失败 `<failure>` 原文见 §4.1) |
| CA-M3 还原后复跑 **28/28/0** | `2026-10-21 17:11 CST` | `CA-M3 还原复跑` | 同上 | `unity/Logs/ca-m3-restored.xml` |
| 还原判据 sha256 `33032f88…` | `2026-10-21` | 同上 | 同上 | `sha256sum -c /tmp/CME-m3.sha256` ⇒ 成功 |
| 未闭 P0 面 = **73** · epic 分布 **13/31** | 债册七订后 | `TD-testasm-01` 入表轮 | — | `docs/tech-debt-register.md:122-127,210-211` |
| Smoke Verdict = **PASS**(5 PASS + 1 N/A) | `2026-10-21 16:16` / `16:32` / `16:34 CST` | `sprint-17 末轮全量` / `smoke 构建门干净轮` / `探针轮` | `e0ad379`+工作树 | `production/qa/smoke-2026-10-21-sprint17.md:169` |
| `bugs/` 零在册 | `2026-10-21`(本件实测) | — | — | `ls production/qa/bugs/` = `README.md` |

---

## 8. 本轮的元观察(2 条)

### 8.1 「三处测数漂移」在订正后仍留一处 —— 订正须扫全部引用点

本轮订正了 `story-002:134/:172`(22→23)、`story-006:182/:198`(24→32)、
`casebook story:131`(`[ ]`→`[x] 16`)、`smoke:161`(24→32)。
**但 `EPIC.md:70` 的「24 测」仍在** —— 它是实现轮的历史快照(当时确为 24,补齐轮 +8 到 32),
按「原文不删」不订正,但**须加注为历史值**,否则下一次普查会把它当作又一处漂移。
⇒ **教训**:测数订正的检查面是「该 story 的**全部**引用点」,不是「发现的那几处」。

> 🔁 **2026-10-21 写盘后补记**:**本条自己成了它所述教训的又一个实例**。
> 写盘时只订了 4 处(`story-002:134/:172` · `story-006:182/:198` · `casebook:131` · `smoke:161`),
> 写盘后复核又扫出 **2 处漏网**:`story-003:171`(25 → **28 用例位**)与 `EPIC.md:71`(24 → **32**)。
> 两处均已补注。⇒ **「扫全部引用点」这一步本身也要有判据**,不能只靠「我记得还有哪几处」。
> 可复算判据:`grep -rn "2[0-9] 测\|3[0-9] 测\|[0-9] 个测试函数" production/epics/ production/qa/` 逐条核对。

### 8.2 PARTIAL 的载体在「输入面」时,比在「测试面」时更难被发现

S16-4 的 PARTIAL 是「测试没覆盖接线」—— 一眼能看出。
S17-3 的 PARTIAL 是「测试覆盖了,但被校验的输入不存在」—— 测试全绿、`AllSixChecksExecuted == true`,
**绿得毫无异常**。这类 PARTIAL 只能靠读 `CueIdTableReady == false` 这种**结构化哨兵**发现,
不能靠看测试结果。⇒ **建议**:凡「校验器类」story 的 DoD 增一条
「**被校验输入的存在性哨兵**须在 Completion Notes 里逐项列出真值」,与测试结果并列。
