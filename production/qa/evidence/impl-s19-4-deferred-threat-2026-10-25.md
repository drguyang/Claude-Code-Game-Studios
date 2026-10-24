# S19-4 实现轮证据原件 —— `DeferredThreatSlot` 流态载体 + 两 Kind 读写 + 假断言撤换

**日期**:2026-10-25 · **执行**:AutoClaw 会话(`181888b1`)
**对象**:sprint-19 §S19-4(G-7 · `TD-randomevents-01` 兑现)· 测试载体
`unity/Assets/Tests/EditMode/RandomEvents/deferred_threat_slot_test.cs`(新,21 条)+ `event_budget_defer_test.cs`(改写 7 条)

---

## 一、改动清单(Landed)

| 文件 | 动作 | 内容 |
|---|---|---|
| `unity/Assets/Sim/EventBudget.cs` | 改 | **判据①②**:`BudgetState.DeferSlots` 由 `List<int>` 改 `Dictionary<int,int>`(slotIndex→eventKey,payload 显式寻址);**唯一写入路径 = `EventBudget.FoldDeferEvent`**(折叠两 `Kind`,经 `IPayloadDecoder` —— ADR-031 消费点,Sim 引用集不变);`ReconstructDeferSlots`(全序流→槽集);**`AddToDefer`/`ClearDefer` 删除**(第二真源拆除);`ResetForNewDay` 不再碰槽(跨日作废经流);`ValidateReconstructable`「验证非空」假断言删除,换 `(state, 流, decoder, deferMax)` 四参真判据(重构逐元素比对);折叠防御三类流损坏 fail-loud:越界 / 同槽双占 / 清未挂起槽 |
| `unity/Assets/Sim/DeferredThreatSlotWriter.cs` | 新 | **写路径**:两 Kind 共一写者(同一状态机):`TryDefer` / `TryClear` 具名结果码(`Applied` / `SlotOutOfRange` / `InvalidEventKey` / `InvalidReason`);`Patient = PatientId.None`(**必须**值,ADR-007 §四 —— 与 `CareApplied` 的「None 拒收」方向相反);`Seq = -1` 哨兵;reason 序数 **0=补发 / 1=作废**(落地约定本件首定 —— registry 未写序数;转发常量 `ReasonReissued`/`ReasonVoided` + 机械断言钉住,将来 registry 补序数须三处齐,`CareApplied/CarePhase` 先例) |
| `unity/Assets/Editor.Tools.Gates/WriterExistenceGate.cs` | 改 | **判据③**:`ThreatDeferred` / `ThreatDeferralCleared` 两行豁免**同批撤除**(判据①「已存在写者仍挂豁免 = 红」强制同批;承 `CaseClosed`/`CareApplied`/`PatientOutcomeChanged` 先例) |
| `unity/Assets/Tests/EditMode/RandomEvents/deferred_threat_slot_test.cs` | 新 | 21 条(见 §二) |
| `unity/Assets/Tests/EditMode/RandomEvents/event_budget_defer_test.cs` | 改写 | 三条假绿真判据化 + `test_newDayReset` 随直改面删除改写 + 头注 AC 映射订正(见 §二) |
| `.meta` ×2 | 新 | guid 各自独立(`ae76cf1d…` / `1601dab9…`) |

**「不保留第二真源」的架构语义**:槽集唯一真源 = 流(ADR-007 §三 承重条款);写者只写流、不簿记;
内存字典只是折叠投影 —— 三类「绕过流直改」的旧路径全部物理删除。

## 二、测试面(21 新 + 7 改写,全绿)

**新增 `DeferredThreatSlotTest`(21 条)**:

| 组 | 测试 | 钉什么 |
|---|---|---|
| 读路径(判据①) | foldDeferOccupiesSlot / foldClearReleasesSlot / emptyStream_yieldsEmptySlots / bothClearReasons_releaseSlot / unrelatedKinds_skipped | 折叠语义:入槽·清槽(两 reason 都清,折叠不分支)· 空流空槽 · 无关 Kind 跳过不解码 |
| **迁移夹具(判据④)** | `test_deferSlots_replayTwice_identicalSlotElements` | **同一段流重放两次,槽集逐元素相同**(主机迁移不丢不重 —— 卡面④逐字) |
| 折叠防御 | slotOutOfRange_throws / doubleOccupy_throws / clearUnoccupied_throws | 三类流损坏 fail-loud(静默 = 迁移后槽集漂移) |
| 判据①机械面 | `test_budget_deferSlots_noDirectMutationPath` | 反射:`AddToDefer`/`ClearDefer` 不存在;`DeferSlots` 字段类型 = `Dictionary<int,int>` |
| 判据② | validateReconstructable_trueWhenFoldMatches / **falseWhenDrift** | 真判据:一致 true;同槽异值 / 幻影槽 ⇒ **false**(原「验证非空」判不出任何漂移) |
| 写路径 | writer_defer_appendsHistoryEventWithNonePatient / writer_clear_appendsWithReason / reasonConstants_reissuedZeroVoidedOne | 事件头契约:Kind / **Patient=None** / **Seq=-1** / 路由 History(StreamRouting)/ 编码解码往返逐字段 / reason 序数断言 |
| 写者拒收 | defer_slotOutOfRange_noWrite / clear_slotOutOfRange_noWrite / defer_negativeEventKey_noWrite / clear_invalidReason_noWrite | 具名结果码 + **零写入**(AppendCount 恒 0) |
| 端到端 | test_endToEnd_writeThenFold_reconstructMatchesState | 写者→流→折叠闭环;生产流过重构验证 |
| 判据③ | `test_gate_threatKinds_notInExemptTable` | 反射 `ExemptKinds`:两 Kind 不在豁免表(撤表钉住,防回添) |

**改写 `EventBudgetDeferTest`(7 条,条数不变)** —— 三条假绿真判据化(sprint-19 :430 点名的必做面):

| 原假绿 | 新判据 |
|---|---|
| `test_deferSlotBounded`(空断言:加 10 断 ≤10) | slot∈[0,DEFER_MAX) 全可挂(写者 Applied)· slot=DEFER_MAX 写者具名拒收 + **零增量** · 折叠侧遇越界事件(绕过写者域校验构造)**抛** |
| `test_dwellNotInBudget`(恒真:同参调两次比相等) | 签名判据:`BudgetParams` 公共字段**恰四**(无 dwell 可进权重)+ `ComputeDailyBudget` 参数名无 dwell(AC-52-28 的可测投影) |
| AC-52-30 头注张冠李戴 | 头注逐条订正(27=下钳 0 / 29=医馆压制非冷却 / 30=三案链脚本;冷却=F2 机制;体积上界挂 GDD :300)—— 原文不删,git 历史可查 |
| (第 4 条,如实登记)`test_newDayReset` | **非假绿面内** —— 随判据①结构性改写:原 `AddToDefer` API 已删;新形态 = 入槽经流 + 跨日逐槽发 `ThreatDeferralCleared(作废)` + 折叠清(AC-52-48 经流形态);`ResetForNewDay` 只断预算两数 |

## 三、回归数字(三元组)

- **全量 EditMode**:`4867 / 4832 / 0 / 35` @ `2026-10-25 07:19 CST`(UTC `2026-10-24T23:19Z`,XML 实戳)·
  批次 `s194`(全量)· commit `6198671`
  (前基线 `4846/4811/0/35` @ `2026-10-25 01:42` · `s193` · 同 commit ⇒ **total +21 = 本轮新测数,零回归**;`4846+21=4867` 可复算)
  - 全名级差集:**新增 21**(全部 `DeferredThreatSlotTest`)· 消失 0 · 翻转 0 · 红 0 · skipped 35 同基线
  - 复算:`grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' /home/gu/.claude/jobs/181888b1/tmp/s194-full.xml | head -1`
- **门子集**(撤表联动面):`373/373/0/0` @ `s194-gates.xml` —— 含 `writer_existence_gate_test`
  「真工程零红错」正向夹具:**撤表 + 写者同批落地后门仍绿**(判据①未触发 ⇒ 同批纪律成立)
- **夹具修正一轮(如实登记)**:① 首跑编译炸 CS0308(`event_budget_defer_test.cs` 重写漏
  `using System.Collections.Generic`)⇒ 补 using;② `test_deferSlotBounded` 断言写错
  (拒收后断 `sink.Events.Count == 0`,实际含成功 5 条)⇒ 改「零增量」断言 `== DEFER_MAX`。
  两处均为夹具侧,生产码零改动。
- **PlayMode**:未触(纯 sim/gates 逻辑与折叠,无场景依赖)—— 如实登记 NOT-RUN

## 四、证伪轮 M1–M9(S12-4 形态;变异 → 跑 DeferredThreatSlotTest → 还原同命令内完成)

**纪律**:变异窗口 = `EventBudget.cs` / `DeferredThreatSlotWriter.cs` / `WriterExistenceGate.cs` 三文件
(窗口内不读盘,锚唯一性用脚本断言);还原判据 = SHA-256 逐字节(基准:`5a58c9e8…` / `f4764122…` / `257d2adb…`,备份 `fz-s194/`)。

| # | 变异(停用哪个守卫) | 结果 | 转红的测试 |
|---|---|---|---|
| M1 | 清槽分支反转(`Remove` 成功反抛) | 7 红 | foldClearReleases / bothClearReasons / clearUnoccupied / replayTwice / endToEnd / falseWhenDrift / validateTrueWhenFoldMatches |
| M2 | 同槽双占防御停用 | 1 红 | doubleOccupy_throws |
| M3 | 折叠侧越界防御停用 | 1 红 | slotOutOfRange_throws |
| M4 | 真判据退化(恒 `return true`) | 1 红 | falseWhenDrift |
| M5 | 事件头 Patient 改 `new PatientId(3)`(违 §四) | 1 红 | writer_defer_appendsHistoryEventWithNonePatient |
| M6 | TryDefer 越界校验停用(两处同锚,count==2 断言后 replace 第一处) | 1 红 | writer_defer_slotOutOfRange_noWrite |
| M7 | reason 值域校验停用 | 1 红 | writer_clear_invalidReason_noWrite |
| M8 | 门豁免表回插 `("ThreatDeferred", …)` | 1 红 | gate_threatKinds_notInExemptTable |
| M9 | `ReconstructDeferSlots` 丢一半事件(`i += 2`) | 4 红 | doubleOccupy / foldClear / unrelatedKinds / endToEnd |

**判别力自证**:9/9 轮全数转红;三文件还原后 SHA-256 与基准逐位一致(`uniq -c` = 2 / 逐文件比对)。
证伪后终验:RandomEvents 子集 `74/74` 绿(`s194-final.xml`)。

## 五、残余与边界(禁借绿)

- **写者未接线**:52 生产触发点(导演主循环:威胁 ∧ 医馆 ∧ 同日 → `TryDefer`;离开医馆 →
  `TryClear(补发)`;跨日 tick → 逐槽 `TryClear(作废)`)归 52 实现轮 —— 「有写者 ≠ 已接线」
  (与 `CaseOpenWriter` / `CareAppliedWriter` 同格先例)。
- **`TICKS_PER_DAY` 权威值未定**:夹具用 1200 并注明夹具值;权威值归 52 配置轮(烘焙数据)。
- **ADR-007 §三 表的其他行不在本债面**:抽取记录(`EventRolled`)/ 历史旗标(`HistoryFlagChanged`)
  的从流重构仍是豁免表在册债(`EventRolled` 等写者未落行),本件只兑延迟威胁槽一行。
- **reason 序数是落地约定**:registry 未写序数,0=补发/1=作废 本件首定(转发常量 + 断言钉);
  registry 将来补序数须三处齐(①registry ②转发常量 ③机械断言)。
- **折叠加 `IPayloadDecoder` 形参** = ADR-031 §Migration「『退』的执行归各归属系统实现轮」的
  一个正向消费点(52 折叠器直接走接口,不经任何绕行)。

## 六、复算命令

```bash
# 全量回归
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' \
  /home/gu/.claude/jobs/181888b1/tmp/s194-full.xml | head -1
# 差集(对照 s193-full)
python3 - <<'EOF'
import xml.etree.ElementTree as ET
old = {tc.get('fullname') for tc in ET.parse('/home/gu/.claude/jobs/181888b1/tmp/s193-full.xml').iter('test-case')}
new = {tc.get('fullname') for tc in ET.parse('/home/gu/.claude/jobs/181888b1/tmp/s194-full.xml').iter('test-case')}
print(len(new - old), len(old - new))   # 期望 21 0
EOF
# §五 二十订数字(权威脚本本体,docs/tech-debt-register.md §五)
```
