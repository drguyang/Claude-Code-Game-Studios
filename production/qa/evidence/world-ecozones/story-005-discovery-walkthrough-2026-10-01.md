# World-Ecozones Story 005: 发现体验走查记录

**Date**: 2026-10-01
**Story**: Story 005 — 发现体验走查与两层世界一致性([L] / EXTERNAL)
**Status**: Pending Desktop Verification

## AC-6-24 [L] 发现体验走查

### Checklist
- [ ] 玩家走近 POI 时「被发现」有可感反馈(经 42 拟物通道,非弹字)
- [ ] 跳过 Discovered 直达 Resolved 的 POI 不产生半状态表现
- [ ] 走查记录 SIGN-OFF

### Notes
This is a [L] Visual-Feel story requiring manual verification on desktop Unity Editor.

## AC-6-25 两层一致性抽样

### Sampling Grid
| Ecozone | Sample 1 | Sample 2 | Sample 3 | Status |
|---------|----------|----------|----------|--------|
| 草甸    | (4, 0, 4) | (7, 0, 2) | (2, 0, 8) | Pending |
| 湿地    | (12, 0, 5) | (15, 0, 3) | (10, 0, 7) | Pending |
| 密林    | (22, 0, 4) | (25, 0, 6) | (20, 0, 2) | Pending |
| 岩地    | (32, 0, 5) | (35, 0, 3) | (30, 0, 7) | Pending |

### Check Items
- [ ] Logic layer walkable matches visual terrain readability
- [ ] K_speed colors/indicators consistent with visual slope
- [ ] POI placement visible and reachable in visual layer
- [ ] No "floating" POIs or terrain discontinuities

## AC-6-26 EXTERNAL 工具 CI

### C1–C6 Status
| Check | Description | CI Status |
|-------|-------------|-----------|
| C1 | Two-layer drift (warning) | Not-RUN |
| C2 | Walkability consistency (hard fail) | Not-RUN |
| C3 | NavMesh ↔ NavGrid consistency | Not-RUN |
| C4 | Polygon validity (hard fail) | Not-RUN |
| C5 | Boundary single-source (hard fail) | Not-RUN |
| C6 | Slice completeness (hard fail) | Not-RUN |

### Notes
Tool CI is EXTERNAL to sim assembly (ADR-022). Verification deferred to desktop session.

## P0 范围守门(走查项 · 原误标 `AC-6-27`)

> ⚠️ **2026-10-03 订正(评审 N6 附带项)**:本二节原标 `AC-6-27` / `AC-6-28`,
> 但该二编号**已由 `story-006`(POI 载荷接线 · ADR-029 支)占用** ——
> 走查件与之冲突。**本件不占用 AC 编号**,改用描述性标题;
> 本项的判据归属须待 story-005 的 AC 编号最终定稿时回填。

### Scope Verification
- [ ] Active scene = 医馆 + 1 small scene
- [ ] No open-world content beyond P0 scope
- [ ] Scope debt explicitly logged (if any)

## 帧率无关激活体验(走查项 · 原误标 `AC-6-28`)

### Verification Method
- Scripted playback at 60fps and 144fps
- Fast traversal across chunk boundaries
- Visual check: no "world flash" > 1 tick

### Notes
Hand-feel item — requires desktop Unity Editor session.

---
**Next Steps**: Desktop Unity verification required for all [L] and EXTERNAL items.
