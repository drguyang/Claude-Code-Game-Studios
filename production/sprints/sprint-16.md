# Sprint 16 — 2026-10-20 起 · **口径复位与欠账(非 feature · 零 story 交付)**

**周期**:`2026-10-20` 起(不设结束日期口径,做完即收 —— 承 sprint-12 起惯例)
**上游件**:`production/milestones/p0-completion-plan-2026-10-20.md` §三 sprint-16 行(本 sprint 即其兑现)
**规划依据**:`production/CAPACITY-MODEL.md` · `docs/tech-debt-register.md` ·
`production/retrospectives/retro-sprint-14-2026-10-18.md` §Action Items · `production/sprint-status-sprint15-archive.yaml`

**性质**:⚠️ **本 sprint 不交付任何 story**。它的产物是**判据** —— 让后续每一个 sprint 的
「还剩几件 / 哪些是 P0 债 / M3 闭没闭」**可机械复算**。承规划件 §三 的自我定性:
「本 sprint 的产物是**让后续所有判据可复算**」。

---

## ⚠️ Phase 1 硬前置:重扫实测发现(2026-10-20 · 全部可复算,非引用)

> `/sprint-plan` Phase 1 要求「不得据陈旧快照写入」。本轮重扫**核出五处矛盾**,其中三处
> **直接改变本 sprint 的面**。这些发现本身就是本 sprint 存在理由的证据。
>
> | # | 文件怎么说 | 今天实测 | 影响 |
> |---|---|---|---|
> | **1** | 规划件 §6.1 ① 「债册 **82 条** / ~20 张表」 | **92 个唯一 TD 编号** / **~17 张债型表**(+7 张非债型) | S16-3 的面**小了 12%** |
> | **2** | `milestones/README.md:371`「31 个 epic 全 Complete **22/31**」 | 按 story `Status:` 逐条归并 = **18/31** | M3 Exit 判据差 **4 户** ⇒ 归 S16-2 |
> | **3** | **三份静态件争真源** —— `p0-remaining-roadmap-2026-10-20.md`(路线图) / `p0-completion-plan-2026-10-20.md`(规划件) / `p0-remaining-backlog-2026-10-20.md`(backlog,`:60` 明写「**以本件为准**」) | 三份都自称静态层;**「剩余 X 件」今天不可复算** | ⇒ **S16-2 物理合并**(用户裁定 2026-10-20) |
> | **4** | 规划件 §三 sprint-16 ⑤「`TD-epic-01` 的 **3 条 `In Review`** 重评」 | `telemetry-analytics` 10 条 story **全部 `Complete`**,`In Review` = **0**;债册自身三处互斥(「3 条」/「两条」/「10 条一条未动」) | ⑤ 的工序描述**从起点就错** ⇒ 已**移出本 sprint → sprint-19**,并须按实测重定工序 |
> | **5** | retro-14 行动项 5「`TD-player-01` 归口」deadline = sprint-16 | **已修完(2026-10-20)**,`3940/3904/0/36`,**全量首次真正零红**;原件 `production/qa/evidence/impl-td-player-01-2026-10-20.md` 在盘 | A5 **只剩回写** ⇒ 移出本 sprint → sprint-17 |

**本轮自采基线**(脚本逐 story 读 `> **Status**:` 行,**非引用**):

| 量 | 实测值 | 复算命令 |
|---|---|---|
| P0 story 总数 | **226** | `ls production/epics/*/story-*.md \| wc -l` |
| `Complete` | **182** | 见下方脚本 |
| `Ready` | **41** | 同上 |
| `In Review` | **1**(`clinic-machine/story-001`) | 同上 |
| 部分完成 / `Pending` | **1 / 1**(`skeuo-019` / `world-ecozones-005`) | 同上 |
| ⇒ **非 `Complete`** | **44** | 182 + 44 = 226 ✓ |
| **全 `Complete` epic** | **18 / 31** | 逐 epic 归并其全部 story 的 `Status:` |

```bash
# 复算脚本(与 p0-remaining-backlog-2026-10-20.md §1.1 同款;⚠️ Status 行带 `>` 前缀)
python3 - <<'EOF'
import re,glob,collections
by=collections.defaultdict(list); c=collections.Counter()
for f in sorted(glob.glob('production/epics/*/story-*.md')):
    t=open(f,encoding='utf-8',errors='replace').read()
    m=re.search(r'^\s*>?\s*\*{0,2}Status\*{0,2}\s*[:：]\s*(.+)$',t,re.M)
    s=re.sub(r'[\*\[\]]','',m.group(1)).strip() if m else '(none)'
    c[s.split()[0] if s else '?']+=1; by[f.split('/')[-2]].append(s.startswith('Complete'))
print(dict(c)); print('全 Complete epic:',sum(1 for v in by.values() if v and all(v)),'/',len(by))
EOF
```

> ⚠️ **五处与 backlog 的独立实测吻合**(226 / 182 / 44 三方一致)⇒ 本轮**采用 backlog 的计数**,
> 但**不采用它的文档形态**(它仍是一份并行静态件,归 S16-2 合并)。

---

## Sprint Goal

把 P0 收官的**判据面**从「四处口径互相矛盾、剩余件数不可复算」压到
「**四份文件同值、分母有单一真源、八道门的清偿载体可机械求值**」——
**本 sprint 不产出任何 story**。

> **本 sprint 不做**:
> ① 任何 story 实现;② TD 的**实际清偿**(载体成文 ≠ 清偿);
> ③ 数值轮 / 外部美术;④ 改 story 的 `Status:`(唯一例外见 S16-5 的 EPIC.md 刷账,那是**分母**不是 story 状态)。

## Capacity

- **不用 `estimate_days`**(依 `production/CAPACITY-MODEL.md`)· 复杂度 = S/M/L
- **件/日基线**:sprint-13 = 7 done · sprint-14 = 11 done · sprint-15 = 7 done + 1 removed
- **并行纪律**:同时在跑 ≤ 3 件;⚠️ **Unity batch 独占工程** ⇒ 全量与证伪**一律串行**
- **本 sprint 预算**:Must **5 件**(**2×L + 3×M**)· Should 0 · Nice 0
  —— ⚠️ **含 2 个 L 件,是本 sprint 最大风险**(与 sprint-15 同形)

## Tasks

### Must Have (Critical Path)

| ID | 件 | C | 依赖 | 判据(可复算) |
|----|----|---|------|--------------|
| **S16-1** | **`CAPACITY-MODEL` 重标定 = 三元**(件 + 复杂度分布 + **计划外追加率**);`estimate_days` / 「件/日」正式废除 | M | 无 | ① `production/CAPACITY-MODEL.md` 落三元旋钮表;② §二 的「件/日」列整列移除(它已自废一次);③ §六 失败条件 #2/#3 改写为新口径;④ 本 sprint 的 `/sprint-status` **引用三元**,否则本件判失败(承 §六 条件 #1 的既有形态) |
| **S16-2** | **三份静态件物理合并** + **M3 Exit 两条合取落盘** + **分母真源裁决** | **L** | S16-3 的债册列(第二条合取求值需它) | ① 合并后**唯一位于** `production/milestones/p0-*.md`(给出 `ls` 实测,只应剩一份);② `milestones/README.md:371` 改写为**两条合取**(「31 个 epic 全部 `Complete`」∧「该 epic 名下 TD 的 P0 面 = 0」);③ 四处分数同值 **226 / 182 / 44 / 18-31**;④ 三处 `:371` 型的旧判据就地加注降级,不删原文 |
| **S16-3** | **债册增「P0 面 / 状态」两列**(实测 **92 条 / ~17 债型表**)+ **八道门清偿载体成文** | **L** | 无(但 S16-2 反向依赖它) | ① 两列落地到全部债型表;② **八道门的清偿载体 = TD 工作项**(不立 story、不重开已 `Complete` 的 epic)成文;③ M3 第二条合取**可机械求值** —— 附求值命令与当前预期值(**当前 ≠ 0**,如实报) |
| **S16-4** | retro-14 **A4**:「测试 / 工具不得写仓库」立**机械门** | M | 无 | ① grep 生产测试树(`File.WriteAllText` / `Directory.CreateDirectory` / `StreamWriter`)+ 白名单;② **证伪**:注入一处 `File.WriteAllText` ⇒ 门**必须红**(判据是「改坏生产 ⇒ 门红」,非「删断言 ⇒ 绿」) |
| **S16-5** | **EPIC.md 4 户分母刷账** | M | 无 | ① 四份 EPIC.md 的分数分母 = **实际 story 文件数**(`emergency-procedures` 7→**9** · `patient-ai` 4→**5** · `skeuomorphic-ui` 18→**25** · `world-ecozones` 分子 −1);② 与 S16-2 的 **18/31** 同源(刷账后须能由 EPIC.md 直接读出同一结论) |

> ### ⚠️ S16-3 的「P0 面」判定 —— 边界须用户认(本 sprint 的自我约束)
>
> 92 条里「是否 P0 面」**本身是一个新裁决面**。本 sprint **只判可机械判定的三条规则**:
> ① 触发条件引用 **P0 面判据**(M3/M4 出口、P0 AC)⇒ P0;
> ② 触发条件明写 **P1a / P1b / 外部 / 数值轮** ⇒ 非 P0;
> ③ 其余 ⇒ **单列「待裁」,不自行裁**。
> ⚠️ **不得**为让 M3 第二条合取「好看」而把待裁项判成非 P0。

## Carryover from Previous Sprint

| Task | Reason | New Estimate |
|------|--------|-------------|
| — | sprint-15 无 carryover(7 done + 1 `removed`,S15-6 已移出为 `TD-listen-01`) | — |

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| **含 2 个 L 件(S16-2 / S16-3)同 sprint** | **高** | 中 | 各自**独占一批**;若 S16-3 的「P0 面」判定需逐条裁 ⇒ **立即降为「登记待裁」**并让 S16-2 先收 |
| **S16-2 的自我指涉** —— 被合并的三份件之一是**承载本件序列表的文档**(`p0-completion-plan` 本身) | 中 | 中 | **先定合并落点再动手**;合并后须重跑「三份件只剩一份」的实测;规划件 §三 随之搬家(本 sprint 同批回写) |
| S16-3 的「P0 面」是新裁决面,易被顺手裁 | 高 | 中 | 见上方自我约束;**待裁项须进 §六 型清单交用户**,不进「已裁」 |
| **零 story ⇒ 「文档写完但判据没动」** | 中 | **高** | 每件**必须**附可复算命令 + 预期输出;**无命令的件不计 done** |
| 合并期「两份并存」窗口内有人据旧件排期 | 中 | 中 | S16-2 未收口前,任何「剩余 X 件」读数**须并列三份来源** |

## Dependencies on External Factors

- **无**。本 sprint 全部 5 件均可单方推进(`TD-listen-01` / 外部美术 / 数值轮均不在面内)。

## 移出与去向(⚠️ 必须登记,否则蒸发)

> 本仓已知失败模式:**「从 sprint 移出」若不写去向 ⇒ 该件即消失**。逐条落定:

| 移出件 | 原在 | 去向 | 状态 |
|---|---|---|---|
| `TD-epic-01` 清 P0 面 | 本 sprint ⑤ | **sprint-19**(AC 门批,与 G-4/5/6/7 同族) | ⚠️ **工序须按实测重定** —— 0 条 `In Review`,不是「重评 3 条」而是「选定哪些 story 的未验半边须降级/挂 `BLOCKED-BY`」 |
| retro-14 **A3**「已证」补分支覆盖判据 | 本 sprint ④ | **sprint-19**(与 AC 判据同族) | 未开工 |
| retro-14 **A5** `TD-player-01` 回写 | 本 sprint ④ | **sprint-17**(轻件) | 实质**已修完**,只差刷 retro 行动项表那一行 |

## Definition of Done for this Sprint

- [ ] Must **5 件**全部完成,**每件附可复算命令 + 预期输出**(禁止「已落盘」当判据)
- [ ] **零 story 转 `Complete`** —— 本 sprint 不产生任何 story 状态变更
- [ ] 四处分数**同值**:`226` / `182` / `44` / `18-31`;债册 **92 条**两列齐
- [ ] S16-4 的机械门过**证伪轮**(改坏生产 ⇒ 门转红)
- [ ] S16-2 后「静态件只剩一份」有实测命令输出
- [ ] QA plan 存在:`production/qa/qa-plan-sprint-16-2026-10-20.md`
- [ ] Smoke check 记录:`/smoke-check sprint`
- [ ] QA sign-off:`/team-qa sprint`
- [ ] 规划件 `p0-completion-plan-2026-10-20.md` §三 与 `TD-epic-01` 行**同步回写**(件集变更 + 移出去向)
- [ ] 无 S1 / S2 bug

---

> **⚠️ 本 sprint 无 QA plan 以外的人工验收面** —— 5 件全部是纸面 + 门,
> **唯一**需要跑 Unity 的是 S16-4 的证伪轮与全量回归。承 `production/qa/qa-plan-sprint-16-2026-10-20.md`。
>
> **不经用户指令不 commit。**
