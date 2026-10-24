# Smoke Check Report — Sprint 19

**Date**: 2026-10-24
**Sprint**: 19 — AC 门清偿(G-4 + `TD-persist-01` 剩余两项)
**Engine**: Unity 6.3 LTS (6000.3.24f1)
**QA Plan**: `production/qa/qa-plan-sprint-19-2026-10-24.md`
**Argument**: `sprint`
**Review Mode**: `lean`

**基线溯源三元组(强制 · S12-0)**:

| 套件 | 截至时刻 | 批次 | commit |
|---|---|---|---|
| EditMode | `2026-10-24 19:49 CST (+0800)` | `s19-smoke-editmode`(全量) | `9e23c42` |
| PlayMode | `2026-10-24 19:51 CST (+0800)` | `s19-smoke-playmode`(全量) | `9e23c42` |

> ⚠️ **工作树在跑测时含 13 项未提交改动**(11 改 + 2 新)。
> `commit` 记的是 **HEAD**,不是「被验的那棵树就是 `9e23c42`」。
> 被验的树 = `9e23c42` + 13 项未提交改动。**下游引用本报告时不得把 `9e23c42` 读成完整基线。**

---

## Phase 1 — 环境

| 项 | 值 |
|---|---|
| 测试目录 | `tests/` 存在(`EditMode PlayMode unit integration smoke golden evidence`) |
| CI | 已配置(`.github/workflows/tests.yml` · `unity-tests.yml`) |
| 引擎 | Unity 6.3 LTS(`.claude/docs/technical-preferences.md`) |
| Smoke 清单 | `tests/smoke/critical-paths.md`(A/B/C/D 四节) |
| QA 计划 | `production/qa/qa-plan-sprint-19-2026-10-24.md` |

---

## Automated Tests

**Status**: ✅ **PASS**

| 套件 | 退出码 | XML | 本次结果 | 对照基线(同带三元组) |
|---|---|---|---|---|
| EditMode | 0 | `unity/Logs/smoke-s19-editmode.xml` | `total=4786 passed=4751 failed=0 inconclusive=0 skipped=35` | `4736/4701/0/35` @ `2026-10-24 07:47` · `s18-29story004-final-editmode-20261024` · commit 见该 XML `start-time` |
| PlayMode | 0 | `unity/Logs/smoke-s19-playmode.xml` | `total=202 passed=202 failed=0 inconclusive=0 skipped=0` | `202/202/0/0` @ `2026-10-24 07:49` · `s18-29story004-final-playmode-20261024` |

> ⚠️ **PlayMode 是本次真跑的结果,不是引用 sprint-18** —— QA plan 逐字禁止引用 `202/202/0/0` 当本 sprint 的数
> (承「借绿的两种数字形态」)。本次证书:`start-time 2026-10-24 11:50:51Z` / `end-time 11:50:55Z` / `duration 4.71s`,
> XML 独立存在于 `unity/Logs/smoke-s19-playmode.xml`。
> 数值与 sprint-18 相同**是因为本 sprint 零 PlayMode 面改动**,不是因为照抄。

### 全名级差集(QA plan 强制,不只比总数)

**EditMode vs sprint-18 基线 `s18-29story004-final-editmode-20261024.xml`**:

| 项 | 条数 | 明细 |
|---|---|---|
| 新增 | **51** | 全部 `Passed` |
| 消失 | **1** | `FoldedPredicateTest.test_iSaveService_publicMethodSetUnchanged` |
| 结果翻转 | **0** | — |
| 本次非绿 | **0** | — |

**新增 51 条的构成**(按测试类):

| 测试类 | 新增条数 |
|---|---|
| `SaveSlotContractTest` | 36(新文件) |
| `SaveComposerWiringTest` | 13(新文件) |
| `FoldedPredicateTest` | 2(`publicMethodSetIsFive` 改名计入新增 + 1 条) |

> 注:上表 36 + 13 + 2 = 51,与差集「新增 51」对得上。可复算:

```bash
python3 - <<'PY'
import xml.etree.ElementTree as ET, collections
def load(p):
    return {tc.get('fullname'): tc.get('result') for tc in ET.parse(p).getroot().iter('test-case')}
b = load('unity/Logs/s18-29story004-final-editmode-20261024.xml')
c = load('unity/Logs/smoke-s19-editmode.xml')
cnt = collections.Counter(n.split('.')[-2] for n in set(c) - set(b))
for k, v in sorted(cnt.items()):
    print(f'{k}: {v}')
print('合计:', sum(cnt.values()))
PY
```

实跑输出:

```
FoldedPredicateTest: 2
SaveComposerWiringTest: 13
SaveSlotContractTest: 36
合计: 51
```

**消失项归因 —— 不是丢失,是改名**。可复算:

```bash
git show 41b7d87 -- unity/Assets/Tests/EditMode/PersistenceService/folded_predicate_test.cs \
  | grep -nE "^[-+].*(publicMethodSet|IsFive|Unchanged)"
# 实跑输出:
# 81:-        public void test_iSaveService_publicMethodSetUnchanged()
# 82:+        public void test_iSaveService_publicMethodSetIsFive()
```

⇒ 实质差集 = **+51 / −0 / 翻转 0 / 红 0**,判据 **PASS**。

**EditMode vs 本 sprint 自身 `s19-1-final.xml`(可复现性对照)**:`+0 / −0 / 翻转 0 / 红 0`,
两次全量结果**逐名一致**。

**PlayMode vs sprint-18 基线**:`+0 / −0 / 翻转 0 / 红 0`,判据 **PASS**。

### Skipped 35 条的构成(证明无新增跳过)

35 条 `Skipped` 全部来自既有测试类,分布与 sprint-18 基线**同值**:

| 测试类 | skipped | 测试类 | skipped |
|---|---|---|---|
| `CameraObligationsReconciliationTest` | 6 | `LocomotionChainTest` | 2 |
| `FeelLatencyTest` | 3 | `ReconstructionAndWritePathTest` | 2 |
| `FocusGateTransitionTest` | 3 | `DiscoveryReportTest` | 2 |
| `TutorialTest` | 3 | 其余 13 类各 1 条 | 13 |
| **合计** | | | **35** |

⇒ **S19-1 零新增 `Skipped`**。

---

## Test Coverage

| 工作项 | 类型 | 测试载体 | 覆盖状态 |
|---|---|---|---|
| **S19-1** G-4 + `TD-persist-01` 剩余两项 | Integration | `EditMode/PersistenceService/save_slot_contract_test.cs`(36 条)· `save_composer_wiring_test.cs`(13 条) | ✅ **COVERED** |
| S19-2 G-5(`IPlayerMotor`) | Logic | — | **不在本 sprint 面内**(`backlog`) |
| S19-3 G-6(`LogiPose` / `ITameable`) | Logic | — | 同上 |
| S19-4 G-7(`DeferredThreatSlot`) | Integration | — | 同上 |
| S19-5 记账订正 | Config/Data | — | 同上 |

**Summary**:1 COVERED · 0 MANUAL · 0 MISSING · 4 不在本 sprint 面内。

**范围依据**(可复算):

```bash
python3 -c "
import re
t=open('production/sprint-status.yaml',encoding='utf-8').read()
for m in re.finditer(r'- id: \"(S19-[0-9])\"\n(?:.*\n){0,8}?\s+status: \"([^\"]+)\"', t):
    print(m.group(1), '->', m.group(2))"
# 实跑输出:
# S19-1 -> done
# S19-2 -> backlog
# S19-3 -> backlog
# S19-4 -> backlog
# S19-5 -> backlog
```

> QA plan 原计划的 ~28 测,实测 **36 条 `SaveSlotContractTest`** —— **高于计划,不是缺口**。

---

## Manual Smoke Checks

**本 sprint 不适用**。QA plan §Smoke Test Scope 第 3 条逐字:

> 3. **不要求**启动可玩性 smoke —— 本 sprint 零玩家可见变化

⇒ **不跑** Batch 1/2/3 的人工问询,也**不得**记为 FAIL 或 NOT RUN。

理由:S19-1 的四件交付全是契约 / 谓词 / 内部状态载体,且**不开任何下游 story**
(`save-slot-ui` 仍 `BLOCKED-BY-7a`),零运行期可玩行为变化。

---

## Missing Test Evidence

**无。** S19-1 是本 sprint 唯一需自动化证据的工作项,两处载体均在盘上且全绿。

**同批登记的一条非 smoke 面事项**(不计入本报告判定):
QA plan §未及核查项 7 记「`tests/integration/persistence-service/` 目录当前不存在」。
实测该目录仍不存在 —— 账本树(`tests/`)与编译树(`unity/Assets/Tests/`)的这条差
属**已知且已登记**(承 `coding-standards.md` §Test Evidence by Story Type 的 2026-10-15 注:
两棵树都可能承载活测试,编译树是主体),**不构成本 sprint 的证据缺口**。

---

## Verdict: ✅ **PASS**

判定规则逐条:

- 自动化测试跑过且零失败 ✅
- 全名级差集:无实质消失(唯一消失项经 `git show` 归因为改名)/ 无翻转 / 无红 ✅
- 无 MISSING 测试证据 ✅
- 人工 smoke 项 = **不适用**(非失败)✅

---

## 可跑复算命令(承 `coding-standards.md` §数字须带可跑复算命令)

以下命令**自包含**(不依赖任何临时脚本),在仓库根逐条可跑。

### ① EditMode 计数

```bash
python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('unity/Logs/smoke-s19-editmode.xml').getroot(); print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
```

实跑输出:

```
{'total': '4786', 'passed': '4751', 'failed': '0', 'inconclusive': '0', 'skipped': '35'}
```

### ② PlayMode 计数

```bash
python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('unity/Logs/smoke-s19-playmode.xml').getroot(); print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
```

实跑输出:

```
{'total': '202', 'passed': '202', 'failed': '0', 'inconclusive': '0', 'skipped': '0'}
```

### ③ S19-1 两测试类条数

```bash
python3 -c "
import xml.etree.ElementTree as ET, collections
r=ET.parse('unity/Logs/smoke-s19-editmode.xml').getroot()
c=collections.Counter(t.get('fullname').split('.')[-2] for t in r.iter('test-case') if '.PersistenceService.' in t.get('fullname',''))
print({k:c[k] for k in ('SaveSlotContractTest','SaveComposerWiringTest')})"
```

实跑输出:

```
{'SaveSlotContractTest': 36, 'SaveComposerWiringTest': 13}
```

### ④ 全名级差集(基线 vs 本次 · 自包含)

```bash
python3 - <<'PY'
import xml.etree.ElementTree as ET

def load(p):
    return {tc.get('fullname'): tc.get('result')
            for tc in ET.parse(p).getroot().iter('test-case')}

b = load('unity/Logs/s18-29story004-final-editmode-20261024.xml')
c = load('unity/Logs/smoke-s19-editmode.xml')

added   = sorted(set(c) - set(b))
removed = sorted(set(b) - set(c))
flipped = sorted(n for n in set(b) & set(c) if b[n] != c[n])
reds    = sorted(n for n, r in c.items() if r not in ('Passed', 'Skipped'))

print('新增', len(added))
print('消失', len(removed), removed)
print('翻转', len(flipped), flipped)
print('非绿', len(reds), reds)
PY
```

实跑输出:

```
新增 51
消失 1 ['DaYiJingCheng.Tests.PersistenceService.FoldedPredicateTest.test_iSaveService_publicMethodSetUnchanged']
翻转 0 []
非绿 0 []
```

### ⑤ PlayMode 差集(把 ④ 的两个路径换成 PlayMode 的两份 XML 即可)

```bash
python3 - <<'PY'
import xml.etree.ElementTree as ET

def load(p):
    return {tc.get('fullname'): tc.get('result')
            for tc in ET.parse(p).getroot().iter('test-case')}

b = load('unity/Logs/s18-29story004-final-playmode-20261024.xml')
c = load('unity/Logs/smoke-s19-playmode.xml')
print('新增', len(set(c)-set(b)), '消失', len(set(b)-set(c)),
      '翻转', len([n for n in set(b)&set(c) if b[n]!=c[n]]))
PY
```

实跑输出:

```
新增 0 消失 0 翻转 0
```

---

## 与四个收口动作的衔接

本报告是 S19-1 收口的**第 4 步(末步)**:

| # | 动作 | 状态 |
|---|---|---|
| ① | `/code-review` | ✅ 4 必修全落 + 4 警告已修 · 原件 `production/qa/evidence/review-s19-1-2026-10-24.md` |
| ② | `/story-done`(8 阶段) | ✅ Verdict = COMPLETE WITH NOTES · Falsifiability Gate PASS(4 变异 / 5 窗) |
| ③ | `sprint-status.yaml` + 债册 | ✅ S19-1 → `done` · `TD-persist-02` → `closed` · `TD-persist-01` 加注仍 open |
| ④ | `/smoke-check sprint` | ✅ **本件** |

**未提交** —— 用户未下 commit 指令。工作树 13 项未提交改动待裁。
