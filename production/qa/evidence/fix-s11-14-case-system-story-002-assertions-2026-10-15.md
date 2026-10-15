# 修复证据原件 — case-system story-002 六条断言形态

**S11 编号**:批 2(承 2026-10-15 范围变更 · A 组 A3–A8)
**对象**:`case-system` story-002(`BLK-1` 恒真半边 / `BLK-2` / `BLK-3` / `BLK-5` / `BLK-9` / `BLK-10`)
**日期**:2026-10-15
**触发裁定**:用户 2026-10-15「**在 sprint-11 完成这些,不带入下个 sprint**」
**性质**:BLOCKING 级判定的闭合件 ⇒ 含 **原判定 → 修复落点 → 验证命令**(可证伪)

---

## 一、原判定(引补评原件)

出处:`production/qa/evidence/review-case-system-story-002-retro-2026-10-15.md`(⚠️ 已按该件 §七 勘误修正)

> **② 声明件 `case_open_and_judgment_test.cs` 自身的断言形态问题**
> (`decide(A)==decide(B)` 恒真 / 测试自造 `J(c)` / 只查路由表 / 重复断言)。

六条逐项(编号承 `sprint-11.md` §范围变更 A 组):

| ID | 原判定 |
|---|---|
| A3 = `BLK-2` | 同 tick 边界 `caseIdA != caseIdB` 由**测试字面量**构造 ⇒ **恒真** |
| A4 = `BLK-1` 恒真半边 | `decide(A)==decide(B)` 因 `Decide` 不读 `sourceKind` 而**构造上恒真** |
| A5 = `BLK-3` | 自造 `J(c)`(`.Where().OrderBy().Last()`)不调**生产** `LastJudgmentOf` |
| A6 = `BLK-5` | 两条测同一事实(**重复断言**)+ **S-8.3 未验** |
| A7 = `BLK-9` | 药材短缺负断言**未按 AC 契约构造**(无点名 fixture、无流断言) |
| A8 = `BLK-10` | **证据面错配** —— AC-37-05 / 23 是 `[I]` 集成,却指 EditMode 单测 |

---

## 二、修复落点

**单文件**:`unity/Assets/Tests/EditMode/CaseSystem/case_open_and_judgment_test.cs`
`git diff --stat` = **207 insertions / 59 deletions**(已由主轮独立复核);
`[Test]` 计数 **25 → 25**(**只改断言形态,未增删用例**)。

### 2.1 `BLK-1`(恒真半边)—— **删除**,不重包装

被删断言 `Assert.That(decisionA, Is.EqualTo(decisionB), ...)` **被保留的两条分路径断言逻辑蕴含**
(二者皆 `OpenNew` ⇒ 必相等),且 `CaseOpenDecider.Decide` **永不读 `sourceKind`**。

**零独立判别力的证明**:任何使它转红的实现变异(如 `if (sourceKind == A) return NoCase;`)
都**必然**使前两条之一转红 ⇒ 它报的警**永远已被邻条报出**。

⇒ 取「**删除 + docstring 写明删因与真载体**」;**未**灌输入重包装(那只是把恒真藏得更深)。

### 2.2 `BLK-2`(纯常量比较)—— 期望值**由流派生**

```csharp
// 改前:var caseIdA = MakeCaseId(tick, 1, 0);  // 测试字面量,与 caseIdB 构造性不等
// 改后:造两条同 tick 的 CaseOpened 入流,经**生产查询**取 id
var stream = new[]
{
    MakeEvent(tick, new PatientId(1), 0, EventKind.CaseOpened),
    MakeEvent(tick, new PatientId(2), 0, EventKind.CaseOpened),
};
var caseIdA = CaseStreamQuery.OpenCaseOf(stream, 1);
var caseIdB = CaseStreamQuery.OpenCaseOf(stream, 2);
```

**可捕获的变异**:`OpenCaseOf` 丢掉三元组里的 `Patient` 维度 ⇒ 两次查询返回同一条 ⇒ 两 id 相等 ⇒ **红**;
`CaseId.Equals/GetHashCode` 忽略 `Patient` ⇒ **红**;恒返回 `null` ⇒ `HasValue` **红**。

### 2.3 `BLK-3`(自造 `J(c)`)—— 两处均改调**生产**函数

改前(两处,`:184-187` 与 `:331-334`):`.Where(...).OrderBy(EventOrder.KeyOf).Last()`。
改后:一律调 `CaseStreamQuery.LastJudgmentOf(events, caseId)`,且 `caseId` **由流派生**
(`CaseStreamQuery.OpenCaseOf`);期望 `Tick` / `Seq` 亦由夹具构造序派生。

**可捕获的变异**:`LastJudgmentOf` 改「首条胜」⇒ `Tick` / `Seq` 两条**红**;恒 `null` ⇒ `HasValue` **红**。

> ⚠️ **如实登记**:把 `inCase` 归属跟踪整段删掉(= S11-7 之前的旧实现)**本测仍绿** ——
> 单案夹具天然不施压 `caseId`。这一半由同文件两条跨案夹具 +
> `Boot/ink_age_test.cs` 承担(**已写在 docstring 里**,不静默)。

### 2.4 `BLK-5`(重复断言 + S-8.3)—— 去重 + 改成 S-8.3 实证

- **去重**:删掉只覆盖 `JudgmentRecorded` 那条路线断言,保留覆盖面更宽的
  `test_ac3705_judgmentRecorded_and_revised_bothGoToCaseStream`(同时覆盖 Recorded + Revised)。
- **S-8.3 实证**:本测改走**真装配 + 真 `JudgmentWriter`**,验「**擦除 = 写一条空判断**」:

```csharp
var result = writer.TryRevise(tick: 3, patient, lexiconId: 0, confidence: 0);
Assert.That(result, Is.EqualTo(JudgmentWriteResult.Recorded), "空判断合法(S-8.3)");
Assert.That(bag.Stream.Count, Is.EqualTo(before + 1), "擦除 ⇒ 病例流恰增 1 条(非删事件)");
Assert.That(empty.LexiconId, Is.EqualTo(0), "空判断 lexicon_id = 0");
```

**可捕获的变异**:`TryRevise` 加 `if (lexiconId <= 0) return …拒收` ⇒ 结果码 + 流增量两条**红**;
走成「不 Append」⇒ 流增量**红**;载荷写成 `lexicon_id = 1` ⇒ 载荷断言**红**。

### 2.5 `BLK-9`(药材短缺)—— 点名 fixture + 真流零断言

改前只喂布尔(`Decide(hasOpenCase: false, hasPatient: false, ...)`),**无点名 fixture、无流断言**。

改后:
```csharp
private static readonly PatientId MedicineShortageFixture = PatientId.None;  // 点名 fixture

var result = writer.TryOpen(tick: 100, patient: MedicineShortageFixture, diseaseSnapshot: ...);
Assert.That(result, Is.EqualTo(CaseOpenWriteResult.NoPatient), "药材短缺不立案(无病人)");
Assert.That(bag.Stream.Count, Is.EqualTo(streamBefore), "药材短缺 ⇒ 流总事件数不变(零写入)");
Assert.That(CountCaseOpened(bag.Stream.Events), Is.EqualTo(caseBefore), "药材短缺 ⇒ 零 CaseOpened");
```

新增 helper `CountCaseOpened`,docstring 点明「负存在断言的**扫描闭集** = 病例流全部事件」。

**判别力的关键**:旧稿形态下,删掉 `CaseOpenWriter.TryOpen` 的 `if (patient.IsNone) return NoPatient;`
**不会红**;改后**两条都会红**。

### 2.6 `BLK-10`(证据面错配)—— 登记面订正

**非断言类**,修复 = 补 **AC → 真实载体映射表**(11 行),已落 `story-002` §Test Evidence。
表中同时登记补评件的**测数勘误**:`casebook_command_router_test` 记 8 实为 **9** ·
`ink_age_test` 记 4 实为 **5**。

### 2.7 附带改动

- 文件头(`:16-26`)改写:说明本件**由「纯数据层」变为「纯数据层 + 两条真装配段」**(BLK-9 + S-8.3),
  代价是**触 codec(仅载荷回读)** —— 原「不触 codec」的自我描述若不改即成**假声明**。
- 新增 `using DaYiJingCheng.Gameplay.Boot;` / `using DaYiJingCheng.Sim.Codec;`。
  ⚠️ **已核 asmdef 引用可达**:`EditMode.asmdef` 走 **GUID 形引用**(非具名串),
  且同目录 `case_open_writer_test.cs:18` 早有同款 `using` ⇒ **无需改 asmdef**。
- 其余 20 条用例未动。

---

## 三、验证命令(可复跑 · 可证伪)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
git diff --stat -- unity/Assets/Tests/EditMode/CaseSystem/case_open_and_judgment_test.cs
#  → 1 file changed, 207 insertions(+), 59 deletions(-)
grep -c '\[Test\]' unity/Assets/Tests/EditMode/CaseSystem/case_open_and_judgment_test.cs   # → 25
```

全量门(2026-10-15 实测):EditMode `3387/3341/0/46` 退出码 **0** ·
PlayMode `186/186/0` 退出码 **0** · `error CS` = **0 / 0**。
XML 对照基线:`3366/3320/0/46`(Δ +21 = 两个新写者测试件,非本件产生)。

> ⚠️ **S12-0 登记(2026-10-15 规划期补)**:上行 XML 对照基线是**S11-5 后值,引用合法**(作本件的上一批对照)。
> 本件自身实跑 `3387/3341/0/46`;该值**亦非终值** —— 当日批 6(`a5ef7c5` · 13:50)后 EditMode
> 终值 = **`3388/3342/0/46`**。此处与批 5(`3387/3341`)碰巧同值,纯属巧合,勿据此认为批 6 无新增。

---

## 四、未闭合 / 部分闭合(如实 · 禁借绿)

1. **`BLK-5` 的 `freehand_text = ''` 半边 —— 部分闭合**:
   该字段**不在** `JudgmentRecordedPayload` / `JudgmentRevisedPayload` 里
   (ADR-006 Amendment A 的**自由文本例外**,住 blob 变长段)⇒ 要在 sim 层验须**解 blob 段**,
   超出 `PayloadCodec.TryGetPayload` 的接口面。本轮验了**可验的两半**(`lexicon_id = 0` +
   擦除事件真的入流)。**若要全验,须另开一处 blob 段读取的测试面** —— 本轮未动产品代码。
2. **`BLK-10` 的「无第二副本」半边 —— 未以断言闭合**:
   `grep -rn "第二副本"` 全仓 **0 命中**;语义载体是 `CaseCloseQuery.HasTreatmentInWindow`
   (入参即**病史流**,37 不自建副本)⇒ **语义结构性满足,无点名断言**。
   **不得**写成「已覆盖」。
3. **本文件性质变化的登记义务**:登记若跟不上,会落入 BLK-10 的**反向**形态
   (原件变强了、登记没跟上)。已在 story-002 §Test Evidence 同批登记。

---

## 五、本件的取证限度(如实)

- **未跑 Unity 测试**(实现轮按硬约束禁起 batch);全部「绿 / 红」结论来自
  **dotnet 编译 + 逐行照抄已绿配方**。两条新真装配测试的自查风险点已列(见 story-002 登记块)。
- **本轮全量测由主轮统一执行并确认 0 failure**(见 §三)。
