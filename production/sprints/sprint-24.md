# Sprint 24 — 2026-11-03 to 2026-11-09

> **Status**: Planned（2026-11-03 · 用户指令「开 sprint24」）
> **件集裁定**:用户 2026-11-03 选「2 件:炮制尾 + 7a 在制品」—— **不提前拉后续 sprint 的活**
> **前置**:`sprint-23` 已收口（2026-11-01 · PASS，见 `sprint-23.md` §七；收口后追加件见 §八）

## ⚠️ Phase 1.5 复核：路线图的 sprint-24 行**已过期**（原文不删，实况为准）

`production/milestones/p0-remaining-roadmap-2026-10-20.md:950` 原写：

> `| **sprint-24** | 炮制链尾 + 采集尾 | `processing-002` · `processing-004` · `processing-005` · `foraging-002` | sprint-21 | `processing-002` 的 42 走查半边仍挂 |`

**逐件实跑卡面 `Status:`（2026-11-03）**：

| 计划件 | 实际状态 | 判定 |
|---|---|---|
| `processing-002` | **Complete** | 已交付，**不是** sprint-24 的活 |
| `processing-004` | **Complete** | 同上 |
| `processing-005` | Ready | **唯一真在** |
| `foraging-002` | **Complete** | 同上 |

⇒ **4 件里 3 件已先期完成**，计划内容实际只剩 **1 件**。路线图该行与 `processing/EPIC.md`
的 Stories 表（把 001–004 全标 `Ready`，而卡面全是 `Complete`）**同为过期源**；以**卡面**为准。

这正是 `sprint-plan` Phase 1.5 点名的失效形态（先例：sprint-11 的 S11-4 / S11-5 两件都写在过期前提上）。

---

## Sprint Goal
清掉 18 炮制链的最后一卡，并结清挂了 9 天的 7a 在制品（QA 假绿退回件）。

## Capacity（按 `production/CAPACITY-MODEL.md` §3.2，**不估天数**）

| 元 | 值 |
|---|---|
| 计划件 | **2**（Must 2 · Should 0 · Nice 0） |
| 复杂度分布 | **M 2** · L 0 · S 0 |
| 并发上限 P | **3** ⇒ 本轮 2 件可并行 |
| 追加额度（§3.3） | 散件 **1** + 链条 **1**（链条入场须显式裁定四样） |
| 判别力门槛 D | 每件须有「改坏生产 ⇒ 夹具转红」的**可证伪判据**，否则不计 done |

> ⚠️ **不填 Nice 是刻意的**：承 §3.2「L 件单独占批」与 §六 条件 #3（禁用人日线性外推反过来凑件数）。

## Tasks

### Must Have

| ID | 任务 | Type | 复杂度 | 掀债 | 掀 story | 链 | 验收判据 |
|---|---|---|---|---|---|---|---|
| **S24-1** | `processing-005` 完成调度 · 溢出落地 · 等待期音频护栏（`design/gdd/processing.md` AC-18-21 / 18-09 / 18-22 / 18-24 / 18-08） | Integration | **M** | 0 | 0 | **炮制链**（尾件） | AC-18-21 [I] 可判；四条 `[L]`（听测 3 + 手柄走查 1）**须人听/人走** ⇒ 不勾即 `NOT-RUN`，**不得借绿** |
| **S24-2** | `persistence-service-001` 7a 存储抽象 —— **7 条 AC 逐条重判** | Logic | **M** | 0 | 0 | —（散件） | 7 条 AC 各有载体；`Type: Logic` ⇒ 自动化证据是 **BLOCKING** 门 |

### Should Have
无（用户裁定只填 2 件）。

### Nice to Have
无。

## Carryover from Previous Sprint

| 任务 | 原因 | 备注 |
|---|---|---|
| `persistence-service-001` | **2026-10-25 QA 轮退回** —— 此前标 `Complete` 但 7 条 AC 全未勾（`[x]`=0 / `[ ]`=7），qa-lead 判为**第五个假绿形态**：用「文件已落盘」顶替「AC 已判定」 | 已挂 **9 天**未动；本 sprint 必须**收口或明确退回**，不得继续悬空 |
| `processing-005` | 路线图排在本 sprint，但同行的 3 件已先期完成 | 本件**从未开工**（Ready） |

## Risks

| 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|
| **S24-1 五条 AC 里 5 条全需人**（3 听测 + 1 手柄走查 + 1 逐屏零数字走查） | 高 | 中 | 注定 `COMPLETE WITH NOTES`；`[L]` 半随如实标 `NOT-RUN`，**不得记 ✅** |
| **S24-2 重判可能再掀出欠账** —— 它本身就是「假绿退回」的样本 | 中 | 高 | 逐条 AC 给**载体**（测试名 / 证据件 / `file:line`），不按「文件已落盘」记绿 |
| 当前工作树有 **S23-39 的 7 个文件未提交**（C10 收紧轮） | 确定 | 低 | **开工前先落定**，避免与 S24 改动混批 |
| `origin/main` 落后 **32 笔**，CI job 从未在 GitHub Actions 上执行过 | 确定 | 中 | 与 `TD-ci-01`（`unity-tests.yml`，secret 门控）**无关**；本门在 `tests.yml` 不依赖 secret，推送即生效 |

## Dependencies on External Factors

- **S24-1** 的 AC-18-09 / 18-22 / 18-24 / 18-08 = **听测与手柄/逐屏走查**（人，不可自动化）
- **S24-2** 的 AC-7a-03 黄金字节对拍依赖 **ADR-012 三格矩阵**（外部 CI 面）

## Definition of Done for this Sprint

- [ ] Must 2 件完成（`[L]` 半随如实标 `NOT-RUN`，**不借绿**）
- [ ] QA 计划存在（**见下方阻塞**）
- [ ] 证伪轮：每件至少一条「改坏生产 ⇒ 夹具转红」
- [ ] 回归三元组（EditMode / PlayMode 读数 + 截至时刻 + commit）
- [ ] `python3 tools/debtcount/debtcount.py --check` ⇒ exit 0
- [ ] `python3 tools/acid/acid_check.py --check` ⇒ exit 0
- [ ] 收口判定与收口提交同日

---

> ⚠️ **No QA Plan**：本 sprint 开工时**没有** QA 计划（`production/qa/` 下无 `qa-plan-sprint-24*`）。
> 先跑 `/qa-plan sprint` 再开工。Production → Polish 门要求 QA 签核报告，而签核报告需要 QA 计划。

---

## 复算（自包含）

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# ① 路线图该行的陈腐性
grep -m1 '^> \*\*Status\*\*' production/epics/processing/story-002-*.md   # Complete
grep -m1 '^> \*\*Status\*\*' production/epics/processing/story-004-*.md   # Complete
grep -m1 '^> \*\*Status\*\*' production/epics/foraging/story-002-*.md     # Complete
grep -m1 '^> \*\*Status\*\*' production/epics/processing/story-005-*.md   # Ready  ← 唯一真在

# ② 在制品
grep -m1 '^> \*\*Status\*\*' production/epics/persistence-service/story-001-*.md  # In Progress

# ③ 门
python3 tools/debtcount/debtcount.py --check; echo "DEBT=$?"
```
