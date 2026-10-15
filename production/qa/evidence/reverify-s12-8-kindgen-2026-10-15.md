# 复验报告 —— S12-8:case-system 001 B-2 / B-4 kindgen 判据复验

- **对象**:S11-12 的两条修复判据(`fix-s11-12-case-system-story-001-b2-b4-2026-10-15.md`)+ 其后 `605961b` 的探针接线
- **日期**:2026-10-15 · **Sprint**:12 · **执行者**:Claude
- **性质**:本件 = **复验(评当下)**,**不追认** S11-12 的判定(承 `review-workflow.md` §义务 3)
- **复验任务**(逐字承 sprint-12 的 S12-8 验收条件):「具名复核 S11-12 两件的判据**是否真判**
  (kindgen A2 严格形 + 唯一 CI job 载体);**若发现假绿即立件,不追认**」

---

## 一、复验结论

| # | S11-12 的判据 | 复验方法 | 结论 |
|---|---|---|---|
| ① | **B-2** A2 严格形「非空跑」(39 struct / 158 字段受判) | 独立复跑 + 对**真实产品文件**施变异 | ✅ **真判**(端到端红) |
| ② | **B-4** 载体唯一化(CI job 是真跑,测试面停复刻) | 读 job 定义 + 复跑两 job 的本地等价命令 | ✅ **真判**(接线与判据成立) |
| ③ | `605961b` 变异探针(未闭项 1 的兑现) | 复跑 + **对探针自身证伪**(中和断言 ⇒ 须报逃逸) | ✅ **真判**(逃逸路径可红) |
| ④ | 测试面停复刻 | grep 复刻符号 + `[Test]` 计数 | ✅ **已清零**(9 条) |

**未发现假绿。** 但复验出 **3 条须如实登记的边界**(§四),其中 1 条是**口径问题**(非缺陷),2 条是 S11-12 已自陈的未闭项现状核实。

---

## 二、① B-2 复验 —— A2 严格形是否真判

### 2.1 复跑(基线)

```bash
$ python3 tools/kindgen/kindgen.py --check
kindgen: A1–A5 全过(--check 不写文件)· A2 严格形:39 个载荷 struct / 158 个字段类型受判(非空跑)
EXIT=0
```

与 S11-12 §2.3 逐字一致 ⇒ 「非空跑」的自述**复核为真**(对照其修复前 `case 流命中 = 0`)。

### 2.2 ⚠️ 关键:探针**不等于** A2 —— 故另做端到端变异

`mutation_probe.py` 的变异施加在**内存副本**上(monkeypatch `parse_payload_structs`)。
它证的是「**断言函数**对给定 struct 表会报告」,**不证**「从真实 `.cs` 文件解析出的 struct 表会被抓」。
⇒ 复验**另做**一次真实文件变异(这是 S11-12 §四 的 7 变异**未覆盖**的一面):

```bash
# 对 unity/Assets/Sim.Contracts/Payloads/CasePayloads.cs 的
#   CaseOpenedPayload.PatientId : int → float
$ python3 tools/kindgen/kindgen.py --check
FAIL  A2 CaseOpenedPayload: 字段 CaseOpenedPayload.PatientId 类型 'float' ∉ 整数域白名单 —— 非整数域
      —— ADR-006 定点域边界(sim 禁 float)

kindgen: 1 条断言失败 —— 构建失败(ADR-024 §⑤)
KINDGEN_EXIT=1
```

⇒ **端到端真判**:真实产品文件的字段类型变异 ⇒ `--check` 红。**A2 不是只对内存副本有效。**
变异后**立即还原**,`diff` 校验字节相同,`REVERT_PROBE` 全仓零残留。

---

## 三、② B-4 复验 —— 载体唯一化

### 3.1 CI job 定义(读)

`.github/workflows/tests.yml`:
- `:38` **`kindgen-assertions`**(独立 job,`:36` 明注「后者(guard-assertions)是占位 TODO,本 job 是真跑的绿」)
  - `:52` `python3 tools/kindgen/kindgen.py --check` —— 非零即红
  - `:54-62` 重生成 + `git diff --quiet -- StreamRouting.g.cs` —— 有 diff 即红
- `:72` **`kindgen-mutation-probe`**(`605961b` 新增)—— `:86` `python3 tools/kindgen/mutation_probe.py`

**独立成 job 的判断经复核成立** —— `guard-assertions` 确为占位(`:27` `TODO(实现轮)` + `:29` `exit 0`),
混读会重演「占位冒充证据」的失败模式。

### 3.2 两条本地等价复跑(CI 命令的本地复现)

| 步骤 | 本地命令 | 实测 |
|---|---|---|
| 断言批 | `python3 tools/kindgen/kindgen.py --check` | `EXIT=0` |
| 产物一致性 | `python3 tools/kindgen/kindgen.py` + `git diff --quiet -- …StreamRouting.g.cs` | 无 diff ⇒ 一致 ✅ |

### 3.3 「把 kindgen 删掉,测试照样全绿」是否仍成立(判定原文的直证)

判定原文 = 「**把 kindgen 删掉,13 条测试照样全绿**」。复验:

- 测试面 kindgen 断言**已清零**(§五);现存 9 条测试**无一调用 kindgen** ——
  `grep -n "kindgen"` 的命中**全部**落在**注释**与**方法名**(`test_kindgenA1_*`)上,
  **无任何代码级调用**(实测 `grep -nE "^\s*[^/].*kindgen"` 仅命中方法名声明行与注释行)。
- ⇒ **删掉 kindgen 对测试面无影响** —— 但**这正是修复要的效果**:断言执行权归 CI job,
  **测试删光也不影响 CI 判红**。载体不再由测试冒充。
- ⚠️ **边界**:「删掉 kindgen 测试照样全绿」这一**事实**仍为真,但**不再构成缺陷** ——
  因为「kindgen 的断言是否执行」与「测试是否全绿」已**解耦**。这是修复的**目标态**,非漏洞。

---

## 四、③ 变异探针复验 + 对探针自身的证伪

### 4.1 复跑

```bash
$ python3 tools/kindgen/mutation_probe.py
捕获 7 · 逃逸 0 · 跳过 0
mutation_probe: 全部变异被捕获 —— A2/A2b 判别力成立
EXIT=0
```

### 4.2 ⚠️ 探针是否**恒绿**(空跑风险)?

`main()` 的判据是 `if rep:` —— 若未变异时 `rep` 非空,则「捕获」恒真 ⇒ 探针空跑。**独立实测**:

```
未变异 A2 报告条数 = 0  (期望 0 —— 否则探针恒绿)
```

⇒ 基线零报告 ⇒ 「捕获」判据**非恒真**,探针**不空跑**。

### 4.3 ⚠️ 逃逸路径是否**可达**(死代码风险)?

若 `escaped` 分支永不触发,则「判别力成立」是纸面的。**证伪**:把 `a2_payload_types` 改为提前返回
(即中和 A2 断言),重跑探针:

```
捕获 1 · 逃逸 6 · 跳过 0
mutation_probe: 有变异逃逸 —— A2/A2b 判别力不成立(须修)
PROBE_EXIT=1
```

⇒ **逃逸路径真能红**(`EXIT=1`),探针的判决力**可证伪**。变异后**立即还原**,
`diff` 校验字节相同,`REVERT_PROBE` 零残留。

---

## 五、④ 测试面停复刻复核

```bash
$ grep -nE "GetCaseKindNamesFromRegistry|GetCasePayloadSchemas|GetCaseAuthors|kindgenA[2-5]" \
    unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs
(无输出)
$ grep -c "\[Test\]" …case_identity_routing_test.cs
9
```

⇒ 复刻与手搓设施**已清零**;测试数 **9**(与 S11-12 §五 的「13 → 9」一致)。
S11-12 未闭项 3 的 `test_kindgenA1_*` **确在保留面内**,复核其理由:

> A1 打的是**生成物** `StreamRouting.Of(...)`(生产码),**不是**手搓扫描器 ——
> 与 B-4 点名的「复刻其扫描」**不同型**。

**本复验采纳该读法**(理由:缺陷定义逐字是「在测试内**复刻其扫描**」,A1 不落此面;
且删它**无覆盖损失** —— 五 Kind 路由在 5 个 writer 测试里均有等价断言)。
⇒ **S11-12 §七 的「不确定项 Q」就此结案:取「保留」,不立新件。**

---

## 六、如实登记的边界(复验出,非缺陷)

| # | 边界 | 性质 | 归属 |
|---|---|---|---|
| 1 | **探针 ≠ A2 端到端** —— 探针变异施加在**内存副本**;若不另做真实文件变异,「A2 对真实 `.cs` 有效」无证据 | **本件已补做**(§2.2),登记以明「探针覆盖面的边界」 | 已闭 |
| 2 | **CI 从未在真 GitHub runner 上执行** —— 「失败即红」是**设计判据**,非**已观测行为**(S11-12 §七 未闭项 2 现状核实:**仍开放**) | 未闭 | devops / 首次 push |
| 3 | **`guard-assertions` job 仍是占位**(`:27` TODO + `:29` `exit 0`)—— 不属 S12-8 面内,但同工作流内**唯一的假绿源** | 未闭(既有登记) | 守卫实现轮 |

**关于 #2**:本仓 `tests.yml:4` 自陈「Unity 测试 job 已移除(UNITY_LICENSE 不配置)」⇒
无 runner 配额 / secrets 未验证属实。本复验**不改变**该状态,只核实「接线与判据已落、未运行过」。

---

## 七、复验判决

**S11-12 的两条修复判据(A2 严格形 + 载体唯一化)经复验 = 真判,未发现假绿。**

- 三条独立证伪(真实文件变异 / 探针基线零报告 / 探针逃逸路径)均按预期转红,且**全部原子还原、零残留**。
- 一条口径问题(§五 的 A1)结案为「保留」,不立新件。
- 两条未闭项(CI 未真跑 / guard-assertions 占位)**如实登记**,不因本复验而消解,亦**不构成本件阻塞**
  (它们不是 S11-12 判据的缺陷,是环境与其它 job 的既有状态)。

**⇒ S12-8 的验收条件「若发现假绿即立件」的结论 = 未发现 ⇒ 不立件。**
