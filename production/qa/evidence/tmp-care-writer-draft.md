# CareApplied 写者落地 —— 过程草稿(增量落盘)

| 项 | 状态 | 证据(file:line) |
|---|---|---|
| 读 `CaseOpenWriter.cs` 结构(sealed / 构造注入 / 结果码 enum / Seq=-1 哨兵) | 完成 | unity/Assets/Sim/CaseOpenWriter.cs:71-161 |
| 读 `CareAppliedPayload` 四字段 | 完成 | unity/Assets/Sim.Contracts/Payloads/HistoryPayloads.cs:130-141 |
| 读 `WriterExistenceGate` 豁免条目 + 撤表先例 | 完成 | unity/Assets/Editor.Tools.Gates/WriterExistenceGate.cs:99-107,129 |
| GDD 照护事件化(起止事件 / 非 EnvMod / 非「在医馆」) | 完成 | design/gdd/disease-simulation.md:207-228 |
| GDD last_intervention 照护支(消费者侧) | 完成 | design/gdd/disease-simulation.md:1126-1133 |
| GDD 校验 13 ② t(止) ≥ t(起) · K ≤ 护理动作表 | 完成 | design/gdd/disease-simulation.md:344 |
| CARE_EVENTS_PER_TICK 密度界(值 *待定*) | 完成 | design/registry/entities.yaml:1776-1792 · disease-simulation.md:1784 |
| registry 载荷 schema 逐字 | 完成 | design/registry/entities.yaml:2186-2187 |
| phase 序数 0/1 的落地约定出处(全库无序数权威) | 完成 | unity/Assets/Sim/DiseaseSimulation/DiseaseCourse.cs:150-174 |
| actionId / caregiver 合法域 | 未找到权威定义(只查到「K ≤ 护理动作表」种类界,无上界值;caregiver 零定义)⇒ 仅非负校验 | entities.yaml:2186 · grep `护理动作表` 全库无表体 |
| 新建 `unity/Assets/Sim/CareAppliedWriter.cs` | 完成 | 见 §一 |
| 撤 `WriterExistenceGate` 的 CareApplied 豁免 + 撤表注释 | 完成 | 见 §一 |
| 新建测试 `care_applied_writer_test.cs` | 完成 | 见 §一 |
| ⚠️ 连带必改:`writer_existence_gate_test.cs:186-187` 以 CareApplied 作「陈旧豁免」探针样本 ⇒ 撤表后该断言必红,改点名仍挂表的 Kind | 完成 | 见 §一 |
| 编译 / 跑测试 | **未跑**(编排方统一做) | 见 §三 |

---

## 一、落地清单(file:line)

> ⚠️ **行号口径**:下列行号是我按落盘时的文件结构**估算**的(±3 行),交报告时被要求
> 不再读文件复核 ⇒ **未经 Read 校验**。锚点文本是准确的,可用 `grep -n` 定位。

### `unity/Assets/Sim/CareAppliedWriter.cs`(新建,约 235 行)

| 内容 | 锚点(精确) | 估算行号 |
|---|---|---|
| 文件头权威来源(registry payload_schema 逐字 / GDD :207-228 / :344 / :1784 / ADR-005/008/029/031) | 文件首行 `// 照护事件写者 ——` | :1-24 |
| 「有写者 ≠ 已接线」声明 | `⚠️ **有写者 ≠ 已接线**` | :45-47 |
| phase 序数落地约定声明(全库无序数权威) | `⚠️ phase 序数(必读)` | :40-43 |
| 登记项(actionId 上界 / caregiver 域 / 止不重复 / IsHost 门) | `⚠️ 登记项(本批不裁)` | :49-60 |
| 结果码 enum 七值 | `public enum CareAppliedWriteResult` | :70-98 |
| `NoDensityCap = -1` 哨兵(为何不用 0) | `public const int NoDensityCap = -1;` | :118 |
| 构造(4 依赖 + cap,全部 null / 负值 fail-loud) | `public CareAppliedWriter(` | :143-155 |
| `TryApply` 签名 | `public CareAppliedWriteResult TryApply(` | :177-178 |
| tick < 0 抛 | `if (tick < 0) throw` | :180 |
| **前置 ①** PatientId.None ⇒ `NoPatient` | `if (patient.IsNone)` | :185-186 |
| **前置 ②** phase ∉ {0,1} ⇒ `InvalidPhase` | `if (phase != DiseaseCourse.CarePhaseStart && phase != DiseaseCourse.CarePhaseStop)` | :189-190 |
| **前置 ③** actionId < 0 ⇒ `InvalidActionId` | `if (actionId < 0)` | :193-194 |
| **前置 ④** caregiver < 0 ⇒ `InvalidCaregiver` | `if (caregiver < 0)` | :195-196 |
| **前置 ⑤** 密度界 ⇒ `RateCapExceeded` | `if (_careEventsPerTickCap != NoDensityCap)` | :202-213 |
| **前置 ⑥** 止无起 ⇒ `StopWithoutStart` | `if (phase == DiseaseCourse.CarePhaseStop && !HasPriorStart(` | :219-220 |
| 载荷编码(禁 `new PayloadRef(`) | `_encoder.Encode(EventKind.CareApplied, payload)` | :225 |
| 事件头 Append(Seq = -1 哨兵) | `_eventSink.Append(new SimEvent(` | :230 |
| `HasPriorStart` 私有配对扫描 | `private bool HasPriorStart(` | :240-256 |

> 注:**「六个前置」中 ①②③④⑥ 在 `TryApply` 体内、⑤ 也在体内但含内联循环**;
> 序号与文件头 `核心机制` 注释里的 ①–⑥ 一一对应(文件头 :27-35)。

### `unity/Assets/Editor.Tools.Gates/WriterExistenceGate.cs`(改)

| 内容 | 锚点 | 估算行号 |
|---|---|---|
| 删除原豁免条目 `("CareApplied", "24 医馆与案头写者未落…")` | 原 :129(已删) | — |
| 新增撤表注释块(日期 2026-10-22 · 写者路径 `Sim/CareAppliedWriter.cs` · 同批提交纪律 · 「有写者 ≠ 已接线」· 正向断言载体指向测试文件) | `✅ 2026-10-22 撤 \`CareApplied\` 豁免` | :128-138 |

### `unity/Assets/Tests/EditMode/Sim/writer_existence_gate_test.cs`(改,1 处)

| 内容 | 锚点 | 行号 |
|---|---|---|
| 「陈旧豁免」探针样本 `CareApplied` → `CompoundTriggered` + 换样本理由注释 | `// ⚠️ 2026-10-22 换样本` / `Assert.IsTrue(stale.Any(e => e.Contains("CompoundTriggered"))` | :186-192 |

> 为什么必须改:该测试的探针 yaml 只声明 1 支 Kind,故**真豁免表全表**在探针上都算「陈旧」;
> 它点名 `CareApplied` 作为「须被抓到的样本」。撤表后 `CareApplied` 不再在表内 ⇒ 该断言**必红**,
> 且红的理由与「陈旧豁免判据是否生效」无关(样本不在表里了)⇒ 换一个**仍挂表**的样本。
> 选 `CompoundTriggered`(9 并发症触发写者,归 9 实现轮,实体见 gate :103)。

### `unity/Assets/Tests/EditMode/DiseaseSimulation/care_applied_writer_test.cs`(新建)

放此目录的理由:与 `care_lever_and_aggregator_test.cs`(照护杠杆 F4 消费侧)同目录,
同 namespace `DaYiJingCheng.Tests.Unit.Sim`(该目录既有文件已在用)。

**10 个 `[Test]`**(⚠️ 协调方说 9 个,实为 10 个 —— 以本条为准):

| # | 测试名 | 钉什么 |
|---|---|---|
| 1 | `test_careApplied_realBag_appendsExactlyOneHistoryEvent_roundTrip` | 真装配写入一条:结果码 `Applied` · 总流 +1 · 病史流 CareApplied +1 · Kind/StreamId=History · 事件头 `Patient == _patient` 且非 None · `Tick` · `Seq == 0`(发号)· 载荷 **四字段逐字段**经 `Decoder.Decode` 往返(tick/actionId/caregiver/phase) |
| 2 | `test_careApplied_phaseOutOfRange_noWrite` | phase = 2 / -1 / 99 逐一 ⇒ `InvalidPhase` 且**总流零增**、病史流零条(静默取值会让「止」被当「起」) |
| 3 | `test_careApplied_noneSentinel_noWrite` | `PatientId.None` ⇒ `NoPatient` + 零写入(高水位污染) |
| 4 | `test_careApplied_negativeActionOrCaregiver_noWrite` | actionId=-1 ⇒ `InvalidActionId`;caregiver=-5 ⇒ `InvalidCaregiver`;两条均零写入 |
| 5 | `test_careApplied_stopWithoutStart_noWrite` | 无「起」直接「止」⇒ `StopWithoutStart` + 零写入 |
| 6 | `test_careApplied_startThenStop_appendsTwo_tickNonDecreasing` | 先起后止 ⇒ 两条都写 · 总流 +2 · 两条 CareApplied · `t(止) ≥ t(起)` · 流上相位序列 == [起, 止](照护支派生量依赖可判别性) |
| 7 | `test_careApplied_densityCapExceeded_noWrite` | cap=1 时同 tick 第 2 条 ⇒ `RateCapExceeded` + 零写入;对照:默认 `NoDensityCap` 下同 tick 再发成功 |
| 8 | `test_careAppliedWriter_nullDependency_throws` | 四个依赖分别为 null ⇒ `ArgumentNullException` |
| 9 | `test_careAppliedWriter_invalidCap_throws` | cap = -2 ⇒ `ArgumentOutOfRangeException`(只接受 ≥0 或 -1 哨兵) |
| 10 | `test_careApplied_negativeTick_throws` | tick = -1 ⇒ `ArgumentOutOfRangeException`(与 CaseOpenWriter 同口径 fail-loud) |

写盘:本测试**零文件写入**(不触 `FsWriteBoundaryGate`)。

---

## 二、我自查出的可疑点(不确定是否错,但没时间验)

### 2.1 前置 ⑥「止须有可配对的起」—— 这是**推导**,不是 GDD 明写

- GDD 原文(design/gdd/disease-simulation.md:344)只有:
  「② **护理动作事件**:`t(止) ≥ t(起)`,`K ≤ |护理动作表|`」。
  entities.yaml:2186 同款措辞:「一条事件表示起或止,写入期校验 t(止) ≥ t(起)」。
- **GDD 全文没有一句说「没有起就不许有止」**,也没有定义「起止如何配对」(按什么键配对、
  是否跨 tick 可配、caregiver 是否入键)。⇒ 我的 ⑥ 是**把「比较式无从求值」推成「拒收」**,
  属**推导 + 一种取舍**,不是转录。
- 风险:若 24 的真实动作生命周期允许「只发止」(例如重连后补记 / 动作开始时玩家已离场),
  本写者会拒掉合法写入,而**没有任何门会提醒**。
- 我已把「配对键 = (病人, actionId),**不含 caregiver**」和「连续两条止不拒」写进文件头登记项
  (writer :55-57),但**「拒 vs 放过」这个方向本身没裁**。→ **建议协调方决定:是保留 ⑥,
  还是降级为「不拒 + 记一条警告」**。若保留,建议在 24 的 GDD 轮补一句「止须有起」。

### 2.2 `_streamEvents` 实时性 —— **源码级确认,未运行验证**

- 我读到 `unity/Assets/Sim/EventStream.cs:65`:`public IReadOnlyList<SimEvent> Events => _events;`
  ⇒ 返回的是**内部 List 的只读视图**(同一实例),`Append` 往同一 List 加 ⇒ 视图**会实时反映**
  刚 Append 的事件。这是源码读数,不是实测。
- ⚠️ **未验证的两点**:
  ① `EventStream.Clear()`(EventStream.cs:164)**体没读** —— 若它做的是 `_events = new List<...>()`
     而不是 `_events.Clear()`,则构造期捕获的视图会**变陈旧**,密度界与配对扫描同时失效
     (且**静默**:不会抛,只会永不命中)。
  ② 我的测试在 `Setup` 里传 `_bag.Stream.Events`(append 之前捕获)—— 若 ① 成立,测试仍会绿
     (因为 Setup 每次新 `Assemble`),**测试无法发现该缺陷**。⇒ 这是一处**测试覆盖不到**的缝。
- 同类前例:`CaseOpenWriter` / `CaseCloseWriter` 也走同一注入形态,故风险不是本件引入的;
  但本件是**第一个**在**同一次调用内先读流再写流**的写者(前置 ⑤⑥ 读 → Append 写),
  所以对实时性的依赖比它们更强。

### 2.3 `RateCapExceeded` 计数口径 —— GDD 原文是「**事件数**」,我的口径**忠实**

- GDD :1784 原文:「`CARE_EVENTS_PER_TICK` | **每病人每 tick 护理动作事件数上界**」;
  entities.yaml:1776 同名同措辞「每病人每 tick 护理动作事件数上界」。
- ⇒ 「不区分起止、起+止在同 tick 算 2」是**按字面**读的,不是我的引申。
- ⚠️ 但有一处**我未能核实**:GDD :1031 的复杂度表里同一旋钮被描述为「密度界」,并说
  「速率界(续照判定)另记」—— 我**没读 :1031 全文**,只知道它的摘要。若 :1031 对「事件数」
  有更细的定义(例如「一个动作的起止算一条」),我的口径就与之冲突。**未核实,标记为风险。**
- 另:旋钮值仍是 `*待定*`(GDD :1784 · entities.yaml `value: "待定"`)⇒ 我用 `-1` 哨兵 =
  默认不设界。**这意味着默认装配下 ⑤ 永不触发**,密度界实际上**未生效** —— 直到数值轮定值。
  这不是 bug,但**不应被读成「密度界已落地」**。

### 2.4 其它不确定点(时间不够,未验)

1. **`actionId` 上界**:`K ≤ |护理动作表|` 只界**种类**,而全库**找不到护理动作表本体**
   (24 的 `clinic-machine.md` 只有一句上游契约提到「只收护理动作事件」,无表)。⇒ 我只做 `≥ 0`。
   若 24 的表其实已存在于别处(我没全仓扫),这条登记项就是错的。
2. **`caregiver` 语义**:是不是 `player_id`?是否须与玩家 id 共用空间(ADR-006 注记)?
   **完全未查证**,只做 `≥ 0`。
3. **`CareAppliedPayload.Tick` 与事件头 `Tick` 重复存两处** —— registry 的 payload_schema
   明确列了 `tick: i64`,所以我照登记落字段(与 `PatientOutcomeChanged` 的「t_end 不落载荷」
   相反)。但这是**第二真源**(ADR-016 §一 同型病),若协调方认为应以事件头为准,
   则应改 registry 而非改写者。**我没有改 registry(任务禁止)**,只在此标注。
4. **`DiseaseVitalsService.cs:342 / :381` 的注释现已陈旧** —— 它们写「`CareApplied` 的写者未落
   (`WriterExistenceGate` 具名登记)」,撤表后为假。该文件在**禁改清单**里,我**未动**。
   建议协调方在下一轮修该注释(纯注释,不影响行为)。
5. **`writer_existence_gate_test.cs` 文件头 :18 的「24 条豁免」** 已过时(现 23 条),
   但那是**注释**,不是断言(`:274` 的断言只是 `IsNotEmpty` + 消息文案里也写「24 条」)。
   我**只改了 :186-192 的样本**,没改这两处文案 —— 断言不会红,但文案已失实。
6. **namespace 选择**:测试落 `DaYiJingCheng.Tests.Unit.Sim`,与同目录既有文件一致;
   但目录名是 `DiseaseSimulation`。该目录内两种 namespace 混存(见
   `catchup_replay_test.cs` 用 `DaYiJingCheng.Tests.DiseaseSimulation`)⇒ 无强制约定,我取多数派。

---

## 三、明确未验证项(禁借绿)

- **测试未跑。一条都没跑。** 绿/红**未知**。
- **未编译。** 以下是我**读签名**推断可编译、但**未经编译器确认**的点:
  - `_bag.Presence.AddPresent(patient, new WorldPos(0,0,0))` —— 抄自 `case_open_writer_test.cs` 的既有用法;
  - `_bag.PatientSpawner.SpawnNext(diseaseId: 0, tick: 0)` —— 同上;
  - `_bag.Decoder.Decode<CareAppliedPayload>(EventKind.CareApplied, evt.Payload)` ——
    按 `IPayloadDecoder.cs:45-57` 的签名;`in PayloadRef` 传属性值(编译器生成临时量)应合法;
  - `DiseaseCourse.CarePhaseStart/Stop` 为 `public const int`(`DiseaseCourse.cs:169,174`),
    `DiseaseCourse` 为 `public sealed class`(`:79`),`Sim` asmdef 内同程序集可见 ⇒ 应可引用。
- **未跑构建前门**(`WriterExistenceGate` 探针轮 / `AssemblyGates.RunAll`)⇒
  **「门现在绿」是推断,不是观测**。推断依据:写者文件内含
  `_encoder.Encode(EventKind.CareApplied, payload)`,而门的正则 `Encode\(\s*EventKind\.(\w+)`
  会命中 ⇒ `written` 含 `CareApplied`,且它已不在 `ExemptKinds` ⇒ 判据「缺写者」与判据①均不触发。
  **这是源码级论证,不是构建日志证据**(承 CLAUDE.md §构建前门:绿 ≠ 门跑了)。
- **未核实** `EventStream.Clear()` 的实现(见 2.2 ①)。
- **未核实** GDD :1031 复杂度表对「护理动作事件数」的完整措辞(见 2.3)。
- **未全仓扫**护理动作表本体是否存在于我未查的路径(见 2.4 ①)。

## 四、本次改动的文件清单(绝对路径)

1. `/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Sim/CareAppliedWriter.cs`(新建)
2. `/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Editor.Tools.Gates/WriterExistenceGate.cs`(改:撤豁免 + 撤表注释)
3. `/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Tests/EditMode/Sim/writer_existence_gate_test.cs`(改:陈旧豁免样本 CareApplied → CompoundTriggered)
4. `/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Tests/EditMode/DiseaseSimulation/care_applied_writer_test.cs`(新建)
5. `/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/production/qa/evidence/tmp-care-writer-draft.md`(本文件)

**未改**:`DiseaseCourse.cs` / `DiseaseCourseBook.cs` / `DiseaseVitalsService.cs` / 任何 GDD / `entities.yaml`。
**未 commit、未 push。**
