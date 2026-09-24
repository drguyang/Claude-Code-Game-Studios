# AC-21a-29 跨平台确定性对拍 —— Mono ↔ IL2CPP 逐位证据

> **日期**: 2026-09-24(桌面 Linux-x64)
> **判据出处**: `design/gdd/item-database.md` AC-21a-29 · `production/epics/item-database/story-011-determinism-golden-fixtures.md`
> **执行载体**: ADR-012 §一 层级 1 / F4(纯逻辑哈希入口进 player → 无头 player 算哈希 → 外部对拍)
> **后端切换**: `Ac29Il2CppSwitch`(Editor.Tools.Spike;`Standalone: Mono2x → IL2CPP → 归位 Mono2x`,
>   归位后 `git restore unity/ProjectSettings/ProjectSettings.asset` 清痕)

## 执行面

| 腿 | 载体 | 命令要点 | 结果 |
|---|---|---|---|
| **Mono** | 编辑器 PlayMode(Editor 进程恒 Mono,F4) | `unity test --mode PlayMode --filter DeterminismGoldenCrossplatform` | **3/3 Passed**(`unity/Logs/ac29-mono-playmode.xml`) |
| **IL2CPP** | StandaloneLinux64 player(UTF `-testPlatform`,独立 player 构建) | `Unity -batchmode -runTests -testPlatform StandaloneLinux64 -testFilter DeterminismGoldenCrossplatform` | **3/3 Passed**(`unity/Logs/ac29-il2cpp-results.xml`,退出码 0) |

两腿同一测试:`unity/Assets/Tests/PlayMode/determinism_golden_crossplatform_test.cs`(3 测)
① 19 条金标准全量计算 → 落盘 + 金标准可达时就地逐位断言;② F7 SplitMix64 回绕已知值;
③ F7 Fix.MulRaw 中间乘峰值参数已知值(2^32×2^32 / int.Max² / long.MinValue 量积 / half-away ties / 域外抛)。

## 证据文件(三方逐位 diff)

| 文件 | 头标 | 说明 |
|---|---|---|
| `tests/unit/item_database/golden/golden-v1.txt` | 独立 Python 参考实现产出 | 金标准(AC-28,19 条) |
| `production/qa/evidence/ac29-hashes-mono.txt` | `# ac29 backend=Mono \| Mono 6.13.0 (explicit/…)` | Mono 腿输出(编辑器 PlayMode,`platform=LinuxEditor`) |
| `production/qa/evidence/ac29-hashes-il2cpp.txt` | `# ac29 backend=IL2CPP \| Mono Unity IL2CPP (…)` | IL2CPP 腿输出(player,`platform=LinuxPlayer`,XML `_APPDOMAIN=IL2CPP Root Domain`) |

**外部 diff 判决(AC-29 口径:测试绿且外部 diff 逐位相同才 VERIFIED)**:

```
entries = 19
IL2CPP_vs_GOLDEN: ALL19_BITWISE_IDENTICAL
IL2CPP_vs_MONO:   ALL19_BITWISE_IDENTICAL
```

⇒ **AC-21a-29 VERIFIED(Mono ↔ IL2CPP Linux-x64 逐位相同,19/19,含 F7 峰值向量 player 侧已知值断言)。**

## 过程订正(诚实记账)

- **后端标记首版为假**:初版以 `Mono.Runtime` 类型存在性判后端,IL2CPP 同样生成该类型 ⇒
  IL2CPP 腿首跑头标误写 `backend=Mono`。同日改为 `FrameworkDescription` 含 `IL2CPP` 判据,
  两腿全部重跑重取证(本文件所列即修正后结果);测试源已留订正注释。
- **首跑经 `unity run` 触发被 `-quit` 吞掉**(`unity run` 保留旗标与 `-runTests` 冲突,静默退出未跑测)⇒
  改直调 Unity 二进制 `-runTests` 路径。

## 残余(不在本证据内,不冒充已跑)

| 项 | 状态 | 归属 |
|---|---|---|
| IL2CPP-ARM64 交叉构建 + 原生 ARM64 runner 跑 player(ADR-012 F2) | **未跑** | CI 矩阵轮(`unity-builder@v4` + ARM64 runner;qemu 否决) |
| Windows-x64-IL2CPP / Apple Silicon(发版前必跑两格) | **未跑** | 发版前 |
| CI 三格常驻矩阵 job(`tests.yml`)| **未建** | /test-setup · CI 故事 |
| F7 反汇编验无 FMA(`--compiler-flags=` 语义,ADR-012 §三) | **未跑** | 矩阵轮(纵深防御项;sim 全整数域零杠杆) |

> 本证据覆盖 AC-29 的**判据本体**(同组参数 Mono vs IL2CPP 哈希逐位相同)在 Linux-x64 两后端上的实测;
> 上表四项为 ADR-012 矩阵**建设面**,持续挂账。
