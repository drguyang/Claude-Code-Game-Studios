# ADR-012 双格确定性矩阵 —— 2026-10-27 实测证据原件

> **日期**:2026-10-27 · 截至时刻 `2026-10-27T10:37Z`
> **批次**:sprint-20 收口后补判(用户裁定「落两格 + 第三格登记未达」)
> **commit**:`e095b7b`(工作树参差,见下方 §六 边界 —— 本证据的**可复算是靠工具+金标准**,
> 不是靠 commit 独一性;**两格落盘件与金标准三者的 SHA-256 已在此登记**)
> **执行载体**:`tools/determinism-matrix/run_matrix.py`(2026-10-27 立,本机可重跑)
> **ADR 依据**:`docs/architecture/adr-012-cross-platform-determinism-ci-gate.md` §Validation
> 「单元级黄金哈希三格全绿」复选框的 🔁 2026-10-27 注记

---

## 零、这份证据**不**声称什么(先读,禁借绿)

| 不声称 | 原因 |
|---|---|
| ❌ **「三格矩阵齐」** | 判据面 = **2 / 3 格**。第三格 ARM64 **目标不存在**(见 §四) |
| ❌ **`AC-18-15` 已验收** | 见下方 ⚠️ **判据面差异** —— 本矩阵测的是 **item-database 的 19 条黄金哈希**(`AC-21a-28/29` 面),**不是** processing 的三出参 |
| ❌ **CI 常驻性** | 缺 `UNITY_CLIENT_ID` / `UNITY_CLIENT_SECRET`(**`TD-ci-01` · EXTERNAL**),agent 侧无任何路径可取得 |
| ❌ **金标准刷新协议已演示** | ADR-012 §Validation 该项**仍未勾**,本证据不涉及 |
| ❌ **全量回归绿** | 本矩阵只跑确定性判据面的三个 `TestFixture`;**全量回归归 `smoke-check`**,两者数字**不可互相引用** |

### ⚠️ 判据面差异(**本证据最重要的一条边界**)

`AC-18-15` 要的是 **processing 的三出参**(`(recipe, L, 输入实例集, EquipMod, 两 EnvMod 分量)`
⇒ 逐位相同的出参)。本矩阵跑的是 **item-database 的 19 条黄金哈希**,两者**不是同一条 AC**。

| | `AC-21a-28/29`(本证据实测的) | `AC-18-15`(仍未验收的) |
|---|---|---|
| 被测对象 | `Fix` 四则 / `SplitMix64` / `CDF walk` / 编解码器往返(**19 条场景**) | processing F-18.1 契约的三出参 |
| 黄金值来源 | `tests/unit/item_database/golden/golden-v1.txt`(独立 Python 实现产出) | **无**(processing 侧零黄金夹具) |
| 载体 | `DeterminismGoldenCrossplatformTest`(PlayMode,可进 player) | `recipe_settlement_solver_test.cs` —— **PlayMode 但为同后端两入口对拍**,非跨后端 |

**实测佐证(本证据本轮实跑)**:

```bash
grep -rn "AC29_OUT\|AC29_GOLDEN" unity/Assets/Tests/ --include=*.cs
# 唯一命中:unity/Assets/Tests/PlayMode/determinism_golden_crossplatform_test.cs(item-database 面)
grep -rln "golden\|Golden" unity/Assets/Tests/EditMode/Processing/
# 零命中 —— processing 侧无黄金哈希夹具
```

⇒ **本证据对 `AC-18-15` 的作用 = 证明「矩阵载体可跑」,不证明「AC-18-15 的判据通过」。**
`AC-18-15` 的验收面**仍缺一环**:processing 侧须先有可进 player 的黄金夹具
(把三出参钉成版本化黄金值),这条**本条不解除**。它是 ADR-012 之外的**新 gap**,
记于 §五 残余项 ①。

---

## 一、两格是什么、结果如何

| 格 | 后端 | 平台 | XML | 结果 |
|---|---|---|---|---|
| **格 1** | Linux-x64-**Mono** | `EditMode` | `matrix-mono-EditMode.xml` | `total=53 passed=53 failed=0 skipped=0` |
| | | `PlayMode` | `matrix-mono-PlayMode.xml` | `total=3 passed=3 failed=0 skipped=0` |
| **格 2** | Linux-x64-**IL2CPP** | `StandaloneLinux64`(构建并运行 player) | `matrix-il2cpp-StandaloneLinux64.xml` | `total=3 passed=3 failed=0 skipped=0` |

**复算命令(任一条可独立重跑)**:

```bash
cd <repo>
python3 -I tools/determinism-matrix/run_matrix.py            # 两格全跑 + 对拍(约 12 min)
python3 -I tools/determinism-matrix/run_matrix.py --dry-run  # 只打印命令与前置检查
```

---

## 二、五条判据逐条结账

工具 `run_matrix.py` 的判据(与 AC-29 / ADR-012 口径逐字对齐,**不自创**):

| # | 判据 | 结果 | 实跑输出 |
|---|---|---|---|
| ① | 每格测试套件**零 `failed`** | ✅ | 三套件 `failed=0`(见 §一 表) |
| ② | 两格各自把 **19 条**场景哈希落盘(`AC29_OUT`) | ✅ | 两格各 `19 vs 19 条` |
| ③ | 两格落盘件**逐位相同**(外部 diff,非测试内断言 —— 防自指) | ✅ | `mono vs il2cpp: ✅ 逐位相同` |
| ④ | 两格落盘件都等于**独立 Python 金标准** | ✅ | 两格均 `✅ 逐位相同(19 vs 19 条)` |
| ⑤ | 落盘件头自报的后端标记**确实是两个不同后端**(防「拿 Mono 冒充 IL2CPP」) | ✅ | `Mono` vs `IL2CPP` |

> ⚠️ **判据 ① 单独成立不构成矩阵绿** —— 这正是 ADR-012 原话「测试绿**但 diff 不同**等价于红」。
> 工具因此把 ① 只当作必要条件;本轮 ①–⑤ **五条全过**。

### 2.1 三方 SHA-256(供第三方独立复核,**不依赖 commit 独一性**)

```
0f064869516a3c3970bb6631b6259d1e1f3d486ab5e36fa9ddec36bd11e7ad55  tests/unit/item_database/golden/golden-v1.txt
4171bff316f066735f066bde237210cc0d3b18e407331068871e649e9fabba45  unity/Logs/determinism-matrix/computed-mono.txt
a008772a273241ad85cae95ed29cc8ebdb5620d09cb2ac12733e17a2e51208cc  unity/Logs/determinism-matrix/computed-il2cpp.txt
```

⚠️ **两格落盘件的完整字节不同是预期的** —— 头两行是 provenance(`backend=` / `platform=`),
判据 ③④ 比的是**去注释后的 19 行正文**。逐字节相同的是正文,不是整个文件。

### 2.2 落盘件头标(后端标记相异的直接证据)

```
mono    : # ac29 backend=Mono   | Mono 6.13.0 (explicit/9d7c9afd)
          # unity=6000.3.24f1 platform=LinuxEditor
il2cpp  : # ac29 backend=IL2CPP | Mono Unity IL2CPP (Sep 25 2026 00:43:16)
          # unity=6000.3.24f1 platform=LinuxPlayer
```

两个后端标记来自 `RuntimeInformation.FrameworkDescription`(`determinism_golden_crossplatform_test.cs:258-263`)。
> ⚠️ 该判据首版曾**为假**(2026-09-24):初版以 `Mono.Runtime` 类型存在性判后端,IL2CPP 同样生成该类型
> ⇒ IL2CPP 腿误标 `backend=Mono`。当日改为 `FrameworkDescription` 含 `IL2CPP` 判据并重取证。
> **该缺陷未复发**,本轮两格标记相异。

---

## 三、本轮修掉的两处工具缺陷(如实记账)

工具是**本轮新写的**,首跑不是一次绿 —— 两个缺陷都是**实测暴露**的,不是预先想到的。

### 3.1 测集过滤器漏了一族(首跑 EditMode 只到 48 测)

- **现象**:首跑格 1 EditMode `total=48`,而基线是 53。
- **根因**:过滤器写 `GoldenHashV1|DeterminismGoldenCrossplatform|TelemetryGolden`,
  漏了 `ItemDatabase.DeterminismGoldenFixturesTest`(它名字里**没有** `Crossplatform`)——
  而那恰是**直接读 `golden-v1.txt` 的那一族**。
- **处置**:改为 `GoldenHashV1|DeterminismGolden|TelemetryGolden`,重跑 ⇒ `total=53 passed=53`。
- **留痕**:`run_matrix.py:62-66` 有订正注释。
- ⚠️ **这是「判据面悄悄缩小」的典型形态** —— 若不比对基线条数,48 也会被判绿。

### 3.2 player 腿没注入 env ⇒ 19 条就地断言整段被 Skip

- **现象**:首跑格 2 `total=3 passed=2 failed=0 skipped=1`。
- **根因**:`test_ac21a29_allScenarioHashes_writtenAndCompared_whenGoldenReachable` 命中
  `Assert.Ignore`(`determinism_golden_crossplatform_test.cs:289-294`)—— player 内
  `Application.dataPath` 指向 `Game_Data` ⇒ 仓库相对回退路径不可达。
  **只有「落盘」那一半生效,19 条就地断言整段没跑。**
- **为什么必须修**:夹具自陈「判决由外部 diff 承担」,外部 diff 本工具做了(**判据 ③④ 因此仍成立**);
  但 2026-09-24 原证据件第 2 跑**注入 env 后是 15/15 Passed、0 Skipped**
  ⇒ 本工具若不注入,**证据强度就比既有证据弱一格**。
- **处置**:`run_cell` 统一注入 `AC29_OUT=<outdir>/computed-<cell>-<plat>.txt` + `AC29_GOLDEN=<金标准绝对路径>`。
  重跑 ⇒ 格 2 `total=3 passed=3 failed=0 skipped=0`。
- **留痕**:`run_matrix.py:187-197` 有订正注释(含「首跑 1 测被 Skip」的原文记录)。

### 3.3 后端归位:语义正确 ≠ 字节正确

首跑 `exit 3`,报 `ProjectSettings 未逐字节还原`。**工具拦得对** —— 这是真问题:

- `Ac29Il2CppSwitch.SetMono` 调 `PlayerSettings.SetScriptingBackend(..., Mono2x)`
  ⇒ Unity 把 **`Standalone: 0` 显式写进** `scriptingBackend:` 段;
- 而基线里**该键根本不存在**(缺省即 Mono2x)⇒ 语义等价,字节不同。

**处置**:开跑前存 `ProjectSettings.baseline.bak`,归位时 `SetMono`(语义)+ **`cp` 回字节副本**,
再以 SHA-256 校验。重跑 ⇒ `✅ SHA-256 逐字节还原通过(0fc69c20204e9954…,= 基线)`,
`git status --porcelain unity/ProjectSettings/ProjectSettings.asset` **零输出**。

> ⚠️ **全程未用 `git checkout`** —— 它会抹掉未提交改动(本项目既有纪律)。
> 还原判据 = **SHA-256 逐字节**,不是 `grep`。

---

## 四、第三格 ARM64 —— **不可达**,不是「未跑」

**依据(2026-10-17 探针,`BuildTargetProbeCli`,可复跑)**:

```
Unity 6000.3.24f1 · LinuxEditor · x86-64
── ① BuildTarget 枚举共 46 项 ──
   含「arm」的目标:(无)          ← 46 个目标里一个 arm 都没有
── ② IsBuildTargetSupported ──
   StandaloneLinux64 = ✅ 受支持   ← 唯一受支持的 Linux 目标 = x64
   StandaloneLinux / EmbeddedLinux / LinuxHeadlessSimulation / StandaloneLinuxUniversal = ❌
── ③ PlaybackEngines ──
   LinuxStandaloneSupport └ il2cpp · mono
```

**旁证**:Unity Hub `releases-linux.json`(2020/2021/2022 LTS 三版)的 Linux 模块**只有**
`linux-il2cpp`(x64)与 `linux-server`,**无任何 ARM64 模块**。

⇒ **第三格在本 pinned 工具链上结构上不可达成。** 用户两次裁定(2026-10-17 / 2026-10-27)
**维持三格原样、登记为长期未达**(不降格 / 不换面 / 不改挂)。

**解锁条件(唯一)**:引擎升级后重跑探针;若出现受支持的 ARM64 目标,该格**自然解锁**。

---

## 五、残余项(不冒充已跑 · 禁借绿)

| # | 项 | 状态 | 归属 |
|---|---|---|---|
| ① | **processing 侧黄金夹具**(把 `AC-18-15` 的三出参钉成版本化黄金值) | **不存在** —— 见 §零 判据面差异 | **新 gap**,归 processing 实现轮;本条**不解除** `AC-18-15` |
| ② | **格 3 ARM64-IL2CPP** | **不可达**(目标不存在) | ADR-012 · 用户裁定「长期未达」 |
| ③ | **CI 常驻性** | **EXTERNAL** —— 缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET` | `TD-ci-01` · 凭证只有用户侧能申请 |
| ④ | **金标准版本化刷新协议演示**(公式变更 → `golden-v2` + 变更日志 + 三格重签 + 单平台独签被拒) | **未做** | ADR-012 §Validation 该项**仍未勾** |
| ⑤ | **Windows-x64 / Apple Silicon** | **未跑** | ADR-012 §二:归**发版前必跑**,不进常驻 |
| ⑥ | **测集窄** —— 只覆盖确定性判据面的三个 `TestFixture`,非全量 | 设计如此 | 全量回归归 `smoke-check`,**两者数字不可互相引用** |
| ⑦ | **`AC-18-15` 本体** | **仍未验收** | 见 §零;`AC-18-15` 的复选框**不因本证据而勾** |

---

## 六、可复现性边界(**诚实登记**)

- **本证据的复算靠「工具 + 金标准」,不靠 commit 独一性** —— 本轮工作树含 sprint-20 收口的
  并行改动,`git rev-parse HEAD` = `e095b7b` 但**工作树 ≠ 该 commit 的树**。
- 因此本证据给出的复算路径是:三份文件的 **SHA-256**(§2.1)+ **重跑同一工具**(§一 复算命令)。
  任一第三方按此重跑,应得到**同样的 19 行正文**;若不同,即**判据不成立**,无须相信我。
- **落盘目录 `unity/Logs/` 已 gitignore**(`.gitignore:251`)⇒ 矩阵产物**不进库**,
  `git status` 只应看到 `?? tools/determinism-matrix/`。
  ⚠️ **这意味着 `computed-*.txt` 不随仓库分发** —— 第三方复核须**自己重跑**生成,
  不能 `git show` 取回。这是**刻意的**(落盘件是派生物,真源是金标准 + 工具)。

---

## 七、结论

- **判据面 = 2 / 3 格 · 结果 = ✅ 两格 VERIFIED**(五条判据全过,`exit 0`)。
- **`AC-18-15` 仍未勾**,且本证据**不改变**这一点。本证据把它的阻塞面**再收窄一格**:
  从「矩阵不存在」降级为「**两格已可重跑并可复算;缺的是 processing 侧黄金夹具 + 第三格(不可达)+ 常驻性(缺凭据)**」。
- **ADR-012 §Validation 的「单元级黄金哈希三格全绿」复选框仍不得勾** ——
  判据是**三格齐 + 常驻 + 版本化刷新协议**,三样缺两样半。
- **两格可重跑 = 把缺口缩小,不是把缺口填平。**

**证据产出物**:
- 工具:`tools/determinism-matrix/run_matrix.py` + `README.md`
- 运行输出:`unity/Logs/determinism-matrix/run-both.out`(gitignored)
- 落盘件:`unity/Logs/determinism-matrix/computed-{mono,il2cpp}.txt`(gitignored,§2.1 已登记哈希)
- XML:`unity/Logs/determinism-matrix/matrix-*.xml`(gitignored)
