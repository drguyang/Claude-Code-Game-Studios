# 实现轮证据件 —— TD-randomevents-02(52 出诊启动写者 + 活跃出诊目标派生查询)

**日期**:2026-10-25 · **轮次**:sprint-19 · **commit**(回归时点):`9edb75c` 工作树
**权威件**:`design/gdd/random-events.md` 规则十二(12.1–12.4)· `entities.yaml` HouseCallStarted ·
`docs/tech-debt-register.md` TD-randomevents-02 · 2026-10-25 三裁(同 tick 唯一链接 fail-loud / 谓词接口注入 / P0 零检查 + hook 登记)

## 一、解除动作 → 落点(四条全落)

| # | 解除动作(债册原文) | 落点 | 判据 |
|---|---|---|---|
| ① | 写者 `HouseCallStartedWriter` + b7 豁免同批撤 | `unity/Assets/Sim/HouseCallStartedWriter.cs`(五具名结果码;前置全在写者内复核:活跃目标 / 本 tick 跨格 / 前格 ∈ 医馆 ∧ 新格 ∉ 医馆 / 同 tick 去重)· `WriterExistenceGate.cs` 撤表 + ✅ 撤表注(承 `ThreatDeferred` 先例) | `grep -c "Encode(EventKind.HouseCallStarted" unity/Assets/Sim/HouseCallStartedWriter.cs` = 1;b7 接线 = `AssemblyGates.cs:144`,全量绿含「写者已被识别」 |
| ② | 派生查询(12.2 三条件纯函数) | `unity/Assets/Sim/ActiveHouseCallQuery.cs`(最新出诊类 `EventArrived` → 同 tick 恰一条 `DiseaseOnset` 链接 → `CaseOpened` 终止;P0 零窗口 / 零拒访) | `grep -c "class ActiveHouseCallQuery" unity/Assets/Sim/ActiveHouseCallQuery.cs` = 1 |
| ②' | 「医馆格」判据注入面(裁定②) | `unity/Assets/Sim.Contracts/IClinicCellSource.cs`(单方法谓词;同源义务三处 = 本谓词 / 规则十「无人」/ `F1PipelineInput.IsInClinic`,装配归 Boot 接线轮) | `grep -c "IsClinicCell" unity/Assets/Sim.Contracts/IClinicCellSource.cs` ≥ 2 |
| ③ | `NoPendingMedicalActionQuery` 防呆注 | `unity/Assets/Gameplay.Boot/NoPendingMedicalActionQuery.cs` 类头 ⛔ 块(12.4:旅途段安全,活跃目标不并入 unsafe;并订正旧「落地时并入」口径) | `grep -n "防呆注" unity/Assets/Gameplay.Boot/NoPendingMedicalActionQuery.cs` |
| ④ | 夹具 AC-52-56 / 57 / 58 | `unity/Assets/Tests/EditMode/RandomEvents/house_call_test.cs`(**20 条**测试) | 见 §二 |

**「有写者 ≠ 已接线」**:生产触发点(52 导演主循环逐 tick 调 `TryStartAtTick`)归 52 的接线轮,本件不声称已接线。
**裁定③ hook**:窗口过期 / 拒访检查 P0 为零,hook 登记归 37 的 GDD 轮(查询头注已载)。
**跨系统前置(债册「⚠️ 跨系统前置」)**:病人创建时机归 9 / 37 —— 本轮以**链接 fail-loud**(同 tick 恰一条 `DiseaseOnset`,0 条或多条皆抛)把该不变量钉在流上;若 9 / 37 将来裁出不同时机,该异常即为可见冲突面,不静默。

## 二、回归与夹具判读(基线三元组齐备)

| 项 | 值 |
|---|---|
| **本轮全量回归** | **`5019/4984/0/35`**(EditMode total/passed/failed/skipped) |
| **基线** | `4999/4964/0/35` @ `2026-10-25T10:21 UTC` · TD-persist-04 动作① 收口轮(`act1-full2`)· commit `d157b4e` 工作树 |
| **本轮三元组** | `2026-10-25T11:08 UTC` · TD-randomevents-02 实现轮全量 · commit `9edb75c` 工作树 |
| **增量等式** | 5019 − 4999 = **+20**(全为 `HouseCallTest` 新夹具,XML 逐条核 `Passed` = 20)· 4984 − 4964 = **+20** · failed **0**(零回归) |
| **复算命令** | `python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('unity/Logs/td-re02-editmode.xml').getroot(); print({k:r.get(k) for k in ('total','passed','failed','skipped')})"` |

## 三、证伪轮(S12-4 · 五条变异 · 同命令改→跑→还原 · SHA-256 逐字节还原全过)

| # | 变异(停用什么) | 期望红 | 实测 |
|---|---|---|---|
| M1 | 写者④ 同 tick 去重(`false &&`) | `test_ac5256_secondCallSameTick_refused` | ✅ 1 红(19/20) |
| M2 | 写者③ 新格 ∉ 医馆判据(`\|\| false`) | `test_ac5258_moveInsideClinic_noStart` | ✅ 1 红(19/20) |
| M3 | 查询③ 立案终止判据(恒 skip) | `test_ac5257_caseOpenedTerminatesTarget` | ✅ 2 红(+`replayEvaluate_bitwiseStable`,同守卫下游) |
| M4a | 查询② 0 条 onset 抛(`false &&`) | `test_ac5257_linkMissingOnset_throws` | ✅ 3 红(+`onsetAtAdjacentTick` / `writer_brokenLink`,同守卫下游) |
| M4b | 查询② 多条 onset 抛(`false &&`) | `test_ac5257_linkDuplicateOnset_throws` | ✅ 1 红(19/20) |

五轮均:锚串恰命中 1 次(脚本断言)· Unity 过程退出即还原 · **还原后 SHA-256 逐字节一致**(脚本内断言,违则 abort)· 残留检查 `grep -n "false &&" unity/Assets/Sim/{ActiveHouseCallQuery,HouseCallStartedWriter}.cs` = 0 命中。
证伪轮跑的是 `HouseCallTest` 单夹具过滤(`-testFilter`),符合 S12-4「重跑该 fixture」口径。

## 四、AC 映射

| AC | 载体 | 状态 |
|---|---|---|
| AC-52-56(跨出 ⇒ 恰一条;同 tick 重入 / 跨回再跨出仍恰一条) | `house_call_test.cs` M1 反证钉 + `crossOut` / `secondCall` / `reenterAndOut` / `recrossOutLaterTick` 4 测 | ✅ RUN(证伪过) |
| AC-52-57(派生三条件 + 重放逐位一致 + 链接 fail-loud) | `house_call_test.cs` M3/M4a/M4b 反证钉 + 9 测 | ✅ RUN(证伪过) |
| AC-52-58(医馆内 / 无目标 / 无跨格不触发;门答安全) | `house_call_test.cs` M2 反证钉 + 5 测 + `checkpointGateSafeAfterHouseCallStarted`(12.4 否证钉) | ✅ RUN(证伪过) |

## 五、未跑 / 未声称(禁借绿)

- 52 导演主循环接线(谁逐 tick 调写者)—— 归 52 接线轮。
- `IClinicCellSource` 生产装配 + 三处同源反射钉 —— 归 Boot 接线轮(P0 夹具用 stub,不构成「已同源」)。
- 37 侧窗口 / 拒访 hook、b7 之外的门链探针轮 —— 不在本件面内。
