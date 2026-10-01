# Sprint 02 Plan

> **Status (2026-10-01 停刷)**: **16/16 Complete** ✅ — EditMode 全绿(【超算】batchmode:
> DiseaseSimulation 55 / TimeWeather 32 / RandomEvents 53),提行见
> `production/epics/{disease-simulation,time-weather,random-events}/EPIC.md`。
> AC-S02-1 的「EditMode 全通过」已由本轮验证;AC-S02-5 的**跨平台对拍面**(IL2CPP/ARM64)
> 仍归 CI / 桌面批,`[L]` 项不适用本 sprint。

**Sprint**: 2
**Milestone**: Sim Core Complete
**Duration**: 2026-10-19 ~ 2026-11-01（2 周）
**Goal**: 完成 sim 层核心 — 疾病模拟、时间天气、随机事件
**Capacity**: ~16 story points
**Dependency order**: 按「先数据、再 sim、再表现」排序

---

## Stories

| # | Epic | Story | Story Points | Depends On | Status |
|---|------|-------|:------------:|------------|--------|
| 1 | disease-simulation | Story 002 — 抽象点、SimEvent 与病史流机制 | 2 | — | Complete ✅ 2026-10-01 |
| 2 | disease-simulation | Story 003 — 注册表 schema、烘焙管线与门 A 护栏 | 2 | 1 | Complete ✅ 2026-10-01 |
| 3 | disease-simulation | Story 004 — F1 病程求值与 F2 体征投影 | 2 | 2 | Complete ✅ 2026-10-01 |
| 4 | disease-simulation | Story 005 — F4 九态阈值机与照护杠杆 | 2 | 3 | Complete ✅ 2026-10-01 |
| 5 | disease-simulation | Story 006 — F3 CatchUp、F5 共病合成与跨平台黄金夹具 | 2 | 4 | Complete ✅ 2026-10-01 |
| 6 | time-weather | Story 001 — Tick provider 与时间推进 | 1 | — | Complete ✅ 2026-10-01 |
| 7 | time-weather | Story 002 — 天气纯函数与季节调制 | 1 | 6 | Complete ✅ 2026-10-01 |
| 8 | time-weather | Story 003 — 生态区查询与 cell 归属 | 1 | 7 | Complete ✅ 2026-10-01 |
| 9 | time-weather | Story 004 — 天气影响与事件调制 | 1 | 8 | Complete ✅ 2026-10-01 |
| 10 | time-weather | Story 005 — 跨平台黄金夹具与确定性验证 | 1 | 9 | Complete ✅ 2026-10-01 |
| 11 | random-events | Story 001 — 事件导演与抽池机制 | 2 | — | Complete ✅ 2026-10-01 |
| 12 | random-events | Story 002 — 脚本链与三案触发 | 2 | 11 | Complete ✅ 2026-10-01 |
| 13 | random-events | Story 003 — 世界事件与 POI 触发 | 2 | 12 | Complete ✅ 2026-10-01 |
| 14 | random-events | Story 004 — 事件调制与季节权重 | 1 | 13 | Complete ✅ 2026-10-01 |
| 15 | random-events | Story 005 — 事件与模拟层接口 | 2 | 14 | Complete ✅ 2026-10-01 |
| 16 | random-events | Story 006 — 跨平台黄金夹具与确定性验证 | 1 | 15 | Complete ✅ 2026-10-01 |

---

## Dependency Graph（sprint 内）

```
无依赖层（并行）:
  [1] disease-simulation #002
  [6] time-weather #001
  [11] random-events #001

第二层:
  [2] disease-simulation #003 ← [1]
  [7] time-weather #002 ← [6]
  [12] random-events #002 ← [11]

第三层:
  [3] disease-simulation #004 ← [2]
  [8] time-weather #003 ← [7]
  [13] random-events #003 ← [12]

第四层:
  [4] disease-simulation #005 ← [3]
  [9] time-weather #004 ← [8]
  [14] random-events #004 ← [13]

第五层:
  [5] disease-simulation #006 ← [4]
  [10] time-weather #005 ← [9]
  [15] random-events #005 ← [14]

第六层:
  [16] random-events #006 ← [15]
```

---

## Acceptance Criteria（sprint 级）

| AC | 判据 |
|----|------|
| AC-S02-1 | 16 个 story 的单元测试全部通过（EditMode） |
| AC-S02-2 | disease-simulation 五抽象点 + SimEvent + 病史流完整 |
| AC-S02-3 | time-weather 天气纯函数 + 生态区查询 + 季节调制 |
| AC-S02-4 | random-events 事件导演 + 脚本链 + 世界事件 |
| AC-S02-5 | 跨平台黄金夹具入库（disease-sim + time-weather + random-events） |

---

## Out of Scope（不进入本 sprint）

| Story | 原因 |
|-------|------|
| 45 联机实现 | P1b，非 sim 层 |
| 七屏走查 | 依赖 45 联机夹具 |
| 手柄走查 | P1a |
| 任何 P1a story | 不在 P0 范围内 |

---

## Risk & Notes

- **disease-simulation** 是 sim 层核心，F1-F5 公式必须严格按 GDD 实现
- **time-weather** 的 `ITickProvider` 是全案 tick 唯一来源
- **random-events** 的事件导演必须保证确定性（WorldSeed 派生）
- 三个 epic 的跨平台黄金夹具是 ADR-012 CI 门的对拍面
