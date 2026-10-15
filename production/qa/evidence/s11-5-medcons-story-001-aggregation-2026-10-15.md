# 实现报告原件:S11-5 —— 53 medical-consequences Story 001(入向闭集与跨流聚合)

> **对象**:`production/epics/medical-consequences/story-001-input-closure-and-stream-aggregation.md`
> **日期**:2026-10-15
> **执行**:sprint-11 · **S11-5**(nice-to-have)
> **判定**:story-001 **实现完成** —— 6 条 AC **全部有专测且全绿**,状态 `Ready` → **`In Review`**
> **前提订正**:**任务书的「story 立件」措辞已过时** —— 4 个 story 件早在 2026-09-28 立好且全 `Ready`;
> 缺席的是**实现**(`WriterExistenceGate.cs:117` 具名「53 未实现」)。本轮是**真活**。详见 §二。

---

## 一、原判定(逐字引用,未加工)

`sprint-11.md:144`(**修复前**):

> | S11-5 | **53 medical-consequences 起首 story**(支柱四载体,机械自足 P0 水龙头,纯 C# + SplitMix64,Engine Risk LOW) | Claude | 1-2 | 9+37 已 Complete ✓ | **story 立件** + 判据面成型;不强求当轮完成 | **QA**:随立件定测试路径(Logic 级) |

`story-001-….md:4` / `:96`(**修复前**):

> **Status**: Ready
> …
> **Status**: [ ] Not yet created

`WriterExistenceGate.cs:117`(现状,本件**不改动**):

> `("ConsequenceResolved", "53 医疗后果与责任**未实现** —— 归 53 轮(P0 未排;entities.yaml author 注)(2026-10-10)")`

---

## 二、勘察结论:**立件从未缺席,缺席的是实现**

| 面 | 实测 |
|---|---|
| story 件 | ✅ **4 件齐备**,立件日 **2026-09-28**(`/create-stories`),全部 `Status: Ready` |
| EPIC | ✅ `production/epics/medical-consequences/EPIC.md:6` `Status: Ready`;`:74-77` Stories 表四行全 `Ready` |
| GDD | ✅ `design/gdd/medical-consequences.md`(527 行)规则一~八齐备,F-53.1/2/3 已定 |
| **实现** | ❌ **零** —— `WriterExistenceGate.cs:117` 具名登记「53 未实现」 |
| 依赖 9 / 37 | ✅ 两者已 Complete(37 的 `CaseCloseDecider` / `EventOrder` 可复用) |

⇒ 原判词「**story 立件** + 判据面成型」的**前半已由 2026-09-28 完成** ⇒ 本轮内容 = **实现 story-001**。

### 2.1 ⚠️ 过程面观察:sprint 计划未回扫 epic 现状

本 sprint 的 **should-have(S11-4)与 nice-to-have(S11-5)两项任务书前提均已过时**:

| 任务 | 描述写作日 | 相关产物形成日 | 过时点 |
|---|---|---|---|
| S11-4 | 2026-10-14 | 2026-10-03 / 10-07 | 判为「DoD 残留需实现」,实为**登记回刷** |
| S11-5 | 2026-10-14 | 2026-09-28 | 判为「story 立件」,实为**实现** |

⇒ **sprint 计划时未回扫 epic 现状**。本件如实登记,供 sprint-11 回顾参考(不属本件裁决面)。

---

## 三、实现落点

### 3.1 交付(2 个新文件,两个新目录均为 53 首件)

| 文件 | 内容 |
|---|---|
| `unity/Assets/Sim/Consequences/ConsequenceInputAggregator.cs` | `IDiseaseProjection`(只读边界)· `DiseaseProjectionSnapshot`(占位)· **`Inputs53`**(恰五路)· `ConsequenceInputAggregator.Aggregate` |
| `unity/Assets/Tests/EditMode/MedConsequences/medcons_aggregation_test.cs` | **20 条测**(AC-1~AC-6 全覆盖 + 张力/语义/边界) |

**设计要点**:

- **零载荷解码(门 A 的结构性后果)**:`Sim` 引用集恰 = `{BCL, Sim.Contracts}` ⇒ 够不着 `Sim.Codec`
  ⇒ 只操作 `SimEvent` **头部**(Tick/Patient/Seq/Kind),**从不读 `Payload`**。
  P/C 的**内容**归 story-002。**这不是范围裁剪**,是门 A 使然。
- **置换不变由结构保证**:「先复制 → 全排序 → 后处理」⇒ 输入的任意置换归约到同一有序序列。
  比较体复用 `EventOrder.Compare`(**禁减法**,防 int64 溢出 —— 卡面 §Implementation Notes 2)。
- **P = 最后一笔**:有序扫描 + 逐条覆盖 ⇒ 自然得「结案前最后一笔 `JudgmentRecorded`」。
- **闭集之外零渗入**:第 20 条测对每组的每一路做 Kind ∈ 闭集断言。

### 3.2 与批准草案的**五处偏离**(均以卡面 / AC 为准)

| # | 草案 | 落地 | 依据 |
|---|---|---|---|
| 1 | 结构名 `PerCaseInputs` | **`Inputs53`** | 卡面 **AC-1 逐字**点名 `Inputs53`;§Implementation Notes 1 的 `PerCaseInputs` 是**异名** ⇒「AC 是判据」 |
| 2 | `Tests/…/MedicalConsequences/consequence_input_aggregation_test.cs` | `Tests/EditMode/**MedConsequences**/medcons_aggregation_test.cs` | 卡面 §Test Evidence **点名**该路径 |
| 3 | `IDiseaseProjection DiseaseProjection`(第五路) | `DiseaseProjectionSnapshot` | 接口引用逐组重复装同一对象,**无语义**;快照才是「该病人 @ 结案 tick」 |
| 4 | (未定) | `Assembly.GetReferencedAssemblies()` 元数据 | `test-standards` 禁文件系统依赖;且查**实际**引用集比读声明**更严** |
| 5 | (未定) | 扫描面 = `DaYiJingCheng.Sim.Consequences` 命名空间 | **53 无独立程序集**(ADR-025 具名六装配清单无 53 专属件);**刻意不扫全 `Sim`** —— 会把 9/25/37 的字段纳入 53 判据 = **越权**(承 `review-workflow` 义务 4) |

### 3.3 ⚠️ 本件**不产出** `ConsequenceResolved`

story-001 只做**聚合器(输入侧)**。`ConsequenceResolved` 的产出归 **story-003**。
⇒ `WriterExistenceGate.cs:117` 的「53 未实现」注记**不应因本件改动**,现状维持。

---

## 四、证伪三步(可复核)

**环境**:Unity `6000.3.24f1`,batch `-runTests -testPlatform EditMode`,**不加 `-quit`**。
判决口径 = **退出码 · XML `failed=` 双信号一致**。

### 命令

```bash
UNITY=/home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity
PROJ=/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity
LOGS=$PROJ/Logs

# ① 基线(filtered)
"$UNITY" -batchmode -nographics -projectPath "$PROJ" -runTests -testPlatform EditMode \
  -testFilter "MedConsequences" -testResults "$LOGS/s11-5-medcons.xml" -logFile "$LOGS/s11-5-medcons.log"

# ② 反证 ①:停用排序 ⇒ 退化为到达序(精确替换,Python assert count==1)
#    锚点 = `            ordered.Sort((a, b) => EventOrder.Compare(in a, in b));`
# ③ 还原:cp 备份 + 四重独立复核(承 S11-7:不信 cp / diff 的退出码)
md5sum /tmp/s11-5-backup.cs ConsequenceInputAggregator.cs     # 期望同
grep -c "反证停用" ConsequenceInputAggregator.cs                # 期望 0
grep -c "ordered.Sort((a, b) => EventOrder.Compare" …           # 期望 1
diff -q /tmp/s11-5-backup.cs ConsequenceInputAggregator.cs      # 期望 identical

# ④ 反证 ②:给 Inputs53 注入第六字段 `SixthLaneProbe`(两处锚点各 assert count==1);重跑;还原
# ⑤ 全量 EditMode(零回归)
"$UNITY" -batchmode -nographics -projectPath "$PROJ" -runTests -testPlatform EditMode \
  -testResults "$LOGS/s11-5-editmode-full.xml" -logFile "$LOGS/s11-5-editmode-full.log"
```

### 结果

| 步骤 | 退出码 | XML | 判决 |
|---|---|---|---|
| ① 基线(filtered,20 测) | **0** | `total=20 passed=20 failed=0` | ✅ 20 条全部**已执行** |
| ② **反证 ①**(停用排序) | **2** | `passed=18 **failed=2**` | ✅ 恰红**两条排序专向探针** |
| ③ 还原 ① | — | 残留 0 · 排序调用完好 · `diff` identical · **md5 同** | ✅ |
| ④ **反证 ②**(注入第六路) | **2** | `passed=19 **failed=1**` | ✅ 恰红一条并**逐字点名** |
| ⑤ 还原 ② | — | 残留 0 · `diff` identical · **md5 同** | ✅ |
| ⑥ 全量 EditMode | **0** | `total=3366 passed=3320 failed=0 skipped=46` | ✅ **零回归** |

### 反证失败的**消息**(证明指向变异点,非泛红)

**反证 ①(停用排序)**:

```
FAILED test_ac5304_permutationInvariance_twentyShufflesYieldIdenticalOutput
   msg: AC-53-04:第 0 次置换后结果须逐位一致(到达序不得影响聚合)
FAILED test_ac5304_sameTickStreamPatientDifferentSeq_isOrderedBySeq
   msg: 同 (Tick, Stream, Patient) 内须按 Seq 升序(全序键的第四分量)
```

⇒ 两条**都是排序语义的专向探针**(置换不变 + Seq 定序),消息**精确指向**「到达序 / Seq 升序」。

**反证 ②(注入第六路)**:

```
FAILED test_ac5305_inputs53_hasExactlyFiveLanes
   msg: Inputs53 须恰含五路(CaseClosed / PatternRecognized / JudgmentRecorded / CareApplied /
        病程投影);实际:CaseClosed,PatternRecognized,JudgmentRecords,CareRecords,
        DiseaseProjection,SixthLaneProbe
```

⇒ 消息**逐字列出**注入的 `SixthLaneProbe`。

### 零回归基线对照

| 节点 | EditMode 全量 |
|---|---|
| S11-9 后 | `3344 / 3298 / 0` |
| S11-4 后 | `3346 / 3300 / 0` |
| **本件(S11-5)** | **`3366 / 3320 / 0 / 46`** —— **+20 passed,恰为本轮新增** |

`error CS` 计数 = **0**。**无 asmdef 改动**(`Sim` 引用集仍恰 = `["Sim.Contracts"]`;门 A / b2 门 / ADR-025 §① 全成立)。

### AC → 测载体映射(story 的 6 条 AC 全覆盖)

| AC | 测 |
|---|---|
| ① `Inputs53` 恰五路 | `test_ac5305_inputs53_hasExactlyFiveLanes` |
| ② AC-53-04 置换不变 | `test_ac5304_permutationInvariance_twentyShufflesYieldIdenticalOutput` · `test_ac5304_sameTickStreamPatientDifferentSeq_isOrderedBySeq` |
| ③ 哨兵先行 | `test_ac5304_sentinelSortsBeforePatientEventAtSameTick` · `test_ac5304_sentinelNotFoldedIntoPatientJudgmentLane` · `test_ac5304_pureSentinelStream_yieldsNoCaseGroups` |
| ④ AC-53-05 反射守卫 | `test_ac5305_noRewriteCountFieldsInConsequencesNamespace` · `test_ac5305_guardHasDiscriminatingPowerOnInjectedFixture` |
| ⑤ 病程投影只读边界 | `test_ac53_diseaseProjectionBoundary_simAsmdefReferencesOnlyContracts` |
| ⑥ 边界退化 | `test_boundary_emptyStream_…` · `…singleCaseClosed_…` · `…closeWithoutJudgment_…` · `…projectionOnly_…` · `…projectionReportsMissing_…` |

外加 6 条语义/张力测(P 最后一笔 · C 窗口上界 · 同 tick 窗内 · `careKinds` 三态 · 组序 · 闭集零渗入)。

---

## 五、如实登记(未清项)

### 5.1 两处张力**未**裁定(承批准口径,归 53 的 GDD 轮)

- **① 处置 Kind 集** —— 卡面 §Implementation Notes 4 提 10/11 两支(`EmergencyAttempt` / `DrugTreatmentApplied`),
  而 GDD 规则五 `:165` **只列** `CareApplied`。实现取 **`careKinds` 显式参数**,由装配层决定;
  测试**只**钉「参数被尊重」(窄集/宽集/null ⇒ 三结果),**不**钉该集本身该是什么。
- **② 处置窗口下界** —— 取 37 的 `[CaseOpened.Tick, CloseTick]` 窗口须加第六路,与 AC-1「恰五路」**直接冲突**。
  实现取 **`Tick ≤ 结案 tick`(无下界)**;测试钉**当下口径**并注明其待裁。

### 5.2 6 条 AC 复选框**未勾**

判据已全绿(每条均有专测),但**关闭归 `/story-done`**(承 S11-4 口径,不自行打勾)。

### 5.3 `WriterExistenceGate.cs:117` **未动**

本件不产出 `ConsequenceResolved` ⇒ 该「53 未实现」注记**不应因本件改动**。

### 5.4 story 件内的**异名**(就地登记,原文不追改)

AC-1 逐字 `Inputs53` vs §Implementation Notes 1 逐字 `PerCaseInputs` = **同一结构的两个名**。
实现取 AC 名。**这是继 S11-4 四处之后的又一处「同文件内口径不一致」** —— 已在 story 件 §Test Evidence 下就地登记。

### 5.5 黑名单扫描面与 AC 原文的**载体识别**

AC-3 QA 写「**53 程序集**全部类型」,但 **53 无独立程序集**(无 53 专属 asmdef)。
载体识别 = `DaYiJingCheng.Sim.Consequences` 命名空间。**刻意不扫全 `Sim` 程序集** ——
那会把 9/25/37 的类型纳入 53 的判据(**越权**)。
**实测**:黑名单敏感词全仓 C# 命中 10 处,**全在 `Tests/EditMode/DiagnosisSystem/`(8 的测试)**,
`Sim/` 生产代码**零命中**。

### 5.6 超 AC 原文的一条测

`test_ac5305_guardHasDiscriminatingPowerOnInjectedFixture` 是**判别力自检**
(对注入 `revision_count` 的对照类型断言**必红**)—— 卡面 AC-3 逐字要求「注入负夹具必红」,
但**未点名**以「同台扫描器扫对照类型」的形态实现。本件采纳该形态并如实登记。

---

## 六、改动清单

| # | 文件 | 类型 |
|---|---|---|
| 1 | `unity/Assets/Sim/Consequences/ConsequenceInputAggregator.cs` | 代码(新增,§三) |
| 2 | `unity/Assets/Tests/EditMode/MedConsequences/medcons_aggregation_test.cs` | 测试(新增,20 条) |
| 3 | `production/qa/evidence/s11-5-medcons-story-001-aggregation-2026-10-15.md` | 本件(新建) |
| 4 | `production/sprints/sprint-11.md` | 台账(S11-5 行转 ✅ + 订正框) |
| 5 | `production/sprint-status.yaml` | 台账(S11-5 → `done`) |
| 6 | `production/epics/medical-consequences/story-001-….md` | 状态(`Ready` → `In Review` + §Test Evidence) |
| 7 | `production/session-state/active.md` | session 状态 |

**生产代码**:除新增的 1 件外,`Sim/` 其余部分与 `Sim.Codec/` **零改动**
(反证期的临时改动已四重复核还原,md5 与备份一致)。
**`.`meta`**:两处新目录与文件的 meta 由 Unity 导入生成,格式与既有同构(59 字节最小形态)。

---

## 七、未清项

1. **story-001 的 6 条 AC 复选框** —— 归 `/story-done`(§5.2)。
2. **两处张力** —— 处置 Kind 集 / 处置窗口下界,归 **53 的 GDD 轮**(§5.1)。
3. **`WriterExistenceGate.cs:117`** —— 待 story-003 产出 `ConsequenceResolved` 后才可解除(§5.3)。
4. **story 件内的异名**(`Inputs53` / `PerCaseInputs`)—— 本件只登记不复名(§5.4);
   若 53 的 GDD 轮要统一命名,须**同时改卡面 AC 与实现**。
5. **story-002 / 003 未开工** —— P/C 的**内容**、Judge 求值、`ConsequenceResolved` 产出均未实现。
6. **S11-5 的「起首 story」口径** —— 本件完成**story-001 一件**;S11-5 任务书未要求完成全部 4 件。
7. **sprint 计划未回扫 epic 现状**(§2.1)—— 本件如实登记,处置归 sprint-11 回顾。
