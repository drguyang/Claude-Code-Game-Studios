# Story 007: 处置轴 + treatable_by 数据面 + NOISE_BAND_9 具名常量

> **Epic**: disease-simulation(系统 9)
> **Type**: Config-Data
> **Status**: Complete ✅ 2026-10-07(双代理一轮评审 → 修复 → 复跑全绿;原件 `production/qa/evidence/review-disease-action-axis-2026-10-07.md`)
> **ADR**: ADR-014(两阶段烘焙)· ADR-024(Kind 单一真源)· ADR-005(确定性模拟)
> **GDD**: design/gdd/disease-simulation.md

## Context

11(处方用药)的 story-001 已把 **DC-2(action_id 闭集)** 与 **DC-6(相邻档药效差 ≥ 噪声带)**
两条构建期判据的**校验机制**建成可跑的机器,但**判据本体维持 NOT-RUN** —— 两条的真源都不存在:

- **DC-2 的真源缺席**:GDD `prescription-and-medication.md:748` 逐字写「该枚举的 master 住哪一份
  文件**未登记**」。实测比登记更糟:`unity/Assets/Sim/RegistrySchema.cs` 的 `DiseaseRegistryEntry`
  **连 `treatable_by` 字段都没有**;`assets/data/disease_registry.json` 不存在;全库无 `Disease*`
  的 baker/binder/cookedwriter;9 的 story-003 自陈只交付 schema + 17 条校验,**未接烘焙管线**
  (测试用 C# 内造的合成 fixture)。
- **DC-6 的真源缺席**:`NOISE_BAND_9` 归 9,**未立**(BL-2 / `D-9-J` ⏳ 待认领)。9 现文只有
  `curve.sigma`(旋钮)与 `Noise(seed,t)`(函数),**二者都不是「带宽度」这个可比门槛量**。

用户 2026-10-07 裁定三项,均落到 **「重开系统 9」**:

1. **DC-2 = 单一轴归 9,两贡献者共用 id 空间** —— 9 的注册表成为处置 id 的**唯一 master**;
   10 的 `EmergencyAction` 与 11 的 `prescription_actions.json` 都是该轴上的条目。
2. **DC-6 = 9 立具名常量**,并**为两个量纲各给一把尺**(不得跨量纲挪用 `σ`)。
3. **交付形态 = 只建动作轴子集数据文件** —— **不**建完整 `disease_registry.json`(曲线/严重度等
   数值仍冻结待数值轮,建全表 = 被迫填占位值,而 9 的 17 条区间校验会面对假数据 = GDD 点名的
   「判据对合法输入类误判」)。

预期结果:11 的 `DC-2` / `AC-11-19` 从「**不可执行**」升为「**可执行**(数值待数值轮)」;
10 的 `DC-4` 判据源改指 9;`O-11→9`(BL-2)结清。**不**宣称 11 的表已合规 —— 数值轮未到。

## Acceptance Criteria

| # | 判据 | 验证方式 |
|---|------|----------|
| AC-9-01 | `disease_action_axis.json` 存在且可烘焙 | `DiseaseActionAxisBaker.BakeFromRepo` 不抛 |
| AC-9-02 | 处置轴闭集 = {0, 1, 10, 11, 12} | 烘焙产物 `Actions.Count == 5` |
| AC-9-03 | treatable_by 关系 = 8 病种 × 处置 × 极性 | 烘焙产物 `TreatableBy.Count == 6` |
| AC-9-04 | 9-DC-1: owner ∈ {"10", "11"} | 负夹具:owner="12" ⇒ 烘焙期硬失败 |
| AC-9-05 | 9-DC-2: polarity ∈ {causal, symptomatic} | 负夹具:polarity="unknown" ⇒ 烘焙期硬失败 |
| AC-9-06 | 9-DC-3: action_id 唯一 | 负夹具:重复 id ⇒ 烘焙期硬失败 |
| AC-9-07 | 9-DC-4: treatable_by[].action ∈ 处置轴闭集 | 负夹具:action=99 ⇒ 烘焙期硬失败 |
| AC-9-08 | 9-DC-5: 病种 key 非空 | 负夹具:key="" ⇒ 烘焙期硬失败 |
| AC-9-09 | 9-DC-6: 至少 1 个处置 | 负夹具:actions=[] ⇒ 烘焙期硬失败 |
| AC-9-10 | 9-DC-7: 至少 1 个病种 | 负夹具:treatable_by={} ⇒ 烘焙期硬失败 |
| AC-9-11 | `NOISE_BAND_PROGRESS_9` 已立(Progress 域) | `entities.yaml` constants 段有条目 |
| AC-9-12 | `NOISE_BAND_POTENCY_9` 已立(药效幅值域) | `entities.yaml` constants 段有条目 |
| AC-9-13 | 量纲纪律:三处不是同一把尺 | `entities.yaml` 条目 constraint 字段明写 |
| AC-9-14 | 11 的 DC-2 判据源改指 9 的烘焙轴 | `PrescriptionActionIdRegistry.RealActionIds()` 含 10 |
| AC-9-15 | 11 的 DC-6 判据源改指 9 的 `NOISE_BAND_POTENCY_9` | `PrescriptionActionIdRegistry.RealPerceptibleFloorRaw` 存在 |
| AC-9-16 | 10 的 DC-4 判据源改指 9 的烘焙轴 | `EmergencyActionSchema.ValidateActionId` 读 9 的轴 |
| AC-9-17 | `prescription_actions.json` 的 action_id 重排 1→10 | 文件内容 + 烘焙产物 |
| AC-9-18 | `architecture.yaml` 幽灵引据订正 | `disease_registry.json` → `disease_action_axis.json` |
| AC-9-19 | `disease-simulation.md` D-9-J 结案 | §Debt Register 行状态 = 已结案 |
| AC-9-20 | `prescription-and-medication.md` O-11→9 结案 | §Cross-References 行状态 = 已闭 |

## Implementation Notes

### 一、9 侧:建处置轴 + treatable_by 数据面(单一 master)

**新建 `assets/data/disease_action_axis.json`** —— 一份文件两段:**处置轴**(闭集本体)+
**`treatable_by` 关系**(处置 × 病种 → 极性)。两段同住 = 单一真源,DC-2/DC-4 读第一段,
9 的 F1 门与 11 的 DC-4 双表对拍读第二段。

**改 `unity/Assets/Sim/RegistrySchema.cs`**:
- `DiseaseRegistryEntry` 增 `TreatableByEntry[] TreatableBy`。
- 增校验 **R1-18**:`treatable_by[].polarity ∈ {causal, symptomatic}`;
  **R1-19**:`treatable_by[].action` ∈ 处置轴闭集(**引用却无登记**的反向守卫)。
- 增 **`handle` 派生**(GDD `:329` 的规则九,数据非代码):`causal` ⇔ ∃对因处置;
  否则 ∈ {`DIS_TYPHOID`, `DIS_DYSENTERY`, `DIS_HEART_FAILURE`} ⇒ `care`;
  `DIS_TETANUS` ⇒ `none`;其余 ⇒ `symptomatic_only`。**派生而非手填,防「表里写 causal、
  实际没有对因药」的静默撒谎** —— 这是 `treatable_by` 对 8 的接口意义所在(GDD `:294`)。

**新建 9 侧烘焙三件套**(照 `Editor.Tools.Bake/` 既有先例):
`DiseaseActionAxisBaker.cs` / `DiseaseActionAxisBinder.cs` / `DiseaseActionAxisCookedWriter.cs`,
接 `DataBakeMenu.cs`。产物 `unity/Assets/DataCooked/disease_action_axis.cooked.bytes`。
**阶段 2 绑定层须走自研 + `FixParse` 纪律**(ADR-014):本件全整数,无 `Fix` 字段。

### 二、9 侧:立 `NOISE_BAND_9` 具名常量(两个量纲各一把尺)

**改 `design/gdd/disease-simulation.md`** §Tuning Knobs(`:1533-1538` 的 🔴 块)与 §Debt Register
(`:1463` 的 `D-9-J` 行),把「无此常量」改为**已立**:

- **`NOISE_BAND_PROGRESS_9`** —— **Progress 幅值域**(9 自己的 F1/F3 波动域),
  定义为 9 拥有的一把尺(形状 `= k_progress × σ`,或独立具名值)。
- **`NOISE_BAND_POTENCY_9`** —— **药效幅值域(Q16.16 raw)**,**11 的 DC-6 消费这一把**。
- **量纲纪律写死在常量条目里**:三处**不是同一把尺** —— 9 的 `σ`(Progress 域)≠ 本常量
  (药效幅值域)≠ 21a 品级地板(tick 域,`D-21-24`)。**不得互相借用数值**(TR-prescription-019)。

⚠️ **数值归用户数值轮** —— 本轮交付的是**符号存在、有主、有域、有单位、可从 9 的烘焙产物导出**,
**不是**一个数字。9 的烘焙产物须导出 `NOISE_BAND_POTENCY_9` 供 11 读。

**改 `design/registry/entities.yaml`** `constants:` 段(`:1191` 起)新增 `NOISE_BAND_9` 条目
(照 `SKILL_CAP` / `perceptible_floor` 的字段形状:`name / status / source / referenced_by /
value / unit / constraint / added / revised`),`source: design/gdd/disease-simulation.md`,
`referenced_by` 含 `design/gdd/prescription-and-medication.md`(DC-6 / AC-11-19)。
**同步** `:1448-1456` 的 21a `perceptible_floor` 条目 —— 其 `value` 现写「待定 —— 须 > 9 的病史
噪声带」,须回刷为指向已具名的 9 常量,并保留「不同轴」警示。

### 三、10 侧:DC-4 判据源改指 9

**改 `unity/Assets/Sim/EmergencyProcedures/EmergencyAction.cs`** 的
`EmergencyActionSchema.ValidateActionId`(`:89-93`):判据从
`Enum.IsDefined(typeof(EmergencyAction), actionId)` 改为**读 9 的烘焙轴**(`owner == "10"` 段)。
- `EmergencyAction` 枚举**保留**为 10 侧对 0–1 段的**类型别名**(载荷 `EmergencyAttempt.action`
  的字段类型不变 ⇒ 零涟漪)。
- 新增**构建期断言**:枚举 ordinal ⊂ 9 的轴 ∩ `owner=="10"`,且逐值相等 —— 把「单一轴」变成
  可红的机器,而非散文。

### 四、11 侧:DC-2 指向 9 的烘焙产物

**改 `unity/Assets/Editor.Tools.Bake/PrescriptionActionIdRegistry.cs`**:
- 删影子集 `ShadowRows`(`:78-82`)与 `RealRegistryFileName` 悬空指针(`:72`)。
- `ValidateActionId` 的闭集源改为**读 9 的烘焙轴**。
- 影子期「只出 Warnings 不硬失败」的口径**升格为硬失败**(真源已落地)。
- **`assets/data/prescription_actions.json`** 的 `action_id` 重排:`salicylic_acid` 由 `1` 改为
  `10`(柳树皮,对症)。

**改 `unity/Assets/Editor.Tools.Bake/PrescriptionActionIdRegistry.cs`** 的
`ValidatePerceptibleFloor`:`ShadowPerceptibleFloorRaw = 100L`(`:65`)删除,地板源改读 9 的
`NOISE_BAND_POTENCY_9`。`DataBakeMenu.cs` 的影子报出块改为「已用真源」或不报。

### 五、story 拆分与登记面(承既有先例)

**新 story 文件**:`production/epics/disease-simulation/story-007-action-axis-and-noise-band.md`
(编号 = 最大号 + 1,承 `interaction-system` 007 / `persistence-service` 002 先例)。
结构照 `story-003-registry-schema-bake-pipeline-gate-a.md` 的 section 序
(Header → Context → Acceptance Criteria → Implementation Notes → Out of Scope → QA Test Cases
→ Test Evidence → Dependencies → Completion Notes)。⚠️ story-003 有**重复的 `## Completion Notes`
标题**(既有瑕疵)—— **照抄时勿复制该错**。

**登记面回刷清单**:

| # | 文件 | 动作 |
|---|---|---|
| 1 | `production/epics/disease-simulation/EPIC.md:6` | 状态行 `Complete` → **`In Progress`**(承 `persistence-service` 7a 先例) |
| 2 | 同上 `:80-90` | Stories 表加 `007` 行 |
| 3 | `production/epics/index.md:26` | Stories 计数 6 → 7 · 状态 · **「重开…如实登记」散文** |
| 4 | `production/session-state/active.md:185/:928/:127` | NOT-RUN 项回刷(DC-2 / DC-6 两条**结清**);计数重算(`:630` 口径) |
| 5 | `design/gdd/disease-simulation.md:4`(头部 ⑤)· `:1463`(`D-9-J` 行)· `:1533-1538`(Tuning Knobs 块) | 承接侧三处 |
| 6 | `design/gdd/prescription-and-medication.md:909` | `O-11→9` 行状态 `⏳ 须 9 重开` → **已闭** |
| 7 | `design/registry/entities.yaml` | `constants:` 新增 `NOISE_BAND_9`;回刷 `:1448-1456` |
| 8 | `docs/architecture/tr-registry.yaml` | 追加 `TR-disease-023`(`:1123` 块末);`TR-prescription-005` / `-019` 状态回刷 |
| 9 | `docs/architecture/traceability-index.md` | 追加变更记录行 |
| 10 | `docs/registry/architecture.yaml:640/:742` | ⚠️ 该处把不存在的 `disease_registry.json` **当真源引用**(幽灵引据)—— 须订正为 `disease_action_axis.json` |

> ⚠️ **重开 = 已 Approved 文件的重开**(11 侧 `:912` 的口径):承接件(9)的 Approved 状态与其
> 重开触发条件清单**须同步追加**,否则本轮 = **单方宣布**。

**登记新债(不静默)**:`清创` —— 9 点名、10 无实现。归 10 的 GDD 轮,记 `O-9→10` 类条目。

### 六、验证

**测试**(承标准协议第 1 步,先写测试):

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# 9 侧新测 + 既有回归
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.DiseaseSimulation" \
  --output unity/Logs/disease_axis.xml
# 11 侧 DC-2 / DC-6(须由 174 基线变化 —— 影子测删除、真源测新增)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.PrescriptionMedication" \
  --output unity/Logs/prescription_axis.xml
# 10 侧 DC-4
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.EmergencyProcedures" \
  --output unity/Logs/emergency_dc4.xml
# 全量
unity test unity --mode EditMode --output unity/Logs/editmode_full_axis.xml
```

⚠️ **XML 解析陷阱(已踩两次)**:`re.findall(r'total="(\d+)"...')` 会返回**首个** `<test-suite>`
节点(per-fixture),不是根。须显式匹配根节点
`<test-suite ... type="TestSuite" name="unity" ...>` 再取属性。基线:全量 **2935 / 2888 / 0 红**。

**可红性证明(突变验证,项目标准)**:对新增守卫逐一注入改坏点 ⇒ 须实测红 ⇒ 还原:
- ① 删 `R1-19` 的闭集守卫 ⇒ 引用空动作的 fixture 须红
- ② `handle` 派生改手填 ⇒ 写 `causal` 但无对因药的 fixture 须红
- ③ 10 侧枚举-轴一致性断言改 `if (false)` ⇒ 须红
- ④ 11 侧地板源改回硬编码 ⇒ 须红

**评审 + 收口**(承标准协议第 2–5 步):双代理一轮(`lead-programmer` 代码面 +
`qa-lead` 测试面)→ 修复 → 复跑绿 → 原件落
`production/qa/evidence/review-disease-action-axis-2026-10-07.md` → 提交推送。

## Out of Scope

- **不建完整 `disease_registry.json`** —— 曲线/严重度/传染性等数值冻结待用户数值轮。
- **不给 `NOISE_BAND_9` 填数值** —— 数值归用户;本轮只让符号存在且有主。
- **不重排 9 的 16 字段 + 17 条区间校验** —— 探查发现的 C# 与 GDD §R1 的**形状断裂**
  (C# 17 条全是区间/非空,GDD 17 条是曲线/复发块/compounds 无环等语义规则,**无一条对应**)
  是**既有**问题,不属本轮授权面。**登记为本轮发现的新债**,归 9 的下一轮。
- **不实现 10 的 `清创`** —— 只登记。

## QA Test Cases

| # | 用例 | 预期 |
|---|------|------|
| T1 | 烘焙 `disease_action_axis.json` | 不抛,产物 5 处置 + 6 关系 |
| T2 | 负夹具:owner="12" | 烘焙期硬失败 |
| T3 | 负夹具:polarity="unknown" | 烘焙期硬失败 |
| T4 | 负夹具:重复 action_id | 烘焙期硬失败 |
| T5 | 负夹具:action=99 | 烘焙期硬失败 |
| T6 | 负夹具:病种 key="" | 烘焙期硬失败 |
| T7 | 负夹具:actions=[] | 烘焙期硬失败 |
| T8 | 负夹具:treatable_by={} | 烘焙期硬失败 |
| T9 | 11 的 DC-2:action_id=10 ∈ 真源闭集 | 通过 |
| T10 | 11 的 DC-2:action_id=99 ∉ 真源闭集 | 硬失败 |
| T11 | 11 的 DC-6:相邻档差 ≥ 真源地板 | 通过 |
| T12 | 11 的 DC-6:相邻档差 < 真源地板 | 硬失败 |
| T13 | 10 的 DC-4:action_id=0 ∈ 真源闭集 | 通过 |
| T14 | 10 的 DC-4:action_id=99 ∉ 真源闭集 | 硬失败 |

## Test Evidence

> **2026-10-07 评审修复轮后复跑**(双代理一轮的原判:代码面 CHANGES REQUIRED · 测试面不予通过;
> 全部修复落点与突变复验见 `production/qa/evidence/review-disease-action-axis-2026-10-07.md`)。

| 套件 | 命令 | 结果 |
|------|------|------|
| 9 侧 | `unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.DiseaseSimulation" --output unity/Logs/disease_fix_v2.xml` | **67 / 67 passed / 0 failed** |
| 11 侧 | `unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.PrescriptionMedication" --output unity/Logs/prescription_fix_v3.xml` | **174 / 174 passed / 0 failed** |
| 10 侧 | `unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.EmergencyProcedures" --output unity/Logs/emergency_fix_v2.xml` | **116 / 116 passed / 0 failed**(4 skipped 既有) |
| 全量 | `unity test unity --mode EditMode --output unity/Logs/editmode_final.xml` | **2948 / 2901 passed / 0 failed / 46 skipped / 1 inconclusive**(基线 2935/2888;+13 = 本轮新增) |

**突变验证(2026-10-07 复验)**:9-DC-1…7 七条守卫逐一注入改坏点 ⇒ 全部实测红 ⇒ 还原绿;
10 侧 `ValidateActionIdFromAxis` 首跑突变**存活** ⇒ 补反向断言(轴外 id=12 须被拒)⇒ 复跑红 ⇒ 还原绿。

## Dependencies

- 系统 9(disease-simulation)—— 本 epic 主体
- 系统 10(emergency-procedures)—— DC-4 判据源改指 9
- 系统 11(prescription-medication)—— DC-2 / DC-6 判据源改指 9
- ADR-014(两阶段烘焙)· ADR-024(Kind 单一真源)· ADR-005(确定性模拟)

## Completion Notes

- **交付**:`assets/data/disease_action_axis.json`(处置轴 + treatable_by 单一真源)·
  9 侧烘焙三件套(`DiseaseActionAxis{Baker,Binder,CookedWriter}` + `Validator`)·
  `NOISE_BAND_PROGRESS_9` / `NOISE_BAND_POTENCY_9` 双常量(entities.yaml,值 = 100 raw,
  2026-10-07 用户裁定)· 11 侧 `PrescriptionActionIdRegistry` 真源接线 ·
  10 侧 `ValidateActionIdFromAxis` 接线点 · `architecture.yaml` 幽灵引据订正 ·
  D-9-J / O-11→9 结案。
- **测试**:见 Test Evidence(9 侧 67 · 11 侧 174 · 10 侧 116 · 全量 2948/2901/0 红)。
- **双代理一轮评审(2026-10-07)**:原判代码面 CHANGES REQUIRED(2B/6M/6m/3n)、
  测试面不予通过(S1×5 借绿…)。**修复轮已完成并复跑全绿**;
  逐条「原判定 → 修复落点 → 验证命令」见
  `production/qa/evidence/review-disease-action-axis-2026-10-07.md`。
- **已知弱点(不静默,登记)**:① DC-4 生产烘焙侧接线归后续 story(真源点 + 测试已建,
  `ValidateActionId` 仍影子);② R1-19 = `NotImplementedException` 哨兵(待 disease_registry.json);
  ③ DC-6 对当前数据集零求值(烘焙期显式报出);④ MINOR/NIT 按一轮协议登记不修;
  ⑤ `清创`(O-9→10)归 10 的 GDD 轮。
