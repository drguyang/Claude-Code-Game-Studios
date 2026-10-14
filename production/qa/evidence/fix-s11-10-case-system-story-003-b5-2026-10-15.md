# 修复证据原件 —— S11-10 / case-system story-003 B5(处置集错收 `InjuryOnset`)+ 顺批 A-1

**日期**:2026-10-15
**对象**:`unity/Assets/Sim/CaseCloseDecider.cs`(`TreatmentKinds`)+
`unity/Assets/Tests/EditMode/CaseSystem/case_close_precondition_test.cs`
**来源判定**:`production/qa/evidence/review-case-system-story-003-retro-2026-10-15.md` §二 **B5**(BLOCKING)
+ 同件 **ADVISORY A-1**(顺批,与 B5 同址)
**排期**:`production/sprints/sprint-11.md` **S11-10**(用户裁定「必须项优先」并入;A-1 顺批经用户裁可)
**性质**:本件 = **修复证据**,**不是评审**(承 `.claude/docs/review-workflow.md` §义务 3 不可追补)。

> ⚠️ 承 S11-8 的同一纪律:原始 XML 落在 `unity/Logs/`(**`.gitignore:196`**)⇒ **易失中间产物**,
> 不得充当交付证据。故判据、命令、输出原文收进本件,使其**自洽可复核**。

---

## 一、原缺陷判定(逐字承补评件 §二 B5)

37 实现把 **`InjuryOnset` 收进了处置集**(`Sim/CaseCloseDecider.cs:72-77`),
而它是 **25 的战斗伤害结算** ⇒ **「病人挨了打」被计入「已被处置」**
—— 玩家把病人**打到**即可满足结案前置,**正面击穿** `case-system.md:567`/`:839`
「玩家空手连关三例 ⇒ **不可能**」(支柱四的机制落点)。同集又**正确排除**了
`EmergencyAttempt`(对),却**未收** `CareApplied`(8 的护理)—— **取舍方向与语义完全相反**。

**病因(不是「写错了」,是「无权威可依」)**:`adr-008` §四、`case-system.md` F-37.2 `:666`/`:679`、
规则六 `:254`、`tr-registry.yaml` **四处皆不枚举**处置 `Kind` ⇒ 实施侧无权威可依,
遂自行其是。story-003 自身的 Implementation Note #1 早已明令「不自行扩集」,
**但因为当时没有可依之物,该纪律落不了地**。

**权威已于 2026-10-15 补立**(`adr-008` §四 `:216-249`,该补立**先行于**本修复):

```
treatment event := e.Kind ∈ { EmergencyTreatmentApplied, DrugTreatmentApplied, CareApplied }
```

| Kind | 写者 | 收录理据 |
|---|---|---|
| `EmergencyTreatmentApplied` | 10 | 急救动作**已施加**(ADR-009 Amendment I) |
| `DrugTreatmentApplied` | 11 | 用药**已施加** |
| `CareApplied` | 8 | 护理动作(病史流)——「照顾」是玩家对病人做的**处置**,代价最轻的一档 |

**明确排除**:`EmergencyAttempt`(判定输入 ≠ 结算)· `InjuryOnset`(伤害 ≠ 治疗)。

---

## 二、修复落点

### 2.1 代码(`TreatmentKinds`)

| # | 落点 | 修前 | 修后 |
|---|------|------|------|
| ① | 处置集成员 | `{Emergency, Drug, **InjuryOnset**}` | `{Emergency, Drug, **CareApplied**}` |
| ② | 集合暴露面(**A-1**) | `public static readonly HashSet<EventKind> Set` | `private static readonly HashSet<EventKind> Members` + `Contains` 出口 + **`MembersSnapshot()` 返独立副本** |

**② 的理由(A-1)**:`readonly` 只锁**引用**,集合**内容全仓可写** —— 任一处
`TreatmentKinds.Set.Add(...)` 即**静默扩集**,破坏 ADR-005 的确定性纪律。
`private` 真源 + 返**拷贝** ⇒ 调用方**改不动**真源。

> ⚠️ 实际污染面:全仓 grep 确认 `TreatmentKinds.Set` **当时无外部写点**
> (生产侧唯一读点 = `CaseCloseDecider.cs:118` 的 `Contains`;测试侧只用 `Contains`)。
> ⇒ A-1 是**未被触发的**隐患,**不是已发生的故障** —— 此点如实登记,不夸大。

### 2.2 权威对齐(闭环纪律四处已核,ADR-008 §四 `:244-249` 明令)

| 处 | 行 | 状态 |
|---|---|---|
| `docs/architecture/adr-008-case-event-stream.md` | §四 `:229`(定义)+ `:232-236`(写者表) | ✅ 权威 |
| `design/gdd/case-system.md` 规则六 | `:255` | ✅ 一致 |
| `design/gdd/case-system.md` 规则八 | `:283` | ✅ 一致 |
| `design/gdd/case-system.md` F-37.2 | `:671` / `:674` / `:686` / `:709-736` | ✅ 一致 |

四处均写 `{EmergencyTreatmentApplied, DrugTreatmentApplied, CareApplied}` 且均排除
`EmergencyAttempt` 与 `InjuryOnset` ⇒ **代码现已等于该枚举**。
(`tr-registry.yaml` 不枚举处置集,**不属**闭环纪律点名的四处。)

> ⚠️ **核验期附带发现(2026-10-15 提交前复核)—— `case-system.md` 的行号自引成片漂移**:
>
> | 自引处 | 它写的 | **实测** |
> |---|---|---|
> | `:738`(F-37.2 闭环纪律行) | `本处 :666/:679` · 规则六 `:254` · 规则八 `:280` | **`:671`+`:674`+`:686`** · **`:255`** · **`:283`** |
> | `:718`(F-37.2 订正框) | `§EC-37-1 :839 / :567` | 实际在 **`:570`**(F-37.1 边界表)与 **`:880`**(**EC-37-3** 表 —— 且**不是** EC-37-1,后者在 `:856`) |
>
> **本件 §2.2 表内行号是当日实测值(正确)**;代码注释初稿照抄了 GDD 的自引旧号 ⇒
> **已改为去编号引用**(节名稳定、行号不稳)。**GDD `:738`/`:718` 两处自身的订正不在本件范围**
> —— 需 GDD 侧写入授权,已单独登记提请用户裁。

### 2.3 测试(`case_close_precondition_test.cs`,37 → 39)

1. **撤** `test_treatmentKinds_containsInjuryOnset` —— 该测**反向断言了缺陷**
   (`Contains(InjuryOnset) is True`,`:306-312`),是 B5 逃过测试面的**直接原因**;
2. **新增** `containsCareApplied` · `excludesInjuryOnset` · `excludesEmergencyAttempt`
   (末者守住**原有的正确排除**,防回归);
3. **双向穷尽** `equalsAuthorityExactly` —— 期望值**逐字引自 `adr-008` §四**
   (**不引生产代码默认值**,S11-10 AC 的兑现),断言生产集 **恰等于**权威枚举;
   单向 `Contains` **抓不到**「偷偷多收一员」,而 B5 正是**错收**这样逃过去的
   ⇒ 本测是「处置集 == 权威」的**充要**判据;
4. `snapshotIsIsolatedFromSource` —— 污染快照后真源须纹丝不动(**A-1 的经验判据**)。

---

## 三、验证命令(可证伪 · 逐字可复跑)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
U=/home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity
F=DaYiJingCheng.Tests.CaseSystem.CaseClosePreconditionTest

# ── ① fixture(红 → 绿)───────────────────────────────────────────────
"$U" -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode \
  -testFilter $F -testResults "$PWD/unity/Logs/s11_s10_after.txt" -logFile unity/Logs/s11_s10_after.log

# ── ② 全量双平台(零回归)─────────────────────────────────────────────
"$U" -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode \
  -testResults "$PWD/unity/Logs/s11_s10_editmode.xml" -logFile unity/Logs/s11_s10_editmode.log
"$U" -batchmode -nographics -projectPath unity -runTests -testPlatform PlayMode \
  -testResults "$PWD/unity/Logs/s11_s10_playmode.xml" -logFile unity/Logs/s11_s10_playmode.log

# ── ③ 回退成员表即转红(精确隔离「集合内容」这一变量)────────────────
cp unity/Assets/Sim/CaseCloseDecider.cs /tmp/s11_s10_fixed.cs
#   只把 `EventKind.CareApplied,` 改回 `EventKind.InjuryOnset,`
#   ⚠️ 只回退**成员表**,保留 `MembersSnapshot()` ——
#      若整段回退到 HEAD,`MembersSnapshot` 不存在 ⇒ **编译失败** ⇒
#      夹具一条不跑,红/绿失去可比性(这一点是刻意的)
sed -i 's|EventKind\.CareApplied,                // 8  护理动作(病史流)|EventKind.InjuryOnset,                // 25 格斗(旧实现 —— 反证用)|' \
  unity/Assets/Sim/CaseCloseDecider.cs
"$U" -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode \
  -testFilter $F -testResults "$PWD/unity/Logs/s11_s10_revert.txt" -logFile unity/Logs/s11_s10_revert.log
cp /tmp/s11_s10_fixed.cs unity/Assets/Sim/CaseCloseDecider.cs   # 还原;diff -q 须一致
```

判决口径 = **退出码 0 且 XML `failed="0"`** 双信号一致。

---

## 四、结果

| # | 状态 | 退出码 | 结果 | 信号一致? |
|---|------|--------|------|-----------|
| ① | **修复前**(旧成员表 + 新哨兵) | **2** | `total=37 passed=35 **failed=2**` | ✅ 双信号均红 |
| ② | **修复后** | **0** | `total=39 passed=**39** failed=0` | ✅ 双信号均绿 |
| ③ | **回退成员表** | **2** | `total=39 passed=35 **failed=4**` | ✅ 双信号均红 |

### ① 修复前 —— 恰 2 红,即我**先证伪、后修**的两条

```
[Failed] test_treatmentKinds_containsCareApplied
  处置集须含 CareApplied(8 护理)……必须收(ADR-008 §四 :236)
  Expected: True          But was:  False        ← 漏收
[Failed] test_treatmentKinds_excludesInjuryOnset
  InjuryOnset 是 25 的战斗伤害结算……(ADR-008 §四 :241-242)
  Expected: False         But was:  True         ← 错收
```

`test_treatmentKinds_excludesEmergencyAttempt` **通过** ⇒ **原有的正确排除未被本次改动波及**。

### ③ 回退成员表 —— 4 红,恰为全部集合判据

```
test_treatmentKinds_containsCareApplied          Expected: True   But was: False
test_treatmentKinds_excludesInjuryOnset          Expected: False  But was: True
test_treatmentKinds_snapshotIsIsolatedFromSource Expected: False  But was: True
test_treatmentKinds_equalsAuthorityExactly
  处置集须**恰等于** ADR-008 §四 的权威枚举 ——
  实际 = {EmergencyTreatmentApplied, DrugTreatmentApplied, InjuryOnset};
  权威 = {EmergencyTreatmentApplied, DrugTreatmentApplied, CareApplied}
```

★ 末条的失败消息**直接打印出两个集合**,是本件最强的可证伪信号。

### ④ 全量零回归

| 套件 | 结果 | 退出码 | 对比 |
|------|------|--------|------|
| EditMode | `total=3334 passed=3288 failed=0 inconclusive=0 skipped=46` | **0** | 较 S11-8 后 `3330/3284` ⇒ **+4** |
| PlayMode | `total=186 passed=186 failed=0` | **0** | `story009_review_playmode.xml` = `186/186/0` ⇒ **逐位相同(M2 链零回退)** |

**EditMode 增量的逐名归因**(`comm` 双向比对 `fullname`,零意外消失):

- **+5 新增**(全部属本 story 夹具)
  `test_treatmentKinds_{containsCareApplied, equalsAuthorityExactly, excludesEmergencyAttempt,
  excludesInjuryOnset, snapshotIsIsolatedFromSource}`
- **−1 移除**:`test_treatmentKinds_containsInjuryOnset` —— **反向断言缺陷的那条**,刻意撤除

⇒ 净 **+4**,与 `3330 → 3334` 相符。

### ⑤ 静态面

`grep -c "error CS"` = **0**。`Sim` 引用集**一字未动**(门 A / b2 / ADR-025 §① / ADR-029 全部继续成立);
`TreatmentKinds` 的新增出口 `MembersSnapshot()` 只依赖 BCL(`System.Collections.Generic`)。

### ⑥ 提交前复核(2026-10-15)

因 §2.2 附带发现,`TreatmentKinds` 的文档注释由**行号引用**改为**节名引用**(纯注释改动,
零行为影响)。改后**重跑夹具**确认未破坏编译:`39/39 passed · 0 failed · 退出码 0 · error CS = 0`
(`Logs/s11_s10_verify.txt`)。⇒ §四 ①②③ 三行的结论**继续有效**。

---

## 五、未清项与一处结构缺口

- **story-003 维持 `In Review`** —— 补评 4 类中余 **② 快照半场**(写者未落 ⇒ **无载体可测**)·
  **③ AC-37-18 恒真化**(A-4);ADVISORY **A-5**(结案副作用序列 →8/→53 无判据)亦未清。
  (① B-1 归 **S11-8**;⑤ B5 + A-1 归本件。)
- ⚠️ **结构缺口(登记,归用户裁)**:B5 是 **BLOCKING 级**判定,却**不对应任何 `AC-37-xx`**
  —— GDD 36 条 AC **无一条枚举处置集**。故该缺陷的守卫**只存在于测试件 + ADR 闭环纪律**,
  **不在 AC 面上**。是否补一条显式 AC(或挂到支柱四不变量名下)归用户;本轮**不自行加 AC**。
- **A-1 的登记口径提醒**:实测 `TreatmentKinds.Set` **无外部写点** ⇒ A-1 是**未被触发的**隐患,
  已按此如实登记(见 §2.1 ② 的注)。
