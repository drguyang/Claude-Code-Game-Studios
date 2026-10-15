# 修复报告原件:S11-4 —— prescription EPIC §Epic Status 三项残留

> **对象**:`production/epics/prescription-medication/EPIC.md` §Epic Status 未闭项表(story-001 / story-003 两行)
> **日期**:2026-10-15
> **执行**:sprint-11 · **S11-4**(should-have)
> **判定**:三项 —— **两项(登记回刷)闭合** · **一项(AC-11-22 半边②,落判据)闭合**;
> 另**三项真残余如实保留**(DC-6 零求值 · BL-7 · AC-11-22 半边甲归 producer/TD)
> **前提订正**:**任务书的 3.5 天估算与「DC-2 / DC-6 需实现」的假设均不成立** —— 勘察证明两项实现侧早在
> 2026-10-07(`2a7764f`)已闭环,残留**仅**在登记散文。详见 §二。

---

## 一、原判定(逐字引用,未加工)

`EPIC.md` §Epic Status 未闭项表(**修复前**):

| 归属 | 未闭项 | 阻塞源 |
|---|---|---|
| story-001 | DC-2(`action_id` 闭集)· DC-6(`NOISE_BAND_9` 无主) | OQ-11-2 · BL-2(O-11→9) |
| story-003 | 影子装配注入半边 · `CheckPayloadPairing` 不覆盖 codec tag 序 · **AC-11-22 的 7a 白名单本体** | 需动态生成程序集 · 归 ADR-014 阶段 2 · BLOCKED-BY-7a |

同段 `:131`(**修复前**)逐字:

> ⚠️ **仍未闭**(禁借绿):DC-2 处置 id master 未登记 · DC-6 `NOISE_BAND_9` 未立(BL-2)·
> **DC-6 对当前数据集零求值**(`salicylic_acid` 的 `dose_range = null`,已由覆盖率记账逐字声明)·
> 两条已知测试弱点(M1 反守卫用干净夹具 / M4 正向判别力过窄,已登记不静默)。

GDD `prescription-and-medication.md:760-766`(**修复前**)：

> ⚠️ **本节的判据有三条无法立即执行**:
> ① `DC-2` …… **该枚举的 master 住哪一份文件未登记** ⇒ 在 `OQ-11-2` 裁定前**不得判绿**。
> ② `DC-6` 在 9 立具名噪声带常量前不可执行(`O-11→9` · BL-2)—— 常量缺席,`NOISE_BAND_9` 无量纲。

story-001 `:26` / `:40` / `:41`(**修复前**)三条同口径 NOT-RUN / BLOCKED-BY。

---

## 二、勘察结论:**两项的前提已过时,不是「未实现」**

### 2.1 DC-2 —— master 已登记

| 面 | 实测 |
|---|---|
| 数据产物 | ✅ `assets/data/disease_action_axis.json`(2026-10-07 12:31,`_note` 自陈「系统 9 的**处置轴单一 master**」) |
| 烘焙器 | ✅ `unity/Assets/Editor.Tools.Bake/DiseaseActionAxisBaker.cs` |
| 架构登记 | ✅ `traceability-index.md:509` 逐字「✅ story-007 落地(2026-10-07;`disease_action_axis.json` + …)」;`TR-disease-023` 已登 |
| 提交 | ✅ `2a7764f`(2026-10-07,disease-simulation story-007) |

⇒ 原判词「**master 住哪一份文件未登记**」**今日为假**。

### 2.2 DC-6 —— 常量已具名,判据本体 RUN

| 面 | 实测 |
|---|---|
| 常量 | ✅ `entities.yaml:1466` `NOISE_BAND_PROGRESS_9`(Progress 域)· `:1484` `NOISE_BAND_POTENCY_9`(**药效幅值域**,值 = 100 raw) |
| 门 | ✅ `PrescriptionActionIdRegistry.cs:176 ValidatePerceptibleFloor`,真源期**硬失败**;同文件 `:168` 逐字「判据本体 `RUN`」 |
| 读取路径 | ✅ `RealPerceptibleFloorRaw()`(`:109`)→ `DiseaseActionAxisBaker.BakeFromRepo(...).NoiseBandPotencyRaw`(`DiseaseActionAxisBaker.cs:82` 硬编码 100 raw) |
| 测试 | ✅ `prescription_tables_test.cs` 的 `test_dc6_*` 族 10 条 + `registry_bake_test.cs:466 test_noiseBand9_constantsRegistered` |

⇒ 原判词「常量缺席,`NOISE_BAND_9` 无量纲」**今日为假**。

### 2.3 ⚠️ 原判词的**自相矛盾**已在树中可观测(故本件非「无中生有」)

- `EPIC.md:105` 写「DC-6 `NOISE_BAND_9` 无主」,而 `EPIC.md:128` 同文件记「判据……可跑」、`:131` 记「未立」
- `PrescriptionActionIdRegistry.cs:4` 文件头写「BL-2 …… 未立」,而同文件 `:168` 写「已立 ⇒ 判据 RUN」
- `dose_potency_test.cs:9` / `:194` 写「9 侧不存在」,而 `entities.yaml` 有登记
- GDD `:763`「常量缺席」 vs GDD `:783`「9 侧已立此常量」

⇒ 四处**同文件内自相矛盾**,证「回刷缺失」而非「实现缺失」。

### 2.4 AC-11-22 半边② —— 载体识别错位(非缺产物)

AC-11-22 逐字(GDD `:974`):「……且 **7a 的序列化器两个 Kind 均在白名单内**」。

- 7a **从未有过**独立的「存档白名单**表**」——`ADR-010` 与 `persistence-service.md` 全文皆无此产物
  (`grep NOISE_BAND_9` 级别的全仓检索亦无 Kind 白名单表) ⇒ 原 NOT-RUN 把它读成一张数据表。
- 序列化器的白名单在实现里**天然是** `EventKind` 35 支闭集 ∩ `PayloadCodec.DecodeBoxed`(`PayloadCodec.cs:49-91`)
  / `PayloadEncoder.EncodeBoxed`(`PayloadEncoder.cs:77-139`) 的分派表 —— 两者 `default` **逐字**抛
  「不在 35 支闭集内」。
- **真缺口在别处**:现有 `test_drugEvent_codecHasBothBranchesForThisKind`(`drug_event_test.cs:572`)
  **只覆盖 `DrugTreatmentApplied` 一支**,而 AC 要的是**两个** Kind(`EmergencyTreatmentApplied` 侧零覆盖)。

---

## 三、修复落点

### 3.1 代码面(3 文件)

| 文件 | 改动 |
|---|---|
| `unity/Assets/Tests/EditMode/PrescriptionMedication/drug_event_test.cs` | ① 文件头 NOT-RUN 声明第 4 条(:24-26)改写为「已落判据 + 载体识别说明」<br>② **新增两测** `test_ac1122_serializationWhitelist_containsBothNamedKinds` · `test_ac1122_serializationWhitelist_coversEntireEventKindClosedSet` |
| `unity/Assets/Editor.Tools.Bake/PrescriptionActionIdRegistry.cs` | `:4` 文件头 `BL-2 …… 未立` → `BL-2 ✅ 已闭`(消同文件自相矛盾) |
| `unity/Assets/Tests/EditMode/PrescriptionMedication/dose_potency_test.cs` | `:9` / `:164` / `:169` / `:194` 四处过期注释回刷(「9 侧不存在」→「已闭 ⇒ 本体 RUN,本骨架保留为单元判据」) |

**新判据的形态**(两条互补):

1. `containsBothNamedKinds` —— **贴 AC 原文**:两个 Kind **正向可往返**。
   判据 = 不在分派表内的 Kind 落 `default` ⇒「能往返」⇔「在表内」。
2. `coversEntireEventKindClosedSet` —— **超出 AC 原文**(原文只点名两个 Kind),钉死「白名单 = 闭集」语义;
   探针 = `Enum.GetValues` 逐值 × `Decode<InjuryOnsetPayload>`(空 span),断言**零个**落 `default`。
   可捕获的变异 = 「枚举加了成员而 `PayloadCodec.cs:53-87` 手写 `case` 漏接」。

### 3.2 登记面(3 文件)

| 文件 | 改动 |
|---|---|
| `design/gdd/prescription-and-medication.md:760-766` | 三条注 → **两条划闭 + 一条仍开**;保留「DC-6 零求值」真残余 |
| `production/epics/prescription-medication/EPIC.md` | `:99-101` 补回刷说明 · `:105` 表行划闭 · `:116` 段标题 · `:131` 段末改列**实际**残余 |
| `production/epics/prescription-medication/story-001-….md` | `:26` Guardrail · `:40` DC-2 · `:41` DC-6 |

### 3.3 ⚠️ **本件未动 7a** —— S11-4 的附加条件**不触发**

S11-4 的 AC 逐字:「若动 7a 须带 case↔7a 折叠语义集成夹具(TD 风险 1 缓解)」。

**判据落点在 `Sim.Codec`(`PayloadCodec` / `PayloadEncoder` 分派表),不触及 7a 的存档层**
(`SaveCodec` / `SaveService` / 折叠谓词 / 高水位重构)。⇒ 该附加条件**不触发**,故本件**不带**该集成夹具。
**不得**把本件读成「7a 已被触及」或「TD 风险 1 已缓解」。

---

## 四、证伪三步(可复核)

**环境**:Unity `6000.3.24f1`,batch `-runTests -testPlatform EditMode`,**不加 `-quit`**。
判决口径 = **退出码 · XML `failed=` 双信号一致**(项目既有口径:退出码不单独反映测试结果)。

### 命令

```bash
UNITY=/home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity
PROJ=/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity
LOGS=/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Logs

# ① 基线(filtered)
"$UNITY" -batchmode -nographics -projectPath "$PROJ" -runTests -testPlatform EditMode \
  -testFilter "PrescriptionMedication" -testResults "$LOGS/s11-4-prescription.xml" \
  -logFile "$LOGS/s11-4-prescription.log"

# ② 反证:注释掉 PayloadCodec.cs:59 的 case,重跑
#    精确替换(Python,assert count==1 —— 承 S11-7 的脚本同名变量教训)
# ③ 还原:cp 备份 + **独立 grep 复核**(承 S11-7:不信 cp / diff 的退出码)
grep -c "反证停用" "$PROJ/Assets/Sim.Codec/PayloadCodec.cs"                      # 期望 0
grep -c "case EventKind.EmergencyTreatmentApplied: return Decode" "$PROJ/Assets/Sim.Codec/PayloadCodec.cs"  # 期望 1
git status --short "$PROJ/Assets/Sim.Codec/PayloadCodec.cs"                     # 期望空

# ④ 全量 EditMode(零回归)
"$UNITY" -batchmode -nographics -projectPath "$PROJ" -runTests -testPlatform EditMode \
  -testResults "$LOGS/s11-4-editmode-full.xml" -logFile "$LOGS/s11-4-editmode-full.log"
```

### 结果

| 步骤 | 退出码 | XML | 判决 |
|---|---|---|---|
| ① 基线(filtered,176 测) | **0** | `passed=176 failed=0` | ✅ 两条新测均**已执行**(非被 filter 漏掉) |
| ② **反证**(停用 `EmergencyTreatmentApplied` case) | **2** | `passed=174 **failed=2**` | ✅ **恰红两条新测** |
| ③ 还原 | — | `grep` 残留 0 · case 完好 1 · `git status` **空** | ✅ 生产代码回到 HEAD |
| ④ 全量 EditMode | **0** | `total=3346 passed=3300 failed=0 skipped=46` | ✅ **零回归** |

### 反证失败的**消息**(证明指向变异点,非泛红)

```
FAILED: test_ac1122_serializationWhitelist_containsBothNamedKinds
   msg: System.IO.InvalidDataException : 未知 EventKind=6 —— 不在 35 支闭集内
FAILED: test_ac1122_serializationWhitelist_coversEntireEventKindClosedSet
   msg: 以下 Kind 落在 codec 分派表 default —— 白名单闭集不完整:EmergencyTreatmentApplied
        Expected: <empty>   But was:  < "EmergencyTreatmentApplied" >
```

⇒ **判别力确认**:两测均对「分派表缺成员」这一变异敏感,失败消息**精确点名**被移除的 Kind。
**零回归基线对照**:S11-9 后 EditMode = `3344 / 3298 / 0`;本轮 = `3346 / 3300 / 0`(**+2 passed**,恰为本轮新增)。
`error CS` 计数 = **0**。**无 asmdef 改动**(门 A / b2 门 / ADR-025 §① 全成立)。

### S11-4 的 QA 三项 —— 均已存在且在 176/176 内(**无需新写**)

| QA 项 | 载体 |
|---|---|
| AC-11-22 IL 扫描 | `PrescriptionWriterGates.cs` 门 + `drug_event_test.cs` 的 `test_drugEvent_writerExclusivity_*` 4 条 + **本件新增 2 条** |
| DC-2 闭集 fail-loud | `prescription_tables_test.cs` 的 `test_realRegistry_*` / `test_realWarnings_*` 5 条 |
| DC-6 引用链闭环 | `prescription_tables_test.cs` 的 `test_dc6_*` 族 10 条 + `registry_bake_test.cs:466` |

---

## 五、如实登记(三处真残余 + 一处边界)

### 5.1 **DC-6 对当前数据集零求值** —— 真残余,未消解

`assets/data/item_database_items.json` 的 `salicylic_acid` 的 `dose_range = null` ⇒ 无相邻档可判。
覆盖率记账已**逐字**声明「本次 DC-6 零求值(判据未跑,禁读成绿)」。
⇒ **判据可执行,但无数据可判**。本件**不得**被读成「11 的表已合规」。
**归属**:`drug_profile` 数据归 21a 内容轮。

### 5.2 **BL-7(21a `drug_potency` 域未声明)** —— 仍开

GDD `:760` ③ 原样保留(归 21a)。`PrescriptionActionIdRegistry.cs` 文件头 `:5` 的 BL-7 一行**未动**。

### 5.3 **AC-11-22 半边甲(文本背离)归 producer/TD** —— 本件**未处置**

`EPIC.md:109` 逐字:「**AC-11-22 / TR-prescription-018 与实现的文本背离**(解码点在 11 之外构造载荷)|
门已**显式记账**;收窄裁定**归 producer / TD**,本 epic 不自行改 AC 文本」。
具体代码:`PayloadCodec.History.cs:332`(`Sim.Codec` 程序集)构造 `DrugTreatmentAppliedPayload`
—— 与 AC 原文「构造点**仅存在于 11 的程序集**」背离。
**本件维持其显式记账现状,不改 AC 文本、不改门白名单。** 半边甲的裁决仍待 producer/TD。

### 5.4 本件对半边②的读法**不是** AC 文本收窄(边界声明)

AC-11-22 文本**一字未动**。本件做的是**识别「白名单」在实现里的真实载体**(= 分派闭集),
并以判据钉死。这与 5.3 的**文本收窄**(须改 AC 措辞才能对齐)性质不同,
**故不越 `EPIC.md:109` 的「本 epic 不自行改 AC 文本」边界**。
⚠️ 若 producer/TD 认为「白名单」应指另一产物(如另立一张 7a 数据表),则本件判据需重做 —— **该读法待其裁定**。

---

## 六、改动清单

| # | 文件 | 类型 |
|---|---|---|
| 1 | `unity/Assets/Tests/EditMode/PrescriptionMedication/drug_event_test.cs` | 代码(测试) |
| 2 | `unity/Assets/Editor.Tools.Bake/PrescriptionActionIdRegistry.cs` | 注释 |
| 3 | `unity/Assets/Tests/EditMode/PrescriptionMedication/dose_potency_test.cs` | 注释 |
| 4 | `design/gdd/prescription-and-medication.md` | 登记 |
| 5 | `production/epics/prescription-medication/EPIC.md` | 登记 |
| 6 | `production/epics/prescription-medication/story-001-prescription-lexicon-tables-polarity-gate.md` | 登记 |
| 7 | `production/qa/evidence/fix-s11-4-prescription-dod-residuals-2026-10-15.md` | 本件(新建) |

生产代码 `Sim/` 与 `Sim.Codec/` **零改动**(`PayloadCodec.cs` 仅为反证临时改动,已三重复核还原)。

---

## 七、未清项

1. **DC-6 零求值** —— 归 21a 内容轮(`drug_profile` 数据);本件只登记不消解。
2. **BL-7** —— 21a `drug_potency` 域未声明;GDD `:760` ③ 原样保留。
3. **AC-11-22 半边甲** —— 归 producer/TD(§5.3)。
4. **本件对半边②的读法** —— 若 producer/TD 另裁「白名单 = 独立数据表」,判据须重做(§5.4)。
5. **story-001 的 AC 勾选状态** —— 本件只回刷判词(`NOT-RUN` → `RUN`),**未把 AC 复选框打勾**;
   两 AC 的最终关闭归 `/story-done` 流程。
6. **EPIC 仍 `5/5 Complete` 但 DoD 未达** —— DC-2 / DC-6 划闭后,DoD 第 2 条仍受
   story-002(AC-11-15 三格矩阵)· story-003(半边甲 / 影子装配)· story-004(正式对拍)· story-005(走查)制约。
   **本件不改变 epic 的 DoD 状态。**

---

## 八、二轮回刷(2026-10-15 · 本件提交后的补记)

> **触发**:本件(`8401e6a`)提交后复核发现 —— **§三的回刷只做到了「段」,没做到「表 / 件」**。
> 即:本件在 `EPIC.md:102` / `:138` 两处**声明**「AC-11-22 的 7a 白名单半边已落判据」,
> 而**同一文件**的 `:88`(story 表行)与 `:112`(未闭项表)**仍写它未闭**,`story-003:110` 亦然。
> ⇒ 这与本件 §2.3 诊断出的病灶(**同文件内自相矛盾**)**同为一种失效模式** —— 本件修掉了四处,又留下三处。

### 8.1 本次改动(3 处,纯登记回刷,**零裁定 · 零代码**)

| # | 文件:行 | 改前 | 改后 |
|---|---|---|---|
| 1 | `EPIC.md:88` | `…/ 7a 白名单 NOT-RUN)` | `~~7a 白名单~~ ✅ **已闭 2026-10-15**(S11-4 落判据);…` |
| 2 | `EPIC.md:112` | `…· AC-11-22 的 7a 白名单本体 \| … · BLOCKED-BY-7a` | `…· ~~AC-11-22 的 7a 白名单本体~~ ✅ **已闭**(…闭集 ∩ codec 分派表) \| … ~~· BLOCKED-BY-7a~~` |
| 3 | `story-003:110` | `…/ 7a 白名单本体)` | `…/ ~~7a 白名单本体~~ ✅ **已闭 2026-10-15(S11-4)**:判据落于 …(载体识别,AC 文本一字未动)` |

第 3 处**顺带消除**了 story-003 件的**件内自相矛盾**:`:36` 的 `[x] AC-11-22`(已勾)vs `:110`(列为未闭)。
改后在 `:110` 就地声明「本项划闭**不动**同行的『AC 文本收窄』—— 后者是**半边甲**,归 producer / TD」,防混读。

### 8.2 扫描面(全仓 `7a 白名单` / `BLOCKED-BY-7a`,逐项判为何动 / 不动)

| 命中 | 判定 |
|---|---|
| `EPIC.md:88` / `:112` · `story-003:110` | ✅ **本次改**(8.1) |
| `EPIC.md:102` / `:138` · `active.md:3549` | 本件的声明段 / 判词表 ⇒ 与新口径**一致**,不动 |
| `sprint-11.md:119` | S11-4 的**任务书行**(描述原任务,非判定)⇒ 不动(订正框已在其下) |
| `active.md:1908` | **历史 session 态**(本件之前的轮次)⇒ 不动 |
| `tests/unit/item_database/README.md:358` · `item-database/story-009:41/:97/:108` · `story-010:44/:144/:155` | **21a 域**,指 `ItemInstanceCodec` 的**文件级存档往返** ⇒ **不同事项**,不动 |

### 8.3 ⚠️ 第四处口径分歧(域外,**本次未动**,如实登记)

`production/qa/qa-plan-sprint-11-2026-10-14.md:62` 逐字:

> - **AC-11-22**(7a 白名单本体):**登记处方载荷字段的 7a 白名单条目**;IL 扫描断言 …

⇒ 它把「7a 白名单」读作**一张需登记的条目表** —— 与本件的**载体识别**(= `EventKind` 闭集 ∩ codec 分派表,**无独立表**)**冲突**。
**这正是 §5.4 预告的「另一种读法」的实例**。本次**未动**,理由:
① 它是 **sprint 计划期的历史产物**(2026-10-14,早于本件);
② 改动它 = 改「该 AC 的判据形态」,属 **§5.4 / §七-4 已明归 producer/TD** 的裁定面。
⇒ **留作 producer/TD 复议时的输入材料。**

### 8.4 半边甲的**性质纠正**(先前措辞订正)

复核时读到 `story-003:103-105`(**本件 §5.3 未引用到的行**):

> - **M-2 全闭(显式记账式)**:`Sim.Codec/PayloadCodec` 从 `ProductionCtorSites` **移出**,
>   新立 `DecoderCtorSites` 分离登记 + `RunAll` 摘要报「⚠️ 判据-文本背离」

⇒ **实现侧早已处理**:门把「**解码器**构造点」与「**写入**构造点」**分离登记**并摘要报警。
故半边甲的准确性质 = **「已显式记账、待裁收窄」**,**不是**「未处理的背离」。
⇒ **§5.3 的措辞据此订正**(原文保留,判据以本行为准)。
**真剩余**仍只有一项:**AC 文本收窄的裁定**(归 producer/TD)—— 与 §七-3 一致,未变。

### 8.5 本次的**零**影响面

- **零代码改动** ⇒ **不跑 Unity、无回归面**(本件 §四的证伪三步仍为 `8401e6a` 时的有效记录,不重跑)。
- **不改任何判据 / 数值 / AC 文本** ⇒ §5.4 的边界声明继续成立。
- **epic DoD 状态不变** ⇒ §七-6 继续成立。

### 8.6 过程面观察(承 §2.1,**第二次现身**)

本件 §2.1 已登记「sprint 计划未回扫 epic 现状」。**本次是同一失效模式的第二次现身**:
**一次回刷如果只改「声明段」而不改「表 / 件」,就会**生产**新的同文件矛盾** ——
本件修掉四处,又留下三处,即为例证。
⇒ 建议:后续回刷的收口判据 = **改完后对同一关键词做全仓 grep,逐条判「动 / 不动」并落纸**(即 8.2 那张表的做法)。
