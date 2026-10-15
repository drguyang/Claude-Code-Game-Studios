# 修复证据原件 — case-system 写者落地(批 1)

**S11 编号**:批 1(承 2026-10-15 范围变更)
**对象**:`case-system` story-003 ② B2 · story-004 CB-1 / TB-4
**日期**:2026-10-15
**触发裁定**:用户 2026-10-15「**解冻写者,sprint-11 内实现**」(范围变更子裁定 ①)
**性质**:BLOCKING 级判定的闭合件 ⇒ 按 `story-done/SKILL.md` §评审报告原件要求,
含 **原判定 → 修复落点 → 验证命令**(可证伪)

---

## 一、原判定(逐字引补评原件)

### 1.1 story-003 ② B2 —— 快照半场零行为覆盖

> 出处:`production/qa/evidence/review-case-system-story-003-retro-2026-10-15.md`
>
> **② 快照半场零行为覆盖(5 处反射字段存在性检查 = 借绿);生产侧无快照计算**

「反射字段存在性检查」= 测试只验证 `CaseClosedPayload` **有** `disease_set` / `treated` 字段,
**不验证值**;且**生产侧根本没有计算这两个快照的代码** ⇒ 借绿。

### 1.2 story-004 CB-1 / TB-4 —— 发射语义无生产载体

> 出处:`production/qa/evidence/review-case-system-story-004-retro-2026-10-15.md`
>
> **④ 发射语义无生产载体(借绿)**:`PatternRecognized` 写者未落
> (`WriterExistenceGate.cs:88` 具名豁免)、`IsFired` 零生产调用者,
> 「不重发」由一份**未写、未测**的调用方契约保证。

---

## 二、修复落点

### 2.1 新增件

| 文件 | 行数 | 职责 |
|---|---|---|
| `unity/Assets/Sim/CaseCloseWriter.cs` | 181 | **结案写者** —— 落 `CaseClosed` 事件 |
| `unity/Assets/Sim/PatternRecognitionWriter.cs` | 199 | **同源检测写者** —— 落 `PatternRecognized` 事件 |
| `unity/Assets/Tests/EditMode/CaseSystem/case_close_writer_test.cs` | 12 测 | 结案写者 |
| `unity/Assets/Tests/EditMode/CaseSystem/pattern_recognition_writer_test.cs` | 9 测 | 检测写者 |

### 2.2 `CaseCloseWriter` 如何闭合 ② B2(快照半场)

缺陷的要害是「**生产侧无快照计算**」。本写者把两个快照字段的**产生责任**明确归位:

① **`treated`** —— **本类自算**,非调用方转述(`CaseCloseWriter.cs:169`):

> `// treated = **本类自算的窗口证据**(非调用方转述);走到此分支 hasTreatmentInWindow 恒 true。`

窗口 = **闭区间** `[caseId.Tick, tick]`(承 ADR-008 §四 方向订正 2026-09-17)。
⇒ 「病人挨了打也被算作已处置」这一类错误**不再可能**由调用方注入。

② **`disease_set`** —— 结案时刻的病种快照,由**调用方**给出。
⚠️ **这是已登记口径,非本件新开例外**:承 `CaseOpenWriter.disease_snapshot` 的
「**Sim 侧够不着者由调用方给出**」先例(`CaseOpenWriter.cs:28-31`;`WorldSeed` / 版本 /
解码 delegate 全部照此)。理由 = ADR-025 §①(`Sim` 引用集恰 = {BCL, `Sim.Contracts`}),
`DiseaseIdSet` 的真源解码归 9 / `Sim.Codec`。

结果码 `CaseCloseWriteResult`:`Closed=0` / `RejectAlreadyClosed=1` / `RejectNotOpen=2` /
`RejectNotChecked=3` / `RejectNoTreatment=4`;判序**统一交** `CaseCloseDecider.Decide`
(**不复制判定逻辑** —— 与既有 `CaseOpenWriter` 同构)。

### 2.3 `PatternRecognitionWriter` 如何闭合 CB-1 / TB-4

载荷构造(`PatternRecognitionWriter.cs`):

```
PatientId        = PatientId.None.Value          // = -1,承 ADR-007 §四 哨兵
AnchorCase       = emission.MemberSet[Count - 1] // = MemberSet[末位](跨阈值那一例)
SaltedKey        = emission.SaltedKey
MemberDiseaseSet = new DiseaseIdSet(1UL << emission.DiseaseOrdinal)
Version          = _version
```

**复用 `PatternDetector.NextEmissions`** —— **不重写检测、不自行排序**
(单一定序源 = `SortBySaltedKey`,承 AC-37-27)。
ctor 9 参,其中两个 delegate(`diseaseExtractor` / `saltedKeyExtractor`)由调用方给出
(**同一「Sim 侧够不着者由调用方给出」口径**)。

### 2.4 撤豁免(与 2.1 **同批**,不可拆)

`unity/Assets/Editor.Tools.Gates/WriterExistenceGate.cs` 撤除两条具名豁免:

```
-  ("CaseClosed", ...)        ← 已删
-  ("PatternRecognized", ...)  ← 已删
```

⚠️ **本类与两个写者必须同批提交** —— 任一半边单独提交皆红(判据①「已存在写者的 Kind
仍挂豁免表 = 红」/ 判据「写者缺失 = 红」)。留痕注释见该文件 `:87`。

### 2.5 ⚠️ 未做、且不应做:生产触发点接线

三条硬理由(**这不是偷懒,是结构上做不到**):

1. **结案是玩家显式动作**(GDD 规则六「结案唯一出口」),「结案页宿主」意图源**尚不存在**;
2. 两个解码 delegate 的**真实现**要落 `Sim.Codec`(跨装配面);
3. `EventStream` **无订阅机制**,而 `PatternRecognized` 要求「`CaseClosed` 入流后**同 tick** 触发」。

**先例**:`CaseOpenWriter` 自己**也未接线**(其文件头明写「生产触发点尚未接线」),
而已被 `WriterExistenceGate` 接受为「有写者」。⇒ 接线归**交互装配轮**。

---

## 三、验证命令(可复跑 · 可证伪)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
git diff --stat -- unity/Assets/Tests/EditMode/CaseSystem/case_close_writer_test.cs \
                  unity/Assets/Tests/EditMode/CaseSystem/pattern_recognition_writer_test.cs
grep -c '\[Test\]' unity/Assets/Tests/EditMode/CaseSystem/case_close_writer_test.cs          # → 12
grep -c '\[Test\]' unity/Assets/Tests/EditMode/CaseSystem/pattern_recognition_writer_test.cs # → 9

# 写者半边(扫描面内是否存在生产 Encode 调用点)
grep -rn "Encode(EventKind\.CaseClosed\|Encode(EventKind\.PatternRecognized" \
  unity/Assets/Sim unity/Assets/Sim.Contracts unity/Assets/Gameplay.Presentation \
  unity/Assets/Gameplay.Boot unity/Assets/Gameplay.Input unity/Assets/Gameplay.UI
# → CaseCloseWriter.cs:172 · PatternRecognitionWriter.cs:189

# 撤表半边(豁免表内是否还残留)
grep -n '("CaseClosed"\|("PatternRecognized"' \
  unity/Assets/Editor.Tools.Gates/WriterExistenceGate.cs
# → 空(已撤净)
```

### 全量门(2026-10-15 实测 · 双信号一致)

```bash
UNITY=/home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity
PROJ=/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity
"$UNITY" -batchmode -nographics -projectPath "$PROJ" -runTests -testPlatform EditMode \
  -testResults "$PROJ/Logs/s11-close-editmode.xml" -logFile "$PROJ/Logs/s11-close-editmode.log"
"$UNITY" -batchmode -nographics -projectPath "$PROJ" -runTests -testPlatform PlayMode \
  -testResults "$PROJ/Logs/s11-close-playmode.xml" -logFile "$PROJ/Logs/s11-close-playmode.log"
```

| 平台 | 退出码 | XML | 对照基线 |
|---|---|---|---|
| EditMode | **0** | `total=3387 passed=3341 failed=0 inconclusive=0 skipped=46` | `3366/3320/0/46` ⇒ **+21** |
| PlayMode | **0** | `total=186 passed=186 failed=0 inconclusive=0 skipped=0` | `186/186/0` ⇒ **逐位一致** |

`error CS` 计数 = **0 / 0**。

> ⚠️ **`+21` 的构成已核**:`case_close_writer_test` 12 + `pattern_recognition_writer_test` 9 = **21**。
> 同期 story-001 测试 13→9(−4)、story-004 30→34(+4)⇒ 净 0。

> ⚠️ **日志是易失指针**:`unity/Logs/` 在 `.gitignore:196`。权威载体是**本件**;可在本机复跑再生。

---

## 四、未闭合 / 不确定(如实)

1. **生产触发点未接线**(见 §2.5)—— 结构上做不到,归装配轮。**不是本件的缺口**,但**不得**被读成「写者已端到端可用」。
2. **首轮曾 7 红**:两个写者测试件的夹具**违反 `EventStream.Append` 的 Tick 非降不变量**
   (`EventStream.cs:109` 的 BCD-码-3 硬不变量),**非实现缺陷**。已修(拆开「铺人」与「铺案」,
   使 Append 严格按 tick 升序),并**保住场景与判别力** —— 修后 `total` 仍为 3387(未靠删测变绿)。
3. **`pattern_recognition_writer_test` 的 TB-1 对策保留**:`multiFired_writtenInSaltedKeyAscendingOrder`
   令流中首现序 = salted **降序**,期望输出序 = 升序 ⇒ 删 `SortBySaltedKey` 即红。
   这是**刻意的判别性设计**,不是夹具凑巧。

---

**签署**:本件由实现轮落盘;`WriterExistenceGate` 的门验证由 EditMode + 门测试在本轮全量内覆盖。
