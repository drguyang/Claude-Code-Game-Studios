# Sprint 06 — 玩家驱动轮(交互面收口)

> **2026-10-11 开 · 上游**:sprint-05 收口(Phase A/B 全绿 + 第三轮 playtest 5A + M2 Exit 复评 `b0df422`)
> **本 sprint 判据**:P0 核心循环七环节(走近→查体→对照→落笔→开方→施治→纸面反馈)**玩家可玩闭环**
> **规模**:≈4–6 人日(sprint-05 行 6 估 ~2 天 × 交互面扩 2–3 倍)
> **规划依据**:5 专家代理(producer / lead-programmer / qa-lead / systems-designer / technical-director)
> + 1 轮查体分叉对撞裁决(all 2026-10-11,详见 §6)

---

## §1 背景:为什么接线后还要「玩家驱动」

sprint-05 把 M2 核心循环的**事件写手全部接入运行期 tick 驱动**(病史流
DiseaseOnset → CaseOpened → EmergencyAttempt → EmergencyTreatmentApplied),
但四支玩家行为仍是**自动化接线形式**(急救自动触发、知体/落笔/开方无入口),
42 脉案值区因 F2 词表 tripwire 恒空行。

M2 Exit 复评(`m2-exit-recheck-2026-10-11.md`):#2/#8 维持 ◐ ——
剩余缺口 = **玩家驱动缺失**,#2 处方腿 / #8 形态件 ②③④ 施加点。
本 sprint = 把「玩家」接进每条链:**意图事件走 ADR-009 §七 三段式**,
不做任何新机制(六条候选经 tech-director 判定:1/3/4 沿用机制零新 ADR,2 经对撞收敛为派生态)。

---

## §2 任务清单

### G1 查体入口 + 脉案值区非空(9 / 8 / 39 / 42 · **咽喉**)

| 项 | 落点 | 依赖 | 判据(可证伪) |
|---|---|---|---|
| G1.1 移除投影 tripwire | `Gameplay.Boot/DiseaseVitalsService.cs:167-172`(signs 非空即 throw)+ `ProgressionEvaluator` 产词 | 无 | 求值器产词条 ⇒ GetVitals 不再拒收;删 throw 后 mask 真实传输(变异:删 throw 后断言仍绿 = 掩码说谎,恰红) |
| G1.2 8 纯函数求值门面 | `Gameplay.Presentation/Diagnosis`(与 `DiagnosisVitalsFacade` 同族,零字段):`(VitalsDto, QueryLevel, 两tick, 8烘焙表) → {SignLexemeRow集, PrecisionSlot}` | G1.1 | EditMode:同输入两次调用逐字相同;8 程序集 `IEventSink` 引用集空(`[D-TREF]` 绿) |
| G1.3 已查/未查 + tick 住 39 分册 | 39 侧存储 + 查询接口(snapshot_tick / last_recheck_tick,**player 分册** —— 铁律⑤「每人一本」) | — | 重载保册:两玩家技能 5/50 同病人同动作,39 各自存册、病史流哈希逐位相同 |
| G1.4 查体意图 → 第二 QoS 上行 | ADR-001 第二 QoS(意图语义归 10,D-8-3 现有登记) | — | 玩家查体意图**不落三流**;零新 Kind |

**乙案裁决(对撞 2026-10-11):revealed = 派生态不进三流**,但三项修正:
① 乙方理由「与 ADR-016 §一同构」错 —— 查体**意图**是判定输入,按 ADR-011 F1 Amendment B 走第二 QoS(不是「决策不进流」);
② 甲方「闭集无支 ⇒ 必新增」不成立 —— Q1 判据根本不进注册表;
③ 双方漏账 —— 「查了没有」若只活在 8 静态表,重载必丢 ⇒ **已查/未查 + two tick 住 39**(GDD 已裁「8 持规则,39 持值」)。

### G2 落笔写者 + 墨龄派生态(37 / Boot)

| 项 | 落点 | 依赖 | 判据 |
|---|---|---|---|
| G2.1 `JudgmentRecorded` 写者 | 新 `JudgmentWriter`(住 `Gameplay.Boot`,仿 CaseOpenWriter 同构;**8/42 禁持 IEventSink** ⇒ 写者必须住装配层,登记「37 写者落点 = 装配层」归 ADR-008 Amendment 或 ADR-031) | 开案已在跑 | 落笔 ⇒ `JudgmentRecorded` 入病例流(载荷五字段齐,freehandText P0 不做);`WriterExistenceGate` 豁免**同批撤回**(否则 b7 判据①假绿) |
| G2.2 InkAgeTicks 派生态 | `CasebookVitalsSource.InkAgeTicks` 改扫描 `EventStream.Events` 最近 Judgment Tick(公开只读列表,零新抽象点) | G2.1 | 落笔后 `InkAgeTicks > 0`;改写从**最新一条**重算(变异:固定常量 17 ⇒ 前后不变红;参照首条 ⇒ rewrite 后年龄错红) |
| G2.3 错流防护 | 写者落**病例流**(ADR-008) | — | 落病史流 ⇒ 路由断言恰红 |

### G3 处方驱动(戥子 → 方笺 · 11)

| 项 | 落点 | 依赖 | 判据 |
|---|---|---|---|
| G3.1 `PrescribeRequestDriver` | 新(住 `Gameplay.Boot`,照 CaseOpenedDriver 同构);消费 42 侧 `PrescribeStrokeIntent`(`DentchDoseSelector` 已有)→ `PrescribeRequest` → `PrescribeFlow.Prescribe` → `DrugTreatmentApplied` 入流 | G1(选档需查体产出才有意义)· 21a/20 数据就绪半 | 玩家选档→落笔→`DrugTreatmentApplied` 入病史流→体征变化;≥2 档 ⇒ 不同 `dose` 载荷;不选档零事件 |
| G3.2 端口生产实装 | `IPortionsConversion` / `IPortionsStore` / `ISkillGrowthPort` 生产实装(现垂直切片 Fake 登记) | 21a / 20 库存 | 无货不能开出(变异:删查库存前置 ⇒ 无货仍开出恰红) |

> ⚠️ 若 21a/20 未就绪:G3 降级为「结构性半边」—— 验证 `Prescribe` 有生产调用方 + 载荷入流即可,不做剂量自由度。
>
> **✅ 2026-10-11 用户裁定(替换降级预案)**:G3 改走「**最小生产端口实装**」—— 组合根装配层内联
> **21a 最小 drug 表** + **20 最小库存面**(`HasPortions`/`ConsumePortions` 真行为)+ **30 装配**
> (`SkillGrownEmitter`),令 G3 判据**全判**;21a/20 完整实现(烘焙装载面 / 库存账本 / 世界流消耗)
> 独立排期后只换端口实现,装配签名零改动。此裁定已落 `production/milestones/README.md` M2 #2
> 与 §4 风险表(详见 §2 G3 / §4 →G3←)。

### G4 急救手动化(移除自动触发 · 10)

| 项 | 落点 | 依赖 | 判据 |
|---|---|---|---|
| G4.1 移除自动触发 | `Gameplay.Boot/EmergencyAttemptDriver.cs` 的 `_triggeredPatients` 自动分支(注释自陈 P0 接线载体)删除 | — | **零输入观察窗,病史流零 `EmergencyAttempt`**(变异:漏删一支 ⇒ 无输入却有事件恰红) |
| G4.2 玩家手动链 | 直读通道 Arm/End → `EmergencyAttemptAggregator` → `HostEmergencyProcessor.Process`(Method/ActorId 补全);`EmergencyCandidateList` + `EmergencyDirectReadChannel` **生产实例化**(现零) | Emergency 候选集判据 | 玩家选候选 ⇒ EmergencyAttempt + EmergencyTreatmentApplied 双事件;跳过 = Method=Skip 同形状 |
| G4.3 候选集真判据登记 | 病情阈值归 9/10 **未裁** ⇒ 登记「另开 ADR 或归 9 GDD 轮」;`cause` 取值(降级 vs 玩家选择)= 实现期义务 | — | P0 沿用「在场且未触发过急救」候选集,不阻塞 |

> ⚠️ **qa-lead 红线**:`emergency_attempt_wiring_test.cs` 现断言「启动即自动两事件」—— 手动化**必须改造或废弃**该测试,否则旧裁切面恒绿(假绿)盖住手动接线。

### G5 验证与收口

| 项 | 判据 |
|---|---|
| G5.1 全量门 | EditMode 基线 3147 → ≈3180–3200 · PlayMode 132 → ≈140–145(qa-lead 估:EditMode +40–60 / PlayMode +6–10) |
| G5.2 第四轮人工 playtest | 手柄/键鼠走通七环节:走近→查体→对照→落笔→开方→施治→纸面反馈;三流每笔有合法写者 |
| G5.3 M2 Exit 复评 | #2(处方腿)→ 勾;#8 ①④(G1)、②(G2)、③(G4)→ 逐条复评;五族条 019-b Blocked 不阻塞 |

---

## §3 分阶段验收

- **G1 完成**:脉案值区非空(玩家查体驱动,8 词表真产出)、查体意图走第二 QoS、39 分册在跑
- **G2 完成**:`JudgmentRecorded` 入病例流 + `InkAgeTicks` 派生态 >0 + 豁免撤回
- **G3 完成**:玩家开方 → `DrugTreatmentApplied` 入病史流 → 体征变化
- **G4 完成**:零输入零急救事件 + 玩家手动链双事件 + 候选列表生产实例化
- **本 sprint 完成(2026-10-11)**:G1–G4 全绿(EditMode **3185/0/1/46** · PlayMode **133/133**)+
  G5 全量门已跑 + M2 Exit #2/#5 转闭、#8 ◐ 施加点接大半(复评原件
  `production/qa/evidence/m2-exit-recheck-sprint06-g5-2026-10-11.md`);剩余 = 第四轮人工 playtest +
  玩家输入入口 + ③ 候选列表生产实例化(归下一交互轮)。

---

## §4 风险与依赖

| 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|
| G1 咽喉(查体接线)延期 ⇒ G2/G3 顺延 | 中 | 高 | **优先保 G1**;G2/G3 可顺延 sprint-07 |
| **21a 配方表 / 20 库存未就绪 ⇒ G3 无档位** | 中 | 中 | **✅ 2026-10-11 用户裁定(替换原降级预案)**:G3 改走「**最小生产端口实装**」—— 组合根装配层内联最小 drug 表(21a)+ 最小库存面(`HasPortions`/`ConsumePortions` 真行为,20)+ `SkillGrownEmitter` 装配(30),G3 判据**全判**(≥2 档 dose 区分 · 不选档零事件 · 无货不能开出变异恰红);21a/20 完整实现独立排期,落地后只换端口实现、装配签名零改动。原「结构性半边」预案作废,移出 §5 备注(裁定详情见 §2 G3) |
| G4 候选集真判据未裁 | 低 | 中 | 登记不走:P0 沿用在场集 |
| 重置/掉级跨存档语义(G1.3)触到 7a | 低 | 低 | 39 分册先行,7a 持久化归 Phase 2 |

## §5 不做

- **reveal 五法动画 / 13 姿态表现 / 44 叩听变体库**(占位音/文字条目顶住)
- **教学纸近景 PaperCloseup48 / 教程流程 · 联机同步 · VR 急救 · 笔迹美术纵深**
- **自由书写 freehandText**(ADR-029 ①修正已裁,编码通道归后续)
- **新版 Kind** —— 查体/落笔均走既有机制,零新增 Kind(对撞裁决乙案)

---

## §6 规划证据(2026-10-11 五专家 + 对撞)

| 来源 | 结论要点 |
|---|---|
| producer | 目标集 G1–G5;必保 G1+G4;G3 链式可顺延;咽喉 = G1 |
| lead-programmer | 落点/接缝核实;两条勘误(急救非 400 tick、② 根因在投影 tripwire);T0 数据装载是共享缺口 |
| qa-lead | 四空位判据组(阳性/阴性/变异/禁借绿);G4 必须改造自动断言测;回归基线估 |
| systems-designer | 七环节流程图;必进四项;纸面可读性三约束(零数字/延迟一 tick/双输入) |
| technical-director(①②) | ①11/10 沿用零新 ADR;②37 写者住装配层;③9 查体进流 vs 不进流分叉 |
| **对撞裁决** | 乙案成立(revealed = 派生态零新 Kind),但纠正:意图走第二 QoS(非 ADR-016 §一同构)、「已查/未查」住 39 分册 |

**用户裁定(2026-10-11)**:采纳乙案。本 sprint-06 即按此落盘。