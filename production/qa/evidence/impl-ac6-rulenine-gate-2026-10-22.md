# 实现证据 · S18-1 AC ⑥ 规则九机械门(`R2-AC6` 关闭)

**日期**:2026-10-22
**对象**:`unity/Assets/Editor.Tools.Gates/RuleNineGate.cs`(新立)
**依据**:`design/gdd/disease-simulation.md:243-245` / `:356-358` ·
`production/qa/qa-plan-sprint-18-2026-10-21.md:80`(判据形态 = **IL + 源码双证**)
**结论**:`R2-AC6` 从 **NOT-RUN** → **已兑现**;AC ⑥ 转 ✅

---

## 一、问题是什么(为什么这一条卡了这么久)

AC ⑥ 的字面要求是「规则九有机械门」。它被记为 NOT-RUN 的**根因不是没写**,
而是**用一句自述顶替了一个从未存在的门**:

- S18-1 的测试注释声称「文件末尾有 AST 式断言」守住规则九;
- 实测该断言**从未存在**;
- `unity/Assets/Editor.Tools.Gates/` 下 24 个门类**无一条**与规则九相关;
- 而 `sprint-18.md` 的 DoD 表把「Tasks 表 ②」勾成了 ✅ —— 那勾的是**测试里的一条断言**
  (`test_ruleNine_kindIsDataDrivenNotControlFlow`),不是门。

这是本项目抓过的**第五个假绿形态**:不是「测试跑自造断言」,是
**「有门」这句话本身没有对应物**。同批已把这条写进
`.claude/docs/coding-standards.md` §Review Evidence Standards:
「**「某件有门」的自述须给 `file:line` + 可跑命令**;缺一即按 `NOT-RUN` 登记」。

---

## 二、判据形态为什么是「双证」而不是「grep」

`qa-plan-sprint-18-2026-10-21.md:80` 原文:

> 判据形态 = **反射 / IL 扫描 + 源码 grep 双证**;单靠 grep 可被换写法绕过

两半的**盲区不同**,所以缺一即不达标:

| 半 | 能抓 | 抓不到 |
|---|---|---|
| 源码 | `switch` / 多语句 `if` / 三元 / 具体病种名字面量 | 等价改写(抽成 helper、调用序颠倒)、跨方法拼装 |
| IL | `switch` 分派表、枚举常量穷举、`DiseaseKey` 与 `ldstr` 常量比较 | 语义(判不了「这段分支算不算业务逻辑」) |

---

## 三、规则清单与落点

### 源码半(`RuleNineGate.CheckSource`)

| 号 | 判据 | 落点 |
|---|---|---|
| S1 | `switch (x.Kind)` **且** case 标签含 `case DiseaseKind.X:` ⇒ 红 | `RuleNineGate.cs` `ScanSourceFile` + `SwitchDispatchesOnDiseaseKind` |
| S2 | `if (…Kind == DiseaseKind.X…)` 且分支体**非单条 guard** ⇒ 红 | 同上 + `IsSingleGuardStatement` |
| S3 | `Kind` 比较作三元条件 ⇒ 红 | 同上 |
| S4 | `DiseaseKey == "<字面量>"` 类 ⇒ 红 | 同上 `DiseaseKeyLiteral` |

**S2 白名单形态 = 分支体恰一条 `return` 或 `throw`**。现存两处活体样本:

- `Sim/EntityDeathGates.cs:79-81` —— `return entry.Kind == DiseaseKind.Injury && …`
  (数据成员判定,不是控制流分支)
- `Sim/RegistrySchema.cs:336-339` —— `if (entry.Kind == DiseaseKind.Unspecified) throw …`
  (R1-31 零值陷阱守卫,漏填即响亮失败)

### IL 半(`RuleNineGate.CheckIl`,Cecil)

| 号 | 判据 |
|---|---|
| I1 | `switch` 指令的操作数来自 **`FieldType == DiseaseKind`** 的 `Kind` 字段装载 |
| I2 | 同一方法内 ≥3 个不同 `DiseaseKind` 常量与 `Kind` 比较(穷举式分支) |
| I3 | `DiseaseKey` 字段装载后与 `ldstr` 常量做 `String.op_Equality` / `String.Equals` |

---

## 四、⚠️ 假红首例(就地记账,不隐藏)

**首版判据锚在字段名 `Kind` 上**,真工程立刻报出两处:

| 位置 | 实际字段类型 | 与规则九的关系 |
|---|---|---|
| `Sim/Clinic/ClinicRoomMatcher.cs:279` | `ClinicRoomClauseKind`(MinCount / HasType / AdjacencyMinCount) | **无关** —— 医馆房间子句类型 |
| `Sim/Consequences/ConsequenceInputAggregator.cs:294` | `EventKind` | **无关** —— 且这是 ADR-024 的 35 支 Kind **路由**,禁掉它等于禁掉 ADR-024 |

**判据过宽 = 假红,与假绿同属静默失败。** 收紧到**类型级**:

- 源码半:比较式**必须**带 `DiseaseKind.` 限定(`ClinicRoomClauseKind.Injury` 之类同名尾巴不命中)
- IL 半:`FieldReference.FieldType.FullName == "DaYiJingCheng.Sim.DiseaseKind"`

收紧的代价是**可能失去覆盖**,故补锚点活体守卫
`test_ruleNine_ilHalf_diseaseKindAnchorIsLive`:若 `DiseaseKind` 改名 / 换命名空间 /
`DiseaseRegistryEntry.Kind` 换类型,本门会**静默失去判别力** ⇒ 该测试钉住锚点,并核
`using static …DiseaseKind;` 零命中(裸写会让源码半漏报)。

---

## 五、门真会红(非空转判据)

`unity/Assets/Tests/EditMode/DiseaseSimulation/rule_nine_gate_test.cs` —— **16 条**:

| 组 | 测试 | 证明什么 |
|---|---|---|
| ① 真工程 | `realTree_sourceHalfIsClean` / `realTree_ilHalfIsClean` | 两半对真树零违例 |
| ① 白名单 | `existingLegalForms_areNotFlagged` | 两处合法形态**不被**判红(防白名单过窄) |
| ② 源码注入 | `S1_switchOnKind_isRed` / `S2_multiStatementIfOnKind_isRed` / `S3_ternaryOnKind_isRed` / `S4_hardcodedDiseaseKey_isRed` | **四种形态各自转红** |
| ② 反向 | `legalReturnConjunction_inProbe_isGreen` / `commentedViolation_isGreen` / `sameNamedKindTail_isNotFlagged` | 合法形态 / 注释 / 同名尾巴**不得**误报 |
| ③ IL 注入 | `ilHalf_injectedViolations_areRed` | **I1/I2/I3 三条规则各自抓到注入的违例**(见 §九之二) |
| ③ IL 守卫 | `ilHalf_missingDll_isRed` / `ilHalf_actuallyScansMethodBodies` / `ilHalf_hasThreeRuleFamilies` / `ilHalf_diseaseKindAnchorIsLive` | 产物缺失记红、真 DLL 非零方法体、七条判据号都在、锚点活着 |
| ④ 接线 | `runAll_coversBothHalves` | `RunAll` 同时跑源码半与 IL 半 |

**实测**:`16 / 16 passed / 0 failed`(`Logs/rulenine-test2.xml`)。

---

## 六、探针轮(CLAUDE.md §构建前门纪律 · 三件套)

> 「**凡要确认「某个 `IPreprocessBuildWithReport` 门/接线真的在生效」,不能只看构建绿,
> 必须跑一次探针轮**:在门里插一条**无条件** `errs.Add("[PROBE]…")` 并提前 `return`,
> 重跑同一构建,要求 **`Result: Failure`** 且日志里出现该探针串。」

| # | 要素 | 实测 |
|---|---|---|
| 1 | 探针串出现在**构建日志**里 | ✅ `Logs/build-probe-rulenine.log` 2 处 `[PROBE-RuleNine]` |
| 2 | 栈帧上是目标类的 `OnPreprocessBuild`(给出行号) | ✅ `DaYiJingCheng.EditorTools.Gates.AssemblyGates/BuildGate:OnPreprocessBuild … (at Assets/Editor.Tools.Gates/AssemblyGates.cs:1275)` |
| 3 | 构建**因它失败** | ✅ `Build Finished, Result: Failure.` + `BuildFailedException: [AssemblyGates] 构建前门失败 2 条(… 规则九门)` + `##utp:{"phase":"Immediate",…,"steps":[{"description":"Preprocess Player",…}]}` 即止 |

### 还原判据 = SHA-256 逐字节

```
变异前: 44e502a64073fe5594705bc705c60634b2cba6eebdec0059847c845d0e29c628  Assets/Editor.Tools.Gates/RuleNineGate.cs
还原后: sha256sum -c → 「成功」  EXIT=0
```

**不用 `git checkout` / `git stash`**(承「证伪还原禁用 checkout」纪律)。

### 干净轮

`Logs/build-clean-rulenine.log`:

```
Build Finished, Result: Success.
grep -cE "\[RuleNine|\[fs-write|error CS"  →  0
```

⇒ **「门跑了且零违例」**,不是「门没被调用」(探针轮已排除后者)。

⚠️ **该轮跑于 IL 注入夹具加入之前**。加入夹具后重跑一次:

`Logs/build-clean2-rulenine.log`:
```
Build Finished, Result: Success.
grep -cE "\[RuleNine|\[fs-write|error CS"  →  0
```

⇒ 夹具住在 `Assets/Tests`(门扫描面之外),**未**让构建转红 —— 反过来证明
扫描面**没有**被误扩到测试树(否则注入的违例会让门自己红,那是判据过宽)。

---

## 七、探针轮里的一个附带发现(既有门工作正常)

探针轮日志除探针串外还有**第二条红**:

```
[fs-write·R2] Assets/Tests/EditMode/DiseaseSimulation/rule_nine_gate_test.cs 同时含写盘调用(3 处)
与仓库锚点(Application.dataPath) —— 须在 FsWriteBoundaryGate.Declarations 里**具名声明**…
```

这是**我新写的测试文件**触发了 S16-4 的写盘边界门。处置:

- 测试写盘目标确为 `Path.GetTempPath()/rulenine-probe-<guid>`,`TearDown` 即删;
- `Application.dataPath` 仅用于**读**(定位 `unity/` 根扫源文本 + 取 `ScriptAssemblies` 的 DLL);
- ⇒ 按该门协议在 `FsWriteBoundaryGate.Declarations` 补**具名声明**(声明不是豁免,
  它要求写清「锚点喂的是读还是写、写落哪儿」)。

**这条红是门在正常工作,不是门坏了。**

---

## 八、接线(零调用方的门形同虚设)

| 入口 | 落点 |
|---|---|
| 菜单 / 域重载 | `AssemblyGates.RunMenu` → `RuleNineGate.RunAll(".")` |
| 构建前 fail-fast | `AssemblyGates.BuildGate.OnPreprocessBuild` → 同上 |
| 接线清单(入口级) | `gate_wiring_manifest_test.cs` `EntryWiringManifest["RunMenu"]` / `["BuildGate"]` |
| 接线清单(类级) | 同上 `ClassConsumerManifest` → `RuleNineGate`, `Kind = Chain`, `Consumer = "RunMenu+BuildGate"` |

⚠️ `ClassConsumerManifest` 是**手写常量**,禁从源生成(生成会让「源 ↔ 清单」比对退化成恒等式)。

---

## 九、已知盲区(登记,不借绿)

1. 源码半**不建 AST** —— 条件跨行靠括号配平,极复杂嵌套可能漏;
2. IL 半 I2 阈值 ≥3 刻意留 2 臂余量 —— 2 臂违例由源码半 S2 接住;
3. 反射 / 动态调用构造的分派两半都看不见;
4. **病种数上限(~8)不由本门判** —— 那是 R1 表数据量,归注册表校验(`RegistrySchema`)。

> ✅ **原第 5 条(IL 注入级反证未做)已关闭** —— 见下方 §九之二。原文保留作闭环记录。

---

## 九之二、IL 注入级反证(原 §九.5 缺口的闭合)

### 问题

IL 半首版只有两条支撑:「真工程零命中」+「扫描确实执行(非零方法体)」。
**两条都不能证明它真会红** —— 「永远返回空」与「真没有违例」在结果上完全一样。
这与 AC ⑥ 当初被一句自述顶替属**同型风险**,不能留。

### 走过的两条路(第一条失败,如实登记)

**① Cecil 注入(否决)** —— 复制 `Sim.dll` → Cecil 加违例方法 → 写临时 DLL → 扫它。
编译期即 **111 条 `CS0234`**:`Mono.Cecil` 是 `Editor.Tools.Gates` 的 precompiled 依赖,
**不传递给引用它的测试装配**。要修须改 `EditMode.asmdef` 加引用 —— 那是**架构级改动**,
不在本件授权面内。

**② 测试装配自身产物(采纳)** —— 违例夹具写成**测试源码里的真方法**,
让**编译器**生成 IL,`CheckIl` 扫 `Sim.Contracts.Tests.dll`。

这条路反而更强:

- IL 是**真编译器**产出的,不是手工拼的字节;
- 夹具**不进 `Assets/Sim`** ⇒ 源码半对它**完全失明**(扫描面 = `Sim` / `Sim.Contracts`),
  而 IL 半抓住了 ⇒ **这正是 IL 半独立价值的直接证明**,不只是「它也跑了」。

### 三个夹具(刻意违例,只活在测试装配)

| 号 | 夹具方法 | 形态 |
|---|---|---|
| I1 | `InjectSwitchOnDiseaseKind` | `switch (e.Kind)` 四臂 |
| I2 | `InjectExhaustiveKindComparisons` | 同方法内 3 个 `DiseaseKind` 常量比较 |
| I3 | `InjectHardcodedKeyComparison` | `e.DiseaseKey == "INJ_STAB"` |

### 顺带修掉的一个真 bug

I2 的常量收集窗口首版写 `i+1 … i+3`,只覆盖「`ldfld` 在窗口内命中、常量在其后」一种相位;
若 `ldfld` 在 `i-1`(窗口仍命中)而常量在 `i`,**原写法漏收**。
改为从 `i` 起取,覆盖两种相位。

### 实测

`RuleNineGateTest` **16 / 16 passed / 0 failed**(`Logs/rulenine-test2.xml`),`error CS = 0`。
I1 / I2 / I3 三条断言各自通过 ⇒ **三条规则都不是空转**。

---

## 十、可复算命令

```bash
cd unity

# 源码半
grep -rnE '\bswitch\s*\(\s*[\w.]+\.Kind\s*\)' Assets/Sim Assets/Sim.Contracts

# 判据号齐否
grep -c '\[RuleNine/[SI][1-4]\]' Assets/Editor.Tools.Gates/RuleNineGate.cs

# 接线
grep -n 'RuleNineGate.RunAll' Assets/Editor.Tools.Gates/AssemblyGates.cs

# 测试(15 条)
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter "DaYiJingCheng.Tests.Unit.Sim.RuleNineGateTest" \
  -testResults "$PWD/Logs/rulenine-test.xml" -logFile "$PWD/Logs/rulenine-test.log"
```

---

## 十一、结账表

| ID | Landed(已落) | Proven(已证) | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|
| AC⑥-1 | ✅ | ✅ | `Editor.Tools.Gates/RuleNineGate.cs` | `RuleNineGateTest` 15/15 passed |
| AC⑥-2 | ✅ | ✅ | 同上(源码半 S1–S4) | `test_ruleNine_S{1,2,3,4}_*_isRed` 四条各自转红 |
| AC⑥-3 | ✅ | ✅ | 同上(IL 半 I1–I3) | `ilHalf_missingDll_isRed` + `ilHalf_actuallyScansMethodBodies` |
| AC⑥-4 | ✅ | ✅ | 同上 | 接线由 `gate_wiring_manifest_test` 钉住;**探针轮**已证构建期真跑;IL 半**注入级反证**见 §九之二(`test_ruleNine_ilHalf_injectedViolations_areRed`) |
| AC⑥-5 | ✅ | ✅ | `AssemblyGates.cs`(RunMenu + BuildGate) | 探针轮三件套 + 干净轮 `Result: Success` |
| AC⑥-6 | ✅ | ✅ | `FsWriteBoundaryGate.cs`(声明) | 干净轮零 `[fs-write` |

**结论**:AC ⑥ 由 **NOT-RUN** 转 **已兑现**。六条判据 **Landed ✅ / Proven ✅** 全齐
(原 AC⑥-4 的 IL 注入级缺口已于 §九之二 闭合)。

**最终全量回归**(截至 `2026-10-22 13:51 (+0800)` · 批次 `regress-rulenine2` · 干净树):

```
total=4451  passed=4415  failed=0  inconclusive=0  skipped=36   error CS = 0
基线 s18-ac78-full-20261022 = 4435 / 4399 / 0 / 36
Δtotal = Δpassed = +16     全名级差集 = 新增 16 / 消失 0
```
