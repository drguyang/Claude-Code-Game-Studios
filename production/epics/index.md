# Epics Index

Last Updated: 2026-10-03
Engine: Unity 6.3 LTS (6000.3.24f1)

| Epic | Layer | System | GDD | Stories | Status |
|------|-------|--------|-----|---------|--------|
| item-database | Foundation | 21 物品与配方数据库 | design/gdd/item-database.md | 12 stories | Complete ✅ 2026-09-25 |
| input-system | Foundation | 3 输入与设备 | design/gdd/input-system.md | 13 stories | Complete ✅ 2026-09-28 |
| skill-system | Foundation | 30 技能与熟练度 | design/gdd/skill-system.md | 8 stories | Complete ✅ 2026-09-27 |
| skeuomorphic-ui | Foundation | 42 拟物 UI 框架 | design/gdd/skeuomorphic-ui.md | 18 stories + **019 贴图接入 Ready** | Complete ✅ 2026-09-28(⚠️ 范围见 EPIC §范围边界声明:不含贴图绑定) |
| audio-system | Foundation | 44 音频系统 | design/gdd/audio-system.md | 14 stories + **015 资产接入 Ready**(5 个被引 `.wav`)+ **016 资产接入 Ready**(余 7 项 / R-2 九项闭合) | Complete ✅ 2026-09-28(⚠️ 范围见 EPIC §范围边界声明 **一/二**:不含素材本体;015/016 两件刻意分件,不合并) |
| telemetry-analytics | Foundation | 51 遥测与分析 | design/gdd/telemetry-analytics.md | 8 stories | Complete ✅ 2026-09-26 |
| time-weather | Foundation | 5 时间与天气 | design/gdd/time-and-weather.md | 5 stories | Complete ✅ 2026-09-30 |
| clinic-machine | Core | 24 医馆即机器 | design/gdd/clinic-machine.md | 5 stories | Ready(未实现) |
| foraging | Core | 17 采集 | design/gdd/foraging.md | 5 stories | In Progress(WIP · 未提交) |
| random-events | Core | 52 随机事件导演 | design/gdd/random-events.md | 6 stories | Complete ✅ 2026-09-30 |
| medical-consequences | Core | 53 医疗后果与责任 | design/gdd/medical-consequences.md | 4 stories | Ready(未实现) |
| casebook | Presentation | 39 脉案 | design/gdd/casebook.md | 6 stories | Ready(未实现) |
| tutorial-onboarding | Presentation | 48 教学与引导 | design/gdd/tutorial-and-onboarding.md | 5 stories | Ready(未实现) |
| camera-viewpoint | Foundation | 20 相机与视角 | design/gdd/camera-and-viewpoint.md | 6 stories | Complete ✅ 2026-10-03(6/6) |
| case-system | Core | 37 病例系统 | design/gdd/case-system.md | 6 stories | Ready(未实现) |
| combat-weapons | Core | 25 战斗与武器 | design/gdd/combat-and-weapon-lines.md | 6 stories | Complete ✅ 2026-09-30 |
| death-respawn | Core | 29 死亡与复活 | design/gdd/death-and-respawn.md | 6 stories | Ready(未实现) |
| diagnosis-system | Core | 8 诊断系统 | design/gdd/diagnosis-system.md | 6 stories | Ready(未实现) |
| disease-simulation | Core | 9 疾病模拟 | design/gdd/disease-simulation.md | 7 stories | ✅ Complete(story-007 重开轮闭环 2026-10-07:处置轴 + treatable_by + NOISE_BAND_9 双常量;双代理评审修复后复跑全绿:9 侧 67/67 · 11 侧 174/174 · 10 侧 116/116 · 全量 2948-2901/0 红) |
| emergency-procedures | Core | 10 急救动作 | design/gdd/emergency-procedures.md | 7 stories | In Review(7/7 已实现 · A1/A2/A6/B4/C4/C5 已闭 · 0 红;✅ **评审原件两份均已在库**(首轮+round2,2026-10-03);**真实残留 = D1/D2/D3 文档对齐 + 实跑测试套件**(round2 未实跑,结论均基于源码阅读);另 007 = b6 门查出的手搓点) |
| enemy-ai | Core | 27 敌人 AI | design/gdd/enemy-ai.md | 5 stories | Complete ✅ 2026-09-30 |
| interaction-system | Core | 4 交互系统 | design/gdd/interaction-system.md | 4 stories | Ready(未实现) |
| inventory-items | Core | 21b 库存与物品 | design/gdd/inventory-and-items.md | 6 stories | Ready(未实现) |
| modular-building | Core | 6 世界与生态区 | design/gdd/modular-building.md | 7 stories | Complete ✅ 2026-10-03(7/7 story;C1/C2/N-r1/C8-ID 全闭 · 本轮 72/72 绿 · 全量 2204/2163/0红;**未闭登记 = N-r2 生产装配根(待 Boot 装配轮)+ AC-23-09 跨平台签名(待 ADR-012 矩阵)**) |
| patient-ai | Core | 13 病人 AI | design/gdd/patient-ai.md | 4 stories | Ready(未实现) |
| player-controller | Core | 1 玩家控制器 | design/gdd/player-controller-and-movement.md | 6 stories | Complete ✅ 2026-10-03(6/6 story Complete;两轮评审判据缺陷已修;88 过 + 3 NOT-RUN) |
| prescription-medication | Core | 11 处方与用药 | design/gdd/prescription-and-medication.md | 5 stories | Ready(未实现) |
| processing | Core | 14 加工与制作 | design/gdd/processing.md | 5 stories | Ready(未实现) |
| world-ecozones | Core | 6b 生态区与 POI | design/gdd/world-and-ecozones.md | 6 stories | Complete ✅ 2026-10-03(6/6 story;N1/N5 已修 · 本轮 109/109 绿;**未闭登记 = N3 白名单判据(待 27 侧落地)+ Story 005 走查 EXTERNAL**) |
| persistence-service | Foundation | 7a 持久化服务 | design/gdd/persistence-service.md | 2 stories | Complete ✅ 2026-10-02(2/2;002 = ADR-029 契约支) |
| save-slot-ui | Foundation | 7b 存档位 UI | design/gdd/save-slot-ui.md | — | Ready(未实现) |

---


## 越序实现登记(2026-10-01)

以下两项在 **2026-10-01 大量落盘实现**,但**不属于任何 sprint 计划** —— 登记为
**架构依赖先行(越序)**,sprint 计划表本身不改:

| Epic | 计划归口 | 实际状态 | 越序成因 |
|------|---------|---------|---------|
| `world-ecozones`(6b) | 无(sprint-02 止于 sim 层三 epic;sprint-03 = combat/enemy/emergency) | In Review(4/5;005 = [L] Pending) | ADR-015 单一整数格 `WorldPos` + ADR-021 POI 状态所有权 + ADR-022 关卡工具,三者同指世界层地基;ecozone 查询又是 sprint-03 `time-weather` Story 003 与 chunk 激活的直接输入。**先世界层基建、后计划内 epic**。 |
| `modular-building`(6) | 无 | In Review(6/6 测试绿) | 同族:`BuildSlot` / `SlotType` 与地形共用 `WorldPos`(ADR-015 §三),建造槽位不先落盘则 world-ecozones 的 `EcozoneOf` 被消费侧提前引用。ADR-022 侧 `world_buildslots.json` 导出契约以本 epic 为第一个消费者。 |

**纪律说明**:越序 ≠ 已完成治理。两者的双代理评审**均已发起并有结论**(结论 = REQUEST_CHANGES,
非「初评即 APPROVE」:`bb477e6` 记 modular **5 BLOCKING** / we **4 BLOCKING`)。
**⚠️「已修复」的口径须按下文三条缺口气读,不得读成「5/4 全修」** —— 尤其 ①b 表明 we 的 B1
**未被修复,只是被降级为 TODO**。修复提交为 `da04f41`(modular 5B)/ `43400dc`(we 4B,
其中 B1 为 TODO 化),测试数 51/51 与 87/87 由 `bc7657e`/`c683aa5` **转录**记录。
**测试证据已补(缺口 ② 消除,2026-10-02【超算】)**:全量 EditMode batchmode 复跑
**1964 total / 1933 passed / 1 failed / 29 skipped / 1 inconclusive**,其中
ModularBuilding **51/51** 与 WorldEcozones **87/87** 逐例全绿
(证据:`production/qa/evidence/editmode-full-rerun-2026-10-02.md`,含逐例明细表)。
⇒ `43400dc` 把 story-003/004 AC 整批勾 `[x]` 时**未同批复跑**的取证缺口,由本轮补齐。

**⚠️ 本文一处失实陈述的就地订正(2026-10-02【超算】,commit `39ef397` 原文之误)**:下文原写
「桌面批 `da04f41` / `43400dc` 只改代码与勾 AC,`git show --stat | grep -c production/` = **0**」——
**前半对,后半错**。实测:`da04f41` 的 9 个文件**确无** `production/`;但 **`43400dc` 动了两个 production 件**
(`story-003-poi-state-machine-and-world-stream.md` · `story-004-chunk-activation-and-consumption-boundary.md`,
各自改的正是 AC 勾选)。
错误成因 = `git show --stat` 对长路径以 `...` 前缀截断,故 `grep '^ production/'` 对 `43400dc` 恒为 0;
须以 `git show --name-only` 复核。**结论方向不变**(「BLOCKING 已修」仍无逐条对账件),但
「两个提交都没碰 production/」这句为假,特此订正。

**不记 Complete 的硬缺口(共三条,均实测于 2026-10-02【超算】 HEAD)**:

- ~~**缺口 ①a —— 无逐 BLOCKING 对账件**~~ ✅ **已闭(2026-10-02 对账轮)**。
  两份逐 BLOCKING 对账件已落 `production/qa/evidence/`:
  `reconciliation-modular-building-2026-10-02.md`(5 条)· `reconciliation-world-ecozones-2026-10-02.md`(4 条),
  均含「原判定 → 修复落点 → 实测证据 → 验证命令」,且**逐条独立复核**而非转录 commit message。
  ⚠️ **对账结论 ≠ 修复全部成立** —— 实测:**modular B4 部分修**(bit-packing 在 `StructurePlaced` 侧仍在)、
  **we B1 未修**(仅 TODO 化)。详见 ①b 与下 §对账轮发现。
- **缺口 ①b —— `world-ecozones` 的 B1 未修,被降级为 TODO**。`43400dc` message 自陈 B1 的修法是
  「**添加 TODO 注释**」,HEAD 中 `Sim/World/PoiStateMachine.cs:105` / `:131` 两处 TODO 仍在;
  且 `Append` 仍**绕过 codec 手搓** `new PayloadRef(blobId: poiId, offset: (int)toState, length: 8)`
  —— 把 `poiId` 当 `blobId`、`newState` 当**字节偏移**用,与 `PayloadRef` 契约注释(`Offset` = 字节偏移,
  非业务字段)语义冲突。
  ⚠️ **「前置未就绪」的免责不成立**:`Sim.Codec` 程序集**已存在**,其
  `PayloadCodec.World.cs:375-402` **已有完整的 `PoiStateChangedPayload` 编解码**(tag 1/2 + 完整掩码校验),
  `Sim.Codec/IBlobPool.cs` 亦已在库(`entities.yaml:1974` 的 `payload_schema` 即 `poi_id: i32; new_state: 枚举`)。
  ⇒ 这是**可接而未接**,不是被前置阻塞。
  同族:`da04f41` 把 `StructureModified` 的 bit-packing 改成 `offset: 0` 后**紧接一句 TODO 未接 codec**,
  而 `PayloadCodec.World.cs:304` 的 `StructureModifiedPayload` 编解码**也已存在**;
  且 `StructurePlaced` 的 bit-pack(`moduleId | orientation<<16 | variant<<24`)评审未点,**现状仍在**。
- **缺口 ①c —— host gate 无负向夹具**。AC-6-26a `[B]` 要求「客户端调用写通道 ⇒ 断言失败/拒写」,
  但 `Tests/EditMode/WorldEcozones/poi_state_machine_test.cs:21` 的 `FakeEventAuthority.IsHost => true` 恒真,
  13 个 `[Test]` 中**无一**注入 `IsHost => false` 验证拒写路径 ⇒ 只有正路径,负向判据未执行。

### 对账轮发现(2026-10-02 · 实测 HEAD `1a884c9`)

对账件编制过程中**独立复核**出两处此前未登记的事项:

- 🔴 **modular B4 是「部分修」而非「已修」** —— `da04f41` 自述「移除 bit-packing」,
  实测 `Sim/World/StructureKinds.cs:191-198` 的 `StructurePlaced` 仍走
  `moduleId | (orientation << 16) | (variant << 24)`,**且第一条干净赋值立即被覆盖 = 死代码**。
  `index.md` 原 §①b 末句已点到该 bit-pack「评审未点、现状仍在」,本轮坐实为 **B4 的未闭半边**。

- 🔴 **根因已上溯并裁决(2026-10-02)—— 两份对账件曾把根因写错,特此订正**

  对账件初稿把 modular B4 / we B1 的阻塞写成「接线须先裁 `Sim` → `Sim.Codec` 引用边」。
  **该措辞有误** —— 这条边**不是待裁项,是已裁的禁止项**:ADR-025 §①:111 明文
  「`Sim` 期望引用集 = BCL + `Sim.Contracts`(仅此一件)」,且 **b2 门已将其变为构建失败**
  (`AssemblyGates.cs:148-150`)。两处措辞已就地订正。

  **真正的根因在上一层**:`Sim` 的写者(`PoiStateMachine` / `StructureKinds`)
  **没有任何合法的编码路径** —— `PayloadCodec` 住 `Sim.Codec`(够不着),
  不可变 blob 池住 7a/45(也够不着),而 `PayloadRef` 住 `Sim.Contracts`(够得着)。
  实测后果:**全库每一个写者都在手搓 `PayloadRef`,且零字节真的进池**
  (`craft_event_payload_test.cs:280` 这个「最正确」的写路径同样是 `PayloadRef(0,0,len)` 假引用)。
  这是 **ADR-006 Amendment G-2 遗留的洞**(只定了载荷**读形**,未定**写形**)。

  ⇒ 已由 **ADR-029**(`docs/architecture/adr-029-payload-encoder-abstraction.md`,
  ✅ **Accepted 2026-10-02**)裁决:新增**第七个 P0 抽象点 `IPayloadEncoder`**
  (住 `Sim.Contracts`,乙案 —— 编码+入池一体)。`Sim` 引用集**一字不改**。
  实现归 ADR-029 的实现轮(**须另立 story**,拆契约支 + 接线支)。
- ⚠️ **we B2 的形式已闭但原处置有取证缺口** —— `43400dc` 勾 13 个 AC 时**未同批复跑**,
  该缺口已由 `editmode-full-rerun-2026-10-02.md`(87/87)补齐。

后续 gate-check 以本节为索引:**闭合 ①b(接 codec —— 路径已由 ADR-029 裁决)后,
两 epic 方可转 Complete**。**①a 已闭 · ①c 已闭(2026-10-02)**。

### ADR-029 实现轮 —— 三条 story 已立(2026-10-02)

| Story | 落点 | 内容 |
|---|---|---|
| `persistence-service/story-002` | 7a(拥有 `Sim.Codec`) | **契约支**:`IPayloadEncoder` + `IBlobSink` + `PayloadEncoder` + `EncodeBoxed` 分派 |
| `world-ecozones/story-006` | 6b | **接线支**:`PoiStateMachine` 改走 `IPayloadEncoder`(闭合 B1) |
| `modular-building/story-007` | 6 | **接线支**:`StructureWriter` 三处写入改走 `IPayloadEncoder`(闭合 B4) |

⚠️ **依赖序**:契约支是两条接线支的**硬前置**;§③ 手搓门须待**两条接线支都完成**后才可启用
(否则会误报另一处未接线的写者)。
⚠️ **落点理由**:无 infra epic;既有惯例是横切契约按系统分落(ADR-025 即散落 10+ epic)。
`Sim.Codec` 的归属 = 7a(其 EPIC 自陈「Architecture Module: L3 契约程序集(`Sim.Codec`)」,
`IBlobPool.cs` 头注亦把池生命周期归 7a/45)。代价:7a 由 Complete **回退 In Progress** —— 如实登记。

- ✅ **缺口 ①c 已闭(2026-10-02)** —— `poi_state_machine_test.cs` 补 host gate 负向夹具 6 例:
  客户端拒写(零 Append)· 客户端不改状态 · 发现门同受覆盖 · 主机降级后写被挡 ·
  只读重建**不**受门影响 · 主机基线仍写(反向用例)。`PoiStateMachineTest` **13 → 19/19 通过**。
  ⚠️ **经突变测试坐实非空转** —— 临时移除 `PoiStateMachine.cs:83-84` 的 host gate 后,
  **恰 4 例红**(全部为「拒写」断言),`rebuildFromEvents` 与 `hostBaseline` 不红(正确:
  前者测只读重建、后者测主机侧,均不该依赖写门)。原文件已复原,工作树无残留。
  📌 **同时登记一处新缺陷(未修)**:gate 对客户端返回 `PoiStateTransferResult.PoiNotFound`,
  与「poi_id 不存在」**混同** —— 调用方无法区分「我不是主机」与「该 POI 不存在」。
  夹具刻意**不**把该错误码钉进断言(否则等于把缺陷固化为契约);正确修法 = 新增 `NotHost` 结果码,
  归 we 实现轮(须同步 story-004)。
`world-ecozones/story-003` 唯一未勾的 AC(`entities.yaml` ↔ kindgen 路由一致性,A1–A5)与 ①a 同类,可同批处理。
