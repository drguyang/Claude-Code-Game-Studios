# Sprint 24 — 2026-11-03 to 2026-11-09

> **Status**: **Closed**（2026-11-03 · 2 件 Must 全交付，均 COMPLETE WITH NOTES —— 见文末「收口」节）
> （原文：`> **Status**: Planned（2026-11-03 · 用户指令「开 sprint24」）`，保留备查）
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
| 🔁 **上一行的「推送即生效」已被实测推翻（2026-11-03 收口后核查）** | 确定 | 中 | 见 §收口 —— GitHub API 实测：**该仓自 2026-10-10 起 `total_count = 0` 次工作流运行**，本 sprint 三笔推送（`c6130cde` / `88ad7ae7` / `661d4883`）的 `check-runs` 均为 **0**。两个 workflow 的 `state` 都是 `active`、YAML 可解析、`on.push.branches=[main]` 正确 ⇒ **不是「只在 push 时才跑」的问题，是根本没跑**。根因未定（`/actions/permissions` 需鉴权，本机 `gh` token 已失效） |

## Dependencies on External Factors

- **S24-1** 的 AC-18-09 / 18-22 / 18-24 / 18-08 = **听测与手柄/逐屏走查**（人，不可自动化）
- **S24-2** 的 AC-7a-03 黄金字节对拍依赖 **ADR-012 三格矩阵**（外部 CI 面）

## Definition of Done for this Sprint

- [x] Must 2 件完成（`[L]` 半随如实标 `NOT-RUN`，**不借绿**）—— S24-1 / S24-2 均 **COMPLETE WITH NOTES**
- [x] QA 计划存在 —— `production/qa/qa-plan-sprint-24-2026-11-03.md`
- [x] 证伪轮：每件至少一条「改坏生产 ⇒ 夹具转红」—— S24-1 **6/6** · S24-2 **3/3**（后追加 `TD-processing-08` **6/6**）
- [x] 回归三元组 —— EditMode `6638/6601/0/37` · PlayMode `324/320/0/2+2` @ 2026-11-03 · `c9284b23`+工作树
- [x] `python3 tools/debtcount/debtcount.py --check` ⇒ exit 0
- [x] `python3 tools/acid/acid_check.py --check` ⇒ exit 0
- [x] 收口判定与收口提交同日 —— 2026-11-03（`c6130cde`）
- [ ] ⚠️ **`/smoke-check sprint` 未跑** —— 需真启动序走查（主菜单 → 炮制全链 → 存档往返）；
      本轮只做测试树回归。**按此条 DoD 未完，不得称「全绿收口」。**

---

> ~~⚠️ **No QA Plan**：本 sprint 开工时**没有** QA 计划（`production/qa/` 下无 `qa-plan-sprint-24*`）。
> 先跑 `/qa-plan sprint` 再开工。Production → Polish 门要求 QA 签核报告，而签核报告需要 QA 计划。~~
> 🔁 **2026-11-03 已解除**：`production/qa/qa-plan-sprint-24-2026-11-03.md` 已落盘（开工前）。原文不删。

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

---

## 收口（2026-11-03）

> **Status**: **Closed** —— 2 件 Must 全交付，均为 **COMPLETE WITH NOTES**（无 BLOCKING 缺口）。

### 逐件判定

| ID | Story | Verdict | 判据 |
|---|---|---|---|
| S24-1 | `processing-005` | **COMPLETE WITH NOTES** | `AC-18-21` [I] PASS（18 侧三输出）；四条 `[L]` **NOT-RUN**（人听 / 人走） |
| S24-2 | `persistence-service-001` | **COMPLETE WITH NOTES** | 7 条 AC：PASS **5**（含 `AC-7a-18` 加宽）· NOT-RUN **2**（前件不存在 / EXTERNAL） |

### 证伪轮（两件，9 变异全部命中）

| 件 | 变异 | 命中 |
|---|---|---|
| S24-1 | M1–M6（等号判据 / 每 actor 去重 / 前缀口径 / tick 单调 / cue 未解析 / 墙钟符号） | **6/6** |
| S24-2 | P1–P3（事件头丢 `Seq` / 回退 `BitConverter` / 非 0 `SnapshotOffset`） | **3/3** |

还原判据 = **SHA-256 逐字节**（改前 = 还原后），原件落 `production/qa/evidence/`：
`impl-processing-005-completion-schedule-2026-11-03.md` ·
`review-persistence-001-ac-rejudge-2026-11-03.md`。

### 回归三元组

| 项 | 读数 |
|---|---|
| EditMode | **`6630 / 6593 / 0 / 37`**（total / passed / **failed** / skipped） |
| PlayMode | **`324 / 320 / 0 / 2 skipped + 2 inconclusive`**（两条 inconclusive = 既有的 `U1Assump6` 尖刺 `*_notRun`，非失败） |
| 截至时刻 | `2026-11-03 01:08–01:09 CST` |
| 批次 | sprint-24 收口轮 |
| commit | `c9284b23` + 工作树 |

> ⚠️ **本轮未另跑「改动前」的全量基线** ⇒ **不写 `+N = +N` 等式**（那会变成借绿）。
> 能证的只有「**0 失败**」这一条；新增测试数可复算：
> `grep -c "\[Test\]" unity/Assets/Tests/PlayMode/process_completion_and_spill_test.cs`（13）·
> `grep -c "public void test_ac7a0" unity/Assets/Tests/EditMode/PersistenceService/save_service_test.cs`（6）·
> `grep -c "public void test_ac7a01_" unity/Assets/Tests/EditMode/PersistenceService/save_replay_roundtrip_test.cs`（1）。

### 门

| 门 | 结果 |
|---|---|
| `python3 tools/debtcount/debtcount.py --check` | **exit 0**（条目 154 · 未闭 P0 面 0） |
| `python3 tools/acid/acid_check.py --check` | **exit 0** |
| `/smoke-check sprint` | **未跑** —— 需真启动序走查（主菜单 → 炮制全链 → 存档往返）；本轮只做测试树回归，**不声称 smoke 已过** |

### 本 sprint 掀出的新债

| 债 | 描述 | P0 面 |
|---|---|---|
| **`TD-processing-08`** | 20 侧缺「完成时点的结算入口」⇒ `Craft` 的产出在**点火那刻**就入包（`InventoryFold.ApplyCraftOutputs`），完成 tick 与容量裁决无接线 ⇒ 起货溢出路径**不可达** | **N**（目标行为未接线，非错误行为） |

### ⚠️ CI 核查（2026-11-03 推送后 · 如实登记）

| 事实 | 证据 |
|---|---|
| 该仓最近一次工作流运行 = **2026-10-09** | `GET /repos/.../actions/runs?per_page=20` 逐条 `created_at` |
| **2026-10-10 起运行数 = 0** | `GET /actions/runs?created=>=2026-10-10` ⇒ `total_count: 0` |
| 本 sprint 三笔提交各 **0 次运行** | `GET /actions/runs?head_sha=<sha>` ⇒ `0` × 3 |
| 本 sprint HEAD `c6130cde` 的 **`check-runs = 0`** | `GET /commits/c6130cde/check-runs` ⇒ `0` |
| 两个 workflow `state = active` | `GET /actions/workflows` ⇒ `Lint and Guard Checks` / `Unity Tests` 均 `active` |
| 触发配置正确、YAML 可解析 | `.github/workflows/{tests,unity-tests}.yml` 均 `on.push.branches=[main]`，PyYAML 解析 OK |

⇒ **结论：CI 根本没有在跑**，不是「我的提交没触发」。**已立债 `TD-ci-03`（P0 面 `N`，用户裁定退 P2 实现）** ——
它与 `TD-ci-01`（workflow 内部 6 条结构缺陷 + 凭证）**不同因**：本债是「连 run 都没创建」，
`TD-ci-01` 是「跑起来了也全 skip」；修后者**不会**产生运行。两个 workflow 文件自 2026-10-09 后
还被改过 6 次（含 `1fb81d00` AC 撞号门、`605961b1` 变异矩阵 job），**这些改动从未在 CI 上被执行过**。
根因**未定**：`/actions/permissions` 需鉴权（`gh` token 已失效，`gh run list` 报 401），
本机无法从 API 进一步定位（候选：账户级 Actions 被停 / 计费 / 组织策略）。
⇒ **本 sprint 不得在任何地方声称「CI 绿」**；`tests.yml` 的 `ac-id-collision` job 至今**零次执行**。

### 如实登记的三处「没做完」

1. **S24-1 的起货音**：事件表 19 行 cue 里没有 `sfx_process_complete` ⇒ 驱动装配期解析失败 ⇒
   `OnTickEdge` 首行返回，**生产上不出声**。交付的是**发射时机**，不是音效。
2. ~~**S24-1 的溢出落地**：见 `TD-processing-08`。~~
   🔁 **2026-11-03 同日结清**（用户裁定甲案）：`InventoryFoldContext` 加必填 `AsOfTick` 切点 +
   新增 20 侧 `CraftCompletionSettler`（完成 tick 判容量 ⇒ 整批 `DropSpawned`，id 无第二铸造）。
   `TD-processing-08` **closed**；夹具 8 测、证伪 6/6。⇒ **本条已不成立**。
3. **S24-2 的两条 NOT-RUN**：`AC-7a-02` 前件不存在（无快照段）· `AC-7a-03` EXTERNAL（ADR-012 矩阵）。

### 收口后追加件（同日，不计入本 sprint 的计划件数）

| 件 | 结果 | 落点 |
|---|---|---|
| **`TD-processing-08` 实现轮** | 债 **closed**；fan-out = `InventoryFold` 切点 + `CraftCompletionSettler` | 证据 `impl-td-processing-08-craft-completion-settlement-2026-11-03.md` |
| **`TD-ci-03` 立债** | GitHub Actions 自 2026-10-10 起零运行；**退 P2** | 债册 `TD-ci-03` |

⚠️ 两件均为**收口后的追加工作**，不改变本 sprint「2 件 Must」的件数与判据；但
`TD-processing-08` 的实现让本 sprint「如实登记的三处没做完」中的第 2 条**同日消解**。
