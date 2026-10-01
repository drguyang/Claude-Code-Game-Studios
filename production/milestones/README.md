# Milestones

**Project**: 《大医精诚:破晓之剂》
**Target**: Production

---

## Milestone 1: Pre-Production Complete

**Goal**: All architecture decisions finalized, production management artifacts in place, first sprint ready to start.

**Exit Criteria**:
- [x] `production/epics/index.md` accurate (all paths, all statuses) — committed 0e553e1
- [x] All P0 systems have epic directories
- [x] Sprint 1 plan defined in `production/sprints/sprint-01.md` — committed 1d14107
- [x] CI EditMode baseline green (921 passed, 0 failed) — 2026-09-29 desktop verified
- [x] `UNITY_LICENSE` secret configured — 改用服务账号授权（`UNITY_CLIENT_ID` / `UNITY_CLIENT_SECRET`），CI workflow 已生成（`.github/workflows/unity-tests.yml`）
- [x] OQ-1-12 (接地模型) decision recorded — `player-controller-and-movement.md`
- [x] OQ-10-12 (两动作原型) decision recorded — `emergency-procedures.md`
- [ ] Performance budgets finalized (Draw Calls, Memory Ceiling) — pending target hardware selection (BLOCKED-BY: hardware decision, not an ADR issue)
- [ ] Spike results recorded: ADR-023 S2/S5/S6/S7, ADR-013 assumption 6 — P1 deferred per user

**ETA**: TBD

---

## Milestone 2: Vertical Slice

**Goal**: Playable vertical slice demonstrating the full core loop.

**Exit Criteria**:
- [ ] Core gameplay loop playable end-to-end
- [ ] At least one complete [start → challenge → resolution] cycle
- [ ] Vertical slice playtested with ≥1 documented session
- [ ] Playtest report at `production/playtests/`

**ETA**: TBD

---

## Milestone 3: Production

**Goal**: Full feature development begins.

**Exit Criteria**:
- [ ] All Foundation + Core layer ADRs Accepted
- [ ] All P0 stories decomposed
- [ ] Sprint 1 in progress
- [ ] All blocking concerns from Pre-Production gate resolved

**ETA**: TBD
