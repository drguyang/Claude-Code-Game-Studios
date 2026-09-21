# Test Infrastructure

**Engine**: Unity 6.3 LTS(URP)
**Test Framework**: Unity Test Framework(UTF / NUnit)+ Unity Test Runner
**CI**: `.github/workflows/tests.yml`
**Setup date**: 2026-09-20

## 权威件

| 来源 | 约束本目录的内容 |
|---|---|
| `.claude/docs/coding-standards.md` §Testing / §What NOT to Automate | 目录布局、命名、确定性、禁自动化清单 |
| `docs/architecture/adr-012-cross-platform-determinism-ci-gate.md` | **CI 矩阵**(三格常驻 + 两格发版前)、双级黄金夹具、IL2CPP 对拍须独立 job |
| `docs/architecture/adr-006-fixed-point-boundary-contract.md` §Decision 五 | `Fix` 编码器探针(见 `unit/sim/`),D-21-18 |
| `docs/architecture/architecture.md` §Required ADRs #2 | **`.asmdef` 刻意缺席**(见下「未生成之物」) |
| `.claude/docs/technical-preferences.md` §Testing | 框架选择;必测四项(判断链 / 熟练度成长 / 战斗伤害 / 联机状态同步) |

## Directory Layout

```
tests/
  EditMode/         # UTF 的 EditMode 程序集(纯逻辑:判断链、熟练度成长、伤害公式、库存)
  PlayMode/         # UTF 的 PlayMode 程序集(集成:急救动作流程、建造、联机基础)
  unit/[system]/    # 按系统分目录的单元测试源文件(coding-standards 的落点约定)
  integration/[system]/
  smoke/            # /smoke-check 读的 15 分钟关键路径清单
  evidence/         # 截图与手工签核记录(Visual/Feel / UI 类故事的证据)
```

> ⚠️ **Unity 工程落地时的合并事项**:`unit/` 与 `integration/` 是**按系统分目录的账本约定**;
> UTF 实际编译的是 `EditMode/` 与 `PlayMode/` 两棵程序集树。实现期把 `unit/[system]/*.cs`
> 纳入 `EditModeTests.asmdef`、`integration/[system]/*.cs` 纳入 `PlayModeTests.asmdef`
> (或用 `parentFolder` 引用),**不要**为了迁就 asmdef 而废弃按系统的目录语义 ——
> 后者是 `/gate-check` 与 story 证据的检索面。

## 未生成之物(刻意)

1. **`.asmdef` 文件**(`EditModeTests.asmdef` / `PlayModeTests.asmdef` / `Sim.asmdef` 等)——
   程序集**名称与清单**曾归 `architecture.md` §Required ADRs **#2「契约程序集清单与命名」** 裁决;
   **✅ 该 ADR 已兑现 = `ADR-025`(2026-09-20 Accepted)** —— 六装配清单已具名
   (`Sim` / `Sim.Contracts` / `Sim.Codec` / `Gameplay.Presentation` / `Gameplay.UI` / `Editor.Tools`),
   **命名阻塞已解除**。`.asmdef` 仍**刻意缺席**:工程本体(`Assets/` / `Packages/` /
   `ProjectSettings/`)不存在,脚手架生成归**实现轮**。**改后不生成任何文件 —— 决定不变。**
2. **Unity 工程本体** —— 仓库尚无 `Assets/` / `Packages/` / `ProjectSettings/`。
   `Packages/manifest.json` 须含 `com.unity.test-framework`(UTF 包)。
3. **黄金夹具字节文件**(`golden-vN`)—— ADR-012 裁定「版本化刷新 + 全体平台同时重签」;
   夹具签名是跨平台实测产物,不是脚手架,归实现轮。

## Running Tests

**编辑器内**:`Window → General → Test Runner` → 选 EditMode / PlayMode 页签 → Run All
(Unity Test Framework 随 Unity 6.3 附带,无需单独安装)

**命令行(headless,CI 用的同一条路径)**:

```bash
# EditMode
Unity -batchmode -projectPath . -runTests -testPlatform EditMode \
      -testResults ./test-results/editmode.xml -logFile -

# PlayMode
Unity -batchmode -projectPath . -runTests -testPlatform PlayMode \
      -testResults ./test-results/playmode.xml -logFile -
```

> **ADR-012 F1 的硬约束**:`unity-test-runner` **只在 Editor(Mono)跑 UTF**。
> IL2CPP 的逐位对拍**不能**由这两个命令得出 —— 必须先用 `unity-builder` 出 player,
> 再由**独立 job** 执行夹具对拍。CI 文件即按此拓扑写。

## Test Naming

- **Files**: `[system]_[feature]_test.cs`(如 `sim_fixedpoint_test.cs`)
- **Functions**: `test_[scenario]_[expected]`(如 `test_FixEncodeDecode_roundTripsExactBits()`)
- 两条纪律(coding-standards):**确定性**(无随机种子、无时间依赖断言)、**自 setup/teardown**(不依赖执行顺序)

## Story Type → Test Evidence

| Story Type | Required Evidence | Location | Gate |
|---|---|---|---|
| Logic | 自动化单元测试 —— 须通过 | `tests/unit/[system]/` | BLOCKING |
| Integration | 集成测试 **或** playtest 文档 | `tests/integration/[system]/` | BLOCKING |
| Visual/Feel | 截图 + lead 签核 | `tests/evidence/` | ADVISORY |
| UI | 手工走查文档 **或** interaction test | `tests/evidence/` | ADVISORY |
| Config/Data | Smoke check 通过 | `production/qa/smoke-*.md` | ADVISORY |

## What NOT to Automate

承 `coding-standards.md`:渲染保真度(shader 输出 / VFX 外观 / 动画曲线)、「手感」
(输入响应性、可感知的重量、时机)、平台特定渲染、完整 gameplay session。
**本项目附加**:急救动作的**主观手感**不自动化 —— 但 `L_input < 50 ms` 的**预表现路径**
是可测的(承 `technical-preferences.md` 已裁定的切分口径:`AC-10-08` 只测 `L_input`,
`L_eval` 归 9 的求值节奏,**两者不得重新合并成「端到端 < 50 ms」**)。

## CI

矩阵(承 ADR-012 裁定,**不是模板默认的单 job**):

| Job | 常驻 | 内容 |
|---|---|---|
| `Linux-x64-Mono` | ✅ | UTF EditMode + PlayMode;单元级黄金夹具哈希 |
| `Linux-x64-IL2CPP` | ✅ | `unity-builder` 出 player → **独立 job** 跑 IL2CPP 对拍 |
| `Linux-ARM64-IL2CPP` | ✅ | 交叉构建对拍 |
| `Windows-x64-IL2CPP` | ❌ 发版前必跑 | 同上 |
| Apple Silicon | ❌ 发版前必跑 | 同上 |

**qemu 已否决**(ADR-012:softfloat 不可信为逐位判据)。
**禁单平台独签**夹具;刷新须全体平台同时重签、旧版保留回归对比。
测试失败阻断合并 —— 禁为过 CI 而 disable/skip 测试。

> **Secret 前置**:Unity CI 需要 `UNITY_LICENSE`(Game CI activator 产物)配到仓库 secrets。
> 手工配置,不自动化。**未配置前 CI 三格全红是预期状态,不是测试缺陷。**
> 亦见 ADR-012 **BLOCKING spike(F7)**:C# `int64` 溢出为定义性回绕,
> 而 IL2CPP 生成的 C++ 有符号溢出为 **UB** —— `SplitMix64` 与 Q16.16 中间乘正踩此线。
