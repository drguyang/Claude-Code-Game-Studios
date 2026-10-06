# 评审报告原件 — prescription-medication story-003(F-11.2 半衰期)

**日期**: 2026-10-06
**评审对象**: `unity/Assets/Sim/Prescription/HalfLifeCalculator.cs` + `unity/Assets/Tests/EditMode/PrescriptionMedication/half_life_test.cs`
**评审轮次**: 单轮(承「评审只做一轮」协议)

## 交付物

### 生产代码
- `unity/Assets/Sim/Prescription/HalfLifeCalculator.cs` — F-11.2 半衰期计算器
- `unity/Assets/Sim/Prescription/HalfLifeCalculator.cs.meta`

### 测试代码
- `unity/Assets/Tests/EditMode/PrescriptionMedication/half_life_test.cs` — 24 条测试
- `unity/Assets/Tests/EditMode/PrescriptionMedication/half_life_test.cs.meta`
- `unity/Assets/Tests/EditMode/PrescriptionMedication/PrescriptionFloatScan.cs` — 零浮点扫描共享实现(修复轮新增)

## 测试结果

- **filter(评审前)**: `unity/Logs/half_life.xml` = **17 / 17 passed / 0 failed**
- **全量(评审前)**: `unity/Logs/half_life_full.xml` = **2766 passed / 0 failed / 46 skipped / 1 inconclusive**
- **filter(修复后)**: `unity/Logs/half_life_fix4.xml` = **59 / 59 passed / 0 failed**(PrescriptionMedication 全目录)
- **全量(修复后)**: `unity/Logs/half_life_full3.xml` = **2773 passed / 0 failed**(exit code 2 源自既有 Inconclusive,非失败)

## 评审结果

### 结构侧评审(unity-specialist)

**判定**: **CHANGES REQUIRED**(1 BLOCKING + 2 MAJOR + 3 MINOR)

### QA 侧评审(qa-tester)

**判定**: **APPROVED**(2 MAJOR + 8 MINOR · **0 BLOCKING**)

## 修复记录(结构侧)

| # | 问题 | 落点 |
|---|------|------|
| **B-1** | raw `long` 加法绕过 `Fix.operator+` 的 `checked` 溢出保护(静默回绕;IL2CPP 下为 UB) | 改走 `Fix effective = axisBase + offset;` —— `Fix.cs:124-127` 的 `operator+` 用 `checked` 抛 `OverflowException` |
| **M-1** | `CalculateForDrug` 是纯透传,无抽象价值 | 改为读 `DrugProfile` 的真实组合入口(取 `HalfLife` / `AxisOffsetByQuality` 两字段 + 可空校验,与 `DoseCalculator.CalculateForDrug` 处理 `DoseRange?` 同构) |
| **M-2** | 缺溢出行为测试 | 已补 3 条(见 QA 侧 M1);B-1 修复后它们走 `Fix.operator+` 的 `checked` 路径 |
| **m-1** | 误导性注释(称「long 加法回绕定义良好,无需检查」) | 随 B-1 修复一并删除,替换为「走 `Fix.operator+`,溢出 = bug ⇒ 硬失败」 |
| **m-2** | 静态扫描范围过宽(扫全目录而非单文件) | **保留全目录扫描** + 加注说明:AC-11-11① 是**系统级**约束,同目录 `DoseCalculator` 亦在约束内;某文件引入浮点 ⇒ 本测**应当**失败(特性非缺陷) |
| **m-3** | `EffectiveQuality` 无变换 | 加注:当前为**透传**(11 侧不重复 clamp,越界由 `Calculate` 抛异常拦截);预留扩展位 |

## 修复记录(QA 侧)

| # | 问题 | 落点 |
|---|------|------|
| **M1** | 缺 int64 溢出行为测试;代码注释称「回绕定义良好」与 ADR-012 F7(IL2CPP 有符号溢出 UB)相抵 | `HalfLifeCalculator` 补**显式溢出检测**(正向 / 负向两路,抛 `OverflowException`,与 `DoseCalculator` 同纪律);测试补 3 条(`positiveOverflow` / `negativeOverflow` / `noOverflowAtBoundary`) |
| **M2** | 静态扫描目录缺失时 `Assert.Ignore` ⇒ 静默跳过(借绿) | 改 `Assert.IsTrue(Directory.Exists(...))` 硬失败 |
| **m1** | 未扫描 `ToFloat()` 调用 | 正则增 `\bToFloat\s*\(` |
| **m2** | 未扫描 `Math.Sqrt` / `Math.Pow` 等浮点函数 | 正则增 `\bMath\.(Sqrt\|Pow\|Exp\|Log\|Log10\|Sin\|Cos\|Tan\|Atan\|Atan2\|Abs\|Floor\|Ceiling\|Round)\s*\(` |
| **m3** | 未覆盖 `decimal` | 正则改 `\b(float\|double\|decimal)\b` |
| **m4** | 正则大小写敏感 | 加 `RegexOptions.IgnoreCase` |
| **m7** | 缺 quality = 上界合法值显式测试 | 补 `test_halflife_qualityAtUpperBound_valid` |
| **m8** | 缺单元素偏移表测试 | 补 `test_halflife_singleElementOffsets_valid` |

> **m5 / m6 接受不修**(QA 评审自陈「可接受」):字符串字面量误报 = false positive 比 false negative 安全;注释跳过逻辑保守策略。

> ⚠️ **M1 的实现选择说明**:溢出时**抛异常而非回绕**,与「11 侧不重复 clamp」**不冲突** ——
> clamp 是静默改值(掩盖 21a 断言失败),throw 是硬失败(暴露失败)。GDD 禁的是前者。

### 修复轮连带发现(非评审提出,修复中实测暴露)

> **两测试共享扫描面的重复实现漂移**:`DoseCalculatorTest`(story-002)与 `HalfLifeCalculatorTest`(story-003)
> **各自**实现了一份 `test_*_noFloat_staticScan`,扫描**同一** `Sim/Prescription/` 目录。
> 修复轮给 story-003 侧加「字符串剥离」后,story-002 侧**未同步** ⇒ 全量跑时 story-002 的扫描
> 把 `HalfLifeCalculator.cs` 异常消息里的「F-11.2」误判为浮点字面量 ⇒ **1 红**。
> **根因** = 同一判据两份实现(与 11 侧反复警惕的「零第二实现」同型)。
> **修法**:抽出共享 `PrescriptionFloatScan.Scan(simDir)`(本目录唯一扫描实现),两处测试均调用它。
> **注**:此发现**不在**任何评审报告内,是修复轮实测暴露 —— 显式登记,不并入评审判定。

## NOT-RUN 登记(禁借绿)

- **AC-11-08 ②**: 21a 构建期断言 `Axis_base + min(offset) ≥ MIN_USABLE_HALF_LIFE` 不存在(BL-1)
- **AC-11-15**: 三格矩阵(ADR-012 未实跑)
- **TR-prescription-008**: 21a 半边

## 验证命令

```bash
# filter 测试
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.PrescriptionMedication.HalfLifeCalculatorTest" --output unity/Logs/half_life.xml

# 全量测试
unity test unity --mode EditMode --output unity/Logs/half_life_full.xml
```
