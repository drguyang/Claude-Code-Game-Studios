# T1 · S4 —— `CostTable53` 只读视图(2026-10-30)

- **对象**:`ADR-035 §Migration Plan` 的 **S4**;对应 §Decision ③ 的 **L3 运行期纵深防御**
- **交付**:`unity/Assets/Sim/Consequences/CostTable53.cs` + `unity/Assets/Sim.Contracts/CostHarmKind.cs`
  + `unity/Assets/Tests/EditMode/MedConsequences/cost_table53_test.cs`(**13 条**)
- **形态先例**:`Sim/Consequences/DelayTable53.cs`(构建期主校验 + Sim 侧复述拒收的「双点」形态)

---

## 一、交付件与三处设计判断

| 件 | 内容 |
|---|---|
| `CostHarmKind`(Sim.Contracts) | `Contraindication = 0` / `Overdose = 1`。**住 Contracts 不随机**:S5 的载荷 `TreatmentHarmOnset` 也要用同一个枚举(载荷 struct 住 Contracts,而 Sim 引用 Contracts)⇒ 放 Sim 会让载荷侧够不着,逼出第二份枚举。**成员只许追加**(ordinal 是落盘格式的一部分)。 |
| `CostTable53`(Sim) | `Row` / `Table{IsEmpty, RowCount, TryLookup}` / `CreateTable`。构造期**拷贝**行集(同 `DelayTable53` 口径:防外部集合事后改写绕过校验)。 |
| `cost_table53_test` | 13 条:10 拒收 + 3 反例(空表合法 / 同伤情异档合法 / 构造期拷贝有效)。 |

**判断 ①:L3 也查 `lethal`(比 §Key Interfaces 的草稿多一条)。**
`§Decision ③` 的 L3 原文是「**复述同一拒收**」,而 `§Key Interfaces` 的草稿只列了三种
(结局轴符号 / 不在 9 闭集 / 非单射)。若构造器不查 `lethal`,`绕过 Bake 直构` 就能把**致死伤情**
塞进表里 ⇒「永不致死」只剩构建期一条路径在守。**按「同一拒收」执行,不按草稿的最小集**。

**判断 ②:`N-3`(结局轴**符号**)在整数域退化为「id 不在注册表内」。**
运行期只认整数 id;作者态字符串 → 整数的转换是烘焙期职责 ⇒ 符号级判据在运行期没有独立形态,
同一个拒收由外键检查承担。**不另设一条同义分支**(否则两条判据互相冒充)。

**判断 ③:过量行**不是**静默忽略,是拒收。**
`harm_kind = Overdose` 的行在构造期直接 `throw`(§⑥ 挂起),报文点名 `TD-sim-02`。
**静默忽略会让「表里装了行但永不生效」重演**,那正是 W-4 / R4 的形态。

---

## 二、判据 ↔ 夹具

| 判据 | 夹具 |
|---|---|
| 空表合法(惰性,连注册表都可以没有) | `test_emptyTable_isEmpty_andLookupMisses` |
| 合法行可查 | `test_legalRow_bindsAndLooksUp` |
| **N-1**(`lethal == false`) | `test_n1_lethalInjury_isRejected` |
| 外键存在 | `test_unknownInjuryId_isRejected` |
| 落点须是**伤情** | `test_diseaseRowId_isRejected`(用 `lethal=false` 的**病种**当诱饵) |
| **§⑥** 过量支挂起 | `test_overdoseKind_isRejected`(报文须点名 `TD-sim-02`) |
| 闭集 | `test_unknownHarmKind_isRejected` |
| 单射 | `test_duplicateKindBand_isRejected` |
| 负档序数 | `test_negativeBand_isRejected` |
| 程度档上界 | `test_severityAboveMax_isRejected` |
| 有行无注册表 | `test_rowsWithoutRegistry_isRejected`(`ArgumentNullException`) |
| 反例(防过宽) | `test_sameInjuryOnDifferentBands_isLegal` · `test_ctorCopiesRows_externalMutationDoesNotAffect` |

---

## 三、测试执行

| 轮 | 退出码 | XML | 实况 |
|---|---|---|---|
| ① | 0 | ❌ | **未跑** —— 编译错 `CS0103`(`CostTable53.cs` 漏 `using DaYiJingCheng.Sim.Contracts;`) |
| ② | 0 | ✅ | **13 / 13 全绿** |

> ⚠️ 又一次「exit 0 + 无 XML」。这是本会话**第三次**同型假绿(前两次在 S3)。
> 判据始终是 **XML 存在 + 计数**,不是退出码。三次都已如实登记。

---

## 四、证伪轮(S12-4 · BLOCKING)

纪律同 S3:变异与跑测同一命令内完成;还原判据 = **SHA-256 逐字节**;窗口内未编辑任何文件。
改前哈希:`26c1f8bb…`(生产) / `ec8313b7…`(夹具)。

| # | 变异 | 预判红 | 实测红 |
|---|---|---|---|
| M1 | `if (e.Lethality != 0)` → `… && false`(停 N-1) | 1 | `test_n1_lethalInjury_isRejected` ✅ |
| M2 | `if (e.Kind != DiseaseKind.Injury)` → `… && false`(停 kind 闸) | 1 | `test_diseaseRowId_isRejected` ✅ |
| M3 | §⑥ 的 `throw` → `return;`(停过量闸) | 1 | `test_overdoseKind_isRejected` ✅ |
| M4 | 单射 `if (_rows.ContainsKey(key))` → `if (false && …)` | 1 | `test_duplicateKindBand_isRejected` ✅ |

**结果:13 跑 / 9 过 / 4 红 —— 与预判集合逐一相同,不多不少。** 还原 `sha256sum -c` 逐字节通过。
（M4 的机理值得记一笔:关掉单射后 `Dictionary.Add` 会抛**另一条** `ArgumentException`
⇒ 只断「会抛」的夹具会**假绿**;本夹具断的是报文含「单射」,故仍转红。）

---

## 五、本件**不**主张

| 项 | 状态 |
|---|---|
| 「永不致死」已获证明 | ❌ 没有 —— `N-1` 的可行集今日为空(`TD-sim-03`);本件只保证「**就算有人想塞致死行,也进不来**」 |
| 表已接入运行路径 | ❌ **零读方**:没有 CookedWriter、没有 `DataBakeMenu` 入口、没有装配点调用 `CreateTable` |
| V-5 已过 | ❌ V-5 要的是「**构建** Failure」,本件证的是**绑定器 / 构造器**层拒收 |
| `SeverityBand` 语义已定 | ❌ 只钉「非负 + 不超上界」;`band → magnitude` 的换算归 9(ADR-035 §Implementation 5) |
| 与绑定器判据**逐条**一致 | ⚠️ 差一处:**单射的键域**两侧都用 `(kind, band)` ✓;但绑定器还查**未知键 / 零 float / schema_version**,运行期无对应物(那是作者态的事) |
