# Sprint 12 — 治理债结清

**周期**:2026-10-16 起,**不设结束日期**(用户裁定 2026-10-15 —— 贴合真实吞吐,治理债清完即收)
**规划依据**:`production/retrospectives/retro-sprint-11-2026-10-15.md`(行动项 5 条 + 产能结论)

## Sprint Goal

清掉 sprint-11 结转的**治理债** —— 人工走查前置 Driver bug、case-system 四件 In Review 的
顺延面、EPIC 口径刷新、两条「建闸」行动项落地;并**重建产能模型**。

> **⚠️ 本 sprint 起换度量口径**(行动项 1 落地):**不再用 `estimate_days`**。sprint-11 声明 8 天 /
> 可用 6.4 天,实际 2 个执行日清 16 件 —— 旧「solo 天数」与真实吞吐脱节 **2.6×**,超容规则因此
> 从未生效。新口径 = **复杂度(S/M/L)+ 依赖数**,实际吞吐由 sprint-11 反推基线监督。

## Capacity

- **件/日基线**:sprint-11 实测 16 件 / 2 执行日 = **8 件/日**(治理债类)。
- **本 sprint 预算**:Must 7 件(2×S + 5×M)+ Should 2 件(1×S + 1×L)+ Nice 1 件(人不可代劳)。
- **并行纪律**:同时在跑 ≤ 3 件;超限即拆分或顺延讨论。
- **时长**:不设结束日期 —— **做完即收,收口时补记结束日**(治 sprint-11「无结束日期即收工」的反面)。

## Tasks

> ⚠️ **S12-7 销项订正框(2026-10-15 规划期「重扫当前状态」抓到 · Phase 1 硬前置的第一次兑现)**
> **原 S12-7**(承接 sprint-11 story-001:107 的顺延登记)写「AC-37-06 判据算术上不可证伪 ⇒ **须退设计重述**」。
> **实测该前提已过期** —— 与 sprint-11 S11-4/S11-5 的「stale 前提」同型,但**方向更隐蔽**:那次是「实现早于计划」,
> 这次是「**设计裁定早于 story 自己的顺延登记**」。
> **三处核实**:① `design/gdd/case-system.md:1121` 已有 **2026-10-15 评审订正(收窄裁定)**,四步明列:
> 完整命题当前不可测(7a/45 未实现)/ 可测半边 = 生产 `ReconstructPatientHighWater` 由事件流重构 /
> 未测半边记 `BLOCKED-BY-7a/45` / 原形态不可证伪,修复 = 改打生产载体;
> ② 代码已按 ②④ 落地 —— `case_identity_routing_test.cs:261` docstring 明写「打的是**生产** `IdAuthority.ReconstructPatientHighWater`
> (非测试内自造 max)」,`:298` 另有**全哨兵流 `{-1,-1}`** 专项用例(正是不可证伪点的正面覆盖);
> ③ `story-001` 已 `Complete ✅ 2026-10-15`,B-1 测试半边经窄审通过(边界 = 只覆盖测试半边)。
> ⇒ **无事可做的是「重述设计」这一半**,已由 GDD 裁毕;**真残余 = 文书回填**(story-001:107 顺延行 + EPIC 口径 + BLOCKED-BY 登记)。
> **若不做这次重扫,S12-7 会变成一件假活**:按旧前提去「退设计重述」= 推翻一个已 Accepted 的收窄裁定 = **范围外破坏**。
> **同类纪律**:这是本 sprint 第二次「计划承接自结转,而结转自身滞后于磁盘」—— 第一次是 S12-5 拆 5a/5b 时的 002–004 已清面。

> ⚠️ **S12-9 退回框(2026-10-15 · 勘察后触发收缩判据 · 用户裁定「退回单独立项 + 立读侧解码 ADR」)**
> **S12-9 的验收条件自带退出口**(卡面 `story-002:112` 逐字):「若『需补 / 不存在』**≥ 2 路** ⇒
> **退回单独立项**,不在 S12-9 硬吃(承 sprint-12 Risk「链深即退」)」。
> **勘察实测:四入向 P / C / D / U 全部「需补」或「不存在」= 4/4 ⇒ 触发。**
>
> **根因不是四笔独立欠账,而是同一条结构性缺口** ——
> `unity/Assets/Sim/Sim.asmdef:4` 的 `"references": [ "Sim.Contracts" ]`(+ `noEngineReferences: true`)
> ⇒ `Sim` 引用集**恰 = {BCL, `Sim.Contracts`}**(ADR-025 §① / b2 门),而 **`PayloadCodec` 住 `Sim.Codec`**
> ⇒ `Sim` **物理上够不着任何载荷内容**(`PayloadRef` 只有 `BlobId/Offset/Length`,不含字节)。
> 而 `Judge` 按定义就要读 P / C 的**内容**(GDD `:263` 真值源登记:「`P` = 判断记录(病名 / 置信度)·
> `C` = 处置适切性」)。story-001 已自陈绕开此墙(`ConsequenceInputAggregator.cs:11-13`:
> 「本件只操作 `SimEvent` 头部 … **从不读 `Payload` 内容**。P / C 的**内容** … 归 story-002」),
> 把问题**原样**交给了 story-002;`CaseCloseDecider.cs:177-181` 是同一堵墙的第二次绕行。
>
> **这是 ADR-029 的对称缺口** —— ADR-029 立了**写侧** `IPayloadEncoder`(第七抽象点),
> 而**读侧全库零抽象点**(`grep "interface IPayloadDecoder|IPayloadReader|IPayloadCodec"` = **0 命中**)。
> **⇒ 补法须 ADR 级裁决,不在 story 自决面内。**
>
> **用户裁定(2026-10-15)**:① **S12-9 退回单独立项**;② **立读侧解码 ADR = 甲案**
> (新增 `IPayloadDecoder`,住 `Sim.Contracts` / 实现住 `Sim.Codec`,与 `IPayloadEncoder` 严格对称 ——
> **`Sim` 引用集一字不改**,门 A / b2 / ADR-025 §① 继续成立)。
>
> **四条独立于根因的缺口(即便解了上面那条也仍在,一并登记)**:
> ① **D 侧 `treatable_by` 门自身结构性缺位** —— `ProgressionEvaluator.cs:336-338` 自陈
> `TreatableBy` 是派生字段、**恒 null**;真源 `disease_action_axis.json` 的装载面未接进 sim 路径(归 9 的装载轮);
> ② **判定表数据不存在** —— `assets/data/53_outcome_table.json` **无此文件**(卡面 Note 1 点名它是核心交付物);
> ③ **53 零装配层** —— 全仓除定义件与测试外**无任何代码引用** `ConsequenceInputAggregator` / `Inputs53`;
> ④ **AC-53-03 零浮点门无载体 · AC-53-02/15 的 `tests/golden/medcons/*` 不存在**。
>
> **待裁口径(与退回与否无关,一并登记)**:处置记录的 Kind 闭集**四处不一致** ——
> GDD 规则五 `:165` 只列 `CareApplied` vs 卡面 Note 4 提 10/11 两支;实现取 `careKinds` **显式参数**
> (无生产调用点决定它)。归 **53 的 GDD 轮**。
>
> **原件**:`production/qa/evidence/recon-s12-9-medcons-outcome-judge-2026-10-15.md`(含七条可复核判据)。
> ⚠️ **本件是勘察不是评审** —— 未跑任何测试、未改动任何生产码,不产生「已绿」声称。

### Must Have (Critical Path)

> ⚠️ **S12-0(规划期新立 · 前置 · 用户裁定 2026-10-15)** —— **批 1 起手,先于 S12-1**:
> **伪基线传播**(5 个文件把 `3366/3320/0/46` 当 sprint-11 终值,真终值 = **`3388/3342/0/46`**)。
> **根因 = 时序**:`smoke-2026-10-15.md` 落于 `ff21027`(**10:59**,DoD 收尾时点),如实记了**当刻**值;
> 批 6(ADVISORY 全清)在 `a5ef7c5`(**13:50**)才把数推到 `3388/3342/0/46`。
> **失守处 = smoke / signoff 件不记「截至时刻 + 批次」**,只写裸数 + 「逐位一致」⇒ 下午批 6 后,
> 早上的记录**静默变成伪基线**,并被 `qa-signoff-sprint-11` / `medical-consequences/story-001` /
> `fix-s11-13` / `fix-s11-14` 四处引用继承。
> **处置(用户裁定)**:五处**补订正注**,**不改原行**(原值是其各自时点的真实实跑,改数 = 伪造当时记录,
> 踩「评审不可事后追补」纪律);`sprint-11.md` **不动**(它每批记当批值,自洽)。
> **同批建闸**:smoke / signoff 模板加**「截至时刻 + 批次 + commit」三字段**,禁裸数。
> **判据**:`grep -rln "3366/3320" production/ docs/` 的命中**只剩** `sprint-11.md`(当批值,合法)+
> 五处各自的订正注;新件无裸基线数。

| ID | Task | Owner | Est | Dependencies | Acceptance Criteria |
|----|------|-------|-----|--------------|---------------------|
| **S12-0** | **伪基线订正 + smoke/signoff 模板建闸**(见上方框) | Claude | **S** | 无 | 五处补订正注(原值保真 + 真终值溯源到 `a5ef7c5` 13:50);模板加「截至时刻 + 批次 + commit」三字段;判据见上方框末行 || ID | Task | Owner | Est | Dependencies | Acceptance Criteria |
|----|------|------|-----|--------------|---------------------|
| **S12-1** | **产能模型重建**(行动项 1) | Claude | S | 无 | `production/CAPACITY-MODEL.md` 落盘:以 sprint-11 的 16 件/2 执行日 / 68 commits 为基线,给出「件/日」与「复杂度权重」两列;**废止旧的 8 天/6.4 天口径**;并回填 sprint-09/10 逐件数据使 velocity 表可判。判据 = **sprint-12 收尾时 `/sprint-status` 确实引用了它**,否则文档即失效 |
| **S12-2** | **修 `Story009WalkthroughDriver` 的 `passed` 汇总 bug**(行动项 4 / S11-6 前置) | Claude | M | 无 | Driver 汇总行与实际断言一致(消除「全绿但汇总错」);带夹具:注入 1 条失败 ⇒ 汇总如实报 1;零产品码变更 |
| **S12-3** | **处方前置检查入 control-manifest**(行动项 2) | Claude | M | 无 | control-manifest 新增硬规则:凡处方涉及 `Sim` 侧引用集(`{BCL, Sim.Contracts}`),**写入前先验可达性**;并回填 S11-7 / S11-8 ×2 三次违规为**已登记案例**(同族根因不得第四次) |
| **S12-4** | **假绿夹具强制证伪轮入 DoD**(行动项 3) | Claude | M | S12-3(同属「建闸」) | `/story-done` SKILL DoD 增一条:定序 / 共存 / 共存-收窄类 AC **必须附「回退即转红」证据**;回填 S11-9 假绿案例具名 |
| **S12-5a** | **case-system EPIC 口径刷新(002/003/004)** | Claude | S | 无(三件顺延面已清) | `EPIC.md` §Epic Status 与 story-002/003/004 `Status:` 逐字一致;三件已清顺延面 ⇒ **可翻**;**不通篇重写**,只订正口径行 || ✅ **2026-10-15 完成**:EPIC 表三行翻 `Complete ✅ 2026-10-15 (S12-5a 口径刷新)`;头部订正为 `⚠️ 5/6 Complete(仅 001 待 S12-5b)`;**三处旧「四件维持 In Review」结论就地加订正注(原句不删)** —— S11-12…S11-17 六批当日清空顺延面,用户裁定「在 sprint-11 完成这些,不带入下个 sprint」;story-002/003/004 `Status:` 均为 `Complete ✅ 2026-10-15`,逐字一致。⚠️ 明确不代翻 001(B-1 设计侧已裁于 `GDD:1121`,残余文书回填归 S12-7') |
| **S12-5b** | **case-system EPIC 口径刷新(001)** | Claude | S | **S12-7'** | story-001 `Status:` 与 S12-7' 结论一致;001 已 `Complete 2026-10-15`(窄审通过)⇒ 本件把 EPIC §Epic Status 的「001–004 In Review」口径订正为逐字一致 || ✅ **2026-10-15 完成**:EPIC 头部 `5/6` → **`6/6 Complete`**;表内 001 行 → `Complete ✅ 2026-10-15 (S12-5b 口径刷新)`;四处历史 `In Review` 句**全部加订正注(原句不删)**,并**顺带订正两处活口径错误** —— ① CB-3「未触及 ⇒ 归 sprint-12」实为 **S11-15 已闭** ② story-004 行「S12-6 进行中」→ **已完成**。**逐字核验**:头部 / 表内四行 / 四件 story `Status:` 行全对齐 || **S12-6** | **case-system 写者侧「同 tick 批量跨阈值」夹具** | Claude | M | 无 | TA-7 已覆盖检测器侧;本件补**写者侧**:同 tick 跨阈值达 `PATTERN_THRESHOLD` ⇒ 只发一条 `PatternRecognized`,带「删门即发 N 条」反证 || ✅ **2026-10-15 完成**:`pattern_recognition_writer_test.cs` +7 条(36→43);全量 `3394/3348/0/46` → `3401/3355/0/46`(**+7/+7 零回归**);**三处独立反证均成立**(停 `IsFired` 门 ⇒ 6 红 / 停 `candidates.Sort` ⇒ 4 红 / 守卫 `<2`→`<3` ⇒ 1 红),三次「改→跑→还原」字节相同 + 探针零残留;**产品码零改动**。原件 = `production/qa/evidence/fix-s12-6-writer-same-tick-batch-2026-10-15.md`;`story-004` §六②-5 残余已销项 |
| ~~**S12-7**~~ | ~~**case-system 001 B-1**(AC-37-06 判据算术上不可证伪,须退设计重述)~~ **→ 销项 2026-10-15(规划期重扫发现无事可做)** | — | — | — | 见下方订正框 |
| **S12-7'** | **AC-37-06 口径回填 + BLOCKED-BY 登记**(原 S12-7 销项后的**真残余**) | Claude | **S** | 无 | ① `story-001:107` 顺延行订正为「设计侧已裁(`GDD:1121` 四步),残余 = 回填」;② 显式登记未测半边 **`BLOCKED-BY-7a/45`**(禁借绿);③ 核实生产载体 `ReconstructPatientHighWater` 三态用例(含全哨兵流)已在册;④ **不重述设计** —— GDD 已于 2026-10-15 裁定 || ✅ **2026-10-15 完成**(**纯文书回填,零代码改动**):① `story-001` :107 顺延行加 🔴 订正注(原句不删)—— 三项已当日于 sprint-11 就地闭合;② 未测半边 **`BLOCKED-BY-7a/45`** 已入 story-001 §Test Evidence(显式「禁借绿」);③ 三态用例在册已核(`test_highWaterMark_sentinelMinusOne_noPollution`,以**方法名**引用而非行号);④ 未重述设计。EPIC 加指针 ⇒ **001 翻转条件已满足,归 S12-5b** |

### Should Have

| ID | Task | Owner | Est | Dependencies | Acceptance Criteria |
|----|------|------|-----|--------------|---------------------|
| **S12-8** | **case-system 001 B-2 / B-4 kindgen 复验**(S11-12 已闭合,此处为**复验**) | Claude | S | S12-6 同族 | 具名复核 S11-12 两件的判据是否真判(kindgen A2 严格形 + 唯一 CI job 载体);若发现假绿即立件,不追认 || ✅ **2026-10-15 完成**:**未发现假绿 ⇒ 不立件**。三条独立证伪均按预期转红且原子还原零残留 —— ① 对**真实产品文件**(`CasePayloads.cs`)施 int→float 变异 ⇒ `--check` **EXIT=1**(探针只证内存副本,此面为探针**未覆盖**处,本件补做)② 探针**基线零报告**(未变异 A2 报 0 条 ⇒ 「捕获」非恒真)③ 中和 A2 ⇒ 探针报 **6 逃逸 EXIT=1**(逃逸路径可达,非死代码)。**S11-12 §七 不确定项 Q(A1 是否删)结案 = 保留**(A1 打生成物非扫描复刻)。原件 = `production/qa/evidence/reverify-s12-8-kindgen-2026-10-15.md` |
| ~~**S12-9**~~ | ~~**53 story-002 `Outcome` judge 实现**~~ **→ 退回 2026-10-15(勘察触发收缩判据 · 用户裁定)** | — | — | — | 见上方 S12-9 退回框。**勘察实测四入向 4/4 需补/不存在 ⇒ 触发卡面自带的退出口**(`story-002:112`)。根因 = `Sim` 够不着载荷内容(ADR-029 的读侧对称缺口),须**另立 ADR**(甲案 `IPayloadDecoder`)后方可动手。原件 = `production/qa/evidence/recon-s12-9-medcons-outcome-judge-2026-10-15.md` |
| **S12-11** | **【新立 · 承接 S12-9 退回】** 读侧解码 ADR:新增 `IPayloadDecoder`(甲案) | Claude | **M** | 无(勘察已毕) | 另开 ADR:读侧抽象点住 `Sim.Contracts` / 实现住 `Sim.Codec`,与 `IPayloadEncoder` 严格对称;**`Sim` 引用集一字不改**(门 A / b2 / ADR-025 §① 继续成立)。须含:① 与 ADR-029 的对称性论证;② 引用集不变的结构性证明;③ 第七 → 第八抽象点的计数订正涟漪(承 ADR-029 的「六 → 七」先例);④ 实现拆「契约支 + 接线支」两支 || ✅ **2026-10-15 完成**:ADR 落盘 `docs/architecture/adr-031-payload-decoder-abstraction.md`(**Accepted** —— 用户两轮裁定,§Decision 五项全确认)。五项裁决:① 第八抽象点 `IPayloadDecoder`(住 `Sim.Contracts`)· ② **抛形** `T Decode<T>(EventKind, in PayloadRef)`,与 `Decode<T>`/`Encode<T>` 对称、与 `TryGetPayload` 刻意分家 · ③ **池在实现构造函数**(承 ADR-029 §② 模式 ⇒ `IBlobPool` 不搬、**零新增程序集边**)· ④ **接口不含 freehand 路径**(G-3 升为编译期)· ⑤ **b5 门补两项**(`AssemblyGates.cs:381-385` 实测**漏了 `IPayloadEncoder`** ⇒ 44 今日可合法引用编码器)。**起草期两处自查修正**:struct 数 34 → **35**(实测)· 补模板必需的 `### Architecture` 节。**⚠️ V-1…V-10 全部未勾,禁借绿**(实现未落) |

| **S12-12** | **【新立 · ADR-031 实现轮】** 契约支(`IPayloadDecoder` + `PayloadDecoder`)+ 接线支(b5 门补两项 + V-8 逐处登记)+ V-10 计数订正 | Claude | **M** | S12-11(ADR Accepted) | ADR-031 §Validation V-1…V-10 全勾;S12-4 证伪轮三探针(定义域收窄 / 共存互斥 / 抛形退化)全红;回归零 | ✅ **2026-10-15 完成**:① 契约支 —— `Sim.Contracts/IPayloadDecoder.cs` + `Sim.Codec/PayloadDecoder.cs`(`IBlobPool` 不搬、**零新增程序集边**);② 接线支 —— b5 门 8 → **10 项**(补 `IPayloadEncoder`【ADR-029 漏项】+ `IPayloadDecoder`)+ 错误串「六抽象点」→「八抽象点」;③ **V-8 实测 11 处**(ADR 原稿只写**三处** —— 已就地订正 + 逐处裁定「退 6 / 暂不退 5」);④ **V-10 计数订正**:A 组 **25 处**改 · B 组 3 改 + 4 注 · C 组历史件 6 注 + 现行件 9 注(并纠正原清单两处错);⑤ **S12-4 证伪轮 3/3 全红**(探针 1 首次因「编辑与跑分属两次调用致 trap 提前还原」而假绿,重做后正确转红);⑥ 回归 `3421/3375/0/46`(基线 `3401/3355` ⇒ **+20/+20,零回归**)。**证据原件**:`production/qa/evidence/impl-adr031-payload-decoder-2026-10-15.md` |

### Nice to Have

| ID | Task | Owner | Est | Dependencies | Acceptance Criteria |
|----|------|------|-----|--------------|---------------------|
| **S12-10** | **S11-6 结转:11 人工走查 AC-11-12/13/21** | **user + 三签**(主创 / 医学从业 / 音频 lead) | — | **S12-2** | 真人走查 + 三签落盘。**agent 不可代劳** —— 前置满足即可启动,不占 sprint 治理容量 |

## Carryover from Previous Sprint

| Task | Reason | New ID |
|------|-------|--------|
| S11-6 人工走查 AC-11-12/13/21 | 需真人 + 三签;前置 Driver bug | S12-10 |
| case-system 写者侧「同 tick 批量跨阈值」夹具 | 批 6 未覆盖 | S12-6 |
| case-system EPIC 状态刷新 | 四件 story 转 Complete 但 EPIC 口径未同步 | S12-5 |
| CI 真 runner 验证 | 离线做不到(**未闭环**) | **不在本 sprint** —— 需 GitHub 环境,agent 侧无解,单独立项登记 |

> ⚠️ **不在本 sprint、如实登记的三项**:① 写者「生产触发点接线」—— 仍需装配轮(先例 `CaseOpenWriter`
> 同样未接线);② CI 真 runner;③ case-system 001 B-1 的**用户裁定轮**—— 它在 S12-7 内,但需你参与,
> 不是 agent 单方可闭。

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| S12-7 判据重述**可能推翻既有结案判定** | 中 | 高 | 先走裁定轮、**先不实现任何东西**;通过后才动 S12-5 对应口径 |
| 「建闸」两条(S12-3/S12-4)又变没人用的空规则 | 中 | 中 | 条文必须带**回填案例**(三次违规 / S11-9 假绿);S12-4 由 `/story-done` **机械检查**回退证据,不靠自觉 |
| 产能模型又建一份没人引的文档 | 中 | 中 | 判据即「sprint-12 收尾时 `/sprint-status` 引用了它」—— 不引用即失败 |
| 53 story-002 依赖链比预期深 | 中 | 中 | 先做依赖面勘察再动手;**链深即退回**,不硬吃 L 件 —— ✅ **2026-10-15 该风险已兑现并处置**:勘察实测 4/4 入向需补 ⇒ S12-9 退回,另立 **S12-11**(读侧解码 ADR 甲案)。**退出口是卡面自带的判据,不是临时让步** |

## Dependencies on External Factors

- **S12-10 人工三签**(主创 / 医学从业 / 音频 lead)—— 项目外部,不可催。
- **S12-7 用户裁定轮** —— 需你参与设计重述方向。
- CI 真 runner 环境 —— 需 GitHub 侧配置。

## Definition of Done for this Sprint

- [x] All Must Have tasks completed(S12-0 + S12-1…S12-4 + S12-5a/5b + S12-6 + S12-7')
  - ✅ **S12-5a 完成 2026-10-15**(002/003/004 翻绿;001 不代翻,归 S12-5b)
  - ✅ **S12-6 完成 2026-10-15**(写者侧同 tick 批量夹具 +7,三处反证成立)
  - ✅ **S12-7' 完成 2026-10-15**(纯文书回填;`BLOCKED-BY-7a/45` 已登记 ⇒ **S12-5b 已解锁**)
  - ✅ **S12-5b 完成 2026-10-15**(EPIC 口径刷新 ⇒ **case-system 6/6 Complete**)—— **All Must Have 全部完成**
- [x] 各任务按衍生验收标准通过,**每条附可证伪判据** —— 不用「绿了」代替
      **2026-10-16 收口核账**(10 件 done / 0 件未证伪):
      测试型「回退即转红」6 件 —— S12-2 · S12-4 · S12-6 · S12-9(**18/18 RED**)·
      S12-11(3/3 全红)· S12-12;
      断言型(可逐条核、无测试面)3 件 —— S12-0(五处补注)· S12-1(见下条)· S12-3(三次违规回填)。
- [x] **产能模型落盘且在 sprint-12 收尾时被 `/sprint-status` 引用过**(行动项 1 的判据,不是写份文档)
      ✅ **2026-10-16 已兑现**:`/sprint-status 12` 输出含「### 产能(无结束日期 —— 依
      `production/CAPACITY-MODEL.md`)」段,三旋钮(复杂度 C / 并发上限 P / 判别力门槛 D)逐项给出。
      ⇒ 行动项 1 的判据成立,`CAPACITY-MODEL.md` **不是一份没人引的文档**。
- [x] control-manifest 新增「处方前置检查」硬规则 + 三次违规回填
      ✅ `docs/architecture/control-manifest.md:536` §Cross-Cutting Constraints 之七(S12-3 立)
- [x] `/story-done` DoD 新增强制证伪轮条款 + S11-9 案例回填
      ✅ `.claude/skills/story-done/SKILL.md:145` §Falsifiability Gate(S12-4 立,
      含 S11-9 假绿案例具名 + 「Gate 不通过 ⇒ 不得转 Complete」BLOCKING 措辞)
- [x] Design documents updated for any deviations
      ✅ `design/` 零改动(返工未改 GDD 语义)⇒ 无 GDD deviation 需记录;
      偏离全部落在 story-002 §返工轮登记(3 处 AC 缺口 + 2 处形状留槽 + 9 条 Minor)
- [x] Code reviewed and merged
      ✅ story-002 两轮 `/code-review`(首轮 CHANGES REQUIRED → 重评 APPROVED WITH SUGGESTIONS),
      两份原件俱在。⚠️ **未 commit** —— 依「No commits without user instruction」待用户指令。
- [x] **`end` 日期在收口时补记**(治 sprint-11 反面)
      ✅ `end: "2026-10-16"`(见下 §收口记)

## 附带发现(规划期实测,非本 sprint 范围但须登记)

- **M3 实测 = 21/31 epic 全 Complete**(`production/milestones/README.md:49` 仍写 16/31,陈旧)。
  `save-slot-ui`(7b)只有 README + 1 件 story、**无 `EPIC.md`**(目录壳,`Status: Ready`)。
  ⇒ **case-system 是 M3 临门一户**:翻绿即 22/31。**不得为凑数放松判据**。
- **无跨 epic 新增 story**(53 story-002 / case-system 均为既有 story 推进)⇒ 不需 `/scope-check`。
- **S12-9 勘察的附带发现(2026-10-15,非本 sprint 范围但须登记)** ——
  **ADR-029 的读侧对称缺口**:写侧 `IPayloadEncoder` 已立,读侧**全库零抽象点**。
  已观测到**三次**同型绕行(`CaseCloseDecider:177-181` · `ConsequenceInputAggregator:11-13` ·
  `PatternDetector:182,197` 的「解码归调用方」委托形),**均以绕开收场**。
  ⇒ 这是一条**跨系统**的结构性缺口,不止 53 一家;S12-11 立的 ADR 应把它作为**通用缝**处理,
  而非 53 专用补丁。

---

## 收口记(2026-10-16)

- **`end` 日期 = 2026-10-16**(依「做完即收,收口时补记结束日」裁定)。
- **⚠️ `start` 口径矛盾(登记,不静默改)**:计划写「2026-10-16 起」,但 git 实证
  **10 件提交全部落在 2026-10-15**(`git log --since=2026-10-14` 全量 10-15)——
  计划声明日**晚于**实际首执行日 1 天。收口时**只补 `end`、不改写 `start` 原文**,
  矛盾如实留档;sprint-13 规划时须先裁「计划日 vs 执行日」的取数口径。
- **产能结论**:10 件 / 1 执行日 = **10 件/日**,高于 sprint-11 基线(16 件 / 2 执行日
  = 8 件/日)**25%**。差异来源 = 件均复杂度低(S×4 + M×5,无 L)+ agent 并行实到 2 路
  + 治理债类判别力判据较轻(断言型占 3/10)。
- **结转 sprint-13**:S12-10(11 人工走查 AC-11-12/13/21 + 三签;agent 不可代劳,
  需 user + 主创 / 医学从业 / 音频 lead)。
- **附带待办(收口时发现,未排)**:`production/epics/medical-consequences/EPIC.md`
  `Status: Ready` + stories 表 001/002/003/004 **四行全 `Ready`**,而 story-002 本次已翻
  `Complete`(story-001 早前亦已收)⇒ EPIC 口径 stale,须 reconcile。
