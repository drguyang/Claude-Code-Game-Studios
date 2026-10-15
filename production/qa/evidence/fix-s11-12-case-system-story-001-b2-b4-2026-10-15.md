# 修复证据原件 —— S11-12 / case-system story-001 B-2 + B-4(kindgen A2 空跑 · 载体借绿)

**日期**:2026-10-15
**对象**:
① `tools/kindgen/kindgen.py` —— A2 严格形(A2 断言本体)
② `.github/workflows/tests.yml` —— 新增 job `kindgen-assertions`(唯一载体接线)
③ `unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs`(**纯测试件,零产品代码**)—— 删掉测试内复刻
**来源判定**:`production/qa/evidence/review-case-system-story-001-retro-2026-10-15.md` §二 **B-2 / B-4**(均 BLOCKING)
**排期**:`production/sprints/sprint-11.md` —— **顺延 sprint-12**(S11-11 已先行闭合 B-3;本件 = S11-12)
**性质**:本件 = **修复证据**,**不是评审**(承 `.claude/docs/review-workflow.md` §义务 3 不可追补)。
**分工**:① ② 由另一 agent 落在同一修复批内(本件**引用其已实测输出**,不重跑其沙箱);③ 由本 agent 收口。

> ⚠️ 承 S11-8 / S11-10 / S11-11 的同一纪律:原始 XML 落在 `unity/Logs/`(**`.gitignore:196`**)⇒ **易失中间产物**,
> 不得充当交付证据。故判据、命令、输出原文收进本件,使其**自洽可复核**。

---

## 一、原缺陷判定(逐字承补评件 §二)

> **[B-2 · 空跑断言(vacuous)] AC-37-03 的 A2 子项零断言**
> - `:153-172` 内层 `foreach` 在当下 5 条 case `payload_schema` 上**执行 0 次**(实测 float/double 命中数 = 0);
> - 唯一活着的断言是 `:157` 的 `Count == 5`;且扫的是 registry **散文**,非载荷 struct 字段类型;
> - 旁注:kindgen 自己的 A2 是同款弱实现,但**已登记在案**(`kindgen.py:11-14`「严格形未实现」)⇒ 测试把一份**已承认的弱检查**当成 AC 达标证据。

> **[B-4 · 借绿(载体未跑却报绿)] AC-37-03 的执行半边 = NOT-RUN**
> - story §QA Test Cases 明写 Given「entities.yaml 五 Kind 行」→ When「**运行生成器**」;
> - 测试**从不运行 kindgen**(`grep kindgen` 零命中),改为在测试内复刻其扫描(`:361-442`);
> - `StreamRouting.g.cs:3,142` 的「产物存在即断言已过」是**生成物自述**,不是证据(同「不得凭 commit 自述判定」);
> - ⇒ **把 kindgen 删掉,13 条测试照样全绿**;
> - **修复**:「CI 执行 `kindgen --check`」应为 AC-37-03 的**唯一载体**,测试面不再复刻(B-2/B-4 是同一缺口的两半)。

**两缺陷的同一性**:B-2 = A2 断言**本体**空跑;B-4 = 整批 A1–A5 断言**载体**从未执行。
二者共同根因 = 「断言写在了**测试里**,而测试从不运行生成器」。故修法是一个整体:
**断言搬到 kindgen 本体(真跑)+ 接线到 CI(失败即红)+ 测试面停复刻(载体唯一)**。

---

## 二、B-2 的修复 —— A2 空跑的根因与实测证据

### 2.1 根因(两层)

| 层 | 旧实现 | 病症 |
|---|---|---|
| **对象错** | 扫 `entities.yaml` 的 `payload_schema` **散文** | ADR-024 §⑤ A2 原文是「**字段类型** ∈ 整数域白名单」⇒ 判据面本应是 **C# 载荷 struct 的字段声明** |
| **判据弱** | 对散文 grep `float\|double` + 「前 4 字否定语境豁免」 | 命中数**实测 0**(见 2.2)⇒ **内层循环体执行 0 次**,结构上**不可证伪** |

旧 `kindgen.py:11-14` 自陈「严格形未实现」—— 即 kindgen 侧**早已承认**这是弱检查;
而 story-001 测试把这份**已承认的弱实现**当作 AC-37-03 达标证据,并在测试里**又复刻了一遍**。

### 2.2 实测证据(A2 空跑 —— 本件已复跑复现)

命令(本机,只读,对**当下** registry):

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -I - <<'PY'
import re, yaml, pathlib
reg = yaml.safe_load(pathlib.Path("design/registry/entities.yaml").read_text(encoding="utf-8"))
ks = [e for e in reg["constants"] if str(e.get("name","")).startswith("SimEvent.Kind.")]
outer=inner=exempt=fire=0; prefixes=[]; case_hits=0
for e in ks:
    outer+=1
    ps=str(e.get("payload_schema",""))
    for m in re.finditer(r"[Ff]loat|[Dd]ouble", ps):
        inner+=1
        ctx = ps[max(0,m.start()-4):m.start()]; prefixes.append(repr(ctx))
        (exempt, fire) = (exempt+1, fire) if re.search(r"[无禁非零不]", ctx) else (exempt, fire+1)
    if e.get("stream")=="case":
        case_hits += len(re.findall(r"[Ff]loat|[Dd]ouble", ps))
print(f"外壳迭代: {outer} | 内层命中: {inner} | 豁免: {exempt} | 真触发: {fire}")
print("命中前置 4 字:", "/".join(prefixes))
print(f"case 流命中: {case_hits}")
PY
```

输出(逐字):

```
外壳迭代: 35 | 内层命中: 4 | 豁免: 4 | 真触发: 0
命中前置 4 字: '数,禁 '/'标 无 '/'2。无 '/');禁 '
case 流命中: 0
```

**判读**:
- 全 35 支 Kind 的散文里 `float|double` 字样**仅 4 次命中**,且 4 次的前置 4 字依次为
  `'数,禁 '` / `'标 无 '` / `'2。无 '` / `');禁 '` —— **全部落「无 / 禁」豁免** ⇒ **真触发 = 0**;
- **case 流 5 支命中 = 0** ⇒ 测试里那段 `foreach` 的**内层循环体执行 0 次** ——
  **该断言在任何输入下都不可能红**,即 `review` 所称的「空跑 / 结构上不可证伪」;
- 唯一活着的断言是外层 `schemas.Count == 5`(即测试:157 的 `Assert.That(schemas.Count, Is.EqualTo(5))`)。

### 2.3 修复后的 A2(严格形 · 对象与判据双换)

`tools/kindgen/kindgen.py` 的 `a2_payload_types()`:

- **对象** = `unity/Assets/Sim.Contracts/` 全树 `public readonly struct <Kind>Payload` 的 **C# 字段声明**
  (非 registry 散文;实测 35/35 命名命中);
- **判据** = **白名单**(`INT_PRIMITIVES` = 8 种整数原始类型 + `bool`),白名单外一律 FAIL;
  `float` / `double` / `decimal` / `string` / `char` / `object` 无需单列即被拒(白名单语义),
  `HAZARD_TYPES` 只为把「为何被拒」写进报错文本;
- **递归展开**嵌套 struct(`WorldPos` / `CaseId` / `DiseaseIdSet` / `Fix` …),每个字段类型判一次;
- **非空跑守卫(关键)**:扫得 `fields == 0` ⇒ FAIL;某 `<Kind>Payload` 缺失或零字段 ⇒ FAIL(逐支)。

**实测输出(本件复跑,当下树)**:

```bash
$ python3 tools/kindgen/kindgen.py --check
kindgen: A1–A5 全过(--check 不写文件)· A2 严格形:39 个载荷 struct / 158 个字段类型受判(非空跑)
EXIT=0
```

> 「**39 个载荷 struct / 158 个字段类型受判(非空跑)**」= 修复前 `case 流命中 = 0` 的**直证反面对照**:
> 同一份 registry,旧判据命中 0 次,新判据**受判 158 个字段** ⇒ 断言面不再为空。

> 📌 **A2b 保留**:`a2b_registry_prose()`(对象 = registry 散文)作为**辅助半边**保留,
> 理由见其 docstring —— ① 数组 `[]` 的**界证据**只有散文侧看得见(C# 侧只看得见 `T[]`);
> ② 顺手拦 registry 新增散文漂移。它**单独不足以**构成 A2 达标。

---

## 三、B-4 的修复 —— CI job 落点与「失败即红」判据

**落点**:`.github/workflows/tests.yml` → job **`kindgen-assertions`**(`:38-62`)。
**独立成 job**(不并入 `guard-assertions`):后者是**占位 TODO**(`exit 0` 注释自陈「守卫未实现」),
本 job 是**真跑的绿** —— 二者混读会重演「占位冒充证据」的失败模式。

**两条判据(任一失败 = job 失败 = CI 红)**:

| 步骤 | 命令 | 判据 |
|---|---|---|
| 断言批 | `python3 tools/kindgen/kindgen.py --check`(`:52`) | **退出码非零即红** —— A1–A5 任一 FAIL ⇒ `sys.exit(1)` |
| 产物一致性 | `python3 tools/kindgen/kindgen.py` + `git diff --quiet -- unity/Assets/Sim/StreamRouting.g.cs`(`:54-62`) | 重生成后产物**有 diff 即红** —— 拦「registry 改了、产物没重跑」的脱钩 |

**何以闭合 B-4**:判定原文是「**把 kindgen 删掉,13 条测试照样全绿**」。
修复后,「kindgen 的断言是否执行」与「测试是否全绿」**解耦**:
断言执行权归 CI job,测试删光也不影响 CI 判红 —— 载体**不再由测试冒充**。

**环境**:`ubuntu-latest` + `actions/setup-python@v5`(`python-version: 3.12`)+
`PyYAML==6.0.3`(钉死版本,与本地验证版一致);**零 Unity 依赖**,与本工作流既有取向一致。

---

## 四、变异矩阵(可证伪验证 —— A2 严格形的判别力)

对 `Sim.Contracts/` 载荷 struct 施加 7 种「应被 A2 抓」的变异,逐一跑 `kindgen.py --check`:

| # | 变异 | 期望 | 实测 |
|---|---|---|---|
| 1 | `int` 字段 → `float` | 拒 | **EXIT=1** |
| 2 | `bool` 字段 → `string` | 拒 | **EXIT=1** |
| 3 | 未登记 struct 内含 `double`(间接字段) | 拒 | **EXIT=1** |
| 4 | 字段类型 = 未登记类型(声明了但无人定义 / 非白名单) | 拒 | **EXIT=1** |
| 5 | 删改 struct 名(`<Kind>Payload` 缺失) | 拒 | **EXIT=1** |
| 6 | 抽空字段(零字段 ⇒ 空跑) | 拒 | **EXIT=1** |
| 7 | registry 散文注入非否定 `float`(A2b 面) | 拒 | **EXIT=1** |

**7/7 全部 EXIT=1** ⇒ 判据**可证伪**(变异被抓,非恒绿)。

> ⚠️ **如实标注(禁借绿)**:
> - 上述 7 条是 **另一 agent 在 `/tmp` 沙箱的一次性验证**,**未固化为 CI 步骤** ——
>   即「变异矩阵本身没有常驻执行体」,其证据力 = **一次性实测**,非持续判据;
> - **CI 从未在真 GitHub runner 上跑过**(本仓 Unity 测试 job 已移除、`tests.yml` 改由纯 bash/python 守卫;
>   是否有 runner 配额 / secrets 未验证)—— **本件的 CI 结论是「接线与判据」,不是「运行过」**;
> - 变异矩阵的**复算入口**记于此,任何人可随时重跑(§六 命令 4 给出沙箱外的最小重跑说明)。

---

## 五、本次收口 —— 删掉测试内复刻,载体唯一化

**判定口径(承 retro B-4 的「修复」段与 story-001 §修复排期)**:
> 「CI 执行 `kindgen --check`」为 **AC-37-03 的唯一载体**,**测试面不再复刻**。

**收口前**:`case_identity_routing_test.cs` **仍在测试里重做了一遍 kindgen 的扫描** ——
`process` 表如下(修复前实测):

| 测试内重做的断言 | 所依赖的手搓设施 | kindgen 侧对应 |
|---|---|---|
| `test_kindgenA2_…integerDomainOnly` | `GetCasePayloadSchemas()` | A2(严格形,**更强**) |
| `test_kindgenA3_…unique` | `GetCaseKindNamesFromRegistry()` | A3 |
| `test_kindgenA4_…all37` | `GetCaseAuthors()` | A4 |
| `test_kindgenA5_…presentInEventKindEnum` | `GetCaseKindNamesFromRegistry()` | A5 |

四条测试共用同一段**手搓 `entities.yaml` 行解析器**(`GetCaseKindNamesFromRegistry` /
`GetCasePayloadSchemas` / `GetCaseAuthors` + `FindRepoPath`)= retro B-4 点名的「复刻其扫描(`:361-442`)」。

**本次删除**(纯删,零产品代码):

| 类别 | 内容 |
|---|---|
| 测试 | `test_kindgenA2_casePayloadSchemas_integerDomainOnly`(B-2 的**空跑**本体) |
| 测试 | `test_kindgenA3_caseKindNames_unique`(弱代理) |
| 测试 | `test_kindgenA4_caseKindAuthors_all37`(弱代理) |
| 测试 | `test_kindgenA5_caseKinds_presentInEventKindEnum`(弱代理:A5 反向用**名字前缀启发式**) |
| 辅助 | `GetCaseKindNamesFromRegistry()` / `GetCasePayloadSchemas()` / `GetCaseAuthors()` / `FindRepoPath()`(均**只被上述四条使用** ⇒ 一并清理,**无孤儿**) |
| 头注 | 新增「载体纪律」段(说明载体 = CI、测试面停复刻、仅留 A1) |
| 段标 | `[story 判据] kindgen 断言批 A1–A5` → `[story 判据] kindgen A1 —— 生成物路由(A2–A5 载体 = CI)` |

**保留(并说明理由)**:

- **`test_kindgenA1_fiveCaseKinds_routeToCaseStream` 保留** —— 它**不是**「复刻 kindgen 的扫描」:
  其被测对象是**生成物** `StreamRouting.Of(...)`(生产码),不是手搓解析器。
  补评件 §三「正面核实」**逐条点名它是「直打生产码的真断言」** ⇒ 与 B-2/B-4 所指的复刻**不同型**,不在删除面内。
  (⚠️ 其断言的内容确实由 CI 的 `--check` + 重生成 `git diff` **传递覆盖** ——
  此点如实登记为**未闭项**,见 §七。)
- `AC-37-01` 两条 · `AC-37-02` 一条 · `载荷形状断言` 两条 · `跨流全序` 两条 · `高水位` 一条
  —— 均无 kindgen 关系,原样保留。

**diff 量级(`git diff --stat`)**:`1 file changed, 11 insertions(+), 191 deletions(-)`。
**测试数**:**13 → 9**(实测 `[Test]` 特性计数;**未跑 Unity**,见 §六 NOT-RUN 声明)。

**零外部引用**(实测):被删方法名 / 辅助名在**全仓 `.cs` 零引用**
(仅本测试件私有;`case_replay_persistence_test.cs:420` 的 `FindRepoPath` 是**同名但独立**的私有方法,
非跨文件引用,不受影响;Unity Test Framework 按 `[Test]` 特性发现,**不依赖方法名**)。

---

## 六、验证命令(可证伪 · 逐字可复跑)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
F=unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs

# ── ① B-2 的直证:A2 严格形真跑且非空跑(令 CI job 的判据在此可见)──────
python3 tools/kindgen/kindgen.py --check; echo "EXIT=$?"
#    期望: kindgen: A1–A5 全过(--check 不写文件)· A2 严格形:39 个载荷 struct / 158 个字段类型受判(非空跑)
#          EXIT=0

# ── ② B-2 的反面(旧判据空跑的复现)—— 期望「真触发: 0 / case 流命中: 0」──
#    见 §2.2 脚本原文。

# ── ③ B-4 的载体:CI 失败即红(接线判据)────────────────────────────
grep -n "kindgen-assertions" -A 24 .github/workflows/tests.yml
#    期望: job 存在,含 --check 步骤 + 重生成后 `git diff --quiet` 步骤(非零即红)

# ── ④ 本次收口:测试面不再复刻(载体唯一化)────────────────────────
grep -nE "GetCaseKindNamesFromRegistry|GetCasePayloadSchemas|GetCaseAuthors|FindRepoPath|kindgenA[2-5]" $F
#    期望: 无输出(复刻与手搓设施已清零)
grep -c "\[Test\]" $F      # 期望: 9(修复前 13)

# ── ⑤ 权威复跑(本件**未跑** —— 硬约束:不起 Unity batch)────────────
# /home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics -projectPath unity \
#   -runTests -testPlatform EditMode \
#   -testFilter DaYiJingCheng.Tests.CaseSystem.CaseIdentityRoutingTest \
#   -testResults "$PWD/unity/Logs/s11_12_identity.txt" -logFile unity/Logs/s11_12_identity.log
#    期望: total=9 passed=9 failed=0 且 error CS = 0
```

判决口径 = **退出码 0 且 XML `failed="0"`** 双信号一致。

### 6.1 ⚠️ NOT-RUN 声明(禁借绿)

- **⑤(Unity 夹具)本件未执行** —— 硬约束:用户可能开着编辑器,batch 需独占工程;
  跑测由发起方统一执行。故 §六 的 **9/9 绿不可由本件声称**;
- 收口的**静态自检已做**(可复核):`[Test]` = 9 · 花括号配平(22/22)· 无悬空引用(③ 无输出)·
  `using` 未动(见 §七 观察 ①)· 零产品代码改动。

---

## 七、未闭项与观察(如实)

**未闭(带归属)**:

1. **【变异矩阵无常驻执行体】** §四 的 7 变异是 `/tmp` 沙箱**一次性**实测,**未固化为 CI 步骤** ⇒
   「A2 的判别力」目前是**一次性证据**,不是持续判据。若要与 `--check` 同等地位,须另立步骤
   (如 CI 内对副本施变异跑 `--check` 期望非零)。**归属:kindgen 侧 / 后续修复批。**
2. **【CI 未在真 runner 上跑过】** `kindgen-assertions` job 的**接线与判据**已落,但**从未在
   GitHub Actions 上实际执行** ⇒ 「失败即红」是**设计判据**,非**已观测行为**。
   **归属:devops / 首次 push 时验证。**
3. **【`test_kindgenA1` 保留 —— 载体唯一性的边界】** 本件保留 A1 一条(理由见 §五)。
   其断言的内容**由 CI 传递覆盖**(`--check` 保 registry 合法 + 重生成 `git diff` 保产物同步)。
   ⇒ **若「唯一载体」被读作「测试文件内零 kindgen 断言」**,则 A1 亦应删(其覆盖不损失:
   五 Case Kind 的路由在 `case_open_and_judgment_test` / `case_close_precondition_test` /
   `judgment_writer_test` / `pattern_recognition_writer_test` / `case_close_writer_test` 均有等价断言)。
   **本件取「保留」并由发起方裁定** —— 见下方「不确定项」。
4. **【B-1 不在本件面内】** story-001 的另一条 BLOCKING(B-1:AC-37-06 判据**算术上不可证伪**,
   须**退设计重述**)未触及 —— 归 sprint-12 另一行。
5. **【story-001 仍 `In Review`】** 本件清 B-2/B-4 两条;**门是否通过须待 §六 ⑤ 的权威复跑 +
   story 回刷**(story 文件回刷由发起方统一做,本件不改 story)。

**观察(非本件缺陷,登记备用)**:

- **① `using System;` / `using System.Collections.Generic;` 现无直接消费者** ——
  删辅助段后,`List<…>` / `HashSet<…>` / `Math.` / `Exception` 等均不再出现(实测 §六 命令外的
  `grep -nE "List<|HashSet<|Math\."` 无输出)。**本件未删这两个 `using`** ——
  理由:本机**无编译器可验证**(承 `kindgen.py` docstring「本机无 .NET SDK」),而未用 `using`
  仅产生 hidden 诊断(CS8019,默认不报),不构成构建失败;删错则引 CS0246 硬错。
  ⇒ **保守保留**,由有编译环境的复跑轮按需清理。
- **② 同型号复刻在他处存在(不在本件面内,未动)** ——
  `unity/Assets/Tests/EditMode/Combat/combat_actions_schema_test.cs:121 test_kindgen_registeredKinds`
  亦以「测试内反射校验 EventKind 成员」的方式复查 registry 登记面,与 B-4 属**同型失败模式**,
  但对象是 Combat 的 3 支 Kind、归属系统 25,**不在 story-001 面内** ⇒ 本件**未动**,
  作为系统性观察登记(是否统一收口归后续断言形态批)。
- **③ `StreamRouting.g.cs` 未被本件触碰**(`git status` 干净)⇒ 生成物与 registry 的一致性不受本件影响。

**不确定项(须发起方裁定)**:

- **Q**:`test_kindgenA1` 的保留是否符合「载体唯一化」的口径?
  本件的读法 =「复刻」= **手搓扫描器**(已在 B-4 点名,`:361-442`),A1 打的是**生成物**故不在其内;
  另一种读法 = 「测试文件内**任何** kindgen 断言都是第二载体」⇒ 应连 A1 一并删。
  **两种读法的差异 = 1 条测试;删除无覆盖损失(理由见未闭项 3)。**
