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
| ~~F7 反汇编验无 FMA(`--compiler-flags=` 语义,ADR-012 §三)~~ | **✅ 2026-09-25 跑毕(见下节)** | —— |

> 本证据覆盖 AC-29 的**判据本体**(同组参数 Mono vs IL2CPP 哈希逐位相同)在 Linux-x64 两后端上的实测;
> 上表残余项为 ADR-012 矩阵**建设面**,持续挂账(F7 行已于 2026-09-25 勾销)。

## F7 反汇编验无 FMA(✅ 2026-09-25 跑毕)

**对象**:本证据 AC-29 构建产出的 `unity/Library/Bee/artifacts/LinuxPlayerBuildProgram/il2cppOutput/build/GameAssembly.so`
(553 MB,2026-09-25 05:42 链接,`Link_Linux_x64_Clang`)。

**方法**:`nm` 定位 IL2CPP 符号 → `objdump -d` 按函数边界取指令流 → 按 mnemonic 正则扫
`v?fmadd / v?fmsub / v?fnmadd / v?fnmsub / vmadd`(FMA 家族)与 `xmm/ymm`、`cvt*`、
`v?(add|sub|mul|div)(ss|sd|ps|pd)`(FP/SIMD)。

| 函数 | 符号 | 地址 | 指令数 | FMA | FP/SIMD | 判决 |
|---|---|---|---|---|---|---|
| `Fix.MulRaw`(Q16.16 中间乘) | `Fix_MulRaw_mF27897…` | `0x3df3730–0x3df3850` | 73 | **0** | **0** | **CLEAN** |
| `SplitMix64.Avalanche`(z*=常量) | `SplitMix64_Avalanche_m45D582…` | `0x3df4cf0–0x3df4d30` | 15 | **0** | **0** | **CLEAN** |

**指令面旁证**(读反汇编本体):
- `MulRaw` = 纯整数:`imul`×4(32 位四路拆分 hi/lo 手工 128 位乘,承 ADR-005 Amendment G)+
  `add/adc`(进位链)+ `shld $0x30`(48 位移)+ `test $0x8000`(舍入位)+ `OverflowException` 分支 ——
  与 C# 源结构逐段对应;**零浮点寄存器参与**。
- `Avalanche` = 纯整数:`shr/xor/imul` + 两个金标准常量 `0xbf58476d1ce4e5b9`(Mul1)与
  `0x94d049bb133111eb`(Mul2)字面出现在指令流中 —— **与 Python 参考实现同一组常量,肉眼可核**。
- 工具链:构建日志 `C_Linux_x64_Clang` + `Link_Linux_x64_Clang`(Linux-x64 = Clang);
  `SetAdditionalIl2CppArgs` / `--compiler-flags` / `-ffp-contract` 全部 **0 命中**
  ⇒ 本次跑用默认工具链旗标,**未设任何自定义 il2cpp args**。
  ⚠️ 逐目标旗标**登记表**(ADR-012 §三:per-target compiler flags + 来源 PR)仍属 CI 矩阵轮
  —— 本跑证明的是「产物反汇编无 FMA」,不是「旗标登记表已建立」。
- 定性承 ADR-012 §三纵深防御口径:sim 全整数域 ⇒ FMA 本无整数形态可言;本节的可执行价值 =
  **实证这些热路径没有浮点泄漏进 IL2CPP 产物**(FP/SIMD = 0),而非数学上消除 FMA。
