# Smoke Check Report(S20 · sprint-20)

**Date**: 2026-10-27
**Sprint**: sprint-20(16 件中 **8 件 done**、3 件 ready-for-dev、5 件 backlog)
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

sprint-20 全部 16 件,逐件核声明的证据路径(两棵树都扫过:`unity/Assets/Tests/EditMode|PlayMode/` 与 `tests/`)。

| Story | Type | 状态 | 声明载体 | 实际 |
|---|---|---|---|---|
| S20-1 clinic-004 交付边界 | Integration | done | `EditMode/ClinicMachine/clinic_env_delivery_test.cs` | **COVERED** ✅ |
| S20-2 clinic-005 纯度重放 + Memoize | Integration | done | `EditMode/ClinicMachine/clinic_purity_replay_test.cs` | **COVERED** ✅ |
| S20-3 foraging-001 品种闭包 + 装载校验 | Logic | done | `EditMode/Foraging/foraging_data_load_test.cs` + `foraging_bake_wiring_test.cs` | **COVERED** ✅ |
| S20-4 processing-001 配方子集 + Admissible | Logic | ready-for-dev | `EditMode/Processing/admissible_predicate_test.cs` | **EXPECTED** —— `[ ] Not yet created` |
| S20-5 判别力收紧轮(TD-clinic-02/08/09) | Logic(推断) | done | 证伪轮 + 收紧测 | **COVERED**(`ClinicMachine/` 11 件) |
| S20-6 TD-prescription-07 材质分类器 | Logic(推断) | done | 单测 + 反证 | **COVERED** |
| S20-7 治理盘点轮 | Config/Data(推断) | ready-for-dev | 无(零新代码) | **EXPECTED** |
| S20-8 processing-002 F1 调用契约 | Logic | ready-for-dev | — | **EXPECTED** —— `[ ] Not yet created` |
| S20-9 processing-004 单炉空闲判定 | Logic | done | `EditMode/Processing/furnace_timing_no_process_state_test.cs` | **COVERED** ✅ 31 条 |
| S20-10 inventory-001 InventoryOf 折叠 | Logic | done | `EditMode/Inventory/inventory_fold_test.cs` | **COVERED** ✅ |
| S20-11 inventory-003 堆叠/容器/折重 | Logic | done | `EditMode/Inventory/stacking_container_rules_test.cs` + `EditMode/Sim/gate_wiring_manifest_test.cs` | **COVERED** ✅ |
| S20-12 inventory-004 CanCarry/载重 | Logic | done | `EditMode/Inventory/carry_load_adjudication_test.cs` + `Sim/gate_wiring_manifest_test.cs` | **COVERED** ✅ |
| S20-13 processing-003 Craft 落流 | Logic | backlog | — | **EXPECTED** —— `[ ] Not yet created` |
| S20-14 inventory-005 事务模板 | Logic | backlog | — | **EXPECTED** —— `[ ] Not yet created` |
| S20-15 foraging-006 novelty 接线 | Logic | backlog | — | **EXPECTED** —— `[ ] Not yet created` |
| S20-16 foraging-003 三事件 + 身份铸造 | Logic | done | `Foraging/harvest_orchestrator_test.cs` | **COVERED** ✅ |

**Summary**: **COVERED 10** · **EXPECTED 6** · **MISSING 0** · **MANUAL 0**

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

**无。** 10 COVERED · 6 EXPECTED · **0 MISSING**。

但须点名两处**证据件自身的问题**(证据件也是证据件,不是例外):

| # | 项 | 事实 |
|---|---|---|
| 1 | **PlayMode 基线已失效** | `202/202/0/0`(@ 10-25 12:35Z)跑于 `InventoryFold.cs` 入库前。新基线 = `204/204/0/0` @ `2026-10-27 01:16 CST` · `smoke-fix` · `5bdd226`+2 改动。 |
| 2 | **A–D 无可执行化** | `tests/smoke/critical-paths.md` 至今零可执行判据。不是本轮的债,但**是 P0 收口前必须兑现的债** —— 否则每次 smoke-check 的 A–D 只能记 N/A。 |

---

## Verdict: **PASS**

- ✅ 两套件零红(EditMode 退出码 0 · PlayMode 退出码 0)
- ✅ Batch 1 / 2 / 3 无用户报告的失败
- ✅ 覆盖率零 MISSING

> ⚠️ **本 PASS 的边界**:A–D 22 条是 **N/A 不是 PASS**;PlayMode 的 204 条里新增的 2 条是**扫描器自证**,
> 不是新的游戏行为覆盖。可玩面在本轮**未被任何自动化判据覆盖**(无构建可跑)。

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
