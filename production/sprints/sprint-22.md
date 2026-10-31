# Sprint 22 — 2026-10-31 起(不设结束日期,做完即收 · 承 sprint-12…21 同例)

> **三元组**:规划轮截至 **`2026-10-31`** · 批次 **`sprint-22 规划`** · commit **`218bd758`**(+ 本批未提交:
> `production/qa/smoke-carrier-backlog-2026-10-31.md` 与本文件)。
>
> **规划依据**:`production/milestones/p0-sweep-2026-10-29.md` §二(26 件归位)· §五(排期插入)· §六(sprint 形状)
> · `p0-remaining-roadmap-2026-10-20.md` §6.5.2(硬序/自由序)· §6.5.3(清债总体规划)
> · `production/CAPACITY-MODEL.md` §3.1(记录三元)· §3.2(约束三条)· §3.3(追加额度两分)
> · `production/qa/smoke-carrier-backlog-2026-10-31.md`(本轮**新建**)。
>
> ⚠️ **开工前置(不是建议,是判据)**:本 sprint **无 QA 计划**。承 sprint-21 同例,
> **`S22-1` 开工之前须跑 `/qa-plan sprint`**,产物落 `production/qa/qa-plan-sprint-22-<date>.md`。
> 未补 QA 计划即开工 = 违反本卡自身的前置。
>
> ⚠️ **取件前置(承 `/sprint-plan` Phase 1 硬前置)**:每件开工前**重读**该 epic 的 `EPIC.md` §Epic Status
> + story 的 `Status:` 行,**不得**照抄本文件的快照。本文件的件集已于 2026-10-31 重扫过
> (§一 的复算命令),但那是**规划时点**的读数。

---

## 〇、与总体规划的对账(先对账,再排卡)

**路线图 `p0-remaining-roadmap-2026-10-20.md` §6.5 的 sprint-22 原行(逐字)**:

| sprint | 段 | 件集 | 前置 | Exit 判据 |
|---|---|---|---|---|
| **sprint-22** | **容量簇** | `inventory-003` · `inventory-004` · `foraging-004` · `foraging-005` | sprint-21 | **`CanCarry` 采前闸接通** |

**三处对账结论**:

1. **件集已缩为 2 件**(现件集以 `p0-sweep-2026-10-29.md` §六 为准,承 §7 SP-11)——
   `inventory-003` / `inventory-004` **均已 `Complete`**:

   ```bash
   for f in production/epics/inventory-items/story-00{3,4}*.md; do
     printf "%s: " "$(basename $f)"; grep -m1 -o "Status\*\*: [A-Za-z ]*" "$f"; done
   # 实跑:story-003-…  Status**: Complete
   #       story-004-…  Status**: Complete
   ```

   ⇒ **路线图口径的 sprint-22 只剩 `foraging-004` + `foraging-005`**。

2. **路线图口径的 Exit 判据 = 一条:「`CanCarry` 采前闸接通」**,由 **`S22-1`(采前闸)+ `S22-2`(满载走 `CanCarry`)** 兑现。
   **本 sprint 的另外 6 件(`S22-3` … `S22-8`)不计入该 Exit** —— 它们是
   **计划内新增的工作项**(smoke 载体 / 搭车债),属「容量填充」,**不得**写成「Exit 已超额达成」。
   这是本文件与路线图之间**唯一的差集**,在此显式登记(承 §6.5 开头「本节主张顺序,不主张件集」)。

3. **序位性质与硬序检查**:sprint-22 属 **自由序段**(§6.5.2「sprint-20→22 可任意交错」),
   不在「sprint-21 / sprint-26 两簇不得拆」的约束面内;其前置 sprint-21 **已收口**;
   且 **sprint-26 的前置含 sprint-22** ⇒ 本轮不得晚于 26 开工。**三条硬序均未违反。**

> ⚠️ **为什么不把 6 件追加写成「路线图要求」**:路线图从未要求它们。把它们记成「22 的 Exit」 =
> 用**计划外工作**冒领**计划内判据**,是 F7 借绿的同族形态。

---

## Sprint Goal

**采集链收尾 + 载体补全。** 把路线图 §6.5 自由序段里属于 22 的两件 foraging 做完
(`story-004` / `story-005`,全链其余四件已 `Complete`);同时把 smoke 22 条里**本轮真的动得了**的两条
(`#1` 启动序 · `#14` 52 抽池)从「无载体」变成「有判据」;并把两条**搭车债**顺手清掉。
**本轮不新开实现层故事链** —— `#10` 受 2026-10-11 的 G4.1 裁定约束、`#13` 无承接者,
两条各出一张**裁定 / 勘察卡**,不擅自开工。

---

## 一、分母实测(禁照抄规划期算术)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 - <<'PY'
import re, glob, os, collections
eps = [d for d in os.listdir('production/epics')
       if os.path.isdir('production/epics/'+d) and d != 'level-tool']
c = collections.Counter(); n = 0
for e in eps:
    for f in glob.glob(f'production/epics/{e}/story-*.md'):
        t = open(f, encoding='utf-8').read()
        s = re.search(r'^>?\s*\*\*Status\*\*:?[: ]*(.+)$', t, re.M)
        s = s.group(1).strip() if s else '?'; n += 1
        for k in ('Complete','In Review','In Progress','Pending','Ready'):
            if k in s: c[k] += 1; break
        else: c['其他'] += 1
print('epic', len(eps), 'story', n, dict(c))
PY
python3 tools/debtcount/debtcount.py --print | tail -4
python3 - <<'PY'
import json
d = json.load(open('tests/smoke/critical_paths.json'))
c = {}
for it in d['items']: c[it['status']] = c.get(it['status'], 0) + 1
print('smoke', len(d['items']), c)
PY
```

**实跑输出(2026-10-31 规划轮)**:

| 量 | 实测 | 与上一真源对比 |
|---|---|---|
| P0 epic | **31**(+1 `level-tool` 不计) | `p0-sweep` §1.1 同值 ✅ |
| P0 story | **230** | `p0-sweep` 记 229 ⇒ **+1**(新立 `skeuomorphic-ui/story-026-subtitle-rendering.md`) |
| 已完成 | **205** | 规划期 203 ⇒ **+2**:`clinic-machine/001`(`In Review`→`Complete`,C-1)· `inventory-items/002`(`Ready`→`Complete`,S21-2)。**逐件复算**见下方命令 |
| **未闭** | **25**(Ready 23 · In Progress 1 · Pending 1) | `p0-sweep` 记 26 ⇒ **−1** = 上两件结案 `−2` + 新立 `skeuomorphic-ui/026` `+1` |
| 债册条目 | **142**(`closed` 124 · `open` 14 · `partial` 3 · `done` 1) | — |
| **未闭 P0 面** | **0** | M3 Exit 第二条合取仍为**真** |
| smoke 22 条 | `test` **11** · `manual` **3** · **`none` 8** | 收口轮 `test 0` ⇒ **+11**;`none` 由 12 降到 8 |

**「+2 已完成」的逐件复算(与 `p0-sweep` 的基线 commit `da578832` 对拍)**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 - <<'PY'
import subprocess, re, os
base = 'da578832'
files = subprocess.run(['git','ls-tree','-r','--name-only',base,'production/epics'],
                       capture_output=True, text=True).stdout.split()
files = [f for f in files if re.search(r'story-\d+.*\.md$', f) and '/level-tool/' not in f]
def st(t):
    m = re.search(r'^>?\s*\*\*Status\*\*:?[: ]*(.+)$', t, re.M)
    return m.group(1).strip() if m else '?'
for f in files:
    old = st(subprocess.run(['git','show',f'{base}:{f}'],
                            capture_output=True, text=True).stdout)
    new = st(open(f, encoding='utf-8').read()) if os.path.exists(f) else 'MISSING'
    if old != new:
        print(f'{f}\n    {old}  ->  {new}')
PY
# 实跑:恰 2 条 ——
#   clinic-machine/story-001  : In Review -> Complete
#   inventory-items/story-002 : Ready     -> Complete(...)
```

> ⚠️ **「未闭 25」不等于「25 件都能排」** —— 其中 `world-ecozones/005` 是 **EXTERNAL(Pending)**、
> `emergency/008` 与 `patient-ai/005` 是**外部素材件**,`persistence-service/001` 是 **QA 退回**。
> 逐件归位见 `p0-sweep` §二,本文件不复制。

---

## 二、Capacity(承 `CAPACITY-MODEL.md` §3.1 三元 + §3.2 三条约束 + §3.3 追加两分,**不估天**)

| 元 | 值 |
|---|---|
| **件数** | 计划 **8** 件(Must 4 · Should 2 · Nice 2)|
| **复杂度分布** | Must:`M×3 + S×1` · Should:`S×2` · Nice:`S×2` ⇒ **全轮 M×3 + S×5,L = 0** |
| **追加率** | 预留 **散件 1 件**;**链条额度 = 0**(§3.3 两分口径 —— 本轮**不开新链**,链条额度不启用) |

**三条约束(§3.2)**:
- **并发上限 P = 3** —— 同时在跑 ≤ 3 件。
- **L 件单独占批** —— 本 sprint **无 L 件**。
- **判别力门槛 D** —— 每件必须有「回退即转红」的证伪轮,否则**不计 done**。

**为什么是 8 件**(不按件数凑,按判据面):
- Must 4 件里 2 件是路线图 §6.5 指定的 22 段件(`foraging-004/005`),2 件是 smoke 台账里
  **本轮唯一动得了的载体卡**(`#1` 造载体、`#14` 接线);
- Should / Nice 的 4 件全是**零产品码或单点改动**(裁定 / 勘察 / 方法拆分),不占并发批的能力。

---

## 三、Tasks

### Must Have(关键路径)

| ID | 卡 | Owner | 复杂度 | 依赖 | 验收判据 |
|---|---|---|---|---|---|
| `S22-1` | **`foraging/004`** 节点余量前缀 F-17-3 + 采前闸 | unity-specialist | **M** | 无(001/002/003/006 已 `Complete`) | story 的 AC-17-06 / -17-17 / -17-13 / -17-14·15 逐条判定;采前闸「不足 ⇒ **零事件**」须有**零副作用**夹具(F4:不能只断异常);`AC-17-13` 只做**形状**差分(⚠️ `BLOCKED-BY-OQ-17-2`,禁以合成表冒充平衡) |
| `S22-2` | **`foraging/005`** 动作原子性与失败收敛 | unity-specialist | **M** | `S22-1`(采前闸被本件调用) | 三类失败(满载 / 余量不足 / 超时未裁决)**各自具名**且可区分;打断 = 零效果(无事件、余量不变);DTO 递归反射无「原始品级 / 本可采到」;**三条 `[L]` 走查 AC 挂 `EXTERNAL` 不记绿** |
| `S22-3` | **smoke `#1` 载体**:启动序 E-13 硬失败链 | unity-specialist | **S** | 无 | 一条**真跑** `Addressables.InitializeAsync` + `data-core` 预载的夹具;E-13 分支须有**反例**(预载失败 ⇒ 抛且**不降级**);`critical_paths.json` 的 `status`/`check` 同批改 + **实跑 `run_smoke.py` 看到自动 PASS 数 +1** |
| `S22-4` | **smoke `#14` 载体**:52 急召生成链接线(抽池 → 掷骰 → F1 → `EventArrived`) | unity-specialist | **M** | 无硬前置(夹具须**合成输入**) | 生产调用点存在;**CAP 界内 / 超界拒收 + 明确报错**两侧都有夹具;⚠️ **必须用合成 key 集 + 合成格集驱动** —— 否则「双重空集 ⇒ 恒零写入」会让本条变成**假绿**(见 `RandomEventsDirectorDriver.cs` 文件头) |

### Should Have

| ID | 卡 | Owner | 复杂度 | 依赖 | 验收判据 |
|---|---|---|---|---|---|
| `S22-5` | **`TD-processing-06`** 跨存档往返夹具 | unity-specialist | **S** | 无(`SaveService.Load` 已在盘) | compose → 写盘 → 读回 → 重放,断言 `CompleteTick` 与直算**逐位相同**;⚠️ 若测试内**无法真装配**读路径,本卡转**勘察卡**并如实登记 `NOT-RUN`,**不得**用同进程换池重编码冒充往返 |
| `S22-6` | **smoke `#10` 冲突裁定**:G4.1「急救手动化」vs 「一次动作落两条事件」 | producer + unity-specialist | **S** | 无 | **只出两案,不实现**。两案须各写「动了什么 / 判据怎么判 / 与 ADR-009 Amendment I 的关系」;裁前**不开工任何 `#10` 夹具**(对象不存在 = F2 假绿) |

### Nice to Have

| ID | 卡 | Owner | 复杂度 | 依赖 | 验收判据 |
|---|---|---|---|---|---|
| `S22-7` | **`TD-persist-13`** `SaveService.ParseBody` / `WriteExitSave` 拆到 ≤40 代码行 | unity-specialist | **S** | 无 | 两个方法**实测** ≤ 40 代码行(给扫描命令 + 输出);**≤40 界须有夹具**(`TD-persist-08` 的教训:只拆不钉 = 没证);行为面零回归 |
| `S22-8` | **smoke `#13` 勘察 + 两案**:帐篷(P0 唯一新建例外)的承接者 | producer | **S** | 无 | 查清 GDD `game-concept.md:698` 的裁定原文与 23 的 card 面;出两案(补 story / 登记为已知缺口),**不实现** |

---

## 四、明写不做的事(与前文同等重要)

| 项 | 为什么不做 |
|---|---|
| **`#18` / `#22` 两条 smoke 载体** | `TD-ci-01` **EXTERNAL**(CI 凭证在用户侧)。造夹具会变成「造一个跑不了的绿」 |
| **`#8` 拾取三段式玩家侧接线** | 属系统 1/世界的实现轮,不在本轮排期;`#14` 已占掉本轮唯一的「接线」额度 |
| **`#9` 42 运行期观测** | 需 42 接真 `VitalsDto`,42 的呈现轮未排 |
| **`TD-processing-03`** | 须 **ADR-008 裁「同键 `Craft`」语义** —— 裁定件,不在本轮 |
| **`TD-processing-04`** | `BusyActorCount` **零生产调用方**,无实测压力可造 |
| **`TD-persist-12` / `TD-building-04`** | 均**须裁**(前者两件同批且 `RequestLoad` 生产零调用;后者 ADR-010 义务 12 二选一)。裁定件留给专门轮次,**不与 debt-clearance 混批** |
| **任何 `Complete` epic 的重开** | 承 §6.6 ①:门的清偿载体 = **TD 工作项**,不重开已 `Complete` 的 epic |

---

## 五、Risks

| # | 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|---|
| R1 | **`#14` 变假绿** —— 双空集(空 key 集 / 空地块集)下链路「跑了但恒零写入」,夹具若只断「不抛」就绿了 | **高** | 高(假绿) | 卡面已钉:**必须合成输入**;夹具须同时有「有输入 ⇒ 有事件」正例与「超界 ⇒ 拒收」负例 |
| R2 | **`#1` 找不到可跑的预载路径** —— Addressables 在 EditMode 下无运行期 catalog | 中 | 中 | 优先 PlayMode;若两种都不可跑,本卡**如实登记 `NOT-RUN`**,不造替代断言 |
| R3 | **`S22-2` 依赖 `S22-1`** 形成串行,两件同时占 Must 的 2/4 位 | 高 | 低 | 已知并可接受:`S22-3` / `S22-4` 与之无依赖,可并行推进(P = 3 够用) |
| R4 | **`S22-5` 的读路径不可真装配**(`SaveReader` 类名全仓不存在) | 中 | 低 | 卡面已预置转勘察的分支;转卡时须在 story 记录里**具名**写「为什么不能真装配」 |
| R5 | **foraging 数值未裁**(`GATHER_MUL_CAP` / `OQ-17-2`) | 高 | 中 | `AC-17-13` 只做形状差分;数值归用户数值轮,**不得**以合成表当真实平衡 |

---

## 六、Dependencies on External Factors

- **`TD-ci-01`(CI 凭证)** —— 解锁 `#18` / `#22` / `TD-processing-07` 的验收半。**在用户侧**。
- **37 内容件定稿**(急召 `event_key` 集)· **23/24 的医馆格集** —— `#14` 的**生产**数据源;
  本轮不依赖它们(夹具用合成输入),但它们不回填时 `#14` 的**生产链路**仍是「通电待信」。
- **数值轮**(foraging 的 `GATHER_MUL_CAP` 等) —— 影响 `S22-1` 的 AC-17-13,不影响形状判据。

---

## 七、Definition of Done for this Sprint

- [ ] QA 计划存在(`production/qa/qa-plan-sprint-22-<date>.md`),且**在 `S22-1` 开工之前**
- [ ] Must Have 4 件全部完成,或**逐件具名**留存(留开 = 不记绿)
- [ ] 每件有**证伪轮**记录(「回退即转红」),`grep -rn "FALSIFY-" unity/Assets --include=*.cs` ⇒ **0**
- [ ] 回归两套件全量零失败,且数字带**三元组**(截至时刻 · 批次 · commit)
- [ ] 债册 `python3 tools/debtcount/debtcount.py --check` ⇒ exit **0**;未闭 P0 面**不涨**
- [ ] smoke:因 `S22-3` / `S22-4` 落地的条目,`run_smoke.py` **实跑**后自动 PASS 数**增加**;
      未落地条目**维持 `none`**,不得改 json 充数
- [ ] 台账 `smoke-carrier-backlog-2026-10-31.md` 的对应行**同批更新**(类型列 → 已解除 / 仍缺)
- [ ] smoke-check 判定 **PASS 或 PASS WITH WARNINGS**(理由逐条写明)
- [ ] QA 签核 **APPROVED** 或 **APPROVED WITH CONDITIONS**
- [ ] 收口判定与收口提交**同日**
