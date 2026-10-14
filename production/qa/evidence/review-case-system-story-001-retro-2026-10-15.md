# 补做评审原件 —— case-system story-001(病例流 Kind 登记与病例身份)

**评审时点**:2026-10-15(**补做评审** —— 评当下代码,承 `.claude/docs/review-workflow.md` §义务 3 不可追补)
**被评对象**:`production/epics/case-system/story-001-case-stream-kinds-identity.md`(Status: Complete @ `e64efad`,2026-10-06)
**实现**:`Sim.Contracts/Payloads/PayloadCommon.cs` · `CasePayloads.cs` · `Sim/StreamRouting.g.cs` · 真源 `design/registry/entities.yaml` · 生成器 `tools/kindgen/kindgen.py`
**测试**:`unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs`(13 测)
**轮次**:双代理(unity-specialist 代码面 + qa-lead 测试面)
**原件缺口确证**:`review-case-system-001-004-gap-audit-2026-10-15.md` §二(001 确证产出 BLOCKING 级判定 + 无原件)

> ⚠️ **两面判定不同 —— 非矛盾,是覆盖面不同**:
> · **代码面**(unity-specialist):**无 BLOCKING** —— `case_id` 三元组契约 / 载荷形状 / 路由生成物 / 跨流全序键 皆与 GDD·ADR 一致,注册链条严整;
> · **测试面**(qa-lead):**4 BLOCKING** —— 病灶全在**断言形态**(扫错对象 / 空跑 / 借绿 / AC 号一号两义),不在产品代码。
> 结论:**story-001 的 Test Evidence 门不通过**,但**产品代码无须返工**。

---

## 一、逐条 AC 判定

| AC | 代码面 | 测试面 | 综合 |
|---|---|---|---|
| **AC-37-01** 三元组全流唯一 | ✅ `CaseId.Equals`(`PayloadCommon.cs:59-80`) | ⚠️ 前半 ✅(`:52-74`) / 后半 **NOT-RUN**(「被引用 case_id 须存在」全库无载体;`:92-99` 是测试自建 `HashSet` 演示) | ⚠️ 半 |
| **AC-37-02** 两病人同 tick 不撞号 | ✅ | ✅(`:108-124`,打生产 `CaseId.Equals`) | ✅ |
| **AC-37-03** kindgen A1–A5 | ✅ 生成物对齐(`StreamRouting.g.cs:39-43` 五 arm) | **❌** A1 ✅ / **A2 空跑** / A3·A4·A5 弱代理 | ❌ |
| **AC-37-04** 无 `opened_tick`;含 `salted_key` | ✅ | ✅(`:246-275`,生产类型反射) | ✅ |
| **AC-37-05** 跨流全序无平局 | ✅ `EventOrderKey.cs:50-59` 四分量齐 | ✅ / 局限:6 条事件**全 `Seq=0`**,`Seq` 分量作用未被断言 | ✅(弱) |
| **AC-37-06** 高水位不受 −1 污染 | ✅ `PatientId.None = -1`(`PatientId.cs:18-22`) | **❌ 扫错对象 + 恒真** | ❌ |

---

## 二、判定清单

### 测试面 BLOCKING(4)

**[B-1 · 扫错对象 + 测试自造函数 + 恒真断言] AC-37-06 打的是测试内 LINQ,生产载体零调用**
- 生产载体 `IdAuthority.ReconstructPatientHighWater`(`Sim/ItemDatabase/IdAuthority.cs:90-102`)**存在且零调用**;
- 测试 `:342-345` 自写 `Where(!IsNone).Select(Value).Max()`,`:347` 断言 `Is.EqualTo(5)` 由测试字面量 `{1,5,None,3}` 决定 ⇒ **任何实现下恒真**;
- **更深一层**:AC 原文判据**算术上不可证伪** —— 哨兵 −1 永不可能成为含非负 id 集合的最大值;真实风险只在「把 −1 重解释为无符号大值」,而该路径全库不存在;
- ⚠️ `if (id < 0) continue;`(`IdAuthority.cs:96`)在 `candidate ≥ 0` 不变量下是**观测冗余** ⇒ **不存在能打红的正例**;
- **修复**:AC-37-06 须**重述判据**(如「`next = max(seen)+1` 只升不降」)并改打生产载体。仅加大输入无效。

**[B-2 · 空跑断言(vacuous)] AC-37-03 的 A2 子项零断言**
- `:153-172` 内层 `foreach` 在当下 5 条 case `payload_schema` 上**执行 0 次**(实测 float/double 命中数 = 0);
- 唯一活着的断言是 `:157` 的 `Count == 5`;且扫的是 registry **散文**,非载荷 struct 字段类型;
- 旁注:kindgen 自己的 A2 是同款弱实现,但**已登记在案**(`kindgen.py:11-14`「严格形未实现」)⇒ 测试把一份**已承认的弱检查**当成 AC 达标证据。

**[B-3 · 覆盖虚报 / AC 号一号两义]**
- `:5-8` 测试头**自填** `AC-37-03/04/05/06`,与 GDD 权威编号(`case-system.md:1036-1051`)**指向完全不同的需求**:
  GDD AC-37-03 = 重复结案幂等拒收 · AC-37-04 = 未勾「已处置」拒绝 · AC-37-05 = 判断记录落病例流 · AC-37-06 = 迁移重放一致;
- 这三条在本目录内**已被姊妹测试占用并声称覆盖**(`case_close_precondition_test.cs` / `case_open_and_judgment_test.cs` / `case_replay_persistence_test.cs`);
- ⇒ 任何按 AC 号做的覆盖矩阵都会**重复计数**,把 story-001 的测试误记为 AC-37-03/05/06 的证据;
- story 文件里这 4 条 bullet **本就无编号**,测试头是**自行编号**。**修复零成本**:改为无编号引用。

**[B-4 · 借绿(载体未跑却报绿)] AC-37-03 的执行半边 = NOT-RUN**
- story §QA Test Cases 明写 Given「entities.yaml 五 Kind 行」→ When「**运行生成器**」;
- 测试**从不运行 kindgen**(`grep kindgen` 零命中),改为在测试内复刻其扫描(`:361-442`);
- `StreamRouting.g.cs:3,142` 的「产物存在即断言已过」是**生成物自述**,不是证据(同「不得凭 commit 自述判定」);
- ⇒ **把 kindgen 删掉,13 条测试照样全绿**;
- **修复**:「CI 执行 `kindgen --check`」应为 AC-37-03 的**唯一载体**,测试面不再复刻(B-2/B-4 是同一缺口的两半)。

### 代码面 ADVISORY(6)

- **CA-1** `CaseId` 缺比较器 —— story Note #2 要求「readonly 三元组 **+ 比较器(Tick→Patient→Seq)**」,实测 `PayloadCommon.cs:59` 仅 `IEquatable`,**无 `IComparable`、无 `CaseIdComparer`**(全库 grep 零命中)。ADR/GDD 未要求;现消费方按已排序列表取 `CaseId` 不需自身排序 ⇒ 属**实现对 Note 的偏离**,非 AC 冲突。若 story-002/003 将来要直接排 `CaseId`,须补
- **CA-2** A2/A3/A4/A5 测试**读文件系统**(`:361-455` 手搓 YAML 解析 + `Application.dataPath`),违 `.claude/rules/test-standards.md`「Unit tests must not depend on external state(filesystem…)」,且与文件头「不触引擎 API」自述相抵。缓解:`Count==5` 兜底 ⇒ 解析落空会红而非假绿
- **CA-3** 反射名精确匹配 PascalCase(`GetField("OpenedTick")`)不命中 snake_case,判据比「扫任意大小写」弱一档
- **CA-4** `DiseaseIdSet` 的 `IEquatable`(`PayloadCommon.cs:89-100`)已实现但本 story 测试未触
- **CA-5** **重点专项**:`IEquatable` 与 ADR-006 Amendment A「Payload 无引用字段」**结构层相容** —— `CaseId`/`DiseaseIdSet` 只含值类型字段,补 `IEquatable` 不引入引用存储字段。⚠️ 代理自陈**部分核查**:`Equals(object)`/`GetHashCode()` **调用时装箱**(方法级非存储字段;泛型容器走强类型不装箱),**sim 热路径未逐一排查**;未打开 ADR-006 Amendment A 原文逐字比对
- **CA-6** 代理**未执行 kindgen 本体** ⇒「A1–A5 在当下真源上真跑绿」由 C# 代理测试**间接**坐实,非直证(补:测试面已实跑 `kindgen.py --check` → EXIT=0)

### 测试面 ADVISORY(8)

- **TA-1/TA-2** 边界值覆盖缺失:无空集、无单元素;**`Seq` 同 `(Tick,Patient)` 内单调零覆盖**(6 条事件全 `Seq=0`,无一对「同 (Tick,Stream,Patient) 不同 Seq」)⇒ 全序键的 `Seq` 分量参与排序从未被断言 —— 而 AC-37-01「全流唯一」**依赖**它
- **TA-3** A3/A4/A5 的**反例能力未证**:A5 反向方向用**名字前缀启发式**(`:218-225`),加一个不以 `Case/Pattern/Judgment` 开头的 case 流 Kind 即可漏检而仍报绿
- **TA-4/A-4** 别名纪律未机械化:`:256` 注释断言「tick 只在 `SimEvent.Tick` 一处」被 `CaseClosedPayload.CaseId.Tick`(`CasePayloads.cs:26`)**证伪** ⇒ 应做成全库 payload 扫描
- **TA-5/A-5** story §AC 六条 bullet **全部仍为 `- [ ]` 未勾**,而 §Status 已是 `Complete ✅`
- **TA-6/A-8** 「排序确定且无平局」是**构造性举例**(手挑 6 条互异事件,`OrderBy` 后必然严格递增)⇒ 要成不变量须改为可反例形式
- **TA-7/A-6** `AC-37-01` 的 `:71-73` 恒真(对测试自造 `Seq` 集合演示,非测生产码;同 AC 另有真断言,不改判定)
- **TA-8/A-5** §Implementation Notes 5 要求钉 `struct Judgment` 形状;实测全库**无 `struct Judgment`**,偏离已在 `PayloadCommon.cs:18-21`**登记** ⇒ 属**已登记的接受偏离**,仅记录

---

## 三、正面核实(记录 —— 防「全 BLOCKING」失真)

**代码面全绿**:
- 必交夹具**未缺失**;被测对象确为生产类型,无测试侧副本;
- **A1**(`:132-148`)、**AC-37-02**、**AC-37-04 两条**、**AC-37-05 的 `StreamPriority` 序**(打生产枚举 `StreamId.cs:10-16`)皆**直打生产码的真断言**;
- 无「扫错文件」:`FindRepoPath`(`:444-455`)解析到**真实仓库根**的 `entities.yaml`,读真源非字面量;
- `case_id = (Tick, Patient, Seq)` 与 GDD F-37.2(`case-system.md:661`)一致;`opened_tick` 已删,与 ADR-008 `:157-161` 一致;
- `CaseClosedPayload.Treated` 为 `bool` —— 与 ADR-008 `:210` 及抄本 `:302-303` 一致;
- 跨流键字段序 `Tick→StreamPriority→Patient→Seq`(`EventOrderKey.cs:50-59`)与 ADR-008 §二 / ADR-006 Amendment C 一致;
- 真源整齐:`entities.yaml` 五支 case Kind **三必填字段齐**(`stream: case` / `author: "37…"` / `payload_schema`);生成物对齐;
- **无 ADR/GDD 冲突**(代码面结论)。

**测试面**:
- 无 `[Ignore]`、无 `Assert.Pass` 桩、无测试自造的 decider 副本;
- 13/13 全绿(基线借用,见 §四归属声明)—— **绿 ≠ AC 成立**(本轮四 BLOCKING 全是「跑了但判据不成立」)。

---

## 四、验证命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# 1) 基线:13 全绿(⚠️ 借用日志 —— 来源 = story-009 轮 unity/Logs/story009_editmode.xml,10-14 19:20)
grep -o 'test-case[^>]*CaseIdentityRoutingTest[^>]*' unity/Logs/story009_editmode.xml \
 | sed -E 's/.*fullname="[^"]*CaseIdentityRoutingTest\.([^"]*)".*result="([^"]*)".*/\2  \1/'
# 2) 反证 B-4:生成器真过,但测试从不调它
python3 tools/kindgen/kindgen.py --check; echo "EXIT=$?"          # 期望 EXIT=0
grep -n "kindgen" unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs   # 期望:无输出
# 3) 实测 B-2(A2 空跑)
python3 - <<'PY'
import re, yaml, pathlib
reg = yaml.safe_load(pathlib.Path("design/registry/entities.yaml").read_text(encoding="utf-8"))
case = [e for e in reg["constants"] if str(e.get("name","")).startswith("SimEvent.Kind.") and e.get("stream")=="case"]
n = sum(len(re.findall(r"[Ff]loat|[Dd]ouble", str(e.get("payload_schema","")))) for e in case)
print("case Kinds:", len(case), "| float/double 命中:", n, "=> 内层 foreach 执行次数:", n)
PY
#    期望: case Kinds: 5 | float/double 命中: 0 => 内层 foreach 执行次数: 0
# 4) 反证 B-1(AC-37-06 打的是测试内代码)
grep -n "ReconstructPatientHighWater\|HighWater" unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs  # 期望:无输出
grep -rn "ReconstructPatientHighWater" unity/Assets --include=*.cs        # 期望:仅 IdAuthority.cs:90 + id_authority.cs:168
sed -n '342,348p' unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs  # 期望:自写 Where(...).Max() + Is.EqualTo(5)
# 5) 权威复跑(我未跑;Unity 空闲,可直接跑)
pgrep -f Unity; ls unity/Temp/UnityLockfile 2>/dev/null
/home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics -projectPath unity \
  -runTests -testPlatform EditMode -testFilter DaYiJingCheng.Tests.CaseSystem.CaseIdentityRoutingTest \
  -testResults "$PWD/unity/Logs/s11_case001_review_editmode.xml" -logFile unity/Logs/s11_case001_review_editmode.log
#    期望:13 passed / 0 failed —— 与 B-1..B-4 不矛盾(全绿是「测试能过」,不是「AC 被验证」)
```

---

## 五、修复归属与边界

- **B-1 需退设计**:AC-37-06 判据重述(**不是**加输入)归 `case-system` epic 所有者 + qa-lead;
- **B-2/B-4 归 kindgen 侧**:「CI 跑 `kindgen --check`」+ A2 严格形(逐字段类型化)是同一缺口两半,`kindgen.py:11-14` 已登记未兑现;
- **B-3 先行、零成本**:测试头自填 AC 号改无编号引用;
- **TA-1/2/3/6** 由 qa-tester 补(空集/单元素 / 同 `(Tick,Stream,Patient)` 不同 `Seq` 的排序对 / A3·A5 变异反例);
- **全部修复归新一轮**(义务:不得声称「原判定如此」);story-001 状态须标注「原件为补评件」;
- **产品代码无须返工**(代码面无 BLOCKING)。

---

# 六、勘误复核(2026-10-15 落盘后追加)

**触发**:story-002/003 勘误(见 `review-case-system-story-002-retro-2026-10-15.md` §七·一)——
复核发现补评代理存在「只核声明件却作全域否定」的失效模式。本件据此重扫全树(含 `EditMode/Boot/`、
`PlayMode/`、`tests/`)以确认本判定是否同样受影响。

**复核结论:本件无勘误,4 条 BLOCKING 全部保留。**

理由:本件的四条判定**全部是 `case_identity_routing_test.cs` 这一声明件自身的断言形态问题**,
不依赖「别处有无覆盖」:

| 判定 | 为何不受他件影响 |
|---|---|
| B-1(AC-37-06 打的是测试内 LINQ,生产载体 `IdAuthority.ReconstructPatientHighWater` 零调用) | 生产载体的调用点在全仓唯一,不因他件存在而改变;且判据**算术上不可证伪**的结论独立成立 |
| B-2(AC-37-03 A2 **空跑**) | 内层 `foreach` 执行 0 次是**该测试在当下真源上**的实测性质 |
| B-4(kindgen 载体**借绿**) | 「把 kindgen 删掉,13 条测试照样全绿」是该测试**自身**的性质 —— 已复核全树,无第二处运行 kindgen 的测试 |
| B-3(测试头 AC 号一号两义) | 测试头 `:5-8` 的自填编号,与别处无关 |

**附带确认**:复核新发现的各件(尤其 `Boot/judgment_writer_test.cs` / `PlayMode/case_opened_wiring_test.cs`)
均属 story-002/003 的面,**不构成 story-001 任何 AC 的载体** ——
本 story 的 AC-37-01/02/03/04/05/06 的证据面确实只有声明件一处。

**运行基线补正**:PlayMode `story009_review_playmode.xml`(2026-10-14 20:00,`total=186 passed=186 failed=0`)
对本 story 无增量证据(其中无 case 身份/路由相关件)。
