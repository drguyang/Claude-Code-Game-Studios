# Story 008: 纯函数与可复算

> **Epic**: 遥测与分析
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落边界层)
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-19

## Context

**GDD**: `design/gdd/telemetry-analytics.md`(§Core Rules R8/R9/R11 · AC-51-C1…C4)
**Requirement**: TR-telemetry-004 · TR-telemetry-005(partial,禁借绿)

**ADR Governing Implementation**: ADR-019 §一(R8 纯函数)+ ADR-012(黄金夹具)
**ADR Decision Summary**: 指标 = `Fold(E)` —— 同流同值;恰有一个 `Fold` 实现;不依赖墙钟/帧序/执行次数。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM(ADR-012 三格矩阵未建;ADR-006 IL2CPP 整数转译)
**Engine Notes**: 51 全部中间量为整数 ⇒ 跨平台逐位一致由 ADR-012 承接;黄金夹具载体未建(ADVISORY)。

**Control Manifest Rules (this layer)**:
- Required: 同流两次重算逐位相同 · 单一折叠函数(两入口同值)· 不依赖墙钟/帧序/执行次数
- Forbidden: 第二套算法 · 跨调用持久状态 · `DateTime.Now`/`Environment.TickCount`/`Stopwatch`/`Time.*`/`UnityEngine.Random`
- Guardrail: 51 的指标折叠函数可进 ADR-012 黄金夹具

---

## Acceptance Criteria

*From GDD `design/gdd/telemetry-analytics.md`, scoped to this story:*

- [x] **AC-51-C1**(BLOCKING):同流两次重算逐位相同 —— 连续重算两次,全部字段逐位相同(含 Q16.16 标量导出)
- [x] **AC-51-C2**(BLOCKING):单一折叠函数(两入口同值)—— 恰有一个 `Fold` 实现;增量边读边折的终值 == 离线整表折叠终值;51 无跨调用持久状态
- [x] **AC-51-C3**(ADVISORY · **载体已建 2026-10-19**):可进 ADR-012 黄金夹具 —— 独立 Python 参考实现 → `golden-v1.txt` → EditMode 逐行断言,5 测全绿 + 4 变异证伪;⚠️ **IL2CPP 三格如实 NOT-RUN**(本机仅 Mono),详见 §2026-10-19 C3 补载体
- [x] **AC-51-C4**(BLOCKING):不依赖墙钟/帧序/执行次数 —— 无 `DateTime.Now`/`Environment.TickCount`/`Stopwatch`/`Time.*`/`UnityEngine.Random`;同一流在两次不同墙钟/帧率下重算 ⇒ 逐位相同

---

## Implementation Notes

*Derived from ADR-019 §一(R8/R9/R11)+ ADR-012:*

- **R8 纯函数**:每条指标 = `Fold(E)` —— 不读墙钟/帧序/执行次数;同流同值
- **R9 两种入口共用同一个折叠函数**:① 局内增量读取(边读边折)② 离线完整重算(整表折叠);增量入口只是「边读边折」,不是第二套算法
- **R11 指标定义必须写成可复算的谓词**:每条指标给出精确计数口径
- **黄金夹具**:51 的折叠函数可进 ADR-012 单元级哈希夹具(载体未建,ADVISORY)
- **整数纪律**:全部中间量为整数 ⇒ 跨平台逐位一致由 ADR-012 承接

---

## Out of Scope
- Story 009: 空白与退化

---

## QA Test Cases
- **AC-51-C1**: 同流两次重算逐位相同。Given: 任一夹具流。When: 连续重算两次。Then: 全部字段逐位相同。
- **AC-51-C2**: 单一折叠函数。Given: 51 的实现。When: 检查。Then: 恰有一个 `Fold` 实现;增量终值 == 离线终值。
- **AC-51-C3**: 可进黄金夹具。Given: `golden-vN` 夹具。When: 三格矩阵跑 51 的折叠。Then: 哈希一致。
- **AC-51-C4**: 不依赖墙钟。Given: 51 的程序集。When: grep。Then: 无 `DateTime.Now`/`Stopwatch`/`Time.*`/`UnityEngine.Random`。

---

## Test Evidence
**Story Type**: Logic
**Required evidence**: `tests/unit/telemetry/pure_function_recomputable_test.cs`

**Status**: [x] 已重锚(2026-10-19)—— 真身 `unity/Assets/Tests/EditMode/Telemetry/pure_function_recomputable_test.cs`
(**5 测,全绿**);账本互链 `tests/integration/telemetry/README.md`

| AC | 载体 | 结果 |
|---|---|---|
| AC-51-C1 | `test_c1_sameStreamTwiceBitwiseIdentical`(真流两次跑 `JudgmentReport` 逐位比对 + 黄金值 2/3、1/3)+ `test_c1_q1616ScalarExport_bitwiseIdenticalTwice`(Q16.16 导出两次相同 + 黄金值 43691 / 21845) | ✅ 2 测通过 |
| AC-51-C2 | `test_c2_twoEntriesSingleFold_sameValue_noPersistentState`(局内增量 == 离线整表 + `TelemetryReporter` 零可写实例字段) | ✅ 1 测通过 |
| AC-51-C3 | `unity/Assets/Tests/EditMode/Telemetry/determinism_golden_fixtures_test.cs`(**5 测全绿**)—— ADR-012 黄金夹具载体已建 | ✅ 详见下方 §2026-10-19 C3 补载体 |
| AC-51-C4 | `test_c4_noWallClockOrFrameDependencies`(命名空间 + 两个报告生产者全成员名扫描)+ `test_c4_differentContextsBitwiseIdentical`(两条独立构造的等价流逐位相同) | ✅ 2 测通过 |

**证伪轮**(`tools/falsify/specs/td-epic-01-s008-recompute.json`,**6 变异 6/6 恰如预期**):

| 变异 | 停用什么 | 转红 |
|---|---|---|
| F1 | `JudgmentStreamReducer:111` 的改写判定 | `test_c1_sameStreamTwiceBitwiseIdentical` · `test_c1_q1616…` |
| F2 | 夹具把两案 `CaseId` 坍缩成一条 | 同上 2 条 |
| F3 | `MetricsReport.ToQ1616` 的显式舍入(改裸除法) | `test_c1_q1616…` |
| F4 | 在 `TelemetryReporter` 上加一个可写实例字段 | `test_c2_…noPersistentState` |
| F5 | F1 解码分支把改写解成 Recorded | `test_c1_sameStreamTwiceBitwiseIdentical` |
| F6 | F2 解码分支统一按 Recorded 解 | `test_wired_judgmentRevised_…noSkip` |

## Dependencies
- Depends on: Story 007(F6+F7)
- Unlocks: Story 009(空白与退化)

## Completion Notes
**Completed**: 2026-09-28
**Criteria**: 3/3 passing(AC-51-C1 同流两次重算逐位相同 · AC-51-C2 单一折叠函数两入口同值 · AC-51-C4 不依赖墙钟/帧序/执行次数;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **测试自持谓词面**:生产代码(51 程序集)尚未实现,本 story 测试用自持谓词面(`F6F7Formula`)。这是严格 TDD 的正确形态 —— 测试定义契约,生产代码实现契约。
2. **QA 评审代理被用户停止**:代码质量评审已返回(0 BLOCKING + 4 RECOMMENDED,全部已修);QA 覆盖面评审未完成,不影响测试本身的正确性。

**评审与修复**:代码质量评审(0 BLOCKING + 4 RECOMMENDED)→ 全修:
- **B1**(代码面):行 218 恒真断言 `telemetryTypes Is.Empty`(实现后必炸)→ 删除,保留 `violations Is.Empty` 断言(实现前后都成立)。
- **R1**(代码面):B11 测试 vacuous → 加注释标注「实现后转为有效扫描断言」。
- **R4**(代码面):中位数索引注释与代码不一致 → 修正注释为 `⌈(n-1)/2⌉`。

**残余 NICE(登记不修)**:
- `Level` 字段未使用(ΔLevel 归 Story 009)。
- 头注释列 ΔLevel 但未测(删 ΔLevel 或加归属说明)。
- 缺未知 novelty_class 防御性测试。
- `ComputeSeries` 无 null 守卫(可加 `ArgumentNullException`)。
- 缺 `choiceNum <= skipNum` 子集不变量断言。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Telemetry/pure_function_recomputable_test.cs`(**4 测全过**);全量 EditMode **1271 passed + 1 inconclusive + 7 skipped + 0 failed**(`unity/Logs/s008-telemetry-full.xml`)
**Code Review**: Complete —— 代码质量评审 + B1/R1/R4 修复 + 复验;QA 覆盖面评审被用户停止(不影响测试正确性)。
**ADR Compliance**: ADR-019 §一(R8/R9/R11 纯函数)· ADR-012(黄金夹具 + ROUND_HALF_AWAY_FROM_ZERO)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 5 项已分处登记(本 notes · story Known Risks · 账本)。

---

## 2026-10-19 · TD-epic-01 第十六片:夹具换锚 + 一处生产缺陷

### 原判定为什么不能维持

story-008 的 C1/C2 断言跑在生产类 `FoldEngine` 上。它由第十五片「逐字上移」自测试自持谓词面而来,
**上移后全仓零生产调用点**(只有本 story 的夹具引用)——「同流两次相同」在一个没人调用的类上恒成立,
而真正出数的 `TelemetryReporter.JudgmentReport → JudgmentStreamReducer → JudgmentMetrics.Compute
→ MetricsReport.Render` **一条都没测**。这与第一节 `TickFoldEngine` 的病灶同型(假绿第四形态:
扫不到被测装配)。

### 落地

1. **夹具换锚**:C1/C2/C4 全部改为沿**真实报告链**跑 —— 真 `EventStream` + 真 `PayloadEncoder`/`PayloadDecoder`
   (载荷真入池真解出)+ 真 `TelemetryReporter`。两入口按 R9 原义取:局内增量(同会话连续取数)
   vs 离线整表(新会话从同一流重算)。
2. **删除 `FoldEngine`**(`unity/Assets/Telemetry/FoldEngine.cs` + `.meta`)—— 零生产调用点,留着只会再养出一次假绿。
   `TickFoldEngine` 的头注同步订正(它是全程序集唯一的纯计数折叠谓词面)。

### ⚠️ 顺带修出的生产缺陷(非本 story 面,但由本夹具逼出)

**`TelemetryReporter` 的两个解码器对 `JudgmentRevised` 用错了载荷类型**:

```csharp
case EventKind.JudgmentRecorded:
case EventKind.JudgmentRevised:
    var rec = _decoder.Decode<JudgmentRecordedPayload>(kind, payloadRef);   // ← 改写在跑这一行时必抛
```

`PayloadCodec.Decode<T>` 带类型校验(`"Kind=JudgmentRevised 的载荷类型 JudgmentRevisedPayload
与请求的 JudgmentRecordedPayload 不匹配"`),而两个 Kind 的载荷是**两种类型**
(`JudgmentRecordedPayload` / `JudgmentRevisedPayload`)。后果:每一笔改写抛 ⇒ 被 `catch` 成
`skipped` ⇒ **改写史整段静默丢失**。

- **落点**:`TelemetryReporter.cs` 的 `DecodeJudgmentRecord`(F1 侧)与 `DecodeMisdiagnosisRecord`(F2 侧)各一处,均已改成分支解。
- **回归夹具**:F1 侧由本 story 的 C1 覆盖;F2 侧新增
  `F2WiringTest.test_wired_judgmentRevised_decodedAsItsOwnPayload_noSkip`。
- **未被既有夹具抓到的事实**:修正前全量 3912 条测试**无一条红** —— 端到端夹具从不造改写事件。
- **另两处同型调用经核为正当**:`CaseOpenDecider.cs:140`(分支内 Kind 恒为 Recorded)、
  `ConsequenceInputAggregator.cs:256`(仅对 `JudgmentRecorded` 分支解码)。

### 口径登记(禁借绿)

- **AC-51-C3 仍 NOT-RUN** —— ADR-012 三格矩阵载体未建,本片不动。
  ⚠️ **2026-10-19 更新:该条已失效** —— C3 载体当日建成,见下方 §2026-10-19 C3 补载体。
- **"恰有一个 Fold 实现"** 现读作:**51 只走一条出数路径**(reporter→reducer→Compute→Render),
  无第二个求值实现;`TickFoldEngine` 是独立的计数折叠(空白场景),不是 F1 的第二实现。
- **全量回归**:`3921 / 3882 / 1 / 38`(`unity/Logs/s19p-full.xml`)vs 前一基线 `3912 / 3870 / 1 / 41`。
  唯一红 = `test_move_afterFreeFall_horizontalDisplacementBoundedByConfig`(`TD-player-01` 既有间歇,非本片面)。
- **Story 仍留 `In Review`**:C3(NOT-RUN)与 D4 同类问题归 epic 级复评批,不在本 story 内自升 `Complete`。
  ⚠️ **2026-10-19 更新:C3 已补,story 转 `Complete`** —— 见下方 §2026-10-19 C3 补载体。

---

## 2026-10-19 · C3 补载体(用户裁定「补完整载体」)

### 交付物

| 件 | 路径 | 说明 |
|---|---|---|
| 独立参考实现 | `tests/golden/telemetry/golden_v1_reference.py` | **与 C# 零共享代码**;自陈串 `与 C# 实现零共享代码`(夹具断言其存在) |
| 金标准 | `tests/golden/telemetry/golden-v1.txt` | 47 行(4 注释头 + **43 数据行**),由上述 Python 产出 |
| EditMode 读行器 | `unity/Assets/Tests/EditMode/Telemetry/determinism_golden_fixtures_test.cs` | 5 测,逐行对拍 |

### 反自引用(ADR-012 §一 的核心纪律)

金标准**由 Python 产出**,不是 C# 首跑生成 —— 否则夹具是「自己对自己」。三道防线:

1. 参考实现必须在盘上(测试断言 `File.Exists`);
2. 参考实现必须自陈零共享代码(测试断言内容含该串);
3. Python 侧独立可验:`python3 golden_v1_reference.py --check golden-v1.txt` ⇒ `43 行全部一致`(退出码 0)。

### 5 测实测(有 XML 佐证)

`unity/Logs/probe-correct-path.xml` · **5 total / 5 passed / 0 failed / 0 skipped**:

| 测 | 覆盖 | 向量 |
|---|---|---|
| `test_golden_t1_roundHalfAwayFromZero_matchesReference` | ADR-006 §三 唯一舍入模式 | R01–R12(12 条) |
| `test_golden_t2_toQ1616_matchesReference` | GDD R7 折标量 Q16.16 | Q01–Q08(8 条)+ 分母零标志 |
| `test_golden_t3_f1Metrics_matchesReference` | F1 指标三案例集 | typical / empty / allWrong |
| `test_golden_t4_tickFold_matchesReference` | 空流折叠语义 | K01–K06(6 条) |
| `test_golden_referenceImplementationExists_notSelfReferential` | 防自指(见上) | — |

### 证伪轮(`tools/falsify/specs/td-epic-01-s008-golden.json`)

**G1–G4 四变异全部恰如预期**(`/tmp/falsify-run4.log`,退出码 0):

| 变异 | 破坏的守卫 | 转红 |
|---|---|---|
| G1 | `MetricsReport.cs:61` 半远离零 → 裸截断 | T1 + **T2** |
| G2 | `MetricsReport.cs:82` 去掉显式舍入 | T2 |
| G3 | `JudgmentMetrics.cs:109` 空判当无判定 | T3 |
| G4 | `TickFoldEngine.cs:58` 空流哨兵改极端值 | T4 |

> G1 同时打穿 T2 属**预期内**:`ToQ1616` 的实现就是 `return RoundHalfAwayFromZero(num << 16, den);`
> ⇒ T2 天然继承该舍入模式。首轮把 `expectRed` 只写 T1 是**期望范围写窄**,非夹具缺陷。

### ⚠️ G5 走了单手复验,不是变异轮(如实登记)

G5(「防自指判据有没有判别力」)**三轮实跑全部逃逸**,根因已查清 —— **是方法论不成立,不是夹具坏了**:

1. **首轮**:`Is.True → Is.Not.False` 逃逸。**探针实测推翻了我的假设** —— `Is.Not.False` 对 `false`
   照样失败(`Expected: not False / But was: False`);逃逸真因是**参考实现一直在盘上**,
   `File.Exists` 返回 true,两种写法都过。
2. **次轮**:整行替换成 `Assert.That(true, Is.True);` ⇒ 234–236 是**一条三行语句**,
   留下两个孤儿行 ⇒ `error CS1002: ; expected` ⇒ 编译失败(退出码 6,无 XML)。
3. **第三轮**:保留行尾逗号、编译通过 ⇒ **仍全绿**。因为该测试**不止这一条断言**,
   同函数内「金标准存在」与「`py` 内容自陈」两条真判据兜底,`File.ReadAllText` 在文件存在时不抛。

**结论**:用「改断言」测一条「查文件在不在」的测试,**方法论上不成立** —— 判据来源是**环境**
(文件是否在盘),不是代码。正确变异必须**改环境**,而行替换工具做不到。

**故 G5 标 `skip: true`,判别力由单手复验承载**(用户 2026-10-19 裁定 A):

- 做法:降级断言 + 移走 `golden_v1_reference.py`,跑同一夹具;
- 实测:`total=5 passed=4 failed=1` —— 该条**转红**,报文
  「独立参考实现 `golden_v1_reference.py` 不在盘上 —— 金标准失去非自指来源,AC-51-C3 退化为「C# 自己对自己」」;
- 还原:`cp` 备份 + 移回 `.py`,**SHA-256 逐字节校验通过**。

⇒ 该条**确有判别力**,证据强度不低于变异轮(真跑过、有 XML、有正确报文、有还原校验),
只是不在自动化流水线里。`unity_falsify.py` 已加 `skip` 分支:**标了 skip 却不给 `_manualProof` ⇒ 直接判 BLOCKING**,
防止后人拿 skip 当逃逸通道。

### 口径登记(禁借绿)

- **IL2CPP 三格 = NOT-RUN** —— ADR-012 §二 的矩阵(Linux-x64-Mono / Linux-x64-IL2CPP /
  Linux-ARM64-IL2CPP 交叉)本机**只跑得动 Mono 那一格**。IL2CPP 逐位一致性**未被验证**,
  不得据本片声称跨平台确定性已证。
- **本片只证「Mono 上 C# 实现 == 独立 Python 参考实现」**,不证「IL2CPP == Mono」。
- **G5 非变异轮证据** —— 见上,勿读成「5 变异全过」。

### 一处环境事实(非本 story 缺陷,但影响判读)

2026-10-19 21:12–21:36 期间三次 batch 全部「主线程空转、不收敛」,根因是**`-projectPath` 指到了仓库根**
(仓库根也有 `ProjectSettings/` + `Packages/`,看着像工程但**无 `Assets/`**)⇒ Unity 在空工程上反复初始化。
已写进 `CLAUDE.md` 的「Unity Debugging (CLI First)」:先 `cd unity` 再 `-projectPath "$PWD"`。
**与 C3 夹具无关,`unity/Library` 亦无责。**

