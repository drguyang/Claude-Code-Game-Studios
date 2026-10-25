# Smoke Check Report — Sprint 19(收口轮)

**Date**: 2026-10-25
**Sprint**: 19 — AC 门清偿(G-4 · G-5 · G-6 · G-7 + `TD-persist-01` 剩余两项 + 记账订正)
**Engine**: Unity 6.3 LTS(6000.3.24f1)
**QA Plan**: `production/qa/qa-plan-sprint-19-2026-10-24.md`
**Argument**: `sprint`
**Review Mode**: `lean`

**基线溯源三元组(强制 · S12-0)**:

| 套件 | 本次结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `total=4867 passed=4832 failed=0 inconclusive=0 skipped=35` | `2026-10-25 08:27 CST (+0800)`(XML `start-time 00:25:32Z` / `end-time 00:27:09Z`) | `qa-full`(全量) | `4d468a5` |
| PlayMode | `total=202 passed=202 failed=0 inconclusive=0 skipped=0` | `2026-10-25 08:27 CST (+0800)`(XML `start-time 00:27:45Z` / `end-time 00:27:49Z`) | `qa-full`(全量) | `4d468a5` |

> ✅ **被验的树 = `4d468a5` 本身** —— 跑测时 `git status --porcelain` 仅含两份**文档**改动
> (`qa-plan-sprint-19-*.md` / `sprint-19.md`,本 QA 轮的加注)与 `fz-s194/` 备份目录,
> **生产码零未提交改动**。与既有 smoke 报告(工作树含 13 项未提交改动)不同,本报告的三元组
> 可如实读作「`4d468a5` 这棵树跑出这个数」。

> 🔁 **2026-10-25 评审修复轮** —— 上表三元组**已过期**(它验的是修复前的树)。评审 W1 / W4 / W5
> 三条必修落盘后,生产码有 7 项改动(`CompositionRoot.cs` / `SaveService.cs` / `EventBudget.cs` +
> 四个测试件),**旧数不再代表当前树**。修复后重跑的两套件:
>
> | 套件 | 本次结果 | 截至时刻 | 批次 | commit |
> |---|---|---|---|---|
> | EditMode | `total=4876 passed=4841 failed=0 inconclusive=0 skipped=35` | `2026-10-25 09:12 CST (+0800)` | `s19fix-full`(全量) | `4d468a5` + **7 项未提交生产码** |
> | PlayMode | `total=202 passed=202 failed=0 inconclusive=0 skipped=0` | `2026-10-25 09:12 CST (+0800)` | `s19fix-full`(全量) | 同上 |
>
> ⚠️ **commit 字段的读法**:这两行是「`4d468a5` **加上**评审修复轮的 7 项未提交改动」跑出来的,
> **不是** `4d468a5` 本身。若日后 commit,该 commit 才是可复现的锚点。
> **全名级差集**(对 `qa-full-editmode.xml` 基线 `4867`):
> `新增 11 · 消失 2 · 翻转 0 · 非绿 0`;算术自洽 `4867 + 11 − 2 = 4876` ✓。
> 新增 11 = W5 纯函数夹具 6 + W5 装配级 1 + W1 两条 + W4 一条 + `beforeAnyAnchor` 改名 1;
> 消失 2 = 两条按裁定改名的旧测试。
> PlayMode:`新增 0 · 消失 0 · 翻转 0 · 非绿 0`(202 条逐条同名同结果)。
> 复算命令见本报告 §③;XML = `unity/Logs/s19fix-full-{editmode,playmode}.xml`。
>
> ⇒ **Verdict 不变(PASS)** —— 两套件零红、零回归,且修复轮新增的 11 条夹具全绿。

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
| EditMode | 0 | `qa-full-editmode.xml` | `total=4867 passed=4832 failed=0 inconclusive=0 skipped=35` | `4867/4832/0/35` @ `2026-10-25 07:19 CST` · `s194` 全量 · `6198671` |
| PlayMode | 0 | `qa-full-playmode.xml` | `total=202 passed=202 failed=0 inconclusive=0 skipped=0` | `202/202/0/0` @ `2026-10-24 23:11 CST` · `tdp03` 全量 · `8ba3fb4` |

> ⚠️ **两次跑测的 commit 不同不是回归**:`6198671`/`8ba3fb4` → `4d468a5` 之间只有
> `2847a4a` + `4d468a5` 两个 **纯文档** commit(S19-5 记账订正),零代码改动。
> 数字逐项相同**是这个原因**,不是照抄 —— 两份 XML 的 `start-time` 独立存在(见上表)。

### 全名级差集(QA plan 强制,不只比总数)

| 对照 | 新增 | 消失 | 翻转 | 非绿 |
|---|---|---|---|---|
| EditMode vs `s194-full` | **0** | **0** | **0** | **0** |
| PlayMode vs `tdp03-full-playmode` | **0** | **0** | **0** | **0** |

⇒ **零回归**,且是**逐测试全名**一致,不是总数巧合。

---

## Test Coverage

| 工作项 | 类型 | 测试载体(实测条数) | 覆盖状态 |
|---|---|---|---|
| **S19-1** G-4 + `TD-persist-01` 剩余两项 | Integration | `EditMode/PersistenceService/save_slot_contract_test.cs`(36)· `save_composer_wiring_test.cs`(15)· `fold_row_codec_test.cs`(8)· `eventstream_restore_test.cs`(9)· `save_replay_roundtrip_test.cs`(1) | ✅ **COVERED** |
| **S19-2** G-5(`IPlayerMotor`) | Logic | `EditMode/PlayerController/player_motor_test.cs`(**9**) | ✅ **COVERED** |
| **S19-3** G-6(`LogiPose` / `ITameable`) | Logic | `EditMode/EnemyAI/logi_pose_test.cs`(**15**) | ✅ **COVERED** |
| **S19-4** G-7(`DeferredThreatSlot`) | Integration | `EditMode/RandomEvents/deferred_threat_slot_test.cs`(**21**)· `event_budget_defer_test.cs`(7,全改写) | ✅ **COVERED** |
| **S19-5** 记账订正 | Config/Data | —(纸面:grep 双命中 + 实跑输出) | ✅ **EXPECTED** |

**Summary**:4 COVERED · 0 MANUAL · 0 MISSING · 1 EXPECTED。

**范围依据**(可复算):

```bash
python3 -c "
import re
t=open('production/sprint-status.yaml',encoding='utf-8').read()
for m in re.finditer(r'- id: \"(S19-[0-9])\"\n(?:.*\n){0,8}?\s+status: \"([^\"]+)\"', t):
    print(m.group(1), '->', m.group(2))"
# 实跑输出(2026-10-25 08:2x):
# S19-1 -> done
# S19-2 -> done
# S19-3 -> done
# S19-4 -> done
# S19-5 -> done
```

**实收 vs 规划**(详见 QA plan §实收对账):

| 件 | 规划 | 实收 | 判读 |
|---|---|---|---|
| S19-1 | ~28 | 36(+**15**+8+9+1 分支载体) | **高于计划,不是缺口**;字节半两组原标 `NOT-RUN`,已由 `TD-persist-03` 兑现(证据件 `impl-td-persist-03-replay-and-body-2026-10-24.md`) |
| S19-2 | ~15 | 9 | 四判据**合并夹具**(判据④并入 `interface_members_areComplete` 的 `Tick(float, in MoveInput)` 反射断言,:79-83),非缺覆盖 |
| S19-3 | ~12 | 15 | 多 3 = 推进算子面(`advance` 5 条 + `facingBetween` + 路由/效果分离机械面) |
| S19-4 | ~16 新 + 改 3 | 21 新 + 7 全改写 | F-1/F-2/F-3 全兑现;判据①(删直改面)与 F-1 修法①相抵,实现取判据①优先 |

---

## Manual Smoke Checks

**本 sprint 不适用**。QA plan §Smoke Test Scope 第 3 条逐字:

> 3. **不要求**启动可玩性 smoke —— 本 sprint 零玩家可见变化

⇒ **不跑** Batch 1/2/3 的人工问询,也**不得**记为 FAIL 或 NOT RUN。

理由:五件交付全是契约 / 谓词 / 内部状态载体 / 纸面记账,且**不开任何下游 story**
(`save-slot-ui` 仍 `BLOCKED-BY-7a`),零运行期可玩行为变化。

---

## Missing Test Evidence

**无。** 四件实现各有实测载体且全绿;S19-5 是 Config/Data(纸面判据 = grep + 实跑输出,
已在 `sprint-19.md` DoD 各带 🔁 勾选依据)。

**同批登记的两条非 smoke 面事项**(不计入本报告判定):

1. **账本树缺目录**(QA plan §未及核查项 7):`tests/integration/persistence-service/` 不存在。
   属**已知且已登记**的账本树 / 编译树差异(承 `coding-standards.md` §Test Evidence by Story Type
   的 2026-10-15 注:两棵树都可能承载活测试,编译树是主体),**不构成证据缺口**。
2. **`TD-persist-03` 的残余**:`AC-7a-01` / `AC-7a-02` 的语义层在 `story-001` 标 `Complete` 时
   零测试(QA plan §未及核查项 1 的 `R-2`,记为「第五个假绿形态」)⇒ **须另立评审项**,
   本 sprint 不代修。

---

## Verdict: ✅ **PASS**

判定规则逐条:

- 自动化测试跑过且零失败(两套件 `failed=0`)✅
- 全名级差集:新增 0 / 消失 0 / 翻转 0 / 非绿 0 ✅
- 无 MISSING 测试证据 ✅
- 人工 smoke 项 = **不适用**(非失败)✅

---

## 可跑复算命令(承 `coding-standards.md` §数字须带可跑复算命令)

### ① EditMode 计数

```bash
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' \
  /home/gu/.claude/jobs/181888b1/tmp/qa-full-editmode.xml | head -1
```

实跑输出:

```
total="4867" passed="4832" failed="0" inconclusive="0" skipped="35"
```

### ② PlayMode 计数

```bash
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' \
  /home/gu/.claude/jobs/181888b1/tmp/qa-full-playmode.xml | head -1
```

实跑输出:

```
total="202" passed="202" failed="0" inconclusive="0" skipped="0"
```

### ③ 全名级差集(两套件,自包含)

```bash
python3 - <<'PY'
import xml.etree.ElementTree as ET
def load(p):
    return {tc.get('fullname'): tc.get('result') for tc in ET.parse(p).getroot().iter('test-case')}
for name, base, cur in [
    ('EditMode', '/home/gu/.claude/jobs/181888b1/tmp/s194-full.xml',
                 '/home/gu/.claude/jobs/181888b1/tmp/qa-full-editmode.xml'),
    ('PlayMode', '/home/gu/.claude/jobs/181888b1/tmp/tdp03-full-playmode.xml',
                 '/home/gu/.claude/jobs/181888b1/tmp/qa-full-playmode.xml'),
]:
    b, c = load(base), load(cur)
    added, removed = sorted(set(c)-set(b)), sorted(set(b)-set(c))
    flipped = sorted(n for n in set(b)&set(c) if b[n]!=c[n])
    reds = sorted(n for n,r in c.items() if r not in ('Passed','Skipped'))
    print(f'{name}: 基线 {len(b)} · 本次 {len(c)} | 新增 {len(added)} · 消失 {len(removed)} · 翻转 {len(flipped)} · 非绿 {len(reds)}')
PY
```

实跑输出:

```
EditMode: 基线 4867 · 本次 4867 | 新增 0 · 消失 0 · 翻转 0 · 非绿 0
PlayMode: 基线 202 · 本次 202 | 新增 0 · 消失 0 · 翻转 0 · 非绿 0
```

### ④ 四件测试类条数(证明载体存在且条数与自述一致)

```bash
python3 - <<'PY'
import xml.etree.ElementTree as ET, collections
r = ET.parse('/home/gu/.claude/jobs/181888b1/tmp/qa-full-editmode.xml').getroot()
c = collections.Counter()
for t in r.iter('test-case'):
    n = t.get('fullname','')
    for cls in ('SaveSlotContractTest','SaveComposerWiringTest','PlayerMotorTest',
                'LogiPoseTest','DeferredThreatSlotTest','EventBudgetDeferTest'):
        if f'.{cls}.' in n: c[cls] += 1
for k, v in sorted(c.items()): print(f'  {k}: {v}')
PY
```

实跑输出:

```
  DeferredThreatSlotTest: 21
  EventBudgetDeferTest: 7
  LogiPoseTest: 15
  PlayerMotorTest: 9
  SaveComposerWiringTest: 15
  SaveSlotContractTest: 36
```

---

## ⚠️ 本轮踩坑如实登记(不影响判定,但须留痕)

**首跑命令漏了 `-runTests`**,导致 Unity 在 batch 模式打开工程后**主线程单核空转不收敛**:
编译于 `08:06` 正常完成(`Library/ScriptAssemblies/` 有 dll 写入),此后 **16 分钟零进展** ——
日志停在 `[Indexing] Starting Initial Indexing for Assets` + `TrimDiskCacheJob` 后零增长,
主线程 `utime+stime` 达 967 s(墙钟 960 s,单核 100%),`Library/` mtime 冻结,XML 永不产出。

**处置**:`kill -TERM` → `rm -f unity/Temp/UnityLockfile` → 带 `-runTests` 重跑。
重跑后 30 秒内即出现 `Running tests for ExecutionSettings`,EditMode 全程约 **97 秒**完成。

**判据(已写入记忆 `project-unity-test-command`)**:卡死的判据**不是「等了多久」**,
是「**编译完成后 `Library/` 还有没有写入**」—— 全量正常只需 ~95 s,超过 5 分钟且
`ls -lat unity/Library/` 的 mtime 停在编译完成时刻 ⇒ 卡死,不是慢。

⚠️ 另一处误判:用 `pgrep -af "6000.3.24f1/Editor/Unity"` 判断存活会命中**自己那条 bash `-c`
包装串**,须改用 `pgrep -f "<testResults 的 xml 路径>"` 或看 `/proc/<pid>/stat` 的 CPU 增量。

---

## 与 sprint-19 收口的衔接

| # | 动作 | 状态 |
|---|---|---|
| ① | `/qa-plan sprint` | ✅ QA plan 已存在(规划期 2026-10-24 落盘);本轮追加 §实收对账 + 判据⑦形状订正 |
| ② | `/smoke-check sprint` | ✅ **本件** |
| ③ | `/team-qa sprint`(签核) | ⏳ 待跑(DoD 剩余项) |
| ④ | `/code-review` 双列结账表 | ⏳ 待跑(DoD 剩余项) |

**未提交** —— 用户未下 commit 指令。
