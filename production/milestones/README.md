# Milestones

**Project**: 《大医精诚:破晓之剂》
**Target**: Production
**Last Updated**: 2026-10-15

---

## 一、MVP 是什么(先定义,后分档)

**MVP = P0(垂直切片)**。它不是「demo」,是**核心假设的证伪装置**:

> **核心假设**(`design/gdd/game-concept.md:689`):玩家会认为「**判断 → 施治**」这个医疗循环
> **本身**就好玩,且「亲手做小游戏」与「跳过用判断结算」两种路径都能带来满足感。
> 全案唯一「**不知道好不好玩**」的东西就是判断层 —— 加人解决不了(`game-concept.md` §优先级排序原则 1)。

**MVP 的内容面**(`game-concept.md:686` §MVP 定义,8 项):
判断链(西医诊断)· 两个急救动作 · 处方用药 · 一间固定医馆 + 小改造 · 极简采集 + 炮制 ·
极简脉案日记 · 格斗线(徒手 + 短兵)· P0 熟练度七项。

**MVP 的系统面**(`design/gdd/systems-index.md:180`):**P0 = 31 个系统**。
⚠️ **两个数不矛盾** —— 8 项是**内容条目**(玩家看得见的玩法块),31 是**系统条目**
(承载这些内容的工程件,含 42 拟物 UI / 44 音频 / 7a 持久化 / 51 遥测 / 52 随机事件导演 / 53 医疗后果等
不直接表现为「一项内容」的地基系统)。

**MVP 的工期基线**:**6-9 个月**(2026-09-14 用户裁定「重算不用,写一个 6-9 就当你重算了」——
`systems-index.md:190`)。⚠️ 该值按 26 项估,后实为 31 项;**用户已裁定不重算,此即基线**,
此后任何文档**不得**自行改算此值。

**MVP 明确不做**(`game-concept.md:719`):联机、VR、开放世界、自由建造、时代事件、精神压力、
辨证与一切中医内容、中药与针灸、装备/商业/公沟/公卫、异步医案 UGC、主机、火器。
⇒ **MVP 是纯西医切片**:有「**诊断**」(西医,看得清),没有「**辨证**」(中医,看得全)。

---

## 二、里程碑分档(5 个)

> **为什么是 5 个而不是 3 个**(2026-10-03 用户批准):
> 原三档(M1 Pre-Prod / M2 Vertical Slice / M3 Production)里,**原 M3「Production」是不可判据的过程陈述**
> (「开发开始了」—— 什么时候算开始?),且 **M2 → MVP 之间隔着两个独立完成面**:
> **机制面**(31 系统骨架齐,靠代码)→ **内容面**(资产与数值冻结,靠美术 + 数值轮)。
> 两者的**阻塞源不同、退出条件不同**,合成一个里程碑会让「做完了吗」无法回答。
> 故拆为 5 个,每个都有**可机械验证**的退出条件。

| # | 里程碑 | 一句话 | 阻塞源 | 当前 |
|---|--------|--------|--------|------|
| **M1** | Pre-Production Complete | 架构与生产档齐备,可以开工 | 硬件选型 / spike | 7/9(**未闭 2**:性能预算 · ADR-023 spike) |
| **M2** | Vertical Slice(**美术待裁**) | **核心循环跑通** —— 机制闭环优先 | 代码 | ✅ **Complete(2026-10-14,Exit 10/10 全勾;Phase 2 = 2/7 系统,余四系统归 M3 推进)** |
| **M3** | Systems Complete | **31 系统机制齐** —— P0 的每一件都在跑 | 代码 + 集成 | **22/31** epic 全 Complete(2026-10-15 实测) |
| **M4** | Content Complete | **42 VS Critical 资产 + 数值冻结** —— 能看了 | 美术 + 数值轮 | 0/42(灰盒豁免中) |
| **M5** | MVP / Release Candidate | 上述合流,可交外部试玩 | 全部 | 未开始 |

> ### ⚠️ **M2 ≠ MVP** —— 这是本次改写最要紧的一条
>
> | | M2 Vertical Slice | M5 MVP |
> |---|---|---|
> | **验证问题** | 「循环能跑通吗」 | 「循环**好玩**吗」 |
> | **美术** | **待裁**(灰盒为主 / 是否含最小真资产 — 见 §六) | **42 VS Critical 真资产** |
> | **系统** | core-loop 子集(7 系统见 §三) | **31 系统全** |
> | **数值** | 占位可调 | **数值轮冻结** |
> | **内容** | 1 病人 + 1 诊断 + 1 治疗 | 8 项内容面全 |
>
> ⇒ **M2 是 M5 的前置验证,不是 M5 的缩小版。** 任何把 M2 的完成读成「MVP 快好了」的表述都是错的。

---

## 三、M2 Vertical Slice 明细(**美术待裁**)—— ✅ **Exit Criteria 全勾(2026-10-14,M2 收口)**

> ✅ **2026-10-14 M2 收口**:Exit Criteria 10/10 全勾 —— 末条「五族切图与 atlas 布局冻结」
> 经复评翻勾(复评原件 `production/qa/evidence/m2-exit-recheck-sprint10-253-2026-10-14.md`,
> 三残留转独立待办,禁借绿声明在档)。**形态面注**:用户 2026-10-03 裁定「形态优先」后,
> 本节「美术待裁」已由「4 项形态件真做 + 灰盒登记表」裁决路径兑现(见下方灰盒登记表与
> Exit Criteria 第 5 条),不再悬置。

**Goal**: 1 病人 + 1 诊断 + 1 治疗 —— **机制闭环**,**灰盒美术**,验证核心循环。

**M2 要证的是什么**(此条为本文**主张**,非已裁定 —— 待多专家轮与用户确认):
「**管道通不通**」(跨格 → 采集 → 诊断 → 处方 → 施治 → 事件流 → 体征变化),
而非「**好不好看**」。
⚠️ **但「机制真」是否**足以**替代「贴图真」是开放问题** —— 拟物 UI(纸质感)本身是 pillar,
若灰盒 UI 会掩盖手感问题,则 M2 的结论失真。见 §六。

> ### ✅ **美术口径已裁(2026-10-03)**
> 用户裁定:**形态优先** —— M2 只做**创意总监 4 项「呈现契约」**,不做 42 项全真,不做全灰盒。
> 依据:`art-bible §8.11 Gray-Box Policy`(2026-10-03 补)—— 灰盒**不得用于验证手感与可读性**,
> 而以下 4 项**全属手感/可读性** ⇒ **必须真做**。
>
> | # | 4 项形态件 | 为何不可灰盒 |
> |---|---|---|
> | ① | 脉案**线格 / 空行 / 焦点明度轴** | 判断链的**读入形态** —— 无血条无小地图,可读性全靠纸面物理形态 |
> | ② | 墨**乾湿两态** | 落笔反馈 = 手感本体(§8.11.1 明列「手感不得用灰盒验证」) |
> | ③ | 急救**零数字 + 可跳过** | 反数值化是 pillar;数字角标会绕开要证的东西 |
> | ④ | **一条真实状态反馈通道** | playtest 要测「玩家读懂了没有」,灰盒无反馈通道可读 |
>
> ⚠️ **①② 的执行前置 = 切图先冻结**:①压在**九宫格切图**上,②压在**墨迹 brush** 上。
> 美术总监裁定(`art-asset-ruling-recommendation-2026-10-03.md` §一):
> **切图族的九宫格切图与 atlas 布局须先定稿** ——
> 切图早错是**全局返工**(牵连全部 USS + atlas,ADR-013 §三),模型晚做只是局部返工。
> ⇒ **不额外铺贴图**,只把 4 项涉及的那几张切图定稿。
>
> ✅ **口径订正(2026-10-04 用户裁定 D)—— 「三族」→「五族」**:
> 原文写「**纸 / 墨 / 铜**」三族,**是该词的措辞误,非技术事实**。实测 16 张
> `*-final.png`(逐张复验)= **五族**:纸 5(`paper_xuan/aged/hemp/burnt_edge` + `border_paper`)·
> 墨 4(`ink_wet/dry/light/dot`)· 卷轴 4(`scroll_cap/rod/knot` + `border_scroll`)·
> 印章 2(`seal_red/surface`)· 图标 1(`ui_icons_sprite`)。
> 「**铜**」族**在 16 张里零资产**(全库 `grep brass|copper|bronze` = 0)—— 该词来自 GDD 的**材质侧**
> 语义(`art-bible §8.1` 用 `ui_paper`/`ui_ink`/`ui_seal`),**不是实物分族**。
> ⚠️ **漏冻即悬空**:按三族枚举会漏掉 `scroll_*` / `seal_*` / `ui_icons_sprite` **三族已入库资产**。
> ⚠️ **铜族缺口另裁(2026-10-04 E)**:焦点高亮的**黄铜 2px** 是「真做」项(见下表)且**无实物** ⇒
> **其最小切片升为 M2 硬前置**(详见本文件 §三 Exit Criteria 第 5 条括注)。
>
> 其余(主角 / 病人 / 医馆 / 敌人 / 环境 / 药材 / 手术灯 / 听诊器)**全部灰盒**,
> 按 §8.11.2 **D1 逐项登记**(清单落本节日录后的「附:M2 灰盒登记表」)。

**Exit Criteria**(可机械验证):
- [x] `interaction-system` 实现(**已闭** —— 7/7 story Complete 2026-10-04;原「关键路径断点」措辞已作废)
- [x] `patient-ai` → `diagnosis-system` → `case-system` → `prescription-medication` 链至少端到端可跑
      **✅ 2026-10-11 T3.3 复评 ◐ 转「机制侧闭环 · 处方腿机制半」** —— 五写者**全部**有运行期生产调用方:
      `PatientSpawner`←`PatientAppearedDriver:100` · `CaseOpenWriter.TryOpen`←`CaseOpenedDriver:59` ·
      `HostEmergencyProcessor`←`EmergencyAttemptDriver`(BootRoot tick 序列)⇒ 运行期病史流完整
      `DiseaseOnset → CaseOpened → EmergencyAttempt → EmergencyTreatmentApplied`
      (patient_appeared / case_opened / emergency_attempt 三 wiring 测 + 第三轮 playtest 世界侧确认)。
      **唯一悬空 = 11 处方腿**:`PrescribeFlow.Prescribe` 全库**零生产调用方**(无「开方」驱动/输入端)⇒
      启动物理上写不出 `DrugTreatmentApplied`(PlayMode 该事件均为测试直喂)。**这是机制半边
      (玩家交互轮未开),非接线缺口** —— 复评原件 `production/qa/evidence/m2-exit-recheck-2026-10-11.md`。
      **✅ 2026-10-11 用户裁定(G3 推进策略)**:处方腿缺口由 sprint-06 G3 承接,
      端口依赖(21a/20/30)改走「**最小生产端口实装**」—— 组合根装配层内联最小 drug 表 +
      最小库存面(`HasPortions`/`ConsumePortions` 真行为)+ `SkillGrownEmitter` 装配,
      G3 判据**全判**(非「结构性半边」降级);21a/20 完整实现独立排期,落地后只换端口实现、
      装配签名零改动。裁定全文见 `production/sprints/sprint-06.md` §2 G3。
      **✅ 2026-10-11 G5 复评转闭** —— `PrescribeRequestDriver`(CompositionRoot:170)+
      真 `PrescribePorts`(最小端口实装)⇒ `DrugTreatmentApplied` **运行期可达**;
      `prescribe_driver_test` 8 测全绿(选档落笔 / ≥2 档 dose 区分 / 无货拒开 / GateHit SkillGrown)。
      复评原件 `production/qa/evidence/m2-exit-recheck-sprint06-g5-2026-10-11.md`。
      **✅ 2026-10-14 sprint-09 S3 复评 —— 翻 `[x]`(勾选滞后订正)**:G5 已判转闭,惟行首未翻。
      本轮**独立实测**(非重读旧件)推翻旧读数「`PrescribeFlow.Prescribe` 全库零生产调用方」——
      真生产调用点 = `PrescriptionPageHost.cs:137`(sprint-08 S2 接线,2026-10-12,晚于旧快照)。
      完整触发链闭合:`CasebookScreen.SubmitDose:482` → `CasebookCommandRouter:174/184` →
      `PrescriptionPageHost:127/137` → `PrescribeFlow.cs:339-341` `Append(DrugTreatmentApplied)`。
      ⚠️ **两处运行期前置约束(诚实登记,不改「可达」判定)**:① 生产库存种子只 1 份
      (`CompositionRoot.cs:258` 唯一生产 `.Grant(`)⇒ 仅「档 1」首次开方能过;② 须先 E 查体绑定病人。
      ⚠️ **未确认**:玩家 `actorId == 种子 id(0)` 的次序不变量无显式生产断言,归第七轮 playtest 人工确证。
      复评原件 `production/qa/evidence/m2-exit-recheck-sprint09-2026-10-14.md`。
- [x] ⚠️ **链的写者存在性前置(2026-10-08 补 · ✅ 2026-10-10 批次 F-6 复评转闭)** —— 上一条链上的每个 `Kind` 至少有 1 个**生产 `Append` 调用点**
      ~~实测生产侧 `Append` 仅 3 处(`HostEmergencyProcessor`×2 · `PrescribeFlow`×1),
      **9/25/37 的写者不存在** ⇒ epic 全 `Complete` 链仍跑不起来(**epic Complete ≠ 写者存在**;
      `vertical_slice_test` 病人腿 `Assert.Ignore` 即此缺口的可证伪表现)~~
      **✅ 2026-10-10 F-6 复评:原读数为 2026-10-08 旧快照,现已满足** ——
      实测写者:`PatientSpawner.cs:77`(9·DiseaseOnset)· `CaseOpenWriter.cs:135`(37)· 10/11 早已在(HostEmergencyProcessor×2 / PrescribeFlow×1)· 25 敌伤在 Sim 侧。
      ⚠️ **本条闭 ≠ 下一条端到端可跑闭** —— 缺口已迁移为「**写者零生产调用方**」
      (`SpawnNext` / `CaseOpenWriter.TryOpen` 全库唯一引用 = 测试)⇒ 运行期链可达性 0%。
      复评原件 `production/qa/evidence/m2-exit-recheck-2026-10-10.md`;原文不删,留闭环记录。
- [x] ⚠️ **管道终点 = 体征变化可测(2026-10-08 补 · ✅ 2026-10-10 批次 F-2/F-6 转闭)** —— 至少 1 条事件写入病史流后,
      **`IVitalsQuery` 可观测到体征变化**。依据:Goal 管道终点是「体征变化」(本文件 §三 M2 Goal),
      原判据 8 条**无一条**覆盖该末端 —— 事件流写进去了、体征变没变**无人验**
      **✅ 2026-10-10 批次 F-2 兑现**:`vertical_slice_test` 禁 Fake 换真 —— 真 `CompositionRoot` 装配 + 真写者 +
      生产 `IVitalsQuery`,**7/7 绿**,含 `test_vitalsQuery_returnsAfterTreatment`;
      **变异实测**(`sum += Fix.Zero`)恰红 2 = 判据有牙。原文不删,留闭环记录。
- [x] ⚠️ **施治腿纳入 M2 链(2026-10-08 补 · ✅ 2026-10-11 T3.3 转闭)** —— 核心假设 `design/gdd/game-concept.md:687` 为「**判断 → 施治**」,
      现 7 系统只有 **11 处方**半边,10 急救(`EmergencyTreatmentApplied`)**不在链内** ⇒ 实际只证
      「诊断 → 处方」半环。**7 系统集须补 10**(急救已 Complete 2026-10-08,系登记遗漏非未实现)
      **✅ 2026-10-11 T3.3 转闭**:急救链运行期已接 —— `EmergencyAttemptDriver` 对在场且未触发过的病人
      **每 400 tick 自动触发一次急救** ⇒ 病史流含 `EmergencyAttempt` + `EmergencyTreatmentApplied`
      (`emergency_attempt_wiring_test` 断言 + 第三轮 playtest 世界侧确认)。**11 处方腿的机制半
      由 #2 复评承接**(无玩家交互驱动,非接线缺陷)。原文不删,留闭环记录。
      **✅ 2026-10-11 G5 复评转闭(手动化)**:G4.1 移除自动触发(`EmergencyAttemptDriver` 退役壳,
      零输入零事件 = 红线守死 —— wiring 测**反向改造**,重新引入自动触发即红);
      G4.2 `EmergencyManualDriver` 手动入口 ⇒ `EmergencyAttempt` + `EmergencyTreatmentApplied`
      双事件入病史流(病人归因 + Skip 载荷 method=1 可判别 R-6)。判据 5 测全绿
      (`emergency_manual_driver_test`)。复评原件 `production/qa/evidence/m2-exit-recheck-sprint06-g5-2026-10-11.md`。
- [x] PlayMode 集成测试 `unity/Assets/Tests/PlayMode/vertical_slice_test.cs` **零桩方法**
      (**已闭 2026-10-08** —— 7 测重写为真验证(驱动 `PrescribeFlow` / `CaseOpenDecider` 真生产路径 + `IPayloadEncoder`),
      结果 **6 passed + 1 skipped(NOT-RUN: 病人腿 25/9 写者未实现)**,`Assert.Pass` 桩 **0 个**;
      证据 `production/qa/evidence/review-phase3-vertical-slice-2026-10-08.md`。
      原记「现 8 个 `Assert.Pass` 桩 / `grep -c TODO` = 14」为**陈旧读数**,已于同日消除)
- [x] ≥1 次**文档化** playtest,报告落 `production/playtests/`
      (✅ 2026-10-10 批次 F-5/F-6 转闭 —— 目录两份报告在档:
      `playtest-2026-10-08-vertical-slice.md`(自动化文档化)+
      **`playtest-2026-10-10-m2-manual.md`(人工 playtest · 用户 dr_guyang 执行)**
      ~~但该轮是 **自动化测试的文档化**,是否满足本条「文档化 playtest」按口径待用户裁定;
      若要求**人工**执行则仍未闭~~
      **✅ 口径已于 2026-10-10 裁明:至少一次人工执行** —— 依据:该条意图是让真人给出
      可感知面反馈,自动化文档化不能证明「可玩」。人工轮已于 2026-10-10 满足。原文不删,留闭环记录)
- [x] **4 项形态件交付**(① 脉案线格/空行/明度轴 · ② 墨乾湿两态 · ③ 急救零数字+可跳过 ·
      ④ 一条真实状态反馈通道)—— 承 `art-bible §8.11.1`「手感/可读性不得用灰盒验证」
      ⇒ **✅ 2026-10-14 sprint-10 复评全条闭合(翻勾)**:③ 的功能半(端到端直读通道 +
      跳过入口接线 + Idle 门)经 **story-009 落地兑现**(Complete ✅ · 评审原件
      `production/qa/evidence/review-story-009-2026-10-14.md`:1 BLOCKING F-1 当场修复 +
      回归测试;PlayMode 186/186 · EditMode 3279/0 零回归)—— sprint-09 S3 复评原件
      (`m2-exit-recheck-sprint09-2026-10-14.md` 判决 B)明写的翻勾判据「story-009 落地」
      就此兑现;逐判据实测(focusable/RegisterCallback 命中 · SetVisible 生产链 ·
      通道生产实例化 · 端到端链 · 双套件)见复评原件
      **`production/qa/evidence/m2-exit-recheck-sprint10-169-2026-10-14.md`**。
      ⚠️ AC-9-10/11(ADVISORY 人工走查)NOT-RUN 欠账在案,归人工走查批,不构成本条判据 —— ✅ **2026-10-14 已执行全绿结清**(`production/playtests/playtest-2026-10-14-story009-walkthrough.md`;AC-9-10/11 翻勾,story-009 三处登记)。
      **勾选滞后面(sprint-09 S3 三条)全部结清**;M2 §三 内的 `:252`(五族切图冻结,
      story-020/019 链)为独立推进项(◐ 半闭),不随本翻勾联动。
      ⇒ ◐ **2026-10-08 ① 交付 ✅**(story-021:线格 `.ruled` 挂脉案行 + 空行等重声明层 +
      贴图级明度轴 `test_ac021_3` minΔ=0.266 ≥ 0.12 提案值;B2 转正)·
      **② 交付 ✅ 同日**(story-022:两态主题色明度轴湿 `#26241f` 权威/干 `#191714` 提案 +
      `.ink-wet`/`.ink-dry` 接冻结图 + 洇开覆盖方向 `test_ac022_3` wet ≥ dry;art-bible §4.5/G4 转可验)——
      · **③ 交付 ✅ 2026-10-09**(story-023:`.skip-entry` 跳过入口元件形态 + G2 零数字角标门
      (剥注释全库 `content:`/`X/N` 双模式)+ 焦点落点 44px/单槽铁律/属性交集判据;
      art-bible **G2** 转可验;零新变量零新图)·
      **④ 交付 ✅ 2026-10-09**(story-024:通道分发纯函数 `SignChannelBinder`(体征词条→五通道,
      F-8.2 向尾回退/并列保序/非法枚举 fail-loud)+ `.channel-reading`/`-negative` 两态类
      (阳性满墨/阴性降档,:1185 零标注)+ 34 行真表端到端判据;零新图零新变量)——
      ⇒ **4 项形态件 4/4 齐**;**但整条仍保持 `[ ]`** —— 形态件是**形态半**,其**施加点**
      (story-024 的运行时挂行 = 9 查体链 + 数据绑定轮;③ 的候选列表载体 = 10 轮)未接,
      **交付 ≠ 可 playtest 的真通道**(同五族条先例,禁借绿;M2 Exit 条判据待接线后复评)
      ⇒ ✅ **2026-10-10 批次 F-6/F-7 回填(施加点状态已实测锁定)**:逐条查运行期挂点 ——
      **零个形态件在运行期被施加**:`SkeuoRuntimeDriver` 两 TODO 仍是形态件施加轮未开的空壳
      (playtest 人工面 0% 覆盖独立互证:编辑器里无任何拟物 UI 呈现)。
      **回填后的判据口径(留待接线后逐条勾)**:本条 `[ ]` 的**唯一剩余内容 = 施加点接线**,
      形态半(4/4)**已闭不再重开**;施加点归属 = story-024 → 9 查体链 + 数据绑定轮 ·
      story-023 → 10 急救轮(sprint-04 行 6「阶段 3 形态件施加轮」·~2 天)。
      原文不删,留闭环记录。复评原件 `production/qa/evidence/m2-exit-recheck-2026-10-10.md`。
      ⇒ **✅ 2026-10-11 T3.3 复评(施加点接一半,维持 ◐ 不勾)**:运行时已接 ——
      `SkeuoRuntimeDriver` 两 TODO 清零(Boot.unity 预摆 + 七具名 Screen 工厂 + UIDocument 运行期
      自建);`CasebookScreen.ApplyData` 每 tick 生产调用 `SignChannelBinder` ⇒ **形态件① 线格
      (`.ruled` 挂脉案行)、② 墨龄两态(`InkAgeTicks` 挂 `.ink-wet`/`.ink-dry`)、④ 通道分发
      (脉案五通道+问诊栏挂真源读取面)** —— 第三轮 playtest(5A)人工确认纸页/线格/空行呈现。
      **仍未接(诚实登记,不禁借绿)**:③ 急救候选列表 `EmergencyCandidateList` **零生产实例化**
      (仅测试;载体形态就绪,施加点归 10 交互轮);② 的墨龄上游落笔 tick 归 37 未实现;
      ④ 的**词条真值源**(9 查体 revealed 集)归 9 未接线 —— `CasebookVitalsSource.RevealedRows`
      恒空集(F2 tripwire)⇒ 脉案值区运行期**空行**(禁假数据,恰是判据)。复评原件
      `production/qa/evidence/m2-exit-recheck-2026-10-11.md`。
      ⇒ **✅ 2026-10-11 G5 复评(施加点接大半,维持 ◐ 不勾)**:G1–G4 后 ——
      ② 墨龄上游已接:`JudgmentWriter`(G2 落笔写者)生产装配 + `InkAgeTicks` >0(落笔输入入口未接);
      ④ 真值源已接半:查体分册快照(G1.4)接 `RevealedRows`(查体输入入口未接);
      **③ 候选列表仍零生产实例化**(仅测试);玩家输入入口(Interact/Emergency 键位已有零消费)。
      复评原件 `production/qa/evidence/m2-exit-recheck-sprint06-g5-2026-10-11.md`。
      ⇒ **✅ 2026-10-14 sprint-09 S3 复评(逐条重核,维持 ◐ 不勾 · 但订正两处陈旧读数)**:
      ①②④ 施加点**确已接**(独立实测):① `.ruled` 经 `CasebookScreen.ApplyData` 每 tick 挂行 ·
      ② `JudgmentWriter`(`JudgmentWriter.cs:48`)经 `PrescriptionPageHost.cs:68/100` 生产装配,
      `InkAgeTicks` 挂 `.ink-wet`/`.ink-dry` · ④ `RevealedRows`(`CasebookVitalsSource.cs:187`)
      + E 键消费链(`PlayerInputReader.cs:35` → `InteractionInputDriver.cs:82-88` →
      `ExamineDriver.cs:93` → `BootRoot.cs:224-225` → `CasebookScreen.cs:655-660`)完整闭合。
      **③ 维持不勾 —— 但残余已收窄**:本轮新发现 ③ 的**载体确已生产挂载**
      (`CompositionRoot.cs:246` + `BootRoot.cs:237`),**真缺口 = 跳过入口零交互绑定**
      (`EmergencyCandidateList.cs` 无 `focusable`/`RegisterCallback`/`clicked`;`SetVisible` 全仓
      **零生产调用**)⇒ 即「实例化了但不可交互」,**非**上文 `:196-200` 记的「零生产实例化」。
      ⚠️ **订正 `:196-200`(已过时)**:「③ 急救候选列表零生产实例化(仅测试)」**已被推翻** ——
      载体自 2026-10-11 S3.1 起生产挂载于 `BootRoot.cs:237`。
      ⚠️ **订正 `:202`(已过时)**:「玩家落笔输入入口未接(TryRecord 无生产调用点)」**已被推翻** ——
      sprint-08 S1.1(2026-10-12)已接:`CasebookScreen.PointLexicon:383` →
      `CasebookCommandRouter.SubmitDiseaseName:102` → `JudgmentWriter.TryRecord/TryRevise:117-121`。
      ⇒ **③ 的归属不变且有主**:story-023 边界裁定①(`story-023-...md:29/34`)明写
      「施加点 / 候选列表载体 / `Idle` 时序归 **10 实现轮 + 数据绑定轮**」。
      复评原件 `production/qa/evidence/m2-exit-recheck-sprint09-2026-10-14.md`(判决 B)。
      ⇒ ✅ **2026-10-14 同日:该「10 实现轮」已具名化为 `emergency-procedures/story-009`**
      (Ready ⬜ · 归 sprint-10 · 用户批准)。**本条的③项翻勾判据 = story-009 落地**
      (端到端直读通道 + 跳过入口接线 + Idle 门);**在此之前维持 `[ ]`(禁借绿)**。
      ⇒ ✅ **2026-10-14 sprint-10 复评:判据兑现,全条翻 `[x]`**(见本条目首行结案注;
      复评原件 `production/qa/evidence/m2-exit-recheck-sprint10-169-2026-10-14.md`)。
      ⚠️ **附查(同批复评)**:「10 实现轮」此前**全库无 epic/story/工量登记** ——
      与 ADR-022 关卡工具同型的「引用却无登记」失效模式;实测急救链**运行期可达性 0%**
      (`EmergencyDirectReadChannel` 零生产实例化 · `InputActionAsset` 零装载点)。
      story-009 即其承接件(详见 `EPIC.md` §2026-10-14 补注)。
- [x] **五族切图与 atlas 布局冻结**(①② 的前置;切图早错 = 全局返工)
      ⇒ ✅ **2026-10-14 复评翻 `[x]` —— M2 Exit Criteria 至此全勾(§三 10/10)**:原登记理由
      「atlas 半 = 019-b Blocked(PAGES_MAX spike 未冻结)」已陈旧 —— spike 同日落定且冻结
      (`design/assets/specs/atlas-budget-freeze-2026-10-14.md` round-2:固定 2 页/16 瓦片页/
      单帧同屏瓦片 ≤ 32,实测倒推非占位值)+ 两张超规图重生 1024² 结清(`e891ecd`,重冻铁律
      兑现)+ 实测门 batch 三跑 **2/2 Passed**(`Logs/s09_atlas_reimport2.xml`,断言 18/0,
      `1c03f2f`)。复评原件
      **`production/qa/evidence/m2-exit-recheck-sprint10-253-2026-10-14.md`**(三残留 R1 门接线 /
      R2 真机瓦片实测 / R3 F5 量纲修订 逐件裁「不卡 M2 Exit」,转独立待办;禁借绿声明在档)。
      ⇒ ⚠️ **口径订正(2026-10-04 D)**:原文写「纸/墨/铜」三族 —— 实测为**五族**(纸/墨/卷轴/印章/图标),
      「铜」族零实物。**冻结清单须按五族枚举**,否则三族已入库资产漏冻
      ⇒ 切图冻结**不等于**已接入:冻结件须由 **`skeuomorphic-ui/story-019 贴图接入`** 真正绑到 USS
      (该 story = 本条硬前置;2026-10-04 J 拆分,见 §三之附;**◐ 已推进至 c/d/e/f ✅,余 b Blocked**)。
      **冻结 + 019 二者齐**方算本条成立
      ⇒ ◐ **2026-10-08 切图冻结半 ✅**(story-020 步①③ 收口):五族 16 张 + 铜规格值冻结件落盘
      (`design/assets/specs/nine-slice-freeze-2026-10-08.md`,`freeze-v1` 机器块)+ 019 接线 c/d/e/f 齐 +
      C8 冻结件一致性门接棒(scroll 并修正 68→72)⇒ 余 **atlas 布局/页数半 = 019-b Blocked**
      (`PAGES_MAX` spike 未冻结)⇒ **本条保持 `[ ]`,禁借绿**
- [x] ✅ **焦点黄铜 2px 最小切片交付**(2026-10-04 E 裁:**铜族最小切片升为 M2 硬前置** · **2026-10-08 交付**)
      ⇒ 依据:`milestones:132` 已裁「焦点高亮的**黄铜 2px** 须真做」(可读性);而形态件 ① 的**明度轴判据**
      要跑在「**贴图 × 主题色的最亮像素**」上,`art-bible §4.5` 已警告纸纹是纹理非纯色底
      ⇒ **无铜族贴图 ⇒ ① 的验收无载体**。本项**只升「焦点 2px」这一件的最小切片**,
      **不拉整个铜族进来**(避免「通道 > 4」的蔓延,承 §三边界原则)
      ⇒ ✅ **证据(2026-10-08 · story-020 步②)**:`unity/Assets/Gameplay.UI/Skeuomorphic/Textures/Brass/
      focus_brass_2px-final.png`(64×64 RGBA,2px 环 `#B8863B` = art-bible §4.1 权威值,中心透明)
      + `.meta`(`spriteBorder: {2,2,2,2}`)入库;放 `Brass/` 子目录(顶层 16 张计数门不受影响)
- [x] 其余资产**显式登记为灰盒**(见下 §三之附「M2 灰盒登记表」),不留「看起来像忘了做」的空白
      **✅ 2026-10-14 sprint-09 S3 复评 —— 翻 `[x]`(已做未勾)**:登记表 `:236-249` 逐类齐,
      与 `design/assets/entity-inventory.md:249-262` 汇总表的 **42 项 VS Critical 逐类对齐**
      (Characters 2 / Props 6 / Buildings 1 / Items 6 / UI 1 / HUD 8 / SFX 10 / Ambient 1 / VFX 7;
      Enemies 与 Music Cues 的 VS 列本为 0 ⇒ 正确无行),**无「看起来像忘了做」的空白**。
      复评原件 `production/qa/evidence/m2-exit-recheck-sprint09-2026-10-14.md`(判决 C)。

**ETA**: TBD

### 附:M2 灰盒登记表(承 `art-bible §8.11.2` **D1 逐项登记**)

> **2026-10-03 落**:原判据此处引「见下 §五」,而 §五 实为「M3 / M4 / M5」—— **悬空引用**,
> 且所承诺的清单从未写出。现补为**本节附表**(挂在 §三 名下,不新起小节)。
> 登记口径 = `design/assets/entity-inventory.md` 的 **42 项 VS Critical**;
> 「真做 / 灰盒」两态,**无第三态**(§8.11.1)。
> ⚠️ **灰盒 ≠ 免验**:灰盒项**不得**承担 §8.11.1 的手感/可读性验证 —— 那 4 项已单列在 Exit Criteria。

| 类 | VS Critical 项 | M2 状态 | 依据 |
|---|---|---|---|
| **真做** | ① 脉案线格/空行/焦点明度轴 · ② 墨乾湿两态 · ③ 急救零数字+可跳过 · ④ 一条真实状态反馈通道 | **真做** | `§8.11.1`:属手感/可读性,**灰盒会掩盖要验的东西** |
| **真做** | **五族**九宫格切图 + atlas 布局(纸/墨/卷轴/印章/图标) | **冻结**(非新出图) | 承上 ①②:切图早错 = 全局返工(ADR-013 §三)。**2026-10-04 D 订正**:原写「纸/墨/铜」三族 = 措辞误 |
| **真做** | **焦点黄铜 2px 最小切片**(铜族**零实物** ⇒ 须新出图) | **新出图** | 2026-10-04 E 裁:① 明度轴判据的载体;**只此一件**,不拉整个铜族 |
| 角色 | 主角(医者) · 普通病人 T0–T3 | **灰盒** | 本里程碑验的是机制闭环,非角色辨识 |
| 环境 | 医馆(单房间 P0) | **灰盒** | 布局占位(§8.11.1 合法用途之二) |
| 道具 | 脉案纸页 · 出诊箱 · 诊脉台刻度盘 · 听诊器 · 戥子 · 手术灯 | **灰盒** | 几何/交互占位;**其纸面形态已由「真做」四项覆盖** |
| 物品 | 柳树皮 · 毛地黄 · 金鸡纳树皮 · 止血草 · 针具 · 绷带 | **灰盒** | 图标可由 `ui_icons_sprite` 族临时顶替 |
| 界面 | 脉案页(诊断态纸面) | **灰盒** | 走查界面,非出图目标 |
| HUD | 8 项(含焦点高亮) | **灰盒** | 承 ADR-013;焦点高亮的**黄铜 2px**须真做(可读性) |
| SFX | 10 项 | **不在 M2 面内** | 归 44;`assets/audio/` 现为空,零音频资产 |
| 环境音 | 医馆室内环境音 | **不在 M2 面内** | 同上 |
| VFX | 7 项(水墨晕染除外) | **灰盒** | ② 已覆盖墨迹;余项占位 |

**⚠️ 已知缺口(实测 2026-10-03,登记不隐藏)**:`skeuomorphic-ui` epic 标 `Complete ✅`(18/18),
其 `story-001` 的 6 条 AC(九宫格区间 / 变量完整 / 图集配额 / 两条 lint / fallback 字体)**均为 USS 结构断言**,
**无一条要求贴图绑定** ⇒ 该 epic 通过**不蕴含**「贴图已接入」。实测:16 张 `*-final.png` 的 GUID
在全库引用数为 **0**,USS/UXML 内 `url()` / `background-image` 零命中。
⇒ **M2 的「真做」四项须真正接图**,不能因 epic 已 Complete 而推定已接。

> **✅ 2026-10-03 已立补件**:该缺口现由 **`skeuomorphic-ui/story-019 贴图接入`(Ready ⬜)** 承接 ——
> AC = 每个已注册元件类的 USS 绑到真贴图 / 九宫格 slice 对齐真实切图 / 图集页数 ≤ `PAGES_MAX` /
> 屏幕级 UXML 禁直引贴图 / 贴图缺失构建期硬失败。**它已登记为 M2 Exit Criteria 第 5 条的硬前置**
> (形态件 ①② 压在切图上,不接图则无法交付),见本文件 §三 Exit Criteria 与
> `production/epics/skeuomorphic-ui/EPIC.md` §里程碑归属。
> ⚠️ **019 Ready ≠ 缺口已闭** —— 本行登记的是「已有承接件」,非「已完成」。

---

## 四、M1 Pre-Production Complete(保持原档,仅订正计数)

**Goal**: All architecture decisions finalized, production management artifacts in place, first sprint ready to start.

**Exit Criteria**:
- [x] `production/epics/index.md` accurate (all paths, all statuses) — committed 0e553e1
- [x] All P0 systems have epic directories
- [x] Sprint 1 plan defined in `production/sprints/sprint-01.md` — committed 1d14107
- [x] CI EditMode baseline green (921 passed, 0 failed) — 2026-09-29 desktop verified
      (⚠️ 2026-10-03 实测现行基线 = **2204 total / 2163 passed / 0 failed / 40 skipped / 1 inconclusive**)
      —— ⚠️ **该值是 2026-10-03 快照,已过时**;现基线见文末「附:计数与基线(2026-10-15 实测)」。
      本行**原文不删**(它是 M1 当年勾选时的真跑,改数 = 伪造当时记录)
- [x] `UNITY_LICENSE` secret configured — 改用服务账号授权（`UNITY_CLIENT_ID` / `UNITY_CLIENT_SECRET`），CI workflow 已生成（`.github/workflows/unity-tests.yml`）
- [x] OQ-1-12 (接地模型) decision recorded — `player-controller-and-movement.md`
- [x] OQ-10-12 (两动作原型) decision recorded — `emergency-procedures.md` · ✅ **OQ-10-6(枚举归属归系统 10)2026-10-02 同批裁,落 `production/decisions/oq-10-6-oq-10-12-adjudication.md`**
- [ ] Performance budgets finalized (Draw Calls, Memory Ceiling) — pending target hardware selection (BLOCKED-BY: hardware decision, not an ADR issue)
- [ ] Spike results recorded: ADR-023 S2/S5/S6/S7, ADR-013 assumption 6 — P1 deferred per user

**ETA**: TBD

> ⚠️ **M1 未闭的 2 项与 M2 的关系**(待技术侧裁定,见 §六):性能预算未冻结 ⇒ 纹理档位无硬约束反推;
> ADR-023 spike 未跑 ⇒ 三场景拓扑未实测。二者**是否**为 M2 的硬前置,**尚未裁定**。

**M2 已解锁**(2026-10-03):`sprint-04.md` 原门禁「Phase 1 未收口前不启动 Phase 2」已满足,
Phase 2 实际进度 = **3/7 系统**(player-controller ✅ · camera-viewpoint ✅ · **interaction-system ✅ 7/7 收口 2026-10-04** ·
patient-ai ⬜ · diagnosis-system ⬜ · case-system ⬜ · prescription-medication ⬜)。
> ⚠️ **2026-10-04 口径订正**:原记「interaction-system ⬜ 断点」**已作废** —— 实测 7 份 story 全 Complete
> (001…006 + 007 装载接线,NR-1 已闭)。剩余关键路径 = `patient-ai → diagnosis-system → case-system →
> prescription-medication` 四系统端到端链(见上一条 Exit Criteria)。

---

## 五、M3 / M4 / M5(新增档)

### Milestone 3: Systems Complete

**Goal**: P0 的 **31 个系统机制全部落地** —— 每一件都在跑,能用灰盒资产走完全部路径。

**Exit Criteria**:
- [ ] 31 个 P0 epic 全部 `Complete`(⚠️ 现 **22/31**)
- [ ] 全部 P0 story `Complete`(⚠️ 现 **220 story / 172 Complete / 46 Ready / 1 In Review / 1 Pending**)
- [ ] `Sim` 引用集门 / b2 / b6 全绿(承 ADR-025 §① / ADR-029 §③)
- [ ] 零 S1/S2 未关闭 bug(⚠️ 实测 `production/qa/bugs/` 仅有 `README.md`,**零 bug 文件** ⇒ 本项**形式上已满足**,
      但须注意其含义是「无登记」而非「已修复」—— bug 追踪机制 2026-10-02 建立后尚无实际录入)

> ### 📊 **2026-10-15 实测复核(三元组:截至 2026-10-15 · 批次 sprint-12 收口 · commit `a5ef7c5`)**
>
> 本节原记 `16/31` 与 `207 story / 136 Complete / 71 Ready` 均为 **2026-10-03 快照**,
> 滞后 12 天。本轮以脚本逐目录实测(非重读旧件),订正如下:
>
> | 量 | 旧记(2026-10-03) | **实测(2026-10-15)** | 差 |
> |---|---|---|---|
> | epic 全 Complete | 16 / 31 | **22 / 31** | +6 |
> | story 总数 | 207 | **220** | +13 |
> | story Complete | 136 | **172** | +36 |
> | story Ready | 71 | **46** | −25 |
> | story In Review | 0 | **1** | +1 |
> | story Pending | 0 | **1** | +1 |
>
> **口径说明(防误读)**:
> - **分母 31** = `production/epics/` 下的 **31 个目录**(含 `save-slot-ui`,其**无 `EPIC.md`**,
>   故按「非 Complete」计 —— 这是**如实计数**,不是遗漏)。
> - **In Review 那 1 条** = `medical-consequences/story-001`(sprint-12 的 S12-9 待办对象)。
> - **Pending 那 1 条** = `world-ecozones/story-005`(桌面走查 + 工具 CI,非代码面)。
> - **+6 的构成**(逐户实测,非推断):2026-10-03 之后翻绿 **6 户** ——
>   `patient-ai`(10-05)· `prescription-medication`(10-06)· `disease-simulation`(10-07)·
>   `diagnosis-system`(10-08)· `emergency-procedures`(10-08)· `case-system`(10-15)。
>   ⚠️ **`case-system` 是本轮(10-15)唯一翻绿户**;其余 5 户在 10-05…10-08 已翻,
>   是**旧快照滞后**所致,非本轮成果 —— **不得记到 sprint-12 账上**。
>
> ⚠️ **本节其余两条 Exit Criteria 的「未勾」是真实状态,非陈旧标记** ——
> `Sim` 引用集门 / b2 / b6 的门定义散在 ADR-025/029 与 `Editor.Tools.Gates`,**尚无单一「全绿」实测记录**;
> 零 S1/S2 一条见上方括注。**本轮只订正计数,不改判定。**

**ETA**: TBD

### Milestone 4: Content Complete

**Goal**: **42 项 VS Critical 资产做完 + 数值轮冻结** —— 游戏能**看**了。

**Exit Criteria**:
- [ ] `design/assets/entity-inventory.md` 的 **42 项 VS Critical 全交付**(⚠️ 现 **0/42**,全部灰盒豁免中)
      ⇒ **归属已裁(2026-10-03,R-2「按类型分别归属」)** —— 42 项分到 **11 个 epic**,
      逐项归属见 `production/qa/evidence/r2-asset-ownership-ruling-2026-10-03.md`:
      消费系统类(角色/环境/道具/物品/界面/HUD)= 挂消费它的系统 epic;
      **SFX/Ambient** = 集中 `audio-system`(**急救专用音例外** → `emergency-procedures`);
      **VFX** = 挂触发系统。承接 story 的**编号与排期归 producer**(`art-bible §8.11.3`)
- [ ] 全部资产通过 art-bible §8 的格式 / LOD / 材质槽 / 线性工作流约束
- [ ] 数值轮冻结(阈值 / 系数 / `MAG_MAX` 等 —— 承「数值用户自己调」纪律)
- [ ] 资产验收证据落 `production/qa/evidence/`(Visual/Feel = Screenshot + lead sign-off)

**ETA**: TBD

### Milestone 5: MVP / Release Candidate

**Goal**: M3 + M4 合流,`game-concept.md:686` 的 8 项内容面全齐,可交**外部**试玩。

**Exit Criteria**:
- [ ] `game-concept.md:686` 的 8 项 MVP 内容面逐项可验
- [ ] ≥1 次**外部**(非团队成员)文档化 playtest
- [ ] 核心假设**获证或证伪** —— 「判断 → 施治」本身好玩(这是全案的立项问题)
- [ ] AC 走查全闭;[L] ADVISORY 项经主创签核

**ETA**: TBD

---

## 六、开放问题(待用户裁定)

> 本节由 2026-10-03 的 7 位专家调研提出,**尚未裁决**。裁定前 M2 **只锁机制面**(§三 退出条件),
> 美术面**不做任何预设** —— 不预设灰盒,也不预设真资产。

1. **M2 是否允许「最小真实体验资产」?**
   全灰盒能验证「管道通」,但**拟物 UI 是 pillar**(无血条 / 无小地图 / 纸质感是视觉识别的一半)——
   灰盒 UI(**纯色方块**)可能让 M2 的评审结论**失真**(评审说「不好玩」,而实际是灰盒掩盖了纸感的乐趣)。
   待裁:是否至少把**纸面 UI 元件**做成真资产(承 art-bible §8.2「UI-纸 1K」档,单张 ≤1K + atlas ≤2K)。
2. **M1 未闭的 2 项是否为 M2 硬前置?**
   性能预算(待硬件)与 ADR-023 spike(S2/S5/S6/S7)未闭,是否阻塞 M2 开工?
3. **`art-bible.md` ↔ `entity-inventory.md` ↔ `sprint-04.md` 三处资产口径的合流**
   —— 勘误轮上与本文对齐(勘误清单另行落盘)。

4. **⚠️ M4 的工量从未进入 sprint 序列(2026-10-04 K 登记 · 结构性风险,非排期问题)**
   M4 的退出条件要求 **42 项 VS Critical 全交付**(现 **0/42**),但**现有 sprint 序列
   (sprint-01…04)没有任何一段承载它** —— sprint 排的是**机制/story**,M4 排的是**资产工量**,
   两者**量纲不同,从未对接**。⇒ 现状 = **42 项资产没有时间归属**。
   **具体三问(须 producer 在 M4 启动前裁)**:
   - ① **42 项外部产 vs 内部产?** 若外部(外包/美术),需**前置交付批次 + 验收循环**,
     其**工期不随 sprint 伸缩**;若内部,**占谁的工时**?
     ⇒ **✅ 已裁(2026-10-04)= 乙 · 混合(外部主体 + 内部收口)**。
   - ② **资产批次归口谁排?** `art-bible §8.11.3` 明写「排期归 producer」——
     但 producer 现有载体(sprint 序列)**装不下它** ⇒ 须**另立批次载体**,或在 sprint 内**切出资产轨**。
     ⇒ **✅ 已裁(2026-10-04)= 甲′**(甲载体;**内部收口**入 `sprint-05` 轨 B,外部走独立交付循环)。
   - ③ **42 项 × 单件工时 = ?** —— 本量**从未估过**(`entity-inventory.md` 只有项数,无工时)。
     **无此估算 ⇒ M4 的 ETA 写不出来**(本文件 §五 M4 的 `ETA: TBD` 即由此而来,
     **不是「待定」,是「无法定」**)。
     ⇒ ⬜ **仍待办** —— **待估清单已备**(`sprints/sprint-04.md` §「③ 的前置件:待估清单」,
     **外部侧 / 内部侧两表,按 11 epic 分组**),但**每一格的数仍为空**;**填数归 producer**。
     ⚠️ **填数后的强制复核**:表一(外部)合计 **vs** 表二(内部)合计 ——
     **表一 > 表二 ⇒ 甲′ 不成立,须重开乙′**(这是甲′ 的可证伪条件,非备注)。
   > ⚠️ **本项与 1–3 不同类**:1–3 是**待裁的选项**;本项是**已实测的缺口** ——
   > 即使三项全裁完,M4 的工量归属**仍为空**,须单独处理。
   > **只见于本文件登记;四份调研原件**（`asset-embedding-plan-2026-10-04.md` §六）**亦记其为未核实项**。
   >
   > **✅ 2026-10-04 已立三步登记** —— 排期的**做法**已落 `sprints/sprint-04.md`
   > §「资产排期:三步」(① 定载体 → ② 定外部产/内部产 → ③ 定工时与编号)。
   > **✅ ① 已定(2026-10-04 用户裁定)= 甲 · sprint 内切资产轨** —— 自 **sprint-05** 起
   > 每条 sprint 切 **轨 A(机制 story)/ 轨 B(资产工量)**;理由:内部产为主 + 产能可切分 + 零新登记面。
   > **✅ ② 已定(2026-10-04 用户裁定)= 乙 · 混合(外部主体 + 内部收口)** ——
   > 外部做 42 项真资产图 + 五族切图 + 铜族 2px;内部做导入格式订正 + 九宫格元数据读出/USS 绑定 + atlas 打包 + 验收
   > ⇒ **须立前置交付批次**(交付 → 验收 → 退回修正)。
   > ⚠️ **① 与 ② 不同调(登记不隐藏)**:**① 甲的理由是「内部产为主」,② 实测为「外部主体」**。
   > **✅ 处置已裁(2026-10-04 用户裁定)= 甲′ · 保留甲载体** —— **内部收口工量入 `sprint-05` 轨 B**;
   > **外部 42 项走独立交付循环,不占 sprint 轨**。
   >
   > ### ⛔ 甲′ **已被证伪并翻转** → **① 终裁 = 乙′ · 重开乙载体**(2026-10-04)
   >
   > **工时已估**(producer 高层代理 · 锚 `art-bible §8` 规格档 + 行业类比):
   > **表一 外部侧 ≈58.5 人日 : 表二 内部侧 ≈10.5 人日 ≈ 5.6 : 1**。
   > ⇒ 触发甲′ 的可证伪条件(**表一 > 表二**)⇒ **甲′ 的条件「外部体量小」被证伪**。
   >
   > **终裁形态**:**`production/asset-batches/`(独立载体 · 须新建)装外部 58.5 + 内部 10.5 全部**;
   > **`sprint-05` 只装轨 A(机制 story),轨 B 取消** ⇒ **资产工量整体移出 sprint 序列**。
   > ⚠️ **乙′ 同时解决 ② 的 splice 风险** —— ① 与 ② 自此同调。
   > **裁定沿革不删**:甲 → 甲′(可证伪,判据明写在 ③)→ 工时证伪 → 乙′。
   > **这不是反复,是可证伪的设计按设计工作。**
   >
   > ⚠️ **仍有两项未闭**:① **乙′ 的载体目录不存在**,须新建;
   > ② **U-8 产能分母(团队内部可用人日)仍从未登记** ⇒ **即使载体已定,M4 的 ETA 仍写不出来**。
   > ⚠️ **定 ①②+乙′ ≠ 已排期** —— 编号/时点仍归排期轮,且 `sprint-05.md` **尚未产出**(待 sprint-04 收尾)。
   > ⚠️ **甲的代价(不隐藏)**:轨 A/B **争同一产能池**,且 M2 咽喉 `interaction` 同在抢 ⇒ 资产轨**不得挤占** 13→8→37→11 链。
   > 反向约束:R-2「须在 **M4 启动前**」= **截止**,不是开始。

---

## 附:计数与基线(**2026-10-15 实测** · 三元组:截至 2026-10-15 · 批次 sprint-12 收口 · commit `a5ef7c5`)

> ⚠️ **本表 2026-10-15 全表重测**(原为 2026-10-03 快照,滞后 12 天)。
> 行内**逐量标注来源**;凡跨文件引用的量**贴实测命令或文件路径**,使本表自洽可复核(承 S12-0 伪基线教训)。

| 量 | 值 | 来源(可复核) |
|---|---|---|
| P0 系统 | 31 | `design/gdd/systems-index.md:180`;`ls -d production/epics/*/ \| wc -l` = **31**(目录数与 GDD 数**一致**) |
| epic 全 Complete | **22 / 31** | `production/epics/*/EPIC.md` 的 `> **Status**:` 行逐件实测(30 个有 `EPIC.md`;`save-slot-ui` **无** ⇒ 计非 Complete) |
| story | **220 total · 172 Complete · 46 Ready · 1 In Review · 1 Pending** | `production/epics/*/story-*.md` 的 `> **Status**:` 行逐件实测 |
| EditMode 基线 | **3401 total · 3355 passed · 0 failed · 0 inconclusive · 46 skipped** | `unity/Logs/s12-6-final.xml`(2026-10-15 S12-6 终轮;`s12-6-editmode.xml` 首轮**逐位一致**) |
| PlayMode 基线 | **186 total · 186 passed · 0 failed** | `unity/Logs/s12-2-full-pm.xml`(2026-10-15 S12-2) |
| VS Critical 资产 | 42(全 spec 化) | `design/assets/entity-inventory.md`(**未重测** —— M4 面,本轮不触) |
| P0 工期基线 | 6-9 个月(**不得重算**) | `systems-index.md:190` 用户裁定 2026-09-14 |

> **⚠️ 两条基线的时点差(如实登记,防伪基线)**:EditMode `3401/3355` 取自 **S12-6 终轮**;
> 若其后有新增测试件落地,该值即**滞后** —— 引用时须连带三元组,不得裸引(承 S12-0 建闸)。
>
> **⚠️ 本表原记 EditMode `2204 / 2163 / 0 / 40 / 1`(2026-10-03)** —— 该值是**当时**的真跑,
> 原文不删留证;现值见上。两值差 **+1197 total / +1192 passed**,系 12 天内的正常增量,非订正。
