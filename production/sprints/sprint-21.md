# Sprint 21 — 2026-10-31 起(不设结束日期,做完即收 · 承 sprint-12…20 同例)

> **三元组**:规划轮截至 **`2026-10-31`** · 批次 **`sprint-21 规划`** · commit **`f2cc7e07`**(工作树干净)。
> **规划依据**:`production/milestones/p0-sweep-2026-10-29.md` §六(现件集)+ §5.3 · 路线图 §6.5 · `retro-sprint-20-2026-10-27.md` 行动项 1/2。
>
> ⚠️ **开工前置**:本 sprint **无 QA 计划** —— 用户 `2026-10-31` 裁定「**开工前补 `qa-plan`**」。
> ⇒ 在 `S21-1` 开工之前跑 `/qa-plan sprint`,产物落 `production/qa/qa-plan-sprint-21-<date>.md`。
> **未补 QA 计划即开工 = 违反本卡自身的前置**(不是建议,是判据)。

---

## Sprint Goal

**机制面清零之后的第一次清账。** 解掉会让排期死锁的 `TD-deps-01`,落地 §6.5 仅剩的 `inventory-002`,
按「同编译单元成批」清掉 14 条可动债中的 13 条,并用 `level-tool` 两件闭合 3 条挂标。
**本轮不新开故事链**,也不碰任何外部阻塞项。

---

## Capacity(承 `production/CAPACITY-MODEL.md` §3.1 三元 + §3.2 三条约束,**不估天**)

| 元 | 值 |
|---|---|
| **件数** | 计划 **14** 件(Must 9 · Should 3 · Nice 2)|
| **复杂度分布** | Must:`S×4 + M×4 + L×1` · Should:`S×3` · Nice:`S×2` |
| **追加率** | 预留 **1** 件(散件额度)。⚠️ 承 retro-20:预留额度按**件**计,遇**成链追加**时须先做容量再裁定 |

**三条约束(§3.2)**:
- **并发上限 P = 3** —— 同时在跑 ≤ 3 件。
- **L 件单独占批** —— `S21-9`(世界几何)不与 M/S 混批。
- **判别力门槛 D** —— 每件必须有「回退即转红」的证伪轮,否则不计 done。

---

## Tasks

### Must Have(关键路径)

| ID | 卡 | Owner | 复杂度 | 依赖 | 验收判据 |
|---|---|---|---|---|---|
| `S21-1` | **解 `TD-deps-01` 三方依赖环** | unity-specialist | **S** | 无 | 三条 `Depends on` 收窄为「依赖 X 的载荷/接口类型(已 Complete)」;`story-readiness` 对 `inventory-001/002` / `processing-003` **不再报 BLOCKED**;债册 `TD-deps-01` → `closed` |
| `S21-2` | **`inventory-002`** id 铸造权威与高水位重构 | unity-specialist | **M** | `S21-1` | 见 `story-002-id-authority-and-highwater.md` 的 AC;**证伪轮 ≥1 条/AC**;`BakedInitial` 缺失时「重建可撞号」反例夹具须真转红 |
| `S21-3` | **processing 判别力债 ×6**(`TD-processing-01…06`) | unity-specialist | **M** | 无 | 每条的「缺陷形态」在修复前**可复现红**、修复后转绿;六条同编译单元,同批落 |
| `S21-4` | **clinic 自指判据 ×2**(`TD-clinic-19` / `-21`) | unity-specialist | **S** | 无 | `clinic_modifier_eval_test` 的 6 处**不再直引被测常量**(改用字面量神谕);`TD-clinic-21` 的「生产不可达」须给 `grep` 级判据并登记为设计态 |
| `S21-5` | **治理债 ×2**:`TD-gate-04` · `TD-smoke-01` | unity-specialist | **M** | 无 | `TD-gate-04`:b2 门对「声明而未使用」的 asmdef 引用**转红**(造一个只声明不引用的 asmdef 作反例);`TD-smoke-01`:22 条清单至少落执行体骨架 + PASS/FAIL 出口,**未落的那几条须逐条具名** |
| `S21-6` | **`TD-build-01`** —— 真构建冒 3 组假改动 | unity-specialist | **S** | 无 | 三组逐组定性为「正常派生 / 真改动 / 误读」并落 `coding-standards.md` 同款三分类表;派生组进 `.gitignore` **窄 glob**(须验命中 + 验反例未误伤) |
| `S21-7` | **persistence ×2**:`TD-persist-08` · `TD-persist-10` | unity-specialist | **S** | 无 | `-08`:51 行方法拆到 ≤40 且**补 ≤40 界夹具**(拆了但不钉 = Landed ✅ / Proven ✗);`-10`:补 `SaveService` ↔ `SaveMigrationChain` 接缝的集成夹具(空区间 `[2,2)` 下须显式说明夹具路径) |
| `S21-8` | **`level-tool/story-006`** —— C1–C6 七项一致性检查 | unity-specialist | **M** | 无 | 七项从 `UnimplementedChecks` **移出**且各自有夹具;C2/C4/C5/C6 仍须是**硬失败**(`throw`),C1/C3 仍是**告警**;闭 `S20-20` #3 |
| `S21-9` | **`level-tool/story-002`** —— 世界几何真产出 | unity-specialist | **L** | 单独占批 | `assets/data/world_*.json` 从 **1 件**到 ADR-022 §三 的 **7 件**;闭 `S20-20` #5 与 `S20-17` #1;⚠️ 运行期装载面另测,**不得**以「工具产出」代「运行期可读」 |

### Should Have(须先出裁定,或体量待定)

| ID | 卡 | 复杂度 | 前置 | 说明 |
|---|---|---|---|---|
| `S21-10` | **foraging 公式 vs 散文互斥 ×2**(`TD-foraging-01` `CapTable` · `-02` `GatherMul`) | **S** | ⚠️ **用户裁**(改公式 / 改散文) | 数值归用户 —— 我先出两案 + 各自影响面,**不预设**。裁完再实现 |
| `S21-11` | **`TD-medcons-20`** —— 药害停在「伤/后遗」而战斗可「打死改写昏迷」的正面理由 | **S** | ⚠️ **用户裁** | 可写成 ADR 级裁定的一半;未裁则「不致死」机械门(ADR-035)的理由链缺一环 |
| `S21-12` | **`TD-s13-01` 结转** —— code-review 增量落盘判据仍是前瞻性 | **S** | 无 | 只能补机制;**有效性待下次真实重评**,当下**不可自证**(禁借绿) |

### Nice to Have

| ID | 卡 | 复杂度 | 说明 |
|---|---|---|---|
| `S21-13` | **容量模型二改** —— 预留拆「散件 / 链条」两种额度 + 「将掀几件 story」列 | S | retro-20 行动项 1/2;纯文书,零产品码 |
| `S21-14` | **假绿形态登记表** —— 已知 8 形态集中到一处 | S | retro-20 Process Improvement 2;让下次写夹具前能先查 |

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
| 路线图 §6.5 的 sprint-21 = 4 件 | **只剩 `inventory-002`**(foraging-003 / processing-003 / inventory-001 已 Complete) | 本文件的 Must 面按实测重排,**不用 §6.5 原行** |
| 里程碑 §5.3 的 `C-1`(clinic-001 复核) | **已于 2026-10-29 兑现**(`/story-done` lean → COMPLETE WITH NOTES) | 本 sprint **不再列** |
| `inventory-002` 可直接开工 | **否** —— 三方 `Depends on` 成环(`TD-deps-01`),`story-readiness` 会判 BLOCKED | 拆出 `S21-1` 作前置 |
| audio 5 条可清 | **否** —— P2 2 条 + 外部阻塞 3 条 | 全部移出本 sprint 面 |
| `TD-audio-101` 正文有「✅ 结案」 | **属于 `TD-audio-028` 的行首**,不是 101 的结案 | 已核,101 仍 `open`(P2) |

---

## Carryover from Previous Sprint

**无。** sprint-20 收口时 `21/21 done`,结转 0 件(§十四 已核实)。
唯一跨 sprint 的「未完成」是**挂标 16 条**与**债 28 条**,均非 carryover 卡。

---

## Risks

| # | 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|---|
| R1 | **`S21-9`(L 件)吃掉两批复用** —— 世界几何依赖 Terrain / 导航 / 生态区整链 | 中 | 高 | 单独立项、单独占批;若两轮内未收敛,**缩到「地形 + 生态区边界」两件产物**并把导航格留到下一卡(须显式登记,不静默缩) |
| R2 | **`S21-1` 改的是 story 正文** —— 可能被读成「改历史」 | 低 | 中 | 只在 `Depends on` 行加注 + 保留原文;`TD-deps-01` 记录的「不预设选哪条」由本轮裁定并落债册 |
| R3 | **`S21-3` 六条同批** —— 六件同编译单元,一次改坏即六条全红 | 中 | 中 | 逐条独立提交 + 每条一条反证;**不得**六条合并成一次证伪轮 |
| R4 | **两道前置未清就开工** —— QA 计划(本文件头部)+ `S21-1` | 中 | 高 | 开工门 = 两者皆落;未落即记 `NOT-RUN`,不计任何判据面 |
| R5 | **Should 两件卡在用户裁** —— `S21-10` / `S21-11` 无裁定则无法开工 | 高 | 低 | 我先出「两案 + 影响面」;未裁则保持 `backlog`,**不占 Must 额度** |

---

## Dependencies on External Factors

- **`TD-ci-01`**(CI 常驻矩阵)—— 需 Unity 服务账号凭证,**只能用户侧申请**。本 sprint 不依赖它,但 `TD-smoke-01` 若做成 CI 常驻件则会撞上同一阻塞 ⇒ **`S21-5` 的 smoke 半边按「本地可跑」设计,不挂 CI**。
- **目标硬件**(`TD-audio-108` 的 DSP P95)—— 未定稿,本 sprint 不排。

---

## Definition of Done for this Sprint

- [ ] **开工前置**:`production/qa/qa-plan-sprint-21-<date>.md` 已落
- [ ] Must Have 9 件全部完成(或**具名挂标**且逐条给 `file:line` + 不可达理由)
- [ ] 每件都有证伪轮记录(「回退 ⇒ 哪条转红」),`FALSIFY-` 零残留 + SHA-256 逐字节还原
- [ ] 回归三元组(截至时刻 · 批次 · commit)两套件全量,零失败
- [ ] 债册 `--check` exit 0;`未闭 P0 面` 仍为 **0**(不得因本 sprint 新立 P0 面债)
- [ ] 挂标:**16 → 目标 ≤10**(`S21-8` 闭 1 · `S21-9` 闭 2 · `S21-2` 若触及 id 高水位可再闭 1)
- [ ] smoke-check:本 sprint 无新 UI/手感面 ⇒ 允许 `PASS WITH WARNINGS`,但**理由须逐条写明**
- [ ] QA 签核(APPROVED 或 APPROVED WITH CONDITIONS)
- [ ] 收口判定与收口提交**同日**(承 retro-19 行动项 3 / retro-20 §What Went Well 4)

---

## 边界(本 sprint 不做什么,防 scope 漂移)

1. **不新开故事链** —— 除 `inventory-002` 与 `level-tool` 两件外,不把 sprint-22/23/25 的 Ready 件前移。**追加链入场前须先做容量再裁定**(retro-20 行动项 1)。
2. **不碰外部阻塞项** —— `TD-ci-01` / audio 三条 / `TD-net-01` 一律不动,登记在案即可。
3. **不代作数值裁定** —— `S21-10` / `S21-11` 由用户裁;我只出选项与影响面。
4. **不以「工具产出」代「运行期可读」** —— `S21-9` 的产出是数据,运行期装载是另一件事,须分开记。
5. **不把 `Landed` 当 `Proven`** —— 回办格式承 `code-review` Phase 8b 双列(已落 / 已证 + 分支覆盖)。

---

## 变更记录

| 日期 | 变更 | 依据 |
|---|---|---|
| 2026-10-31 | 建立 | 用户指令「sprint21 排卡」· 里程碑 §六现件集 · retro-20 行动项 1/2 |
