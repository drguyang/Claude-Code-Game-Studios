# 登记债总册(Registration Debt Register)

> **用途**:全案**未结登记债**的唯一真源。评审 / 复核 / 观察项产出的「登记不修」项一律入此册,
> 避免散落在 `active.md` / GDD / 评审原件里无人收口。
> **清理纪律**:每项须写「归属轮」(哪个批次 / 哪一轮处理);**归属轮到达时本册是收口清单**。
> 已结项标 ✅ 并留结案锚点(提交 / 原件),不删行(审计轨迹)。

---

## 一、未结登记债(Active)

### A. M2 接线轮 · 阶段 2(A/B/C/D/E 五批 · 2026-10-10 收口未提交)

| ID | 来源 | 内容 | 归属轮 | 备注 |
|---|---|---|---|---|
| AE-测-2 | A+E 评审 | 确定性复算测判别力≈0(纯函数连调两次恒等) | 不列为证据 | 已登;非阻塞 |
| AE-测-3 | A+E 评审 | SeriesTerms 10→9 截断 ≪1 LSB 不可观测 | 可接受 | ε 内盲区 |
| AE-码-3连带 | A+E 评审 | 「施救时病人离场 + 满 CAP」边界未穿真 EventStream | 批次 C 已确认 | 语义已由 C 确认 |
| AE-Process | A+E 评审 | Process 方法 60 行超 40 行 | 后续提私有方法 | 既有状态 |
| BCD-码-1 ✅ | BCD 评审 | GDD 校验 12 `R_rise>ε_MIN` 未进写入期 | ~~数值轮~~ | **2026-10-10 结案**(用户授权「值须你裁」):ε_MIN = **1/65536(Q16.16 raw=1)** 定值入 GDD Tuning §六 + **R1-30** 写入期校验(计算形与求值侧 ExpNeg 逐位同源 ⇒ fail-loud 变子集兜底)+ 双测(负:比值舍 0 ⇒ R_rise 精确 0 抛 RuleNumber 30;正:≈6.5 LSB 不误拒);MUT-R1-30 恰红 1(`test_registry_rRiseAtEpsilonMin_throwsRule30`),还原 SHA-256 逐位一致;锚点 = DiseaseSimulation 134/134(工作树,未提交);GDD 三件套(校验 12 / F3 注 / Tuning §六)同批同步 |
| BCD-码-2 ✅ | BCD 评审 | 玩家事件头 stamp 帧末 `CurrentTick` 与边沿 tick 不同源 | ~~接线轮~~ | **2026-10-10 结案**:`OnTickEdge(long tick)` 签名 + `MovementFeed` 逐边沿回推 `edgeTick`;锚点 = PlayerController 过滤跑 99/96/0 红(工作树,未提交) |
| BCD-码-3 ✅ | BCD 评审 | 流按 Tick 非降不变量无断言 | ~~建议 Append 加单调断言~~ | **2026-10-10 结案**:`EventStream.Append` 去重后/入列表前单调断言 + `Clear` 复位 + `event_stream_test` 新增 3 条;唯一违例 `case_replay_persistence_test` 改 tick 升序;变异恰红 1;锚点 = DiseaseSimulation 132/132(工作树,未提交) |
| BCD-码-4 ✅ | BCD 评审 | b4 asmDirs/白名单缺 `Gameplay.Boot` | ~~gate 轮~~ | **2026-10-10 结案**:`ToFloatScanDirs` 提为字段 + 补 Boot 面 + 白名单补 `Gameplay.Boot`;新增 `assembly_gate_b4_test` 4 条;MUT-b4 恰红 1;锚点 = Unit.Sim 159/159(工作树,未提交) |
| BCD-码-5 ✅ | BCD 评审 | WriterExistenceGate 扫描面缺 Sim.Contracts | ~~gate 轮~~ | **2026-10-10 结案**:`WriterScanDirs` 补 `Assets/Sim.Contracts`(实测该面零命中,不改绿态)+ 结构测试收紧六面全断言;MUT-b7 恰红 1;锚点 = Unit.Sim 159/159(工作树,未提交) |
| BCD-测-1 | BCD 评审 | B 时点 `m2b_disease.xml` 是过滤跑非全量 | 措辞登记 | 已被 C/D 全量重证 |
| BCD-测-2 | BCD 评审 | b7 对 24 豁免 Kind 零覆盖 | 设计可接受 | 三重防护已验 |
| BCD-C-GDD ✅ | C 批交付 | GDD 缺口 8 项(MAX_ACTIVE_DOSE/treatable_by 装载面/极性双源/复发计数无 Kind 等) | ~~9 的 GDD 轮~~ | **2026-10-10 结案**(权威清单 = 批次 C 报告 §四):8 项全数落 `disease-simulation.md` Debt Register **D-9-K…D-9-R** —— 3 项本批裁定(K 极性双源 = **轴为准** / L 极性序数 = **Symptomatic=0·Causal=1 锚 11** / M 复发次数 = **派生态闭式 clamp(⌊τ/relapse_interval⌋−1, ≥0)** 零新 Kind)+ 1 项办结(N = ε_MIN + R1-30,即码-1)+ 4 项登记归属(O 剂量 clamp 值+装载面 → 数值轮+9 装载轮 · P treatable_by → 9 装载轮 · Q F2 上臂盲区 · R signs 阈值 → 8+接线轮);裁定正文同步至 F1 取舍 4 / AC-28 / F1 复发式注 |
| BCD-D-豁免 | D 批交付 | 豁免表 24 条(每条带日期理由 + 已写者禁豁免) | 已写者禁豁免 | b7 三假绿防护;**2026-10-10 复核:无独立修复动作** —— 判据①「已写者禁豁免」②③专测已在 Unit.Sim 159/159 绿,撤条随各归属轮写者落地时机械强制,保持监控 |
| BCD-C-其他 | C 批交付 | ~~b4 门补 Boot 面~~(✅ 2026-10-10 随 BCD-码-4 结案)· 9 GDD 轮 · 数值轮 · 7 项 | 各归其轮 | b4 半边已结 |

### B. O-6 观察项修复轮(`f148fab` · 已收口)

| ID | 来源 | 内容 | 归属轮 | 备注 |
|---|---|---|---|---|
| O6-11项 | O-6 评审 | 扫描面未含 `Gameplay.UI` · 块注释剥除行号下偏 · `EndsWith` 豁免偏松 · 重编码重发去重→45 轮 · 组合层零接线→接线轮 · 同 actor 双写者二选一→接线轮 · 重复 `<summary>` · 文件系统探针=豁免型 · 多 actor×多 tick 真流有界性 · 存档/网络 blob 重映射 round-trip(7a/45 轮) | 各归其轮 | 原件 §二之二 |

### C. GDD 内联登记债( scattered · 待收口到本册)

| ID | 来源 | 内容 | 归属轮 | 备注 |
|---|---|---|---|---|
| D-21-26 | `design/gdd/inventory-and-items.md` | `IIdAuthority` / `ItemInstanceId.Next()` 21a 侧登记债未回收 | 数值轮 / 21a 轮 | 三处(:207/:621/:895)同条 |
| D-8-11 | `design/gdd/diagnosis-system.md` | 试药史是判据之一不是扣分项 → 归 #53 结算 | **#53 结算轮** | :1243/:2051 同条 |
| skill-30 | `design/gdd/skill-system.md` | 30 侧欠两项:① 等级→`EFF` 映射(形状已裁,剩逐档填表)② `EmitGrowth` 入口存在性断言 | 30 数值轮 | :139 |
| PC-债务 | `production/epics/player-controller/story-001` | `Sim` 引用仍在,豁免是已登记债务的显式化 | story-001 轮 | :47 |
| 10-ResultMul-漂移 | round33 实测(2026-10-23) | **生产急救驱动的 `ResultMul[Missed]` 违反已裁定数值** —— `EmergencyManualDriver.cs:84` 写 `{0, 32768, 65536}`,而 GDD `:639`/`:856`/AC-10-16 与已结 `OQ-10-1`(用户 2026-09-18 裁定)均为 **16384(0.25)**。GDD `:856` 明写「改回 0 须重开 OQ-10-1」。影响:`BandageRow` 是活生产路径(`CompositionRoot.cs:260` → `BootRoot.cs:285` → `SubmitEmergencySkip`),玩家跳过 / 判 Missed 时 potency = 0 ⇒ 处置全废。**为何既有测试没抓到**:`judge_test.cs:322` 断言的是 `GetResultMulTiers()`(schema 默认),不是生产副本 —— 判据测默认表、生产用副本 | **✅ 已结清(round34 · 用户裁定 ②)** —— 「改回 16384 **+** 消掉副本」已执行:`EmergencyManualDriver.cs:104` 改为 `ResultMul = EmergencyActionSchema.GetResultMulTiers()`。溯源 = `8457d5b`(sprint-06 G4.2,2026-10-11)占位随手填入,非有意改动 ⇒ 属修正而非改设计,GDD `:856` 的「改回 0 须重开 OQ-10-1」约束的是**往 0 改**,不触发重开。**副本消除的可证伪证据** = SCHEMA 探针(改 `EmergencyAction.cs` 的 schema 默认 `16384→4096` ⇒ 生产判据 ② 跟着红;副本若还在,② 会保持绿)。回归 `4665/4629/0/36` @ 2026-10-23 08:26 +0800 · round34 · `99a2292` | 判据 = `Tests/EditMode/EmergencyProcedures/emergency_result_mul_production_drift_test.cs`(5 条,**全绿**;探针 FIX 全绿 / HALF 恰 1 红 / SCHEMA 恰 2 红) |
| 25-MAG单位错 | round35 实测(2026-10-23) | **`CombatMagnitude` 的 `MAG_FLOOR`/`MAG_CAP` 把数值当 Q16.16 raw 写** —— `:43-44` 写 `1000`/`10000`,消费点 `new Fix(MAG_FLOOR)` 是 **raw 构造**(`Fix.cs:47`)⇒ 实际值 **0.01526 / 0.15259**,而 GDD `:1351`/`:1352` **✅ 裁定**为 `1/20 = 0.05` / `1/2 = 0.5`(2026-10-07)。两者差**同一 3.28×**(`1000:10000` = `1/20:1/2` = 10:1)—— **比例对、绝对尺度错**,典型单位错。**当前零生产接线**(`grep CombatMagnitude. --include=*.cs unity/Assets/ \| grep -v /Tests/` 零命中)⇒ 今天不显形;一旦 25 装配轮接上,敌/兽与零技能玩家每次命中只有 GDD 地板 30.5%,高技能段被削平到 30.5%(= AC-25-2-03 想防的模式)。**为何既有测试没抓到**:`combat_magnitude_f252_test.cs:34/42/62/99` 自引 `CombatMagnitude.MAG_FLOOR` 断言 ⇒ 常量是 3277 还是 1000 都绿(与 `judge_test.cs:322` 自引 schema 同型)。**同批发现**:`MAG_FLOOR` 三处各有定义且互不相同 —— `CombatMagnitude=1000` · `CombatActionSchema=1` · GDD=`1/20` | **✅ 已结清(round36 · 用户裁定 ②)** —— 「改值 **+** 消掉三处副本」已执行:唯一真源 = `CombatActionSchema.MagFloor/MagCap`(`static readonly Fix`,`FixParse.Parse("1/20"/"1/2")`,`Sim/CombatActionSchema.cs:63`/`:69`);`CombatMagnitude` 副本已删(`:44-46` 改为引用真源),旧名 `MAG_FLOOR`/`MAG_CAP` 保留为 `long` 属性(值 1000→**3277**、10000→**32768**),4 处既有自引式夹具不破。**落点选 `CombatActionSchema` 的理由**:A13a/A13b 断言的归属方在此,无反向依赖(双向 grep 零命中)。同批把原「只是一句注释」的 A13a/A13b 补成**可执行**断言 `ValidateMagnitudeBounds()`(⚠️ 生产调用点尚无,登记为待接线)。**GDD 自洽验算**:`1/2 − 1/20 = 9/20` = `:1350` 的 `BASE_STEP_MAX` ✓。回归 `4670/4634/0/36` @ 2026-10-23 09:06 +0800 · round36 · `47bcbd2` | 判据 = `Tests/EditMode/Combat/combat_magnitude_gdd_value_drift_test.cs`(5 条,3 红 2 绿;探针 FIX 4 绿 1 红(抓到我自己的坏判据,已删)· HALF 3 红(19660 vs 19661 精确到 1 raw)) || skill-30-接线 | round32 实测(2026-10-23) | **技能等级在生产侧从不从病史流重建** —— 三层叠加:① 两条生产写者(`SeenDiseaseProjector.cs:108` / `PrescriptionPorts.cs:150`)恒传 `level: null` ⇒ 流里 `SkillGrown.Level` 恒哨兵 `-1` ⇒ `RebuildLevels` 全跳过;② `RebuildLevels` / `TryLevelUp` / `XpGainCalculator` 生产调用点均 **0**;③ `QueryLevel` 回装配时硬编码的固定值(`CompositionRoot.cs:408` = 0)。后果:`UnlockLevel > 0` 的处方恒不解锁(今天不显形仅因数据里无门槛)。**缺口本身不修** —— 修须先裁「累计 XP 住哪 / 进不进三流 / 要不要快照」(~~ADR-026 对此零字~~ **⚠️ 2026-10-23 round37 实测订正:此句为假,原文不删留痕**) + 改 story-008 已钉死的签名 | **须用户裁定(范围已收窄,见下条 `30-XP余量无主`)** | 判据 = `unity/Assets/Tests/EditMode/SkillSystem/skill_level_never_rebuilt_from_stream_gap_test.cs`(6 条,3 探针全命中) |

### C-2. `30-XP余量无主`(round37 · 2026-10-23 新立)

| ID | 来源 | 内容 | 归属轮 | 备注 |
|---|---|---|---|---|
| 30-XP余量无主 | round37 实测(2026-10-23) | **`TryLevelUp` 需要的 XP 余量(`ref Fix currentXp`)全案无主** —— 这是 `skill-30-接线` 收窄后的**唯一真空白**(等级侧 ADR-026 §⑥ 已裁,见下条)。**链条**:`XP_gain = BASE × K_difficulty × K_novelty`(`XpGainCalculator.cs:55-56`);`SkillGrownPayload` 的**全部**字段 = `{ActorId, PatientId, SkillId, ObjectId, NoveltyClass, Level}`(`HistoryPayloads.cs:251-256`)⇒ 载荷里 **`BASE` 可由 `SkillId` 查表复得 ✓ · `K_novelty` 可由 `NoveltyClass` 查表复得 ✓ · `K_difficulty` 零字节 ✗**。而 `K_difficulty` 是**调用方推入**的(`GDD :132` 裁「30 不拉取」)。**实测两处**:① 11 侧**算了又丢** —— `PrescribeFlow.cs:351` `DeriveKDifficulty(...)`(缺 `drug_potency` 即抛,代价实打实)→ `:357` 传真值 → `PrescriptionPorts.cs:144` 形参收下 → **方法体 147-155 行零使用**;② 8 侧**连概念都没有** —— `SeenDiseaseProjector.cs` 全文 `difficulty` 命中数 = **0**。**⚠️ 2026-10-23 就地订正(初稿过度断言,已收回)**:初稿写「信息在流里不存在,无论怎么写代码都补不出来」—— **该断言为假**。`K_difficulty` **可**由三源重建:11 侧 = `|DrugPotency| / DOSE_BASE`,而 `DrugTreatmentAppliedPayload` 自带 `DrugPotency`(`HistoryPayloads.cs:233`)且 `DOSE_BASE` 是常量 65536(`CompositionRoot.cs:411`);8 侧 = 诊断对错(×0.5)而病例流有 `JudgmentRecorded` / `JudgmentRevised`(registry `:2063` / `:2078`)。⇒ 真实形态是「**载荷里没有,但流里可重建**」。**⇒ 真空白 = 该量的权威口径未裁**(三选一,各有代价):**(甲) 独立快照** —— 违 ADR-026 §⑥「少一份可失步的状态」精神(§⑦ 正是以「与 §⑥ 冲突」否决了同型的快照基线案);**(乙) 从三源重建** —— 合 ADR-016 §一 三源不变量,但须**新登记跨流关联键**(「哪条 `JudgmentRecorded` / `DrugTreatmentApplied` 对应哪条 `SkillGrown`」全案零定义),且重建是带关联的 O(n);**(丙) 扩 `SkillGrown` 载荷加 `xp_gain`** —— 最直接,但 `PayloadCodec.History.cs:430` 的 `RequireCompleteMask(..., (1u<<6)-1)` **强制 6 tag 全在场** ⇒ 非纯加法:须改编码器 + `entities.yaml` `payload_schema` + 存档 `SaveVersion` 迁移链(ADR-010 §七)。**玩家可感后果**:练到 99% 读档 ⇒ 回到该级 0%(今天不显形仅因 `TryLevelUp` 零生产调用)。**不自行裁定** | **✅ 已结清(round38 · 用户裁定 丙 + 不升版本)** | 判据 = `Tests/EditMode/SkillSystem/skill_grown_xp_gain_test.cs`(**7 条**,round38 新增 —— ⚠️ 2026-10-23 收口实测订正:初稿写「6 条」为假,复算 `grep -c "^\s*\[Test\]" <该文件>` = **7**) |
| 30-诊断错诊难度未落 | round38 实测(2026-10-23) | **丙-3 的具名代价**(用户 2026-10-23 同批裁定丙-3):8 诊断的 `×0.5 K_difficulty`(GDD `:494`「诊断错误仍涨经验 ×0.5」)**今天不实现**。**根因 = 发射时点**:`SeenDiseaseProjector.OnExamined` 在**查体完成那一刻**发 `SkillGrown`(用户 2026-10-12 裁定「查体时」),而玩家此时**尚未落笔诊断** ⇒ 「对错」这个信息**在发射点不存在**,`DiagnosisGrowthExit.EmitGrowth` 签名里压根没有 difficulty 形参。⇒ 丙-3 取**基准难度 1.0**(GDD `:353` 区间下端)使诊断成长**照常涨经验**(GDD `:238`「不论对错」),错诊 ×0.5 **不伪造、不静默跳过**。**⚠️ 修它须改 8 的状态机语义**(把发射点后移到「落笔诊断」时刻,此时 `JudgmentRecorded` 已在流上)⇒ **超出「扩载荷」范围**,归 8 的 GDD 轮 | 8 的 GDD 轮 | 落点 = `SkillGrownEmitter.cs` 7 参重载的 `<remarks>`(丙-3 说明)+ `SeenDiseaseProjector.cs:105-110` 注释 |
| ~~skill-30-接线「ADR-026 零字」~~ | **✅ round37 订正** | 该句**为假**:ADR-026 `:100` / `:153` / `:159` **明确裁定**「`Level` 以 int 承载 · **不独立快照** · **从病史流的 `SkillGrown` 重构**」,§⑦ 否决「fold 读侧从快照取基线」时理由是「与 §⑥ 直接冲突」。⇒ **快照问题对 `Level` 已有答案**,不是空白。原文按「不删」纪律保留在上行括注中 | 已结 | 复算:`grep -n "不独立快照\|从流重构" docs/architecture/adr-026-skill-growth-fixed-point.md` ⇒ `:100`/`:159` 两命中 |
| 黄金夹具生成器未入库 | round38 实测(2026-10-23) | **`Tests/EditMode/Sim/sim_codec_roundtrip_test.cs` 的黄金字节期望值,其生成器不在库内** —— 文件 `:647` 自陈「黄金字节夹具(**独立 Python 实现产出**;禁改期望值)」、`:17` 自陈「与独立实现产字节一致」,但全仓 `grep -rln --include=*.py "SkillGrown\|simCodec"` **零命中**,`tools/` 下亦无对应脚本。⇒ **该「独立实现」不可复算** —— 第三方无法重跑它来验证既有 6 条期望值,只能靠「当前实现产出的字节与字面量相符」这一**循环**关系(即自证)。ADR-012 §四 要求的「刷新协议:golden-vN → 变更日志 → 全体平台重签 → 旧版保留」**因此无法执行**:没有生成器,就无法在换实现后重签,只能手改字面量。**本轮 round38 的实际处境**:扩载荷使夹具失效,我**临时重写**了一份独立 Python 参考实现(`/home/gu/.claude/jobs/181888b1/tmp/golden/ref.py`),先要求它逐字节复现既有**全部 6 条**才用它推新值 —— **但该脚本落在 job 临时目录,未入库,任务删除即丢失** ⇒ 本轮之后,**同一缺口依旧存在,且新值同样不可复算** | ADR-012 落地轮(建三格矩阵时) | 判据 = 全仓 `grep -rln --include=*.py "SkillGrown\|simCodec"` **零命中** · 及 `ls tools/` 无 golden 相关项。⚠️ **不是本轮引入** —— 缺口早于 round38 存在(既有 6 条同样不可复算);本轮只是**又一次依赖了它**并把代价显式化。修法二选一:① 把参考实现入库(`tests/golden/simcodec/`)+ 带 manifest 头(ADR-012 `:185-187`);② 改为从**双实现**产字节(真按 ADR-012 §三 三格矩阵)。**禁**继续手改字面量 |
| 30-多玩家技能重建 | round31 立缺口 / round39 结清(2026-10-23) | **`SkillStateRebuilder.RebuildLevels` 曾返回单层 `Dictionary<int,int>`** —— 实现 `result[e.SkillId] = e.Level` **完全不看 `ActorId`** ⇒ 两个玩家练同一技能互相覆盖,「谁赢」纯由流序决定(同一技能同两玩家,A→B 得 3、B→A 得 10)。而生产契约 `ISkillGrowthPort.QueryLevel(int actorId, int skillId)`(`PrescribeFlow.cs:99-107`)**本来就是 actor 维度的** ⇒ 契约与重建不匹配。本项目 1–4 人联机从 P0 预留(`technical-preferences.md:31`)。**2026-10-23 用户裁定「改签名」**(否决「新开方法 + 旧方法加守卫」—— 两条路径并存会让 7a 有机会用错那条) | **✅ 已结清(round39 · 用户裁定「改签名」)** | 形状 = `Dictionary<int, Dictionary<int,int>>`(actor 在前,与 `QueryLevel(actorId, skillId)` 参数序对齐)。判据 = `Tests/EditMode/SkillSystem/skill_state_rebuilder_multi_actor_gap_test.cs`(**5 条,由 round31 的缺口记录改写为正向判据** —— 该文件头早已写明「改了实现就该改写,不是删掉」)。证伪 **M4 精准探针**:所有 actor 折到同一桶(单玩家时行为不变)⇒ **恰好 2 条红**(`test_multiActor_resultIndependentOfStreamOrder` / `test_multiActor_sameSkill_eachActorKeepsOwnLevel`),**单玩家 4 条全绿** ⇒ 判据只在多玩家语义被破坏时红。⚠️ **M1/M3 是粗变异**(13 红含单玩家)—— 它们只证明「改动会被发现」,**不证明判别力**;判别力由 M4 单独证明。回归 `4678/4642/0/36` @ 2026-10-23 10:42 +0800 · round39 · 基线 `67c6d7b`,Δ = +1/+1/0/0(多玩家文件 4 条 → 5 条) |
| story-008「逆序保护」自述 | round39 实测(2026-10-23) | **story-008 的 Completion Notes 声称「ordering protection added」,而该保护从不存在** —— ① 实现 `SkillStateRebuilder.RebuildLevels` **零逆序检测**(`grep -n "ArgumentException" unity/Assets/Sim.Contracts/SkillSystem/` 零命中);② 测试文件头 `skill_state_rebuilder_test.cs:10` 原写「RebuildLevels 会抛出 ArgumentException 检测到逆序」—— **假**;③ 同文件 `:159` 原写「AC-5: 逆序输入 → ArgumentException」—— **该测试方法从未存在**(全文件零逆序用例)。⇒ 这是「**用一句自述顶替一个从未存在的门**」形态(与 `TD-s13-01` / S18-1 规则九门同型),且它撑过了一整条 story 的收口评审。**实际语义**:顺序是**调用方约定**(7a 读出顺序已保证),不是执行体强制。 | **✅ 本轮已就地订正(round39)** —— 三处自述全部改为「原文为假 + 保留留痕」,并明写「顺序是调用方约定」。**是否要真的加逆序守卫 = 另裁**(会改变契约语义:当前实现容忍乱序,加了就变成 fail-loud;7a 尚未实现,加守卫的收益待评估) | 判据 = `grep -n "ArgumentException" unity/Assets/Sim.Contracts/SkillSystem/SkillStateRebuilder.cs` **零命中** · `grep -c "outOfOrder\|逆序" unity/Assets/Tests/EditMode/SkillSystem/skill_state_rebuilder_test.cs` 仅命中注释 |

### C-3. `OQ-9-1` 乙案的具名代价(round40 · 2026-10-23 新立)

| ID | 来源 | 内容 | 归属轮 | 备注 |
|---|---|---|---|---|
| 9-照护杠杆键前缀 | round41 实测(2026-10-23) | **照护杠杆(`handle == care` 的那条支)在生产路径上恒不适用** —— 根因是**两处数据用不同的键形态**:`StateMachine.CareApplicableDiseases`(`StateMachine.cs:189-194`)装的是**裸键** `{typhoid, dysentery, heart_failure}`,而注册表 `disease_registry.json` 的 `key` 是 **`DIS_` 前缀**形态(`DIS_TYPHOID` / `DIS_DYSENTERY` / `DIS_HEART_FAILURE`)。两者交集 = **空集**(`CareLeverApplicability.AppliesToKey` 零归一化,`grep -n "Substring\|TrimStart\|StartsWith(\"DIS_" CareLeverApplicability.cs StateMachine.cs DiseaseHandle.cs` 零命中)。**两处后果**:(a) 生产消费点 `DiseaseVitalsService.cs:442` 的 `CareLeverApplicability.AppliesTo(entry)` 对**每一行真表**返回 `false` ⇒ 照护通道恒 `CareChannelInput.None` ⇒ **照护暂停死亡倒计时在三种病上全部失效**(GDD `:1135-1140` 的 K8 收窄只该排除破伤风,现在把伤寒/痢疾/心衰一起排除了);(b) `DiseaseHandle.Derive` 的 `care` 分支永不命中 ⇒ 这三行派生成 `symptomatic_only` 而非 GDD `:294` 要求的 `care`。**为何既有测试全绿**:`care_lever_and_aggregator_test.cs:52-54` 与 `state_machine_death_test.cs:70-72` 都用**裸键夹具**(`Entry("typhoid")`),而真表用 `DIS_` 前缀 —— 判据测的是夹具形态,不是真表形态(与本册 `judge_test.cs:322` / `combat_magnitude_f252_test.cs:34` **同型**:自引式夹具把被测对象与期望值锁在一起)。**已钉绊线**:`disease_registry_bake_pipeline_test.cs:125` 的 `test_realSource_handleIsDerivedIntoClosedSet_perRow` 如实钉住**当下**产物(`symptomatic_only`)并在 `:116-122` 点名该缺陷;证伪轮 **M2**(把 `care` 判定提到 `causal` 之前)28/28 **全绿**—— 这正是「`care` 分支不可达」的独立佐证:顺序怎么改都碰不到该分支。修好后该测试期望值须同步改成 `care`(三行) | **✅ 已结清(round42 · 用户裁定 ②)** —— 「在 `AppliesToKey` 做前缀归一」已执行:归一表 `CareLeverApplicability.CareApplicableNormalized` **从** `StateMachine.CareApplicableDiseases` 静态展开(裸形 + `DIS_` 大写前缀形),不含独立字面量 ⇒ 改适用集只需改那一处;三入口(`AppliesTo` / `AppliesToKey` / `StateMachine.IsCareApplicable`)**全部收敛到同一实现**(后两者是转发口,不再各存判据)。⚠️ **实测踩坑**:第一版拼 `DiseaseKeyPrefix + key` 得 `DIS_typhoid`(小写),`Ordinal` 下与 `DIS_TYPHOID` 不等 ⇒ 重烘产物**一位没变**(2172 B 前后同尺寸,13 行仍全 `symptomatic_only`);抓到它的是新判据 `test_prefixedKey_equivalentToBareKey`(期望值用 `ToUpperInvariant()` 拼)—— **实现错了,判据是对的**。修后产物 **2172 → 2136 B**(恰短 36 = 3 行 × 12),逐行解码确认三行 `care` = `{DIS_HEART_FAILURE, DIS_TYPHOID, DIS_DYSENTERY}`,与 GDD `:294` 一致。② 的具名代价已写进实现注:**隐式键改写** —— 若将来出现裸名与另一病种前缀名同形的两个病种会被混为一谈,新增病种时须复核(当前 13 行无此形态) | 复算:`sed -n '188,194p' unity/Assets/Sim/StateMachine.cs`(裸键)· `python3 -c "import json;d=json.load(open('assets/data/disease_registry.json'));print([r['key'] for r in (d['diseases'] if isinstance(d,dict) else d)])"`(`DIS_` 前缀)· 判据 = `care_lever_and_aggregator_test.cs` 的 `test_prefixedKey_equivalentToBareKey` + `disease_registry_bake_pipeline_test.cs:125` 的三行 `care` 期望 |
| 9-signs真表全空 ✅ | round41 实测(2026-10-23) | **真表 13 行的 `signs[]` 全是 `[]`** —— GDD `:508-513`(B6)要求「9 的 `signs[]` 不该全空」,而当前 `disease_registry.json` 每行的 `signs` 均为空数组。**编码面已就绪**(round41 已把 `signs[]` 写进 cooked 布局,含 `null` / `[]` 区分),**数据面为零** ⇒ 六通道位域(`Face/Voice/Posture/Breath/Touch/Wound`)在生产里从未被任何一行填过。**归属 = 8 的词表轮**(病种→体征映射是 8 的词表,不是 9 的编码),归 9 的实现轮**不能**自填(会造第二真源)。**已钉**:`test_richRow_signsAndRelapse_surviveRoundTrip` 用**非空**合成夹具把编码/解码真的走了一遍,故「全空也绿」的假绿形态不成立;但**真表内容为零**这个事实仍无判据钉住(有人删掉合成夹具也不会红) | **✅ 已结清(round43 · 用户裁定「甲 + 乙 + 乙」全做)** —— ① **13/13 行全部填实**(**8 病种 + 5 伤情**),词条总数 **44**;② **通道闭集六 → 八**:9 侧 `SignChannels` 加 `Subjective = 1<<6` / `History = 1<<7`(8 侧 `SignChannel` 枚举同步加 `Wound=6` / `Subjective=7`,映射表 `MaskByOrdinal` / `PhysicalBits` 同步扩位),**理由**:`INJ_*` 五行的伤口词条无处安放(8 侧无 Wound 序数标签)+ `DIS_NEURASTHENIA` 的 R3.6 主观族 + `sign_purulent_stool` 的病史证据(9 侧无位可落,填 `TOUCH` 会把「病人说拉脓血」伪装成「医生触诊有发现」);③ 词表 34 → **43 行**(+5 伤口 +4 主观),两条金标**有意识重钉**:`GoldenContentHash` `3954e294 → 3f8a0cd1` · `GoldenConstantsHash` `b711a17c → bfb52fd8`;④ **仍未结的相邻事实(不属本行)**:43 行词表里 **5 条无病种引用**(`sign_retraction` / `sign_cheyne_stokes` / `sign_dullness` / `sign_fever_skin` / `sign_pulse_bounding`),**悬空引用 0 条**;`test_ac834_reverseOrphan_blockedish` 仍 `Ignore`(解锁须走 TR-diag-013 归属件),其头部已加注「34 这个数字已过期」。**⚠️ 2026-10-23 就地订正(本行此前两处事实错误,原文留痕)**:**①** 原文写「**5 条 = 为 P1a 病种预留**」—— 该断言**无出处**(全仓唯一这么写的就是本行自身,自引式断言);实测五条的 GDD 关联列点的是**症状名 / 病理名**(重度呼吸困难 / 濒死 / 肺实变·胸腔积液 / 发热 / 高热),13 行病种/伤情表无对应项,**是否于 P1a 引入对应病种未裁**。**②** 原文写「**6 病种 + 7 伤情**」—— 实测为 **8 病种 + 5 伤情 = 13**(`python3 -c "import json,collections;d=json.load(open('assets/data/disease_registry.json'));print(collections.Counter(r['kind'] for r in d['diseases']))"` ⇒ `Counter({'disease': 8, 'injury': 5})`)。**③** `test_ac834_reverseOrphan_blockedish` 已于同日**解锁并改写**为三条测试(见 AC-8-34 反向子句条目),原 [Ignore] 测试体已删除 —— 它用空引用集夹具,测的是夹具形态不是数据。 | 复算:`python3 -c "import json;d=json.load(open('assets/data/disease_registry.json'));rs=d['diseases'] if isinstance(d,dict) else d;print(sum(1 for r in rs if r.get('signs')),'/',len(rs),'行有 signs')"` ⇒ `13 / 13`;词条总数 = `44`;孤儿 5 / 悬空 0(见下条复算脚本) |
| 9-潜伏痊愈边关闭 | round40 实测(2026-10-23) | **用户裁定 `OQ-9-1` 取乙案(起算点 = `onset + incubation`)的具名代价:GDD `:567` 的「潜伏 → 痊愈」边**当前不可达**。**GDD 原文**(`:544`/`:567`)把「不经发病直接痊愈 —— 有些病自己会好」列为**合法**转移,其触发条件 = `self_limit ∧ ¬relapse` —— 与痊愈支条件 ②③④ **同一谓词**。乙案下潜伏期内:① `position = 0 < RECOVER_THRESHOLD` ✓;② `trend = 0 ≤ 0` ✓(非上升期门放行);③ `held = 0 < W` ✗(**乙案直接造成**)⇒ 判不出 Recovered。潜伏期结束后 `position` 已开始爬升,「不经发病」的字面语义**再也不会成立**。⇒ 该边**在乙案下永久关闭**,不是「暂时未实现」。**恢复须另裁**(可选方向:① 为潜伏期单开一个「未发病痊愈」终态;② 把该边的触发条件从「position 低」改为「τ < incubation ∧ 其他自愈条件」;③ 承认该边在设计上是死边并删 GDD 措辞)—— **三选一均改 GDD 语义,不自行裁定**。**已钉判据**:`test_cost_latentToRecoveredEdgeIsCurrentlyUnreachable`(在 `onset + incubation` 时刻断言 position < RECOVER ∧ held = 1 < W ⇒ 不可达),该判据红 = 有人悄悄恢复了这条边 | **须用户裁定**(恢复 / 删措辞 / 单开终态) | 复算:`grep -n "潜伏.*痊愈\|不经发病" design/gdd/disease-simulation.md` ⇒ `:544`/`:567`;判据 = `unity/Assets/Tests/EditMode/DiseaseSimulation/low_position_held_incubation_boundary_test.cs` 第 6 条 |

### D. 其他散落登记(active.md 内联 · 待收口)

| ID | 来源 | 内容 | 归属轮 | 备注 |
|---|---|---|---|---|
| 静置骑 | sprint-04 :82 | 生产静置骑 y 格界(结构性) | 门④ PlayMode 实测 | 挂 EC-1/AC-1-21 |
| OnPositionSample | sprint-04 :58 | 跨格写者运行期不发 | Phase 1 尾/Phase 2 | 6 项 |
| 新架构缺口 | sprint-04 :297 | 未结待裁 | 待用户裁 | — |
| 未闭色值 | sprint-04 :1094 | `--skeuo-brass-aged` 与 art-bible 不一致 | 待裁 | — |
| C7 已注册类 | sprint-04 :1156 | 黄铜/器具/焦点族零登记表 | 待裁 | 决定 019-d 接图量 |
| S3 落点 | sprint-04 :1758 | S3 落点待裁(新 story-005 或挂 tick 接线 epic) | 归 TD 裁 | — |

---

## 二、已结登记债(Closed · 留审计轨迹)

| ID | 结案 | 锚点 |
|---|---|---|
| — | — | — |

---

## 三、维护规则

1. **新债入册**:评审 / 复核 / 观察项产出「登记不修」时,同时往本册加一行(来源 + 内容 + 归属轮)。
2. **归属轮到达**:按本册「归属轮」列逐项收口;结案标 ✅ + 锚点(提交号 / 原件路径)。
3. **不静默删除**:结案行不删,只标状态;audit trail 完整。
4. **唯一真源**:GDD / active.md 里的「登记债」字样指向本册,不各自维护清单。

> **当前未结总数**:A 类 14 项中 **4 项已结**(码-2/3/4/5)⇒ 未结 ≈ **21 项**(去重前)。
> 🔁 **2026-10-29 复算订正(原文不删)**:上行 `21` 是 **2026-10-10** 的值,此后本册又结了 9 项
> (码-1 · C-GDD · 10-ResultMul · 25-MAG · 30-XP余量无主 · skill-30「ADR-026 零字」·
> 30-多玩家 · story-008逆序 · 9-照护杠杆 · 9-signs),同时新增 2 项(黄金夹具生成器 · C7 等)。
> **现值 = 未结 23 行 · 已结 14 行**。
>
> **复算命令与口径(⚠️ 本册不能用纯脚本给结论,原因见下)**:
>
> ```bash
> # ① 物理数据行(含 §二 的 `| — |` 占位)
> grep -c '^| ' production/registration-debt-register.md     # 含表头行,读时须减
> # ② 第 46 行把**两条债挤在一行**(`25-MAG单位错` 与 `skill-30-接线`,以 `||` 相连)
> grep -n '||' production/registration-debt-register.md      # ⇒ 恰 1 处
> # ③ 因此逻辑行 = 37,分类须**逐行人工判**(判据 = 行内是否出现「✅ 已结清 / 结案 / 已结」)
> ```
>
> ⚠️ **`||` 那一处是数据卫生问题**:它让任何按行切分的脚本把两条债数成一条,
> 且会让「未结计数」少 1。**未修**(改它会动行内正文,而本册的纪律是「结案行不删、不改史」),
> 在此登记为**已知失真源**。⇒ 引用本册计数者必须说明是**人工分类**的结果。

> **最近清理**:2026-10-10 · 由「清理登记债」指令创建。
> **2026-10-10 收口批**:BCD-码-2(接线轮)· BCD-码-3(Append 单调断言)· BCD-码-4(b4 补
> Boot 面)· BCD-码-5(b7 补 Sim.Contracts 面 + 六面收紧)—— 均为**代码修复 + 测试 + 变异实测**
> (三发变异各恰红 1)。
> **2026-10-10 第二收口批**:BCD-码-1(ε_MIN = 1/65536 + R1-30 + 双测,MUT 恰红 1)·
> BCD-C-GDD(8 项全落 GDD,D-9-K…R:3 裁定 + 1 办结 + 4 登记归属)· BCD-D-豁免
> (复核 = 无独立修复动作,转监控)。**A 类无「待用户决策」项残留** —— 剩余 open 项均已
> 归属到具体轮次(数值轮 / 9 装载轮 / 8+接线轮 / 各归属轮写者落地撤条)。
