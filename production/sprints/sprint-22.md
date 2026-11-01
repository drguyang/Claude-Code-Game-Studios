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

> ✅ **`S22-8` 2026-11-01 收口(COMPLETE —— 交付两案,待用户裁)** —— 只勘察 + 出案,**未实现**:
> 未改 `SlotType` / `build_modules.json` / `world_buildslots.json` / `critical_paths.json`。
> **裁定是完整的,缺的是承接者**:`game-concept.md:697-698`(2026-09-20 改判「P0 允许帐篷这一次新建」)
> 已落到 `modular-building.md:246-257` 规则六(实现形态逐字写死:预置 `tent` 槽 + `StructurePlaced`
> 载荷不变);但 **23 的 `story-001…007` 无一张认领它**。
> **实测成本已很低**:放置五条件(`story-002`)· 写权(`story-003`)· 叠加重建(`story-004`)全在,
> 帐篷只是第 10 个输入 —— 代码 `grep -rniE "\btent\b"` = **0**,槽位 13 个无 tent,模块 9 个无帐篷,
> `SlotType` 闭集六员无 `Tent`。
> **两案**:甲(推荐)—— 补 `modular-building/story-008-tent`:`SlotType.Tent` + 模块条目 + 关卡工具产出
> `tent` 预置槽 + 「放置 ⇒ 恰一条 `StructurePlaced`,骨架外零新事件」夹具,`#13` 转 `PASS(2 载/全绿)`;
> 乙 —— 登记为已知缺口,但那是**正面推翻 2026-09-20 裁定**(它拿掉的正是「P0 也必须能新建」的宾语)。
> 原件 = `production/qa/evidence/s22-8-smoke13-tent-two-options-2026-11-01.md`。
>
> 🔁 **`S22-8` 第二轮 2026-11-01(用户指令「甲案,落地」⇒ COMPLETE · 已实现)** ——
> 上一段的**「未实现」状态就此终结**,原文不删,留作勘察轮的记录。落地件:
> `SlotType.Tent = 7`(闭集 6 → 7)· `build_modules.json` +1(`module_id 10`)·
> `world_buildslots.json` +1(`(12,0,12)`,骨架外)· 两侧重烘(611 B/10 模块 · 372 B/14 槽)·
> `tent_placement_test.cs` **3 条**(读真 cooked 产物)· `story-008-tent` 立卡 · EPIC `7/7 → 8/8`。
> **`#13`: none → test,PASS(3 载/全绿)**;smoke 自动 PASS **14 → 15**,无名 **5 → 4**,exit 仍 3。
> **证伪 4 变异各转红**(2 / 11 / 18 / 7 条),SHA-256 逐字节还原 4/4。
> **回归**:EditMode `6345/6308/0/37`(+4,恰等新增)· PlayMode `273/269/0/2/2`(零变化)
> @ `2026-11-01 10:06:00 CST` · `1825dac7`+工作树。
> ⚠️ **本轮最值得记的一条**:「扩员只改一个枚举」不成立 —— 三条既有**哨兵**当场转红
> (词表字面量 / 掩码上界 127→255 且 `128` 由越界位变合法位 / 目录行数 9→10),
> 每条都**按契约复核后同步常量**,不是改期望值凑绿。
> 原件 = `production/qa/evidence/s22-8-smoke13-tent-implementation-2026-11-01.md`。

> ✅ **`S22-7` 2026-11-01 收口(COMPLETE WITH NOTES)** —— `TD-persist-13` **closed**。
> **开工实测推翻了一半的债**:`ParseBody` **74 代码行**(真超,已拆)· `WriteExitSave` **实测 6**
> (债记的「61」是**度量器假数**:`MethodBody` 只做花括号配平,表达体方法签名行没有 `{`,
> 一路吞到后一个方法)。**同批修了尺子**并给它加自测。
> **拆分**:`ParseBody` **17** + `ParseConfigVersionSegment` **10** + `ParseFoldSegment` **20** +
> `ParsePayloadSegment` **25** + `ReadPayloadEntry` **21**(全 ≤40;校验顺序/报文/游标次序逐字照搬)。
> **判据 4 条**:5 方法 ≤40 · 表达体不吞下一个方法(≤10)· 度量器自测(3 行字面量)· 尾随字节必拒。
> **证伪 4 轮**:M1 第一版**绿**(38 行仍 ≤40 —— 定性为**弱变异**而非假绿,已重做)·
> **M1′**(撤回整套拆分)⇒ 预算判据红 · M2(停用表达体识别)⇒ 2 红 ·
> **M3**(删 `EnsureFullyConsumed()`)⇒ **首次 271 条零红** ⇒ 该守卫**此前零载体**,
> 补 `test_td13_trailingBytesAfterPayloadSegment_areRejected` 后 **M3′ 恰 1 红**。
> **回归**(三元组 `2026-11-01T09:44–09:47 CST` · `S22-7` 批 · `ec7893b2`+未提交):
> EditMode `6341/6304/0/0/37`(**+4 恰等**)· PlayMode `273/269/0/2/2`(零变化)。
> 债册 `--check` exit 0(`closed 127 / open 13`)。
> 原件 = `production/qa/evidence/s22-7-td-persist-13-line-budget-2026-11-01.md`。

> ✅ **`S22-6` 2026-11-01 收口(COMPLETE —— 交付两案,原题「待用户裁」;⚠️ 当日上午已裁并执行,见本块末行)** —— 卡面只要求出案不实现,**已照办**:
> 本轮**未改** `critical_paths.json`、**未开**任何 `#10` 夹具。
> **核心发现:`#10` 记的「生产路径未接线 ⇒ 无判据可挂」与实测不符。** G4.1(2026-10-11)退役的是
> **自动触发**驱动(`EmergencyAttemptDriver._triggeredPatients`),而 `#10` 判据讲的是**手动动作**的
> 事件形状 —— 手动路径今天在(`EmergencyChannelDriver` / `BootRoot.cs:611` 的跳过入口 →
> `EmergencyManualDriver` → `HostEmergencyProcessor:206/:233` 双写,`:212` 主机 Judge 出 potency),
> 且**已有两条真装配载体**(`applied_payload_settlement_test.cs:240` 恰两条 ·
> `emergency_skip_entry_wiring_test.cs:61` `CompositionRoot.Assemble()` 真装配 + 恰两条)。
> **两案**:甲案(推荐,零代码零裁定)—— 判「无冲突」,`#10` 挂既有双载体,只动账本;
> 乙案 —— 把「手动化」读成「一次动作只该留一条」,那要**推翻 ADR-009 Amendment I** 并连带改
> 编解码 / 路由 / 37 结案门 / 9 体征链 / ADR-001 上行,风险高且与本轮未找到的任何文字依据相反。
> 两案均写明「动了什么 / 判据怎么判 / 与 Amendment I 的关系」。
> 原件 = `production/qa/evidence/s22-6-smoke10-conflict-2026-11-01.md`。
> ✅ **用户裁定「甲案,落账本」并已执行**(同日):`#10` `none → test` 挂两条载体 ⇒
> smoke `自动 PASS 13 → 14` · `无名 6 → 5`,退出码仍 **3**;零产品代码、零 ADR 改动。

> ✅ **`S22-4` 2026-11-01 收口(COMPLETE WITH NOTES)** —— smoke `#14` 载体落地,`自动 PASS 12 → 13`。
> **交付**:`Sim/EventDraw.cs`(抽池命中纯函数 `SplitMix64(WorldSeed, win, tier, ordinal) % 候选数`)·
> `Gameplay.Boot/SeedEventAuthority.cs`(52 掷骰的**生产实装**,取代抛形占位)·
> `Gameplay.Boot/RandomEventSpawnChain.cs`(抽池 → F1 上下文门/档配额 → **CAP 预检** → 掷骰 →
> `EventRolled` + `EventArrived` → 病人出现)· 驱动每 tick 调用(异常接住不杀帧,记 `LastSpawnError`)。
> **唯一原子性主张**:CAP 预检排在**第一次 Append 之前** —— 排在之后会留下「掷过骰、事件没到」的孤儿行,
> 读档重放时凭空多一次掷骰(证伪 M1 正面打中)。
> **夹具 6 条,全用合成 key 集 `{7001,7002,7003}` + 合成格 `(9,0,4)`**(卡面硬约束:生产双真空下照生产数据
> 写夹具 = 假绿);判据含「零写入 / 零掷骰 / 零创建」的副作用断言(超界拒收)。
> **证伪 4 变异各 red**:M1 CAP 挪到 Append 后(超界两条红)· M2 空池短路只判 null(仅 C 红)·
> M3 绕开掷骰(仅 D 红)· M4 驱动不调链(E/F 红)。两文件 SHA-256 逐字节还原。
> **⚠️ 首跑全量 6 红 = 门抓住了我**:b7 要求「写者落地同批撤豁免」⇒ 已撤 `EventRolled`/`EventArrived`;
> E3 哨兵扫到新的 `IEventAuthority` 实装 ⇒ `SeedEventAuthority` 实现 `IP0AuthorityPlaceholder`
> (恒 host、零联机语义),哨兵允许集改为**显式双名封闭枚举**。
> **回归**(三元组 `2026-11-01T09:10–09:13 CST` · `S22-4` 批 · `dd9a82d1`+未提交):
> EditMode `6337/6300/0/0/37`(**+6/+6 恰等**)· PlayMode `273/269/0/2/2`(零变化)。
> **smoke 实跑**:`自动 PASS 13 · 无名 6`(原 `12 / 7`);**退出码仍 3**。
> **生产仍是「通电待信」(如实)**:候选池空(归 37 内容件)⇒ 恒 `NoPool` 零写入;
> `spawnPatient` 缝**刻意抛**(急召 key → 37 病种映射不存在)。**未实现**:密度预算 / 权重 / 生成点解析。
> 原件 = `production/qa/evidence/s22-4-smoke14-spawn-chain-2026-11-01.md`。

> ✅ **`S22-3` 2026-11-01 收口(COMPLETE)** —— smoke `#1` 载体落地,`自动 PASS 11 → 12`。
> **原 reason 被证伪**:「启动序需真启动;headless 无载体」—— 前半句对(EditMode 里预检必然判死,
> `pipeline_gate_test.cs:1243` 已钉),**后半句不成立**:PlayMode 有真 catalog,初始化 + 预载真跑得起来。
> **交付**:`PlayMode/boot_e13_preload_chain_test.cs`(2 条)——
> ① 真跑 `InitializeAsync` + `TryPreload` + **反射读 `_items` 私有缓存**断「预载当场已解码」
> (⚠️ 只断 `Load<ItemDataSet>()` **不够**:`EnsureItems()` 惰性,空实现的 `Preload()` 照样能补装);
> ② 装载抛 ⇒ `false` + 无半预载态 + `Provider` **抛**(不返回 null);该 `catch` 分支 EditMode 侧零覆盖。
> **证伪 3 变异各 red**(M1 空实现 Preload ⇒ ①;M2 Provider 返回 null ⇒ ②;M3 catch 吞成 true ⇒ ②),
> 两文件 SHA-256 逐字节还原,`FALSIFY-` 残留 0。
> **工具**:`run_smoke.py --results` 改 `nargs="+"` 支持多份合并(`#1` 在 PlayMode、其余 test 条在 EditMode);
> 单份调用向后兼容(实跑输出与改动前逐字一致)。
> **回归**(三元组 `2026-11-01T08:47–08:50 CST` · `S22-3` 批 · `f15c76a8`+未提交):
> EditMode `6331/6294/0/0/37`(零新增)· PlayMode `273/269/0/2/2`(**+2/+2 恰等**)。
> **smoke 实跑**:`自动 PASS 12 · 无名 7`(原 `11 / 8`);**退出码仍 3** —— 7 条无载体仍在,
> 不得把 INCOMPLETE 读成 PASS。
> 原件 = `production/qa/evidence/s22-3-smoke1-e13-carrier-2026-11-01.md`。

> ✅ **`S22-1` 已于 2026-10-31 收口(COMPLETE WITH NOTES)** —— 交付
> `Sim/Foraging/NodeRemaining.cs`(`PrefixScan` **一次遍历三消费** = 条数 / `Σ_all` / `Σ_window`;
> `YieldDecay` / `EffCapacity` / `Remaining` / `CanHarvest` 采前闸)+ 夹具
> `EditMode/Foraging/node_remaining_prefix_test.cs`(15 测)。
> **AC**:AC-17-17 ✅ · AC-17-14/15 前缀一致性 ✅ · **AC-17-06 🟡 只落谓词半**(「余量不足 ⇒ 零事件」
> 那一跳属 `S22-2` 的动作路径,本件不自造调用方去断言 —— 那是 F1/F4)· **AC-17-13 🟡 仍 `BLOCKED-BY-OQ-17-2`**。
> **证伪 3 条,各 exact 1 红**:M1 `>=`→`>` · M2 `<`→`<=` · M3 删前缀守卫;还原 SHA-256 逐字节一致,
> `FALSIFY-` 残留 0。
> **回归**(三元组 `2026-10-31T13:5x CST` · `S22-1` 批 · `6f6109bb`+未提交):
> EditMode `6239/6202/0/0/37`(基线 `6224/6187` ⇒ **+15/+15 恰等新增**),PlayMode `238/234/0/2/2`。
> **三条如实登记**:① 夹具抓到我的口算错(`decay(Σ_all)` 含本次采集);② **GDD `:352` 写「累计 40 份触底」,
> 按表值算触底在 21** —— 值归数值轮,未改数据;③ `EffCapacity` 的运算次序本夹具**不能区分**,已明写不主张。

> ✅ **`S22-2` 2026-10-31 收口(COMPLETE WITH NOTES)** —— 首轮落闸门,收尾轮补 AC-17-09 / 09b / 10。
> **已落**:`HarvestOutcome.cs`(三支具名 + `HarvestGate`)+ `HarvestOrchestrator.Resolve(request, gate)`,
> 闸门**排在铸号与 `NoveltyTracker.Query` 之前**;夹具 `harvest_action_atomicity_test.cs`(8 测)。
> **AC**:AC-17-07 ✅ · AC-17-07c ✅ · AC-17-08 ✅(结构半边)。
> **AC-17-10 ✅ 同轮补上**:新门 `Editor.Tools.Gates/ForagingFloatGate.cs`(递归扫
> `Assets/Sim/Foraging/**`)+ 接进 `AssemblyGates.RunAll`;夹具 `foraging_float_gate_test.cs`(8 测);
> **探针轮 M4** = 真放一个含 `float` 的文件 ⇒ **exact 1 红**,删文件回绿。
> **AC-17-09 / 09b ✅ 收尾轮补上**:新增 `GatherAffordanceDto`(整数域四字段,零余量数字)+
> `AntiFantasyDtoGate`(递归类型扫描,按名从 `Sim.Contracts` 载入 DTO),**接进 RunAll 与 BuildGate 两条链**;
> AC-17-10 由**文本级升为源级 + IL 级**(Cecil)。证伪 M4 exact 1 红 · M5 2 红。
> **`[L]` 三条仍 `EXTERNAL`(42 的 UX Flag ③ 未交付)⇒ 不勾、不记绿**。
> **证伪 3 条**:M2(闸门移到 `Query` 之后)**exact 1 红** · M3(判定换序)**exact 1 红** ·
> M1(满载分支失效)**3 红超集**(如实记)。还原 SHA-256 一致。
> **回归** `2026-10-31T16:5x–16:59 CST`:`EditMode 6261/6224`(+6/+6 恰等;含首轮 +8)· `PlayMode 238/234`。

> ✅ **`S22-5` 2026-11-01 收口(COMPLETE)** —— `TD-processing-06` **closed**。
> **前提修正**:债写「`SaveReader` 全仓不存在 ⇒ 造样本 = 造不存在的路径」——
> 那是**类名**层面的事实;能力住 `SaveService.Load`(`Sim.Codec/SaveService.cs:807`),
> ADR-034 实现轮后载荷段已落地 ⇒ **能真装配,故不走卡面预置的「转勘察卡」分支**。
> **交付**:`EditMode/PersistenceService/craft_save_roundtrip_test.cs`(1 测)——
> 两 actor 三炉(世界流)→ `Checkpoint` → 新流 + 同目录 `Load` → 24 采样点 × 2 actor
> 逐点断言 `TryCompleteTickOf` / `BusyActorCount` 与直算同值 + **字面量哨兵**(`100+50⇒150`)。
> **证伪 2 条**:M1(组版丢世界流 `Craft`)⇒ 前置条数断言红 `3 vs 0`;
> **M2(信封 tick 整体 +1,行数不变)⇒ B 面单独红、A 面仍绿**(证明等值断言不是附庸)。
> 还原 `SaveComposer.cs` SHA-256 逐字节一致,`FALSIFY-` 残留 0。
> **回归**(三元组 `2026-11-01T08:31–08:33 CST` · `S22-5` 批 · `32e15f3b`+未提交):
> EditMode `6331/6294/0/0/37`(基线 `6330/6293` ⇒ **+1/+1 恰等**),PlayMode `271/267/0/2/2`。
> **不覆盖**:跨构建逐位一致(= `TD-ci-01` EXTERNAL)。
> 原件 = `production/qa/evidence/s22-5-craft-roundtrip-2026-11-01.md`。

### Should Have

| ID | 卡 | Owner | 复杂度 | 依赖 | 验收判据 |
|---|---|---|---|---|---|
| `S22-5` | **`TD-processing-06`** 跨存档往返夹具 | unity-specialist | **S** | 无(`SaveService.Load` 已在盘) | compose → 写盘 → 读回 → 重放,断言 `CompleteTick` 与直算**逐位相同**;⚠️ 若测试内**无法真装配**读路径,本卡转**勘察卡**并如实登记 `NOT-RUN`,**不得**用同进程换池重编码冒充往返 |
| `S22-6` | **smoke `#10` 冲突裁定**:G4.1「急救手动化」vs 「一次动作落两条事件」 | producer + unity-specialist | **S** | 无 | **只出两案,不实现**。两案须各写「动了什么 / 判据怎么判 / 与 ADR-009 Amendment I 的关系」;裁前**不开工任何 `#10` 夹具**(对象不存在 = F2 假绿) |

### Nice to Have

| ID | 卡 | Owner | 复杂度 | 依赖 | 验收判据 |
|---|---|---|---|---|---|
| `S22-7` | **`TD-persist-13`** `SaveService.ParseBody` / `WriteExitSave` 拆到 ≤40 代码行 | unity-specialist | **S** | 无 | 两个方法**实测** ≤ 40 代码行(给扫描命令 + 输出);**≤40 界须有夹具**(`TD-persist-08` 的教训:只拆不钉 = 没证);行为面零回归 |
| `S22-8` | **smoke `#13` 勘察 + 两案**:帐篷(P0 唯一新建例外)的承接者 | producer | **S** | 无 | 查清 GDD `game-concept.md:698` 的裁定原文与 23 的 card 面;出两案(补 story / 登记为已知缺口),**不实现**<br>🔁 **2026-11-01 用户裁定「甲案,落地」⇒ 本卡扩为「勘察 + 落地」**:原验收判据的「不实现」**已被用户裁定覆盖**,不是自行扩权。落地件 = `modular-building/story-008-tent`(`SlotType.Tent = 7` + 两个数据件 + 3 条判据),`#13` none → test |

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

- [x] QA 计划存在(`production/qa/qa-plan-sprint-22-2026-10-31.md`),且**在 `S22-1` 开工之前**(13:43 vs 13:55)
- [x] Must Have 4 件全部完成,或**逐件具名**留存(留开 = 不记绿)—— 4/4 完成,Should 2/2 · Nice 2/2 亦全收
- [x] 每件有**证伪轮**记录(「回退即转红」),`grep -rn "FALSIFY-" unity/Assets --include=*.cs` ⇒ **0**(实测 0;`S22-6` 纯裁定卡零产品码 ⇒ N/A 已注明)
- [x] 回归两套件全量零失败,且数字带**三元组** —— `2026-11-01 10:06:00 CST` · `S22-8` · `1825dac7`+工作树
- [x] 债册 `python3 tools/debtcount/debtcount.py --check` ⇒ exit **0**(实测 0);未闭 P0 面 **0 → 0**(不涨)
- [x] smoke:`S22-3` / `S22-4` / `S22-6` / `S22-8` 落地的条目,`run_smoke.py` **实跑**后自动 PASS 数 **11 → 15**;
      未落地 4 条**维持 `none`**(`[8, 9, 18, 22]`),**未改 json 充数**
- [x] 台账 `smoke-carrier-backlog-2026-10-31.md` 的对应行**同批更新**(类型列 → 已解除 / 仍缺) —— ⚠️ **实际上是收口轮才补的**,见 §收口判定 §二.3
- [x] smoke-check 判定 **PASS WITH WARNINGS**(`production/qa/smoke-2026-11-01.md`,无名 4 条逐条具名)
- [x] QA 签核 **APPROVED WITH CONDITIONS**(`production/qa/evidence/qa-signoff-sprint-22-2026-11-01.md`,C1–C4)
- [x] 收口判定与收口提交**同日**(2026-11-01)

---

### 收口判定(2026-11-01)

**8/8 卡收口 · 判定 = ⚠️ NOT PASS(路线图 Exit 判据未达 —— 1 条)· 其余面全部达标 · 提交同日。**

#### 一、DoD 逐条(§七)

- [x] **QA 计划存在且早于 `S22-1` 开工** —— `production/qa/qa-plan-sprint-22-2026-10-31.md`,
      提交 `6f6109bb` 10-31 **13:43**;`S22-1` 提交 `fa28fc17` **13:55** ⇒ 早 12 分钟。
- [x] **Must 4 件全部完成**(`S22-1`…`S22-4`);Should 2(`S22-5`/`S22-6`)· Nice 2(`S22-7`/`S22-8`)亦全收。
      8/8,零留开。
- [x] **每件有证伪轮** —— 7 件有(3/3+/3/4/2/—/4/4 条变异,`S22-6` = **纯裁定卡零产品码 ⇒ N/A,已逐卡注明**);
      `grep -rn "FALSIFY-" unity/Assets --include=*.cs | wc -l` ⇒ **0**;还原一律 **SHA-256 逐字节**。
- [x] **回归三元组** `2026-11-01 10:06:00 CST` · 批次 `S22-8` · `1825dac7`+工作树(= `04a29f0d`):
      EditMode `6345/6308/0/37` · PlayMode `273/269/0/2/2`,零失败。
- [x] **债册** `--check` exit **0**;`未闭 P0 面` = **0**(规划期亦 0 ⇒ **不涨**)。
      条目 142 → **144**(新增 `TD-foraging-03` **立后即结** · `TD-skeuo-01` 仍 open,`P0 面 = N`),
      `closed 124 → 127` · `open 14 → 13` · `partial 3` 不变。
- [x] **smoke 自动 PASS 增加**:11 → **15**(`#1`/`#10`/`#13`/`#14`);未落地 4 条**维持 `none`**
      (`[8, 9, 18, 22]`),**未改 json 充数**。
- [x] **台账 `smoke-carrier-backlog-2026-10-31.md` 对应行更新** —— ⚠️ **收口轮才补**(见 §二 第 3 条)。
- [x] smoke-check = **PASS WITH WARNINGS**(`production/qa/smoke-2026-11-01.md`,无名 4 条逐条具名)。
- [x] QA 签核 = **APPROVED WITH CONDITIONS**(`production/qa/evidence/qa-signoff-sprint-22-2026-11-01.md`,C1–C4)。
- [x] 收口判定与收口提交**同日**(2026-11-01)。

#### 二、判定表

| 面 | 结果 |
|---|---|
| 卡 | Must **4/4** · Should **2/2** · Nice **2/2** |
| 回归 | EditMode `6345/6308/0/37` · PlayMode `273/269/0/2/2`(零失败;`+4` 恰等新增 4 条) |
| 测试增量 | **+121 passed**(`6224/6187` 起 → `6345/6308`),**含本 sprint 窗口内全部工作**(8 卡 + 间插债清除轮),不是 8 卡单独贡献 |
| 债 | `closed 124 → 127` · `open 14 → 13` · **未闭 P0 面 0 → 0** |
| smoke | `PASS WITH WARNINGS`(自动 PASS 11 → 15;无名 8 → 4) |
| 签核 | `APPROVED WITH CONDITIONS`(C1–C4) |
| **路线图 Exit** | **⚠️ 未达** —— 见 §三 |

#### 三、Exit 判据的实测结论(**本判定为 NOT PASS 的唯一原因**)

路线图 `production/milestones/p0-remaining-roadmap-2026-10-20.md:921` 给 sprint-22 的 Exit:

> 「**`CanCarry` 采前闸接通**」(`:263` 的语境:「`CanCarry` 是**采集动作**的采前闸」)

**已达成的一半**(形状侧,证据齐):

- 采前闸本体:`HarvestOrchestrator.Resolve(request, gate)` 的闸门**排在一切副作用之前**
  (`HarvestOrchestrator.cs:314-317`,`!gate.HasCapacity` → `FullInventory`,`!gate.HasRemaining` → `InsufficientRemaining`,两支具名),夹具 `harvest_action_atomicity_test.cs` 8 条;
- 装配:`new HarvestOrchestrator(` 全仓**恰 1 处**(`CompositionRoot.cs:975`),产出挂
  `CompositionRootServices.Harvest`(`:161`)。

**未达成的一半**(接通侧,零生产):

```bash
grep -rn "\.Resolve(" unity/Assets --include=*.cs | grep -v "/Tests/"     # 零命中
grep -rn "\.Harvest\b"  unity/Assets --include=*.cs | grep -v "/Tests/"     # 零命中
grep -rn "Harvest\|Gather" unity/Assets --include=*.inputactions           # 零命中(无采集 action)
```

⇒ **没有任何生产路径会让闸门被求值**。`HarvestOrchestrator` 是一个**装着服务、没有任何发起方**的孤岛;
测试里仅有的两处 `Resolve` 调用还都传 `HarvestGate.Allow` —— 而生产代码自己的注释写着
「**生产裁决不得传 `HarvestGate.Allow`,那等于跳过采前闸**」(`HarvestOrchestrator.cs:294`)。

**这不是「吹毛求疵」,是项目自己定的口径**:「装配点有、发起方零」在 `TD-building-01` 与 smoke `#14`
上是**同一个判据**,两次都被记成「未通」而不是「已通」。按同一把尺子,sprint-22 的 Exit 未达。

**两条解除路径(须用户裁,本轮不擅自选)**:

| 案 | 内容 | 代价 |
|---|---|---|
| **甲(补接线)** | 补一条生产发起路径:3 的采集 action → 4 的 F-4.1 选目标 → `Resolve(request, 真 gate)`(gate 由 `CarryLoadSolver.CanCarry` + `NodeRemaining.CanHarvest` 求值),并加一条「传 `Allow` 即红」的门 | 一张 `M` 卡(输入侧属 ADR-011 面,须实测 Input System) |
| **乙(改口径)** | 用户裁定 Exit 的「接通」= **闸门本体 + 编排器装配**(现态),发起方登记为 **`TD-foraging-04`**(新债,`P0 面 = Y`) | 零代码,但 Exit 的语义**被收窄**,须逐字记录 |

#### 四、本 sprint 留下来的非绿面(逐条具名,不得被收口判定覆盖)

1. **Exit 判据未达**(§三) —— 采前闸的**发起方为零**,`services.Harvest` 无消费方;
2. `S22-1` 的 **`AC-17-06` 只落谓词半** · **`AC-17-13` `BLOCKED-BY-OQ-17-2`**(数值未裁);
3. **台账行在收口轮才补**(DoD 要求「同批」)—— `#1`/`#14`/`#13` 三行与 §三 统计本来停在规划态;
4. `S22-2` 的 **`[L]` 三条仍 `EXTERNAL`**(42 的 UX Flag ③ 未交付);
5. **smoke 无名 4 条**(`#8`/`#9`/`#18`/`#22`)—— 前两条是生产接线/呈现轮,后两条 `TD-ci-01` EXTERNAL;
6. `TD-skeuo-01` 本 sprint 新立且**仍 open**;
7. 帐篷的 **`cost = 5` / `build_item_key = 8` 是临时值**(归数值轮,已在 EPIC §未闭项具名)。

> ⚠️ **本判定不把「8/8 卡收口」当 Exit** —— 恰是 §〇 第 2 条预先禁止的形状:
> 6 件追加工作属「容量填充」,把它们记成 Exit 就是用计划外工作冒领计划内判据(F7 借绿同族)。

#### 收口判定复评(2026-11-01 下午 · 用户指令「复评 sprint-22 收口判定」)

**判定:⚠️ NOT PASS → ✅ PASS**(上方原判定全文不删、效力不变 —— 它记录的是
10:25 时点的实测;本节记录的是甲案执行后的复评)。

- **唯一原因(§三 Exit 未达)已结清**:甲案(补接线)经两 commit 落地
  (`cd935295` driver + 机械门 · `a956f0c1` BootRoot 调用接线),
  原判定 §三 三条复算命令今日重跑:`.Resolve(` 生产命中恰 1(`HarvestInteractionDriver.cs:254`);
  `.Harvest\b` 仍零命中属**命名伪象**(发起链 = `TryHarvest` / `HarvestDriver`,新命令见复评件 §3.2);
  inputactions 无专用采集 action 属**设计内**(复用 E 交互键经 4 选择器按 Kind 路由,见复评件 §3.3)。
- **证据链四层**(复评件 §3.2):类型面(公开面零 `HarvestGate` 形参,反射断言)· 源面
  (`HarvestGateAllowGate` 接链 + 门链清单双向差集)· 行为面(两闸具名拒绝经真 Resolve,证伪轮 M-A/M-B)
  · **真启动序**(PlayMode 模态夹具首轮一红 = 全链在真烘焙数据上跑通的意外集成证据,修夹具后 4/4)。
- **回归三元组**:`2026-11-01 12:14:36 CST` · `S22-9 第三批` · `a956f0c1`:
  EditMode `6370/6333/0/37`(`−6364 = 6`)· PlayMode `273/269/0/2/2`(`±0`)。债册 `--check` exit 0。
- **非绿面 2–7 照旧成立**(复评件 §五 逐条),不因判字变绿被覆盖;非绿面 2 的
  `AC-17-06` 解除动作(「随发起路径一并验」)已发生,AC 状态归 story 台账。
- 复评原件:`production/qa/evidence/review-sprint-22-exit-recheck-2026-11-01.md`。
