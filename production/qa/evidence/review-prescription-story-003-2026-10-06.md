# 评审报告原件 — prescription-medication story-003(写者独占 / 零病名 / 算法独占)

**日期**: 2026-10-06
**评审对象**: `unity/Assets/Editor.Tools.Gates/PrescriptionWriterGates.cs`(新建门,822 行)+
`unity/Assets/Editor.Tools.Gates/AssemblyGates.cs`(接线)+
`unity/Assets/Tests/EditMode/PrescriptionMedication/drug_event_test.cs`(25 测)
**权威件**: `design/gdd/prescription-and-medication.md` AC-11-01 / AC-11-03 / AC-11-10 / AC-11-22 ·
`docs/architecture/tr-registry.yaml` TR-prescription-001/003/013/015/018 ·
ADR-009 Amendment I · ADR-024 · ADR-025 §① · ADR-029 §③ · `design/registry/entities.yaml:2286-2298`

---

## 〇 · 评审方式声明

- **双代理独立评审,只做一轮**(用户令):**结构侧** = 结构/AC 覆盖/架构合规;**QA 侧** = 测试判别力/变异存活。各自独立读码,互不可见。
- **两侧报告均已回收**(与 story-002 那次的回收缺口不同,本次两侧正文齐备)。
- 评审执行时点 = 本轮修复**之前**的树;本件记录**原判定 → 修复落点 → 验证命令**。

---

## 一 · 原判定汇总

| 侧 | 判定 |
|---|---|
| **结构侧** | **1 BLOCKING · 3 MAJOR · 2 MINOR · 2 NIT** |
| **QA 侧** | **1 MAJOR · 3 MINOR · 2 NIT · 6 变异(MUT-A…F)** |

**两侧独立重合的一条**:`CheckSimReferenceFace` 无阳性对照(结构侧 M-1 家族 / QA 侧 MAJOR-1)。

---

## 二 · 原判定 → 修复落点

### B-1(BLOCKING · 结构侧)· `IsBannedName` 对**属性访问器调用点**恒假阴性 —— AC-11-01① 的 IL 半边对本 AC 点名的威胁是**空转的绿**

**原判定**:`DrugProfile.Indications` 是 **auto-property**(`Sim.Contracts/ItemDatabase/DrugProfile.cs:39`)。11 若写 `profile.Indications`,编译为 `call ... get_Indications()`。该调用名**不含任何词边界**(`get_` 与 `Indications` 之间是 `_`,标识符字符且前一字符非小写)⇒ 原 `IsBannedName` **恒 False**。调用点 IL 也**不引用** backing field(那是 getter 自己体内的事)⇒ 字段面同样够不到。

**连带假信心**:三条自称「证明谓词有能力抓」的测试全部测错对象 ——
`test_drugEvent_diseaseNameIl_catchesFieldReadOfIndications` 拿 `Indications` 扫 **`Sim.Contracts`**(定义侧,getter 体内有 `ldfld <Indications>k__BackingField`)⇒ 命中,于是判「谓词有能力」;**它证的是定义处可抓,不是调用处可抓**。

**修复落点**:

| 件 | 变更 |
|---|---|
| `PrescriptionWriterGates.IsBannedName` | 增**访问器前缀展开**(`get_` / `set_`),**只剥一次**且只在名首;`private` → **`public`**(供单元级判据) |
| `drug_event_test.cs` | 原测**重写**为 `test_drugEvent_diseaseNameIl_catchesPropertyReadOfIndications` —— 阳性对照改打在**真调用点所在命名空间**(`Editor.Tools.Bake` 的 `CookedWriter.cs:156` 读 `drug.Indications`),并断言命中须**点名 `get_Indications`** |
| `drug_event_test.cs` | 新增 `test_drugEvent_isBannedName_expandsAccessorPrefixes` —— **单元级**判据(不依赖产物):访问器名必中 + 前缀只剥一次 + 词边界不误伤 |

### M-1(MAJOR · 两侧独立重合)· 门**未**接进 `BuildGate`;`CheckSimReferenceFace` 无阳性对照

**原判定**(结构侧):注释称「菜单 + 构建前门 + 测试三处驱动」,实测 `BuildGate.OnPreprocessBuild` 段**无本门引用** ⇒ AC-10-06b 字面的「漂移 ⇒ 构建失败」**无执行体**。
**原判定**(QA 侧,变异 MUT-D):把 `CheckSimReferenceFace` 的「零引擎程序集」分支与报错循环整段注掉后,**25 条全绿** —— 该测是唯一**裸判据**(只断言空集,无对照)。

**修复落点**:

| 件 | 变更 |
|---|---|
| `AssemblyGates.BuildGate.OnPreprocessBuild` | 补 `errs.AddRange(PrescriptionWriterGates.RunAll(out var rxSummary));` + 摘要转发;`BuildFailedException` 文案补「11 写者门」 |
| `drug_event_test.test_drugEvent_simReferenceFace_zeroEngineAndOnlyContracts` | 补**阳性对照** —— 对 `Gameplay.Presentation`(必引 `UnityEngine`)跑同谓词,断言 `posEngineRefs` **非空**且真含 `UnityEngine` |

### M-2(MAJOR · 结构侧)· `ProductionCtorSites` 收 `Sim.Codec/PayloadCodec` —— 门单方面放宽 AC 原文,无修订背书

**原判定**:AC-11-22 / TR-prescription-018 **逐字**为「构造点**仅存在于 11 的程序集**」,**无任何 read/write 区分**。而 `PayloadCodec.History.cs:295` **确实**在 11 之外构造该 struct。门把它放进「生产白名单」= **用注释改写 AC**,且降级发生在注释里。

**修复落点**(**不删放行,但把静默豁免改成显式记账**):

| 件 | 变更 |
|---|---|
| `PrescriptionWriterGates` | 新立 `DecoderCtorSites` 表,**与 `ProductionCtorSites` 分离登记**;`allowedSet` = 写者白名单 ∪ 解码表,**下界仍只对写者白名单成立** |
| `RunAll` 摘要 | 增 ⚠️ 行:点名「判据-文本背离」+ 两处出处 + 归 producer / TD |

⚠️ **诚实边界**:本件**未**裁定 AC 该不该收窄 —— 那归 producer / TD。本件只保证**背离是可见的、有账的**,不是静默的。

### M-3(MAJOR · 结构侧)· `DiseaseNameForbidden` 漏掉 GDD 逐字点名的 `DiagnosisResult`

**原判定**:AC-11-01① 原文(`prescription-and-medication.md:141`)逐字写「不含 8 的 `DiagnosisResult` **类**」,而禁名集不含它 ⇒ 反射半边点名的类型**根本没被断言**。当前 8 侧未落该类型(grep 零命中)⇒ 平凡通过,但判据面缺失是事实。

**修复落点**:`DiseaseNameForbidden` 补 `"DiagnosisResult"`(带出处注)。**待 8 侧落型后即成真判据**。

### m-1(MINOR · 结构侧)· `CheckPayloadPairing` 的「序数 = codec tag 锚」立论不成立

**原判定**:本工程 codec 是 **tag 驱动**(具名 `WriteFieldInt64(1,…)` + `RequireCompleteMask`),**字段声明序与 tag 序无关**。谓词比的是**声明序实例字段** ⇒ 注释给它加了一层它没有的能力。

**修复落点**:⬜ **未处置,如实保留** —— 判据本身能抓「名字/类型漂移」,是有用的;**过度的只是注释里的能力宣称**。已在测内注记该边界(见下「诚实登记」)。

### m-2(MINOR · 结构侧)· 故事卡 Test Evidence 陈旧

**原判定**:卡 `:77` 写「`drug_event_test.cs` **全库不存在**」,实测已存在且 25 测全过。

**修复落点**:✅ 本卡 `Test Evidence` / `Status` / `Completion Notes` 全刷(见下 §五)。

### m-3(MINOR · QA 侧)· AC-11-22 缺「影子装配注入」半边

**原判定**:故事卡 `:68` QA 用例写「影子程序集注入一次构造 ⇒ 红」,**测试套件未实现**。现有 `_whitelistCannotBeSilentlyGutted` 只证上界(Sim 不在白名单 ⇒ 报「写者独占」)与下界,未证「**新的**生产装配构造载荷 ⇒ 红」。

**修复落点**:⬜ **未处置,如实保留并登记** —— 现有下界判据 + `_missingArtifactIsRed` 已覆盖「扫描面塌缩」;「影子装配注入」需要**动态生成程序集**,与 EditMode 纯逻辑测的隔离纪律相抵(须落盘 dll)。登记为已知缺口。

### m-4(MINOR · QA 侧)· `IsBannedName` 词边界逻辑无测覆盖

**原判定**:注释声称「实测三例」但套件未实例化任一例;改成纯 `Contains` 后 25 条全绿。

**修复落点**:✅ 已由 B-1 修复的 `test_drugEvent_isBannedName_expandsAccessorPrefixes` **一并覆盖**(该测同时钉死词边界不误伤面)。

### NIT 处置

| 编号 | 原判定 | 处置 |
|---|---|---|
| n-1(结构) | 反射可达性扫描面窄 | ⬜ 如实保留(已在测内注记为反射的能力边界) |
| n-2(结构) | 不覆盖反射构造(`Activator.CreateInstance`) | ⬜ 如实保留 —— 载荷为 readonly-field struct,现无实际路径,休眠 |
| NIT-1(QA) | `diseaseBearingProfileField_isNotRead_positiveControl` 是自指对照 | ⬜ 如实保留(其价值 = 夹具前提守卫,已在测内注明) |
| NIT-2(QA) | 两处源码面测无空集守卫 | ✅ 已补 `Assert.IsNotEmpty(files)`(两处) |

---

## 三 · 变异证明(QA 侧实跑)

`Mut` 列 = 把生产件里对应的一行改坏;**「红」= 该次运行失败的测名**。
全部变异**已回滚**,`grep -rn "MUT-" unity/Assets/ --include=*.cs` ⇒ 目标文件零残留。

| 变异 | 改坏点 | 结果 | 红测 |
|---|---|---|---|
| **MUT-A** | `CheckDrugPayloadCtorSites` 去掉 `call .ctor` 分支(只留 `newobj`) | **3 红** | `_productionCtorSitesAreWhitelisted` · `_testAssembliesAreIsolatedButPresent` · `_whitelistCannotBeSilentlyGutted` |
| **MUT-B** | `IsBannedName` 的 PascalCase 复合边界 → 纯子串匹配 | **0 红(存活)** | — ⇒ 由 B-1 修复的单元测补上 |
| **MUT-C** | `CheckPayloadPairing` 删 10 侧豁免(全等比较) | **1 红** | `_payloadPairing_fieldwiseIdenticalExceptTenSideOnly` |
| **MUT-D** | `CheckSimReferenceFace` 注掉零引擎检查与报错 | **0 红(存活)** | — ⇒ 由 M-1 修复的阳性对照补上 |
| **MUT-E** | `PrescriptionNamespacePrefix` 换成不存在前缀 | **4 红** | 算法独占 + 病名面三条 |
| **MUT-F** | `CheckDrugPayloadCtorSites` 删下界(白名单须命中) | **1 红** | `_whitelistCannotBeSilentlyGutted` |

⚠️ **MUT-A 的前史(如实记账)**:本轮**修复前**实测踩中同一根因 —— 载荷是 **struct**,C# 发 `call .ctor` **而非** `newobj`,原实现只扫 `newobj` ⇒ 站点集恒空 ⇒ 白名单「零命中」误报,**3 测红**。这是 MUT-A 的**真实事故版**(不是构造出来的变异)。修复 = 双指令收口。

⚠️ **MUT-B / MUT-D 存活是本次评审最实的收获**:两条存活变异分别对应结构侧 B-1 与 M-1,两侧**独立**指向同一结论。

---

## 四 · 验证命令(可证伪)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# ① 本卡过滤套件
unity test unity --mode EditMode \
  --filter "DaYiJingCheng.Tests.PrescriptionMedication" \
  --output unity/Logs/drug_event_3.xml
grep -o 'total="[^"]*" passed="[^"]*" failed="[^"]*"' unity/Logs/drug_event_3.xml
# 期望:total="155" passed="155" failed="0"

# ② 全量 EditMode 回归
unity test unity --mode EditMode --output unity/Logs/full_editmode_story003_fix.xml
grep -o 'total="[^"]*" passed="[^"]*" failed="[^"]*"' unity/Logs/full_editmode_story003_fix.xml

# ③ B-1 坐实(访问器展开生效)
grep -n "AccessorPrefixes" unity/Assets/Editor.Tools.Gates/PrescriptionWriterGates.cs

# ④ M-1 坐实(门已进构建前门)
grep -n "PrescriptionWriterGates.RunAll" unity/Assets/Editor.Tools.Gates/AssemblyGates.cs

# ⑤ M-2 坐实(解码点分离登记 + 背离记账)
grep -n "DecoderCtorSites\|判据-文本背离" unity/Assets/Editor.Tools.Gates/PrescriptionWriterGates.cs

# ⑥ M-3 坐实
grep -n "DiagnosisResult" unity/Assets/Editor.Tools.Gates/PrescriptionWriterGates.cs

# ⑦ 变异残留自检(必须为空)
grep -rn "MUT-" unity/Assets/ --include=*.cs | grep -i prescription
```

**实测结果**:
- ① `drug_event_3.xml` = **155 / 155 passed / 0 failed**(25 测 + 新增 1 单元测 = 本 fixture 26 条)
- ② 见 §五
- ⑦ **目标文件零残留**

⚠️ Unity CLI wrapper 退出码不可靠(Skipped/Inconclusive ⇒ 非零),**判据以 XML 属性为准**。

---

## 五 · 未闭登记(NOT-RUN · 禁借绿)

| 项 | 状态 | 阻塞源 |
|---|---|---|
| **AC-11-22 的「影子装配注入」半边** | 未处置 | 需动态生成程序集落盘,与 EditMode 隔离纪律相抵(见 m-3) |
| **AC-11-22 / TR-prescription-018 的文本收窄** | 未裁定 | 归 producer / TD —— 本件只把背离**变可见**(M-2),不改 AC 文本 |
| **AC-10-06b「漂移 ⇒ 构建失败」的执行体** | 部分闭 | 门已进 `BuildGate`(M-1);但**拒绝表本体**仍归 ADR-014 阶段 2 |
| **`CheckPayloadPairing` 的 tag 序判据** | 未处置 | 现谓词只比声明序名/类型;tag 序漂移**无判据**(m-1) |
| **AC-11-01① 的 `DiagnosisResult` 反射面** | 判据已补,断言待型 | 8 侧未落该类型 ⇒ 当前平凡通过(M-3) |
| **AC-11-15 三格矩阵子句** | NOT-RUN | BLOCKED-BY-ADR-012(文件头已登记) |
| **AC-11-15 跨进程半边** | NOT-RUN | 同上(本件只证同进程逐位) |
| **AC-11-10 的「双路径对拍」子句** | 降级登记 | 两条路径载荷形状不同 ⇒ 降为 AC-10-06b 逐位字段比较(文件头已登记) |
| **AC-11-22 的 7a 存档白名单本体** | NOT-RUN | BLOCKED-BY-7a(本件只证 codec 两支) |

---

## 六 · 结论

**修复轮判定:1 BLOCKING 已闭 · 3 MAJOR 已闭(其中 M-2 为「显式记账」式闭合)· 2 MINOR 中 1 闭 1 保留 · NIT 中 1 闭 3 保留。**

- 本卡**转 Complete 的证据自此齐备**(原件 + 可证伪命令 + 变异坐实);
- **两处变异存活(MUT-B / MUT-D)已由修复轮补上判据** —— 不是「解释掉」;
- **未闭项按原口径保持 NOT-RUN**,`禁借绿`。

**残余风险(显式记账)**:
1. **AC-11-22 与实现的文本背离**仍未裁定(解码点在 11 之外构造载荷)——
   本件只保证它**在构建日志里可见**,不保证它**正确**。
2. **`CheckPayloadPairing` 不覆盖 codec tag 序** —— 两侧声明序**同步漂移**而 tag 不变时谓词仍绿。
3. **反射可达性扫描面窄**(仅 `PrescribeFlow` 的 public static 方法 + 4 个类型)——
   其余 11 类型未纳入反射面(IL 面已覆盖)。
