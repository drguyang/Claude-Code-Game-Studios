# ADR-012: 跨平台确定性 CI 门(黄金夹具矩阵与 IL2CPP 编译旗标登记)

## Status

Accepted

> **2026-09-15 起草并转 Accepted。** 三条用户裁定已锁:① **双级黄金夹具**(单元级黄金哈希 +
> 集成级黄金字节,字节错位可下钻到单元级定位);② **三格全免费常驻矩阵**
> (Linux-x64-Mono + Linux-x64-IL2CPP + Linux-ARM64-IL2CPP 交叉构建;Windows-x64-IL2CPP 列
> **发版前必跑**不进常驻);③ **版本化刷新**(夹具带 manifest 版本,刷新 = 新版本目录 + 变更日志
> + 全体平台同时重签,旧版保留回归对比)。
> 引擎侧经 unity-specialist lean 复核(2026-09-15):**无引擎侧 blocker**;结论并入 §Risks
> (F1 双构建 job · F2 ARM64 交叉构建 / qemu 否决 · F3 旗标 per-target 登记 · F4 单元级对拍进 player ·
> F5 SHA256 非漂移源 · F6 夹具双投递 · **F7 表示选择(int64→ulong,2026-09-21 承 RC-4 降级)** ·
> F8 Apple Silicon 发版前验证)。
> 独立评审由下一轮 `/architecture-review` 进行。

## Date

2026-09-15

## Last Verified

2026-09-15

## Decision Makers

dr_guyang(用户 · **2026-09-15 三条裁定,均照准**)· technical-director(起草与裁决)
· devops-engineer(CI 矩阵与构建管线)· unity-specialist(引擎复核,2026-09-15)
· 系统 9 疾病与伤情 / 8 诊断与体征 / 21a 物品与配方 / 7a 持久化服务

## Summary

ADR-005 / 006 / 009 / 010 均已声明「IL2CPP 与 Mono 逐位一致实测」「黄金字节夹具」「黄金对拍」
类验证义务,但**没有任何执行载体** —— `tests/` 与 `.github/workflows/` 不存在,四条跨平台确定性
TR(`TR-disease-015/016`、`TR-itemdb-022`、`TR-diag-018`)全部 `partial`(报告 B-9:「ADR-005 的
『两个平台逐位相同』判据是纸面的」)。本 ADR 裁决:**双级黄金夹具**(单元级哈希 + 集成级字节)·
**三格全免费常驻矩阵**(Mono × IL2CPP × x64 × ARM64,交叉构建)·
**版本化刷新协议**(manifest 版本 + 全体平台重签 + 旧版保留)·
**IL2CPP 编译旗标逐目标登记**(per-target 切换 + PR 审查)。ADR-005/010 的逐位判据由此获得执行载体。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Core(确定性 / CI / 构建管线) |
| **Knowledge Risk** | **HIGH** —— 矩阵实现(game-ci Docker 镜像 / IL2CPP 交叉构建 / ARM64 runner / `SetAdditionalIl2CppArgs` / int64 回绕在 clang 各 `-O` 级的行为)均属 post-cutoff 知识,须 spike 实测;本裁决的**判据**(黄金哈希逐位相同)为纯规格,不受影响 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` · `architecture-review-2026-09-15.md`(R-5 · B-9 · E-4 · E-8) · `adr-005-deterministic-sim.md` · `adr-006-fixed-point-boundary-contract.md` · `adr-009-world-state-event-boundary.md` · `adr-010-persistence-save-format.md` · `.claude/docs/coding-standards.md`(CI/CD Rules) |
| **Post-Cutoff APIs Used** | **None 承诺** —— 矩阵用稳定机制(game-ci / BuildTarget / PlayerSettings),具体参数语义(如 `--compiler-flags=`)标「须 spike」实测后锁定 |
| **Verification Required** | ① **F7 表示选择(原 BLOCKING spike,2026-09-21 降级)**:C# `int64` 溢出为定义性回绕,IL2CPP 生成的 C++ 有符号溢出为 **UB**;`SplitMix64`(`z *= 0x9E3779B97F4A7C15`)与 Q16.16 hi/lo 中间乘**正踩此线** —— 承 `architecture-review-2026-09-20.md` RC-4:**无符号整数的溢出在 C# 与 C++ 两侧都是定义性回绕(mod 2ⁿ)** ⇒ 把 `SplitMix64` 内部状态与 Q16.16 中间乘积改住 `ulong`(必要时 `unchecked`),**UB 从「须实测确认不发生」变成「结构上不存在」**,F7 降级为一条 EditMode 断言(`SplitMix64` 已知向量对拍)。⚠️ 改 `ulong` 只消解 UB 一项,**不消解**「Mono 与 IL2CPP 逐位一致」的其余待实测项 —— 黄金矩阵照旧要跑(对应单元级对拍仍验 `ulong` 回绕向量);② **F2 spike**:x86-64 runner 交叉构建 ARM64 IL2CPP player → 原生 ARM64 runner 只跑 player,验证链路可用性;③ **F1 spike**:`unity-builder@v4` 出 IL2CPP player + Docker 镜像(`unityci/editor:ubuntu-*-linux-il2cpp-*`)可行性;④ `SetAdditionalIl2CppArgs --compiler-flags=` 语义实测(反汇编验无 FMA) |

> **Note**: Knowledge Risk HIGH —— 引擎升级(尤其 IL2CPP C++ 生成器 / clang 版本)时须重跑矩阵,不能只看文档。

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-005**(Accepted —— 逐位一致判据)· **ADR-006**(Accepted —— 编码器往返逐位还原 · 舍入模式)· **ADR-009**(Accepted —— 世界流逐位重建)· **ADR-010**(Accepted —— 黄金字节夹具定义存档字节流)|
| **Enables** | **9 / 21a / 8 / 7a 的确定性 AC 获得执行载体**(AC-1 / AC-5b / AC-21a-29 / AC-8-F5)· `TR-disease-015/016`、`TR-itemdb-022`、`TR-diag-018` 状态推进 · **一切代码阶段的黄金回归** |
| **Blocks** | 标 `[BLOCKING]` 的跨平台确定性 AC 验收;凡依赖「IL2CPP 实测」的门控(9 / 21a / 52 的 IL2CPP Release AC) |
| **Ordering Note** | 本 ADR 是 **R-5 的落点**,且是 **ADR-010 的后置**(黄金字节夹具 = 存档格式的逐位性可测面)。**先 Accepted 本 ADR,再建 tests/ 与 .github/workflows/**。`tests/`、`tests/integration/`、`.github/workflows/tests.yml` 属预门控三项(报告 :479-481),本 ADR 是它们的架构权威件 |

## Context

### Problem Statement

**判据是纸面的,直到有执行载体。** 四份已 Accepted ADR 与三份 GDD 都把「逐位一致」当作
阻塞级验收:

- ADR-005 §Verification:「IL2CPP 逐位性实测(各目标平台)」「同一病史事件流在两个不同平台产出逐位相同 Fix 读数」;
- ADR-006:IL2CPP 与 Mono 的整数逐位一致性实测「阻塞级」;
- ADR-009:「两平台重建地形逐位一致」;
- ADR-010:「同一存档在编辑器 vs IL2CPP 构建产出逐位相同的字节流」;
- GDD:`disease-simulation.md` AC-1 / AC-5b · `item-database.md` AC-21a-29(UNVERIFIED)· `diagnosis-system.md` AC-8-F5 · `random-events.md` AC-52-06 / AC-52-39 · `case-system.md` AC-37-06。

但 `tests/` 不存在、`.github/workflows/` 不存在、CI 无架构权威件 —— **没有一处执行载体**。
报告 B-9 直言:「没有它,ADR-005 的『两个平台逐位相同』判据是纸面的」。

**不裁决的代价**:P0 末期才发现 IL2CPP 某平台逐位漂移(如 F7 的 int64 回绕 UB,或 E-8 的负值舍入),
届时事件流重放全盘对不上 —— 这是**三个月级灾难**,正是本项目反复拒绝的那一类静默失败。

### Current State

- `tests/`、`tests/unit/`、`tests/integration/`、`.github/workflows/` **全部不存在**(预门控三项全 ❌)。
- 四条 TR 状态:`TR-disease-015`(AC-1)· `TR-disease-016`(AC-5b)· `TR-itemdb-022`(AC-21a-29)
  → `partial`,note「无 CI 执行载体(报告 B-9)」;`TR-diag-018`(AC-8-F5)→ `partial`,note「待 IL2CPP spike(报告 E-4)」。
- `coding-standards.md` CI/CD Rules 已定:`game-ci/unity-test-runner@v4`、「every push to main and every PR」、
  「No merge if tests fail」。但 runner 具体形态(IL2CPP 怎么跑)无权威件。
- 报告 E-4 建议矩阵「Linux-x64-IL2CPP + Mono + Linux-ARM64-IL2CPP + Windows-x64-IL2CPP」——
  本 ADR 经引擎复核后修正(见 §二)。

### Constraints

- 不得破坏 ADR-005 / 006 / 009 / 010 的逐位判据 —— 本 ADR 只加执行载体,不改判据。
- 模拟数学**全在整数定点域**(int64 / Q16.16)—— CI 门的第一守护对象是**整数回绕语义**与舍入模式。
- 表现层 float 隔离在 `IVitalsQuery` 门面之后,`Fix.ToFloat()` 是硬件转换(ADR-005/006)。
- PC / Steam 优先,`technical-preferences.md` Target Platforms = PC;**GitHub Actions 免费额度约束**:
  Linux runner 免费、Windows / macOS runner 计费 —— 常驻矩阵必须零计费。
- **qemu 模拟的 ARM64 IL2CPP 不可信为逐位判据**(引擎复核 F2:qemu-user 下走 softfloat)。
- 后台 / 构建进程不得在矩阵内引入平台相关漂移(SHA256 / FileStream 非漂移源,见 §五)。

### Requirements

- 四条跨平台确定性 TR 获得**可执行、可回归**的 CI 载体。
- 单元级与集成级两层对拍:**字节错位可下钻到具体算子**(双级夹具)。
- 常驻矩阵**零计费**(GitHub Actions 免费 runner),覆盖「运行时(Mono vs IL2CPP)× 架构(x64 vs ARM64)」两维。
- 公式 / 编码变更 invalidate 夹具时,有**可持续刷新路径**,同时防静默漂移(版本化刷新)。
- IL2CPP 编译旗标**逐目标登记**(构建管线行为可审计、可复现)。

## Decision

**裁决:双级黄金夹具(单元级哈希 + 集成级字节)· 三格全免费常驻矩阵 · 版本化刷新协议 ·
IL2CPP 编译旗标逐目标登记。ADR-005/006/009/010 的逐位判据自此获得执行载体。**

### 一、双级黄金夹具(执行载体骨架)

**层级 1 — 单元级黄金哈希(EditMode + player 内,sim 程序集纯逻辑)**:

- 覆盖:`Fix` 四则 / 移位 / `unchecked` 环绕(含**负值右移**与**负值除法向零截断**,报告 E-8)·
  定点 `Exp`(F1 的 `Base`/`Relapse`/`Decay`)· `SplitMix64` · `CDF walk` · 编码器往返逐位还原
  (ADR-006 §五)· `ROUND_HALF_AWAY_FROM_ZERO` 负值实现。
- 输出:每组输入的 raw 校验和(如 SHA256 of 序列化轨迹)写**黄金哈希文件**,逐平台对拍。
- **执行载体修正(F4)**:EditMode 测试在 Editor 进程运行,**恒为 Mono,不经 IL2CPP**。单元级
  哈希要过 IL2CPP,必须把纯逻辑哈希入口改写成**可进 player 的 bootstrap**(静态方法 /
  MonoBehaviour)→ `BuildPipeline.BuildPlayer` 出无头 IL2CPP player → 启动即算哈希写
  `Application.persistentDataPath` → CI 读回对拍。**若某平台单元级对拍未建,该平台集成级
  字节错位将无法下钻定位** —— 双级契约即失效,故单元级 player 对拍是矩阵的**硬件**。

**层级 2 — 集成级黄金字节(PlayMode / player,ADR-010 存档字节流)**:

- 覆盖:完整存档字节流(头部 + 三逻辑流 + 快照 + SHA256),矩阵各格产出**逐位相同**字节流。
- 字节错位时的下钻协议:集成级比对失败 → 用二进制搜索定位首个异字节 → 映射到事件 → 下钻到
  单元级哈希(该算子)→ 定位漂移源。**双级契约的价值就是这条下钻路径**。
- 执行载体:同 F4 —— 由无头 IL2CPP player 产出字节流,CI 读回与 golden 对拍。

### 二、CI 对拍矩阵(三格全免费 + 发版前必跑)

| 格 | runner | 构建 | 运行时 | 架构 | 计费 |
|----|--------|------|--------|------|------|
| **Mono** | Linux-x64(免费)| Editor Mono | Mono(JIT)| x64 | 免费 |
| **IL2CPP-x64** | Linux-x64(免费)| `unity-builder@v4` → Linux player IL2CPP | IL2CPP(C++)| x64 | 免费 |
| **IL2CPP-ARM64** | Linux-x64 交叉构建 → 原生 Linux-ARM64 runner 只跑 player | ARM64 IL2CPP player(交叉)| IL2CPP(C++)| ARM64 | 免费 |
| **Windows-x64** | 发版前必跑(不进常驻)| Windows IL2CPP player | IL2CPP | x64 | 计费,按需 |
| **Apple Silicon** | 发版前必跑(不进常驻)| macOS ARM64 player | IL2CPP | ARM64 | 计费,按需;ADR-005 原文要求,显式记账「延后至发版前验证」 |

**矩阵实现(引擎复核 F1 / F2 修正)**:

- **`game-ci/unity-test-runner@v4` 只在 Editor 进程跑 UTF,恒为 Mono,不产 IL2CPP**(F1)。
  IL2CPP 对拍 = **`unity-builder@v4` 出 player(`scriptingBackend: IL2CPP`)→ 另起 job 跑 player**。
  两个后端 = **两个独立 job/构建**。
- **Linux IL2CPP 前置**:需要 C++ toolchain(clang/LD);GitHub 默认 ubuntu runner **不满足**,
  正解是 `unityci/editor:ubuntu-*-linux-il2cpp-*` **Docker 镜像变体**(toolchain 已烘入)(F1)。
- **ARM64 格 = 交叉构建 + 原生 runner 只跑 player**(F2):Unity 不提供 Linux-ARM64 原生 Editor
  (x86-64 only),ARM64 runner 上跑不起 Editor。路径:x86-64 runner 交叉构建 ARM64 IL2CPP
  player(Linux-ARM64 是受支持目标)→ artifact 传到**原生 ARM64 runner** → **只跑 player**(不需 Editor)。
- **qemu 路径否决**(F2):ARM64 IL2CPP 在 qemu-user 下走 softfloat,**不可信为逐位判据**,
  仅可作冒烟,不作黄金对拍。
- **工具链沿用 `coding-standards.md`**:`game-ci/unity-test-runner@v4`(Mono 格)+
  `unity-builder@v4`(IL2CPP 格);`tests.yml` 工作流文件结构归 `/test-setup` 实现,本 ADR 定判据。

### 三、IL2CPP 编译旗标逐目标登记

- **机制(引擎复核 F3)**:`PlayerSettings.SetAdditionalIl2CppArgs` 是**全局单值,非 per-BuildTarget**。
  逐目标独立登记**不能**靠 PlayerSettings 直存 —— 须 **`IPreprocessBuildWithReport` 回调按
  `BuildTarget` 切换**,或自维护 per-target 登记表 + PR 审查。
- **登记表形式**:每个目标平台一格,显式列 compiler flags / il2cpp args / 来源 PR(报告 E-4 的
  `--compiler-flags=` 语义**属 post-cutoff 须 spike**:建议 `il2cpp` rsp/日志反查 + 反汇编验无 FMA)。
- **`-ffp-contract` 只对 IL2CPP(C++ 后端)生效,Mono 无意义**(F3);x86-64 需 `-mfma/-march`
  才触发 FMA 收缩,ARM64 FMA 是基线 ⇒ 差异主要在 ARM64。
- **纵深防御定位(F3 关键洞见)**:sim 全整数域 ⇒ **FMA 对 sim 零杠杆**;编译旗标登记是
  「防浮点泄漏进模拟域」的纵深防御,不是逐位性的主要保障 —— 主要保障是黄金哈希本身。
- **csc.rsp `-checked+` 纪律(报告 E-10)**:sim 程序集发布路径保持 `unchecked`(IL2CPP 环绕
  跨平台一致);测试程序集用 `csc.rsp -checked+` 把溢出 bug 在 EditMode 抓出 —— 但注意
  **黄金夹具守住的是「回绕后的值逐位一致」,`-checked+` 抓的是「不该回绕却回绕了」**,两条互补。

### 四、版本化刷新协议

- **夹具 manifest 版本**(如 `golden-v1`):黄金文件带 manifest 头(版本号 + 生成器提交 + 生成日期 +
  平台快照)。
- **刷新路径**:公式 / 编码变更 invalidate 夹具 → **新版本目录**(`golden-v2`)+ 变更日志(为何失效、
  旧值新值)→ **全体平台同时重签**(禁止单平台独签)→ 旧版保留作回归对比。
- **防漂移**:CI 对**当前版本**做逐位对拍;旧版本保留 = 漂移有审计轨迹,「谁在什么时候改了什么」
  可追溯。
- **不随意刷新**:任何平台对不上就刷新 = 放弃防漂移,与 `TR-disease-020` 单向性验证精神相悖
  (报告 C-3)。刷新必须走变更日志 + 重签流程。

### 五、漂移面纪律(F5 / F6)

- **SHA256 / FileStream 不作漂移源**(F5):SHA-256 是确定性规格,BCL 托管实现在 IL2CPP 下逐行
  转译,digest 逐位相同;FileStream 字节读写精确。纪律:二进制 IO、显式 UTF-8 **无 BOM**、
  十六进制 ASCII;避免文本换行与路径分隔符差异;哈希只对**同一 byte buffer**。
- **夹具双投递(F6)**:同一 golden 文件需要两份投递机制 —— EditMode:`tests/Fixtures/` 经
  `[CallerFilePath]` 定位(在 player 内失效);Player:`Assets/StreamingAssets/` +
  `Application.streamingAssetsPath`(Linux 桌面可直接 File IO);bin 夹具用 `.bytes`。
- **7a 忘词令符号扫描在本门执行(2026-09-21 回写 · 承 `TR-persist-006` 不一致② · 用户裁定补记载)**:
  `persistence-service.md` AC-7a-09(规则七)判据载体**已于 2026-09-16 用户裁定归本 ADR CI 门**
  (挂三格常驻矩阵同一流水线),但本件全文此前对该义务**零记载**(grep=0 实测)—— 执行体落点缺失。
  现补为硬义务:**存档服务公开接口符号表扫描断言不含 `quicksave` / `quickload` / `save-now-slot`
  类 API**(判据端 grep-IL 守卫脚本,与反射断言互补 —— 前者扫符号、后者扫接口签名);
   qa-lead 只评审判据(承 7a `OQ-7a-2` 已裁口径)。

### Architecture Diagram

```
                    ┌───────────────── 常驻矩阵(GitHub Actions,零计费)─────────────────┐
                    │                                                                 │
  Golden repo ────▶ │  Linux-x64 Mono        Linux-x64 IL2CPP      Linux-ARM64 IL2CPP  │
  tests/Fixtures/   │  (unity-test-runner)   (unity-builder player)(交叉构建→原生ARM64  │
  StreamingAssets/  │  EditMode+PlayMode     → 无头 player)        runner 跑 player)   │
                    │       │                       │                    │            │
                    └───────┼───────────────────────┼────────────────────┼────────────┘
                            ▼                       ▼                    ▼
                    单元级黄金哈希           集成级黄金字节(存档字节流)     SHA256 逐位对拍
                    (Fix/Exp/SplitMix64/     (头部+三流+快照)            │
                     CDF walk/编码器往返)          │                       │
                            │                       └── 字节错位 ──▶ 下钻:首异字节→事件→单元级算子
                            └───────────────────────────────────────────────────────────┘

  发版前必跑(计费,按需):Windows-x64-IL2CPP · Apple Silicon(显式记账)
  IL2CPP 旗标:IPreprocessBuildWithReport 按 BuildTarget 切换 + per-target 登记表(PR 审查)
  刷新协议:golden-vN → 变更日志 → 全体平台重签 → 旧版保留
```

### Key Interfaces

```csharp
// ── 黄金哈希生成器(可进 player 的纯逻辑入口,单元级层级 1)──
// 说明:F4 —— EditMode 恒为 Mono,单元级对拍须经无头 IL2CPP player 执行
public static class GoldenHashBootstrap
{
    public static string ComputeUnitLevelHash(string goldenId, uint version);
    //   返回 hex digest;goldenId 选夹具,version 选 manifest 版本
}

// ── 存档字节流生成器(集成级层级 2,ADR-010 格式)──
public static class GoldenBytesBootstrap
{
    public static byte[] ProduceSaveBytes(string worldSeed, long tick);
    //   产出完整存档字节流(头部+三流+快照+SHA256),矩阵各格逐位相同
}

// ── 黄金夹具读取(双投递:F6)──
public static class GoldenFixtures
{
    public static string ResolveUnitLevelPath(string goldenId);   // tests/Fixtures/(Editor)
    public static string ResolvePlayerFixture(string goldenId);   // StreamingAssets/ .bytes(player)
}

// ── IL2CPP 旗标逐目标登记(F3)──
public interface IPerTargetIl2CppArgs : IPreprocessBuildWithReport
{
    string ArgsFor(BuildTarget target);   // 登记表来源:per-target 表 + PR 审查
}
```

### Implementation Guidelines

1. **表示选择(原 BLOCKING spike F7,2026-09-21 承 RC-4 降级)**:`SplitMix64` 内部状态与
   Q16.16 hi/lo 中间乘积改住 **`ulong`**(必要时 `unchecked`)⇐ C# 与 C++ 两侧对无符号溢出都是
   定义性回绕(mod 2ⁿ)⇒ **IL2CPP 有符号溢出 UB 结构性消除**。**F7 不再卡任何前置** —— 降级为
   一条 EditMode 断言(`SplitMix64` 已知向量对拍,**回绕用例 = 单元级黄金哈希第一条**,`ulong`
   向量照旧入库)。⚠️ 黄金矩阵其余待实测项(Mono vs IL2CPP 逐位一致)**不受本降级影响**,照跑。
2. **再建集成级字节对拍**:复用 ADR-010 存档格式,无头 IL2CPP player 产字节流,CI 读回对拍。
3. **矩阵按 §二 落 `.github/workflows/tests.yml`**(归 `/test-setup` 实现,本 ADR 定判据)。
4. **旗标登记表落地**:per-target 表 + `IPreprocessBuildWithReport` 切换 + PR 审查。
5. **版本化刷新协议落地**:manifest 头 + 变更日志模板 + 重签脚本。
6. **把 TR 状态推进**(见 §Validation Criteria 尾部)。

## Alternatives Considered

### Alternative 1: 仅单元级黄金哈希(不做集成级字节)

- **Pros**:最轻、最快绿;EditMode 纯逻辑,无 player 构建复杂度。
- **Cons**:ADR-010 的黄金字节夹具义务(存档字节流逐位相同)仍无执行载体;字节级漂移
  (如序列化器顺序 / 头部布局)漏网。
- **Rejection Reason**:用户裁定① —— 双级夹具,集成级字节错位可下钻到单元级定位。

### Alternative 2: 四格矩阵(E-4 原样,Windows-x64-IL2CPP 常驻)

- **Pros**:覆盖面最宽,Windows 是 Steam 主平台。
- **Cons**:Windows runner 计费,GitHub Actions 免费额度被吃掉;常驻 = 每次 push 付费。
- **Rejection Reason**:用户裁定② —— Windows-x64-IL2CPP 列**发版前必跑**不进常驻;常驻三格
  全免费覆盖「运行时 × 架构」两维。

### Alternative 3: 已签夹具单向性冻结(永不刷新)

- **Pros**:防漂移最强。
- **Cons**:F1 公式(`Base`/`Relapse`/`Decay`)一改 CI 就永久红;需要另立废弃机制。
- **Rejection Reason**:用户裁定③ —— 版本化刷新:新版本目录 + 变更日志 + 全体平台重签 + 旧版
  保留回归对比;漂移有审计轨迹。

### Alternative 4: qemu 模拟 ARM64 跑对拍

- **Pros**:无需交叉构建链路,单 runner 全平台。
- **Cons**:qemu-user 下 ARM64 IL2CPP 走 softfloat,**不可信为逐位判据**(F2 否决)。
- **Rejection Reason**:黄金对拍的判据价值高于便利;qemu 仅可作冒烟。

## Consequences

### Positive

- **ADR-005/006/009/010 的逐位判据获得执行载体**(纸面 → 可执行)
- **四条 TR 状态推进**:`TR-disease-015/016`、`TR-itemdb-022` → covered;`TR-diag-018` → 矩阵
  提供执行载体(E-4 spike 后定判据)
- **双级下钻**:字节错位可定位到具体算子,排查成本从「全盘」降到「单算子」
- **零计费常驻**:覆盖「Mono vs IL2CPP × x64 vs ARM64」两维,不耗免费额度
- **刷新有审计**:版本化 manifest + 变更日志 + 全体重签,漂移可追溯
- **旗标可审计**:per-target 登记表 + PR 审查,构建行为可复现

### Negative

- **矩阵实现成本**:unity-builder 双 job / Docker 镜像 / 交叉构建链路 / 无头 player 对拍 ——
  比单 runner EditMode 测试重得多
- ~~**F7 悬而未决**:int64 回绕 vs IL2CPP C++ UB 的实测是 BLOCKING spike,结论未出前
  IL2CPP 格的黄金对拍不可信~~ (**2026-09-21 承 RC-4:已由 `ulong` 表示选择结构性消除**;
  黄金矩阵照旧要跑,其余逐位待实测项不受影响)
- **发版前必跑两格计费**(Windows + Apple Silicon)—— 有成本,但只在发版窗口
- **夹具维护成本**:公式变更要走刷新流程,不再是「改完就绿」

### Neutral

- 编译旗标登记是纵深防御(sim 全整数域,FMA 零杠杆),不是逐位性主保障
- `-checked+`(测试程序集)与黄金回绕(发布程序集)互补:前者抓不该回绕的,后者守回绕后的值

## Risks

| Risk | Probability | Impact | Mitigation |
| --- | --- | --- | --- |
| **int64 回绕在 IL2CPP 各 `-O` 级下漂移**(C# 定义性回绕 vs C++ 有符号溢出 UB)| 中 | **高** | **已消除(2026-09-21 承 RC-4)**:内部表示改 `ulong` / `unchecked`,两侧同为定义性回绕 ⇒ UB 结构性不存在;残余风险降为「其余逐位待实测项」,由黄金矩阵照跑覆盖 |
| **ARM64 交叉构建链路不可用**(Unity 无 Linux-ARM64 原生 Editor)| 中 | **高** | x86-64 交叉构建 → 原生 ARM64 runner 只跑 player(F2);qemu 否决,备选 = ARM64 格降级为发版前验证 |
| **game-ci 工具链与 6.3 不匹配**(Docker 镜像 / builder 版本)| 中 | 中 | `unityci/editor:ubuntu-*-linux-il2cpp-*` + spike 锁定镜像 tag;CI 配置早建早验证(F1) |
| **`SetAdditionalIl2CppArgs --compiler-flags=` 语义不符预期** | 中 | 中 | `il2cpp` rsp/日志反查 + 反汇编验无 FMA(F3);语义实测后锁进登记表 |
| **夹具静默漂移**(刷新流于形式)| 低 | **高** | 版本化 manifest + 变更日志 + 全体平台重签 + 旧版保留;任何平台独签被 CI 拒绝 |
| **单元级对拍没建 → 双级契约失效**(字节错位无法下钻)| 中 | 中 | 单元级 player 对拍 = 矩阵硬件(§一);实现顺序先单元后集成 |
| **SHA256 / FileStream 引入平台漂移**(文本换行 / BOM / 路径分隔符)| 低 | 中 | 二进制 IO + UTF-8 无 BOM + 十六进制 ASCII;哈希只对同一 byte buffer(F5) |
| **Apple Silicon 缺口**(ADR-005 原文要求但常驻无此格)| 中 | 低 | 显式记账:发版前必跑验证(§二 表);Steam Deck 侧由 ARM64 格覆盖 |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CI 单次 push 时长 | 无 CI | Mono 格 + 2× IL2CPP 构建/对拍 job(并行) | 目标 < 20 min(矩阵并行) |
| CI 常驻计费 | 无 CI | **$0**(Linux runner 全免费) | 免费额度内 |
| CI 发版窗口计费 | — | Windows + Apple Silicon 两格按需 | 发版预算 |
| 黄金夹具仓库体积 | — | 双级夹具 × 各平台哈希(文本) | 低(KB 级);字节夹具用 `.bytes` |
| 运行时开销 | — | 零(黄金对拍仅 CI 期) | 无运行时成本 |

## Migration Plan

**本项目尚无 tests/ 与 CI —— 无迁移,本 ADR 是「第一次就做对」。(同 ADR-005/010/011)**

1. **表示选择(原 BLOCKING spike F7,2026-09-21 承 RC-4 降级)**:`SplitMix64` / Q16.16 中间乘改住
   `ulong`(必要时 `unchecked`),单条 EditMode 断言(已知向量对拍)替代 IL2CPP 溢出实测 —— 结论
   = 不依赖编译器善意的结构解,与 ADR-005/006 取向一致。
2. **建 tests/ 骨架 + 单元级黄金哈希**(EditMode 先绿,Mono 格)。
3. **建集成级字节对拍**(ADR-010 存档格式,无头 player 产字节流)。
4. **矩阵落 `.github/workflows/tests.yml`**(unity-test-runner + unity-builder 双 job + 交叉构建)。
5. **旗标登记表 + 刷新协议落地**。
6. **TR 状态推进**(§Validation Criteria 尾部)。

**Rollback plan**:若 ARM64 交叉构建链路不可行(F2 spike FAIL),ARM64 格降级为**发版前验证**
(与 Windows / Apple Silicon 同类),常驻矩阵退回两格(Linux-x64-Mono + IL2CPP)—— 判据本身
(双级黄金哈希)不变,只减覆盖面,不改变裁决。

## Validation Criteria

- [x] **F7 表示选择落地(2026-09-21 承 RC-4 降级)**:`SplitMix64` / Q16.16 内部表示 = `ulong`
      (必要时 `unchecked`);回绕用例(已知向量)= 单元级黄金哈希第一条,EditMode 对拍通过
      —— **✅ EditMode 对拍通过 2026-09-23**(Unity 6000.3.24f1):`GoldenHashV1Test` 43 条新增 +
      既有 16 条,合计 **59 绿**全过;含 Avalanche 流 / 回绕 2 / fold 2+3+4 / HashTagged /
      FixMul 25+溢出 3 / S7 哈希半边。黄金期望值由独立 Python 参考实现算出,与 C# 实现零共享代码。
      ⚠️ **只勾 EditMode 这条**:「三格全绿」项(下一行)仍需 CI 矩阵 Mono / IL2CPP-x64 / IL2CPP-ARM64
      —— 未跑,不借绿。
- [ ] **单元级黄金哈希三格全绿**:Fix 四则 / 负值右移 / 负值除法向零截断 / 定点 Exp / SplitMix64 /
      CDF walk / 编码器往返 / `ROUND_HALF_AWAY_FROM_ZERO` 负值 —— Mono / IL2CPP-x64 /
      IL2CPP-ARM64 逐位相同
- [ ] **集成级黄金字节三格全绿**:同一存档(ADR-010 格式)三格产出逐位相同字节流
- [ ] **下钻路径可用**:注入一个人为字节错位,能从集成级首异字节下钻到单元级具体算子
- [ ] **刷新协议演示**:公式变更 → golden-v2 新目录 + 变更日志 + 三格重签;单平台独签被 CI 拒绝
- [ ] **旗标登记表落地**:per-target 表 + `IPreprocessBuildWithReport` 切换 + PR 审查
- [ ] **csc.rsp 纪律**:测试程序集 `-checked+`,sim 发布路径 `unchecked`(报告 E-10)
- [ ] **Windows-x64 / Apple Silicon 发版前验证流程建立**(不进常驻,按需触发)
- [ ] **TR 推进**:`TR-disease-015`/`016`、`TR-itemdb-022` → covered;`TR-diag-018` → 矩阵提供
      执行载体(E-4 spike 后定判据)—— registry + traceability-index 同步
- [ ] `tests/`、`tests/unit/`、`tests/integration/`、`.github/workflows/tests.yml` 存在(预门控三项)

## 挂账注(2026-09-25 · story-001 player 红点批)

- **LegacyInputAnalyzer.dll 卷入 player 构建 —— 已结案**:分析器 DLL 原 `platformData: Any: enabled`
  ⇒ 进 player 根程序集(`--include-unity-root-assembly`),其 net6.0 产物直引
  `System.Private.CoreLib 6.0.0.0` ⇒ UnityLinker `AssemblyResolutionException` ⇒ player 构建
  exit 3(实证 `unity/Logs/build-story001-player.log`)。**W2 平台收敛 Editor-only** 已由
  `RoslynAnalyzerLabel.ConfigurePluginImporter` 幂等落地 + 复查门(`editorOnlyOk`);超算复验
  linker 参数 **0× LegacyInputAnalyzer**、构建过原炸点(`unity/Logs/build-story001-w2-player.log`)。
  RoslynAnalyzer label 只在编辑器编译期被消费 ⇒ 门(DY0001)不受影响(宿主装载探针实证)。
- **编辑器域重载噪声**:残留 1 行 `Unloading broken assembly ... LegacyInputAnalyzer.dll` =
  常态噪声,无动作(W1 后 error 级「will not be loaded due to errors」已消失)。
- **分析器进 CI 常驻矩阵的 job 仍挂账** —— 归 F1 / `/test-setup` 轮(与既挂的矩阵建设同批)。
- **超算无头无 X ⇒ UTF `-testPlatform` player 套件不可在超算执行**:PlayerWithTests 无视频设备
  (`Error creating MainPlayerWindow`)→ `Creating GLContext @level -1` 无限循环(实测 ~190 MB/s
  刷 40 GB Player.log,证据摘录 `unity/Logs/build-story001-w2-player-stuck-evidence.log`)→ 编辑器侧
  `RemoteTestRunController:TimeoutCallback` → 必 RunError。**AC-29 先例同向**:桌面腿 3/3 过,
  超算腿 exit 3 / 编辑器 double-fault(`unity/Logs/cluster-ac29-*.log`)。⇒ **player 级测试执行
  归桌面轮(2026-09-25 用户裁定取 B:桌面复跑;xvfb 方案不采纳)** —— 已下载未用的前缀留置
  `/XYFS01/sysu_tyu2_2/xvfb-prefix`,日后若重开须用户重新点名授权。
- **观察项**:`unity/Assets/AddressableAssetsData/link.xml`(机器生成,永不提交)本次跑中缺失 +
  `Build asset version error` Import Code 4;Unity 按需再生成,归下次构建观察,不进本 ADR 判据。

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Addresses It |
|--------------|--------|-------------|--------------------------|
| `design/gdd/disease-simulation.md` | 9 疾病与伤情 | AC-1 跨平台位完全相同 | 双级黄金哈希矩阵 = 执行载体(TR-disease-015) |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情 | AC-5b 定点 Exp 位确定性 | 单元级黄金哈希含定点 Exp 夹具(TR-disease-016) |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情 | 手写定点 Exp 需黄金文件对拍 | 单元级夹具兑现(TR-disease-003) |
| `design/gdd/item-database.md` | 21a 物品与配方 | AC-21a-29 跨平台确定性(UNVERIFIED) | 矩阵 + 编码器往返夹具(TR-itemdb-022) |
| `design/gdd/diagnosis-system.md` | 8 诊断与体征 | AC-8-F5 表现层 float 跨平台一致 | 矩阵提供执行载体;判据待 E-4 spike(TR-diag-018) |
| `design/gdd/random-events.md` | 52 随机事件导演 | AC-52-06 逐位相同 · AC-52-39 IL2CPP Release | 矩阵(IL2CPP 格)即载体 |
| `design/gdd/case-system.md` | 37 病例系统 | AC-37-06 迁移重放逐位一致 | 集成级字节对拍(存档含病例流) |

## Related

- **ADR-005 确定性模拟与状态同步模型**(Accepted)—— 逐位一致判据的来源,本 ADR 是其执行载体
- **ADR-006 定点域边界数据契约**(Accepted)—— 编码器往返逐位还原 / 舍入模式,进单元级夹具
- **ADR-009 世界状态的事件化边界**(Accepted)—— ~~地形逐位重建对拍~~ → **2026-09-15 ADR-015 修订**:
  地形不再经运行期生成,「地形逐位重建」判据**作废**;世界流进流字节的逐位性仍由本 ADR 夹具承载
- **ADR-010 持久化与存档格式**(Accepted)—— 黄金字节夹具 = 存档字节流的逐位性可测面;本 ADR 是其 Enables 的实现
- **R-5 跨平台确定性 CI 门**(architecture-review 2026-09-15)—— 本 ADR 是其落点;报告 B-9 / E-4 / E-8 / E-10 全部并入
- **`/test-setup`**(预门控)—— `.github/workflows/tests.yml` 结构实现,判据以本 ADR 为准
- `docs/registry/architecture.yaml` —— 本 ADR 新增 golden_fixture 接口契约 + CI 矩阵 / IL2CPP 旗标 API 裁决
