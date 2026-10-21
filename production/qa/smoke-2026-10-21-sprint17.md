# Smoke Check Report — sprint-17
**Date**: 2026-10-21
**Sprint**: sprint-17(6 件:4 Must Have + 1 Should Have + 1 补立)
**Engine**: Unity 6.3 LTS (6000.3.24f1)
**QA Plan**: `production/qa/qa-plan-sprint-17-2026-10-21.md`
**Argument**: `sprint`(用户指令「补7」= 补 DoD 第 7 条)

**基线溯源三元组(强制 · S12-0)**:
- EditMode 全量:截至时刻 `2026-10-21 16:16 CST` · 批次 `sprint-17 末轮全量` · commit `e0ad379`+工作树
- 构建期门(干净轮):截至时刻 `2026-10-21 16:32 CST` · 批次 `sprint-17 smoke 构建门干净轮` · commit `e0ad379`+工作树
- 构建期门(探针轮):截至时刻 `2026-10-21 16:34 CST` · 批次 `sprint-17 smoke 构建门探针轮` · commit `e0ad379`+工作树

---

## 判据来源

本 sprint 的 smoke 判据**不是「游戏能跑」** —— 四件 Must Have 均无运行期可见面。
判据取自 `qa-plan-sprint-17-2026-10-21.md` §Smoke Test Scope:

> 「本 sprint 的 smoke 判据**不是「游戏能跑」**(四件均无运行期可见面),而是**门与装配未被打穿**」

---

## 六条逐条判定

### 1. 游戏启动至主菜单不崩 —— **N/A(不适用)**

**依据**:QA plan §Manual QA Checklist 明写本 sprint 零 UI / 零 Visual/Feel 面。
四件交付物 = 纯逻辑 + 数据 schema + 门,无任何运行期可见面。

⚠️ **不得读作「已验证不崩」** —— 是**没有可验对象**,不是验过了。

---

### 2. `AssemblyGates.RunAll` 返回零错(含 `GateWiringManifestTest`)—— **PASS**

**EditMode 侧**(`unity/Logs/s17-6x-full-editmode.xml`):

| 测试类 | 条数 | 结果 |
|---|---|---|
| `GateWiringManifestTest` | 15 | 全 `Passed` |
| `FsWriteBoundaryGateTest` | 17 | 全 `Passed` |
| `PlayerAssemblyGateTest` | 12 | 全 `Passed` |
| `AssemblyGates.RunAll` 零错 + 非空守卫 | 2 | 全 `Passed` |

`GateWiringManifestTest` 的 15 条含双向差集判据(`test_entryWiring_matchesManifest_bidirectionally`)与
「新接一门不声明 ⇒ 红」的封闭性判据,正是本条的承重面。

**构建期侧**(真跑,非纸面):

| 轮次 | 命令 | 结果 | 日志 |
|---|---|---|---|
| 干净轮 | `-runTests -testPlatform StandaloneLinux64` | **`Build Finished, Result: Success.`** | `unity/Logs/smoke-s17-gate-20261021-163141.log` |
| 探针轮 | 同上 + `PlayerAssemblyGate.RunAll` 顶部无条件 `errs.Add("[PROBE]…")` + 提前 `return` | **`Build Finished, Result: Failure.`** | `unity/Logs/s17-probe-20261021-163405.log` |

**探针轮归因三件套(承 CLAUDE.md §构建前门,缺一即不算证明)**:

| # | 要素 | 实测 |
|---|---|---|
| 1 | 探针串出现在**构建日志** | `[PROBE] PlayerAssemblyGate reached OnPreprocessBuild chain`(日志第 **1348** 行) |
| 2 | 栈帧是目标类的 `OnPreprocessBuild` | `AssemblyGates/BuildGate:OnPreprocessBuild` @ **`AssemblyGates.cs:1268`** |
| 3 | 构建**因它失败** | `BuildFailedException: [AssemblyGates] 构建前门失败 1 条`(第 1373 行) |

⚠️ **一处必须如实登记的归因陷阱**:日志第 **595** 行也有一条 `[PROBE]`,但它的栈帧是
`AssemblyGates/<>c:<RegisterReloadHook>b__49_0`(`AssemblyGates.cs:1232`)= **域重载路径**,
**不是**构建门。若只看「探针串出现过」就下结论,会把它误当成构建期证据 ——
**有效证据只有第 1348 行那一条**。

**还原判据 = SHA-256 逐字节**(不是 grep 探针串):
`d7a58e3d6efea82b9cdaab9d3a1fa5b8d28a3a366755f195fdb2adc6306487c7`,
`sha256sum -c` 通过,`grep -c PROBE` = 0。

---

### 3. 构建期烘焙管线对既有产物仍零错 —— **PASS**

`diskMatchesFreshBake` 系列 **9 条全 `Passed`**(磁盘产物 == 新鲜烘焙逐字节):

| 产物 | 测试 |
|---|---|
| `clinicmachine_adj` / `_context` / `_room` | `test_clinic_cooked{Adj,Context,Room}_diskMatchesFreshBake` |
| `48_tutorial_content` | `test_roundtrip_{steps,narrationDemonstrationsPapers}_matchBakedContent` |
| `53_delay_table` / `53_outcome_table` | `test_roundTrip_{delayTable,outcomeTable}_diskMatchesFreshBake` |
| `audio_events` / `audio_settings_registry` | `test_bake_{materialize_crossSideMatches,headerContract_audioAndSettings}` 等 |
| `build_modules` / 家具目录 | `test_{buildModules,furniture}_diskMatchesFreshBake` |

另 `test_bake_twoRunsSameSource_bytesIdentical` / `test_bake_sameSeedTwice_bitIdentical` /
`test_bake_settings_sameSeedTwice_bitIdentical` 三条确定性判据全 `Passed`。

---

### 4. 全量回归等式 `Δtotal = Δpassed = 新增条数`,且「消失 0」—— **PASS**

**独立复算**(按 `fullname` 集合差,不只看计数):

| 时点 | total | passed | 集合大小 |
|---|---|---|---|
| 13:52 基线(`s17-6-full-editmode.xml`) | 4315 | 4279 | 4315 |
| 16:16 末轮(`s17-6x-full-editmode.xml`) | 4347 | 4311 | 4347 |

```
Δtotal = 32   Δpassed = 32   新增 = 32   消失 = 0
等式成立: True    消失0: True
```

**三段式回归链**(本 sprint 全程):
`4315/4279/0/36` → `4335/4299/0/36`(+20)→ `4347/4311/0/36`(+12)
`failed` / `inconclusive` 三次恒 `0 / 0`。

**增量归因**(逐名对账,非估算):
- +20 = 11 e2e + 8 matcher 补测 + 1 extraction 补测
- +12 = 全部来自 `PlayerAssemblyGateTest`(12 条)

⚠️ 「消失 0」是**独立判据**,不是从 `Δtotal` 推出来的 —— 它排除「新增 N 同时坏 N、净增恰好对得上」的掩盖形态。

---

### 5. `S17-5` 两处台账同值 —— **PASS**

| 位置 | 实测 |
|---|---|
| `production/retrospectives/retro-sprint-14-2026-10-18.md:213`(行动项 5) | 含 `✅ **已兑现(2026-10-20`(grep 命中 1 处) |
| `docs/tech-debt-register.md:301`(索引行) | `\| TD-player-01 \| player-controller \| closed \| **Y** \| R1 \|` |

两处同值可 grep 复算。

---

### 6. 无新增 `TD-` 条目被静默转绿 —— **PASS**

**本次工作树对债册的全部改动只涉及 5 条 TD**(`git diff` 实测):
`TD-build-01` · `TD-building-01` · `TD-gate` · `TD-itemdb-01` · `TD-testasm-01`

| TD | 当前状态 | 是否新增转绿 | 原件 |
|---|---|---|---|
| `TD-build-01` | `open` | 否 | — |
| `TD-building-01` | `open` | 否 | — |
| `TD-gate-01/02/03` | `closed` | **否**(历史状态,diff 上下文带出) | 各自在册 |
| `TD-itemdb-01` | `partial` | 否 | — |
| `TD-testasm-01` | **`closed`** | **是** | `unity/Logs/s17-6v-playasm-gate.xml` + 证伪 V-PA1/V-PA2 记录 |

唯一新增转绿条目 = `TD-testasm-01`,**有原件、有证伪反证**,非静默。

⚠️ **另如实登记一处与本次无关的历史缺口**:`TD-prescription-02` / `-03` / `TD-audio-01a…-01e`
共 7 条在索引行标 `closed`,但正文无独立段落、无原件引用。**它们不是本次引入**
(本次 diff 未触及),不构成本 sprint 的 smoke 失败,但作为债册完整性缺口登记于此,
供后续债册审计轮处置。

---

## Missing Test Evidence

**无**。四件 Must Have 的测试载体全在册且通过:

| Story | 载体 | 状态 |
|---|---|---|
| S17-1 | `Tests/EditMode/ClinicMachine/clinic_room_extraction_test.cs` | 在册 · Passed |
| S17-2 | `Tests/EditMode/ClinicMachine/clinic_modifier_eval_test.cs`(~~25 测~~ **28 用例位** = `[Test]=24` + `[TestCase]=4`) | 在册 · Passed |
| S17-3 | `Tests/EditMode/Tutorial/`(内容 schema + 六项校验) | 在册 · Passed |
| S17-4 | `Tests/EditMode/Casebook/`(病例列表全序) | 在册 · Passed |
| S17-6 | `Tests/EditMode/ClinicMachine/clinic_room_matcher_test.cs`(~~24 测~~ **32 测**)+ `Tests/PlayMode/ClinicMachine/clinic_room_matching_player_test.cs`(5 测) | 在册 · Passed |

> 🔁 **2026-10-21 订正(QA 签核轮)**:S17-6 与 S17-2 两行的测数原为**建 story 时的估计值**被误记为实测值。
> 实测:`clinic_room_matcher_test.cs` **32** 测 · `clinic_modifier_eval_test.cs` **28** 用例位(`[Test]=24` + `[TestCase]=4`)。
> **不影响本报告任何判定** —— 判据是「载体在册且 Passed」,不是「条数等于某个数」。原文加删除线保留。

---

## Verdict: **PASS**

六条判据:5 条 PASS、1 条 N/A(无可验对象,非未验)。

**这是 DoD 第 7 条(`Smoke check 通过`)的兑现件。**

---

## 边界声明(禁借绿)

- **`failed 0` 不得读作「M3 已达成」** —— `未闭 P0 面 = 73` 一位不动
  (承 `qa-signoff-sprint-16-2026-10-21.md` §0 边界 ③ · §8.3)。
- **判据 1 是 N/A 不是 PASS** —— 本 sprint 无可验的运行期面,不是验过了。
- **构建门探针轮只证明了 `PlayerAssemblyGate` 在构建链里被调用** ——
  它**没有**证明该门在 player 侧对那 4 个原始肇事件的行为(那 4 件的 player 侧仍是 NOT-RUN,
  走 `Assert.Inconclusive`,不计失败也不计通过)。
- **IL2CPP 三格矩阵仍非齐** —— 本 sprint 只跑 Linux-x64-IL2CPP 一格;
  ARM64 目标不存在(用户裁定长期未达),Windows / Apple Silicon 归发版前。
  > 🔁 **2026-10-21 补记**:该缺口**已于同日按格结清** —— `production/qa/evidence/c4-il2cpp-matrix-2026-10-21.md`。
  > 第 1 格 Mono `3/3/0/0` · 第 2 格 IL2CPP-x64 `3/2/0/1` **+ 外部 diff 19/19 逐位相同 = VERIFIED** ·
  > 第 3 格 ARM64 **不可达**(`Variations/` 零 arm,实测)。原文保留作沿革 —— 本报告的时点结论不变。
- **本报告不覆盖 DoD 第 8 条** —— QA 签核(`/team-qa sprint`)未跑,缺 `qa-signoff-sprint-17-*.md`。
  > 🔁 **2026-10-21 补记**:该缺口**已于同日关闭** —— 签核件 = `production/qa/qa-signoff-sprint-17-2026-10-21.md`。
  > **本报告自身仍是 smoke 判据件,不因签核完成而扩大覆盖范围**;原文保留作沿革。
- **本报告不覆盖 DoD 第 10 条** —— S17-5 无独立代码评审原件。
  > 🔁 **2026-10-21 补记**:该缺口**已于同日关闭** ——
  > `production/qa/evidence/review-s17-5-retro-backfill-2026-10-21.md`(APPROVED WITH SUGGESTIONS,2 MINOR)。原文保留。
