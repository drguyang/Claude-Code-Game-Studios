# Sprint 04 Plan

> **Status (2026-10-02)**: **Phase 1 收尾中** — 4/6 任务已完成;剩下 modular-building 与
> world-ecozones 双评审均为「REQUEST_CHANGES → BLOCKING 已修 → 逐例复跑绿」,
> 但**评审报告件未落 `production/qa/evidence/`** ⇒ 依「不得借绿」维持 In Review 不转 Complete。
> **Phase 2 未启动**(`player-controller` 2/6 为 2026-10-01 桌面批越序先行的在建状态,非 Phase 2 开工)。
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

## Phase 1: 清债务 + 基础设施（~5 天）

| # | 任务 | 状态 | 理由 |
|---|------|------|------|
| 1 | 双评审 modular-building | 🔶 REQUEST_CHANGES→5B 已修(51/51 逐例复跑绿)· **评审报告件缺失** | 解除 3 个系统阻塞 |
| 2 | 双评审 world-ecozones | 🔶 REQUEST_CHANGES→4B 已修(87/87 逐例复跑绿)· **评审报告件缺失** | 87/87 测试绿 |
| 3 | 建立集成测试基础设施 | ✅ 完成 | tests/integration/ 目录 |
| 4 | 修复状态文件漂移 | ✅ 完成 | 4 个文件已修复 |
| 5 | 创建 QA 基础设施 | ✅ 完成 | bugs/ + evidence/ |
| 6 | 修复 G2 假绿 | ✅ 完成 | Assert.Ignore |

**Phase 1 产出**：
- modular-building + world-ecozones 双评审 BLOCKING 全修 + 测试全绿(51/51 · 87/87,
  2026-10-02【超算】batchmode **逐例复跑**坐实,证据
  `production/qa/evidence/editmode-full-rerun-2026-10-02.md`),
  但仍 **In Review 未转 Complete** —— 残留硬缺口:**评审报告原文未落 `production/qa/evidence/`**
  (`da04f41` / `43400dc` 的 `git show --stat | grep -c production/` = 0,
  「BLOCKING 已修」目前只有 commit message 自述,无可证伪对账件)。见
  `production/epics/index.md` §越序实现登记的纪律说明。
  ⚠️ **漂移登记(2026-10-02【超算】)**:本批 `da177e6` 曾把二者在 `index.md` 直改为
  `Complete ✅ 2026-10-02`,而三处 EPIC/story 行未同步 ⇒ 已回退为 `In Review`;
  本批 Phase 1 表原写「world-ecozones 双评 ✅ APPROVE」与 `bb477e6` 的
  REQUEST_CHANGES(4 BLOCKING)不符,已订正为「REQUEST_CHANGES→4B 已修」。
- 集成测试基础设施就绪(`tests/integration/` 5 目录;须注意 `unity/Assets/` 之外的目录
  **不进 Unity 编译**,集成测试真身须落在 `unity/Assets/Tests/` 下,本目录为台账)
- 状态文件可信
- QA 基础设施就绪

---

## Phase 2: 垂直切片核心（~15 天）

| # | 系统 | 故事数 | SP | 依赖 | 理由 |
|---|------|--------|-----|------|------|
| 1 | player-controller (1) | 6 | 6 | 3 (done) | 解锁 4 个系统；垂直切片可玩性 |
| 2 | camera-viewpoint (2) | 6 | 6 | 1 | 垂直切片可玩性 |
| 3 | interaction-system (4) | 4 | 4 | 1, 3 (done) | 交互是核心循环的动词路由 |
| 4 | patient-ai (13) | 4 | 4 | 9 (done) | 病人是诊断对象 |
| 5 | diagnosis-system (8) | 6 | 6 | 9, 30 (done) | 诊断是核心循环的关键环节 |
| 6 | case-system (37) | 6 | 6 | 8, 9 (done) | 病例是诊断与治疗的容器 |
| 7 | prescription-medication (11) | 5 | 5 | 21, 9 (done) | 处方是治疗手段 |

**Phase 2 总计**：~37 SP

**关键路径**：
```
player-controller → camera-viewpoint → interaction-system
                 ↘
patient-ai → diagnosis-system → case-system → prescription-medication
```

---

## Phase 3: 集成验证（~5 天）

| # | 任务 | 理由 |
|---|------|------|
| 1 | 实现垂直切片 PlayMode 测试（替换 TODO 骨架） | Milestone 2 出口证据 |
| 2 | 至少 1 次文档化 playtest | Milestone 2 要求 |
| 3 | 运行 ADR-023 场景加载 spikes | 解锁 chunk 激活 |

---

## 风险登记（新增）

| 风险 | 可能性 | 影响 | 缓解 |
|------|--------|------|------|
| 验证债务累积 | 高 | 中 | Phase 1 双评审 |
| PlayMode 测试覆盖薄 | 高 | 高 | 建立集成测试基础设施 |
| ADR-012 矩阵不存在 | 高 | 中 | 激活 CI 矩阵 |
| 生产文档状态漂移 | 高 | 中 | 已修复 4 个文件 |
| 集成测试门未执行 | 高 | 高 | 建立集成测试基础设施 |

---

## Definition of Done — This Sprint

- [ ] 所有 BLOCKING AC 有自动化测试覆盖
- [ ] 所有 Integration story 有集成测试或文档化 playtest
- [ ] 垂直切片 PlayMode 测试通过（无 TODO）
- [ ] 至少 1 次文档化 playtest
- [ ] 状态文件准确（sprint/epic/index）
- [ ] 零 S1/S2 未关闭 bug

---

## 下一步

1. **Phase 1 残余**:两份评审报告件未落 evidence —— 补写「逐 BLOCKING:原判定 → 修复落点 →
   验证命令」对账件(索引见 `production/epics/index.md` §越序实现登记的纪律说明),
   落件后 modular-building / world-ecozones 方可转 Complete。**其余任务先不进行**。
2. Phase 1 全部收口后启动 Phase 2
3. Phase 2 按依赖顺序推进
