# 评审原件:story-002 F-53.1 确定性结算(Outcome 枚举)

> **评审类型**:`/code-review`(read-only 评审 · 本文件为 BLOCKING 判定原件)
> **评审日期**:2026-10-15
> **评审对象**:`unity/Assets/Sim/Consequences/{Outcome53,TrialHistoryPredicate}.cs` ·
> `unity/Assets/Editor.Tools.Bake/{OutcomeTableBinder,OutcomeTable53,OutcomeTableCookedWriter}.cs` ·
> `unity/Assets/Editor.Tools.Gates/MedConsFloatGate.cs` ·
> `production/epics/medical-consequences/story-002-deterministic-outcome-judge.md`
> **实现 commit**:`b9f400a`(feat(medcons):S12-9 story-002 Judge)
> **评审裁定**:**CHANGES REQUIRED** —— 3 条 BLOCKING(R-1/R-2/R-3)+ 10 条 WARNING/MINOR(A–J)
> **本原件的效力**:依 `.claude/docs/coding-standards.md`「Review Evidence Standards」,
> 本文件是该 BLOCKING 判定的**唯一可证伪记录**;后续修复轮以本文件的
> **原判定 → 修复落点 → 验证命令**三列为账,逐条结清。

---

## 一、裁定摘要

| 编号 | 级别 | 标题 | 结论 |
|---|---|---|---|
| R-1 | **BLOCKING** | 判定表 cooked 头 schemaVersion 槽硬写 0(架构违约 + 第二真源) | 成立 |
| R-2 | **BLOCKING** | S12-4 假绿夹具强制证伪轮未执行(流程缺口) | 成立 |
| R-3 | **BLOCKING** | AC-53-03 判据被门收窄为「标识符 token」,浮点**字面量**抓不到 | 成立 |
| A | WARNING | 行键编码乘子用逐行量 `uBand+1`,band>1 时键碰撞 + 静默覆写 | 成立 |
| B | WARNING | `JudgeInputs.UBand` 是死字段 —— 查表硬编码 0/1,U 无判别力 | 成立 |
| C | WARNING | `TryLookup` 未命中时吐出 `PatientLives`(结局轴,表内永不出现) | 成立 |
| D | WARNING | 试药史谓词无 patient 作用域 —— 换病人静默丢弃他人 run | 成立 |
| E | MINOR | `u_band` 缺键 ⇒ NRE 而非聚合 `BakeValidationException` | 成立 |
| F | MINOR | 门 `TopDirectoryOnly` 不递归 | 成立 |
| G | MINOR | `BandOf` 除零(`bandCount > 65536`)+ `u_band_count` 无上界 | 成立 |
| H | WARNING | AC-53-12 token 集双真源发散(门 vs 测试互不为超集) | 成立 |
| I | WARNING | `StubProjection` 自造 C 轴命中位 —— AC 语义被测试自造断言顶替 | 成立 |
| J | MINOR | `Bind` ≈110 行,超 40 行标准 | 成立 |

**已核验通过、无需改动**:门 A(`noEngineReferences`)· b2(Sim 引用集恰 = {BCL, Sim.Contracts})·
b3(asmdef 封闭性)· b6(Sim 内零 `new PayloadRef(`)· b5(契约类型禁引用表 10 项)·
ADR-031 `IPayloadDecoder` 用法 · ADR-014 两阶段烘焙形状 · 结局轴两名显式记错(F-53.2)。

---

## 二、BLOCKING 三条:原判定 → 修复落点 → 验证命令

### R-1 判定表 cooked 头 schemaVersion 槽硬写 0

**原判定**(`OutcomeTableCookedWriter.cs:64-77`):

头部 offset 8 硬写 `0`,真值 `bound.SchemaVersion` 却编进**载荷首字段**
(`:32 w.Write(bound.SchemaVersion)`)。三宗罪:

1. **架构违约** —— `CookedFormat` 头契约 = magic / formatVersion / **schemaVersion** /
   configVersion / payloadLength。九个姊妹写方(`DiagnosisReadFloorCookedWriter.cs:60` ·
   `DiseaseActionAxisCookedWriter.cs:77` · `DiagnosisSignCookedWriter.cs:104` ·
   `PrescriptionActionsCookedWriter.cs:77` · `JudgmentLexiconCookedWriter.cs:64` ·
   `InteractionKindCookedWriter.cs:76` ·
   `DiagnosisNegativeConfidenceCookedWriter.cs:64` · `CookedWriter.cs:82`)**全部**写
   `schemaVersion`;53 是唯一写 0 的。
2. **第二真源** —— 同一真值存两处,且读方 `CookedCodec.OpenChecked` 只读头槽
   (`CookedCodec.cs:98-101`,`schemaVersion != CookedFormat.SchemaVersion` ⇒
   `InvalidDataException`,启动期硬失败,E-13 口径)。
3. **注释自证** —— `:59-63` 的 doc 自陈「BindResult 是 struct,本写入器只收
   configVersion」—— 这是**签名设计错误**,不是契约豁免。

**失败场景**:53 产物被任何镜像读取方装载 ⇒ 启动期硬失败。当前 53 无读方
(装载归 story-003 装配轮)⇒ **尚静默;接线即死**。

**修复落点**:`OutcomeTableCookedWriter.cs`
- offset 8 改写 `bound.SchemaVersion`
- **删除载荷首字段** `schema_version`(收敛单一出处 —— 头槽即唯一承载)
- `FinishHeader` 增补 schemaVersion 形参(与九个姊妹写方同形)

**安全性实测**(修复前已核):
- `grep -rn "CookedFormat|DYJC|HeaderSize|schemaVersion" unity/Assets/Tests/` ⇒
  **无任何测试断言 53 的 cooked 布局**;命中仅在
  `interaction_kinds_bake_test.cs:93,100` · `data_pipeline_bake_test.cs:311,324,325,422` ·
  `confidence_leak_test.cs:973,1021`(`bad[8] = 9; // schemaVersion 低字节 → 9` ——
  **反证 offset 8 就是指定 schema 槽**)。
- 源 JSON `schema_version: 1` 恰 = `CookedFormat.SchemaVersion` ⇒ 改后头槽值与契约相符。

**验证命令**(可证伪):
```
grep -n "WriteU32Le(output, 8" unity/Assets/Editor.Tools.Bake/OutcomeTableCookedWriter.cs
# 期望:WriteU32Le(output, 8, schemaVersion);  —— 非 0
grep -c "w.Write(bound.SchemaVersion)" unity/Assets/Editor.Tools.Bake/OutcomeTableCookedWriter.cs
# 期望:0(载荷首字段已删)
```

---

### R-2 S12-4 假绿夹具强制证伪轮未执行

**原判定**:`.claude/skills/story-done/SKILL.md:169` + `:398-402` 明定
**S12-4 Falsifiability Gate 为 BLOCKING 级** —— 「Gate 不通过 ⇒ 不得转 Complete,
哪怕全部 AC 已通过」,并要求每条夹具有「停用生产码 ⇒ 该夹具必须转红」的反证。

实测:
- `grep 反证|证伪 production/epics/medical-consequences/story-002-*.md` ⇒ **0 处**
- 证据原件 `impl-s12-9-medcons-outcome-judge-2026-10-15.md` ⇒ 仅 `:4` 表头套话,
  无任何一条夹具的停用转红记录
- **同 sprint 对照**:S12-6 / S12-12 **均有**反证记录 ⇒ **S12-9 是本 sprint 唯一缺席项**

**修复落点**:本文件 §四(反证表)—— 逐条夹具的「停用 X ⇒ 该夹具红」实证,
**改与跑在同一次 Bash 调用内完成**(trap 纪律,防中途退出留下半停用态)。

**验证命令**:见 §四 每行的 `Falsify` 列。

---

### R-3 AC-53-03 判据被门收窄,浮点**字面量**抓不到

**原判定**:AC-53-03 原文 = 「53 源文件零浮点**字面量**(静态检查入构建)」。
`MedConsFloatGate.FloatTokens` 只含**标识符** token
(`float/double/Single/Double/decimal/Decimal`)⇒ `1.5` / `0.5f` / `3e10` / `2m`
**一个都抓不到**,且该收窄**未登记**。

本仓已有更强先例:`EcozoneIntegerGates`(AC-6-22)走 Mono.Cecil IL 级
(`ForbiddenFloatOpcodes = { Conv_R4, Conv_R8, Ldc_R4, Ldc_R8 }` + `float`/`double`
类型面),且自陈三条判据边界(源 token 不充分 / 常量折叠后可变形貌 / 扫整个命名空间
曾造假红)。

**修复落点**:`MedConsFloatGate.cs`
- 补**源级浮点字面量正则**(三类:定点 `1.5` / 带后缀 `0.5f`·`2m` / 科学计数 `3e10`)
- **如实登记残余缺口**:IL 级 `conv.r4`(常量折叠 / 算术产生的非字面量浮点)**不在**
  源级判据面内 ⇒ 指向 `EcozoneIntegerGates` Cecil 先例为**后续升级路径**,
  并写明升级触发条件(53 数值轮引入任何可能产生浮点的算术时)⇒ **不借绿**

**安全性实测**(修复前已核):浮点字面量命中**仅存在于注释与 `.meta`**
(注释被 `StripComments` 剥离;`.meta` 不被 `*.cs` 匹配)⇒ 加强后现行代码不会误红。

**验证命令**:
```
# 反证:注入一个浮点字面量 ⇒ 门须红(见 §四 R-3 行)
```

---

## 三、WARNING / MINOR 十条:原判定 → 修复落点

### A 行键编码乘子用逐行量 ⇒ band>1 键碰撞

**原判定**(`Outcome53.cs:216`):
```csharp
=> ((((int)p * 3 + (int)c) * 2 + (int)d) * Math.Max(1, uBand + 1) + uBand;
```
乘子是**逐行量** `uBand + 1`,不是**逐表量** `bandCount`。
**实测碰撞**:`bandCount = 2` ⇒ 9 个碰撞键 / 卷入 18 行(总 36 行);
`bandCount = 3` ⇒ 15 键 / 33 行。连带两处:
`OutcomeTable53.cs:86-95 Materialize` 静默覆写(`rows[key] = kv.Value` 无存在性检查);
`OutcomeTableCookedWriter.cs:43-44 Sort` 非稳定排序 ⇒ 同源两次烘焙字节可能不一致。

**修复落点**:`Outcome53.cs` 键式乘子 `uBand+1` → `bandCount`
(即 `key = pcd × bandCount + uBand`);`RowKey` / `RowKeyOf` / `TryLookup` 增
`bandCount` 形参;`Materialize` 与 CookedWriter 三处调用点同步。
**对 `bandCount = 1` 向后兼容**(现网配置字节不变)。CookedWriter 改**全序比较器**
(键 → P → C → D → UBand 逐级破平)⇒ 确定性**不依赖** `List.Sort` 稳定性。

### B `UBand` 死字段

**原判定**:`Judge.Evaluate`(`:252`)只调 `TryLookup(inputs.P, inputs.C, inputs.D, …)`
—— `inputs.UBand` **从未被消费**;`TryLookup` 内部硬编码 `RowKey(..., 0)` 与
`RowKey(..., 1)`。连带 `test_ac5302` 对 U 无判别力(现网 `UBandCount = 1` ⇒ 恒 0)。

**修复落点**:`TryLookup` 收 `uBand` 形参(去硬编码 0/1);`Evaluate` 传
`inputs.UBand`。登记「**U 判别力载体 = `band_count > 1`,归数值轮**」。

### C `TryLookup` 吐出 `PatientLives`

**原判定**(`Outcome53.cs:210-211`):`_rows.TryGetValue(...)` 两次未命中时,
`out` 形参被覆写为 `default(Outcome)` = **`PatientLives(0)`** —— 枚举注释自陈
「表内永不出现」的结局轴成员。随 B 一并简化(显式 default 回落置于 `||` 链之后)。

### D 试药史谓词无 patient 作用域

**原判定**(`TrialHistoryPredicate.cs:48-74`):单游标 `run` / `runPatient`。
换病人 ⇒ `run = 1` ⇒ **A 病人在跑的 run 被 B 病人静默丢弃**。
GDD 规则三原文引证:「试药序 = **同一 patient 的**连续 `DrugTreatmentApplied` 序列、
无 `JudgmentRecorded` 支撑」。附带 `decoder` 形参断言非空后**再未使用**(死依赖)。

**修复落点**:改**逐病人** `Dictionary<int,int> runByPatient`
(Drug ⇒ 该病人 +1;该病人 Judgment ⇒ 归 0;他人事件不触碰)+ **删除未使用的
`decoder` 形参**。

### E `u_band` 缺键 ⇒ NRE

**原判定**(`OutcomeTableBinder.cs:148-150`):
```csharp
if (!row.TryGet("u_band", out JsonNode bandNode)) bandNode = default;
```
`JsonNode` 是 class ⇒ `default` = null ⇒ `ReadUBand` 内 `node.Kind` **NRE**,
而非聚合 `BakeValidationException`。对照 `:120` 与 `:198` 均用
`!row.TryGet(...) || n.Kind` 短路先挡。

**修复落点**:`u_band` 改**必填**(缺键 / 非整数 ⇒ `errors.Add` + `continue`),
与该绑定器「拼写错误会静默丢字段 ⇒ 硬失败」一贯口径一致。
**实测**:源 JSON 19 行已全带该键 ⇒ 无需改数据。顺带删 `ReadUBand` 未用的 `row` 形参。

### F 门不递归

**原判定**(`MedConsFloatGate.cs:59`):`SearchOption.TopDirectoryOnly`
—— 53 实现面若分子目录即漏扫。

**修复落点**:改 `SearchOption.AllDirectories`。

### G `BandOf` 除零 + `u_band_count` 无上界

**原判定**(`Outcome53.cs:171`):`ulong span = (ulong)(65536L / bandCount);`
`bandCount > 65536` ⇒ `span == 0` ⇒ `DivideByZeroException`。
`OutcomeTableBinder.cs:247 ReadUBandCount` 只校验 `n.Int < 1`,**无上界**。

**修复落点**:`BandOf` 补 `if (span == 0) return 0;`;
`ReadUBandCount` 补上界拒收(raw 域 16 位 ⇒ `n.Int > 65536` 硬失败)。

### H AC-53-12 token 集双真源发散

**原判定**:门的 token 集 = `{ Reputation, Trust, Favor, Renown, Standing, Prestige }`
+ `{ ScoreAccum, ScorePool, CreditScore }`,`text.Contains(tok)`(序数、大小写敏感);
测试 `test_ac5312_noReputationSymbols_inConsequencesNamespace` 的 token 集 =
`{ Reputation, Trust, Favor, Score, Points, Credit }` + `OrdinalIgnoreCase`。
**两个方向都不互为超集** ⇒ 同一 AC 两套判据,可各绿而语义分叉。

**修复落点**:token 单一真源**留在 `MedConsFloatGate`**(它在被扫目录之外 ⇒ 不会自触门);
测试改读门的 `ReputationTokens` / `ScoreTokens` 数组而非自带字面量;
匹配口径统一为 `\b{tok}\b` + `RegexOptions.IgnoreCase`。
**实测**:`Assets/Sim/Consequences/` 对声誉/累积分 token 大小写不敏感**零命中**
⇒ 加强后现行代码不会误红。
**⚠️ 返工修正(侦察错误,2026-10-15)**:原判词写「`EditMode.asmdef` references 增
`"Editor.Tools.Gates"`」—— **该条不成立**。实测 `EditMode.asmdef` 早已以 **GUID
`09b3130c3cce749c78793250d6d191f4`** 引用 `Editor.Tools.Gates`
(`unity/Assets/Editor.Tools.Gates/Editor.Tools.Gates.asmdef.meta` 的 guid 字段);
按名补一条反而触发 `Assembly has duplicate references` **编译红**(已实测一次)。
⇒ **H 的实际改动只有测试侧读门数组**,asmdef 一字未动。原判词据的是「只读名称列表、
漏看 GUID 形引用」的不完整侦察 —— 判据本身(H 双真源发散)成立,落点判断有误。

### I `StubProjection` 自造 C 轴命中位

**原判定**:测试用自造 stub 产出 C 轴命中位,使 AC-53-08 的「D 输入仅经病程投影 DTO 的
`treatable_by` 命中位」在 story-002 内**没有真实接线载体** —— AC 语义被测试自造断言顶替。

**修复落点**:在 story 如实登记 AC-53-08 的 D 输入接线在 002 仅为**类型面**,
端到端归装配轮 / story-003;**不**以 stub 充当已验证。

### J `Bind` 超长

**原判定**:`OutcomeTableBinder.Bind` 本体 ≈110 行,超 coding-standards 40 行标准。

**修复落点**:拆 `ParseRows` / `ValidateCoverage`。

---

## 四、反证表(改与跑在同一次 Bash 调用内 · trap 纪律)

> 每条 = 一处修复的可证伪探针。`Falsify` 列记录「停用 X ⇒ 该夹具转红」的**实证结果**,
> 由修复轮回填。未回填 = 该条**未证伪**,不得据此声称 Gate 通过。

| # | 夹具 | 对应 | 停用 X ⇒ 须转红 | 验证命令 | Falsify |
|---|---|---|---|---|---|
| F1 | `test_bake_cookedHeader_schemaVersionEchoesBound` | R-1 | offset 8 改回 0 | EditMode `--filter test_bake_cookedHeader_schemaVersionEchoesBound` | ✅ **RED** |
| F2 | `test_bake_cookedBytes_deterministic_sameSourceTwice` | A | 排序比较器改为非稳定 `List.Sort` | 同上 | ✅ **RED*** |
| F3 | `test_rowKey_noCollision_acrossBands` | A | 乘子改回 `uBand+1` | 同上 | ✅ **RED** |
| F4 | `test_table_uBandDiscriminates_whenBandCountGtOne` | B | `Evaluate` 传 0 而非 `inputs.UBand` | 同上 | ✅ **RED** |
| F5 | `test_table_miss_fallsBackToDefault_notLifeAxis` | C | 删显式 default 回落 | 同上 | ✅ **RED** |
| F6 | `test_bandOf_bandCountBeyondRawDomain_doesNotDivideByZero` | G | 删 `span == 0` 守护 | 同上 | ✅ **RED** |
| F7 | `test_oq539_interleavedPatients_doNotContaminateRun` | D | 改回单游标 run | 同上 | ✅ **RED** |
| F8 | `test_ac5301_outcomeAxisNarrowing_tableRejectsLifeAxisRows` | T-2/I | 删构造器拒收 | 同上 | ✅ **RED** |
| F9 | `test_bake_uBandMissingKey_hardFails` | E | `u_band` 改回可选 | 同上 | ✅ **RED** |
| F10 | `test_bake_schemaVersionMismatch_hardFails` | R-4 | `ReadSchemaVersion` 删期望值比对 | 同上 | ✅ **RED** |
| F11 | 门浮点字面量反证 | R-3 | 注入 `1.5` ⇒ 门须红 | `MedConsFloatGate` 负例 | ✅ **RED** |

**纪律**:「改生产码 ⇒ 跑 ⇒ 确认红 ⇒ 还原 ⇒ 跑 ⇒ 确认绿」七步**必须在同一次 Bash
调用内完成**(trap 保证还原),否则视为未证伪。✅ **已执行**:驱动器 `/tmp/s124_falsify.sh`,
每条独立 `trap RETURN` 还原 + 全局快照 `EXIT` 还原作第二保险;
跑完 `grep -rn FZMARK unity/Assets/` = **零残留**。完整输出:`unity/Logs/s12-4-falsify.txt`
(单次 filtered EditMode 运行 ≈ 31 s,11 条合计 ≈ 6 min)。

### ⚠️ F2 的一处如实记录(该夹具对「非稳定排序」子缺陷**无判别力**)

首轮按字面「排序比较器退回单键」突变 F2 ⇒ **GREEN(未证伪)**。原因:A 修复后行键已**互异**,
任何正确的比较排序都产出同一序列 ⇒ 单键比较器同样定序,**不破坏确定性**。

⇒ F2 守的是 ADR-014 §二「同源两次烘焙逐位一致」这一**普遍性质**,不是「非稳定排序」这个
具体子缺陷;后者的判别力由 **F3**(键乘子)承担。

已补跑第二突变「向载荷注入非确定项(`System.Guid.NewGuid().GetHashCode() & 0xFFFF` 进
`row_count`)」⇒ **RED(证伪成功)**。⇒ F2 的证伪成立,但**须用非确定注入**而非退回比较器
(表中 `RED*` 即此注)。

**这条记录的纪律价值**:若照抄「停用 X」的字面描述就断言证伪通过,会得到**假绿**。
S12-4 的「改 ⇒ 跑 ⇒ 判」必须真的跑,不能只改不判。

### ⚠️ F3 首轮 NORUN(驱动器缺陷,非判据缺陷)

首轮 F3 突变串漏了原版行尾的配对 `)`(原版 = `… + uBand);`)⇒ CS1026 编译红 ⇒ 无 XML。
修正突变串后重跑 ⇒ **RED**。登记为**返工期工具错误**,与判据本身无关。

---

## 五、非 BLOCKING 的两处 AC 半缺失(登记,不阻塞)

1. **AC-53-08 缺「引用」半边**:原文「代码面无 37 verdict 读取路径(**引用** + 反射双断言)」
   —— 反射半边在 `test_ac5308` 有载体;**引用半边**(编译期 / 门级)无载体。
2. **「误诊自愈 ⇒ 不产出回响事件」后半句**在 story-002 内无载体
   (回响事件发出归 story-003 `EmitTick` / `ConsequenceResolved` 写流)。

两处均登记为**跨 story 载体**,不在本返工件内造断言顶替。

---

## 六、修复轮结账(由修复轮回填)

| 编号 | 状态 | 落点文件 | 结案证据 |
|---|---|---|---|
| R-1 | ✅ 已落 | 见下行清单 | §四 F1 RED |
| R-2 | ✅ 已落 | 见下行清单 | §四 F1–F11 全 RED |
| R-3 | ✅ 已落 | 见下行清单 | §四 F11 RED + `test_gate_floatLiteralPatterns_*` |
| A | ✅ 已落 | 见下行清单 | §四 F2b / F3 RED |
| B | ✅ 已落 | 见下行清单 | §四 F4 RED |
| C | ✅ 已落 | 见下行清单 | §四 F5 RED |
| D | ✅ 已落 | 见下行清单 | §四 F7 RED |
| E | ✅ 已落 | 见下行清单 | §四 F9 RED |
| F | ✅ 已落 | 见下行清单 | `SearchOption.AllDirectories`(F11 探针覆盖) |
| G | ✅ 已落 | 见下行清单 | §四 F6 RED |
| H | ✅ 已落 | 见下行清单 | `test_ac5312` 改读门数组 |
| I | ✅ 已落 | 见下行清单 | story-002 §返工轮登记 3 |
| J | ✅ 已落 | 见下行清单 | `ParseRows` / `ValidateCoverage` 拆分 |
| R-4 | ✅ 已落 | 见下行清单 | §四 F10 RED |

**落盘文件清单(9 改 + 1 新)**:

| 文件 | 改动项 |
|---|---|
| `unity/Assets/Sim/Consequences/Outcome53.cs` | A / B / C / G / T-2 |
| `unity/Assets/Sim/Consequences/TrialHistoryPredicate.cs` | D |
| `unity/Assets/Editor.Tools.Bake/OutcomeTableCookedWriter.cs` | R-1 / A |
| `unity/Assets/Editor.Tools.Bake/OutcomeTableBinder.cs` | E / G / J / R-4 |
| `unity/Assets/Editor.Tools.Bake/OutcomeTable53.cs` | A |
| `unity/Assets/Editor.Tools.Gates/MedConsFloatGate.cs` | F / R-3 / H |
| `unity/Assets/Tests/EditMode/MedConsequences/medcons_outcome_judge_test.cs` | D 调用点 / H / 11 条新探针 |
| `unity/Assets/Tests/EditMode/EditMode.asmdef` | **未改**(GUID 已覆盖,见 H 条返工修正) |
| `production/epics/medical-consequences/story-002-*.md` | Test Evidence 回填 |
| `production/qa/evidence/review-medcons-story-002-2026-10-15.md` | 本件(新) |

---

## 七、回归基线

| 轮次 | Passed / Failed / Skipped / Inconclusive | 证据 |
|---|---|---|
| 修复前(S12-9 终轮) | 3444 / 3398 / 0 / 46 | `unity/Logs/s12-9-final.xml` |
| 返工迭代 1(rework1) | — / 编译红 / — / — | asmdef 重复引用(侦察错误,见 H 条) |
| 返工迭代 2(rework2) | — / 编译红 / — / — | `Outcome53.cs` 括号少一个(CS1026) |
| 返工迭代 3(rework3) | 3454 / 3407 / 46 / 0 | 1 红 = 新测试自身前置断言写错(`u_band_count` 含子串 `u_band`) |
| 返工迭代 4(rework4) | 3455 / 3408 / 46 / 0 | 1 红 = 新测试目录路径错(`ConsequencesDirRelative` 相对工程根,非 `dataPath`) |
| **返工终轮(rework5)** | **3455 / 3409 / 0 / 46** | `unity/Logs/s12-9-rework5.xml` |
| **证伪轮后复跑(rework6)** | **3455 / 3409 / 0 / 46** | `unity/Logs/s12-9-rework6.xml` |

> **后续(2026-10-16 重评轮)**:见 `review-medcons-story-002-rework-2026-10-16.md` ——
> 重评又发现并修掉 9 条引擎缺陷 + 3 条 QA 补强,补 7 条证伪探针(F12–F17,全 RED);
> 终轮 `3462 / 3416 / 0 / 46`(`unity/Logs/s12-9-rework10.xml`)。
> **J 项在首轮只兑现一半**(拆出的 `ParseRows` 仍 69 行),重评轮补齐后转「已结清」。

rework6 = 证伪轮(11 条突变 + 还原)之后的复跑,确认**突变全部还原干净、终态仍全绿**
(`grep -rn FZMARK unity/Assets/` = 零残留)。

**增量**:+11 测试(10 条返工探针 + 1 条门字面量探针 F11)⇒ **+11 / +11,零回归**。

**禁借绿**:IL2CPP 三格(AC-53-02/15)归 CI 执行,本机 **NOT-RUN**;
本机跑 Mono 侧 + 负夹具。

---

## 八、返工期发现的两处**自身错误**(登记,非原判定)

| # | 错误 | 后果 | 教训 |
|---|---|---|---|
| X-1 | H 判词据「只读 asmdef 名称列表」断言需增引用,漏看 GUID 形引用 | 按名补一条 ⇒ `Assembly has duplicate references` 编译红 | **asmdef 引用面须解析 GUID 到装配名再判**;名称列表不是完整事实 |
| X-2 | `EncodeRowKey` 换乘子时丢一个配对 `)` | CS1026,整条编译链红 | 括号改配平须编译期才可见 —— 已加代码级配平预检 |

两处均在返工迭代 1/2 被 Unity 编译红当场捕获,**未流入终轮**。
