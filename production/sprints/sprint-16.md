# Sprint 16 — 2026-10-20 起 · **口径复位与欠账(非 feature · 零 story 交付)**

**周期**:`2026-10-20` 起(不设结束日期口径,做完即收 —— 承 sprint-12 起惯例)
**上游件**:`production/milestones/p0-remaining-roadmap-2026-10-20.md` **§6.5** sprint-16 行(本 sprint 即其兑现)。⚠️ 原件为 `production/milestones/p0-completion-plan-2026-10-20.md` §三,该件已于 **S16-2** 物理并入路线图并 `git rm`。
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
> | **3** | **三份静态件争真源** —— `p0-remaining-roadmap-2026-10-20.md`(路线图) / `p0-completion-plan-2026-10-20.md`(规划件) / `p0-remaining-backlog-2026-10-20.md`(backlog,`:60` 明写「**以本件为准**」) | 三份都自称静态层;**「剩余 X 件」今天不可复算** | ⇒ **S16-2 物理合并**(用户裁定 2026-10-20)。✅ **已执行**:两件 `git rm`,内容并入路线图(§〇·二/§〇·三/§一/§2.4/§2.5/§3.1/§4.4/§5.1/§5.2/§6.5–§6.7/§7.1/§八) |
> | **4** | 规划件 §三 sprint-16 ⑤「`TD-epic-01` 的 **3 条 `In Review`** 重评」 | `telemetry-analytics` 10 条 story **全部 `Complete`**,`In Review` = **0**;债册自身三处互斥(「3 条」/「两条」/「10 条一条未动」) | ⑤ 的工序描述**从起点就错** ⇒ 已**移出本 sprint → sprint-19**,并须按实测重定工序 |
> | **5** | retro-14 行动项 5「`TD-player-01` 归口」deadline = sprint-16 | **已修完(2026-10-20)**,`3940/3904/0/36`,**全量首次真正零红**;原件 `production/qa/evidence/impl-td-player-01-2026-10-20.md` 在盘 | A5 **只剩回写** ⇒ 移出本 sprint → sprint-17 |
>
> 🔁 **2026-10-20 订正(第 1 行的「今天实测」本身不准 · S16-3 收口轮)**:该格原写
> 「**92 个唯一 TD 编号** / **~17 张债型表**」——**两个数都错**,错因同源(口径未声明):
> ① **92 是全文串扫值**,严格口径 = **91**(`TD-audio-01` 是族引用多算 1;
> `TD-epic-01-a…-e` 被截断少算 5;净 +1 ⇒ 91 − 5 + 6 = 92);
> ② 「~17 张债型表」→ 含 TD 行的 Markdown 表**恰 20 张**(全册 50 张表)。
> **原文保留不删**(它记的是 S16-3 规划当时的读数)。真源见
> `docs/tech-debt-register.md` **§★ 总索引 §二**。⚠️ 该「S16-3 的面小了 12%」的结论
> **仍然成立**(82 → 91 是 +11%),只是涨幅按 91 计。

> **本轮自采基线**(脚本逐 story 读 `> **Status**:` 行,**非引用**):

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
# 复算脚本(与路线图 §1.1 同款;⚠️ Status 行带 `>` 前缀。原件引 `p0-remaining-backlog-2026-10-20.md §1.1`,该件已由 S16-2 并入路线图)
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
- ~~**件/日基线**:sprint-13 = 7 done · sprint-14 = 11 done · sprint-15 = 7 done + 1 removed~~
  → 🔁 **2026-10-20(S16-2 收口本残留)**:该行**用的正是 SPRINT-16 自己要废除的单位** ——
  「件/日」已由 `CAPACITY-MODEL` §二 自废(周期字段混合口径,不可跨 sprint 比)。**改用 §3.1 记录三元**:

  | Sprint | 计划件 | 复杂度分布 | 计划内完成 | 追加 | 追加率 |
  |---|---|---|---|---|---|
  | 13 | 8 | 6S + 1M + 1 人件 | 8 | 0 | 0% |
  | 14 | 7 | 1S + 4M + **2L** | 7 | **4**(3M + 1L) | **57%** |
  | 15 | 8 | 2S + 4M + **2L** | 7(另 1 `removed`) | 0 | 0% |

  ⚠️ 上表数字**与旧行不同不是笔误** —— 旧行写的是**交付件数**(14 = 7+4 追加),表里写的是**三元**。
  **禁把任一元当速率外推**(`CAPACITY-MODEL.md` §六 条件 #3)。
- **并行纪律**:同时在跑 ≤ 3 件;⚠️ **Unity batch 独占工程** ⇒ 全量与证伪**一律串行**
- **本 sprint 预算**:Must **5 件**(**2×L + 3×M**)· Should 0 · Nice 0
  —— ⚠️ **含 2 个 L 件,是本 sprint 最大风险**(与 sprint-15 同形)

## Tasks

### Must Have (Critical Path)

| ID | 件 | C | 依赖 | 判据(可复算) |
|----|----|---|------|--------------|
| **S16-1** | **`CAPACITY-MODEL` 重标定 = 三元**(件 + 复杂度分布 + **计划外追加率**);`estimate_days` / 「件/日」正式废除 | M | 无 | ① `production/CAPACITY-MODEL.md` 落三元旋钮表;② §二 的「件/日」列整列移除(它已自废一次);③ §六 失败条件 #2/#3 改写为新口径;④ 本 sprint 的 `/sprint-status` **引用三元**,否则本件判失败(承 §六 条件 #1 的既有形态) |
| **S16-2** | **三份静态件物理合并** + **M3 Exit 两条合取落盘** + **分母真源裁决** | **L** | S16-3 的债册列(第二条合取求值需它) | ① 合并后**唯一位于** `production/milestones/p0-*.md`(给出 `ls` 实测,只应剩一份);② `milestones/README.md:371` 改写为**两条合取**(「31 个 epic 全部 `Complete`」∧「该 epic 名下 TD 的 P0 面 = 0」);③ 四处分数同值 **226 / 182 / 44 / 18-31**;④ 三处 `:371` 型的旧判据就地加注降级,不删原文 |
| **S16-3** | **债册增「P0 面 / 状态」两列**(实测 **92 条 / ~17 债型表**)+ **八道门清偿载体成文** | **L** | 无(但 S16-2 反向依赖它) | ① ~~两列落地到全部债型表~~ ⇒ **✅ 落为「总索引表」**(用户裁定,非逐表加列;真源 = 债册 §★ 总索引);② **✅ 八道门的清偿载体 = TD 工作项**(不立 story、不重开已 `Complete` 的 epic)—— 债册 §六 载体表;③ **✅ M3 第二条合取可机械求值** —— 求值命令 + 当前值 **≠ 0(实测 72 条未闭 / 12 个 epic,如实报)**。⚠️ 三处口径订正见下方 **S16-3 收口注** |
| **S16-4** | retro-14 **A4**:「测试 / 工具不得写仓库」立**机械门** | M | 无 | ① grep 生产测试树(`File.WriteAllText` / `Directory.CreateDirectory` / `StreamWriter`)+ 白名单;② **证伪**:注入一处 `File.WriteAllText` ⇒ 门**必须红**(判据是「改坏生产 ⇒ 门红」,非「删断言 ⇒ 绿」) |
| **S16-5** | **EPIC.md 4 户分母刷账** | M | 无 | ① 四份 EPIC.md 的分数分母 = **实际 story 文件数**(`emergency-procedures` 7→**9** · `patient-ai` 4→**5** · `skeuomorphic-ui` 18→**25** · `world-ecozones` 分子 −1);② 与 S16-2 的 **18/31** 同源(刷账后须能由 EPIC.md 直接读出同一结论)。✅ **2026-10-21 两条均实测通过** —— 见下方 S16-5 收口注 |

> ### ✅ S16-1 收口注(2026-10-20 · **含两条判据订正**,禁借绿)
>
> **落地** = `production/CAPACITY-MODEL.md`(§二·补二 三元实测表 · §3.1 记录三元 / §3.2 约束三条
> 两层 · §六 #2/#3 换靶 + Bulletin 第二条)+ `.claude/skills/sprint-status/SKILL.md` §4b 改读两层。
>
> **订正 ①(本卡判据 ② 早已完成,非本轮工作)**:原写「§二 的『件/日』列**整列移除**」——
> 实测该列**在本模型写入当日(S12-1)即被 §六 自己废掉并整列删除**(§二 取数口径注 + §六 Bulletin 第一条
> 均已载明)。⇒ 本轮**改为「核实并登记」**,零工作量。**原文保留不改写**(它是 S16-1 规划当时的措辞)。
>
> **订正 ②(本卡 ④ 的判据我自拟的第一版被自己的输出证伪)**:原拟判据 = 「SKILL 内 `grep 件/日` = 0 命中」。
> 实测 = **2 命中**,但两条**都是禁令句**(「把靶从『件/日那一列』扩到三元本身」/「禁用『件/日』作对照单位」)
> ⇒ **判据自身写错了**:该要的是「**无『把件/日用作对照单位』的命中**」,不是「该串 0 命中」——
> 禁令句里必然出现被禁的单位名。**判据已按此收窄并重跑**;若照原判据报,会因**自己的禁令**判自己失败。
>
> **可复算命令 + 预期输出**(承本 sprint DoD「无命令的件不计 done」):
>
> ```bash
> grep -n "记录三元\|追加率\|复杂度分布" .claude/skills/sprint-status/SKILL.md
> #   预期:≥3 命中,全部落在 §4b(实测 8 命中,行 131/133/138/141/144/158/159/162)
> grep -n "件/日" .claude/skills/sprint-status/SKILL.md
> #   预期:命中**仅**禁令句(实测 2 命中 = 行 166/167,均为禁用声明)
> grep -n "记录三元\|复杂度分布\|追加率" production/CAPACITY-MODEL.md
> #   预期:多处命中,至少覆盖 头注 / §二·补二 表头 / §3.1 / §六 #2–#3(实测 15 命中)
> ```
>
> **同批登记(不在本件面内)**:`sprint-16.md` 本节上方 Capacity 行的「**件/日基线**」字样
> 是同一废除单位的残留 ⇒ 归 **S16-2**(其面已含「四处口径同值」)。

> ### ⚠️ S16-3 的「P0 面」判定 —— 边界须用户认(本 sprint 的自我约束)
>
> 92 条里「是否 P0 面」**本身是一个新裁决面**。本 sprint **只判可机械判定的三条规则**:
> ① 触发条件引用 **P0 面判据**(M3/M4 出口、P0 AC)⇒ P0;
> ② 触发条件明写 **P1a / P1b / 外部 / 数值轮** ⇒ 非 P0;
> ③ 其余 ⇒ **单列「待裁」,不自行裁**。
> ⚠️ **不得**为让 M3 第二条合取「好看」而把待裁项判成非 P0。

> ### ✅ S16-3 收口注(2026-10-20 · **含三处口径订正**,禁借绿)
>
> 🔁 **2026-10-21 后续(用户裁定「回填,升级为 94」)**:本节及以下各处的 `91` **是 S16-3
> 交货时的数**,原文不删。三条 `TD-gate-01/02/03` 已回填进 `TD-INDEX` 索引块,债册**现役
> = 94 行**(`open 70 / partial 5 / closed 19` · `P0 面 {Y:87, N:7}`)。**`未闭 P0 面 = 72`
> 一位不动**(三条皆治理债,`P0 面 = N`,不进 M3 出口)⇒ **M3 判定不变**。
> 读本节的 `91` 请按「交货时数」理解;现役真值见 `docs/tech-debt-register.md` §★。
>
> **落地** = `docs/tech-debt-register.md` **§★ 总索引**(91 行索引表 · 三列 `归属 epic / 状态 / P0 面`
> + 求值命令 · 夹在 `TD-INDEX` 标记间)+ **§六 八道门清偿载体表**(G-1…G-8 → TD → 归属 epic)。
> **下方各日期小节零改动**,头部原「待办」块**就地加注**(原文保留)。
>
> **用户四项裁定(2026-10-20)**:① **宽判据**(归属 P0 epic 即算)· ② **总索引表**(非逐表加列)·
> ③ 索引表**声明「归属 epic」列** · ④ **采纳 91**(基线 92 保留不动,加注口径)。
>
> **订正 ①(本卡判据里的「92 条」不成立)**:原写「实测 **92 条 / ~17 债型表**」——
> 92 是**全文串扫**口径:`TD-audio-01` 是**族引用**(正文指 `01a…01f`),**多算 1**;
> `TD-epic-01-a…-e` 五个带后缀号被**截断**,**少算 5**;净 +1。**严格口径 = 91**
> (`###`/`####` 标题 ∪ 表格行首列)。⚠️ 且「~17 债型表」亦不准 —— 含 TD 行的表**恰 20 张**。
>
> **订正 ②(宽判据**如实执行**的结果 ≠ 预览)**:裁定选项预览写「R1 命中 **91 条全中**」,
> **实测 87 Y / 2 N / 2 待裁** —— ⚠️ 这是 **P0 面列的分票**(静态属性);**出口量另算 = 72(未闭)**,见订正 ④。差 4 = **2 条 R2**(`TD-net-01` 归属系统 45 = **P1b** ·
> `TD-prescription-05` 归属 49 = **P2** —— 有优先级、无 epic ⇒ 判 **N**)+
> **2 条 R3**(`TD-s13-01` / `TD-s13-03` 跨 epic 治理 ⇒ 无归属 epic ⇒ **待裁** ——
> **后经同日用户裁定改判 `N`,`待裁` 桶清零,见下方收口补记**)。
> ⇒ **预览的「全中」是未逐条比对的估值,不是口径差异**;宽判据**未收紧**。
>
> **订正 ③(「状态」列不可机械复算 —— 如实登记)**:本卡原拟「三条可机械规则」,
> 实测**「含 `✅` 即 closed」会把 5 处非结案语境读成结案**
> (`TD-clinic-12`「✅ 已核对:不构成缺口」·`TD-audio-103`「✅ 阻塞源改口」·
> `TD-audio-028/030/033`「✅ 评审实测…」)⇒ **91 行的状态为逐条读原文判定**,
> 并在 §★ 总索引 §三 明写「与本表不符时以原条原文为准」。
> **这是本表唯一不能机械复算的列** —— 它**不构成借绿**,但**第三方复核对象是原文,不是该列**。
>
> **可复算命令 + 预期输出**(承本 sprint DoD「无命令的件不计 done」):
>
> ```bash
> python3 - <<'PYEVAL'
> import re, collections
> txt = open('docs/tech-debt-register.md', encoding='utf-8').read()
> blk = re.search(r'<!-- TD-INDEX:BEGIN -->(.*?)<!-- TD-INDEX:END -->', txt, re.S).group(1)
> rows = [[c.strip() for c in l.strip().strip('|').split('|')]
>         for l in blk.split('\n') if l.startswith('| `TD-')]
> st = collections.Counter(r[2] for r in rows)
> p  = collections.Counter(r[3].strip('*') for r in rows)
> live = [r for r in rows if r[2] != 'closed' and r[3].strip('*') == 'Y']   # ⚠️ 未闭才算出口
> ep = collections.Counter(r[1].strip('`') for r in live)
> print('条目', len(rows), '| 状态', dict(st), '| P0 面', dict(p))
> print('未闭 P0 面 =', len(live))
> print('M3 Exit 第二条合取 ⇒', '真' if not live else f'假 —— {len(live)} 条,分布在 {len(ep)}/31 个 epic')
> for k, v in ep.most_common():
>     print(f'    {k}: {v}')
> PYEVAL
> #   实测输出(2026-10-20,逐字):
> #   条目 91 | 状态 {'open': 70, 'partial': 5, 'closed': 16} | P0 面 {'Y': 87, 'N': 4}
> #   （⚠️ 2026-10-20 待裁 4 条裁定后:原 `'N': 2, '待裁': 2}` → 现 `'N': 4`;出口量 72 一位不变）
> #   未闭 P0 面 = 72
> #   M3 Exit 第二条合取 ⇒ 假 —— 72 条,分布在 12/31 个 epic
> grep -n "TD-INDEX:BEGIN\|^## ★ 总索引" docs/tech-debt-register.md
> #   预期:≥2 命中(节标题 + 表标记),实测 = 2
> ```
>
> **⚠️ 该输出不是绿** —— 它把 M3 的出口量**如实显示为 72 条**。
> **~~待裁清单(4 条,交用户裁,本件不自行裁)~~**:`TD-net-01`(义务级 P0 · 归属系统级 P1b · 无 epic,
> **三重错位**)· `TD-prescription-05`(归 49 = P2)· `TD-s13-01` / `TD-s13-03`(跨 epic 治理)。
> ⇒ ~~**M3 第二步判定的分母须并列记 `72(Y,未闭) + 3(待说明)`**。~~
>
> **✅ 收口补记(2026-10-20 · 四条已裁,原文保留留痕)** —— 交用户裁后**全取推荐 ⇒ 四条俱判 `N`**:
> | 条 | 判定 | 理由 |
> |---|---|---|
> | `TD-net-01` | **N** | 归属优先 —— 归属不是 31 个 P0 epic 之一。⚠️ **只回答「算不算 M3 出口」;P0 预埋义务仍有效,仍以 `open` 跟踪** |
> | `TD-prescription-05` | **N** | 49 = P2;且 `AC-11-13` 已按豁免口径转 ✅,**已不卡任何 P0 AC** |
> | `TD-s13-01` / `TD-s13-03` | **N** | 治理债 ≠ 产品/架构债;M3 Exit 是系统机制面 |
>
> ⇒ **M3 第二步的分母 = 单一的 `72`,不再带 `+ 3` 尾巴**(**数一位不变**);
> `P0 面` 列自此**无 `待裁` 值**,全 91 行取值封闭、可机械复算无残留分支。
> 裁法与理由全件见 `docs/tech-debt-register.md` §一 ✅ 块 + §六 尾注。
>
> **订货 ④(我自己的求值命令初版有 bug —— 已修,留痕)**:初版缺 `r[2] != 'closed'` 过滤,
> 把**已闭的 15 条**(`TD-s13-04` · `TD-prescription-01…04` · `TD-audio-023/024` · `TD-audio-01a…01f` ·
> `TD-audio-106` · `TD-player-01`)也计入出口 ⇒ 报 **87**。
> **方向是保守(多报 15 条未偿),不构成借绿;但口径错就是错。** ⇒ **出口量 = 72**,epic 数 **12**。

> ### ✅ S16-4 收口注(2026-10-20 · 门已落 · 证伪 3/3 · 零回归)
>
> **交付件**:
> - 新 `unity/Assets/Editor.Tools.Gates/FsWriteBoundaryGate.cs`(+`.meta`),接进 `AssemblyGates.RunAll`;
> - 新 `unity/Assets/Tests/EditMode/DataPipeline/fs_write_boundary_gate_test.cs`(+`.meta`,**17 条**);
> - 新 `tools/falsify/specs/s16-4-fs-write-boundary.json`(F1–F3);
> - 改 `unity/Assets/Tests/PlayMode/{u1_scene_spikes_test,story007_phase_spike_playmode_test}.cs`
>   —— **删掉 `Assets/` 兜底候选**(原文不删、就地加注);
> - 改 `.claude/rules/test-standards.md` 第 3 条 —— 原文保留,就地加注「现由机械门执行」。
>
> **DoD ① 判据(grep + 白名单)的实际落法 = 两条规则,不是一张 grep 名单**:
> **R1** 写盘第一实参**直接是字面量** ⇒ 红;**R2** 文件含写盘调用**且**含仓库**代码锚点**
> (`Application.dataPath` / `Directory.GetCurrentDirectory` / `Application.streamingAssetsPath`)
> ⇒ 须在 `Declarations` 里**具名声明并写理由**;声明过期 / 悬空同样红。
> 扫描面 = `Assets/Tests/**/*.cs`(用户裁定「仅测试树」);工具侧 35 处 `*.cooked` 写盘
> **刻意不扫**(否则只能整目录白名单 = 自我豁免)。
>
> **两次实测输出(可复算)**:
> ```
> $ grep -rE "<写盘 API 名单>" unity/Assets/Tests --include=*.cs | wc -l
> 131                      # 上界:raw grep,含注释与夹具字符串
> $ 声明表条目数
> 6
> ```
> **门当天抓到 2 处活违规**(不是为将来立规矩):`u1` / `story007` 的 `Assets/` 兜底候选
> 会把尖刺结果写成 **Unity 资产**。两处已删候选;`ls unity/Assets/*_results.txt` ⇒ 无此文件。
>
> **DoD ②(证伪轮)F1 / F2 / F3 三条全部 `exactRed`**(转红集合**恰等于**预期集合),
> 驱动退出码 **0**,SHA-256 还原校验通过,指纹未变;`grep -c FALSIFY-F` = **0 / 0**。
>
> **回归(带基线溯源三元组 · 见下条订正)** —— 截至 `2026-10-20 23:45 CST` · 批次
> `sprint-16 / S16-4 / 全量 EditMode` · commit `f6cdaaf`:
> `total 4071 · passed 4035 · failed 0 · skipped 36`(`unity/Logs/s16-4-full-editmode.xml`)。
>
> **⚠️ 基线订正(QA plan 的 `3940` 已过期,禁借绿)**:QA plan §Smoke 记的
> `3940 / 3904 / 0 / 36` 是**规划时刻**的快照;同日下午 clinic-machine story-001 修复轮
> 先期落了 `4041`(18:44)→ **`4054`**(19:10)。⇒ **正确对照基线 = `4054 / 4018 / 0 / 36`**,
> **Δ = +17 / +17 / 0 / 0**。若拿 `3940` 当对照,Δ 会是 `+131`,**114 条差额会被误读成
> 本门新增或被掩盖的回归** —— 这正是 `feedback_borrowed_green_regression_counts` 记的
> 第一形态(照抄陈旧回归数当「含新测的全量」)。**差集实测**:新增 17 条全为
> `FsWriteBoundaryGateTest.*`,**消失 0 条**(排除「新增 N 同时坏 M,净增恰好对得上」的掩盖)。

> ### ✅ S16-5 收口注(2026-10-21 · 四户刷账 + 状态改判)
>
> **改法(用户 2026-10-21 裁定)**:**刷分母 + 状态改判为未 Complete**。
> 每份 EPIC.md **插入新 `**Status**` 行**于原行之上;原行改标 `**Status(历史 · 原文保留)**`,
> **内容一字未删**(承「原文不删,就地加注」)。派生论断三处同步加注
> (emergency §Epic Status / world §Epic Status + §Next Step / skeuo §里程碑归属)。
>
> | epic | 刷前 | 刷后 | 实际文件数 | 变更 |
> |---|---|---|---|---|
> | `emergency-procedures` | `Complete ✅(7/7)` | 🔁 **未 Complete —— 8/9** | 9 | 分母 7→9 + 状态改判 |
> | `patient-ai` | `✅ 4/4 story 全闭` | 🔁 **未 Complete —— 4/5** | 5 | 分母 4→5 + 状态改判 |
> | `skeuomorphic-ui` | `Complete ✅(18/18)` | 🔁 **未 Complete —— 24/25** | 25 | 分母 18→25 + 状态改判 |
> | `world-ecozones` | `Complete ✅(6/6)` | 🔁 **未 Complete —— 5/6** | 6 | **分子 6→5**(分母本已对) |
>
> **AC ① 实测(分母 = 实际 story 文件数)**:
> ```
> $ for d in ...; do echo "$d 文件=$(ls production/epics/$d/story-*.md|wc -l) 分数=<header>"; done
> emergency-procedures  文件=9   分数=8/9   ✅
> patient-ai            文件=5   分数=4/5   ✅
> skeuomorphic-ui       文件=25  分数=24/25 ✅
> world-ecozones        文件=6   分数=5/6   ✅
> ```
>
> **AC ② 实测(与 S16-2 的 18/31 同源)**:
> ```
> EPIC.md 直接读出  → 全 Complete epic: 18 / 31
> story 逐条归并    → 全 Complete epic: 18 / 31    非 Complete 13 户名单逐一相同
> 改前 EPIC.md 读出 = 22 / 31   ← 22 与 18 的差 4 户,正是本表四户
> ```
> ⇒ **「由 EPIC.md 直接读出同一结论」已成立**。
>
> **⚠️ 判据设计上的一次险情(留痕)**:首版复算脚本对「**无分数**的 Status 行」
> (`input-system` / `interaction-system` / `skill-system` 只写 `Complete`,audio-system 写
> 「001–018 全 `Complete`」)**不与「有分数且不等」同行处理** ⇒ 它误报 18,而真因是
> 「按分数判 = 22 且漏掉 4 个「Complete 无分数」户」。**两个错误恰好抵消成 18** ——
> 若当时直接签,得到的是**巧合正确**。修正为「有分数 ⇒ 以分数为准;无分数 ⇒ 看状态词」后
> 基线实测 **22**,再刷账后 **18**,才是可解释的链。
> **教训**:判据的输出**等于预期值**不等于判据**正确**,必须能解释「改前是多少、为何变」。
>
> **零 story 状态变更**:`git status --porcelain production/epics/*/story-*.md` = **空**。

## Carryover from Previous Sprint

| Task | Reason | New Estimate |
|------|--------|-------------|
| — | sprint-15 无 carryover(7 done + 1 `removed`,S15-6 已移出为 `TD-listen-01`) | — |

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| **含 2 个 L 件(S16-2 / S16-3)同 sprint** | **高** | 中 | 各自**独占一批**;若 S16-3 的「P0 面」判定需逐条裁 ⇒ **立即降为「登记待裁」**并让 S16-2 先收 |
| **S16-2 的自我指涉** —— 被合并的三份件之一是**承载本件序列表的文档**(`p0-completion-plan` 本身) | 中 | 中 | **先定合并落点再动手**;合并后须重跑「三份件只剩一份」的实测;规划件 §三 随之搬家(→ 路线图 **§6.5**,本 sprint 同批回写) |
| S16-3 的「P0 面」是新裁决面,易被顺手裁 | 高 | 中 | 见上方自我约束;**待裁项须进 §六 型清单交用户**,不进「已裁」。✅ **2026-10-20 已按此执行** —— 判据本身交用户裁(四项),4 条判不出的(`TD-net-01` / `TD-prescription-05` / `TD-s13-01` / `TD-s13-03`)登记为 **R2=`N` / R3=`待裁`** 并列入待裁清单,**未混进 Y**。**⇒ 同日交裁后 4 条全判 `N`,`待裁` 桶清零**(见上方收口补记) |
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
- [x] **零 story 转 `Complete`** —— 本 sprint 不产生任何 story 状态变更
  ⇒ ✅ **2026-10-21 复核**:S16-4 / S16-5 均未碰 story。S16-5 改的是 **EPIC.md 的 epic 级
  `Status` 词与分母**(四户),`git status --porcelain production/epics/*/story-*.md` = **空**
- [ ] 四处分数**同值**:`226` / `182` / `44` / `18-31`;债册 **92 条**两列齐
  → 🔁 **2026-10-20(S16-3 收口):本行后半已兑现,但数字订正为 `91`** ——
  三列(`归属 epic / 状态 / P0 面`)落于债册 **§★ 总索引**(91 行);**92 是全文串扫口径**,
  严格口径 = 91(详见上方 S16-3 收口注 订正 ①)。**原文保留不删**。
  ⚠️ 且「两列」实为**三列** —— 用户裁定增「归属 epic」列作 R1 的判据载体。
  → 🔁 **2026-10-21(回填):现役 = `94` 行**。三条 `TD-gate-01/02/03` 已入索引块;
  `未闭 P0 面 = 72` 一位不动 ⇒ **M3 判定不变**。`91` 为准入时的交货数,原文保留。
- [x] S16-4 的机械门过**证伪轮**(改坏生产 ⇒ 门转红)
  ⇒ ✅ **2026-10-20 已兑现**:F1 / F2 / F3 **三条全部 `exactRed`**,驱动退出码 0,
  SHA-256 还原校验通过、指纹未变、变异残留 0;**全量 EditMode 零回归**
  (`4071 / 4035 / 0 / 36`,对照 `4054 / 4018 / 0 / 36`,Δ = +17 = 新增测数,消失 0)。
  证据原件:`production/qa/evidence/impl-s16-4-fs-write-boundary-2026-10-20.md`
- [ ] S16-2 后「静态件只剩一份」有实测命令输出
- [ ] QA plan 存在:`production/qa/qa-plan-sprint-16-2026-10-20.md`
- [ ] Smoke check 记录:`/smoke-check sprint`
- [ ] QA sign-off:`/team-qa sprint`
- [ ] 规划件 `p0-completion-plan-2026-10-20.md` §三 与 `TD-epic-01` 行**同步回写**(件集变更 + 移出去向)⇒ ✅ **S16-2 已兑现**:件集变更与移出去向落于路线图 **§6.5.1**(含 🔁 四项调整表 + 移出与去向表 + 同批订正两处数字);`TD-epic-01` 的工序重定归 sprint-19
- [ ] 无 S1 / S2 bug

### 🔁 条件结清:C-1 / C-3(2026-10-21 · 承 QA 签核 §7)

> **来源**:`production/qa/qa-signoff-sprint-16-2026-10-21.md` §7 的条件 C-1 与 C-3。
> 签核判定 = **APPROVED WITH CONDITIONS**,两条条件现已结清。

| 条件 | 内容 | 状态 | 判据 |
|---|---|---|---|
| **C-1** | 给 `AssemblyGates.RunAll` 门链补**反向判据**(`TD-gate-01`) | ✅ **已结清** | 见下 |
| **C-3** | `milestones/README.md:582-583` 基线表刷新为带三元组的当前值 | ✅ **已结清** | 见下 |

**C-1 —— 判据已落且双向可证**

- 新件:`unity/Assets/Tests/EditMode/Sim/gate_chain_source.cs`(解析器)+
  `gate_wiring_manifest_test.cs`(**手写**清单 + 14 条断言)
- **形态偏离原登记**:原文要求在 `assembly_gate_runall_test.cs` 加两条断言;实做改为**独立新件** ——
  判据若与「删一行」的变异同装配,递归没有底(它自己被删的那次,也正好是它不再运行的那次)
- 证伪三向全 `exactRed`:**F1 删 `AssemblyGates.cs:130` ⇒ 3 条红**(S16-4 当时同一变异
  **17/17 全绿**)· **F2 删清单一条 ⇒ 1 条红**(证明双向)· **F3 停用解析器 ⇒ 5 条红**(证明非 `0==0`)
- 全量 EditMode `4085 / 4049 / 0 / 36`(基线 `4071 / 4035 / 0 / 36`),`Δ = +14/+14/0/0`,
  差集**新增 14 / 消失 0**
- ⚠️ **`:1586` 那类限定语(「不得写成已生效」)现解除** —— `FsWriteBoundaryGate` 可写「已生效」
- 证据原件:`production/qa/evidence/impl-c1-gate-wiring-2026-10-21.md`

**C-3 —— 基线表已刷(原文不删,新旧并列)**

| 套件 | 旧值 | 新值 | 截至时刻 · 批次 · commit |
|---|---|---|---|
| EditMode | `3401/3355/0/46` | **`4085/4049/0/36`** | `2026-10-21 01:03 CST` · C-1 实现轮 · `f6cdaaf` |
| PlayMode | `186/186/0` | **`197/197/0/0`** | `2026-10-21 00:22 CST` · sprint-16 收口轮 · `f6cdaaf` |

⚠️ **两条值的时点不同**(PlayMode 早于 EditMode);C-1 零运行时改动故不受影响 ——
**但这是判断不是实测**,若下游需要「C-1 之后」的 PlayMode 证据须另跑。

**未结(不在本 sprint 面内)**:

- ~~`EcozoneIntegerGates`(**AC-6-22 BLOCKING**)/ `EmergencyIntegerGates`(**AC-10-02/03 BLOCKING**)
  是 BLOCKING 级 AC 却**零构建期强制点** —— **该不该接 BuildGate 须另裁**(已具名登记,不擅自接)~~
  → ✅ **2026-10-21 已裁并已接线**(用户裁定「接 —— 复刻 b5 先例」)。两门现同时在
  `RunMenu` 与 `OnPreprocessBuild` 上,守卫 (`scriptCompilationFailed`) 住在门自己的 `RunAll()` 内。
  🔴 **注意:我最初给用户的「不接」理由是错的** —— 说「BuildGate 无等价守卫」,实测
  `RunAudioBoundary` 早在里面且守卫就在其内部;原文写的是「放不进 **reload 路径**」(对的),
  我读成了「放不进 **BuildGate**」。完整留痕见债册 §TD-gate 结案记录。
- ~~`TD-gate-03`(falsify 残留词表盲区 + 并发读取)**仍 open**,且本轮两个盲区**双双再中** ⇒ 另开批次~~
  → ✅ **2026-10-21 已结**:① 经读代码证伪(驱动无词表,判据是 SHA-256);② 纪律已写入 `tools/falsify/README.md`
- ~~`docs/tech-debt-register.md` §四 总索引未回填这三条 `TD-gate`;求值命令实测仍 `91 / 72`(未污染)~~
  → ✅ **2026-10-21 已回填并升版 `94`**(`未闭 P0 面 = 72` 一位不动)

⇒ **本 sprint 的 5 条签核条件至此全部结清**(`conditions_open: []`)。

---

> **⚠️ 本 sprint 无 QA plan 以外的人工验收面** —— 5 件全部是纸面 + 门,
> **唯一**需要跑 Unity 的是 S16-4 的证伪轮与全量回归。承 `production/qa/qa-plan-sprint-16-2026-10-20.md`。
>
> **不经用户指令不 commit。**
