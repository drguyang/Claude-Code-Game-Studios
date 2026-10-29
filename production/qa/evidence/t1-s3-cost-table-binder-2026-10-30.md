# T1 · S3 —— `CostTableBinder` 实现与证伪(2026-10-29 起草 / 2026-10-30 收口)

- **对象**:`ADR-035 §Migration Plan` 的 **S3**(代价表的阶段 2 绑定器)
- **交付**:`unity/Assets/Editor.Tools.Bake/CostTableBinder.cs`(生产)+
  `unity/Assets/Tests/EditMode/MedConsequences/cost_table_binder_test.cs`(19 条夹具)
- **判据来源**:ADR-035 §Decision ② / ③(N-1 · N-3 · N-4′)/ ⑥ / §Risks R2 · R4 · R5 / §Validation V-9 · V-10

---

## 一、交付件

| 文件 | 行数 | 职责 |
|---|---|---|
| `CostTableBinder.cs` | ~575 | 阶段 2 绑定器:**canary(N-4′)** → 根层(词法 / 未知键 / `schema_version`)→ `harm_kind_enabled` / `bands` / `max_severity_band` → 行层(N-1 / N-3 / 外键 / 单射 / 未知键 / 类型)→ **R4** → **V-10 告警** |
| `cost_table_binder_test.cs` | ~300 | **19 条**:13 拒收 / 警示 + 6 反例(防判据过宽) |

**三处设计判断(均写进文件头)**:

1. **两段 JSON 入参**(成本表 + 疾病注册表)—— N-1 / N-3 / V-10 都要读注册表;
   注册表**复用 `DiseaseRegistryBinder`**,不另建第二份读取逻辑。
2. **canary 先于注册表绑定** —— 注册表 binder 对未知键是硬失败,先调它会把 `compounds`
   当一条泛化的「未知键」拦下,丢掉「N-4 该复查了」这个唯一有用的信息。
3. **`harm_kind` 未启用 ⇒ 报错,不静默跳过** —— 静默跳过正是 R4 要防的形态。
   本件第一版确实写成了静默跳过,**自查时抓到并改掉**(见 §三 的 R4 判据调整)。

---

## 二、判据 ↔ 夹具映射

| 判据 | 夹具 | 备注 |
|---|---|---|
| N-1(`lethal == false`) | `test_n1_lethalInjury_isRejected` | 真实注册表(5 件伤情全 lethal) |
| 外键失效 | `test_unknownInjuryId_isRejected` | |
| 落点须是**伤情** | `test_n3_diseaseRowId_isRejected` | 用 `DIS_MEASLES`(**lethal=false 的病种**)—— 专防「只查 lethal 不查 kind」的过宽实现 |
| N-3(结局轴符号) | `test_n3_outcomeAxisSymbol_isRejected` | `patient_lives` / `PatientLoses` 两形态 |
| **N-4′ canary** | `test_n4prime_compoundsKeyInRegistry_isRejected_pointingToReview` | 正向;报文须含 `N-4` + `复查` |
| canary 不误伤 | `test_n4prime_absentCompounds_doesNotFire` | 反向 |
| R4(空表窗口) | `test_r4_rowsNonEmptyButEnabledEmpty_isRejected` | |
| 逐行启用闸 | `test_rowKindNotEnabled_isRejected_notSilentlySkipped` | |
| §⑥(overdose 挂起) | `test_overdoseEnabled_isRejected` | 报文须点名 `TD-sim-02` |
| 单射 | `test_duplicateKindBand_isRejected` | |
| 未知键(根 / 行) | `test_unknownRootKey_isRejected` / `test_unknownRowKey_isRejected` | |
| 零 float | `test_floatSeverityBand_isRejected` | `0.5` ⇒ Float token 结构性拒收 |
| `schema_version` | `test_schemaVersionMismatch_isRejected` | 与 `CookedFormat.SchemaVersion` 比对 |
| **V-10 告警** | `test_realSkeleton_binds_andWarnsNoNonLethalInjury` | 真实两张表:零行 + 告警点名 `TD-sim-03` + 含「无输入」字样 |
| 反例(不过宽) | `test_emptyEverything_isLegal` · `test_happyPath_…` · `test_sameInjuryOnDifferentBands_isLegal` · `test_maxSeverityBandZero_isLegal` | 4 条 |

---

## 三、测试执行:四轮,其中**前两轮的全绿是假的**

| 轮 | 命令 | 退出码 | XML | 实况 | 归因 |
|---|---|---|---|---|---|
| ① | 带 `-quit` | **0** | ❌ 无 | **未跑** | 编译错 `CS0019`(`int` 与 `DiseaseKind` 直接比较) |
| ② | 修错后**仍带 `-quit`** | **0** | ❌ 无 | **未跑** | `-quit` 让 Unity 在导入/编译完成后即退出,**测试从未开始** |
| ③ | 去掉 `-quit` | 0 | ✅ | **19 跑 / 13 过 / 6 红** | 见下 |
| ④ | 修夹具后 | 0 | ✅ | **19 / 19 全绿** | 收口态 |

> ⚠️ **两次 exit 0 都不是通过**。这与仓库既有教训(`CLAUDE.md` §构建前门「绿 = 没报错,不等于门跑了」)
> 逐字同型:判据必须是 **XML 存在 + 计数**,不是退出码。**本轮已按此判**,两次假绿都是当场抓到的。

### 第 ③ 轮的 6 红,分两类

**A 类(5 条)—— 夹具假设错了,而且是真发现**:
把 `INJ_HEMORRHAGE` 的 `lethal` 由 `true` 翻成 `false` 后,`DiseaseRegistryBinder` **在绑定时就拒**:

```text
R1 校验:R1-22: 急性保持型(self_limit=false ∧ plateau=false)仅当 lethal=true
(GDD 校验 4;4b 需 DEATH_THRESHOLD,值待定未实现)
```

⇒ **注册表现存 5 件伤情全是急性保持型,按当前校验它们在 schema 上根本不允许是非致死的。**
这条改变了 `TD-sim-03` 的性质 —— 见 §四。

**B 类(1 条)—— 我自己写错**:
`CostTable(rows: "[]")` 的拼接把空数组包成了 `[[]]`,导致「全空合法」的反例夹具构造出
`rows[0]` 是数组 ⇒ 报「须为对象」。夹具辅助函数的默认值已改为 `""`(拼接后才是 `[]`)。

**夹具修正**:正向夹具改为 **`lethal:false` ∧ `self_limit:true`**(schema 允许的「自限型非致死伤情」),
而不是把一条会被 R1-22 拒的行当「合法行」硬塞。

---

## 四、⚠️ 实测新发现:`TD-sim-03` 的性质要订正

| 原表述(2026-10-29 立债时) | 实测后应当读成 |
|---|---|
| 「注册表**零** `lethal=false` 伤情 ⇒ 53 代价表 `injury_id` 一列无可填」 | **同结论,但性质更重**:不是「没建行」,而是**当前 R1-22 校验禁止急性保持型伤情非致死** ⇒ 要造出合法落点,9 必须新增一件**自限型或平台型**的非致死伤情(或另裁 R1-22) |

**证据**:§三 第 ③ 轮 A 类 5 红的报文;正向夹具在**同时**翻 `self_limit` 后通过(第 ④ 轮)。

⇒ `TD-sim-03` 的「何时消解」须据此改写(已在债册就地加注,**原文不删**)。

---

## 五、证伪轮(S12-4 · BLOCKING)

纪律:变异与跑测**在同一 bash 命令内**完成(改 → 跑 → 还原);还原判据 = **SHA-256 逐字节**;
窗口内**未编辑任何文件**。改前哈希:

```text
b10bcdaae8550dee60324225a0db100a7a4d6c1735e95f8f0a92a7d438f94d0a  CostTableBinder.cs
fd3bc09b2469a3560471ed99b090bc23f962d0ad6e7b43f931cd790507d01ee4  cost_table_binder_test.cs
```

| 轮 | 变异 | 预判红 | **实测红(恰好)** | 还原 |
|---|---|---|---|---|
| **A** | M1 `if (lethality != 0)` → `… && false`(停 N-1)· M2 `if (kind != (int)DiseaseKind.Injury)` → `… && false`(停 kind 闸) | 2 | **2**:`n1_lethalInjury` · `n3_diseaseRowId` | ✅ `sha256sum -c` |
| **B** | M3 `if (registry.HasNonLethalInjury()) return;` → `if (true) return;`(停 V-10 告警)· M4 canary 扫描循环 `i < Items.Count` → `i < 0`(停 canary) | 2 | **2**:`realSkeleton_…` · `n4prime_compoundsKey…` | ✅ |
| **C** | M5 R4 判据前加 `false &&`(停 R4)· M6 删掉逐行「未启用」的 `errors.Add`(停该闸) | 2 | **2**:`r4_rowsNonEmpty…` · `rowKindNotEnabled_…` | ✅ |

**每组只红预期的两条,不多不少** —— 说明六条判据各自被**独立**钉住,不存在「一条夹具顺带替另一条背书」。

> ⚠️ **未做变异的两处(如实登记)**:`test_unknownRootKey` / `test_unknownRowKey` 共用
> `RejectUnknownKeys`(早已被其他 binder 覆盖);`test_floatSeverityBand` 依赖词法层 token 种类。
> 两者都不在本件新增的判据面上,**不主张它们经过了本轮的变异**。

---

## 六、`ADR-035` 判据的落点(逐条)

| 判据 | 本轮状态 |
|---|---|
| **V-9**(canary 有判别力) | ✅ **已证**:`n4prime_compoundsKey…`(注入即红)+ `n4prime_absentCompounds…`(不动即绿)+ M4(停用 canary ⇒ 红) |
| **V-10**(零非致死伤情须告警) | ✅ **已证**:`realSkeleton_…`(告警点名 `TD-sim-03` 且含「无输入」)+ M3(停用 ⇒ 红) |
| **V-5**(N-1 变异 ⇒ **构建 Failure**) | ⚠️ **只在绑定器层证了**(M1 ⇒ 红),**未证「构建失败」** —— 本表**没有 CookedWriter、没有 `DataBakeMenu` 入口**(T1 形状件的既定形态)⇒ 今天不存在「构建」这条路径。**不得记作 V-5 已过** |
| V-1 / V-2 / V-3 / V-4 / V-6 / V-7 / V-8 | ❌ 未跑(分别属 S5 / S6 / 构建期) |

---

## 七、本件**不**主张

| 项 | 状态 |
|---|---|
| `53_cost_table.json` 已被门保护 | ⚠️ **只在单元层**:没有任何烘焙入口调用本绑定器(`data-files.md` 的孤儿偏离仍在,消解点未变) |
| 「永不致死」已获证明 | ❌ **没有** —— `N-1` 的可行集今日为空(V-10 告警即为此而设);证明要等 `TD-sim-03` |
| V-5 已过 | ❌ 见 §六 |
| 本表真跑过 | ❌ `rows` 恒空;真实数据路径一次没走过 |
| 六条判据之外的部分也证伪过 | ❌ 见 §五 末段 |
