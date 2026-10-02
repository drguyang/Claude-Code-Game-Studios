# Sprint 04 Plan

> **Status (2026-10-02 启动)**: **Phase 1 进行中** — 清债务 + 基础设施
> 三专家调研（TD + Producer + QA Lead）综合建议，方案 A 分阶段执行。

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
| 1 | 双评审 modular-building | 🔄 进行中 | 解除 3 个系统阻塞 |
| 2 | 双评审 world-ecozones | ✅ APPROVE | 87/87 测试绿 |
| 3 | 建立集成测试基础设施 | ✅ 完成 | tests/integration/ 目录 |
| 4 | 修复状态文件漂移 | ✅ 完成 | 4 个文件已修复 |
| 5 | 创建 QA 基础设施 | ✅ 完成 | bugs/ + evidence/ |
| 6 | 修复 G2 假绿 | ✅ 完成 | Assert.Ignore |

**Phase 1 产出**：
- modular-building + world-ecozones 正式标记 Complete
- 集成测试基础设施就绪
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

1. 等待 modular-building 双评审结果
2. Phase 1 完成后启动 Phase 2
3. Phase 2 按依赖顺序推进
