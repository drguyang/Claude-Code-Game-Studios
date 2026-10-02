# Sprint 04 Plan

> **Status (2026-10-03)**: **Phase 1 技术侧残余已清零** —— 三条缺口 ①a/①b/①c **全部闭合**,
> 且**另查出并修复了两处更深的缺陷**(见 §Phase 1 产出)。
> **Phase 2 现已解锁**(原门禁「Phase 1 未收口前不启动 Phase 2」已满足)。
> ⚠️ **但两 epic 仍记 `In Review`** —— 残留**不是**技术缺口,而是
> **双代理评审的报告原件从未落盘**(不可追补;义务已立,解除条件 = 各补做一次评审)。
> 详见 `production/epics/index.md` §越序实现登记 与 `.claude/docs/review-workflow.md`。
> 三专家调研(TD + Producer + QA Lead)综合建议,方案 A 分阶段执行。

**Sprint**: 4
**Milestone**: Vertical Slice
**Duration**: 2026-10-03 ~ 2026-10-24（3 周）
**Goal**: 关闭 Milestone 2 垂直切片闭环 — 1 病人 + 1 诊断 + 1 治疗，无美术，验证核心循环
**Capacity**: ~37 SP + 基础设施工作
**Dependency order**: 按「先清债务、再核心循环、后集成验证」排序

---

## 三专家调研结论

| 专家 | 核心建议 |
|------|---------|
| **Technical Director** | 高杠杆解锁：player-controller / persistence / diagnosis / inventory（23 SP） |
| **Producer** | 垂直切片闭环：player-controller / camera / interaction / patient-ai / diagnosis / case-system / prescription（19 story-days） |
| **QA Lead** | 先清债务：双评审在飞 epic + 集成测试基础设施 |

**综合方案 A**：分阶段执行，Phase 1 清债务 → Phase 2 垂直切片核心 → Phase 3 集成验证

---

## Phase 1: 清债务 + 基础设施 —— ✅ **技术侧已收口(2026-10-03)**

| # | 任务 | 状态 | 结果 |
|---|------|------|------|
| 1 | 双评审 modular-building | ✅ **B4 已闭** | 见下 §Phase 1 产出 |
| 2 | 双评审 world-ecozones | ✅ **B1 已闭** | 同上 |
| 3 | 建立集成测试基础设施 | ✅ 完成 | `tests/integration/` 5 目录 |
| 4 | 修复状态文件漂移 | ✅ 完成 | 4 个文件;后续又做了一轮全量回填(见下) |
| 5 | 创建 QA 基础设施 | ✅ 完成 | `bugs/` + `evidence/` |
| 6 | 修复 G2 假绿 | ✅ 完成 | `Assert.Ignore` |

### Phase 1 产出（2026-10-03 刷新）

**① 三条缺口全部闭合**

| 缺口 | 闭合方式 |
|---|---|
| **①a** 无逐 BLOCKING 对账件 | ✅ 三份对账件落 `qa/evidence/`(modular 5 条 · we 4 条 · player-controller 6 story),**逐条独立复核**而非转录 commit message |
| **①b** we B1(`PoiStateChanged` 未走 codec) | ✅ 经 **ADR-029** 裁决路径闭合(`world-ecozones/story-006`) |
| **①c** host gate 无负向夹具 | ✅ 6 例负向夹具 + **突变测试坐实**(删门 ⇒ 恰 4 例红) |

**② 对账轮实测推翻两处「已修」自述** —— 这是 ①a 的直接价值:

- `da04f41` 自述「移除 bit-packing」,**实测 modular B4 是「部分修」**
  (`StructurePlaced` 仍走 bit-packing + 死代码);
- `43400dc` 自述修 B1,**实测 we B1 是「未修」**(仅 TODO 化)。
  ⇒ **若评审原件在库,这两处本可在修复当时即被发现。**

**③ 上溯出共同根因并裁决 → ADR-029(`IPayloadEncoder`,第七抽象点)**

两处缺口**同源**:`Sim` 的写者**没有任何合法的载荷编码路径**
(`PayloadCodec` 住 `Sim.Codec` 够不着、blob 池住 7a/45 也够不着,而 `PayloadRef` 够得着)。
实测后果:**全库每个写者都在手搓 `PayloadRef`,且零字节真的进池**
(连 `craft_event_payload_test.cs:280` 这个「最正确」的写路径也是 `PayloadRef(0,0,len)` 假引用)。
这是 **ADR-006 Amendment G-2 遗留的洞**(只定了载荷**读形**,未定**写形**)。

**ADR-029 实现轮三件全 Complete**:
`persistence-service/story-002`(契约支 14/14)·
`modular-building/story-007`(闭合 B4,12/12)·
`world-ecozones/story-006`(闭合 B1,13/13)。
**`Sim` 引用集一字未改** —— b2 门 / 门 A / ADR-025 §① 全部继续成立。

**④ 收口批:b6 载荷手搓门 + 两处计数订正**

- **b6 门**(`AssemblyGates`):`Sim/` 内 `new PayloadRef(` = 构建失败。
  ⚠️ **启用即查出第三处手搓点** —— `HostEmergencyProcessor.cs:50/59`,
  **比 B1/B4 更严重**:`EmergencyAttemptPayload` 8 字段只写 3 个、
  applied 侧 9 字段只写 1 个有意义且首字段用错。**前两轮评审与对账件均未登记它。**
  ⇒ 已立 `emergency-procedures/story-007`(Ready),语义经 GDD/registry/codec **三方核对**后写明。
- **ADR-005 抽象点计数订正**:实际 **12 处**(非登记的 5 处),按「现行态断言→改 / 历史陈述→加注」二分处理;
- **ADR-010 义务 15**(blob 池写面)已落表;
- **`control-manifest` 版本升 2026-09-21 → 2026-10-02** + §传播范围说明。

**⑤ 治理:立「评审报告原件落盘」BLOCKING 义务**

三处协同(避免孤儿文档):`.claude/docs/review-workflow.md`(完整义务)·
`.claude/docs/coding-standards.md`(CLAUDE.md 已 @ 引用 ⇒ 会话启动即加载)·
`.claude/skills/story-done/SKILL.md`(**收口闸门**)。

**⑥ 全库 manifest 版本回刷(A 案)** —— 207 story 版本号 + 64 处 `Manifest` 行
(7 种旧写法 → 1 条诚实措辞「**仅版本号**」),**未**声称「已复核」。

**⑦ 集成测试基础设施** —— `tests/integration/` 5 目录就绪
⚠️ `unity/Assets/` 之外的目录**不进 Unity 编译**,集成测试真身须落 `unity/Assets/Tests/` 下(本目录为台账)。

---

## Phase 2: 垂直切片核心（~15 天）—— **现已解锁**

| # | 系统 | 故事数 | SP | 依赖 | 理由 |
|---|------|--------|-----|------|------|
| 1 | player-controller (1) | 6 | 6 | 3 (done) | ✅ **6/6 Complete**(Phase 1 期间完成) |
| 2 | camera-viewpoint (2) | 6 | 6 | 1 | 垂直切片可玩性 |
| 3 | interaction-system (4) | 4 | 4 | 1, 3 (done) | 交互是核心循环的动词路由 |
| 4 | patient-ai (13) | 4 | 4 | 9 (done) | 病人是诊断对象 |
| 5 | diagnosis-system (8) | 6 | 6 | 9, 30 (done) | 诊断是核心循环的关键环节 |
| 6 | case-system (37) | 6 | 6 | 8, 9 (done) | 病例是诊断与治疗的容器 |
| 7 | prescription-medication (11) | 5 | 5 | 21, 9 (done) | 处方是治疗手段 |

**Phase 2 总计**：~37 SP（其中 player-controller 6 SP 已在 Phase 1 期间完成）

**关键路径**：
```
player-controller ✅ → camera-viewpoint → interaction-system
                    ↘
patient-ai → diagnosis-system → case-system → prescription-medication
```

---

## Phase 3: 集成验证（~5 天）

| # | 任务 | 状态 | 理由 |
|---|------|------|------|
| 1 | 实现垂直切片 PlayMode 测试（替换 TODO 骨架） | ⬜ **未做**(`vertical_slice_test.cs` 仍有 **14 处 TODO**) | Milestone 2 出口证据 |
| 2 | 至少 1 次文档化 playtest | ⬜ **未做**(`production/qa/` 无 playtest 记录) | Milestone 2 要求 |
| 3 | 运行 ADR-023 场景加载 spikes | ✅ **已完成 2026-09-23**(S1/S3/S4 三条全通过,见 `adr-023` §Validation) | 解锁 chunk 激活 |

---

## 风险登记

| 风险 | 可能性 | 影响 | 缓解 | 现状 |
|------|--------|------|------|------|
| 验证债务累积 | 高 | 中 | Phase 1 双评审 | ✅ 已缓解(三缺口闭合) |
| PlayMode 测试覆盖薄 | 高 | 高 | 建立集成测试基础设施 | ⚠️ **仍在**(14 TODO) |
| ADR-012 矩阵不存在 | 高 | 中 | 激活 CI 矩阵 | ⚠️ **仍在**(需 `UNITY_LICENSE`;也是 sprint-03 AC-S03-5 未兑现的根因) |
| 生产文档状态漂移 | 高 | 中 | 状态文件回填 | ✅ 已缓解(两轮回填) |
| 集成测试门未执行 | 高 | 高 | 建立集成测试基础设施 | ⚠️ **基础设施就绪但未接门** |
| **评审原件缺** | — | 中 | **新立 BLOCKING 义务** | ⚠️ 三 epic 待补做评审 |

---

## Definition of Done — This Sprint

- [x] 所有 BLOCKING AC 有自动化测试覆盖（b6 门已立;两接线支判据经突变测试坐实）
- [x] 所有 Integration story 有集成测试或文档化 playtest（player-controller 005 有 6 例）
- [ ] 垂直切片 PlayMode 测试通过（无 TODO）—— ⬜ **14 处 TODO 仍在**
- [ ] 至少 1 次文档化 playtest —— ⬜ **未做**
- [x] 状态文件准确（sprint/epic/index）—— 两轮回填后
- [x] 零 S1/S2 未关闭 bug（`production/qa/bugs/` 无未关闭项）

---

## 下一步

1. **补做三份评审**(modular / we / player-controller)——
   这是三者转 `Complete` 的**唯一解除条件**,非技术缺口;
   义务与措辞见 `.claude/docs/review-workflow.md`
2. **`emergency-procedures/story-007`**(b6 门查出的手搓点)——
   首步须确认三处输入面(`DrugPotency` 求值点 · `method`/`cause` 来源 · `Seq` 发号点),
   **可能需用户裁定**
3. **Phase 2 开工**(现已解锁)—— 按依赖顺序:camera-viewpoint → interaction-system → …
4. **Phase 3 两项未做**(垂直切片 PlayMode 测试 · playtest)—— Milestone 2 出口所需
5. **抽象点计数订正(60 处)** —— 牵动 GDD/registry/architecture **权威件**,须先定归属方
