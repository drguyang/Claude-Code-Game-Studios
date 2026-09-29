# Sprint 01 Plan

**Sprint**: 1
**Milestone**: Pre-Production Complete
**Duration**: 2026-10-05 ~ 2026-10-18（2 周）
**Goal**: 建立 Foundation + Core 基础设施骨架，为后续 sprint 消除互依赖阻塞
**Capacity**: ~8 story points（solo dev，故事均为契约/抽象/接口级，无 gameplay 实现）
**Dependency order**: 按「先数据、再 sim、再表现」排序；story 间无硬依赖，可并行

---

## Stories

| # | Epic | Story | Story Points | Depends On | Status |
|---|------|-------|:------------:|------------|--------|
| 1 | item-database | Story 003 — Recipe settlement solver（schema + 守恒硬门） | 1 | — | Ready |
| 2 | skill-system | Story 001 — Skill registry and definitions（抽象接口 + 19 技能注册表） | 1 | — | Ready |
| 3 | input-system | Story 001 — Action asset identity（Input System 动作资产契约） | 1 | — | Ready |
| 4 | persistence-service | Story 001 — Storage abstraction（二进制 codec 接口 + 校验骨架） | 2 | — | Ready |
| 5 | player-controller | Story 001 — Controller foundation（CharacterController 参数 + `ITickProvider` 接入） | 1 | 4 | Ready |
| 6 | camera-viewpoint | Story 002 — Yaw basis hard delivery（`ICameraRig.YawBasis` + 半隐式积分器） | 1 | 5 | Ready |
| 7 | disease-simulation | Story 001 — Fixed-point math and hashing（`Fix` Q16.16 + `SplitMix64` + 单元级黄金哈希） | 1 | — | Ready |
| 8 | telemetry-analytics | Story 001 — Readonly boundary, zero egress（51 接口层 + 构建期断言零网络） | 1 | — | Ready |

**Depends On 列 = 本 sprint 内依赖**（跨 sprint 依赖由 epic 的 GDD 自行登记，不在此表）。

---

## Dependency Graph（sprint 内）

```
无依赖层（并行）:
  [1] item-database schema
  [2] skill-system registry
  [3] input-system contracts
  [7] disease-sim Fix math
  [8] telemetry boundary

第二层（等无依赖层完成）:
  [4] persistence service ← 无硬依赖，但与 [7] 共享 Fix 类型约定

第三层（等 [4]+[5]）:
  [5] player-controller ← 等 [4]（codec 接口定稿）
  [6] camera-viewpoint   ← 等 [5]（1 的 `YawBasis` 交付）
```

**实际执行**: [1][2][3][7][8] 第一周并行；[4] 第二周前半；[5][6] 第二周后半。

---

## Acceptance Criteria（sprint 级）

| AC | 判据 |
|----|------|
| AC-S01-1 | 8 个 story 的单元测试全部通过（EditMode） |
| AC-S01-2 | [1] `ItemSchemaValidator` 能对合法 JSON 通过、对非法 JSON 构建期 throw |
| AC-S01-3 | [2] `ISkillStore` 接口可存/取 19 项技能等级，`SkillGrown` Kind 已登记 `entities.yaml` |
| AC-S01-4 | [3] 至少一个 Input Action asset 的 bindings 可被代码读取，绑定 GUID 稳定 |
| AC-S01-5 | [4] `ISaveCodec.Write` + `Read` 对空记录往返 = 同一字节序列（EditMode golden 哈希） |
| AC-S01-6 | [5] `PlayerController` 持有 `ITickProvider` 引用，`CharacterController` 参数已配置 |
| AC-S01-7 | [6] `ICameraRig.YawBasis` 返回 `float` 偏航角，半隐式积分器子步数 = 3 |
| AC-S01-8 | [7] `Fix.FromRaw(0x10000)` == `1.0`（Q16.16 单位值），`SplitMix64` 已知向量对拍通过 |
| AC-S01-9 | [8] 构建期扫描 `src/` 零 `Upload`/`Send`/`Post` 调用点（断言门 A） |

---

## Out of Scope（不进入本 sprint）

| Story | 原因 |
|-------|------|
| 42 元件库（Story 001） | 7 屏 UX spec 未齐，42 实现期再启动 |
| 44 混音拓扑（Story 001–003） | 音频 Event Table 数据未定，归数据轮 |
| 9 F1 进度求值（Story 004） | 依赖 [7] Fix math 完成 |
| 10 急救动作（Story 001–006） | 依赖 [3][5][9] 全部就位 |
| 25 战斗/武器（Story 001–006） | 依赖 [9][20] 先完成 |
| 任何 P1a story | 不在 P0 范围内 |

---

## Risk & Notes

- **Solo dev capacity**: 2 周 8 SP = 平均 0.6 SP/天，留 40% 缓冲给 spike 和调试
- **Blocked by**: 无 — 8 个 story 全部可独立开工
- **Spike 预留**: [7] Fix math 可能有 IL2CPP 溢出行为待确认（ADR-012 F7 已降级，但单元级实现仍需一轮）
- **Milestone 1 对齐**: 本 sprint 完成即满足 Milestone 1 出口 #3（Sprint 1 plan defined）和 #4（CI EditMode baseline green 的前提）
