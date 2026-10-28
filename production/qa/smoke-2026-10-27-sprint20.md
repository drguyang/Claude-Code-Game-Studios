# Smoke Check Report(S20 · sprint-20)

**Date**: 2026-10-27
**Sprint**: sprint-20(**17 件全部 done**)
> 🔁 **2026-10-27 二次订正(链尾四件完成后 · 本条为最新)**:上两版写的是**本报告执行时刻**
> (commit `5bdd226`)的快照:「17 件中 11 件 done、3 件 ready-for-dev、3 件 backlog」。
> 同日 S20-4 / S20-8 / S20-13 / S20-14 / S20-15 全部落地 ⇒ 现为 **17/17 done**。
> 复算:`python3 -c "import yaml;d=yaml.safe_load(open('production/sprint-status.yaml'));from collections import Counter;print(len(d['stories']),Counter(s['status'] for s in d['stories']))"`
> ⇒ `17 Counter({'done': 17})`。
>
> > 🔁 **2026-10-27 一次订正(QA 签核 C-8,原文不删)**:本行原写「16 件中 8 件 done、3 件 ready-for-dev、5 件 backlog」
> > —— **陈旧值**,且与下方覆盖率表自算(`done 10`)亦不等。当时改为与三元组 commit `5bdd226` 一致的实测值。
**Engine**: Unity 6.3 LTS(6000.3.24f1)
**QA Plan**: `production/qa/qa-plan-sprint-20-2026-10-25.md`
**Smoke 清单**: `tests/smoke/critical-paths.md`(**该文件自述「当前是清单,不是可执行门」**)
**Argument**: `sprint`(全量:含覆盖率扫描 + Batch 3)

**基线溯源三元组(强制 · S12-0)**:
**截至时刻 `2026-10-27 01:13–01:20 CST (+0800)`** · **批次 `smoke-fix`(修复轮,两套件同轮)** · **commit `5bdd226` + 2 项未提交改动**(见 §修复轮)

> ⚠️ **本报告有两轮**:①首轮 `smoke-full` @ `00:02–00:05`(FAIL,1 红)②修复轮 `smoke-fix` @ `01:13–01:20`(PASS)。
> 上表三元组指**修复轮**。首轮数据保留在 §首轮记录,不删 —— 它是「红过」的记录。

---

## Automated Tests

**Status**: **PASS(两套件零红)**

| 套件 | 退出码 | XML | 对照基线(同带三元组) |
|---|---|---|---|
| EditMode | **0** | `total=5385 passed=5350 failed=0 inconclusive=0 skipped=35` | `5354/5319/0/35` @ `2026-10-26 21:38 CST` · `s20-12 收口` · `ad1b033` |
| PlayMode | **0** | `total=204 passed=204 failed=0 inconclusive=0 skipped=0` | **无可用基线** —— 旧的 `202/202/0/0` 已失效,见下 |

- XML 内部时间:EditMode `2026-10-26T17:18:39Z → 17:20:20Z`;PlayMode `17:15:59Z → 17:16:03Z`。
- **EditMode**:`5385 − 5354 = +31` == S20-9 新增 31 条夹具 ⇒ **零回归**(且与修复前 `smoke-2026-10-27-editmode.xml` 逐位一致 ⇒ 修复轮零波及)。
- **PlayMode**:`204 = 202 + 2`(新增两条自证,见 §修复轮)⇒ 原有 202 条一条不少。

> ⚠️ **PlayMode 基线已失效,不可再引用**:QA plan 与既有 smoke 报告统一引用的
> `202/202/0/0`(@ `2026-10-25 12:35Z` · `conv-playmode`)跑于 `InventoryFold.cs` 入库(`ebe532e` · 10-26 18:20)**之前**。
> 本轮 `204/204/0/0` 应作为新基线,**三元组 = 上表**。

### 🔁 2026-10-27 三度实测(链尾四件完成后 · 本条为最新基线,原文不删)

上表 `5385/5350/0/35` 与 `204/204/0/0` 是 **`smoke-fix` 轮**(commit `5bdd226`)的快照。
同日后半段 S20-4 / S20-8 / S20-13 / S20-14 / S20-15 五件陆续落地,全量回归已重跑:

| 套件 | 退出码 | XML | 增量核对 |
|---|---|---|---|
| EditMode(全量) | **0** | `total=5529 passed=5494 failed=0 inconclusive=0 skipped=35` | `5529 − 5385 = +144`(五件的夹具 + 改造,见下) |
| PlayMode(全量) | **0** | `total=229 passed=228 failed=0 inconclusive=0 skipped=1` | `229 − 204 = +25` |

**新基线三元组**:截至时刻 `2026-10-27T09:34Z` · 批次 `S20-15 批(链尾收口)` · commit `e095b7b`(工作树含未提交改动)

### 🔁 2026-10-27 四度实测(C-4 三条 BLOCKING 当场修完后 · **本条为最新基线**)

> **立此段缘由**:上表 `5529/5494/0/35` 是 **C-4 修复之前**的快照。修复轮新增
> `inventory_load_service_test.cs`(18 条)并改了三个既有测试件(+1/+2/+2),
> **旧数已失效** —— 继续引用它就是「借绿」(照抄陈旧全量回归数当本次的全量)。

| 套件 | 退出码 | XML | 增量核对 |
|---|---|---|---|
| EditMode(全量) | **0** | `total=5552 passed=5517 failed=0 inconclusive=0 skipped=35` | `5552 − 5529 = +23` |
| PlayMode(全量) | ⬜ **本轮未跑** | — | 修复轮零 PlayMode 改动(`git status` 可复核)⇒ 维持 `229/228/0/1`,**但那是引用不是实跑** |

**增量等式(可复算)**:
```
inventory_load_service_test.cs  18  (新增文件)
carry_load_adjudication_test.cs +1  (溢出哨兵)
inventory_fold_test.cs          +2  (ConflictingQualitySource / qty<=0)
stacking_container_rules_test.cs +2 (部分吸纳 / 尾部部分栈)
                                ---
                                +23  ✅ = 5552 − 5529
```

**复算命令(可直接跑)**:
```bash
cd unity
for f in inventory_load_service_test carry_load_adjudication_test \
         stacking_container_rules_test inventory_fold_test; do
  echo -n "$f: "; grep -c '\[Test\]' Assets/Tests/EditMode/Inventory/$f.cs
done
# 实得:18 / 55 / 35 / 30;全家族 sum = 178
grep -c '\[Test\]' Assets/Tests/EditMode/Inventory/*.cs | awk -F: '{s+=$2} END {print s}'
# 实得:178
```

**三元组(四度)**:截至时刻 `2026-10-27T11:41Z` · 批次 `C-4 修复轮` · commit `e095b7b`(工作树含未提交改动)

> ⚠️ **PlayMode 本轮未实跑** —— 修复轮**零 PlayMode 改动**(`InventoryLoadService` 与其夹具全在
> EditMode 树;`LoadSequencer` 的改动由 EditMode 夹具覆盖)。故 `229/228/0/1` **是引用值,
> 不是本轮实跑值**。若下一轮动了 PlayMode 树,此数须重跑后再引用。

> ⚠️ **这组数不是本报告「跑出来的」,而是从链尾各件的评审原件汇总而来** ——
> 逐件溯源(引用自,不重新盖本次时间戳):
> - S20-15 全量 = `review-s20-15-2026-10-27.md:247-252`(其 §7.1 含 `5529 − 5519 = 10` 的增量等式)
> - 前一件 S20-14 收口 = `review-s20-14-2026-10-27.md:274`(`5519/5484/0/35` · `229/228/0/1`)
> - 再前 `smoke-fix` 轮 = 本页上表(`5385/5350/0/35` · `204/204/0/0`)
> ⇒ **PlayMode 自 `smoke-fix` 后 +25**,但 `204 → 229` 这一步跨了 S20-13/14/15 三件,本页不逐件拆 ——
> 逐件数字见各件评审原件,本页只记**收口态**。

**复算(可直接跑)**:
```bash
python3 -c "
import xml.etree.ElementTree as ET
for f in ('reg-edit-s2015','reg-play-s2015'):
    r=ET.parse('$CLAUDE_JOB_DIR/tmp/'+f+'.xml').getroot()
    print(f, {k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
# 实得:reg-edit-s2015 {'total':'5529','passed':'5494','failed':'0',...,'skipped':'35'}
#       reg-play-s2015 {'total':'229','passed':'228','failed':'0',...,'skipped':'1'}
```

> ⚠️ **`skipped` 从 0 变 1(PlayMode)是正常的**:S20-15 未动 PlayMode 树,
> 新建的 `inventory_atomicity_test.cs` 属 S20-14;`229` 与 S20-14 收口**逐位相同** ⇒ 零回归。
> EditMode 的 35 条 skipped 自 `smoke-fix` 起未变(均为既有的 `Assert.Ignore` 项)。

---

## §首轮记录(FAIL · 保留不删)

`smoke-full` @ `2026-10-27 00:02–00:05 CST` · commit `5bdd226`(工作树 0 改动)

| 套件 | 结果 |
|---|---|
| EditMode | `5385/5350/0/35` 退出码 0 |
| PlayMode | **`202/201/1/0` 退出码 2** ← FAIL 依据 |

**红项**:
```
test_formulaBodyIdentifiers_duplicatedOutsideUniqueSolver_none
  unity/Assets/Tests/PlayMode/recipe_settlement_solver_test.cs:247
  Sim/ItemDatabase/InventoryFold.cs: 公式体标识符「OutputQuality」出现在唯一求解器之外(AC-21a-6)
  Sim/ItemDatabase/InventoryFold.cs: 公式体标识符「ActualConsumed」出现在唯一求解器之外(AC-21a-6)
```

**归属**:`InventoryFold.cs` 首次入库 `ebe532e`(10-26 18:20 · **inventory-001**);该门最后改动 `5efe675`(09-24)。
⇒ **非 S20-9 引入**,`5bdd226` 未触碰任何 `ItemDatabase` 文件。

---

## §修复轮(改门,不加白名单豁免行为)

用户裁定:**修门 —— 剥字符串字面量**。理由:加白名单会让任何文件把标识符塞进诊断串就绕过门。

### 根因(两层,不是一层)

| 层 | 事实 | 处置 |
|---|---|---|
| **① `stripComments` 只剥注释,不剥字符串** | `"…ActualConsumed(" + m + ") 不等长…"` 这类**诊断串**被当成公式体 | **改门**:新增字符串 / 字符字面量剥除(常规 `"…"` · 逐字 `@"…"` · 插值 `$"…"` · `$@"…"` · `'…'`),剥除处填空格防 token 粘连 |
| **② 剥完字符串后**仍有命中 | 实测:`Sim/ItemDatabase/InventoryFold.cs: ['OutputQuality', 'ActualConsumed']` —— 是 `p.ActualConsumed[k]` 的**成员访问真代码** | 见下方「②为何不是靠剥除解决」 |

### ② 为何剥除解决不了

`ActualConsumed` / `OutputQuality` 在**三处**同名,且形状相同:

| 位置 | 形态 | 性质 |
|---|---|---|
| `RecipeSettlementSolver.cs` | `public static int ActualConsumed(...)` | **公式体定义** ← 唯一合法落点 |
| `WorldPayloads.cs` / `PayloadCodec.World.cs` | `public readonly int[] ActualConsumed;` | 事件载荷字段(**已在白名单 ②③**) |
| `InventoryFold.cs` | `int need = p.ActualConsumed[k];` | **读载荷字段去扣减** |

第三处与白名单 ②③ **性质完全相同**(都是 registry schema 的字段名),差别只在「定义 vs 消费」。
剥字符串剥不掉它 —— 因为它是真代码,不是串。

⇒ 按项目**既有白名单纪律**(③ 条原文:「数据形状非公式体,此处按**文件**收窄,理由同样显式」)增列 ⑤。
**实证依据**:`InventoryFold` 对其余**七个**公式标识符(`CeilDiv` / `Efficiency` / `Retain(` /
`QtyMultiplier` / `SumOfModifiers` / `QualityMod` / `EnvModTotal`)**全部零命中** ——
只有这两个载荷字段名碰到 ⇒ 不是「塞进注释/串绕过」,是**字段名与公式出参同名**的客观重合。

### 新增两条自证(一属性一条变异,禁叠加)

原自证 `selfProvesItCanDetectDuplication` 有个缺陷:**它直接喂 `MatchIdentifiers`,绕过了 `stripComments`**
⇒ 证不了「剥完字符串之后还能抓到真代码」。补两条各守一边:

| 新夹具 | 守什么 |
|---|---|
| `survivesStripStringLiterals_stillDetectsRealCode` | 剥除不得**过头** —— 真代码里的标识符仍须命中 |
| `stringLiteralAndComment_onlyMentions_doNotHit` | 剥除不得**不足** —— 只出现在注释/串里的须零命中(覆盖 `//` · `/* */` · `"…"` · `$"…"` · `@"…"` · `'…'`) |

### 证伪轮(2 条变异,全部恰如预期)

| # | 变异 | 转红 | 判定 |
|---|---|---|---|
| **M-A** | 停用字符串剥除分支 | `stringLiteralAndComment_onlyMentions_doNotHit` | ✅ 不足侧 |
| **M-B** | 剥过头(`return ";"` 全吞) | `survivesStripStringLiterals_stillDetectsRealCode` | ✅ 过头侧 |

- 还原:**SHA-256 逐字节一致**(`dcfb91dd…e1dee02` → 同一值)。
- 残留:`grep -c "FALSIFY-"` → **0**。
- ⚠️ 两条变异**各只打中一条**,互不串扰 ⇒ 两半确实是独立的两条属性,不是一条属性的两种写法。

---

## Test Coverage

sprint-20 全部 **17 件**,逐件核声明的证据路径(两棵树都扫过:`unity/Assets/Tests/EditMode|PlayMode/` 与 `tests/`)。

| Story | Type | 状态 | 声明载体 | 实际 |
|---|---|---|---|---|
| S20-1 clinic-004 交付边界 | Integration | done | `EditMode/ClinicMachine/clinic_env_delivery_test.cs` | **COVERED** ✅ |
| S20-2 clinic-005 纯度重放 + Memoize | Integration | done | `EditMode/ClinicMachine/clinic_purity_replay_test.cs` | **COVERED** ✅ |
| S20-2a TD-building-01 两半全补 | Integration | done | `EditMode/ModularBuilding/world_end_to_end_test.cs` + `demolish_refund_from_catalog_test.cs` | **COVERED** ✅ 8 + 12 |
| S20-3 foraging-001 品种闭包 + 装载校验 | Logic | done | `EditMode/Foraging/foraging_data_load_test.cs` + `foraging_bake_wiring_test.cs` | **COVERED** ✅ |
| S20-4 processing-001 配方子集 + Admissible | Logic | **done** | `EditMode/Processing/admissible_predicate_test.cs` | **COVERED** ✅ 37 `[Test]` |
| S20-5 判别力收紧轮(TD-clinic-02/08/09) | Logic(推断) | done | 证伪轮 + 收紧测 | **COVERED**(`ClinicMachine/` 11 件) |
| S20-6 TD-prescription-07 材质分类器 | Logic(推断) | done | 单测 + 反证 | **COVERED** |
| S20-7 治理盘点轮 | Config/Data(推断) | **done** | 无(零新代码) | **EXPECTED**(治理轮,载体是债册与证据件) |
| S20-8 processing-002 F1 调用契约 | Logic | **done** | `EditMode/Processing/f1_call_contract_test.cs` | **COVERED** ✅ 39 `[Test]` + 12 `[TestCase]` |
| S20-9 processing-004 单炉空闲判定 | Logic | done | `EditMode/Processing/furnace_timing_no_process_state_test.cs` | **COVERED** ✅ 31 条 |
| S20-10 inventory-001 InventoryOf 折叠 | Logic | done | `EditMode/Inventory/inventory_fold_test.cs` | **COVERED** ✅ |
| S20-11 inventory-003 堆叠/容器/折重 | Logic | done | `EditMode/Inventory/stacking_container_rules_test.cs` + `EditMode/Sim/gate_wiring_manifest_test.cs` | **COVERED** ✅ |
| S20-12 inventory-004 CanCarry/载重 | Logic | done | `EditMode/Inventory/carry_load_adjudication_test.cs` + `Sim/gate_wiring_manifest_test.cs` | **COVERED** ✅ |
| S20-13 processing-003 Craft 落流 | Logic | **done** | `EditMode/Processing/craft_ignition_guards_test.cs` + `PlayMode/craft_event_payload_test.cs` | **COVERED** ✅ 15 `[Test]` + PlayMode 件 |
| S20-14 inventory-005 事务模板 | Logic | **done** | `EditMode/Inventory/drop_event_writer_guards_test.cs` + `no_negative_ack_test.cs` + `PlayMode/inventory_atomicity_test.cs` | **COVERED** ✅ 32 + 8 + 9 |
| S20-15 foraging-006 novelty 接线 | Logic | **done** | `EditMode/Foraging/harvest_novelty_wiring_test.cs` | **COVERED** ✅ 11 `[Test]` |
| S20-16 foraging-003 三事件 + 身份铸造 | Logic | done | `Foraging/harvest_orchestrator_test.cs` | **COVERED** ✅ |

**Summary**: **COVERED 16** · **EXPECTED 1** · **MISSING 0** · **MANUAL 0**(17 件)

> 🔁 **2026-10-27 二次订正(链尾四件完成后 · 本条为最新)**:原写 `COVERED 11 · EXPECTED 6`。
> 该值是**本报告执行时刻**(`smoke-fix` @ 01:13–01:20,commit `5bdd226`)的快照 ——
> 当时 S20-4/8/13/14/15 五件尚未落地。**同日后续**这五件全部完成并各有测试件在盘:
>
> | 件 | 测试件 | 用例 |
> |---|---|---|
> | S20-4 | `EditMode/Processing/admissible_predicate_test.cs` | 37 `[Test]` |
> | S20-8 | `EditMode/Processing/f1_call_contract_test.cs` | 39 `[Test]` + 12 `[TestCase]` |
> | S20-13 | `EditMode/Processing/craft_ignition_guards_test.cs` + `PlayMode/craft_event_payload_test.cs` | 15 `[Test]` |
> | S20-14 | `EditMode/Inventory/drop_event_writer_guards_test.cs` + `no_negative_ack_test.cs` + `PlayMode/inventory_atomicity_test.cs` | 32 + 8 + 9 |
> | S20-15 | `EditMode/Foraging/harvest_novelty_wiring_test.cs` | 11 `[Test]` |
>
> ⇒ `COVERED 11 → 16`;`EXPECTED 6 → 1`(仅剩 S20-7 治理轮,零新代码,载体是债册与证据件)。
> **`MISSING 0` 不变** —— 它本来就成立,变的只是「已覆盖」的分子。
>
> 复算(可直接跑):
> ```bash
> grep -c '^| S20-' production/qa/smoke-2026-10-27-sprint20.md          # 17
> grep -c '^  - id: "S20-' production/sprint-status.yaml                # 17
> grep -c 'COVERED' production/qa/smoke-2026-10-27-sprint20.md          # 见下表逐行
> # 五件的测试件在盘:
> ls unity/Assets/Tests/EditMode/Processing/{admissible_predicate,f1_call_contract,craft_ignition_guards}_test.cs \
>    unity/Assets/Tests/EditMode/Inventory/{drop_event_writer_guards,no_negative_ack}_test.cs \
>    unity/Assets/Tests/EditMode/Foraging/harvest_novelty_wiring_test.cs
> ```
>
> > 🔁 **2026-10-27 一次订正(QA 签核 C-7,原文不删)**:更早一版写 `COVERED 10`,
> > 因**整行缺席 `S20-2a`**(表内 16 行 vs yaml 17 件)。补行后 `10 → 11`。
> > ⚠️ 缺席 ≠ 无证据 —— `S20-2a` 载体实测在盘(8 + 12),故 `MISSING 0` 当时也成立,
> > 错的只是「逐件核」这句自述(覆盖率被**低估**,方向与 C-3 同)。

### 🔁 2026-10-27 加注:6 条 `EXPECTED` 混了两种语义(QA 签核 C-3)

**问题**:上表 6 条 `EXPECTED` 被同一顶帽子盖着,但它们**不是同一种东西** ——
其中 4 条是「**还没开工**」,2 条是「**本报告执行的那一刻还没落地**」。
混在一起会被下游读成「S20-4 / S20-7 没有测试」,从而**系统性低估覆盖率**。

**拆分(状态截至 `2026-10-27` 收口轮)**:

| 语义 | 件 | 本报告执行时 | 收口轮现状 | 复算 |
|---|---|---|---|---|
| **① 未开工**(无「应有而未有的证据」) | `S20-8` `S20-13` `S20-14` `S20-15` | 未开工 | **仍未开工**(1 `ready-for-dev` + 3 `backlog`),结转 sprint-21 | `grep -A6 'id: "S20-8"' production/sprint-status.yaml \| grep status` |
| **② 执行时点未落地**(当日补齐) | `S20-4` | `ready-for-dev`,夹具 `[ ] Not yet created` | ✅ **已落地**:`admissible_predicate_test.cs` **29** 条 · story `Complete` | 见下方命令 |
| **② 执行时点未落地**(当日补齐) | `S20-7` | `ready-for-dev`,零新代码 | ✅ **已落地**:债册 §「2026-10-27 · S20-7 治理盘点轮」(`docs/tech-debt-register.md:4460`)· story `Complete` | `grep -n "S20-7 治理盘点轮" docs/tech-debt-register.md` |

**② 的复算命令**(S20-4,自包含):

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "
import xml.etree.ElementTree as ET
r = ET.parse('unity/Logs/s20-7-full.xml').getroot()
print(len([t for t in r.iter('test-case')
           if 'AdmissiblePredicateTest' in (t.get('fullname') or '')]))"
# 实跑:29
```

**⇒ 修正后的口径**:本报告执行时 **COVERED 11 · EXPECTED 6(4 未开工 + 2 当日补齐)**;
到 `2026-10-27` 收口轮,其中 **2 条已转 COVERED** ⇒ **有效 COVERED 13 · 未开工 4 · MISSING 0**。
⚠️ **原 16 行原文不删**(它们是本报告执行时刻的真实快照),本节只作语义拆分;
**唯一的结构性改动是 2026-10-27 补入 `S20-2a` 行**(C-7 处置,见下节)—— 该行本应存在,是**补漏**不是改写。

> 🔁 **2026-10-27 三度订正(本节写完后 · 原文不删)**:上表「① 未开工」四件
> (`S20-8` `S20-13` `S20-14` `S20-15`)**同日全部落地**,各自夹具有实测用例数
> (39+12 / 15+PlayMode / 32+8+9 / 11)。⇒ 「有效 COVERED 13 · 未开工 4」**已失效**,
> 现为 **COVERED 16 · 未开工 0 · EXPECTED 1 · MISSING 0**(仅剩 S20-7 治理轮,零新代码)。
> 复算:
> ```bash
> for f in Processing/f1_call_contract Processing/craft_ignition_guards \
>          Inventory/drop_event_writer_guards Inventory/no_negative_ack \
>          Foraging/harvest_novelty_wiring; do
>   printf '%-45s %s\n' "$f" "$(grep -c '\[Test\]' unity/Assets/Tests/EditMode/$f\_test.cs)"
> done
> # 实得:39 / 15 / 32 / 8 / 11
> ```
> ⚠️ 本节保留的价值 = **记录当时的语义拆分动作**(把「未开工」与「执行时点未落地」分开);
> 它拆分得对,只是**被拆分的那批件**随后都做完了。

---

### ✅ 2026-10-27 处置:原「两处缺陷」已修(C-7 补行 / C-8 改实测值)

> **本节原先写的是「已登记待裁,本注未修」**。用户 **2026-10-27 裁定:「C-7 补行,C-8 改成实测值」**
> ⇒ 两条**均已落地**。下文保留原登记文本(原文不删),每条末尾追加 ✅ 处置行。

**缺陷 ①(C-7):覆盖率表整行缺席 `S20-2a`** — ✅ **已补行**

- **原事实**:表内 `S20-*` 行 = **16** 条;`production/sprint-status.yaml` 件数 = **17**(含 `S20-2a`)。
  复算:`grep -c '^  - id: "S20-' production/sprint-status.yaml` ⇒ **17**;
  表内 `grep -c '^| S20-'` ⇒ **16**(修前)。
- **缺席件**:`S20-2a`(TD-building-01 两半全补)—— `status: done` @ `2026-10-25`,
  载体实测在盘:`ModularBuilding/world_end_to_end_test.cs`(**8**)+ `demolish_refund_from_catalog_test.cs`(**12**)。
- **后果**:`MISSING 0` 这个断言**成立**(缺席 ≠ 无证据),但「逐件核」这句**不成立** ——
  表内少一件,且少的是**有证据的那一件** ⇒ 覆盖率被**低估**,方向与 C-3 同。
- **✅ 处置**:已在上表 `S20-2` 之后补入 `S20-2a` 行(载体 8 + 12);Summary `COVERED 10 → 11`。
  复算:`grep -c '^| S20-' production/qa/smoke-2026-10-27-sprint20.md` ⇒ **17** ✅

**缺陷 ②(C-8):报告头部的件数与状态分布与本报告自己的三元组 commit 不符** — ✅ **已改实测值**

- **原头部写**(`:4`):「16 件中 **8 件 done**、3 件 ready-for-dev、5 件 backlog」。
- **实测**(本报告三元组 commit `5bdd226`):**17 件**,`done 11` / `ready-for-dev 3` / `backlog 3`。
  复算:
  ```bash
  git show 5bdd226:production/sprint-status.yaml | python3 -c "
  import sys, re, collections
  t = sys.stdin.read(); c = collections.Counter()
  for m in re.finditer(r'- id: \"(S20-[0-9a-z]+)\"', t):
      e = t.find('\n  - id:', m.end()); seg = t[m.start():e if e != -1 else len(t)]
      c[re.search(r'\n    status: (\S+)', seg).group(1)] += 1
  print(sum(c.values()), dict(c))"
  # 实跑:17 {'done': 11, 'ready-for-dev': 3, 'backlog': 3}
  ```
- **同时**:原表内 16 行的「状态」列自算 = `done 10 / ready 3 / backlog 3`,**也不等于**头部的 `8/3/5`。
  ⇒ 头部的三个数**既不匹配 yaml,也不匹配本表** —— 是**陈旧值**(最接近 `ebe532e` 时刻的
  `17 件 / done 8 / ready 3 / backlog 6`,仍差 backlog 1 与总数 1)。
- **性质**:这是**现行态断言**而非历史陈述,按 control-manifest 二分本可直接改。
- **✅ 处置**:头部已改为 `17 件 / done 11 / ready 3 / backlog 3`(与三元组 commit 一致)。
  **补入 `S20-2a` 后表内 17 行自算 = `done 11 / ready 3 / backlog 3` ⇒ 三方现已全部对齐**:
  头部 = yaml = 本表。复算:见上方 `5bdd226` 命令 + `grep -c '^| S20-'` ⇒ 17。

> 📌 **S20-16 订正**:卡面 `:102` 有**已划掉**的 `~~gather_identity_reflection_test.cs~~ — 不存在`。
> 本表按未划掉的载体计。划掉项是卡面自己留的闭环记录,不是遗漏(初扫时我误报过一次,已收回)。

> ⚠️ **S20-5 / 6 / 7 的 Type 为推断** —— 三件在 `sprint-status.yaml` 里指向 `docs/tech-debt-register.md`,
> 债册条目格式不含 `Type` 字段。已在 QA plan 同注。

---

## Manual Smoke Checks

### A–D 全 22 条 · **N/A(依清单自述)**

`tests/smoke/critical-paths.md` 末尾明写:

> **本文件当前是清单,不是可执行门**。可执行化与实现轮同批;
> 在那之前,任何 `/smoke-check` 调用都应对 A–D 全部报 **N/A** 而非 PASS。

且「条目状态速记」表五组的「当前可跑?」**全部 ❌**:

| 组 | 依赖的最小实现面 | 当前可跑? |
|---|---|---|
| A(1–4 启动/主菜单/60fps) | Unity 工程根 + 主菜单场景 | ❌ |
| B 5-6(Step 20Hz / 病人 ≤24) | sim 骨架 + 病人出现 | ❌ |
| B 7-9(采药/拾取/诊断) | 世界流 + 8 体征 + 37 | ❌ |
| B 10(急救双事件) | 10 急救 + ADR-001 pipe | ❌ |
| C 15-18(存档/checkpoint/校验和/往返哈希) | 7a 持久化 | ❌ |
| D 19-22(内存/延迟/帧预算/跨平台哈希) | ADR-012 三格 + F7 spike | ❌ |

⇒ **22 条一律 N/A**。**这不是「通过了」,是「今天没有可观察对象」。**

### Batch 1 — 核心稳定性
用户答复:**未见异常**。因无可运行构建,实际为 **N/A(无对象)**,不按 PASS 计。

### Batch 2 — sprint 改动与回归
用户答复:**未见异常**。
S20-9 回归无异常,佐证是本件**零生产调用方**(`FurnaceState` / `IgnitionAdmission` 纯谓词,`Tests/` 外零命中)。

### Batch 3 — 数据完整性与性能
用户答复:**均无异常**。7a 未接入生产路径 ⇒ 无存读周期可观测;无构建 ⇒ 帧率未观测。

---

## Missing Test Evidence

**无。** **COVERED 16** · 1 EXPECTED · **0 MISSING**(17 件;`S20-2a` 已按 C-7 补行)

> 🔁 **2026-10-27 三度订正(原文不删)**:原写「11 COVERED · 6 EXPECTED」= 本报告执行时刻的快照。
> 链尾五件(S20-4 / 8 / 13 / 14 / 15)同日落地后,`EXPECTED` 只剩 `S20-7`(治理轮,零新代码)
> ⇒ 见上方 §Test Coverage 的三度实测表。**`0 MISSING` 两版都成立**,变的只是分子。

但须点名两处**证据件自身的问题**(证据件也是证据件,不是例外):

| # | 项 | 事实 |
|---|---|---|
| 1 | **PlayMode 基线已失效** | `202/202/0/0`(@ 10-25 12:35Z)跑于 `InventoryFold.cs` 入库前。新基线 = `204/204/0/0` @ `2026-10-27 01:16 CST` · `smoke-fix` · `5bdd226`+2 改动。**🔁 又一次失效**:链尾四件后为 `229/228/0/1` @ `2026-10-27T09:34Z` · `S20-15 批` · `e095b7b`(见上方三度实测表)。 |
| 2 | **A–D 无可执行化** | `tests/smoke/critical-paths.md` 至今零可执行判据。不是本轮的债,但**是 P0 收口前必须兑现的债** —— 否则每次 smoke-check 的 A–D 只能记 N/A。**已立债册条目 `TD-smoke-01`**(`docs/tech-debt-register.md`,2026-10-27 三十六订,`P0 面 = N`)。 |

---

## Verdict: **PASS**

- ✅ 两套件零红(EditMode 退出码 0 · PlayMode 退出码 0)
- ✅ Batch 1 / 2 / 3 无用户报告的失败
- ✅ 覆盖率零 MISSING

> ⚠️ **本 PASS 的边界**:A–D 22 条是 **N/A 不是 PASS**;PlayMode 的 204 条里新增的 2 条是**扫描器自证**,
> 不是新的游戏行为覆盖。可玩面在本轮**未被任何自动化判据覆盖**(无构建可跑)。
>
> 🔁 **2026-10-27 三度实测后的边界(补注,原判不改)**:链尾五件落地后两套件为
> EditMode `5529/5494/0/35` · PlayMode `229/228/0/1`(三元组见上方三度实测表)。
> **PASS 判定不变** —— 零红依旧。但边界**更宽了一点**:新增的用例全部是
> **Logic / Integration 级**(编排器、写者守卫、载荷落流),**仍无一条覆盖可玩面**;
> A–D 22 条**仍是 N/A**。⇒ 「PASS」这个字在这份报告里始终只表示
> **自动化套件零红 + 用户确认无异常**,不表示游戏能玩。

---

## 🔁 收口后追加批覆盖(2026-10-28 · 原文不删,本节为补注)

> **为什么补这一节**:本报告是 **2026-10-27 收口时点**的快照。收口后用户又追加了
> **4 件**(`S20-17` World 装配 · `S20-18` World 生命周期 · `S20-19` 42 建造发起方 ·
> `S20-20` 最小关卡工具),它们**不在本报告的扫描面内**(`grep -c "S20-1[789]\|S20-20"`
> 本文件 = **0**)。**不补这一节,本报告的 PASS 会被读成覆盖了那 4 件。**

### 追加批的自动化证据(三元组)

截至时刻 `2026-10-28 14:47 CST` · 批次 `S20-20 评审修复轮 · 全量` · commit `78d12421` + 未提交改动

| 套件 | 结果 | 相对本报告**最新**基线(`5552/5517/0/35` · `229/228/0/1`,C-4 修复轮) |
|---|---|---|
| EditMode | `5698 / 5663 / 0 / 0 / 35` | **+146 / +146**,零红 |
| PlayMode | `238 / 234 / 0 / 2 / 2` | **+9 / +6**,零红(inconclusive 0→2 · skipped 1→2) |

> ⚠️ **基线取值说明(禁借绿)**:本文件里有**三组**不同时点的数 ——
> `5385/5350`(smoke-fix 轮 `5bdd226`)→ `5529/5494`(链尾)→ **`5552/5517`(C-4 修复轮,最新)**。
> 本节取**最新那组**作基线,因为它是本报告内时间最晚的实跑值。
> PlayMode 侧 `229/228/0/1` 在文件里明写「**是引用值,不是实跑**」(C-4 修复轮零 PlayMode 改动);
> 本节 `238/234/0/2/2` 是**真跑**(`unity/Logs/s20-fix-final-playmode.xml`)。

```bash
python3 -c "
import xml.etree.ElementTree as ET
for f in ('unity/Logs/s20-fix-final-editmode.xml','unity/Logs/s20-fix-final-playmode.xml'):
    r=ET.parse(f).getroot()
    print(f, {k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})
"
```

### 追加批的证据载体

| 件 | 实现证据 | 评审原件 | 卡面 |
|---|---|---|---|
| `S20-17` | `impl-s20-17-world-assembly-2026-10-28.md` | `review-s20-17-2026-10-28.md` | `sprint-20.md` §十 |
| `S20-18` | `impl-s20-18-world-lifecycle-2026-10-28.md` | `review-s20-18-2026-10-28.md` | 同上 |
| `S20-19` | `impl-s20-19-build-initiator-2026-10-28.md` · `fix-a5-sole-player-2026-10-28.md` | `review-s20-19-2026-10-28.md` | 同上 |
| `S20-20` | `fix-s20-20-baked-initial-baseline-2026-10-28.md` · `fix-s20-20-slot-provider-surface-2026-10-28.md` | `review-s20-20-2026-10-28.md` | 同上 + §十二/§十三 |

### ⚠️ 本节的边界(不得把 PASS 读成「游戏能玩」)

**追加批 4 件的挂标一条未勾**,逐条:
`S20-17` 5 条(世界烘焙数据仍零产出 · `Place/Remove` 生产发起方归 42 · `ClinicCellSource` 接真源 ·
`World` 生命周期后果 · `PlaceableChecker` 不接)· `S20-18` 6 条 · `S20-19` 7 条 ·
`S20-20` 5 条(#3 C1–C6 七项未实现 · #4 单关卡 · #5 世界几何仍合成 —— 已由
`production/epics/level-tool/` 承接,**立件 ≠ 缺口已闭**)。
⇒ **本报告的 PASS 语义不变**:自动化套件零红 + 用户确认无异常,**不表示游戏能玩**。

---

## 复算命令(自包含)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# EditMode(修复轮)—— 预期 5385 5350 0 0 35
python3 -c "import xml.etree.ElementTree as ET;r=next(ET.parse('unity/Logs/smokefix-editmode-full.xml').iter('test-run'));print(r.get('total'),r.get('passed'),r.get('failed'),r.get('inconclusive'),r.get('skipped'))"

# PlayMode(修复轮)—— 预期 204 204 0 0 0
python3 -c "import xml.etree.ElementTree as ET;r=next(ET.parse('unity/Logs/smokefix-playmode-full.xml').iter('test-run'));print(r.get('total'),r.get('passed'),r.get('failed'),r.get('inconclusive'),r.get('skipped'))"

# 首轮那份红(留档)—— 预期 202 201 1 0 0
python3 -c "import xml.etree.ElementTree as ET;r=next(ET.parse('unity/Logs/smoke-2026-10-27-playmode.xml').iter('test-run'));print(r.get('total'),r.get('passed'),r.get('failed'),r.get('inconclusive'),r.get('skipped'))"

# 该门现在 9 条全绿
python3 -c "import xml.etree.ElementTree as ET;r=next(ET.parse('unity/Logs/fix-gate-v2.xml').iter('test-run'));print(r.get('total'),r.get('passed'),r.get('failed'))"
```

> ⚠️ **跑 Unity 测试时 `-batchmode … -runTests` 不得带 `-quit`** —— 会让 Unity 在测试启动前退出,
> 20 秒空跑、零 XML(本 session 2026-10-26 实测踩过一次)。

### 🔁 三度实测的复算命令(本条为最新基线)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# EditMode(链尾收口)—— 预期 5529 5494 0 0 35
python3 -c "import xml.etree.ElementTree as ET;r=next(ET.parse('$CLAUDE_JOB_DIR/tmp/reg-edit-s2015.xml').iter('test-run'));print(r.get('total'),r.get('passed'),r.get('failed'),r.get('inconclusive'),r.get('skipped'))"

# PlayMode(链尾收口)—— 预期 229 228 0 0 1
python3 -c "import xml.etree.ElementTree as ET;r=next(ET.parse('$CLAUDE_JOB_DIR/tmp/reg-play-s2015.xml').iter('test-run'));print(r.get('total'),r.get('passed'),r.get('failed'),r.get('inconclusive'),r.get('skipped'))"
```

> ⚠️ **XML 在 `$CLAUDE_JOB_DIR/tmp/` 是 job 临时目录,会被清理** —— 上面的数值同时
> 抄进了 `production/qa/evidence/review-s20-15-2026-10-27.md:247-252`(含 §7.1 逐项增量等式),
> 那是**持久件**。要长期复算请引那个文件,不要依赖本命令能重跑。
