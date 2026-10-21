# C-4 补跑记录 —— IL2CPP 三格矩阵(2026-10-21)

**对象**:`qa-signoff-sprint-17-2026-10-21.md` §5 条件 **C-4**(PlayMode 全量 / IL2CPP 三格矩阵 / CI 常驻)
**本件范围**:**只结清 C-4 的 IL2CPP 三格矩阵半边**。PlayMode 全量半边见
`c4-playmode-full-2026-10-21.md`;CI 常驻半边**仍开**。

---

## 1. 为什么需要这一轮

签核件 §4 第 2 行记:

> **IL2CPP 三格矩阵** —— **非齐**。Linux-x64-IL2CPP 单格 ✅(`s17-6s-il2cpp-player.xml` = `5/5/0/0`,
> 仅 5 测);**ARM64 目标不存在**(pinned 工具链无 arm BuildTarget,用户裁定长期未达);
> Windows-x64 / Apple Silicon 归发版前,不进常驻。

本轮把 ADR-012 §二 的三格矩阵**逐格跑/逐格验可达性**,不再留「非齐」这种无法判读的措辞。

---

## 2. 三格结果总表

| 格 | 目标 | 状态 | total / passed / failed / skipped | 判据 |
|---|---|---|---|---|
| **1** | Linux-x64 **Mono** | ✅ **已跑** | **3 / 3 / 0 / 0** | `unity/Logs/matrix-mono.xml`;Editor 内跑,金标准可达 ⇒ 就地逐位断言通过 |
| **2** | Linux-x64 **IL2CPP** | ✅ **已跑 · VERIFIED** | **3 / 2 / 0 / 1** | `unity/Logs/matrix-il2cpp-results.xml`;1 条 Skipped 是设计内的退化路径,**判决由 §3 的外部 diff 承担且逐位相同** |
| **3** | Linux-**ARM64** IL2CPP | ⛔ **不可达** | — | pinned 工具链零 arm 目标(§4 实测);用户裁定长期未达 |

**溯源三元组**:截至时刻 `2026-10-21 19:35 CST` · 批次 `C-4 补跑(IL2CPP 矩阵)` · commit `1cd5780`

**测集**:`DeterminismGoldenCrossplatformTest`(AC-29 的跨平台确定性黄金夹具,ADR-012 双级矩阵的
**集成级字节**半边)。三格跑的是**同一测集**,判据可比。

---

## 3. 第 2 格:那条 `Skipped` 的成因与外部 diff

### 3.1 结果原文

```
total=3  passed=2  failed=0  inconclusive=0  skipped=1

Skipped  …DeterminismGoldenCrossplatformTest.test_ac21a29_allScenarioHashes_writtenAndCompared_whenGoldenReachable
Passed   …DeterminismGoldenCrossplatformTest.test_ac21a29_f7_fixMulPeakProducts_knownValuesUnderCurrentBackend
Passed   …DeterminismGoldenCrossplatformTest.test_ac21a29_f7_splitmix64Wraparound_knownValuesUnderCurrentBackend
```

### 3.2 为什么是 Skipped(不是失败,也不是通过)

`unity/Assets/Tests/PlayMode/determinism_golden_crossplatform_test.cs:289-294`:

```csharp
string goldenPath = ResolveGoldenPath();
if (!File.Exists(goldenPath))
{
    Assert.Ignore($"金标准不可达({goldenPath})—— player 未注入 AC29_GOLDEN;本测退化为「只落盘」," +
                  "判决由外部 diff 承担(AC-29 口径:测试绿且外部 diff 逐位相同才 VERIFIED)");
}
```

`ResolveGoldenPath()`(`:234-241`)的回退是**仓库相对路径**:
`Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..")) + tests/unit/item_database/golden/golden-v1.txt`。
player 内 `Application.dataPath` 指向 `/tmp/matrix-il2cpp/Game_Data` ⇒ 该相对路径不可达 ⇒ 命中 `Assert.Ignore`。

**这是测试自身写死的退化路径**,不是本轮配置失误 —— 其自陈判据是「测试绿**且**外部 diff 逐位相同才 VERIFIED」。
⇒ 外部 diff 是**必做项**,不是可选项。§3.3 补上。

### 3.3 外部 diff(判决承担方)

**落盘件**(测试在 Skip 前**无条件**写,`:275-284`):

```
/home/gu/.config/unity3d/DefaultCompany/unity/ac29-computed.txt
# ac29 backend=IL2CPP | Mono Unity IL2CPP (Sep 25 2026 00:43:16)
# unity=6000.3.24f1 platform=LinuxPlayer
（19 条：B01/B02/B03 + S01…S16）
```

**金标准**:`tests/unit/item_database/golden/golden-v1.txt`(19 条;产出方 = 独立 Python 参考实现
`golden_v1_reference.py`,与 C# 实现零共享代码)

**diff 命令与结果**:

```bash
diff <(grep -v '^#' golden-v1.txt | sort) \
     <(grep -v '^#' ac29-computed.txt | sort)
# → 无输出
```

```
✅ 逐位完全相同
golden:   19 条
computed: 19 条
```

⇒ **第 2 格判 VERIFIED**(按 AC-29 口径:落盘成功 + 外部 diff 逐位相同)。

### 3.4 后端确证(防「拿 Mono 结果冒充 IL2CPP」)

| 判据 | 实测 |
|---|---|
| `GameAssembly.so` | 82,049,064 字节 ✅ |
| `global-metadata.dat` | 8,936,608 字节 @ `Game_Data/il2cpp_data/Metadata/` ✅ |
| `Game_Data/` 下有 `Managed/`? | **无**(`Managed/` 只在 `Game_BackUpThisFolder_ButDontShipItWithYourGame/` 里)✅ |
| 落盘件头自报后端 | `backend=IL2CPP \| Mono Unity IL2CPP` ✅ |
| 测试实际跑在哪 | Editor 经 `RemoteTestRunController` 驱动 player 并回传(`TestLauncherBase` / `RemotePlayerTestController:RunFinished`) |
| 构建结果 | `Build Finished, Result: Success.`(`matrix-il2cpp.log:11215`) |

### 3.5 复现命令

```bash
UNITY=/home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity
cd unity
# ① 切 IL2CPP
unity run "$PWD" -- -executeMethod Ac29Il2CppSwitch.SetIl2cpp
# ② Editor 直调 player 跑测(注意:不是 ./Game.x86_64 -runTests —— 该 player 未编入 UTF 运行器)
timeout 1500 "$UNITY" -batchmode -nographics \
  -projectPath "$PWD" -runTests -testPlatform StandaloneLinux64 \
  -testFilter DeterminismGoldenCrossplatform \
  -testResults "$PWD/Logs/matrix-il2cpp-results.xml" \
  -logFile "$PWD/Logs/matrix-il2cpp.log"
# ③ 外部 diff
diff <(grep -v '^#' ../tests/unit/item_database/golden/golden-v1.txt | sort) \
     <(grep -v '^#' ~/.config/unity3d/DefaultCompany/unity/ac29-computed.txt | sort)
```

---

## 4. 第 3 格:ARM64 —— 不可达(实测,非推断)

```
$ ls .../6000.3.24f1/Editor/Data/PlaybackEngines/
LinuxStandaloneSupport

$ ls .../PlaybackEngines/LinuxStandaloneSupport/Variations/
il2cpp
linux64_player_development_il2cpp
linux64_player_development_mono
linux64_player_nondevelopment_il2cpp
linux64_player_nondevelopment_mono
mono

$ find .../PlaybackEngines/ -maxdepth 3 -iname '*arm*'
（零命中）
```

pinned 工具链 **6000.3.24f1 根本没装任何 ARM 目标** ⇒ 这一格不是「没跑」,是**跑不了**。

**处置**:承 2026-10-17 用户裁定「维持三格原样,登记为长期未达」。
**不假装跑过**,**不擅自把矩阵改成两格**,**不用 qemu 代替**(ADR-012 已否决:softfloat 不可信为逐位判据)。

---

## 5. 第 1 格(Mono)原文

```
$ python3 -c "…解析 matrix-mono.xml…"
  Passed   …test_ac21a29_allScenarioHashes_writtenAndCompared_whenGoldenReachable
  Passed   …test_ac21a29_f7_fixMulPeakProducts_knownValuesUnderCurrentBackend
  Passed   …test_ac21a29_f7_splitmix64Wraparound_knownValuesUnderCurrentBackend
合计: 3 3 0 0
```

Editor(Mono)内跑 ⇒ 金标准**可达** ⇒ 该条**就地逐位断言通过**(不依赖外部 diff)。

---

## 6. 还原(工程设置回到原始态)

矩阵期间 `Ac29Il2CppSwitch.SetIl2cpp` 改了 `unity/ProjectSettings/ProjectSettings.asset`
(`scriptingBackend.Standalone: 1`)。**备份 → 还原 → SHA-256 逐字节校验**(不用 `git checkout`):

```
$ cp /tmp/ps-before-matrix.asset unity/ProjectSettings/ProjectSettings.asset
$ sha256sum -c
unity/ProjectSettings/ProjectSettings.asset: 成功
  期望 = 0fc69c20204e99549928eb93670121daef2ecf8316457d7d7cf1729f8aff89fb

$ grep -A3 scriptingBackend unity/ProjectSettings/ProjectSettings.asset
  scriptingBackend:
    Android: 1
  il2cppCompilerConfiguration: {}

$ git status --short
（空）
```

⇒ 工作树干净,**Unity 工程已释放,可重开编辑器**。

---

## 7. 边界与未结项(禁借绿)

| 项 | 状态 | 说明 |
|---|---|---|
| **第 1 格 Mono** | ✅ 已跑 | 3/3/0/0,就地断言通过 |
| **第 2 格 IL2CPP-x64** | ✅ **VERIFIED** | 2 passed + 1 skipped(设计内退化)+ 外部 diff 19/19 逐位相同 |
| **第 3 格 IL2CPP-ARM64** | ⛔ **不可达** | pinned 工具链零 arm 目标;长期未达(2026-10-17 用户裁定) |
| **Windows-x64 IL2CPP / Apple Silicon** | ⛔ 未跑 | ADR-012 §二:归**发版前必跑**,不进常驻矩阵 |
| **CI 常驻 job** | ⛔ **从未跑** | 缺 `UNITY_CLIENT_ID` / `UNITY_CLIENT_SECRET` —— **本件不结清这一半** |
| **矩阵覆盖的测集** | ⚠️ 仅 3 测 | 三格跑的是 `DeterminismGoldenCrossplatform`(AC-29 集成级字节半边)。**不覆盖** EditMode 的单元级哈希半边(那半边归 Mono 常驻全量回归) |

> ⚠️ **本件不得被读作「C-4 整条结清」**。C-4 三半:PlayMode 全量(另有件)· IL2CPP 三格(本件)·
> CI 常驻(**仍开**)。
