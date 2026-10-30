# Sprint 21 — 2026-10-31 起(不设结束日期,做完即收 · 承 sprint-12…20 同例)

> **三元组**:规划轮截至 **`2026-10-31`** · 批次 **`sprint-21 规划`** · commit **`98c4179d`**(+ 本批未提交改动)。
> **规划依据**:`production/milestones/p0-sweep-2026-10-29.md` §六(现件集)+ §5.3 · 路线图 §6.5 · `retro-sprint-20-2026-10-27.md` 行动项 1/2。
>
> ⚠️ **开工前置**:本 sprint **无 QA 计划** —— 用户 `2026-10-31` 裁定「**开工前补 `qa-plan`**」。
> ⇒ 在 `S21-1` 开工之前跑 `/qa-plan sprint`,产物落 `production/qa/qa-plan-sprint-21-<date>.md`。
> **未补 QA 计划即开工 = 违反本卡自身的前置**(不是建议,是判据)。
>
> ✅ **2026-10-31 已兑现**:`production/qa/qa-plan-sprint-21-2026-10-31.md` 已落盘
> (13 卡 · 证据类 E1–E4 · smoke 5 条)。⇒ 该开工门**已开**。

---

## Sprint Goal

**机制面清零之后的第一次清账。** 解掉会让排期死锁的 `TD-deps-01`,落地 §6.5 仅剩的 `inventory-002`,
按「同编译单元成批」清掉 14 条可动债中的 12 条,并把 sprint-20 遗留的 3 条挂标**按复算订正**。
**本轮不新开故事链**,也不碰任何外部阻塞项。

---

## Capacity(承 `production/CAPACITY-MODEL.md` §3.1 三元 + §3.2 三条约束,**不估天**)

| 元 | 值 |
|---|---|
| **件数** | 计划 **13** 件(Must 8 · Should 3 · Nice 2)|
| **复杂度分布** | Must:`S×5 + M×3` · Should:`S×3` · Nice:`S×2` |
| **追加率** | 预留 **1** 件(散件额度)。⚠️ 承 retro-20:预留额度按**件**计,遇**成链追加**时须先做容量再裁定 |

**三条约束(§3.2)**:
- **并发上限 P = 3** —— 同时在跑 ≤ 3 件。
- **L 件单独占批** —— 本 sprint **无 L 件**(原 `S21-9` 世界几何降级为订正卡,见下方订正记录)。
- **判别力门槛 D** —— 每件必须有「回退即转红」的证伪轮,否则不计 done。

> 🔁 **2026-10-31 规划中订正(容量从 14 件 / 1 L 降为 13 件 / 0 L)**:首版把 `level-tool` 的
> `story-006` / `story-002` 排成实现卡(`S21-8` / `S21-9`),复核发现 **两件均已于 `2026-10-29` 完成**:
> `UnimplementedChecks` 现为 `Array.Empty<string>()` · `assets/data/world_*.json` 现 **10** 件 ·
> `MinimalWorldFactory.cs:147-172` 已装载 POI / 资源。
> ⇒ 两卡合并为**一张订正卡**(`S21-8`),释放的 L 名额**不补新件**。
> 记录见 `sprint-20.md` §十四 14.4 的 2026-10-31 订正块。

---

## Tasks

### Must Have(关键路径)

| ID | 卡 | Owner | 复杂度 | 依赖 | 验收判据 |
|---|---|---|---|---|---|
| `S21-1` | **解 `TD-deps-01` 三方依赖环** | unity-specialist | **S** | 无 | 三条 `Depends on` 收窄为「依赖 X 的载荷/接口类型(已 Complete)」;`story-readiness` 对 `inventory-001/002` / `processing-003` **不再报 BLOCKED**;债册 `TD-deps-01` → `closed` |
| `S21-2` | **`inventory-002`** id 铸造权威与高水位重构 | unity-specialist | **M** | `S21-1` | story 的 AC-20-01…04 逐条判定;**证伪轮 ≥1 条/AC**;`BakedInitial` 缺失时「重建可撞号」反例夹具须真转红 |
| `S21-3` | **processing 判别力债 ×6**(`TD-processing-01…06`) | unity-specialist | **M** | 无 | 每条的缺陷形态在修复前**可复现红**、修复后转绿;六条同编译单元,但**逐条一条反证、逐条独立提交** |
| `S21-4` | **clinic 自指判据 ×2**(`TD-clinic-19` / `-21`) | unity-specialist | **S** | 无 | `clinic_modifier_eval_test` 的 6 处**不再直引被测常量**(改字面量神谕);`TD-clinic-21` 须给 `grep` 级判据并登记为设计态 |
| `S21-5` | **治理债 ×2**:`TD-gate-04` · `TD-smoke-01` | unity-specialist | **M** | 无 | `TD-gate-04`:造「只声明不引用」的 asmdef 反例 ⇒ b2 门**转红**;`TD-smoke-01`:落执行体骨架 + PASS/FAIL 出口,**未落执行体的条目逐条具名** |
| `S21-6` | **`TD-build-01`** —— 真构建冒 3 组假改动 | unity-specialist | **S** | 无 | 三组逐组定性并落 `coding-standards.md` 三分类;派生组进 `.gitignore` **窄 glob**(验命中 + 验反例未误伤) |
| `S21-7` | **persistence ×2**:`TD-persist-08` · `TD-persist-10` | unity-specialist | **S** | 无 | `-08`:51 行 → ≤40 **且补 ≤40 界夹具**;`-10`:补 `SaveService` ↔ `SaveMigrationChain` 接缝夹具,空区间 `[2,2)` 下须说明走哪条路径 |
| `S21-8` | **挂标订正 + `level-tool` 两件复核** | unity-specialist | **S** | 无 | ① `S20-17` #1 / `S20-20` #3 / #5 三条挂标**按复算订正为已闭**(账面已改,见 sprint-20 §十四 14.4);② 修 `MinimalWorldFactory.cs:35-37` 的**过期注释**(它说「POI/资源仍全 -1 —— 归 story S4」,而 `:147-172` 已在装载);③ 复跑三条判据并在 sprint-20 §9.1.1 留 🔁 标记 |

> ✅ **`S21-1` 已于 `2026-10-31` 结案**(用户裁定「结案 + 只立向前约定」)。
> 开卡时按 R1 先跑复算命令,发现**环已由事实消解**:`inventory-001` / `processing-003` 均已 `Complete`
> (立债时三件皆 `Ready`),`inventory-002` 的三个前置全部满足。
> 处置:债 → `closed`(带复算证据)· **不改**三方 `Depends on` 原文(改已完成件 = 改历史)·
> 立向前约定 `.claude/docs/coding-standards.md` §「依赖写法:形状 vs 实现」(只约束新件)。
> **未做**:`/story-readiness` 未实跑,留给 `S21-2` 开工前;`S21-1` 判的是它读取的输入(四件上游 `Status`)。

### Should Have(须先出裁定,或体量待定)

| ID | 卡 | 复杂度 | 前置 | 说明 |
|---|---|---|---|---|
| `S21-9` | **foraging 公式 vs 散文互斥 ×2**(`TD-foraging-01` `CapTable` · `-02` `GatherMul`) | **S** | ⚠️ **用户裁**(改公式 / 改散文) | 数值归用户 —— 我先出两案 + 各自影响面,**不预设**。裁完再实现 |
| `S21-10` | **`TD-medcons-20`** —— 药害停在「伤/后遗」而战斗可「打死改写昏迷」的正面理由 | **S** | ⚠️ **用户裁** | 可写成 ADR-035 的窄修订;未裁则「永不致死」机械门的理由链缺一环 |
| `S21-11` | **`TD-s13-01` 结转** —— code-review 增量落盘判据仍是前瞻性 | **S** | 无 | 只能补机制;**有效性待下次真实重评**,当下**不可自证**(禁借绿) |

### Nice to Have

| ID | 卡 | 复杂度 | 说明 |
|---|---|---|---|
| `S21-12` | **容量模型二改** —— 预留拆「散件 / 链条」两种额度 + 「将掀几件 story」列 | S | retro-20 行动项 1/2;纯文书,零产品码 |
| `S21-13` | **假绿形态登记表** —— 已知 8 形态集中到一处 | S | retro-20 Process Improvement 2 |

---

## 本 sprint 不排(具名,防「没人提就以为没人管」)

| 项 | 理由 |
|---|---|
| `TD-ci-01` | **EXTERNAL** —— 凭证在用户侧,agent 侧无路径 |
| `TD-audio-104` / `-107` | BLOCKED-BY **P1b**(VR 听测 / 45 网络层) |
| `TD-audio-108` | 须**目标硬件**定稿(DSP P95) |
| `TD-audio-100` / `-101` | 2026-10-29 用户裁定听测**不在 P0 面** ⇒ 转 P2 |
| `TD-net-01` / `TD-prescription-05` | P1a / P1b 面 |
| `TD-processing-07` / `TD-epic-01-d` | 已按用户裁定**转 P2**(`~~Y~~`,状态不变) |

---

## 开工前的陈旧前提复核(承 `/sprint-plan` Phase 1 硬前置)

**规划时刻实跑**(命令自包含):

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 tools/debtcount/debtcount.py --print
# ⇒ 条目 139 | closed 110 | open 25 | partial 3 | P0 面 Y100/N35/~~Y~~4 | 未闭 P0 面 = 0
python3 - <<'PY'
import re,glob,os,collections
eps=[d for d in os.listdir('production/epics') if os.path.isdir('production/epics/'+d) and d!='level-tool']
c=collections.Counter(); n=0
for e in eps:
    for f in glob.glob(f'production/epics/{e}/story-*.md'):
        t=open(f,encoding='utf-8',errors='ignore').read()
        m=re.search(r'^\s*>\s*\*\*Status\*\*\s*[:：]\s*(.+)$',t,re.M)
        c[(m.group(1).strip()[:8] if m else 'MISSING')]+=1; n+=1
print(n, dict(c))   # ⇒ 230 件;以 Complete 开头的 = 205
PY
```

| 前提 | 实测 | 处置 |
|---|---|---|
| 路线图 §6.5 的 sprint-21 = 4 件 | **只剩 `inventory-002`**(其余三件已 Complete) | Must 面按实测重排,**不用 §6.5 原行** |
| 里程碑 §5.3 的 `C-1`(clinic-001 复核) | **已于 2026-10-29 兑现** | 本 sprint **不再列** |
| `inventory-002` 可直接开工 | **否** —— 三方 `Depends on` 成环(`TD-deps-01`) | 拆 `S21-1` 作前置 |
| audio 5 条可清 | **否** —— P2 2 条 + 外部阻塞 3 条 | 全部移出本 sprint 面 |
| `TD-audio-101` 正文有「✅ 结案」 | **属于 `TD-audio-028` 的行首**,不是 101 的结案 | 已核,101 仍 `open`(P2) |
| `level-tool/story-006` / `story-002` 待实现 | **❌ 两件均 `Complete`,2026-10-29** | 首版 `S21-8`/`S21-9` 作废 ⇒ 合并为订正卡 `S21-8` |
| `S20-20` #3 / #5、`S20-17` #1 「仍开」 | **❌ 三条均已闭** | 同上;账面已在 `sprint-20.md` §9.1.1 / §十四 订正 |

> ⚠️ **本次复核自身抓到的问题**:第一版排卡**转抄了 `sprint-20.md` §9.1.1(10-28 判据面)**,
> 没重跑三条判据 ⇒ 排出两张幽灵卡。**同一错误在 §十四(10-30)里也发生过一次。**
> ⇒ 本节为**第三次**复核,逐条跑了命令。

---

## Carryover from Previous Sprint

**无。** sprint-20 收口时 `21/21 done`,结转 0 件(§十四 已核实)。

---

## Risks

| # | 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|---|
| **R1** | **陈旧前提复发** —— `S21-8` 这张卡本身就是抄旧口径排出来的;`S21-3` / `S21-4` 的债标题同样取自债册,**可能已有部分被后续提交解决** | **高** | 中 | **每张开卡前先跑该债自己的复算命令**并贴输出;账面与实测不符时**先订正账面再开工**(承 `TD-audio-029/109` 等 7 例「账面滞后」) |
| R2 | **`S21-1` 改的是 story 正文** —— 可能被读成「改历史」 | 低 | 中 | 只在 `Depends on` 行加注 + 保留原文;裁定结论落债册 |
| R3 | **`S21-3` 六条同批** —— 一次改坏六条全红 | 中 | 中 | 逐条独立提交 + 每条一条反证;**不得**合并成一次证伪轮 |
| R4 | **两道前置未清就开工** —— QA 计划 + `S21-1` | 中 | 高 | 开工门 = 两者皆落;未落即记 `NOT-RUN` |
| R5 | **Should 两件卡在用户裁** —— `S21-9` / `S21-10` | 高 | 低 | 我先出「两案 + 影响面」;未裁则保持 `backlog`,不占 Must 额度 |

---

## Dependencies on External Factors

- **`TD-ci-01`**(CI 常驻矩阵)—— 需 Unity 服务账号凭证,**只能用户侧申请**。本 sprint 不依赖它,但 `S21-5` 的 smoke 半边若做成 CI 常驻件会撞上同一阻塞 ⇒ **按「本地可跑」设计**。
- **目标硬件**(`TD-audio-108`)—— 未定稿,本 sprint 不排。

---

## Definition of Done for this Sprint

- [ ] **开工前置**:`production/qa/qa-plan-sprint-21-<date>.md` 已落
- [ ] Must Have 8 件全部完成(或**具名挂标**且逐条给 `file:line` + 不可达理由)
- [ ] 每件都有证伪轮记录(「回退 ⇒ 哪条转红」),`FALSIFY-` 零残留 + SHA-256 逐字节还原
- [ ] 回归三元组(截至时刻 · 批次 · commit)两套件全量,零失败
- [ ] 债册 `--check` exit 0;`未闭 P0 面` 仍为 **0**
- [ ] **挂标账面 16 → 13**(`S21-8` 订正 3 条);**每一笔订正都附复算命令**,不得只改数字
- [ ] smoke-check:本 sprint 无新 UI/手感面 ⇒ 允许 `PASS WITH WARNINGS`,但理由须逐条写明
- [ ] QA 签核(APPROVED 或 APPROVED WITH CONDITIONS)
- [ ] 收口判定与收口提交**同日**(承 retro-19 行动项 3 / retro-20 §What Went Well 4)

---

## 边界(本 sprint 不做什么,防 scope 漂移)

1. **不新开故事链** —— 除 `inventory-002` 外,不把 sprint-22/23/25 的 Ready 件前移。**追加链入场前须先做容量再裁定**。
2. **不碰外部阻塞项** —— `TD-ci-01` / audio 三条 / `TD-net-01` 一律不动,登记在案即可。
3. **不代作数值裁定** —— `S21-9` / `S21-10` 由用户裁;我只出选项与影响面。
4. **不以「工具产出」代「运行期可读」** —— `level-tool` 两件的产出与装载分开记(本 sprint 只做订正)。
5. **不把 `Landed` 当 `Proven`** —— 回办格式承 `code-review` Phase 8b 双列(已落 / 已证 + 分支覆盖)。
6. **不重做已完成件** —— 开卡前必跑该卡的复算命令;账面说「仍开」而实测已闭 ⇒ **订正账面**,不排实现。

---

## 变更记录

| 日期 | 变更 | 依据 |
|---|---|---|
| 2026-10-31 | 建立 | 用户指令「sprint21 排卡」· 里程碑 §六现件集 · retro-20 行动项 1/2 |
| 2026-10-31 | **容量 14 → 13 件,L 件 1 → 0**:`S21-8`/`S21-9`(level-tool 两件)合并为一张订正卡;剩余卡号顺移 | 规划期复核发现两件已于 `2026-10-29` 完成(用户裁定「先改卡,再出 QA 计划」) |
