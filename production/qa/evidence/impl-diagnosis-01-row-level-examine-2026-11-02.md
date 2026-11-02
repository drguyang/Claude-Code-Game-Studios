# 实现原件 —— 查体归位轮(结清 `TD-diagnosis-01`)

> **对象**:`docs/tech-debt-register.md` 的 `TD-diagnosis-01`
> **批次**:`S23-14`(sprint-23 追加;用户指令「修 TD-diagnosis-01」)
> **截至时刻**:`2026-11-02 09:00 CST`(见 §五的三元组)· **commit**:`cbcf8712` + 本批未提交工作树
> **结论**:三条消解判据全部兑现;**实现期抓出一条真缺陷**(六行回调一个都没挂上,见 §六)。

---

## 一、修的是什么

`diagnosis-system.md` `S-8.4`(路线甲 = 模态分流)逐字:

> **世界空间裸 `Interact` = 就诊(单义)**;**查体 = 脉案模态行级动作**;施治 = 方笺落笔;急救 = 10 直读通道。

`casebook-39.md` §7 交互表同款:「**查体**(五手段) | 焦点行 + Submit | … | 行级动作**不经 4 的世界路由**」。

实装(修前):世界 E 一次按下 = `ExamineDriver.OnExamine(patient, tick)` —— **整病人一次性查完**
(六通道一起求值,落一张合并快照);脉案屏里**没有任何行级查体入口**
(`ICasebookCommandSink` 只有病名 / 置信度 / 戥子三件)。

---

## 二、改了什么(生产 9 件 + 夹具 5 件)

| # | 文件 | 变更 |
|---|---|---|
| 1 | `Gameplay.UI/Skeuomorphic/CasebookBinding.cs` | `ICasebookCommandSink` 增 `void ExamineChannel(SignChannel)`(行级查体成员;命名空间内 `SignChannel = Presentation.Diagnosis.SignChannel` 别名,避开 `Sim.Contracts.SignChannel` 位域同名) |
| 2 | `Gameplay.UI/Skeuomorphic/Screens/CasebookScreen.cs` | 六行 `BindChannelExamine("channel-…", 通道键)` ⇒ `PointExamine → sink.ExamineChannel`。**绑定点在 `_root.Add(_casebookFace)` 之后**(见 §六);`AddChannel` 签名一字未改(`texture_binding_gate_test` 结构锚点) |
| 3 | `Gameplay.Boot/CasebookCommandRouter.cs` | 增 ctor 参 `ExamineDriver examineDriver` + `ExamineChannel(channel)`(未绑病人 ⇒ no-op)+ `AfterExamined` 回调 |
| 4 | `Gameplay.Boot/ExamineDriver.cs` | 增 `ExamineChannel(patient, channel, tick)`(单通道求值 + 单通道落册);`OnExamine` 保留但**已无生产调用点**(见 §七.3) |
| 5 | `Gameplay.Presentation/Diagnosis/DiagnosisExamineEvaluator.cs` | 增带通道参的重载;`EvaluateCore(…, SignChannel? channel)` 逐行过滤(`row.Channel != channel ⇒ continue`) |
| 6 | `Gameplay.Boot/ExaminationLedger.cs` | 每病人 **按通道分片** `Dictionary<int, Dictionary<SignChannel, Snapshot>>`;新增 `RecordExamined(…, channel, …)` / `HasExaminedChannel` / `ExaminedChannelCount`;`TryGetSnapshot` 按 `ChannelOrder` 合并六片 |
| 7 | `Gameplay.Boot/InteractionInputDriver.cs` | `Visit` **删掉**查体调用 —— 世界 E 只做 ① 立案 ② 切病人 ③ 开册 |
| 8 | `Gameplay.Boot/BootRoot.cs` | `CasebookCommands.AfterExamined = (p,t) => SeenDisease.OnExamined(p,t,actorId)`(查体完成后的「已见病种」更新仍走原路径) |
| 9 | `Gameplay.Boot/CompositionRoot.cs` | `CasebookCommandRouter(…, prescriptionPage, examineDriver)` |

夹具 5 件:

| # | 文件 | 变更 |
|---|---|---|
| 10 | `Tests/EditMode/Boot/casebook_command_router_test.cs` | **+3** 行级查体测试;`test_examineChannel_recordsOnlyThatChannel` 追加**内容面**判据(见 §四 M2) |
| 11 | `Tests/EditMode/Boot/examination_ledger_test.cs` | **+2** 分片测试(两片并存 / 同通道覆盖不影响他片) |
| 12 | **新** `Tests/PlayMode/SkeuomorphicUI/casebook_row_examine_test.cs` | **+3**:六行各发各通道 / 只发查体不误触发邻行 / 未接写通道不抛 |
| 13 | `Tests/PlayMode/Casebook/casebook_entry_wiring_test.cs` | `test_ce_02` 追加「世界 E 零快照」+ **同场正控**(走一次行级查体,要求分册能记下) |
| 14 | `Tests/PlayMode/interaction_input_chain_test.cs` | 3 条 E 测试的观测面由「分册快照 / `LastExaminedPatient`」改为 `caseOpen` 接缝记录 |

---

## 三、三条消解判据逐条兑现

| # | 判据(债册原文) | 兑现 | 可跑复算 |
|---|---|---|---|
| ① | `ICasebookCommandSink` 出现**行级查体成员** | ✅ `void ExamineChannel(SignChannel)` | `grep -n "ExamineChannel" unity/Assets/Gameplay.UI/Skeuomorphic/CasebookBinding.cs` ⇒ 1 命中 |
| ② | 世界 E **不再**调 `ExamineDriver.OnExamine` | ✅ `Visit` 无该调用 | `grep -n "OnExamine" unity/Assets/Gameplay.Boot/InteractionInputDriver.cs` ⇒ 零命中 |
| ③ | 夹具证明「世界 E 只立案 + 开册,**零查体快照**」 | ✅ `casebook_entry_wiring_test.test_ce_02`(真启动序)+ `interaction_input_chain_test.test_modalClosed_acceptsE_visits`(驱动级) | `grep -n "HasExamined" unity/Assets/Tests/PlayMode/Casebook/casebook_entry_wiring_test.cs` ⇒ 2 命中(负判据 + 正控) |

---

## 四、证伪轮(4 条 · 改→跑→还原同一命令内 · SHA-256 逐字节还原 4/4)

| 变异 | 打什么 | 实测红 | 形态 |
|---|---|---|---|
| **M1** | `CasebookScreen` 六行全绑 `FaceColor`(通道键写错) | 1 红:`casebook_row_examine_test.test_sixChannelRows_submit_examineMatchingChannel`(报在 `channel-voice`) | **exact** |
| **M2** | `DiagnosisExamineEvaluator.EvaluateCore` 停用通道过滤 | 1 红:`casebook_command_router_test.test_examineChannel_recordsOnlyThatChannel`(报文点名混入 `Posture`) | **exact** |
| **M3** | `InteractionInputDriver.Visit` 复又调 `ExamineDriver.OnExamine` | 3 红:`test_ce_02`(零快照)/ `test_modalClosed_acceptsE_visits`(就诊≠查体)/ `test_virtualE_keyboardRead_and_doesNotExamineInWorld` | **exact**(三处独立观测面) |
| **M4** | `ExaminationLedger.RecordExamined` 忽略通道(全存 `FaceColor`) | 2 红:`test_perChannel_rowsAccumulate_notOverwrite` · `test_sameChannel_reExamine_overwritesThatSliceOnly` | **超集**(两条分片测试都断言「另一片保留」) |

### ⚠️ M2 的首轮是**假存活**(如实登记)

首版 M2 **total=12 passed=12 failed=0** —— 变异未转红。根因:夹具只把 tick 推到 1,
`signValue01 = dto.Position` 低 ⇒ 六通道**全部读出 0 条** ⇒ 通道过滤有没有做,结果都是空集。
⇒ 修夹具两处(不改生产):
① 测试内把 `VitalsService.OnTickEdge` 推到 tick 60(plateau,`position ≡ 1.0`);
② 判据从「只验面色一片」改成**逐行查、每查一行验一次「已查集合 ⊇ 快照词条的通道集合」**,
末尾加非空前置(`六行全查后总条数 > 0`)。改后同一变异 **1 红**,证明夹具此前对这条属性零判别力。
**这正是 F4(空洞正控)的变体**:不是断言写假,而是**输入构造让变异前后同结果**。

---

## 五、回归(三元组 + 等式)

| 套件 | 本批 | 基线(`S23-13`) | 等式 |
|---|---|---|---|
| EditMode | `6531 / 6494 / 0 / 0 / 37` | `6526 / 6489 / 0 / 0 / 37` | `6531 − 6526 = 5 = +3(路由)+ 2(分册)` ✅ |
| PlayMode | `297 / 293 / 0 / 2 / 2` | `294 / 290 / 0 / 2 / 2` | `297 − 294 = 3`(新 `casebook_row_examine_test`,零删除) ✅ |

`@ 2026-11-02 09:00 CST` · 批次 `S23-14` · commit `cbcf8712` + 工作树。
PlayMode `failed=0` 与 `inconclusive=2` 并存系既有两条 `Inconclusive`(与基线同形),退出码非零即由此。

**复算**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "
import xml.etree.ElementTree as ET
for f in ['unity/Logs/td-diag-em-full2.xml','unity/Logs/td-diag-pm-full2.xml']:
    r=ET.parse(f).getroot()
    print(f, {k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})
"
# 期望: EditMode 6531/6494/0/0/37 · PlayMode 297/293/0/2/2
```

---

## 六、实现期抓出的**真缺陷**(这条是本轮的主要收获)

`casebook_row_examine_test` 首跑 **2 红**,报文是「六行各一次 / 落焦+真派发 ⇒ 应恰发一条查体,
Expected: 1 But was: 0」。

根因:`BindChannelExamine` 用 `_root.Q(rowName)` 回查行,而调用点当时位于
**`_root.Add(_casebookFace)` 之前** —— `channels` 还是游离子树,`_root.Q` 返回 `null`,
于是 `if (row == null) return;` **静默返回**,六行**一个回调都没挂上**。

后果的量级:生产上「脉案六行 Submit ⇒ 查体」**完全哑火**,而**既有全部夹具仍绿**(没有任何一条
从屏幕侧断言过这条路径)。这正是假绿形态 **F2**(接口成员被各 `FakeSink` 实现只是为了过编译)
与 **F6**(「注释声称有接线」)的合体。

修法:把六行绑定移到 `_root.Add(_casebookFace)` **之后**(`CasebookScreen.BuildUI` 末尾),
并在原注释位留警示。**未**把 `if (row == null) return;` 改成抛 —— 行缺失归既有 name 序断言抓,
屏内抛会违反「42 只渲染」的降级纪律;但**静默返回的代价**写进了注释。

---

## 七、证明不了的事(禁借绿)

1. **行级查体在真机上只有 UI 一条路** —— 没有键位夹具验证「玩家按 Enter 到第六行」的焦点序;
   `casebook_row_examine_test` 证的是「元素派发 ⇒ 写通道」,**不证**方向键可达性
   (后者属 `casebook_page_turn_test` ⑤b/⑤c 的焦点族,本轮未扩)。
2. **六通道各自的「读出多少条」不在本件面内** —— 判据只钉「别的通道不混进来」,
   不钉每通道的读数正确性(归 8 的数值轮 / 词表轮)。
3. **`ExamineDriver.OnExamine` 现在是「无生产调用点」的公开 API** —— 保留给夹具与将来
   「整病人普查」语义,**未删**。⚠️ 若有新代码调它,「世界 E 不查体」的判据不会红
   (判据盯的是 `InteractionInputDriver`,不是全仓调用点)。登记为**已知盲区**,
   是否加「全仓零命中」门待裁。
4. **`AfterExamined → SeenDisease.OnExamined` 的接线**只有编译面 + `test_ce_02` 的间接覆盖,
   没有「查体一次 ⇒ 已见病种 +1」的直测。
5. **`ExaminationLedger` 合并快照的 `ExaminedTick`** 取「最近一次」是既定语义
   (`test_perChannel` 断言 11),但**未按通道暴露**单通道 tick —— 若将来 UI 要逐行显示
   「何时查的」,需要新读面。
