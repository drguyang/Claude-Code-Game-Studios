# Smoke Check Report — Sprint 18(最终树)

**Date**: 2026-10-22
**Sprint**: 18 — 链门清偿(G-1 · G-2)
**Engine**: Unity 6.3 LTS (6000.3.24f1)
**QA Plan**: `production/qa/qa-plan-sprint-18-2026-10-21.md`
**Argument**: `sprint`
**Review Mode**: `lean`

**基线溯源三元组(强制 · S12-0)**:截至时刻 `2026-10-22 08:59 (+0800)` · 批次 `s18-2-editmode-commit-20261022` / `s18-2-playmode-final-20261022` · commit `ad7316c`

> ⚠️ **本报告取代 `smoke-2026-10-22.md`,但不删它**。那份判的是 `220acc3`,
> 其 coverage 表仍写 S18-2 = `MISSING`;`ad7316c` 在其后落了 6 个 `.cs`(1004 行)+ 29 测
> ⇒ 引用旧件当本树 PASS 即借绿。两份并存,各自带自己的三元组。

---

## Phase 1 — 环境

| 项 | 值 |
|---|---|
| 测试目录 | `tests/` 存在 |
| CI | 已配置(`.github/workflows/tests.yml` · `unity-tests.yml`) |
| 引擎 | Unity 6.3 LTS(`.claude/docs/technical-preferences.md:12`) |
| Smoke 清单 | `tests/smoke/critical-paths.md` 存在(A/B/C 三节) |
| QA 计划 | `production/qa/qa-plan-sprint-18-2026-10-21.md` |

---

## Automated Tests

**Status**: ✅ **PASS**

| 套件 | 退出码 | XML | 对照基线(同带三元组) |
|---|---|---|---|
| EditMode | 0 | `total=4418 passed=4382 failed=0 inconclusive=0 skipped=36` | `4389/4353/0/36` @ `2026-10-22 07:34 +0800` · `s18-editmode-final-20261022` · `220acc3` |
| PlayMode | 0 | `total=202 passed=202 failed=0 inconclusive=0 skipped=0` | `202/202/0/0` @ `2026-10-22 07:27 +0800` · `s18-playmode-full-20261022` · `220acc3` |

**证书文件**:
- `unity/Logs/s18-2-editmode-commit-20261022.xml`(`start-time 2026-10-22T00:57:37Z`)
- `unity/Logs/s18-2-playmode-final-20261022.xml`(`start-time 2026-10-22T00:55:54Z`)

**两轮均为本轮实跑,不是引用**:commit `ad7316c` 的提交时刻 = `2026-10-22 08:32:50 +0800`
= `00:32:50Z` ⇒ 两轮的 `start-time` 都晚于它;跑测时 `.cs` 面 `git status` 干净
(其后的改动只有 `production/` 与 `design/` 下的文档,零 `.cs`)。

**对账**:

| 量 | 值 |
|---|---|
| Δtotal(EditMode) | **+29** |
| Δpassed(EditMode) | **+29** |
| 新增条数 | **29** |
| 等式 `Δtotal = Δpassed = 新增条数` | ✅ 成立 |
| 全名集差集(EditMode) | 新增 **29** / 消失 **0** |
| 全名集差集(PlayMode) | 新增 **0** / 消失 **0**(本 sprint 零新增 PlayMode 测试,预期如此) |
| `error CS` | **0** |
| failed / 未处理异常 | **0** |
| skipped | **36**(与基线同名同数 —— 既有 `NOT-RUN` 挂标件,非本轮新增) |

**失败测试**:无。

---

## Test Coverage

| 工作项 | 类型 | Test File | Coverage Status |
|---|---|---|---|
| **S18-1** 逐实体判死谓词及依赖面 | Logic | `unity/Assets/Tests/EditMode/DiseaseSimulation/entity_death_gates_test.cs` | **COVERED** |
| **S18-2** 四条件折叠谓词 + 折叠行产出 | Logic | `unity/Assets/Tests/EditMode/PersistenceService/folded_predicate_test.cs` | **COVERED** |
| **S18-3** 台账回写(纸面) | Config/Data | — | **EXPECTED** |

**Summary**: 2 covered · 0 manual · **0 missing** · 1 expected.

> ⚠️ **载体订正(见 QA plan `:55` 订正注)**:S18-1 的原登记路径
> `self_limited_predicate_test.cs` **从未存在**;实际载体是 `entity_death_gates_test.cs`
> (commit `fc1705d` · 17 个 test 方法)。**COVERED 是按实际载体判的**,
> 不是按原登记路径判的 —— 后者若照字面核会反判成 `MISSING`。

---

## Manual Smoke Checks — **NOT RUN(按 QA 计划免测)**

QA 计划「Smoke Test Scope」逐字:*本 sprint 不改变任何运行期可玩行为 —— 两件实现都是 sim 侧谓词,
且不开任何下游 story ⇒ 不要求启动可玩性 smoke*。

**本报告不照抄该豁免,而是核它的依据**(「零运行期变化」须是可观测事实):

| 依据 | 实测命令 | 结果 |
|---|---|---|
| `EntityDeathGates` 生产调用点 | `grep -rn "EntityDeathGates\." unity/Assets --include=*.cs \| grep -v /Tests/` | **1 命中,且是注释**(`FoldedPredicate.cs:23`)⇒ 生产调用 **0** |
| `new SaveService(` 构造点 | `grep -rn "new SaveService(" unity/Assets --include=*.cs` | **3 处全在** `save_service_test.cs` ⇒ 生产构造 **0** |
| `new FoldedPredicate(` 构造点 | `grep -rn "new FoldedPredicate(" unity/Assets --include=*.cs` | **1 处**,在本轮测试件 ⇒ 生产构造 **0** |

⇒ 「零运行期变化」成立,**但成立的理由是「这些件今天还没有生产接线路径」,不是「改得很小心」**。

`tests/smoke/critical-paths.md` 的 A / B / C 三节全部条目记为 **N/A(未运行)**。
**N/A 不得读作 PASS。** 待 S18-1 的生产接线落地、或折叠接入存档路径的那一轮,本节须转为实跑。

---

## Missing Test Evidence

- **无**缺测试文件的 Logic / Integration 项。
- ⚠️ **但另登记一条(不是「缺文件」,是「被测物不存在」)**:S18-1 的
  **AC ⑦**(`DeathCandidate` 产出路径)/ **AC ⑧**(`InjuryStateChanged` 写者)——
  `IDeathCandidateSource` / `PlayerDeathWarranted` 在生产树 **0 命中**,
  `WriterExistenceGate.cs:98-102` 的 `PlayerDied` / `InjuryStateChanged` 仍挂「写者未落」。
  **smoke 只登记事实,归 QA 签核判。**

---

## Verdict: **PASS WITH WARNINGS**

**判据(按 skill Phase 5 规则逐条)**:
- 自动测试**已跑且零失败** ⇒ 不触发 FAIL
- 无 Batch 1 / Batch 2 的 FAIL(两项按 QA 计划 N/A,非 FAIL)
- 无 MISSING 测试证据条目 ⇒ 理论上够 PASS

**仍判 WARNINGS 而非 PASS 的原因**(如实登记):
1. **S18-1 的 AC ⑦⑧ 零证据载体** —— 文件存在 ≠ AC 被验证。把它记成干净 PASS 会给下游一个错误的「本 sprint 全绿」印象。
2. **人工可玩性 smoke 整节 N/A** —— 这是计划内的免测,但免测不等于验过。

⇒ **可交 QA 手动面**(本 sprint 人工项 = 0),**但不得据此认为 sprint-18 可收口** —— 收口判在 `/team-qa` 签核。

---

## 溯源

| 项 | 出处 |
|---|---|
| 件集 | `production/sprints/sprint-18.md` · `production/sprint-status.yaml` `work_items` |
| 判据面 | `production/qa/qa-plan-sprint-18-2026-10-21.md`(本 sprint 的 QA 计划,不重写) |
| 回归证书 | `unity/Logs/s18-2-editmode-commit-20261022.{xml,log}` · `unity/Logs/s18-2-playmode-final-20261022.{xml,log}` |
| 前一份 smoke(不删,已被本件取代) | `production/qa/smoke-2026-10-22.md`(判 `220acc3`) |
| 生成 | `/smoke-check sprint` @ 2026-10-22 |
