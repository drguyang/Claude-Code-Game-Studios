# 实现轮证据件 —— TD-randomevents-05(F1 上下文门:三项输入接真源 + 两处并存缺陷)

**日期**:2026-11-05 · **轮次**:sprint-24 · **commit**(回归时点):`46afde4e` + 工作树
**权威件**:`design/gdd/random-events.md` 规则六之二(含 ③ 出诊路径定义)/ 规则六之三 / **AC-52-29(BLOCKING)** /
**AC-52-20** · `docs/tech-debt-register.md` `TD-randomevents-05` · ADR-020 §四(玩家位移的表现态唯一投影)

> **一句话结论**:债条原文只记了「三项输入硬编码 `false`」。实测**三处缺陷并存**,另有第四处被前两处掩盖。
> 本轮把上下文门整层重做;**但本债不按「已解」结案** —— 见 §六。

---

## 一、勘察:债面之外的三处并存缺陷

| # | 缺陷 | 判据(可跑) | 为什么旧夹具没抓到 |
|---|---|---|---|
| **A** | `IsInClinic` / `IsOnExpedition` / `InCombatCooldown` 三项生产硬编码 `false` | `git show 46afde4e:unity/Assets/Gameplay.Boot/CompositionRoot.cs \| grep -n "isOnExpedition: false"` ⇒ 命中 | 债条已记,非新发现 |
| **B** | **`ComputeContextMult` 零生产调用方** —— `Execute` 调的是**无参** `HamiltonSplit(windowSize)` ⇒ 上下文乘子从未进配额层 | `git show 46afde4e:unity/Assets/Sim/EventF1Pipeline.cs \| grep -n "HamiltonSplit(input.WindowSize)"` ⇒ 命中无参重载 | 旧夹具 `test_contextMult_onlyInQuotaLayer` 只断言 `ComputeContextMult` 的**返回值**,不断言它是否被管线使用 ⇒ **全绿**。这是「自造断言顶替 AC 语义层」(假绿 F1)的教科书形态 |
| **C** | **`EvaluateContextGate` 与 AC-52-29 相反** —— 判据 `IsOnExpedition && !IsInClinic` 是**全档门**;玩家在医馆内 ⇒ 门关 ⇒ **机会 / 反应档也永不出** | AC-52-29 逐字:「威胁档被 `ContextMult = 0` 压制;**机会 / 反应档照常生成**」。GDD 规则六之二同款:「医馆不是绝对安全区」 | 旧夹具 `test_contextGate_boolean` 断的正是**AC 的反面**(`clinicInput ⇒ false`),且注释写「在医馆应不通过」 |
| **D** | (被 B/C 掩盖)门一旦变宽,链路能走到 ⑦ 之后,而旧序是「先 Append `EventRolled` + `EventArrived`、**再**在 `spawnPatient` 抛」⇒ 每次掷骰往流里塞一对**孤儿行** | 旧 `TrySpawnAtTick` ⑧ 在 ⑨ 之前 | 门恒假时链路走不到这里 ⇒ **零可观测**。修 C 会把它变成真回归,故同批修 |

---

## 二、解除动作 → 落点

| # | 解除动作(债册原文) | 落点 | 状态 |
|---|---|---|---|
| ① | `isInClinic` 接 23/24 的真实地块集 | `unity/Assets/Sim/F1ContextEvaluator.cs`(新):玩家格 = 流里**最后一条 `ActorCellEntered`**(`DeathCellQuery.TryLastCellOf`,ADR-020 §四)⇒ 问 `IClinicCellSource`。生产闭包只调它;与 `HouseCallStartedWriter` **共用同一个实例**(同源两点,第三点"规则十"仍缺) | ✅ |
| ② | `isOnExpedition` 按规则六之二 ③ 求值 | 同文件:(有活跃出诊目标) ∧ (不在医馆);医馆内**短路**(不依赖 `ActiveHouseCallQuery` 的实现) | ✅ 结构;<br>⚠️ **生产恒 false**(出诊类 `event_key` 集为空,`TD-randomevents-06`) |
| ③ | `inCombatCooldown` 接战斗冷却源 | **未接** —— P0 无「战斗结算后冷却窗」的流表达(25 / 27) | ⚠️ 恒 false = **当下正确值**,不是占位 |
| ④ | 夹具:三项各一正一反 + 真装配件 | `unity/Assets/Tests/EditMode/RandomEvents/f1_context_evaluator_test.cs`(**12 条**)。**真装配件**:生产闭包 `contextOf` 现在只剩一行 `F1ContextEvaluator.Evaluate(...)` ⇒ 夹具跑的就是生产代码路径,不是另写的等价逻辑 | ✅ |
| **B** | `Execute` 必须把 ContextMult 交给 `HamiltonSplit` | `Sim/EventF1Pipeline.cs` `Execute` | ✅ |
| **C** | `EvaluateContextGate` 改为 `!InCombatCooldown`(档级压制归配额层) | 同文件;旧夹具同步订正 | ✅ |
| **C′** | `CONTEXT_TRAVEL` 接进配额层(此前 `IsOnExpedition` **零消费方**) | `ComputeContextMult(input, travelMult = 1)`:出诊路径抬威胁 / 机会档;`travelMult < 1` ⇒ 抛(GDD `num ≥ den`) | ✅ 结构;<br>⚠️ **倍数 = 1 = 尚未调参**,数值归用户 |
| **D** | 「先判后写」:病人通道不可用 ⇒ 零写入 | `RandomEventSpawnChain`:新增 `SpawnChainOutcome.PatientSpawnUnavailable`;预检排在**第一次 `Append` 之前**;按条目 `handler` 分流(`encounter` 不产病人) | ✅ |

---

## 三、回归(三元组齐备)

| 项 | 值 |
|---|---|
| **本轮全量 EditMode** | **`6901 / 6864 / 0 / 37`**(total / passed / failed / skipped) |
| **本轮全量 PlayMode** | **`341 / 337 / 0 / 2 / 2`** |
| **基线 EditMode** | `6882 / 6845 / 0 / 37` @ `2026-11-05T22:42:48Z` · TD-persist-22 轮 · `78981d08` |
| **基线 PlayMode** | `341 / 337 / 0 / 2 / 2` @ `2026-11-05T22:44:02Z` · 同上 |
| **本轮三元组** | `2026-11-05`(见 `Logs/editmode-full.xml` / `playmode-full.xml` mtime)· sprint-24 `TD-randomevents-05` 轮 · `46afde4e` + 工作树 |
| **增量等式** | `6901 − 6882 = 19` = 新增夹具数(`f1_context_evaluator_test` **12** + `event_f1_pipeline_test` **+5** + `random_event_spawn_chain_test` **+2`)· `6864 − 6845 = 19` · **failed 0**(零回归)· PlayMode 逐位相同 |
| **复算命令** | `python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('unity/Logs/editmode-full.xml').getroot(); print({k:r.get(k) for k in ('total','passed','failed','skipped')})"`(PlayMode 同,改文件名) |

---

## 四、证伪轮(同命令内 改→跑→还原 · SHA-256 逐字节)

**驱动器**:`$CLAUDE_JOB_DIR/tmp/falsify_tdre05.py`(逐条:记 SHA-256 → 单点替换 → 跑 `-testFilter RandomEvents`
→ 无论成败先还原 → 复核 SHA-256 → 解析 XML 的 `Failed` 集合)。**判红 = 指定靶测出现在 `Failed` 里**
(不是「总数变了」)。窗口内**只改被变异的那一个文件**。

| # | 变异(停用什么) | 期望红的靶点 | 实测失败数 | 靶点命中 |
|---|---|---|---|---|
| M1 | `Execute` 回退成无参 `HamiltonSplit(windowSize)` ⇒ 乘子不进配额层 | `test_execute_clinic_zeroesThreatQuota_keepsOthers` | 3 | ✅(连带 `…combatCooldown_allQuotasZero…` / `…gate_openInClinic…`) |
| M2 | `EvaluateContextGate` 回退成 `IsOnExpedition && !IsInClinic` | `test_contextGate_onlyAllTierZeroBlocks`<br>`test_gate_openInClinic_andQuotaZeroesThreat` | 4 | ✅ 两条皆红 |
| M3 | `F1ContextEvaluator` 不查格(`inClinic = false`) | `test_playerOnClinicCell_isInClinic` | 3 | ✅(连带 `…activeSummonsButInClinic…` / `…gate_openInClinic…`) |
| M4 | 删「先判后写」的病人通道预检(`false && …`) | `test_medicalEntry_patientUnavailable_writesNothing` | 1 | ✅ 恰一条 —— **孤儿行面被单独钉住** |
| M5 | 忽略 `requiresPatient`(`needsPatient = true`) | `test_encounterEntry_doesNotNeedPatient_stillSpawns` | 1 | ✅ 恰一条 —— **handler 分流面被单独钉住** |
| M6 | `ComputeContextMult` 不读出诊旗标(`false && IsOnExpedition`) | `test_contextMult_onExpedition_liftsThreatAndOpportunity` | 1 | ✅ 恰一条 —— **`CONTEXT_TRAVEL` 的消费关系被单独钉住** |

**还原判据**:三文件 SHA-256 与变异前**逐字节相同**(`sha256sum -c pre-falsify.sha256` ⇒ 三行 `成功`)。
**除 `EventF1Pipeline.cs` / `F1ContextEvaluator.cs` / `RandomEventSpawnChain.cs` 外,窗口内零文件改动。**

---

## 五、如实登记:本件**没有**证明的事

- **「生产链路真的会落事件」没有端到端夹具**。线索是:门已变宽,`tier = 1`(机会)池里有 `2001 handler=encounter`
  ⇒ 生产**应该**会落 `EventRolled` + `EventArrived`。但这条**只有推理**,没有一条真启动序夹具钉它。
  ⇒ 不得据此声称「52 端到端可跑」。
- **`spawnAnchorOf` 仍是 `(0,0,0)` 具名占位**(6 的出生锚归 P1a 关卡);生产事件都会落在原点。
- **`isOnExpedition` 生产恒 false** —— 结构已接,数据在 37(`TD-randomevents-06`)。
- **`inCombatCooldown` 无来源**(25 / 27)。

---

## 六、本债的状态

**不结案。** 上下文门这一层解了(①②④ + B + C + C′ + D),但:

| 未解面 | 归谁 |
|---|---|
| `isOnExpedition` 生产恒 false | `TD-randomevents-06`(37 的 `event_key` 集) |
| `inCombatCooldown` 无来源 | 25 / 27 的 GDD 轮 |
| `CONTEXT_TRAVEL` 倍数 = 1(未调参) | 数值轮(用户) |
| **AC-52-29 的 `DeferredThreatSlot` 延后半边**(规则六之三:当日内补发 / 跨日作废) | **零生产接线** —— 新立 `TD-randomevents-08` |

⚠️ **AC-52-29 不得签核为已完成** —— 它的 THEN 句有两半,本轮只落了「压制」半边。
