# Code Review — sprint-19 收口(lead-programmer)

- **范围**:`554e2cc^..HEAD`(`554e2cc` S19-1 + TD-persist-03 · `bd4e6a4` S19-2 · `ff853fc` S19-3 · `f398cb2` S19-4 · `2847a4a` / `4d468a5` S19-5 记账)
- **评审日**:2026-10-25
- **评审方式**:只读勘察(读源码 + 读测试源码 + 只读命令)。**本轮未运行 Unity 测试**;全部回归数字引自已落盘证据件,**不标 Proven**。
- **草稿**:`production/qa/evidence/tmp-review-sprint19-lead-programmer.md`(保留)

---

## 一、双列结账表(逐改动单元)

| ID | Landed | Proven | Branch | File:line | Proven-by |
|---|---|---|---|---|---|
| S19-1a `SaveService` 构造 3→7 | ✅ | ✅ | — | `unity/Assets/Sim.Codec/SaveService.cs:88-114` | `save_composer_wiring_test.cs:466` `test_assemble_saveService_checkpointFailsClosed_beforeAnyAnchor` + 装配根 `CompositionRoot.cs:449-456` 七参实参。**🔁 2026-10-25 订正**:原引 `save_slot_contract_test.cs:451-482` 与测试名 `..._withoutNarrativeContext` **均为错引** —— 该测试实际只在 `save_composer_wiring_test.cs`,而 `:451-482` 是 `test_load_olderSaveVersion_doesNotLockSlot`。原文不删,此处订正 |
| S19-1b `Checkpoint` 双门 + compose + 后置高水位 | ✅ | ✅ | ⚠️ 4/6 | `SaveService.cs:234-253` | `save_slot_contract_test.cs:311`(无 compose 抛)/ `:323`(compose 返 null 抛)/ `:477`(无 narrativeContext 抛)/ `:183` 闭合回路 |
| S19-1c `SaveOnExit` 同组版不同门 | ✅ | ⚠️ 部分 | ✗ 2/4 | `SaveService.cs:268-282` | `:641`(不推高水位)/ `:649`(无 compose 抛)/ `:663`(值门不适用)。**未覆盖**:无 narrativeContext ⇒ 抛;compose 返 null ⇒ 抛 |
| S19-1d `Load` 迁移链三分支 | ✅ | ⚠️ 部分 | ⚠️ 5/7 | `SaveService.cs:296-356` | `:489`(无 narrativeContext)/ `:373`(无 replaySink)/ `:446`(低版本 fail-closed)。**未覆盖**:高版本(前向)分支、无 foldedRowSink 分支 |
| S19-1e `ParseBody` 事件段 + 折叠段 | ✅ | ✅ | ✅ 2/2 | `SaveService.cs:365-383` | `save_replay_roundtrip_test.cs:83`(真字节往返);缺折叠段 ⇒ 抛由 `:377-381` 分支承担 |
| TD-persist-03a `SimEventListCodec.ReadWithinBody` | ✅ | ✅ | ✅ 3/3 | `unity/Assets/Sim.Codec/SimEventListCodec.cs:69-86` | `save_slot_contract_test.cs:625`(count 负抛)/ `:676`(超上界抛)/ 往返 |
| TD-persist-03b `FoldRowCodec` 36B + outcome 域拒 | ✅ | ✅ | ✅ 6/6 | `unity/Assets/Gameplay.Boot/FoldRowCodec.cs:51-133` | `fold_row_codec_test.cs:32,48,72,90,100,109,118,131`(黄金字节 / 0 拒 / 未知拒 / 负 count / 超容 / 未读尽 / 空段) |
| S19-1f 接线 compose + replaySink + foldedRowSink | ✅ | ⚠️ 形状已证 · 生产不可达 | — | `unity/Assets/Gameplay.Boot/CompositionRoot.cs:406-455` | `save_composer_wiring_test.cs:221-300` 形状;⚠️ 生产 `narrativeContext: null`(`:453`)⇒ 整条路径生产期**必抛**,零可达 |
| S19-2a `IPlayerMotor` 四成员 | ✅ | ✅ | — | `unity/Assets/Gameplay.Presentation/Player/IPlayerMotor.cs:50-74` | `player_motor_test.cs:69` `test_motor_interface_members_areComplete`(反射) |
| S19-2b 边沿锁存器 | ✅ | ✅ | ✅ 3/3 | `PlayerController.cs:117-127,330-336,394-411` | `player_motor_test.cs:112,136,161,179,199`;反证 F1/F2/F3(证据件 §四,3/3 全红) |
| S19-2c 死字段 `_cellDetector` 删除 | ✅ | ✅ | — | `PlayerController.cs:126-127` | `grep -rn "_cellDetector" unity/Assets --include=*.cs` ⇒ 仅本注释行 |
| S19-3a `LogiPose`/`TameState`/`ITameable`/`TameableFields` | ✅ | ✅ | ✅ 4/4 | `unity/Assets/Sim.Contracts/LogiPose.cs:32-113` | `logi_pose_test.cs:61,71,98,119,129`;反证 M4/M5''/M7 |
| S19-3b `EnemyLocomotion.Advance` F-27-4 沿路径 | ✅ | ✅ | ⚠️ 5/6 | `unity/Assets/Sim/EnemyLocomotion.cs:63-110` | `logi_pose_test.cs:163,184,203,224,240,280`;反证 M1'/M2/M3/M6。**未覆盖**:空 `path` 分支(走「路径耗尽」同一 break,acc 被清零) |
| S19-4a `DeferSlots` → Dictionary + 删直改方法 | ✅ | ✗ | — | `unity/Assets/Sim/EventBudget.cs:76-108` | `deferred_threat_slot_test.cs:190` **仅断言方法名不存在 + 字段类型** —— 字段是 `public readonly Dictionary`,**直接改字典不受任何机械阻拦** ⇒ 「唯一写入路径」未被钉住(自指判据)。见 §四 W1 |
| S19-4b `FoldDeferEvent` 三类 fail-loud | ✅ | ✅ | ⚠️ 3/4 | `EventBudget.cs:169-204` | `deferred_threat_slot_test.cs:159`(越界)/ `:171`(双占)/ `:180`(清未挂起)。**未覆盖**:`eventKey < 0` 折叠期分支(`:185-186`)—— 仅写者侧有测 |
| S19-4c `ReconstructDeferSlots` | ✅ | ✅ | — | `EventBudget.cs:210-218` | `deferred_threat_slot_test.cs:92,100,107,122,140`(重放两次逐元素相同);反证 M9(丢一半事件 ⇒ 4 红) |
| S19-4d `ValidateReconstructable` 真判据 | ✅ | ✅ | ✅ 2/2 | `EventBudget.cs:229-242` | `deferred_threat_slot_test.cs:206,214`(槽值漂移 + 幻影槽);反证 M4(恒 true ⇒ 1 红) |
| S19-4e `DeferredThreatSlotWriter` 两 Kind 共写者 | ✅ | ✅ | ✅ 4/4 | `unity/Assets/Sim/DeferredThreatSlotWriter.cs:62-136` | `deferred_threat_slot_test.cs:231,249,277,286,294,301`;反证 M5/M6/M7 |
| S19-4f `WriterExistenceGate` 撤两行豁免 | ✅ | ⚠️ 表内容已证 · 门红能力 NOT-RUN | — | `unity/Assets/Editor.Tools.Gates/WriterExistenceGate.cs:127-140` | `deferred_threat_slot_test.cs:326` 反射断言「表内无这两个名字」。⚠️ **该测证明的是「表不含名字」,不是「门会红」**;证据件 M8 变异的也是这张表 ⇒ 红的仍是同一反射测。**门的负向能力无夹具** |

---

## 二、ADR / 架构约束合规(逐条)

| 约束 | 结论 | 证据 |
|---|---|---|
| **门 A**:`Sim` 引用集恰 = `{BCL, Sim.Contracts}` | **COMPLIANT** | `unity/Assets/Sim/Sim.asmdef:4` = `[ "Sim.Contracts" ]`,`:13` `noEngineReferences: true`;`grep -rn "using UnityEngine\|using Unity.Entities" Sim/ --include=*.cs` ⇒ 1 命中且为注释(`Sim/Clinic/ClinicRoomExtraction.cs:332`) |
| **ADR-025 §①**:`Sim.Contracts` 恰 = BCL | **COMPLIANT** | `Sim.Contracts/Sim.Contracts.asmdef:4` = `[]`,`:13` `noEngineReferences: true` |
| **ADR-029**:`Sim/` 内 `new PayloadRef(` | **COMPLIANT** | `grep -rn "new PayloadRef(" Sim/ --include=*.cs` ⇒ 全部命中在 `//` / `///` 注释内,零代码调用 |
| **ADR-031**:`Sim` 解码走 `IPayloadDecoder` | **COMPLIANT** | `EventBudget.cs:169,181,195` 形参注入;`HistoryFolder.cs:79,101`;`PatternDetector.cs:187+` |
| **ADR-007 §四**:世界级事件 `PatientId.None` | **COMPLIANT** | `DeferredThreatSlotWriter.cs:104-105,131-132` 两处 |
| **ADR-016 §二**:`LogiPose` 零 `Transform` / `Vector3` | **COMPLIANT** | `LogiPose.cs` 全文零引擎类型;asmdef 结构性保证 |
| **ADR-024**:`Kind` 路由单一真源 | **COMPLIANT** | `design/registry/entities.yaml:2595-2622` 两条 `stream: history`;`Sim/StreamRouting.g.cs:38-39` 两行 `=> StreamId.History` 一致 |
| **ADR-020 §四**:玩家位移不进流 | **COMPLIANT** | `PlayerController.cs:394-411` 只暴露 `Position` / `Cell` / 锁存器;`ActorCellEntered` 主路径未改 |
| **ADR-020 §Key Interfaces**:`Tick(float deltaTime, …)` | **DRIFT** | `PlayerController.cs:383-386`:`Tick` 体 = `Move(input.Value)`,`deltaTime` **被丢弃**;`Move` 自取 `Time.deltaTime`(`:381` 注释自陈)。接口形状在,语义未兑现 |
| 测试写盘边界 | **COMPLIANT** | `save_slot_contract_test.cs:34` `Path.GetTempPath()`;`FsWriteBoundaryGate` 已接进 `AssemblyGates.RunAll`(`AssemblyGates.cs:155`) |

---

## 三、Standards 合规

| 项 | 结论 | 证据 |
|---|---|---|
| 公开 API doc 注释 | ✅ | 五个新文件 public 成员全部带 `///`(抽查 `LogiPose.cs` 16 个 public / `FoldRowCodec.cs` 6 个 / `DeferredThreatSlotWriter.cs` 7 个,无裸 public) |
| 方法 ≤ 40 行 | ✗ 2 处 | `SaveService.cs:296-356` `Load` = **61 行**;`EventStream.cs:120-162` `Restore` = **43 行** |
| 圈复杂度 < 10 | ⚠️ **NOT-RUN** | 未做机械计数;`FoldDeferEvent`(switch 2 案 + 4 判)与 `Advance`(1 if + 1 while + 2 if)目测 < 10,`Load` 约 8 —— **未实测,不作判据** |
| 依赖注入 | ✅ | 全部走构造函数 / 形参(`SaveService` 七参、`DeferredThreatSlotWriter` 两参、`FoldDeferEvent` 注入 decoder);零静态单例 |
| 数据驱动 | ⚠️ 1 处 | `DEFER_MAX` / `ENEMY_SPEED` 走参数注入 ✅;但 `DeferredThreatSlotWriter.cs:65,68` 的 `ReasonReissued=0 / ReasonVoided=1` **序数由本类首定**,`entities.yaml` 的 `payload_schema` 只写「枚举(补发|作废)」无序数 ⇒ 线格式由代码常量定义 |
| 系统暴露接口 | ✅ | `ISaveService`(`Sim.Codec/ISaveService.cs:29`,反射断言 `folded_predicate_test.cs:618`)/ `IPlayerMotor` / `ITameable` |

---

## 四、新缺陷

| # | 级别 | 描述 | 落点 |
|---|---|---|---|
| W1 | **WARNING(已验实 · 必修)** | `BudgetState.DeferSlots` 是 `public readonly Dictionary<int,int>` —— 「唯一写入路径 = FoldDeferEvent」**无机械约束**;`test_budget_deferSlots_noDirectMutationPath` 只查方法名 + 字段类型,是**自指判据**。**结论:S19-4 判据①「第二真源拆除」在机械面上未成立**(`Landed ✅ / Proven ✗`) | `EventBudget.cs:82`;`deferred_threat_slot_test.cs:190-203` |
| W2 | WARNING | `reason` 序数无 registry 定义、无机械三处对齐断言(类头自陈「本件首定」) | `DeferredThreatSlotWriter.cs:26-29,65,68` |
| W3 | WARNING(读源判「不重复」· **NOT-RUN**) | **checkpoint → load → checkpoint 二次折叠**无夹具。读源码判断**不产重复行**,但无回归保护 | `SaveComposer.cs:96-152`;`EventStream.cs:143-149` |
| W4 | **WARNING(已验实 · 必修)** | `SaveOnExit` 写 `save_0.bin`,与玩家手动槽 0 **同一路径**;不推高水位 ⇒ 互相覆盖。ADR-010 §六 未定义退出保存的槽位身份 | `SaveService.cs:273-281,403-405`;`adr-010:366` |
| W5 | **WARNING(已验实 · 必修)** | 生产装配 `narrativeContext: null` ⇒ `Checkpoint` / `SaveOnExit` / `Load` 生产期**一律抛**。所有「接线已完成」须读作**形状已落、生产不可达** | `CompositionRoot.cs:453` |
| W6 | MINOR | `Load` 61 行 / `Restore` 43 行超 40 行标准 | `SaveService.cs:296-356`;`EventStream.cs:120-162` |
| W7 | MINOR | `EnemyLocomotion` 路径耗尽时 `acc = Fix.Zero` —— **F-27-4 无此定义**,是本件发明的收口(文件头自陈);`EnemyPathing.StepAccumulator` 仍在盘(直线逼近),两套步进语义并存(已登记残余) | `EnemyLocomotion.cs:83-95`;`Sim/EnemyPathing.cs:181`;`tech-debt-register.md:1947` |
| W8 | MINOR | `FoldDeferEvent` 头注声明「三类流损坏」,代码实抛**四类**(多 `eventKey < 0`) | `EventBudget.cs:33-35` vs `:185-186` |
| W9 | MINOR | `ITameable` 为 interface,`TameableFields` 是 struct ⇒ 每次经接口访问都装箱(27 的逐 tick 路径若走接口需留意) | `LogiPose.cs:82-113` |
| W10 | INFO | `SpliceBack` 每条事件拼 `string` 身份键(`IdentityKey`)—— 存档非热路径,可换成 `(Kind,Patient,Tick,Seq)` 元组 | `SaveComposer.cs:196-198` |

### 三条高危的核实结论

**W1 — 已验实。** `BudgetState.DeferSlots` 声明为 `public readonly Dictionary<int, int>`(`EventBudget.cs:82`)。
`readonly` 只禁字段重绑,**不禁止 `state.DeferSlots[k] = v` / `.Remove(k)` / `.Clear()`**。
全仓无 `InternalsVisibleTo` 作用于 `Sim`(`grep -rn "InternalsVisibleTo" Sim/ Sim.Contracts/` ⇒ 仅 `Sim.Contracts/Fix.cs:8` 一处注释),无属性包装、无只读视图、无 b 门扫 `DeferSlots` 赋值点。
⇒ **结论:S19-4 判据①「第二真源拆除」在机械面上未成立。** 唯一约束是文件头注释与 `test_budget_deferSlots_noDirectMutationPath`(该测只查「`AddToDefer` / `ClearDefer` 方法名不存在」+「字段类型是 Dictionary」)—— **断言的是当前实现长什么样,不是禁止外部直改**。

**W3 — 读源码判「不重复」,但无夹具 ⇒ NOT-RUN。**
依据:`Fold` 只有在 `Collect` 收到该病人的 `outcome` 记录时才产行(`HistoryFolder.cs:141-168`),否则 `BuildRows` 空 ⇒ `SaveComposer.cs:150` 的 `Rows.Count == 0 ⇒ continue` 走恒等。两条路径分别看:
- **同会话连存两次**(无 load):`SaveComposer` 是纯函数、不改 `EventStream`;`existingFoldedRows` 取自 `stream.FoldedRows`,该表**只由 `Restore` 填充**(`EventStream.cs:60,158`),纯 Append 会话恒空 ⇒ 两次各产同一批行、不叠加。
- **checkpoint → load → checkpoint**:load 后存档里该病人的病史行已被 `SpliceBack` 抹除(`SaveComposer.cs:180-196`),`Restore` 后流中无其 onset / outcome ⇒ 再折得零行 ⇒ `rowsAccum` 只含 `existingFoldedRows`。
⇒ **判断:不重复。但这条链无任何夹具**(`save_replay_roundtrip_test` 只跑一遍往返),若将来 `Fold` 或 `SpliceBack` 变更,无回归保护。登记为**测试缺口 + NOT-RUN**,不作 BLOCKING。

**W4 — 已验实:同一路径。**
`GetFinalPath(slot) = Path.Combine(_basePath, $"save_{slot.SlotSeq}.bin")`(`SaveService.cs:403`);
`SaveOnExit` 固定 `new SaveSlot(0)`(`:274`)⇒ 两者**落到同一个 `save_0.bin`**。
`SaveOnExit` 又刻意不调 `OnCheckpointCommitted`(`:281`),故 `_nextWritableSeq` 不推进 ⇒ 台账「只进不退」**不保护该文件**。
现有测试反向**钉住了这个共享**:`save_slot_contract_test.cs:645,674` 断言 `SaveOnExit` 后 `save_0.bin` 存在,`:341` 又对同文件名做 Checkpoint 断言。
ADR-010 §六 只写「退出保存 = 退出钩子,**非玩家可选槽位**」(`adr-010:366`),**未定义其槽位身份**,也未规定手动槽是否从 1 起。
⇒ 结论:**功能级数据覆盖风险成立**(退出存档与玩家槽 0 互覆),且**缺一条 ADR 级裁定**。列必修。

---

## 五、未及核查项(如实 · 禁借绿)

| # | 项 | 为什么没核 | 影响 |
|---|---|---|---|
| 1 | **Unity 全量测试未由本 agent 跑** | 本轮只读勘察 | 全部回归数字引自已落盘 XML 与证据件,**不标 Proven**;若 XML 造假本报告不能发现 |
| 2 | **26 轮证伪未逐轮重演** | 轮次预算 | 采信各证据件表格;已核轮数 / 基准哈希 / 还原方式 / `git checkout` 禁令遵守 |
| 3 | **圈复杂度未机械计数** | 轮次预算 | `FoldDeferEvent` / `Advance` / `Load` 三处均 **NOT-RUN** |
| 4 | **W1 的机械屏障未穷尽核查** | 轮次预算 | 见 W1 描述:未查 `InternalsVisibleTo` 等旁路 ⇒ 结论限于「未发现机械约束」 |
| 5 | **W3 二次折叠路径未证** | 需真跑 checkpoint→load→checkpoint | **NOT-RUN**,不得读作「已确认有缺陷」 |
| 6 | **IL2CPP 跨平台逐位一致** | 本机仅 x64,CI 缺凭证(`TD-ci-01`) | 不得声称 |
| 7 | **账本树 `tests/integration/persistence-service/` 不存在** | 已知差异 | 不构成证据缺口 |

**补充的未及核查项(本 agent 勘察所得,与上表不重复)**:

8. **`WriterExistenceGate` 的「会红」能力 NOT-RUN** —— 无探针轮(插无条件 `errs.Add` ⇒ 要求构建 `Failure`)。现有 `test_gate_threatKinds_notInExemptTable` 与证据件 M8 都只动**豁免表内容**,证明的是「表里没有这两个名字」,不是「门能因缺写者转红」。
9. **`SaveService` fail-closed 分支未全覆盖** —— 未覆盖:`SaveOnExit` 在 `narrativeContext == null` 时抛;`SaveOnExit` 在 compose 返 null 时抛;`Load` 在 `SaveVersion > CurrentSaveVersion`(前向)时抛;`Load` 在 `foldedRowSink == null` 时抛。
10. **`FoldDeferEvent` 的 `eventKey < 0` 折叠期分支未覆盖**(`:185-186`;仅写者侧 `test_writer_defer_negativeEventKey_noWrite` 覆盖)。
11. **`EnemyLocomotion.Advance` 空 `path` 分支未覆盖** —— `path.Count == 0` 时走 `next >= path.Count` ⇒ `acc` 被清零,与「路径耗尽」同码,无专属夹具。
12. **S19-5 记账类提交未审** —— `4d468a5` / `2847a4a` 为 docs 提交,本轮只核了其声称的回归三元组(基线 `4867/4832/0/35` @ `2026-10-25 07:19` · `s194` · `6198671`;`6198671` 实测为 HEAD 祖先 ✅),**未复算全名级差集**。
13. **`BudgetParams` / `ENEMY_SPEED` 的数据来源未追** —— 未核这些值是否真从 `assets/data/*.json` 装载(只确认了「以参数注入、非硬编码在算法内」)。

---

## 六、Verdict: **CHANGES REQUIRED**

**必修(BLOCKING 面)**:

1. **W1 — S19-4 判据①「第二真源拆除」在机械面上未成立**
   `BudgetState.DeferSlots` 是 `public readonly Dictionary<int,int>`,外部可绕过 `FoldDeferEvent`
   直改;`test_budget_deferSlots_noDirectMutationPath`(`deferred_threat_slot_test.cs:190-203`)
   只断言「方法名不存在 + 字段类型 = Dictionary」= **自指判据**(把当前实现写进断言)。
   修法二选一:(a) 字段收 `private` + 只读视图 `IReadOnlyDictionary`;(b) 补一条**行为判据**
   —— 外部直改字典后 `ValidateReconstructable` 必须 `false`。

2. **W5 — 生产路径不可达须在所有「接线已完成」表述处显式标注**
   `CompositionRoot.cs:453` 传 `narrativeContext: null` ⇒ `Checkpoint` / `SaveOnExit` / `Load`
   生产期一律抛。S19-1 的完成度须读作**「形状已落、生产不可达」**,不得读作「存档可用」。

3. **W4 — `SaveOnExit` 与手动槽 0 同路径**
   `SaveService.cs:273-281` 写 `save_0.bin`,与玩家手动槽 0 同一 `GetFinalPath`;
   且不推高水位 ⇒ 互相覆盖。ADR-010 §六 未定义退出保存的槽位身份 ⇒ 须裁或立债。

**建议(非阻塞)**:W2(reason 序数三处对齐)/ W3(补二次折叠夹具)/ W6(`Load` 61 行 / `Restore` 43 行拆分)/
W7(`acc = Fix.Zero` 非 F-27-4 定义)/ W8(头注「三类」实抛四类,订正措辞)/ W9 / W10。

**ADR 合规**:门 A / ADR-025 / ADR-029 / ADR-031 / ADR-007 §四 / ADR-016 §二 / ADR-024 / ADR-020 §四
**全部 COMPLIANT**;唯一 **DRIFT** = ADR-020 §Key Interfaces 的 `Tick(float deltaTime, …)`
(`PlayerController.cs:383-386` 丢弃 `deltaTime`,`Move` 自取 `Time.deltaTime`)。

**正向观察**:S19-3 / S19-4 的证伪轮设计质量高(S19-2 的 F1 首版零红被自查为假绿夹具并修正;
S19-3 的 M1「插在 return 后」被自查为不可达变异并改 M1');`FoldRowCodec` 的黄金字节 + outcome 域拒
是「解码期即拒」的教科书形态;`EventStream.Restore` 的「三分化」(绕 CAP / 留去重单调 / Seq 不重发)
把三条流不变量的取舍写进了 doc 而非埋在代码里。

---

## 七、修复轮结账表(双列 · 2026-10-25)

> 承 `code-review` skill Phase 8b:`Landed`(改动在盘)与 `Proven`(改对了且被可复算判据钉住)**正交**。
> 一行两列皆 ✅ 才算结清。**本轮未跑其判据 ⇒ 不得标 Proven ✅**。

| ID | 级别 | Landed | Proven | File:line | Proven-by(可复算判据) |
|---|---|---|---|---|---|
| **W1** | WARNING 必修 | ✅ | ✅ | `Sim/EventBudget.cs:88,91,94` | ① `deferred_threat_slot_test.cs` `test_budget_deferSlots_publicSurfaceIsReadOnly`(反射:属性类型 = `IReadOnlyDictionary` / 无 setter / 字段 private);② `test_budget_deferSlots_onlyFoldDrivesContent`(行为:绕过 `FoldDeferEvent` 直改后 `ValidateReconstructable` 必 false);③ **反证**:把公开面退回 `Dictionary` ⇒ ① 红(证伪轮 `W1-publicSurfaceBackToMutableDictionary`,实跑 `85/84/1`) |
| **W4** | WARNING 必修 | ✅ | ✅ | `Sim.Codec/SaveService.cs:308,268-282,403-405` | ① `save_slot_contract_test.cs:656+` `test_saveOnExit_doesNotCollideWithPlayerSlotZero`(字面量 `"save_exit.bin"` vs `"save_0.bin"`,不引被测常量);② **反证**:把 `ExitSaveFileName` 改回 `"save_0.bin"` ⇒ **3 红**(`doesNotCollideWithPlayerSlotZero` + `outsideNarrativeContext_stillWrites` + `writesWithoutAdvancingSlotHighWatermark`),证伪轮 `W4-exitSaveCollidesWithSlotZero`,实跑 `85/82/3`。⚠️ **连带红,非噪声** —— 三条夹具从三个角度钉同一文件名契约且全用字面量 ⇒ 常量一改必然三条全红;每条都是真断言在变异下失败 |
| **W5** | WARNING 必修 | ✅ | ✅ | `Gameplay.Boot/CompositionRoot.cs:507`(`narrativeContext = null`)+ `:1014,1047`(两纯函数保留但零接线) | **两条变异各守一面,不可互替**:<br>**W5-a(安全属性)** `test_assemble_savePath_narrativeContextStaysFailClosed`(台账 `false` **且** 行为「Checkpoint 必抛」);反证 = 接回累积钩子 ⇒ 3 红(`W5a-failClosedRevertedToCumulativeHook`)<br>**W5-b(预备件逻辑)** `test_narrativeAnchor_caseClosed_isTrue` 等六条;反证 = 停用锚点判定 ⇒ 红(`W5b-anchorCheckDisabled`)<br>⚠️ 首版**只留 a 删了 b** —— 那是错的:「留待 10」≠「不需证伪」,要复用的算子更须被钉住 |
| **W6** | MINOR | ✗ | ✗ | `SaveService.cs:296-356`(61 行)/ `EventStream.cs:120-162`(43 行) | **未修** —— 拆分属重构,本轮只做必修三条。如实登记 |
| **W7** | MINOR | ✗ | ✗ | `EnemyLocomotion.cs:83-95` | **未修** —— `acc = Fix.Zero` 的语义须回 27 的 GDD 裁定,不在修复轮权限内 |
| **W8** | MINOR | ✗ | ✗ | `EventBudget.cs:33-35` | **未修** —— 头注「三类」实抛四类,措辞订正留待下轮 |
| **W2 / W3 / W9 / W10** | WARNING / MINOR / INFO | ✗ | ✗ | 见 §四 | **未处置** —— 均为建议项,非本轮必修面 |

### W5 的裁定轨迹(必读 · 本轮最重要的一条)

| 阶段 | 形态 | 结果 |
|---|---|---|
| ① 原状 | `narrativeContext: null` | fail-closed,存档**不可用**但**安全** |
| ② 评审轮按用户前一裁定 | 接 `() => HasNarrativeAnchorEvent(stream.Events)` | 「形状已落」→ 但见 ③ |
| ③ 字面兑现后自查 | **累积语义** vs GDD 规则十四的**瞬时**语义 | **AC-7a-19 (c) 安全门恒开 = fail-open** |
| ④ **用户 2026-10-25 裁定「丙」** | 接线回退 `null`;瞬时源归 10 | 回到 ① 的状态,**但是「评估过、试过、被证伪、按裁定回退」的终态** |

**为什么这是本轮最重要的发现**:②→③ 这一步证明了「字面兑现用户裁定」**不等于**「满足 AC」——
用户裁定的选项描述里**没有覆盖「累积 vs 瞬时」这一维**,而那一维恰好是安全门的全部意义所在。
若不做 ③ 的自查,`AC-7a-19 (c)` 会以「测试全绿」的形态失效 —— 又一个假绿形态。

**W5 的两条纯函数保留理由**(不是遗留垃圾):
① 六条正反夹具已就位,删掉等于销毁「为什么不能这么接」的教训;
② 10 落地时可复用其 Kind 判定。**但零生产调用点**,任何人接线前须先解决累积 vs 瞬时。

#### §七.1 「红集超预期」的归因(首轮实跑,非假绿)

首轮实跑时 W5 与 W4 各红了 3 条(预期 1 条)。**逐条归因如下,均为连带红**:

| 变异 | 预期 | 实得 | 归因 |
|---|---|---|---|
| W5(旧靶:锚点判定行) | `test_narrativeAnchor_caseClosed_isTrue` | +`patternRecognized_isTrue` +`anchorAfterNonAnchors_isTrue` | 变异把**整行 `if`** 恒假,该行同时承载两个 Kind 判定;第三条夹具的锚点也是 `CaseClosed`。**变异粒度比预期粗** |
| W4 | `test_saveOnExit_doesNotCollideWithPlayerSlotZero` | +`outsideNarrativeContext_stillWrites` +`writesWithoutAdvancingSlotHighWatermark` | 三条夹具**全用字面量 `"save_exit.bin"`** ⇒ 常量一改全部失效。**这正是修掉自指判据后的应有行为**(判据已与被测常量解耦) |

⇒ **假绿的定义是「该红的没红」;此处是「该红的红了、额外也红了」,方向安全。**

#### §七.1b W5 拆成两条变异(用户质疑后补 · 最终有效证据)

**用户 2026-10-25 追问**:「W5 的守卫改回『锚点判定行』和留待 10 有什么区别?」

**这一问戳中一处真实的漏做**。首版在裁定丙后把锚点行变异 `W5-anchorCheckDisabled` **删掉**,
只留接线行变异,理由写的是「纯函数零生产调用点 ⇒ 判别力已转移」。该理由**只对了一半**:

| | 守什么 | 红了证明什么 |
|---|---|---|
| **W5-a** 接线行 `:507` | 生产路径 fail-closed(`AC-7a-19 (c)`) | **今天的安全属性成立** |
| **W5-b** 锚点行 `:1020` | 纯函数的 Kind 判定逻辑 | **明天 10 复用的那部分是**对的 |

**两者都能让测试红,形式判据相同,证明的东西完全不同。**
「留待 10」**不等于**「不需要证伪它」—— 恰恰相反:要交给别人复用的算子更须被钉住,
否则 10 接手时拿到的是一个从未被证伪过的东西。
⚠️ 而且首轮那次锚点行变异虽实得 3 红,却因 §七.2 的窗口违规**整体作废** ⇒
在补 W5-b 之前,那六条纯函数夹具的判别力**今天一条有效证据都没有**。

**补跑后的有效结果(2026-10-25,`--spec` 全量重跑)**:

```
══ 证伪轮通过:4 个变异全部恰如预期 ══

W5a-failClosedRevertedToCumulativeHook :507  → 3 红(主判据 ✅ 在红集内)
   test_assemble_savePath_narrativeContextStaysFailClosed
   test_assemble_saveService_checkpointFailsClosed_beforeAnyAnchor
   test_assemble_withSaveBasePath_constructsSaveServiceWithFoldedPredicate
W5b-anchorCheckDisabled             :1020 → 3 红(主判据 ✅ 在红集内)
   test_narrativeAnchor_caseClosed_isTrue
   test_narrativeAnchor_patternRecognized_isTrue
   test_narrativeAnchor_anchorAfterNonAnchors_isTrue
W1-publicSurfaceBackToMutableDictionary :91 → 1 红(恰如预期)
W4-exitSaveCollidesWithSlotZero      :308  → 3 红(主判据 ✅ 在红集内)

还原:SHA-256 逐字节校验通过
受保护路径指纹未变:unity/Assets/AddressableAssetsData, unity/Assets/Audio
```

**三条连带红的归因(均为真红,非噪声)**:
- W5a:三条装配级夹具都断言「钩子为 null」⇒ 接回累积钩子后全部失效。
- W5b:整行 `if` 同时承载两个 Kind 判定,第三条夹具的锚点也是 `CaseClosed`。
- W4:三条 `SaveOnExit` 夹具全用字面量 `"save_exit.bin"` ⇒ 常量一改全部失效。

⇒ `exactRed=false` 的语义 = 「`expectRed` 里的必须在红集内,**不锁死红集大小**」;
不把连带红扩进 `expectRed`,是为了保住「哪条是这条变异的主要靶」这一信息。

#### §七.2 一次流程违规(如实登记)

首轮证伪轮跑动期间,我编辑了被变异文件 `CompositionRoot.cs`(改 doc 注释),违反
`tools/falsify/README.md` §并发读取禁令。随后 `TaskStop` 杀掉任务,而**还原步骤未执行** ——
`CompositionRoot.cs:502` 残留 `Func<bool> narrativeContext = () => ...; // FALSIFY-W5:...`。
发现后手动还原并 `grep -c "FALSIFY-"` 三文件全 0 复核。
⇒ **首轮结果整体作废,已重跑**。教训已落记忆 `feedback-falsify-concurrent-edit-window`。
