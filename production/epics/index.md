# Epics Index

Last Updated: 2026-10-02
Engine: Unity 6.3 LTS (6000.3.24f1)

| Epic | Layer | System | GDD | Stories | Status |
|------|-------|--------|-----|---------|--------|
| item-database | Foundation | 21 物品与配方数据库 | design/gdd/item-database.md | 12 stories | Complete ✅ 2026-09-25 |
| input-system | Foundation | 3 输入与设备 | design/gdd/input-system.md | 13 stories | Complete ✅ 2026-09-28 |
| skill-system | Foundation | 30 技能与熟练度 | design/gdd/skill-system.md | 8 stories | Complete ✅ 2026-09-27 |
| skeuomorphic-ui | Foundation | 42 拟物 UI 框架 | design/gdd/skeuomorphic-ui.md | 18 stories | Complete ✅ 2026-09-28 |
| audio-system | Foundation | 44 音频系统 | design/gdd/audio-system.md | 14 stories | Complete ✅ 2026-09-28 |
| telemetry-analytics | Foundation | 51 遥测与分析 | design/gdd/telemetry-analytics.md | 8 stories | Complete ✅ 2026-09-26 |
| time-weather | Foundation | 5 时间与天气 | design/gdd/time-and-weather.md | 5 stories | Complete ✅ 2026-09-30 |
| clinic-machine | Core | 24 医馆即机器 | design/gdd/clinic-machine.md | 5 stories | Ready(未实现) |
| foraging | Core | 17 采集 | design/gdd/foraging.md | 5 stories | In Progress(WIP · 未提交) |
| random-events | Core | 52 随机事件导演 | design/gdd/random-events.md | 6 stories | Complete ✅ 2026-09-30 |
| medical-consequences | Core | 53 医疗后果与责任 | design/gdd/medical-consequences.md | 4 stories | Ready(未实现) |
| casebook | Presentation | 39 脉案 | design/gdd/casebook.md | 6 stories | Ready(未实现) |
| tutorial-onboarding | Presentation | 48 教学与引导 | design/gdd/tutorial-and-onboarding.md | 5 stories | Ready(未实现) |
| camera-viewpoint | Foundation | 20 相机与视角 | design/gdd/camera-and-viewpoint.md | 6 stories | Ready |
| case-system | Core | 37 病例系统 | design/gdd/case-system.md | 6 stories | Ready(未实现) |
| combat-weapons | Core | 25 战斗与武器 | design/gdd/combat-and-weapon-lines.md | 6 stories | Complete ✅ 2026-09-30 |
| death-respawn | Core | 29 死亡与复活 | design/gdd/death-and-respawn.md | 6 stories | Ready(未实现) |
| diagnosis-system | Core | 8 诊断系统 | design/gdd/diagnosis-system.md | 6 stories | Ready(未实现) |
| disease-simulation | Core | 9 疾病模拟 | design/gdd/disease-simulation.md | 6 stories | Complete ✅ 2026-09-30 |
| emergency-procedures | Core | 10 急救动作 | design/gdd/emergency-procedures.md | 6 stories | Complete ✅ 2026-10-02 |
| enemy-ai | Core | 27 敌人 AI | design/gdd/enemy-ai.md | 5 stories | Complete ✅ 2026-09-30 |
| interaction-system | Core | 4 交互系统 | design/gdd/interaction-system.md | 4 stories | Ready(未实现) |
| inventory-items | Core | 21b 库存与物品 | design/gdd/inventory-and-items.md | 6 stories | Ready(未实现) |
| modular-building | Core | 6 世界与生态区 | design/gdd/modular-building.md | 6 stories | In Review(双评 REQUEST_CHANGES 5B · 51/51 逐例复跑绿 · 对账件缺 + Modified 接 codec 降级 TODO) |
| patient-ai | Core | 13 病人 AI | design/gdd/patient-ai.md | 4 stories | Ready(未实现) |
| player-controller | Core | 1 玩家控制器 | design/gdd/player-controller-and-movement.md | 6 stories | In Review(6/6 story Complete · 仅因 evidence 对账件缺口未转 Complete;2026-10-02) |
| prescription-medication | Core | 11 处方与用药 | design/gdd/prescription-and-medication.md | 5 stories | Ready(未实现) |
| processing | Core | 14 加工与制作 | design/gdd/processing.md | 5 stories | Ready(未实现) |
| world-ecozones | Core | 6b 生态区与 POI | design/gdd/world-and-ecozones.md | 5 stories | In Review(4/5 · 双评 REQUEST_CHANGES 4B,**B1 未修降级 TODO** · 87/87 逐例复跑绿;005 [L]) |
| persistence-service | Foundation | 7a 持久化服务 | design/gdd/persistence-service.md | — | Ready(未实现) |
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

- **缺口 ①a —— 无逐 BLOCKING 对账件**。「5 / 4 个 BLOCKING 已修」目前只有 commit message 的自述(其中 we 的 B1 实为 TODO 化,见 ①b)
  (`da04f41` 列 5 条 / `43400dc` 列 4 条),**无一份「原判定 → 修复落点 → 验证命令」的可证伪报告**
  落 `production/qa/evidence/`。`43400dc` 虽改了 story 件的 AC 勾选,但那是**勾选动作**,
  **不是评审分析**,不构成对账件。
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

后续 gate-check 以本节为索引:**补齐一份逐 BLOCKING 对账件(原判定 → 修复落点 → 验证命令),
并闭合 ①b(接 codec)与 ①c(补负向夹具)后,两 epic 方可转 Complete**。
`world-ecozones/story-003` 唯一未勾的 AC(`entities.yaml` ↔ kindgen 路由一致性,A1–A5)与 ①a 同类,可同批处理。
