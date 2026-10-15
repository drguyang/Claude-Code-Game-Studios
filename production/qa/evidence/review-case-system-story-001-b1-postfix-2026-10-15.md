# B-1 测试半边复评原件 —— case-system story-001(评当下代码)

**评审时点**:2026-10-15
**被评对象**:`unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs`
  (方法 `test_highWaterMark_sentinelMinusOne_noPollution`,`:266-300`)
**生产被评对象**:`unity/Assets/Sim/ItemDatabase/IdAuthority.cs`(`ReconstructPatientHighWater`,`:90-102`)
**原判定来源**:`production/qa/evidence/review-case-system-story-001-postfix-2026-10-15.md` §五(门 = 不通过 · 残余唯一 = B-1 测试半边)
**评审方式**:单代理 · 只读 + 静态分析(逐行手算 + 变异矩阵在 `/tmp` 副本上以 Python 转录复算)
**硬约束遵守**:⚠️ **未起 Unity batch**(未跑 `-batchmode`)· **未改任何仓内文件**(变异在 `/tmp/b1_verify/sim.py` 上做)· Unity 侧结论**全部借用**本机日志(见 §二.3 归属声明)

---

## 一、原判定(逐字)

> **门 = 不通过**,理由:story-001 测试件内仍留着 GDD 自己宣告为「非证据」的断言形态。
> `test_highWaterMark_sentinelMinusOne_noPollution`(`:261-285`)**逐字仍是**:
> ```csharp
> var maxPatientId = events.Where(e => !e.Patient.IsNone).Select(e => e.Patient.Value).Max();
> Assert.That(maxPatientId, Is.EqualTo(5), "max(patient_id) 须忽略哨兵 -1(ADR-007 §四)");
> ```
> 生产 `IdAuthority.ReconstructPatientHighWater` 在 story-001 面内**零调用** ⇒ **任何实现下恒真**。

**同件开出的通过条件(逐字)**:

> 补上「测试改打 `ReconstructPatientHighWater` + 断『只升不降』」后,本门即可转**通过**
> (同型做法在 `id_authority.cs:163 test_ac21a63_patientReconstruct_ignoresNoneSentinel` 已有现成范例)。

---

## 二、现状态(我独立核到的,非采信自述)

### 1. 判据体已被改写 —— 真打生产载体

```bash
F=unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs
grep -n "ReconstructPatientHighWater|new IdAuthority" $F
grep -n "class IdAuthority" $F          # 期望无输出(测试内未自造同名类)
grep -n "\.Max()" $F                    # 期望仅剩 docstring 里描述旧形态的那一处
grep -c "\[Test\]" $F
```

实测输出:

```
261:        /// <para>打的是<b>生产</b> <see cref="IdAuthority.ReconstructPatientHighWater"/>(非测试内自造 max)。</para>
279:            var authority = new IdAuthority();
280:            authority.ReconstructPatientHighWater(events.Select(e => e.Patient.Value));
288:            var monotone = new IdAuthority();
291:            monotone.ReconstructPatientHighWater(new[] { -1, -1 }); // 全哨兵流
298:                () => new IdAuthority().ReconstructPatientHighWater(new[] { int.MaxValue }),
（class IdAuthority：无输出 —— 测试内未自造同名类）
263:        /// <c>events.Where(...).Select(...).Max()</c> 是<b>测试自造</b>的算式，生产函数<b>零调用</b>;   ← 仅 docstring 描述旧形态
9
```

**三处调用点**(`:280` / `:291` / `:298`)全部指向生产类型。**全库唯一 `IdAuthority` 类定义**
= `unity/Assets/Sim/ItemDatabase/IdAuthority.cs:23`(`grep -rn "class IdAuthority" --include=*.cs unity/ tests/`
仅命中此一处 + 测试 fixture 名 `IdAuthorityTest`),测试文件 `using DaYiJingCheng.Sim;`(`:43`)
⇒ **不存在本地 shim 顶替**。旧判据体(`Where(!IsNone).Select(Value).Max()`)已从代码面清除
(`git diff` 逐字可见该段被删)。

⇒ **通过条件前半「测试改打 `ReconstructPatientHighWater`」成立。**

### 2. 三处断言(当下逐字)

| # | 行 | 断言 |
|---|---|---|
| ① | `:279-282` | `new IdAuthority()` → `ReconstructPatientHighWater([1,5,-1,3])` → `NextPatientId() == 6` |
| ② | `:288-293` | 先 `NextPatientId()` ×2(计数器 = 2)→ `ReconstructPatientHighWater([-1,-1])` → `NextPatientId() == 2` |
| ③ | `:297-299` | `Assert.Throws<OverflowException>(() => new IdAuthority().ReconstructPatientHighWater([int.MaxValue]))` |

**通过条件后半「断『只升不降』」成立** —— ② 断言「预先铸造到 2 之后,全哨兵流不得把高水位拉回」
(即只升不降的「不降」半边);① 断言「升」半边(0 → 6)。

### 3. 借用运行基线(⚠️ 归属声明)

```bash
ls -la --time-style=full-iso unity/Logs/s11-b5b-editmode.xml
stat -c '%y' unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs
grep -o 'total="..."' unity/Logs/s11-b5b-editmode.xml
```

实测:

```
unity/Logs/s11-b5b-editmode.xml     2026-10-15 13:09:36 +0800
case_identity_routing_test.cs       2026-10-15 13:04:41 +0800
total="3386" passed="3340" failed="0" inconclusive="0" skipped="46"   （EditMode，退出码 0）
total="3131" passed="3098" failed="0" inconclusive="0" skipped="33"   （另一套件组，同文件）
test-case … classname="…CaseIdentityRoutingTest" … result="Passed"   （9 条全 Passed，start-time 2026-10-15 05:08:48Z = 13:08:48 +0800）
```

- ⚠️ 该 XML 是**借用**本机 `unity/Logs/`(易失中间产物,`.gitignore`)的**只读**日志,**非本件产出**
  (本件受硬约束未跑 Unity)。它证明「9 测当下全绿」,**不证明任何 AC 被验证**。
- **时序关系**:测试件 mtime `13:04:41` < 日志 mtime `13:09:36`,且日志内该 fixture 的
  `start-time` = `13:08:48` ⇒ 该次运行**晚于**本次改写 4 分钟,足以覆盖新代码。
  ⚠️ 但 XML **不记录源码哈希**,我**无法证明**该次运行确实重新编译了本文件(而非复用旧程序集)
  —— 见 §五 不确定项。按硬约束我**不能**跑 batch 去坐实。
- ⚠️ 所有套件 `asserts="0"`(NUnit 约束式断言不计数的 UTF 已知现象)⇒ **只能据 `result="Passed"` 判绿**,
  不能据 assert 计数判「断言真的执行了」。
- `pgrep -af Unity` 实测**无 `Unity -batchmode` 进程**(仅 UnityHub 授权客户端 / VBCSCompiler),未与任何 batch 冲突。

---

## 三、可捕获性逐条(可证伪 —— 逐行手算 + `/tmp` 转录复算)

**方法**:把 `ReconstructPatientHighWater`(`IdAuthority.cs:90-102`)逐行转录进
`/tmp/b1_verify/sim.py`(C# `int32` 补码回绕语义),对每个候选变异重算 ①②③ 的绿/红。
⚠️ **该脚本是静态转录,不是对 Unity 的实跑** —— 它复算的是**算术**,不证明编译产物。

**生产实现逐行编号(供引用)**:

```
:93   int candidate = _nextPatient;
:94   foreach (int id in knownPatientIds)
:96       if (id < 0) continue;                      // 哨兵守卫
:97-98   if (id == int.MaxValue) throw new OverflowException(…);
:99       if (id + 1 > candidate) candidate = id + 1;   // 累加(载重行)
:101  if (candidate > _nextPatient) _nextPatient = candidate;   // 只升不降守卫
```

**变异矩阵实测**(`python3 -I /tmp/b1_verify/sim.py`;「①-重排」= 把 ① 的输入改为 `[1,-1,5,3]`,
见 §五 建议):

| 变异 | ① | ①-重排 | ② | ③ |
|---|---|---|---|---|
| 基线(生产原样) | 绿 | 绿 | 绿 | 绿 |
| **M1 删 `:99`(累加行)** | **红** | 红 | 绿 | 绿 |
| **M2 删 `:96`(哨兵守卫)** | 绿 | 绿 | 绿 | 绿 |
| **M3 删 `:101` 整行** | **红** | 红 | 绿 | 绿 |
| **M3' `:101` 去守卫(无条件赋值)** | 绿 | 绿 | 绿 | 绿 |
| **M4 = M2 + M3'(改写者自陈的复合变异)** | 绿 | 绿 | **绿** ⚠️ | 绿 |
| **M5 删 `:97-98`(溢出守卫)** | 绿 | 绿 | 绿 | **红** |
| **M6 `:96` 的 `continue` → `break`** | 绿 | **红** | 绿 | 绿 |
| **M7 `:93` 改 `int candidate = 0;`** | 绿 | 绿 | 绿 | 绿 |
| **M8 重写为 `max(ids)+1`(整函数)** | 绿 | 绿 | **红** | 红 |
| **M9 = M7 + M3'(2 行改)** | 绿 | 绿 | **红** | 绿 |

### ① 「真 id 累加 ⇒ next = 6」—— **可捕获,成立** ✅

- **删 `:99`** ⇒ `candidate` 恒为 `_nextPatient = 0` ⇒ `NextPatientId()` 返 0 ≠ 6 ⇒ **红**(M1)。
- **删 `:101` 整行** ⇒ 赋值永不发生 ⇒ 同上 **红**(M3)。
- ⇒ 断言打的是**载重行** `:99`,与改写者自陈(`:278` 注释)一致。

### ② 「全哨兵流不回退 ⇒ next = 2」—— **可捕获,但弱于自陈** ⚠️

- **改写者自陈(`:285-287`)**:「可捕获(**复合变异**):同删 `id < 0 continue` 与 `if (candidate > _nextPatient)` 两守卫
  ⇒ candidate 回退到 -1+1 = 0 ⇒ next 发 0 = 与既有 id 重号 ⇒ 红。」
- **实测:该复合变异 = M4 ⇒ ② 仍绿。自陈有误。** 逐行手算(_nextPatient = 2,输入 `[-1,-1]`):

  ```
  candidate = 2
  id = -1：跳过 :96？已删 ⇒ 落到 :97 非 MaxValue ⇒ :99 `-1+1 = 0 > 2`? 否 ⇒ candidate 仍 2
  id = -1：同上
  :101 已删守卫 ⇒ _nextPatient = candidate = 2        ← 不是 0
  NextPatientId() == 2  ⇒ 断言通过(绿)
  ```
  根因:**第三条守卫 `:99` 的 `if (id + 1 > candidate)` 仍在** —— 它把 candidate 锁在 ≥ 初值,
  所以「删两条守卫」不足以让 candidate 掉到 0。自陈漏算了这一条。
- **② 真正能红的变异只有「整函数重写」类**:
  - **M8**(重写为 `_nextPatient = max(ids ≥ 0) + 1`,即**恰恰是本 story 原稿那种自造算式**)
    ⇒ 全哨兵流下 `max` 空集取 0 ⇒ `_nextPatient = 1` ≠ 2 ⇒ **红**;
  - **M9**(2 行改:初始化 `= 0` + `:101` 去守卫)⇒ `_nextPatient = 0` ⇒ **红**。
  - **无任何单行删/改能让 ② 转红**(矩阵 M1/M2/M3/M3'/M5/M6/M7 全部绿)。
- ⇒ **结论**:② 确有判别力(它挡住「从零重算」这一整类实现 —— 而该类正是原稿的病灶),
  但**判别力弱于测试 docstring 与 story 的陈述**。见 §五 新发现 1。

### ③ 「溢出硬失败」—— **可捕获,成立** ✅(带一个前提)

- **删 `:97-98`** ⇒ `int.MaxValue + 1` 在**非受检**上下文回绕为 `int.MinValue` ⇒ `:99` 不触发
  ⇒ 无异常 ⇒ `Assert.Throws` **红**(M5)。
- ⚠️ **前提**:该结论**依赖「算术未受检」**。若工程开启 `CheckForOverflowUnderflow`,
  `:99` 的加法本身会抛 `OverflowException`,`Assert.Throws` **照样绿** ⇒ 该变异**失去**判别力。
  **实测**:全仓 `grep -rn CheckForOverflowUnderflow`(**排除 `unity/Library/`**) **零命中**;
  `unity/Assets/**/*.asmdef` 无该字段,仓内**无** `csc.rsp` / `mcs.rsp` / `Directory.Build.props`
  (`unity/Assets/Sim/Sim.asmdef` 亦无)⇒ 当下为**默认非受检** ⇒ **变异可捕获,成立**。
  ⚠️ 登记为**隐性前提**:一旦有人加 `CheckForOverflowUnderflow`,③ 的判别力**静默失效**。

### 4. 改写者自陈的两个「不可证伪面」逐条核实

| 自陈 | 核实结论 | 依据 |
|---|---|---|
| 「哨兵不污染 max」**单独**在算术上不可证伪(`-1` 是下界) | ✅ **属实** | 纯 `max` 形态下 `-1 < 任何非负 id`,含它与否结果相同;且 `candidate` 初值 = `_nextPatient ≥ 0`(构造器拒负,`:35-36`) |
| **单独**删 `id < 0 continue`(保留只升不降)**不会**红 | ✅ **属实** | 矩阵 M2:①②③ 全绿。**且可证明更强**:`candidate ≥ 0` 恒成立(初值 ≥ 0,且只在 `id ≥ 0` 时取 `id+1`),而 `id < 0 ⇒ id+1 ≤ 0 ≤ candidate` ⇒ **`:99` 的赋值永不触发** ⇒ **删 `:96` 是行为等价的** |
| (顺带)**同删两守卫 ⇒ 红** | ❌ **不属实** | 矩阵 M4:② **绿**。见上文逐行手算 |

**我另行证明的一条(改写者未说、但同源)**:`:101` 的「只升不降」守卫**同样是行为冗余的** ——
`candidate` 初值 = `_nextPatient` 且只增 ⇒ `candidate ≥ _nextPatient` 恒成立 ⇒ 守卫条件为真时
赋值与「无条件赋值」等价,为假时两者都不动 ⇒ **M3 与 M3' 的 ② 结果相同(皆绿)**。
⇒ **「只升不降」在本实现里是结构性性质(靠 `:93` 初值 + `:99` 取 max 形式),不是被守卫强制的**。
这解释了为何**任何**针对该实现的「只升不降」断言都无法被单行变异打红。

---

## 四、Test Evidence 门判定

> ## **通过**

**理由(单条,窄口径)**:**复评开出的通过条件两半均已满足,且经我独立核实(非采信自述)。**

1. **前半「测试改打 `ReconstructPatientHighWater`」** ✅ —— 三处调用点(`:280` / `:291` / `:298`)
   指向**唯一**生产类 `Sim/ItemDatabase/IdAuthority.cs:23`;测试内无同名 shim;旧自造算式已从代码面删除
   (§二.1)。
2. **后半「断『只升不降』」** ✅ —— ②(`:288-293`)在**预置计数器 = 2** 的前提下断言重构后不得回退
   (「不降」半边),①(`:279-282`)断言 0 → 6(「升」半边)。
3. **残余清零**:**原判定点名的唯一残余(测试内自造 `Where().Select().Max()` ⇒ 生产函数零调用)
   在当下代码里已不存在** —— `grep "\.Max()"` 仅命中 docstring 里**描述旧形态**的那一行(`:263`)。
4. **另闭一项**:postfix 复评 §六 新发现 1(GDD `:1121` ② 称「已由 story-001 的测试载体承接」与代码不符)
   **现已在代码面成立** —— 该句所指的调用点在 story-001 测试里**已存在**(`:280`)。
   ⚠️ 时序留痕:该声明**先写、后补代码**(story `:175-177` 已如实自陈),本件确认**当下**自述与代码一致。

⚠️ **判定的边界(防过度)**:本门通过**只**覆盖「B-1 测试半边已闭」这一命题。
B-2 / B-3 / B-4 已于前轮 `CLOSED`,本件**不复核、不改判**。
本件**不**背书:① CI job 是否在真 runner 跑过;② 借用日志是否真编译了本文件(见 §五);
③ story-001 其余 8 条测试的判别力。

---

## 五、新发现问题 / 未闭项 / 不确定项(如实,禁借绿)

### 新发现

1. **【自陈高估 ② 的判别力 —— 且该错误已复制进 story 文件】(非阻塞 · 必改文本)**
   - 测试 docstring `:285-286` 与 **story 文件 `production/epics/case-system/story-001-…md:169-170`**
     同写:「复合删 `id<0 continue` 与 `只升不降` 两守卫 ⇒ next 回退 0 = 重号 ⇒ 红」;
   - story `:198` 的「B-1 测试半边的收口实证」表更把「**三处可证伪断言**」列为闭合证据;
   - **实测(§三 M4):该复合变异下 ② 仍绿** —— 因为第三条守卫 `IdAuthority.cs:99` 仍在,把 candidate
     锁在 ≥ 初值。⇒ **可被单行变异打红的只有 ① 与 ③ 两处,不是三处**;
   - ⚠️ 这是本项目反复出现的**「自述 ≠ 代码」**病灶的**微缩版**:高估的是**判别力**而非覆盖,
     方向朝上(「禁借绿」同侧)。**建议**:订正两处文本(测试 `:285-287` + story `:169-170`/`:198`),
     或按第 2 条**真把 ② 的判别力补上**;订正前**不得**把 `:198` 那行当作「② 可被单行变异捕获」的证据。
   - **本件未改任何文件**(硬约束)。

2. **【② 无单行变异可捕获 —— 可低成本补强】(非阻塞 · 建议)**
   根因已证明(§三.4):`candidate ≥ _nextPatient` 恒成立 ⇒ 「只升不降」是结构性性质。
   若要让 ② 具备单行判别力,须**换一个可观测面**(例如断言「重构后再铸造的号 ≠ 已发号」,
   或对 `IRollbackableIdAuthority` 的负域守卫 `:58` 设断言);**仅靠当前输入做不到**。

3. **【① 的输入顺序使哨兵守卫仍不可观测 —— 免费可补强】(非阻塞 · 建议)**
   ① 现输入 `[1, 5, -1, 3]` 把哨兵放在**最大值之后、且其后元素更小**的位置 ⇒
   **`IdAuthority.cs:96` 的 `continue` 改成 `break`(真实一类 bug:截断扫描)不会被 ① 抓到**
   (矩阵 M6:现顺序绿)。把输入改为 `[1, -1, 5, 3]`(哨兵**夹在**中间)⇒ **M6 转红**(矩阵「①-重排」列),
   且**正确实现下仍绿** ⇒ 零成本、纯增益。⚠️ 注意:这**不**推翻「哨兵不污染 max 不可证伪」的自陈
   (删 `:96` 仍不可观测,M2)—— 它补的是**另一条**面(扫描截断)。

### 未闭项(承前轮,本件确认仍未闭)

4. **【CI job `kindgen-assertions` 从未在真 runner 跑过】** —— 前轮已登记,本件**未复核、不翻案**
   (不在本件范围)。⇒ 「失败即红」仍是**设计判据**,非**已观测行为**。
5. **【③ 的判别力依赖「非受检算术」这一隐性前提】**(本件新登记,见 §三.③)
   当下成立(仓内无 `CheckForOverflowUnderflow` / 无 `.rsp` / 无 `Directory.Build.props`);
   一旦该设置被打开,③ **静默失去**判别力。建议在测试 docstring 里写明该前提。

### 不确定项(本件无法判定)

6. **【借用日志能否证明本文件被重新编译】** —— 时序上成立(mtime 13:04:41 < 日志 13:09:36),
   但 XML 不含源码哈希。**硬约束禁止我跑 batch**,故本件**不能**给出「新代码确已编译执行」的强证据。
   若发起方需**权威复跑**,请在 batch 空闲时执行:
   `-testFilter DaYiJingCheng.Tests.CaseSystem.CaseIdentityRoutingTest`(期望 `total=9 passed=9 failed=0`)。
7. **【`/tmp` 转录复算的效力边界】** —— §三 的变异矩阵是**算术转录复算**,证明的是**判据算术**,
   **不**证明 Unity 编译产物行为(例如若 IL2CPP / 编译器对 `int.MaxValue + 1` 有额外处理,③ 需实测复核)。
   承 §三.③ 的隐性前提,该边界**不改变**本件的门判定(门判的是「测试是否改打生产载体 + 断只升不降」)。

---

## 六、判定摘要(一行)

**门 = 通过**(通过条件两半 **均满足且经独立核实** · 原残余**清零** · GDD `:1121` 的承接声明**当下与代码一致**)——
**新登记 1 条非阻塞必改文本**(测试 `:285-286` 与 story `:169-170`/`:198` 高估了 ② 的判别力:该复合变异实测**不会**转红)
**+ 2 条低成本补强建议**(② 无可单行捕获面 · ① 输入顺序改为 `[1,-1,5,3]` 可捕获 `continue→break`)。
