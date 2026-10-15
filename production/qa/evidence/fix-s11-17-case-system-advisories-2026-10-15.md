# 修复证据原件 —— case-system ADVISORY 全清(S11-17)

- **日期**:2026-10-15
- **sprint**:sprint-11(用户裁定「在 sprint-11 完成这些,不带入下个 sprint」的**收尾批**)
- **范围**:`production/qa/evidence/review-case-system-story-00{1..4}-{retro,postfix}-2026-10-15.md`
  登记的**全部 ADVISORY**(非 BLOCKING —— BLOCKING 已由 S11-12…S11-16 闭合)
- **触发**:用户「**剩下的 ADVISORY 也一并清了**」+ 两项裁定(「加次级键」·「补 AC 挂到支柱四不变量名下」)
- **结论**:**全部 ADVISORY 就地处置完毕**;双平台门通过

---

## 一、原判定 → 修复落点(逐条)

| # | 原判定(出处) | 修复落点 | 可捕获的变异(判据) |
|---|---|---|---|
| 1 | **002 ADV-1** `freehand_text` 的「引用扫描」实为**字段缺席代理**,映射表未登记该缺口 | `story-002` AC→载体映射表**补一行**(显式登记「**代理级、无引用扫描**」+ 真引用扫描的**结构性不可做**理由) | 登记类(无断言面) |
| 2 | **002 ADV-2** `CaseOpenDecider.Decide` **无生产调用者**(与结案侧不对称) | `CaseOpenWriter.TryOpen` 前置改走 `Decide` + `switch`(带 fail-loud default,与 `CaseCloseWriter.cs:148` 同形) | 逐分支行为等价已核对;`Decide` 新增决策成员而未同步 switch ⇒ 抛 |
| 3 | **002 ADV-4** 多条测反复打 `Decide` 同一分支 | **不构成缺陷** —— 同一纯函数的**不同输入分支**各有其断言,重复的是调用形态非断言;判定为「**无处置**」并在此登记 | — |
| 4 | **002 ADV-5** `test_ac3723_revisionHistory_allInCaseStream` 排序断言在**已排序输入**上做(重排恒等) | 夹具插入序改 `[2,0,1]`(期望序 `[0,1,2]`) | 去掉 `OrderBy` ⇒ `sorted[0].Seq == 2` ⇒ **红** |
| 5 | **003 §6.2** `case_close_writer_test.cs` 的 `decoded.Treated is True` **结构性恒真** | **删除**该断言 + 留注说明(`CaseCloseWriter.cs:172` 自陈写入分支 `hasTreatmentInWindow` 恒 true ⇒ 无法区分「自算」与「硬编码 true」,而后者**行为等价**) | 无(该断言本就无判别力;窗口逻辑的判别力载体 = `case_close_precondition_test.cs` 的负例) |
| 6 | **003 §6.3** `IsInWindow` **生产零调用点** + 窗口谓词**重复实现** | `HasTreatmentInWindow` 内联比较 → 复用 `IsInWindow`(消重复 + 给生产调用点) | 语义逐字等价;改 `IsInWindow` 的区间闭合性 ⇒ 两侧同变(单一出处) |
| 7 | **004 TA-1** AC-37-11 夹具与 AC-37-08 **逐字重复**,断言又是其**真子集** | 重写为**两腿**(接口面 + 行为面,见 §二) | 加 `string diseaseName` 形参 ⇒ **红**;判断噪声被计入候选 ⇒ **红** |
| 8 | **004 TA-2** `IsFired` 三例 `salted_key` 塞进 `SimEvent.Seq` **自造载体** | 改**真载体**(`PayloadEncoder` 编码 + `PayloadCodec` 解码);`Emit` 的盐提取器同批换真载体 | 载荷编解码错位 / `IsFired` 比较恒真 ⇒ **红** |
| 9 | **004 TA-5** AC-37-14 负断言按**字段名**判 | 改**闭集相等**(字段名集恰等于白名单 5 元) | 加任一字段(含换名携带)⇒ **红** |
| 10 | **004 TA-7** AC-37-34「第 4 例**同 tick**」边界未覆盖 | 新增 `test_ac3734_sameTickFourthCase_membersAreFirstThreeInFullOrder` | 成员取后 3 例 / 删 `candidates.Sort` ⇒ **红** |
| 11 | **004 TA-8** 阈值「≥ 2」Guardrail **未强制**(ctor 只拒 `< 1`) | `PatternRecognitionWriter.cs:123` 改 `< 2`(真源 = GDD `case-system.md:1004`);测试补**阈值 1** 一格 | 守卫改回 `< 1` ⇒ **红** |
| 12 | **004 §六②-3** 元规则清单措辞略松(`test_ac3709` 列在「输入序 ≠ 期望序」组,而其夹具输入序 == 期望序) | 文件头拆为**两组**(满足该纪律 / **满足另一条轴**),`test_ac3709` 移入后者 | 结构性 |
| 13 | **004 §六②-5** 写者 `anchor_case = MemberSet[末位]` 在「同 tick 批量跨阈值」下**未证伪** | **检测器侧**由 TA-7 新夹具覆盖该维;**写者侧仍无**同 tick 批量夹具 ⇒ **如实登记为残余**(见 §六) | — |
| 14 | **001 §六-5 / 002 §五-5** 变异矩阵**无常驻执行体**(7 变异是 `/tmp` 沙箱一次性实测) | 新建 `tools/kindgen/mutation_probe.py` + CI job `kindgen-mutation-probe` | 任一变异逃逸 ⇒ CI 红(见 §四) |
| 15 | **004 §六②-4** `SortBySaltedKey` 用 `List.Sort`(**不稳定**),64 位碰撞时次序未定义 | **用户裁定「加次级键」** ⇒ 加 ordinal 次级键 + 改**可注入键形**使其**可证伪**(见 §三) | 删次级键 ⇒ 等键组保持插入序(降序)⇒ **红** |
| 16 | **用户裁定** 「补 AC 挂到支柱四不变量名下」 | GDD `case-system.md` 「硬约束(跨系统)」表补一行(处置集 = 权威枚举) | 设计件登记(无断言面) |

---

## 二、TA-1 重写(AC-37-11)—— 为什么必须拆两腿

原稿夹具与 AC-37-08 **逐字相同**:

```csharp
// AC-37-08 与 AC-37-11 原稿(逐字重复)
var caseEvents = new[] {
    MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),
    MakeEvent(200, new PatientId(2), 0, EventKind.CaseClosed),
    MakeEvent(300, new PatientId(3), 0, EventKind.CaseClosed),
};
var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease);
```

而 AC-37-08 断 `Fired is True` ∧ `FiredDiseases == [0]`,AC-37-11 只断 `Fired is True`
⇒ **真子集,零新增判别力**。原注释自陈「用同一病种 ordinal 0 模拟病名不同」——
恰恰**没模拟到「病名」这一维**。

AC-37-11 的真实命题是**负命题**:「公式**不读**诊断」。负命题的可证伪形态只能是:

- **腿 ①(接口面)**:`Evaluate` 两重载的**形参表无任何承载病名 / 文本的入口** ——
  这是「不读」的**结构性**证明(没有入口 ⇒ 读不到)。断言 = 形参类型无 `string`
  ∧ 形参名 ∈ 允许集。
- **腿 ②(行为面)**:流内塞入**空判断**(`lexicon_id = 0`)/ **错名判断**噪声,
  三例同 D 仍**恰触发一次**且成员集**逐位** = 三条 `CaseClosed`。噪声刻意排在各自
  `CaseClosed` **之前**(更早 tick)⇒ 若实现误把判断事件计入候选,「每病人全序最早例」
  会取到噪声 ⇒ 成员集断言必红。

> ⚠️ **`freehand_text` 本体(自由文本)住 blob 变长段**(ADR-006 Amendment G-3,
> `CasePayloads.cs:3`)⇒ `Sim` 消费面**结构性够不着** ⇒ 该维**不可**在本层端到端喂入,
> 只能如上两腿表达。此边界与 002 ADV-1 的登记同源。

---

## 三、次级键(用户裁定)—— 与上一批「已回退」记录的关系

**必须分清**,否则会重犯上一批的错:

| 上一批(2026-10-15 早) | 本轮(2026-10-15 S11-17) |
|---|---|
| 自行加 ordinal 次级键,**自陈引 GDD F-37.1 逐字要求** | 用户**明确裁定**「加次级键」 |
| 引据**失实**(GDD `:828-830` 全文无 ordinal 次级键) | 引据**仍失实** —— 本轮**不重复该引据**,改登记为「确定性加固,**非** GDD 明文兑现」 |
| **无夹具可证伪** | **已消解**:改可注入键形 ⇒ 碰撞键夹具 ⇒ 删次级键**必红** |
| 超出本批 20 条清单 | 在清单内(ADVISORY 清理批) |
| ⇒ 用户裁定**回退** | ⇒ 用户裁定**保留** |

**实现**:`SortBySaltedKey` 拆两形 ——

```csharp
// public:可注入键形(测试可喂人为碰撞键 ⇒ 次级键可证伪)
public static void SortBySaltedKey(List<int> diseases, Func<int, ulong> keyOf)
{
    ...
    diseases.Sort((a, b) => {
        int cmp = keyOf(a).CompareTo(keyOf(b));
        return cmp != 0 ? cmp : a.CompareTo(b);   // ← 次级键
    });
}

// private:生产用便捷形(两个调用点逐字未改)
private static void SortBySaltedKey(List<int> diseases, long worldSeed)
    => SortBySaltedKey(diseases, d => SplitMix64.HashTagged(worldSeed, "case-salt", d));
```

**性质**:次级键在 **2⁶⁴ 键碰撞**这一 **P0 不可构造**场景下才可观测 ⇒ **不改变任何可达行为**。
与「回退前后双平台测数逐字不变」的上批实证**互相印证**;本批 EditMode `+2` **全部来自新增测**。

---

## 四、变异矩阵的常驻执行体(N1/N2)

**病因**(001 补评原件 §六-5 逐字):

> 【变异矩阵无常驻执行体】§四 的 7 变异是 `/tmp` 沙箱**一次性**实测,**未固化为 CI 步骤**。

**修复**:`tools/kindgen/mutation_probe.py` —— 7 变异**注入 → 跑断言 → 断言被捕获 → 复原**。

设计要点:

- **零文件写入** —— 变异施加在**内存副本**上(monkeypatch `kindgen.parse_payload_structs`
  的返回值 / 深拷贝 `kinds`),**不触碰工作树** ⇒ 与 `git status` 无交互、可与其它步骤并发。
- **零 Unity 依赖** —— 纯 Python + PyYAML,与 `.github/workflows/tests.yml` 既有取向一致。
- **目标必须落在 A2 的扫描闭包内** —— 探针自带 `_closure()` 复算闭包;否则断言根本不扫该
  struct ⇒ 会报「逃逸」而其实是**探针选错目标**(假阴)。⚠️ 此坑**实测踩过**:首版未限闭包,
  M2/M3 假逃逸;加 `_closure` 后 7/7 捕获。
- **夹具前提不成立 ⇒ 退出码 2**(与「逃逸」的退出码 1 区分)—— 前者是**夹具过期信号**,
  后者才是**判别力缺口**。

**CI**:`.github/workflows/tests.yml` 新增 job `kindgen-mutation-probe`。
与既有 `kindgen-assertions` 的分工:**那个 job 证「断言跑得过」,本 job 证「断言抓得住」。**

---

## 五、验证命令与门验证

### 命令(可复现)

```bash
# ① 变异探针(纯 Python,零 Unity)
python3 tools/kindgen/mutation_probe.py ; echo "EXIT=$?"     # 期望:捕获 7 · 逃逸 0 · EXIT=0

# ② kindgen 断言(既有)
python3 tools/kindgen/kindgen.py --check ; echo "EXIT=$?"

# ③ EditMode 全量
/home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics \
  -projectPath "$PWD/unity" -runTests -testPlatform EditMode \
  -testResults "$PWD/unity/Logs/s11-b6-editmode.xml" -logFile "$PWD/unity/Logs/s11-b6-editmode.log"

# ④ PlayMode 全量
/home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics \
  -projectPath "$PWD/unity" -runTests -testPlatform PlayMode \
  -testResults "$PWD/unity/Logs/s11-b6-playmode.xml" -logFile "$PWD/unity/Logs/s11-b6-playmode.log"
```

### 实测结果

| 门 | 结果 | 基线 | Δ |
|---|---|---|---|
| `mutation_probe.py` | 捕获 **7** · 逃逸 **0** · EXIT **0** | 无常驻执行体 | 新增 |
| `kindgen.py --check` | EXIT **0**(35 支) | 同 | 0 |
| **EditMode** | `total=3388 passed=3342 failed=0 skipped=46` · exit **0** | `3386/3340/0/46` | **+2**(新增 2 测,**零删除**) |
| **PlayMode** | `total=186 passed=186 failed=0 skipped=0` · exit **0** | `186/186/0` | **0**(逐位不变) |
| `error CS` | EditMode **0** · PlayMode **0** | 0 | 0 |

> ⚠️ **Δ 的归因(禁借绿)**:EditMode `+2` **恰等于本批新增测试数**
> (`test_ac3734_sameTickFourthCase_…` + `test_sortBySaltedKey_keyCollision_…`),
> **不是**「靠删测试变绿」;产品代码 4 处改动**全部行为等价**(见 §一 各行),
> 由「PlayMode 逐位不变 + EditMode 仅 +2」实证。

---

## 六、未闭项 / 如实登记(禁借绿)

| # | 项 | 状态 | 理由 |
|---|---|---|---|
| 1 | **001 的 CI 从未在真 GitHub runner 跑过** | **仍开放** | 本仓 Unity 测试 job 已移除(`tests.yml` 头注);无 runner 配额 / secrets 可验。**离线做不到**。本批新增的 `kindgen-mutation-probe` job 同样**只验「接线与判据」,未验「运行过」** |
| 2 | **写者侧「同 tick 批量跨阈值」夹具** | **仍开放** | 检测器侧已由 TA-7 新夹具覆盖;写者 9 测仍均逐 tick 递增。低置信、非阻塞 |
| 3 | **002 ADV-4(重复调用同一分支)** | **判定为无处置** | 见 §一 #3 —— 重复的是**调用形态**非**断言**,不构成缺陷 |
| 4 | **002 BLK-5 的 `freehand_text = ''` 半边** | **结构性 NOT-RUN** | 该字段住 blob 变长段,超出 `PayloadCodec.TryGetPayload` 接口面 |
| 5 | **002 BLK-10「无第二副本」措辞半边** | **语义结构性满足,无点名断言** | 载体 = `CaseCloseQuery.HasTreatmentInWindow`(入参即病史流,37 不自建副本) |
| 6 | **次级键的 GDD 依据** | **仍缺(如实登记)** | GDD `:828-830` 无 ordinal 次级键明文;本轮**未改 GDD**(需单独授权)。故定性 = 「确定性加固」**非**「GDD 明文兑现」 |
| 7 | **001 的 `Combat/combat_actions_schema_test.cs:121` 同型** | **归系统 25** | 非本 epic 范围 |

---

## 七、变更文件清单(16)

**产品代码(4)**
1. `unity/Assets/Sim/PatternDetector.cs` —— `SortBySaltedKey` 加次级键 + 改可注入键形
2. `unity/Assets/Sim/PatternRecognitionWriter.cs` —— 阈值守卫 `< 1` → `< 2`
3. `unity/Assets/Sim/CaseCloseDecider.cs` —— `HasTreatmentInWindow` 复用 `IsInWindow`
4. `unity/Assets/Sim/CaseOpenWriter.cs` —— 前置改走 `CaseOpenDecider.Decide`

**测试(4)**
5. `unity/Assets/Tests/EditMode/CaseSystem/case_pattern_detection_test.cs`(TA-1/2/5/7 + 次级键 + 元规则措辞 + 真载体)
6. `unity/Assets/Tests/EditMode/CaseSystem/case_open_and_judgment_test.cs`(ADV-5)
7. `unity/Assets/Tests/EditMode/CaseSystem/case_close_writer_test.cs`(003 §6.2 删恒真断言)
8. `unity/Assets/Tests/EditMode/CaseSystem/pattern_recognition_writer_test.cs`(TA-8 补阈值 1)

**新建(2)**
9. `tools/kindgen/mutation_probe.py`
10. `.github/workflows/tests.yml`(新 job)

**设计件(1 · 用户授权)**
11. `design/gdd/case-system.md`(硬约束表补「处置集 = 权威枚举」)

**登记(5)**
12. `production/epics/case-system/story-002-…md`(映射表补 ADV-1 行)
13. `production/epics/case-system/story-004-…md`(ADVISORY 全清登记块 + 测数 34 → 36)
14. 本件
15. `production/sprints/sprint-11.md`(S11-17)
16. `production/sprint-status.yaml`(S11-17)
