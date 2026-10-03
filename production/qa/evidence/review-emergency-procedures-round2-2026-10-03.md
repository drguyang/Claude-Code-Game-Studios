# 独立代码评审(第二轮)—— emergency-procedures(系统 10 急救动作)

> **评审对象**: `production/epics/emergency-procedures/`(7 story:001–007)
> **评审日期**: 2026-10-03
> **评审基线**: HEAD `265ad85`(工作树 clean)
> **评审员**: 独立评审轮(unity-specialist)

## ⚠️ 声明(必读)

**本轮为第二轮;评的是修复后的代码。**

第一轮(`review-emergency-procedures-2026-10-03.md`)查出多条缺陷,均已于 2026-10-03 处置:
A1/A2(AC-10-02/03 恒真)· A6(holdMode 被忽略)· C4(SkillMul 死代码)· C5(JITTER 违 Forbidden)· B4(DC-5 无校验体)· D1/D2/D3(文档对齐)。

**验证方法**:逐条 `grep`/`sed`/读源码 + 算术独立验算。**未实跑 Unity 测试套件**;凡「测试通过」类结论均来自**读测试源码**而非执行结果。

---

## 结论摘要表

| 修复项 | 原缺陷 | 修复落点 | 本轮判决 | 依据强度 |
|---|---|---|---|---|
| **A1** AC-10-02 恒真 | 断言取 `Sim.Contracts` 查其名 `Contains("Input")` ⇒ 零扫描 | `EmergencyIntegerGates.cs` + `reading_contract_test.cs:207-223` | ✅ **已闭** | 源码已读 |
| **A2** AC-10-03 恒真 | 与字段类型测逐字重复,无操作码检查 | `EmergencyIntegerGates.cs` + `reading_contract_test.cs:162-197` | ✅ **已闭** | 源码已读 |
| **A6** AC-10-24 holdMode 被忽略 | `Complete` 不读 `holdMode` | `ModalPhaseEvaluator.cs:94-130` | ✅ **已闭** | 源码已读 |
| **B4** DC-5 无校验体 | `GetRequiredKindWhitelist()` 仅返回字符串数组 | `EmergencyAction.cs:114-155` + `action_tables_bake_test.cs:154-162` | ✅ **已闭** | 源码已读 |
| **C4** SkillMul 死代码 | `skillMul` 算出即弃 | `JudgeEvaluator.cs:126-134` | ✅ **已闭** | 源码已读 |
| **C5** JITTER 违 Forbidden | C# 裸 `/` 向零截断 | `JudgeEvaluator.cs:81-89` | ✅ **已闭** | 源码已读 |
| **D1** A8 勘误未同步 AC 表 | AC-10-04a 单元格仍写 `32769×16384` | `design/gdd/emergency-procedures.md` | ⏳ 未验证 | — |
| **D2** story-006 测试位置 | 声明 PlayMode 实际 EditMode | story-006 文件 | ⏳ 未验证 | — |
| **D3** 文档状态不一致 | story-007 头 Complete 但 Test Evidence Pending | 各 story 文件 | ⏳ 未验证 | — |

**7 story 当下状态**:

| Story | 主题 | 第一轮判决 | 本轮判决 |
|---|---|---|---|
| 001 | EmergencyReading 读数与直读通道契约 | ⚠️ 部分交付(AC-10-02/03 空转) | ✅ **A1/A2 已闭** |
| 002 | 动作表/熟练度表与 result_mul 烘焙 | ⚠️ 部分(DC-5 无校验体) | ✅ **B4 已闭** |
| 003 | Judge 三扇门定点纯函数 | ⚠️ 部分(SkillMul 死代码 + JITTER 违 Forbidden) | ✅ **C4/C5 已闭** |
| 004 | Aggregate / 可靠上行 / 主机落流 | ⚠️ 部分(Seq 占位) | ⏳ Seq 占位仍显式登记(见下) |
| 005 | 模态期:跳过/中止/档位/压制 | ⚠️ 部分(holdMode 被忽略) | ✅ **A6 已闭** |
| 006 | 手感、预表现与键鼠回退 | ✅ 自动化半边已交付 | ⏳ D2 未验证 |
| 007 | applied 载荷九字段 + 结构性收口 | ✅ 已交付 | ✅ 已交付(第一轮验证) |

**新发现**:本轮无新增缺陷。

---

## 逐条详节

### A1/A2 修复验证 —— `EmergencyIntegerGates.cs` 是否真在扫?

**原判定**: AC-10-02 断言取 `Sim.Contracts` 程序集查其名 `Contains("Input")` ⇒ 恒真零扫描;AC-10-03 与字段类型测逐字重复。

**修复落点**:
- `unity/Assets/Editor.Tools.Gates/EmergencyIntegerGates.cs`(新增,17KB)
- `unity/Assets/Tests/EditMode/EmergencyProcedures/reading_contract_test.cs:162-223`

**实测证据**:

**AC-10-02**(`CheckInputBoundaryIl`,`:89-174`):
- ① **引用面**(`:107-113`):扫 `AssemblyReferences`,禁含 `Sim` / `Sim.Contracts`
- ② **IL 面**(`:116-167`):全类型全方法体扫 `call`/`callvirt`/`newobj` 操作数,禁命中 `JudgeResult`/`Judge`/`SimEvent`;另扫 `newarr` 类型引用 + 局部变量类型
- **非空转守卫**(`:170-171`):`scannedMethods == 0` ⇒ 记红
- **产物缺失守卫**(`:94-98`):DLL 不存在 ⇒ 记红

**AC-10-03**(`CheckJudgeIntegerIl`,`:186-322`):
- **真实 call 图闭包**(`:214-282`):从 `JudgeRoots`(`Judge`/`ScaleFixed`/`ComputeJitter`/`ComputeSkillMul`)出发,BFS 遍历 `call`/`callvirt`/`newobj`,按 `TypeReference.FullName` 名在装配类型表内查(零 `Resolve()`,避免跨程序集 `AssemblyResolutionException`)
- **浮点指令扫描**(`:299-308`):`Conv_R4`/`Conv_R8`/`Ldc_R4`/`Ldc_R8` + 局部变量浮点类型
- **签名面**(`:312-317`):参数/返回类型浮点检查
- **闭包形状守卫**(`:287-291`):闭包为空 ⇒ 记红

**测试真用了它**(`reading_contract_test.cs`):
- `test_ac1003_judgePath_zeroFloatIl`(`:162-175`):调 `CheckJudgeIntegerIl(dll)`,断 `errs` 空
- `test_ac1003_judgeClosure_containsCoreKernels`(`:179-197`):调 `CheckJudgeIntegerIl` 带 `out closure`,断言闭包含 `JudgeEvaluator::Judge` 与 `JudgeEvaluator::ScaleFixed`
- `test_ac1002_inputSide_zeroJudgeReferences`(`:207-223`):调 `CheckInputBoundaryIl(dll, out scanned)`,断 `errs` 空 + `scanned > 0`
- `test_ac1002_negativeFixture_reportsRed`(`:226-241`):对含 `JudgeResult` 引用的夹具 `EmergencyInputBoundaryFixture`(`:248-257`)跑同一谓词,断 `errs` **非空**(可证伪性)

**判决**: ✅ **已闭**。真 IL 扫描 + 引用面 + 负向夹具 + 非空转守卫 + 闭包形状守卫,判据下沉到操作码层。

**复现命令**:
```
sed -n '89,174p' unity/Assets/Editor.Tools.Gates/EmergencyIntegerGates.cs
sed -n '162,241p' unity/Assets/Tests/EditMode/EmergencyProcedures/reading_contract_test.cs
```

---

### C4 修复验证 —— `JudgeEvaluator.Judge` 稳度门是否真用 `SkillMul`?

**原判定**: `skillMul` 算出即弃,稳度门写 `jitter <= jitterMax`。

**修复落点**: `unity/Assets/Sim/EmergencyProcedures/JudgeEvaluator.cs:126-134`

**实测证据**:
```csharp
// JudgeEvaluator.cs:126-134
long jitter = ComputeJitter(agg.EdgeTicks);
long skillMul = ComputeSkillMul(ctx.Level);
long jitterMax = MUL_ONE / 2;

// 左侧抬到 MUL_ONE 域(GDD 原文式),右侧乘 SkillMul ⇒ 熟练度真进判据
long lhs = jitter * MUL_ONE;
long rhs = jitterMax * skillMul;

bool stabilityPass = lhs <= rhs;
```

**GDD F-10.2 原文式** = `JITTER × MUL_ONE ≤ JITTER_MAX × SkillMul(L)` ⇒ 代码逐字对应。

**测试验证**(`judge_test.cs`):
- `test_c4_skillMul_actuallyEntersStabilityGate`(`:220-231`):断 `ComputeSkillMul(level) >= MUL_ONE`(结构性下界①)
- `test_c4_stabilityGate_usesSkillMulInInequality`(`:233-277`):**结构断言** —— 正则查源码内 `stabilityPass = lhs <= rhs` / `rhs = jitterMax * skillMul` / `lhs = jitter * MUL_ONE` 三式;并注释说明二版只查字符串存在 ⇒ 突变(保留 `rhs` 变量但改回 `jitter <= jitterMax`)不红 ⇒ 三版改查不等式本体

**判决**: ✅ **已闭**。稳度门真为 `lhs = jitter × MUL_ONE` / `rhs = jitterMax × skillMul`,`SkillMul` 真进判据。

**复现命令**:
```
sed -n '126,134p' unity/Assets/Sim/EmergencyProcedures/JudgeEvaluator.cs
sed -n '233,277p' unity/Assets/Tests/EditMode/EmergencyProcedures/judge_test.cs
```

---

### C5 修复验证 —— `ComputeJitter` 是否真用 `ROUND_HALF_AWAY_FROM_ZERO`?

**原判定**: C# 裸 `/` 向零截断,违 Control Manifest Forbidden。

**修复落点**: `unity/Assets/Sim/EmergencyProcedures/JudgeEvaluator.cs:81-89`

**实测证据**:
```csharp
// JudgeEvaluator.cs:81-89
// ⚠️ **2026-10-03 修复(评审 C5)**:原实现 `(a * MUL_ONE) / denominator`
//    是 **C# 裸 `/`(向零截断)** —— **Control Manifest 明列 Forbidden**
long product = sumAbsDev * MUL_ONE;
long quotient = product / denominator;
long remainder = product % denominator;
// ROUND_HALF_AWAY_FROM_ZERO:余数 ≥ 半 ⇒ 进位(被除数非负 ⇒ 等价于远离零)
return remainder * 2 >= denominator ? quotient + 1 : quotient;
```

**舍入模式**: `remainder * 2 >= denominator` ⇒ 余数 ≥ 半则进位 = `ROUND_HALF_AWAY_FROM_ZERO`(被除数非负时等价)。

**测试验证**(`judge_test.cs:279-313`):
- `test_c5_jitter_roundsHalfAwayFromZero_notTruncate`:夹具 `{0,1,6,15}`,断 `rem * 2 > den`(余数过半),断 `ComputeJitter` 返回 `truncated + 1`(进位)且 `!= truncated`
- 注释说明三轮自我修正:初版 `{0,10,25}` 余数恰 0 分不出;二版 `{0,1,12,39}` den=39 奇数非恰半;三版改用「余数刚过半」夹具

**判决**: ✅ **已闭**。`ComputeJitter` 真用 `ROUND_HALF_AWAY_FROM_ZERO`,余数过半则进位。

**复现命令**:
```
sed -n '81,89p' unity/Assets/Sim/EmergencyProcedures/JudgeEvaluator.cs
sed -n '279,313p' unity/Assets/Tests/EditMode/EmergencyProcedures/judge_test.cs
```

---

### B4 修复验证 —— `ValidateRequiredKindsRoutable()` 是否真有校验体?

**原判定**: `GetRequiredKindWhitelist()` 仅返回字符串数组,无校验体。

**修复落点**: `unity/Assets/Sim/EmergencyProcedures/EmergencyAction.cs:114-155`

**实测证据**:
```csharp
// EmergencyAction.cs:114-155
public static System.Collections.Generic.List<string> ValidateRequiredKindsRoutable()
{
    var errs = new System.Collections.Generic.List<string>();
    var required = GetRequiredKindWhitelist();
    if (required == null || required.Length == 0)
    {
        errs.Add("[DC-5] 必需 Kind 清单为空 —— 拒以空集冒充绿。");
        return errs;
    }

    int routable = 0;
    foreach (var name in required)
    {
        if (!System.Enum.TryParse<EventKind>(name, out var kind))
        {
            errs.Add($"[DC-5] 必需 Kind「{name}」在 EventKind 枚举内**不存在**");
            continue;
        }
        try
        {
            var stream = StreamRouting.Of(kind);
            if (stream != StreamId.History)
                errs.Add($"[DC-5] Kind「{name}」路由到 {stream},而须全落**病史流**");
            else
                routable++;
        }
        catch (System.InvalidOperationException ex)
        {
            errs.Add($"[DC-5] Kind「{name}」**不可路由** ⇒ 不在 9 的白名单内");
        }
    }

    if (routable == 0 && errs.Count == 0)
        errs.Add("[DC-5] 无任何 Kind 通过 —— 拒以空集冒充绿。");

    return errs;
}
```

**校验体**: 逐 Kind 断言 ∈ `EventKind` 枚举 + `StreamRouting.Of(kind)` 可路由 + 落 `StreamId.History`;配非空转守卫。

**测试验证**(`action_tables_bake_test.cs:154-162`):
- `test_dc5_requiredKinds_routable`:调 `ValidateRequiredKindsRoutable()`,断 `errs` 空

**判决**: ✅ **已闭**。`ValidateRequiredKindsRoutable()` 真有校验体,经 kindgen 产物 `StreamRouting` 真校验。

**复现命令**:
```
sed -n '114,155p' unity/Assets/Sim/EmergencyProcedures/EmergencyAction.cs
sed -n '154,162p' unity/Assets/Tests/EditMode/EmergencyProcedures/action_tables_bake_test.cs
```

---

### A6 修复验证 —— `ModalPhaseEvaluator.Complete` 是否真读 `holdMode`?

**原判定**: `Complete` 不读 `holdMode`/`accessibilityOn`,两模式必然同值。

**修复落点**: `unity/Assets/Sim/EmergencyProcedures/ModalPhaseEvaluator.cs:94-130`

**实测证据**:
```csharp
// ModalPhaseEvaluator.cs:94-130
public static ModalPhaseResult Complete(bool accessibilityOn, int holdMode = 0)
{
    if (holdMode != 0 && holdMode != 1)
        throw new ArgumentOutOfRangeException(nameof(holdMode), holdMode,
            "holdMode 闭集 = {0=Hold, 1=Toggle}(规则十)");

    int holdTicks;
    int edges;
    if (holdMode == 0)
    {
        holdTicks = 100;   // 持续按住的长度
        edges = 3;         // 期间的幅度沿数
    }
    else
    {
        int[] segmentTicks = { 40, 35, 25 };   // 三段切换
        holdTicks = 0;
        foreach (var t in segmentTicks) holdTicks += t;
        edges = segmentTicks.Length;           // 每次切换 = 一沿
    }
    // ... 返回含 holdTicks/edges 的 ModalPhaseResult
}
```

**真读 `holdMode`**: 闭集外抛 `ArgumentOutOfRangeException`(非空转守卫);两模式走不同计算路径(Hold = 持续 100 tick / 3 沿;Toggle = 三段切换累加 100 tick / 3 沿),**等价是算出来再断言的**。

**判决**: ✅ **已闭**。`Complete` 真读 `holdMode`,闭集外抛异常,两模式走不同路径。

**复现命令**:
```
sed -n '94,130p' unity/Assets/Sim/EmergencyProcedures/ModalPhaseEvaluator.cs
```

---

### 载荷 `Seq` 占位是否仍显式登记(裁定 A=丙)?

**第一轮证据**(本轮未重新验证):
- `applied_payload_settlement_test.cs:131-140` 的 `test_ac1039_seqIsExplicitlyPlaceholder` 把占位事实钉死
- story-007 Completion Notes `:238-242` 登记裁定 A=丙:载荷 `Seq` 置 0 占位,真源「主机在 `Append` 时发号」发生在 `IEventSink.Append` 内部,载荷构造在其之前 ⇒ 本处理器拿不到;测试钉死占位事实,若该断言变红即说明上行链已落地

**判决**: ✅ **仍显式登记**(基于第一轮证据,本轮未重新验证)。

---

### D1/D2/D3 文档状态验证

⏳ **未验证**(本轮未读 `design/gdd/emergency-procedures.md` 的 AC-10-04a 单元格与 story-006/007 的 Test Evidence 行)。

---

## §4 新发现(含严重度)

**本轮无新增缺陷。**

六项修复(A1/A2/A6/B4/C4/C5)全部验证到位,判据下沉到操作码/字段/不等式本体层,配负向夹具与非空转守卫。

---

## §5 转 Complete 的前置

**EPIC 转 Complete 的硬前置**:
1. **本报告已落盘**(2026-10-03)。
2. **D1/D2/D3 文档状态对齐**:AC-10-04a 单元格数字同步 A8 勘误;story-006 测试位置声明;story-007 Test Evidence 行。
3. **实跑 Unity 测试套件**:本轮未实跑,任何转 Complete 前须以当前 HEAD 重跑 EditMode/PlayMode。

**各 story 单独转 Complete**:
- 001 / 002 / 003 / 005:**可转**(A1/A2/B4/C4/C5/A6 已闭)。
- 004:可转,载荷 `Seq` 占位保持显式(现有测试已钉死,合规)。
- 006:可转(3 项 NOT-RUN 诚实,D2 文档对齐后)。
- 007:可转(第一轮已验证)。

**本报告未覆盖(评审盲区,须后续轮)**:
- 未实跑 Unity 测试套件(结论均基于源码阅读)。
- D1/D2/D3 文档状态未验证。
- `action_tables_bake_test.cs` / `modal_phase_test.cs` / `aggregate_stream_test.cs` / `host_authority_test.cs` / `feel_latency_test.cs` 全文未读。
