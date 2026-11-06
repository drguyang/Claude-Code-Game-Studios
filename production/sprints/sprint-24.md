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
      🔁 **2026-11-05 深夜补记（原文不删）**：`/smoke-check sprint` 已跑到**第三轮**，判定 **FAIL**
      （`production/qa/smoke-2026-11-05.md` §第三轮）。自动化全绿（`16/16` 可执行判据 PASS），
      但**首次启动路径**上「设置 → ESC 关不掉」复现（新立 `TD-skeuo-17`，根因 = `_modalInput` 在 6.3
      才构造、主菜单闸门在 2.95）；脉案 `ICasebookDtoSource` 未注入 ⇒ 值区恒空。
      **本 sprint 仍不得称「全绿收口」，且该条 DoD 依旧不勾。**

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

---

## 收口后追加件 · 第二批（2026-11-04 ~ 2026-11-05）

> **登记时点**：2026-11-05 · 用户指令「把这两天的工作补进 `sprint-24.md` 的收口后追加件节」。
>
> ⚠️ **本节不改变本 sprint 的任何判据**：`Status` 仍为 **Closed**，「2 件 Must」的件数、
> 逐件判定与 DoD 三项**一字不动**。本节记录的是**收口之后两天里实际推进的东西** ——
> 按 `CAPACITY-MODEL.md` 的口径，它们属于 **sprint-25 的候选面**，不是本 sprint 的交付物。
> 不把它们说成「sprint-24 的一部分」，也不因此重开本 sprint。

**范围与复算**（`c6130cde` = 收口时的 HEAD，见上文 §⚠️ CI 核查）：

```bash
git log --pretty=format:"%ad" --date=format:"%Y-%m-%d" c6130cde..HEAD | sort | uniq -c
# 2026-11-05 实测：28 笔 2026-11-03（已由上节覆盖）
#                30 笔 2026-11-04 + 12 笔 2026-11-05 = 42 笔 ← 本节登记面
```

### 一、存档面（本批最大一块，30 笔的大半）

**多世界（ADR-040 D 案）从 `Proposed` 一路做到 UI 可点**：
`3b3fbf11` 主菜单世界列表 + 新起一局 · `5590b8c5` 删除世界（`§D6`，二次确认 + 序号高水位）·
`ceac3539` 主菜单改回固定四行 · `0e916e8c` 修「全新档第一局落在 w1」
（空目录 + 幽灵索引顶号）· `781d5ef8` 补真启动序 PlayMode 判据，**顺带修出重装配二次应用开档
基线**（该缺陷是夹具先红逼出来的）· `57a3ac9e` 修主菜单行重建后焦点悬空 · `25d0a361` 三路径验证 + 首份存档。

**读档时钟链**（三笔同一根因）：`238eef15` 读档后逻辑时钟未定位 · `efa72b06` 补真档现场复核 ·
`a29e16ad` 单调 tick 门不再卡死世界 ⇒ **`TD-sim-05` closed**。

**存档槽语义（ADR-041）**：`18c274d4` 空槽即写 / 覆写不清锁 / 行内二段确认 ·
`9f7c994c` 补 V-9 日志捕获夹具 · `046b6139` 二段态保留行文字 · `bcc29770` **`§D4` 改判** ——
两条 bak 谓词合并为一条（`.bak` 一律算存档）· `d8d42d4f` 玩家槽不再回退 bak ⇒ **`TD-persist-19` closed** ·
`fa2b1738` 走查救回成立、**立 `TD-persist-22`** · `d5db89d8` + `78981d08` 让册子说得出「前版 /
此页已损」⇒ **`TD-persist-22` closed**。

**册子形态**：`93bf989c` 槽号按身份固定 · `5c455b1e` 转册（每页 6 行 / 页缘 / 鱼尾页码）·
`75a3a9c4` 存档遗留三条结清。

### 二、拟物 UI（贴图与屏形态）

- **`TD-skeuo-03` closed**（`a0073916` + 六十订 `787396b0`）：闭集 9 员都能被同一个 toggle 关掉。
- **`TD-skeuo-07` / `-08` closed**（`406044ea`）：`url("guid:…")` 在 Unity 6.3 恒不解析 ⇒ 8 条贴图引用
  自接图起从未生效；同时 `.paper` 一分为二（无框底纹 / `.paper-page` 纸+页框）。
- **`TD-skeuo-12` closed**（`9ec8f39f`）：模态屏统一竖直居中 + 设置壳补页框。
- **`TD-skeuo-04` 的 ④ 半边 / `TD-skeuo-06` closed**（`20af6fbf`）：设置壳入口接通 + 炮制点火生产实现。
- **`TD-skeuo-10` closed**（`85de93fa`）：九宫格冻结件 round-2 重冻 + 测量脚本入库。
- 走查件：`663dca68`（模态开合链图形模式走查）· `29324019`（七帧视觉走查 + 两笔新债）·
  `182044f2` / `f369cd6f`（拟物贴图**不入库**，核心资产不上 GitHub）。

### 三、52 事件链（三笔，把「恒零写入」的最后两级打通）

| 提交 | 内容 | 债 |
|---|---|---|
| `fae861ee` | F1 上下文门接真源，并订正与 `AC-52-29` **相反**的判据 | `TD-randomevents-05` **closed** |
| `7f6af7e9` | 出诊类 `event_key` 集改从数据派生 | `TD-randomevents-06` `open → partial`（机制结，内容侧开） |
| `ea8b946d` | **系统 52** 的事件 → **系统 37** 病种的映射接通（`disease_id` 外键，池 `schema_version 2 → 3`） | `TD-randomevents-03` **closed** |

⚠️ **端到端仍未证**：`isOnExpedition` 需真走出诊路径（人工走局），
且 `PatientAppearedDriver` 脚手架仍在跑 ⇒ 归因须按 `DiseaseOnset.disease_id`（脚手架 = `INJ_STAB` = 2，
52 = 7 / 17 / 11），**不按「有没有病人」**。

### 四、病种与夹具

- `be723016`：**P0 病种上限 8 → 9**（推翻 `disease-simulation.md:1406` 的 8，用户裁定「放宽到 9」）+
  新增热性惊厥对照行 ⇒ `TD-diagnosis-02` / `TD-sim-06` `open → partial`。卡鱼刺 / 偏头痛**未进 P0**，
  `TD-emergency-01` 维持 `open`。
- `3f639bb3`：钉住 `DIS_MALARIA` 的昏迷余量 —— `a_peak/scale = 0.45` 与 `COMA_THRESHOLD = 29491/65536`
  只差 **0.2 ULP**，Q16.16 除法**恰好**舍到 29491、比较用 `>=` 才成立 ⇒ 昏迷支靠「压线」成立，
  此前**零覆盖**。补 4 条夹具（字面量哨兵 + 真表 + 运行时舍入 + 窗口宽度）。

### 五、文档 / 仓务

`46afde4e` 世界管理定 Valheim 形态（ADR-040 `§Amendment A`，实现押后 P1）·
`c4d41a49` ADR-040 `§D11` 补裁「**角色进度跟人走**」（与 `ADR-026 §⑥` 相抵已显式登记，P0 不做事）·
`d25beeea` `TD-persist-21` 先结后**同日重开**（`P0 面 Y → N`，范围裁定不是能力补齐）·
`4737e13b` `.gitignore` 忽略 `ref_valheim/`。

### 债面变化（两日合计，可用 `git show` 只读复算）

| 量 | 11-03 末（`28a8d84a`） | 11-05 末（`3f639bb3`） | 差 |
|---|---|---|---|
| 条目 | 169 | **186** | **+17** |
| `closed` | 147 | **161** | **+14** |
| `partial` | 3 | **6** | **+3** |
| `open` | 18 | **18** | ±0 |
| **未闭 P0 面** | **10** | **10** | **±0（但成分全换）** |
| epic 分布 | **3/31**（persistence 1 / random-events 5 / skeuo 4） | **6/31**（processing 1 / random-events 4 / diagnosis 1 / disease 1 / emergency 1 / skeuo 2） | **3/31 → 6/31** |

⚠️ **「未闭 P0 面 10 → 10」不表示没进展**：11-03 那 10 条（persistence / random-events / skeuo 三个 epic）
**已全部清偿**，今天的 10 条是**另一批**（含三个新 `partial`）。epic 摊布从 3 个变成 6 个是**这条**的读数。

```bash
# 复算（只读取，不改工作树）—— 11-03 末 vs 当前
python3 - <<'PY'
import re,subprocess,collections
def stats(rev):
    t=subprocess.run(['git','show',f'{rev}:docs/tech-debt-register.md'],capture_output=True,text=True).stdout
    m=re.search(r'<!-- TD-INDEX:BEGIN -->(.*?)<!-- TD-INDEX:END -->',t,re.S)
    rows=[[c.strip() for c in l.strip().strip('|').split('|')] for l in m.group(1).split('\n') if l.startswith('| `TD-')]
    live=[r for r in rows if r[2]!='closed' and r[3].strip('*')=='Y']
    return len(rows),dict(collections.Counter(r[2] for r in rows)),len(live),dict(collections.Counter(r[1].strip('`') for r in live))
print(stats('28a8d84a')); print(stats('HEAD'))
PY
# 期望：条目 169 / closed 147 / partial 3 / open 18 / 未闭 P0 面 10（epic 3 个）
#       条目 186 / closed 161 / partial 6 / open 18 / 未闭 P0 面 10（epic 6 个）
```

当前 10 条未闭 P0 面：`TD-processing-09` · `TD-randomevents-04` / `-06`(partial) / `-07` / `-08` ·
`TD-diagnosis-02`(partial) · `TD-sim-06`(partial) · `TD-emergency-01` · `TD-skeuo-04` / `TD-skeuo-11`。

### 门

| 门 | 结果 | 复算 |
|---|---|---|
| `python3 tools/debtcount/debtcount.py --check` | **exit 0** | `✅ §五 输出块与索引块一致` |
| `python3 tools/acid/acid_check.py --check` | **exit 0** | — |

### 回归三元组

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | **`6919 / 6882 / 0 / 37`** | `2026-11-05T10:53:13Z` | sprint-24 追加批（疟疾夹具轮） | `3f639bb3`（工作树干净） |
| PlayMode | **`341 / 337 / 0 / 2 / 2`** | `2026-11-05T10:00:32Z` | 同上（热性惊厥轮） | `3f639bb3` |

```bash
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/ed-mal-full.xml').getroot();print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/playmode-febrile.xml').getroot();print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
git status --short          # 期望：空 ⇒ 上表两行对应 `3f639bb3` 的干净工作树
```

⚠️ PlayMode 那一行是 **`10:00:32Z`** 的跑（疟疾轮的 `10:53` 只跑了 EditMode）。它对本轮成立的理由
**不是省事**：`git diff --stat -- '*.cs' ':!*Tests*'` 在疟疾轮为空 ⇒ 生产代码逐字节未变。
这是**论证**，不是另一次观测。

### 仍未做（承上文「如实登记的三处没做完」）

1. **`/smoke-check sprint` 仍未跑** —— 见 §DoD 第 3 条与本文件末的收口门表，**本 sprint 依旧不得称「全绿收口」**。
2. **`TD-randomevents-04` 退 P1a**（`Mod_Eco` / `Mod_Rep` 无 P0 观测面），`-06` 内容侧、`-07` / `-08` 仍开。
3. **卡鱼刺 / 偏头痛不入 P0**（`TD-emergency-01` 维持 `open`）：动作轴只有 2 个动作，装不下「取出」。

---

## 收口后追加件 · 第三批（2026-11-06）

> **登记时点**：2026-11-06 · 用户指令「补第三节，把今天三批活登记进去」。
>
> ⚠️ **本节不改变本 sprint 的任何判据**，与第一 / 第二批同口径 —— `Status` 仍为 **Closed**，
> 「2 件 Must」的件数、逐件判定与 DoD 三项**一字不动**。本节登记的是收口之后**第三天**实际推进的
> 东西，按 `CAPACITY-MODEL.md` 属于 **sprint-25 的候选面**，不是本 sprint 的交付物。

**范围复算**（自包含，只读）：

```bash
git rev-list --count --since="2026-11-06 00:00" HEAD      # 期望：21
git log --since="2026-11-06 00:00" --pretty=format:"%h %ad %s" --date=format:"%H:%M"
# 21 笔 = 本节「三批」14 笔（批一 4 · 批二 6 · 批三 4）+ 同日凌晨一轮 7 笔
# 注:三批按**主题**分，不按时间连续 —— 批一的真机验收（08:28）落在批二的首两笔（07:09）之后
```

### 一、批一（06:15–08:28）—— 档头 tick 早于流尾（「继续诊治」后走不动）

| 笔 | 内容 |
|---|---|
| `d0d4d9f1` | `fix(persist)`：读侧 `max(档头, 流尾)` + 写侧组版口 `max(调用方, 流尾)`，**布局零改动** |
| `d6e4f2fa` | 会话摘要（P0 修复轮 + smoke 复跑） |
| `2cb17d69` | `smoke-2026-11-06` 补炮制链走查实录 |
| `6e649dab` | 真机验收补跑：用盘上**本来就有的**真坏档 `w2/save_3`（档头 179 / 流尾 1554）启封 |

**根因**：读档按**档头**定位时钟，而档头 tick 早于流尾 ⇒ 时钟落在历史之前 ⇒ 此后每笔 `Append` 被拒收
（`BCD-码-3` 刷 1552 条）；写档侧从不校验该不变式 ⇒ 坏档自我放大。

**判据**：`save_tick_vs_stream_tail_test` **7 条**；证伪轮 **M1 / M2 / M3 各自转红**，还原 SHA-256 逐字节
（端到端那条在 M1/M2 下仍绿 = 纵深防御的预期形态，**如实登记**）。

**真机**：钳位告警出现、时钟按流尾 1554 定位、`BCD-码-3` **0 条**、载入后位移可见。
⚠️ **写档侧钳位的真包可观测面 = NOT-RUN** —— 读侧钳位已让「时钟落后流尾」的活会话不可能自然出现，
只能由 EditMode `test_compose_headerTick_neverBehindStreamTail` 覆盖；**首犯会话不可考**（`Player.log` 每次启动覆盖）。

证据：`production/qa/evidence/fix-p0-tick-behind-stream-tail-2026-11-06.md` · `walkthrough-2026-11-06-p0-realplayer.md`

### 二、批二（07:09–13:25）—— 空间解：病人两角 · 灶台第三角 · 地面覆盖整张图

| 笔 | 内容 |
|---|---|
| `e2d7af89` · `db19e0ee` | 炮制链走查实录 / World 场景计数订正（炮制屏入口不可达的取证） |
| `2ce5ab35` | 立 `TD-building-05`（P0 零结构视觉载体） |
| `d40cc570` | 炮制链真包复测：13 次 `E` 零命中 —— **订正上一轮的错误归因** |
| `d4f3b4be` | 空间解本体：`PatientWiringPositions`（2 格）+ `PresenceCellAnchorSource`（新）· `MaxPatientsDuringWiring` 3 → 2 · 灶台 `(10,0,5)` → `(14,0,14)` · Ground 18×18 居中 |
| `42493f6b` | 全链跑步：采集 → 选料 → 灶台三段跑通，空壳归因坐实 |

**本轮最重要的产出是一条订正**：上一轮记的「病人 `KindPriority` 压住灶台」**不成立** ——
病人当时**全部登记在 `(0,0,0)`**、玩家也在 `(0,0,0)`，按键 ①（格距）病人 `d∞ = 0` 就赢了，
`KindPriority`（键 ②）**根本没参与**。玩家走不动的真因是 `Ground` 只有 **10×10 米**、世界格是 16×16 ⇒
`x,z ≳ 5` 之外脚下没有地板，探针实测 `player=0,-6,-9` / `player=-9,-7,0`（y 为负 = 在坠落）。

**新债**：`TD-building-05`（P0 运行期零结构视觉载体，**open**）· `TD-building-06`（地面 10×10 vs 16×16，
**open** —— 已就地改成 18×18，但**没建立任何门** ⇒ 下次改世界尺寸同型复发）；`TD-building-04` 详情表订正。

⚠️ **批二没有自己的全量回归** —— 它随批三的 13:43 / 13:49 那两次全量一起被判（见下「回归三元组」）。
这是**论证**，不是又跑了一次。

证据：`production/qa/evidence/walkthrough-2026-11-06-space-fix.md` §1–§8

### 三、批三（13:32–14:25）—— `TD-skeuo-20` + 干净包确认

| 笔 | 内容 |
|---|---|
| `be775dec` | `fix(ui)`：容器屏 `Bind` 重建前后按 **`instance_id`** 还原焦点；关卡黄金哈希**第四次重签**（`681d503d…` → `be93eed1…`，1697 → 1698 B，理由 = 灶台格多一个字符） |
| `63c358af` | `.gitignore`：走查/测试截图窄 glob（`production/qa/evidence/**/*.png|jpg|jpeg`），**证据正文仍跟踪** |
| `9233d003` | 证据 §10（干净包确认） |
| `0293d635` | `chore(packages)`：Unity 自动升级 Linux 工具链 1.1.0 → 1.1.1，原样入库 |

**判据**：`inventory_container_content_test` **7/7**（+2：正控 `test_tdSkeuo20_bind_focusesFirstCell` ·
负控 `test_tdSkeuo20_rebind_keepsFocusOnSameInstance`）；证伪 **M1**（注掉 `FocusAfterRebuild`）⇒
**2 红 / 5 绿**，恰为新增两条，还原 SHA-256 `ee6e44c0…02790bc0f` 逐字节一致。

**干净包**：`Gameplay.Boot.dll` @ 14:07 · SHA-256 `bf916580f8d77dbce59e6b60ff121c908eb62d69cc56327e5b316c8cae023ffb`；
`grep -a -c 'PROBE-4'` 该 DLL = **0**、源码 = 0。
⚠️ 可执行文件本体仍是 11-05 20:25 的缓存启动器 —— **判包新旧要看 `_Data/`，不能看它**。

**真包三步**：开屏行周**即有** 2px 铜环 → 只按一次 `Return`（画面无变化：本屏没有 `.selected` 样式，
选中态与焦点环视觉重合）→ 灶台旁 `E` 得炮制屏**两行**（`dry_willow_bark` / `extract_salicylic`）
= **因果下游判据**（行只在 `consumedQualities` 非空时出现）。

**附带登记（不是缺陷）**：在 `(12,0,13)` 按 `E` 开不出炮制屏 —— 采集点 `(12,0,12)` 的 `d∞=1` 赢了
灶台 `(14,0,14)` 的 `d∞=2`；**距离是键 ①，`KindPriority` 只在等距时破平**。同一条机制就是批二订正的那件事。

证据：`production/qa/evidence/walkthrough-2026-11-06-space-fix.md` §9–§10

### 四、附：同日凌晨一轮（02:05–04:04，**不计入「三批」**）

只列指针，避免日账出现缺口：**ADR-042**（存档 v6 落笔时刻 + 锁从读侧搬到写侧 + 存档册行列形态，
`a92775d3` · `6aba5507` · `c87fa607` 立 `TD-skeuo-18`）·
**`TD-skeuo-19`**（存档册滚轮/上下键到页边界翻页 + 留档行落位，`5dff210f` · `79fa3d42`）·
`9854db6b` IL2CPP 工具链包由编辑器解析至 1.1.1（出包派生）。
证据件：`impl-adr042-save-v6-and-slot-lock-2026-11-06.md` · `impl-td-skeuo-19-save-book-paging-2026-11-06.md`。

### 债面变化（11-05 末 → 11-06 末，含凌晨一轮）

| 量 | 11-05 末（`b2c07753`） | 11-06 末（`9233d003`） | 差 |
|---|---|---|---|
| 条目 | 193 | **198** | **+5** |
| `closed` | 164 | **166** | **+2** |
| `partial` | 9 | 9 | ±0 |
| `open` | 19 | **22** | **+3** |
| **未闭 P0 面** | **12** | **15** | **+3** |
| epic 分布 | 6/31 | **7/31** | **+`modular-building`（2 条）** |

新增五条：`TD-building-05`（open）· `TD-building-06`（open）· `TD-skeuo-18`（open）·
`TD-skeuo-20`（closed）· `TD-skeuo-19`（closed）。

⚠️ 与第二批同一个提醒：**未闭 P0 面 12 → 15 不是「没干活」** —— 三批关掉一条（`TD-skeuo-20`），
批二新开两条并带进**一整个 epic 面**（`modular-building`）。这两条是「修好了但没建门」，不是新缺陷面扩大。

```bash
python3 tools/debtcount/debtcount.py --print
# 期望:条目 198 | closed 166 | partial 9 | open 22 | done 1 | 未闭 P0 面 = 15(7/31 个 epic)

# 逐条差集(11-05 末 vs HEAD,只读)
python3 - <<'PY'
import re, subprocess
def rows(rev):
    t = subprocess.run(['git','show',f'{rev}:docs/tech-debt-register.md'],
                       capture_output=True, text=True).stdout
    m = re.search(r'<!-- TD-INDEX:BEGIN -->(.*?)<!-- TD-INDEX:END -->', t, re.S)
    out = {}
    for l in m.group(1).split('\n'):
        if not l.startswith('| `TD-'): continue
        c = [x.strip() for x in l.strip().strip('|').split('|')]
        out[c[0].strip('`')] = (c[1].strip('`'), c[2], c[3])
    return out
a, b = rows('b2c07753'), rows('HEAD')
print('新增:', {k: b[k] for k in b if k not in a})
print('状态变化:', {k: (a[k][1], b[k][1]) for k in b if k in a and a[k][1] != b[k][1]})
PY
# 期望:新增 5 条(building-05/06, skeuo-18/19/20);状态变化为空
```

### 门

| 门 | 结果 | 复算 |
|---|---|---|
| `python3 tools/debtcount/debtcount.py --check` | **exit 0** | `✅ §五 输出块与索引块一致` |
| `python3 tools/acid/acid_check.py --check` | **exit 0** | — |
| 工作树 | **干净** | `git status --short` 期望空（本提交前） |

### 回归三元组

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode（批一） | **`6968 / 6931 / 0 / 0 / 37`** | `2026-11-05T22:10:49Z`（= 11-06 06:10 CST） | 档头 tick P0 修复轮 | `79fa3d42` + 工作树（补丁尚未提交） |
| PlayMode（批一） | **`349 / 345 / 0 / 2 / 2`** | 同上 | 同上 | 同上 |
| EditMode（批二+批三） | **`6968 / 6931 / 0 / 0 / 37`** | `2026-11-06 13:49 CST` | 干净包前的全量（**黄金重签之后**） | `be775dec` 的前一刻 —— 工作树内容即该提交 |
| PlayMode（批二+批三） | **`351 / 347 / 0 / 2 / 2`** | `2026-11-06 14:03 CST` | 同批 | `be775dec`（14:00 提交，14:03 跑测） |

**等式判据**：PlayMode `349 → 351` / `345 → 347` ⇒ **+2 / +2**，恰为 `TD-skeuo-20` 新增的两条夹具，
零回归。EditMode `6968` 与批一逐位相同 —— 批二/批三**没有加 EditMode 夹具**，而黄金哈希重签是
**常量**变更（不增删测试），两者自洽。

⚠️ 批二那次全量首跑是 **`6968 / 6930 / 1 / 0 / 37`**，唯一红的是
`test_ac3_newDispatchPath_bytesEqualPreRefactorGolden`（1697 → 1698 B）。**那是黄金哈希在正常工作**，
不是回归 —— 处置是**声明为合法内容变更 + 重签常量并留注**，不是「改夹具让它变绿」。

```bash
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/skeuo20-editmode2.xml').getroot();print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/skeuo20-playmode-full.xml').getroot();print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
```

### 仍未做（本节口径）

1. **炮制链「点火 → 完成 → 起货」端到端仍 `NOT-RUN`** —— 今天只跑到「灶台屏有两行」（选料面通了），
   `AC-18-21` 的人工腿仍未过。⚠️ 它同时是 `smoke-2026-11-06` §七 的那条阻塞项。
2. **四条 `[L]`（听测 3 + 手柄走查 1）未做** —— 人听 / 人走，不借绿。
3. **`design/ux/processing-18.md` 仍缺** ⇒ `AC-42-F1` ⑨ 阻塞签核。
4. **`TD-building-05` / `TD-building-06` 仍 `open`** —— 前者是 P0 运行期零结构视觉（灶台不可见），
   后者已就地盖住整张图但**没有门**；两条都不在本 sprint 的件面内。
5. **CI 仍零运行**（`TD-ci-03`）；`smoke-2026-11-06.md` 仍是 **PASS WITH WARNINGS**
   （`run_smoke.py` exit 3 = `INCOMPLETE`，3 条无载体）⇒ 按本文件 §DoD 原文，**仍不得**记「smoke 已通过」。
