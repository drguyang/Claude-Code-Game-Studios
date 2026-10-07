# 评审报告原件 —— diagnosis(8)story-005 读数/判断状态机(单轮双代理)

- **对象**: `production/epics/diagnosis-system/story-005-reading-and-judgment-state-machines.md`
  及本轮新落生产四件(`DiagnosisReadingFsm` / `DiagnosisJudgmentFsm` / `DiagnosisSnapshotSampler` /
  `DiagnosisGrowthGate`,住 `unity/Assets/Gameplay.Presentation/Diagnosis/`)+ 测试两件
  (`reading_state_test.cs` 16 测 / `snapshot_staleness_test.cs` 3 测)+ 金标重钉 + Gates `[D-EXIT-GATE]`
- **日期**: 2026-10-07 · **轮次**: 单轮双代理(承用户「评审只做一轮」)
- **评审席**: `lead-programmer`(结构侧)+ `qa-lead`(测试面)—— 均只读、评当下
- **两侧总裁定**: **CHANGES REQUIRED**(结构 8 条 / 测试 10 条)→ 修复 → 复跑绿(§四)→ 本件落库
- **判定 → 修复 → 验证** 三段式逐条登记;**登记不修项显式**(§五,不静默)

---

## 一、结构侧(lead-programmer)—— 1 MAJOR · 4 MINOR · 3 NIT

**原判定**:CHANGES REQUIRED。前置核验全部通过:登记点 1–6 属实、铁律②③由既有前缀级
IL/反射门机器可证(新件自动入面)、金标重钉 = 枚举扩面非掩盖、门控「客户端+非法参数不触达出口」
判别力经 `SkillGrownEmitter` 契约校验坐实、16 条 AC 无缺落点。

| # | 原判定(原文摘要) | 处置 | 修复落点 |
|---|---|---|---|
| S1 | **MAJOR** · **双完成入口**:`GateChannel` 可得分支绕过 RECHECK 抑制,与 `OnExaminationCompleted` 分裂;组合次序无契约 —— 「先 gate 后 complete」可击穿 AC-8-31,只走 gate 可击穿 S-8.2 窗口(BLOCKING 互为可击穿面),且测试固化了分裂用法 | ✅ **已修** | `DiagnosisReadingFsm` **收单一完成入口**:`OnExaminationCompleted(current, outcome, channelAccessible, completionTick, lastRecheckTick, recheckWindow)` —— ①不可得 ⇒ 强制空行(优先于窗口与读数)②可得且窗口内 ⇒ 抑制 ③否则物化;`GateChannel` 删除,全部测试调用同步(8 处);`AC-8-31` 补「不可得不产读数、不看 outcome」断言 |
| S2 | **MINOR** · `GateChannel(false)` 强制 Blank ⇒ **回溯抹除已定读数**,与 AC-8-25 快照冻结、规则五「不得改口未查」有张力;卡与 GDD 均未明说,被测试静默裁定 | ✅ **登记**(语义裁定) | 维持卡字面(AC-8-31「恒空行」含回溯)⇒ `OnExaminationCompleted` doc + 卡 Completion Notes 裁定 1 显式登记;**39 接线前可改判**,改判只动本分支 |
| S3 | **MINOR** · 幂等机制**静默替换** Note 3 的「(patient,旧,新,tick) 当帧集」为状态判重,`OnThresholdTransition` 连 tick 入参都没有 —— 偏离权威 note 未登记,失去延迟投递排序判别 | ✅ **登记** | 卡 Completion Notes 裁定 2:状态判重落地、Note 3 的集不落地(「登记评审点」触发条件消解);排序判别归 39 接线复评;`OnThresholdTransition` doc 补记 |
| S4 | **MINOR** · 被抑制的复查**不刷新** `lastRecheckTick`(两测试编排一致)—— GDD「上次查体的 tick」字面张力,未登记;随 39 接线硬化 | ✅ **登记** | 卡 Completion Notes 裁定 3:现编排 = 不刷新;刷新与否涉 OQ-8-1,随 39 接线裁定 |
| S5 | **MINOR** · AC-8-30 只扫**契约成员名**;未来给 `OnWriteIntent` 增 `isCorrect` 类参数则不红,缺 ground-truth 参数面对称断言 | ✅ **已修** | `reading_state_test` AC-8-28 测试内补**双 FSM 对错真源参数扫描**(correct/truth/answer/match/truedisease)+ 影子 `ShadowJudgeWithTruthInput`(static)负夹具必红 |
| S6 | **NIT** · AC-8-45 双端 `FakeEventLog` 恒真(未接被测代码),装饰性断言 | ✅ **已修**(与 qa M1 同批) | 见 §二 M1 |
| S7 | **NIT** · `IsHost` P0 恒 true ⇒ 客户端拒绝分支生产不可达,判别力仅 fake 权威可证 | ✅ **登记** | `DiagnosisGrowthGate` 头注补「S-7:P0 恒 true、真客户端拦截端到端 NOT-RUN,归 45/P1b 族」 |
| S8 | **NIT** · 卡 Test Evidence `[ ] NOT STARTED`、Completion Notes 空,与实跑不一致 | ✅ **已修** | 卡 Status → Complete、Test Evidence [x] + 跑数 + NOT-RUN 三条、Completion Notes 全量回填(随本件) |

---

## 二、测试面(qa-lead)—— 3 MAJOR · 3 MINOR · 4 NIT

**原判定**:CHANGES REQUIRED —— 3 MAJOR + 3 MINOR + 4 NIT。已核验为真(未重复报):金标重钉
`+7` 逐字吻合且有实算 FNV 变异敏感性守门、门控措辞订正属实、RECHECK 裁定已登记、铁律④
「客户端+非法参数」判别成立(`SkillGrownEmitter` 对负值真 throw)、QA 十条逐条有真身无缺条。

| # | 原判定(原文摘要) | 处置 | 修复落点 |
|---|---|---|---|
| M1 | **MAJOR** · AC-8-45 「双端 FakeEventLog 哈希 + Count=0」**恒真死胡同**:两流从未接给被测代码,FSM 无 sink 入参 ⇒ 物理不可能非 0;真守门在 `[D-TREF]`/`[D-PUB]` 但本文件未引照,同形态已在 boundary_guard 登记为「结构占位」,此处却直接当 QA case 证据 | ✅ **已修** | 删 `FakeEventLog`;改**类型面扫描**(共用 `ScanMemberNamesForTokens(FourTypes, "append","publish","sink","eventstream")` IsEmpty)+ 影子 `ShadowWithWriteApi.Append` 负夹具 IsNotEmpty 点名;真守门引照 IL 门;NOT-RUN 保留 |
| M2 | **MAJOR** · AC-8-46 负夹具**手搓** `GetParameters` 循环,未走共用机器 ⇒ 机器被改坏(恒空)时正负双绿、判别力归零;根因 = 影子 instance 方法 vs 机器 BindingFlags `Public\|Static` | ✅ **已修** | 影子 `ShadowWriter.Write` 改 **static**;负夹具一行改 `ScanMethodParametersForTokens(typeof(ShadowWriter), "skill")` **同机**;AC-8-31 内联参数扫描一并收编共用机器 |
| M3 | **MAJOR** · 铁律④缺**静态闭合**:「门控断言」半边不存在 —— `DiagnosisGrowthExit.EmitGrowth` 是 public,任何未来调用方直调即零门控零红,恰是 TR-diag-005 要防形态;现状仅「今日恰一个调用方」的巧合 | ✅ **已修** | `DiagnosisBoundaryGates` 增 **`[D-EXIT-GATE]` 谓词**:出口在 8 前缀内的调用点 enclosing 恰 = `DiagnosisGrowthGate`;影子 `BypassGate()` 直调出口负例;新测试 `test_rule4_gateIlNegative_bypassGate_red`;**突变实跑**(§六) |
| M4 | **MINOR** · AC-8-28 路径 2「全查不落笔」**恒真**(只验常量构造,零 Act 零可失败面) | ✅ **已修** | 改**签名面**:判断 FSM 公开 API 恰三(`OnWriteIntent/Freeze/CloseCase`,CollectionAssert)+ 刹车参数扫描(exam/count/checked/enough/progress);⚠️ token 集**刻意不含 "reading"**(CloseCase 的 reading 是状态入参,注释登记) |
| M5 | **MINOR** · EditMode AC-8-48 两段源下 completion ∈ [20,24] 同词 ⇒ 「≠ 中段」消息失实,判别力只在 PlayMode 三段源 | ✅ **已修** | EditMode 对齐三段源(completion=25 落段边界,中段词「变词」必异)+ 补 mid 断言 |
| M6 | **MINOR** · PlayMode 三测全 `[Test]` 无帧驱动,标签无实益;真增量盘点后「9/37/39 真接线集成 NOT-RUN 未在卡面登记」(只登了 AC-8-45) | ✅ **已修**(登记面) | 卡 Test Evidence 补「② 9/37/39 真接线集成 NOT-RUN(归接线 story)」;PlayMode 增量边界(ledger 编排纪律/三段源/旧后重查)在头注保留;升 `[UnityTest]` 归 tick 驱动可接后 |
| M7 | **NIT** · 卡 Test Evidence `NOT STARTED` 未回填 | ✅ **已修** | 见 §一 S8 |
| M8 | **NIT** · boundary_guard 头注「完整脚本随 005/006 落」005 未落,占位无主 | ✅ **已修** | 头注显式钉「归 story-006」+ `[D-EXIT-GATE]` 补记 |
| M9 | **NIT** · 参数名 token 扫描是命名面防线(改名即绕) | ✅ **已修**(登记) | `reading_state_test` 头注登记「命名面防线;类型/IL 面另属 Gates `[D-*]` 族」 |
| M10 | **NIT** · 铁律④「主机恰一次」以载荷相等代理(纯函数双调不可观测) | ✅ **已修**(登记) | iron4 测试注记:真调用计数归 IL 侧 `[D-EXIT-GATE]`(唯一调用方) |

---

## 三、AC ↔ 覆盖表(评审时点 → 修复后)

| AC | 评审时点 | 修复后 |
|---|---|---|
| AC-8-21 三态可分 | Covered | 不变 |
| AC-8-22 不改口 | Covered | 不变(经单一入口) |
| AC-8-24 恰四/三态 | Covered | 不变 |
| AC-8-25 快照冻结 | Covered | 不变 |
| AC-8-48 完成 tick | Covered 但 EditMode 判别力弱(M5) | ✅ 三段源 + mid 断言 |
| AC-8-27 幂等旧态 | Covered | 不变(机制改判重已登记 S-3) |
| S-8.2 窗口托底 | Covered 但入口分裂可绕(S1) | ✅ 单一入口强串联 |
| AC-8-28 不踩刹车 | Partial(路径 2 恒真) | ✅ API 恰三 + 参数面 |
| AC-8-29 痕不计分 | Covered(影子共用机器) | 不变 |
| AC-8-30 零反馈 | Partial(缺参数面) | ✅ 真源参数扫描 + 影子 |
| AC-8-31 无法配合 | Covered 但组合无契约(S1) | ✅ 单一入口 + 不产读数断言 |
| AC-8-47 关病例 | Covered | 不变 |
| AC-8-49 潜伏期 | Covered | 不变 |
| 铁律④ 门控 | Partial(缺静态闭合 M3) | ✅ `[D-EXIT-GATE]` + 突变证红 |
| AC-8-45 联机 | **Defective**(M1 恒真) | ✅ 类型面 + 影子;网络子句仍 NOT-RUN |
| AC-8-46 无 Skill 参数 | **Defective**(M2 机器脱钩) | ✅ 影子 static 同机 |

---

## 四、验证(可证伪)

**修复后复跑**(2026-10-07,批独占锁确认后执行):

```bash
# 1) DiagnosisSystem 过滤(原 149 → 150:新增 gateIlNegative)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.DiagnosisSystem" \
  --output unity/Logs/dx005-5.xml
# ⇒ 150 total / 149 过 / 0 红 / 1 跳(跳 = 既有 test_ac834_reverseOrphan_blockedish)

# 2) 全量 EditMode(基线 2955/2908/0红/1inc/46跳)
unity test unity --mode EditMode --output unity/Logs/editmode-full-dx005-r2.xml
# ⇒ 2972 / 2925 过 / 0 红 / 1 inconclusive / 46 跳(+17 = 16 新测 + 1 gate 测试)

# 3) PlayMode 新件
unity test unity --mode PlayMode --filter "DaYiJingCheng.Tests.PlayMode.DiagnosisSystem" \
  --output unity/Logs/dx005-play3.xml
# ⇒ 3 / 3 过 / 0 红
```

**XML 解析口径**:按根节点 `type="TestSuite" name="unity"`(或根 `test-run`)取
total/passed/failed —— per-fixture 节点会误导(已踩两次)。
> 全量 CLI `exit 2` = 既有 `SettingsExposureTest` 1 条 Inconclusive 所致,基线同形。

**关键修复的可证伪判据**:
- **M3 `[D-EXIT-GATE]`**:Gates 谓词条件改 `if (false && …)` ⇒ `test_rule4_gateIlNegative_bypassGate_red`
  必红 —— **实跑见证 §六**;还原回绿 26/26。
- **M1 类型面**:影子 `ShadowWithWriteApi.Append` 从扫描 token 集移除(如 token 改 `__nope`)⇒
  负夹具 IsNotEmpty 红(机器活性内置自证)。
- **M2 同机**:共用机器 BindingFlags 改回含 Instance(影子失配)或 token 改 ⇒ 影子断言红;
  正测与负夹具现已**同一台机器**(原手搓循环已删)。
- **S-1 单一入口**:删 `if (!channelAccessible)` 分支 ⇒ `test_ac8_31` 的 `blocked.State` 期望
  Blank 而实得 Positive ⇒ 红(且 `ProducedNewOutcome` 断言同红)。
- **S-5 真源参数面**:给 `DiagnosisJudgmentFsm.OnWriteIntent` 加 `bool isCorrect` 参数 ⇒
  `truthHits` 非空必红。
- **M4 API 恰三**:给判断 FSM 加 `public static void AutoFill(…)` ⇒ `apiNames` 断言红。
- **金标**:`read_floor_slots` / `sign_table` 以**实算 FNV** 比对常量(含 `AreNotEqual(withExtra)`
  变异敏感性)—— 改任一前缀枚举字面 ⇒ 红(qa-lead 已核)。

---

## 五、残余与边界(不静默)

- **语义裁定三条(39 接线前生效)**:S-2 回溯抹除(卡字面,含与 AC-8-25 张力)· S-3 状态判重
  替代当帧集(排序判别归 39 复评)· S-4 抑制复查不刷新 lastRecheckTick(涉 OQ-8-1)——
  全部登记于卡 Completion Notes,详 §一。
- **NOT-RUN**:AC-8-45 网络传输子句(BLOCKED-BY-45)· **9/37/39 真接线集成**(M-6,39 Ready
  未实现;测试止于 fake)· `IsHost` 恒 true 下真客户端拦截端到端(S-7,归 45/P1b)·
  AC-8-48 真双进程腿(归 ADR-012 确定性 CI;本测为 in-process 结构面)· [L] 走查归 story-006。
- **登记不修**:M9 命名面防线固有极限(改名即绕 —— 类型/IL 面由 Gates `[D-*]` 族补层,两层各守一面)·
  M6 PlayMode 升 `[UnityTest]`(等 tick 驱动可接)· M10 纯函数双调不可观测(代理断言已注记)。
- **金标重钉** `72db379c` → `7ce0ce27` = **有意识重钉**(新枚举 ReadingForm 4 + JudgmentState 3
  字面入面,+7 行与 doc 历史逐字吻合;结构侧核验非掩盖)。
- 本件即 `review-workflow.md` 要求的评审报告原件(原判定 → 修复落点 → 验证命令);
  **评的是 2026-10-07 修复轮落笔后的当下代码**,原报告经单轮双代理产出、未改任何文件。

---

## 六、突变实跑记录(2026-10-07 · M3 新谓词)

| 注入 | 期望红 | 实测 |
|---|---|---|
| `DiagnosisBoundaryGates.cs` 的 `[D-EXIT-GATE]` 条件改 `if (false && dFull == GrowthExitFullName && …)` | `test_rule4_gateIlNegative_bypassGate_red` | ✅ **恰 1 红且点名**(BoundaryGuardTest 26 条:25 绿 / 1 红,零附带) |

还原(`if (false &&` → `if`,`grep -c` = 0)⇒ 复跑 BoundaryGuardTest **26/26 回绿**。
> M1/M2/S-1/S-5/M4 的判别力为**测试内置双侧自证**(正测 IsEmpty/AreEquivalent + 影子 IsNotEmpty
> 同机),突变面已在 §四逐条列出可证伪注入;本轮单跑即证新谓词(唯一新增生产断言)可红。
