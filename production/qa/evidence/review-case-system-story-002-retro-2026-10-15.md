# 补做评审原件 —— case-system story-002(立案两路径·开案唯一·判断记录事件化)

**评审时点**:2026-10-15(**补做评审** —— 评当下代码,承 `.claude/docs/review-workflow.md` §义务 3 不可追补)
**被评对象**:`unity/Assets/Sim/CaseOpenDecider.cs`(含 `CaseStreamQuery`)+ `unity/Assets/Tests/EditMode/CaseSystem/case_open_and_judgment_test.cs`
**轮次**:双代理(unity-specialist 代码面 + qa-lead 测试面)
**原件缺口确证**:`review-case-system-001-004-gap-audit-2026-10-15.md` §二
**代码未变证据**:`git log --all -- <两文件>` 仅一条 commit `c4e3708`(2026-10-06) ⇒ 10-06 至今逐字未动
**运行态基线(只读借用)**:`unity/Logs/case-story-002-full.xml`(10-06)与 10-14 两轮全量均记 `CaseOpenAndJudgmentTest` = 23/23 passed。**绿 ≠ AC 判据成立**(见下)。

---

## 一、逐条 AC 判定(代码面)

| AC 号 | 判据在何处 | 判定 | 备注 |
|---|---|---|---|
| **AC-37-30** 路径 A/B 各产生 CaseOpened 入病例流 | 测试 `:54-69`(仅 `CaseOpenDecider.Decide`);路由断言 `:326` | **❌** | `IEventSink` 全文件**仅出现在 doc 注释 `:52`**;`decide(A)==decide(B)`(`:67`)因实现不读 `sourceKind`(`CaseOpenDecider.cs:47-58`)而**恒真**。GDD `:1040` 要求「均经 `IEventSink` 写入」—— 零证据 |
| **AC-37-26** 开案唯一 | 决策 `:98-108`;`CaseStreamQuery.OpenCaseOf`(实现 `:367-434`) | ✅(弱) | 两半各有真判据,但 `hasOpenCase` 由测试**硬编码 true**(`:105`),**未端到端串联**(生产串联在 `CaseOpenWriter.cs:127`) |
| 药材短缺 ⇒ 零 CaseOpened | `:140-146` | ✅(弱) | 只测决策,未测「零写入」 |
| **AC-37-05(判断半边)** 落笔/改写各产生事件 | `:154-165` | **❌** | 断言的是 `StreamRouting.Of(kind)==StreamId.Case`(静态路由查表),非「经 `IEventSink` 产生事件」 |
| **AC-37-05(存新值)** 全序末条 | 自造 `:169-195`(inline `Where/OrderBy/Last`);生产 `:454-482` | ⚠️ ADVISORY | 生产 `LastJudgmentOf` 存在且另被测 —— 但 AC 命名测试 `:169` **仍自造**;且 `LastJudgmentOf` **忽略 `caseId` 形参**(实现 `:110-125` 零引用);全载荷 = `default(PayloadRef)` ⇒「非差分」**物理不可验证** |
| **AC-37-05(处置半边)** 处置后跨流可查、无第二副本 | **无** | **❌** | `grep 处置/Treatment/Drug/第二副本` 零命中 ⇒ 必交内容缺失 |
| **AC-37-23** 改写史全序列保留 | `:214-239` | ⚠️ | 仅路由 + 自造排序;流级「全序列真的都在」无断言(归 story-006) |
| `Judgment` 三字段 / `lexicon_id` / `confidence` | `:248-278` | ✅ | 反射真实生产类型 |
| freehand_text 零判定消费 | `:283-294` | ⚠️ ADVISORY | 仅证 payload struct 无该字段(代理),非引用扫描;`BindingFlags.Public` 漏私有 |

---

## 二、判定清单

### BLOCKING(4)

- **[BLOCKING-1 · AC 判据缺失] AC-37-30 入流义务零验证** —— 测试 `:54-69`;`IEventSink` 仅注释;`decide(A)==decide(B)` 恒真。
  **修复建议**:以 fake `IEventSink` 驱动 `CaseOpenWriter.TryOpen`(`Sim/CaseOpenWriter.cs:113-139`),断言 A/B 各追加一条 `CaseOpened`,同病人再交互零新增(`:127`)。
- **[BLOCKING-2 · 扫错对象] AC-37-05 判断半边入流未验证** —— `:154-165` 查路由表(`Sim/StreamRouting.g.cs:41-42`)≠ 入流。
  **修复建议**:接 `JudgmentWriter`(`Gameplay.Boot/JudgmentWriter.cs`)或 fake sink,断言落笔/改写各产生一条 `SimEvent`。
- **[BLOCKING-3 · 必交内容缺失] AC-37-05 处置半边断言缺失** —— story §AC 明列此集成断言,测试零实现。
  **修复建议**:补跨流断言(病史流有处置事件时 37 可查到且无第二副本)。
- **[BLOCKING-4 · 自造函数] AC-37-05 命名测试仍自造** —— `:185-187` 内联重造 `J(c)`;同形态残留 `:214-239`、`:302-338`。
  **缓解(不豁免)**:生产 `LastJudgmentOf` 另有真判据(`:454-482`)。**修复**:三处自造点改调生产。

### ADVISORY(5)

- **A1** `LastJudgmentOf` 死参(`caseId` 零引用)+ 无跨案混淆测试 ⇒ 同病人两案泄漏不可被发现。
- **A2** freehand_text 判据是反射代理,非 AC 字面的引用扫描。
- **A3** AC-37-26 端到端未串联(`hasOpenCase` 硬编码)。
- **A4** `OpenCaseOf` 不排序,依赖调用方前置(doc 要求已按全序);传未排序流会**静默**取错。
- **A5** 流程面:story §Status 记 Complete 但未引用报告路径 ⇒ 义务第 2 条「借绿」形态;本件落盘后须回填。

### commit 自述三条修复的独立复核(c4e3708)

| 自述 | 复核 |
|---|---|
| B1「测试原测自定义函数 → 改测生产」 | **部分成立** —— `OpenCaseOf` 确被覆盖;`LastJudgmentOf` 侧仍自造 |
| B2「AC-37-30 → 验证两路径决策一致 + StreamRouting 落病例流」 | **未兑现 AC 本体** —— 降级替换(路由表 ≠ 入流) |
| B3「AC-37-05 存新值 → 改测 LastJudgmentOf」 | **成立(有保留)** —— 函数存在且被测;保留 `caseId` 死参 / 无跨案负测 / 零载荷不可验非差分 |

**总评**:三条自述均非无中生有(生产函数真实存在且被测、23 测全绿),但**判定不完备** —— 正是义务文档「自述是待验证主张」的活例。

---

## 三、架构纪律(全绿 —— 无 BLOCKING)

纯函数 ✅(`Decide:47-59` / `OpenCaseOf:74-93` / `LastJudgmentOf:110-125` 无副作用)·
写路径归 `IEventSink` ✅(`CaseOpenWriter.cs:135` `_eventSink.Append`)·
ADR-029 ✅(全 `Sim/` 内 `new PayloadRef(` 命中皆注释;写者用 `_encoder.Encode` `:132`)·
门 A ✅(`Sim.asmdef` `noEngineReferences:true` + references 唯 `Sim.Contracts`)·
零 float ✅ ·全序键 ✅(`CaseId`=`(Tick,Patient,Seq)` `PayloadCommon.cs:59-65`;跨流键 `EventOrder.cs:21`)

---

## 四、验证命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# 1) 复跑本 story(23 测全绿 —— 但绿 ≠ 判据成立)
unity -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode \
  -testFilter DaYiJingCheng.Tests.CaseSystem.CaseOpenAndJudgmentTest \
  -testResults unity/Logs/retro-s002.xml -logFile unity/Logs/retro-s002.log
# 2) 证 BLOCKING-1/2:测试从不触 IEventSink/写者/Append(期望:仅 :52 注释)
grep -n "IEventSink\|CaseOpenWriter\|\.Append(" unity/Assets/Tests/EditMode/CaseSystem/case_open_and_judgment_test.cs
# 3) 证 BLOCKING-3:处置半边零命中
grep -n "处置\|Treatment\|Drug\|第二副本" unity/Assets/Tests/EditMode/CaseSystem/case_open_and_judgment_test.cs
# 4) 证 BLOCKING-4:自造 J(c) 残留(期望命中 :185-187 / :235 / :332-334)
grep -n "OrderBy\|\.Last()" unity/Assets/Tests/EditMode/CaseSystem/case_open_and_judgment_test.cs
# 5) 证 A1:caseId 死参(期望 0)
sed -n '110,125p' unity/Assets/Sim/CaseOpenDecider.cs | grep -c "caseId"
```

---

## 五、修复归属与边界

- 本件 4 BLOCKING 的修复**归新一轮**(义务:不得声称「原判定如此」);
- story-002 状态须标注「原件为补评件」;`Complete` 的引用路径待回填(§二 A5)。

---

# 六、测试面半边(qa-lead · 2026-10-15 · 同轮合并)

**取证前提(已核实)**:测试文件自 `c4e3708`(2026-10-06)后**零改动**(`git log c4e3708..HEAD -- <file>` 为空)⇒ 当下代码 = commit 自述时代码。
**无借绿**:`unity/Logs/s09_close_editmode_v2.xml`(10-14 12:08Z)记 `CaseOpenAndJudgmentTest` 23 条**全部在列**,整轮 `total=3326 passed=3279 failed=0`。
⇒ 测试面全部 BLOCKING **不是「没跑」,而是「跑了但断言不成立 / 判据落错对象」**。

## 六·一 逐条 AC 判定(测试面)

| AC 号 | 覆盖测试 | 判定 | 理由 |
|---|---|---|---|
| **AC-37-30** 两路径皆入流 | `:53-69`、`:71-89` | ❌ | **恒真**:`Decide`(`CaseOpenDecider.cs:47-59`)**从不读 `sourceKind`** ⇒ `:67-68` 的 `decisionA == decisionB` 构造上不可能为假。AC 核心语义「经 `IEventSink` 写入 `CaseOpened`」**零覆盖**(`IEventSink` 全文件仅 `:52` 注释) |
| **AC-37-26** 开案唯一 | `:97-132`(打 `Decide`);真实路径 `:375-434` + `case_open_writer_test.cs:97-111` | ✅ | **覆盖成立但在别处**:本文件三段只喂布尔 `hasOpenCase`,而 `Decide` 无生产调用者(ADV-2)。要害由 `CaseStreamQuery.OpenCaseOf` 的 `afterClose/revisit/crossPatient` 与 `CaseOpenWriter.TryOpen` 真实覆盖 |
| 药材短缺 ⇒ 零 CaseOpened | `:139-146` | ❌(本文件)/ ✅(真实路径) | **扫错对象 + 夹具缺失**:只把 `hasPatient:false` 当布尔喂进去,**未注入药材短缺事件、无点名 fixture、无「零 CaseOpened」流断言**。真实负路径由 `case_open_writer_test.cs:113-125` 覆盖,但那条测的也不是「短缺注入」 |
| **AC-37-05 · 判断半边**「各产生一条事件进流,无内存私有副本」 | `:154-165` | ❌ | 两条断言只是 `StreamRouting.Of(kind) == StreamId.Case` —— 而 `StreamRouting.g.cs` 是 kindgen 依 `entities.yaml` **生成物**(A1–A5 已在生成期跑过)⇒ **生成物自证**;「各产生一条事件进流」(无 Append 断言)+「无内存私有副本」零覆盖 |
| **AC-37-05 · 存新值**「J(c) = 全序最后一条」 | `:168-195`、`:440-482` | ❌ | 见 §六·三 B3 |
| **AC-37-05 · 处置半边**「跨流可查、无第二副本」 | **本文件零测试** | ❌ | 全仓 `grep "第二副本"` = **0 命中**;AC 明写该断言归本 story |
| 改写史全序列保留(AC-37-23 事件面) | `:213-239` | ❌ | 三重弱:① 只测路由表;② `OrderBy(EventOrder.KeyOf)` 排的是**入参已排好序**的数组,断言 `sorted[i].Seq == i` = 断言「LINQ 保持原序」;③ **「病例流不折叠」(AC-37-23 实质)零覆盖** —— 无折叠算子参与。AC-37-23 = `[I]` 集成 · BLOCKING |
| `Judgment` 类型形状 | `:247-278` | ✅ | 存在性 + 类型 int/byte,两条真判据 |
| freehand_text 零判定消费 | `:280-294` | ⚠️ | 字段缺席代理 ≠ 引用扫描。**反证**:生产经 `JudgmentWriter.TryRecord` 的 `EncodeJudgmentRecorded(in payload, freehandText: string.Empty)` 承载 freehand_text,**该值存在且在场,此负断言照样通过** |

## 六·二 测试面 BLOCKING(10)

| # | 判定 | 证据 |
|---|---|---|
| **BLK-1** | **恒真断言** —— AC-37-30 的 `decisionA == decisionB` | `test:67-68`;`CaseOpenDecider.cs:47-59` 函数体从不引用 `sourceKind`(仅签名携带) |
| **BLK-2** | **测试自造函数(纯常量比较)** —— 同 tick 边界的 `caseIdA != caseIdB` | `test:79-87`:`MakeCaseId(100,1,0)` vs `MakeCaseId(100,2,0)` 由测试字面量构造,`1 ≠ 2` 恒真;未从任何流/生产函数派生 |
| **BLK-3** | **测试自造函数** —— AC-37-05 存新值仍自造 | `test:184-187` 与 `:331-334` 用 `.Where(...).OrderBy(EventOrder.KeyOf).Last()` 在测试内重算 J(c),**未调用** `CaseStreamQuery.LastJudgmentOf` |
| **BLK-4** | **AC-37-05 判断半边覆盖缺失**(同代码面 BLOCKING-2) | `test:154-165` 仅断言生成路由表;文件内无 `IEventSink` 使用 |
| **BLK-5** | **重复断言 + 误标边界** —— `test_ac3705_edge_emptyJudgment_isLegal` | `test:198-206` 构造 `default(PayloadRef)` 后断言路由表,与 `:158` **同一事实**;「空判断 = `lexicon_id` 0 ∧ `freehand_text` ''」(S-8.3)**未被验**(无载荷、无 lexicon_id) |
| **BLK-6** | **AC-37-23「改写史全序列保留/不折叠」未真被覆盖** | `test:213-239` 三条断言中无一条涉及折叠算子 |
| **BLK-7** | **AC-37-05 处置半边「无第二副本」断言全仓不存在** | `grep -rn "第二副本" unity/Assets design production` → **0 命中** |
| **BLK-8** | **必交边界夹具缺失 ⇒ 断言强度不足以证伪** —— `LastJudgmentOf` 的 `caseId` 形参**从未被使用**;⚠️ **实害已存在** | `CaseOpenDecider.cs:110-125`(`caseId` 体内零引用,注释自承「P0 简化:调用方负责传入该案的事件子集」);`test:455-468` 输入为单案升序 ⇒ 跨案/乱序缺陷不可见。**实害**:`CompositionRoot.cs:245-246` 把 `stream.Events`(`EventStream.cs:65` = 全部混合流)传给 `CasebookVitalsSource`,其 `InkAgeTicks`(`:221-224`)据 `LastJudgmentOf(_caseStream, openCase.Value)` 求墨龄 ⇒ **会取到别的病人的判断事件**;而 `CasebookVitalsSource.cs:214` 注释写「按 case 过滤」,**与实现相悖** |
| **BLK-9** | **负断言未按 AC 契约构造** —— 药材短缺 | AC 明写「点名 fixture」;本文件无 fixture(`Fixtures/` 下仅词表文件) |
| **BLK-10** | **必交夹具类型错位** —— story-002 声明的 Evidence = 本 EditMode 文件;而 AC-37-05 / AC-37-23 在 GDD 均为 **`[I]` 集成 · BLOCKING** | `case-system.md:1050 / 1088`(AC 表)与 `:1136`(载体欠账表:AC-37-05/06/28 → `tests/integration/case-system/` ❌ 目录不存在)。本文件是纯数据层单测(自陈 `test:16-17`),无 sink、无真实流、无折叠 ⇒ 不构成 `[I]` 证据 |

## 六·三 测试面 ADVISORY(5)

- **ADV-1** `test_judgmentShape_freehandText_zeroJudgmentConsumption` 是字段缺席代理,非「引用扫描」(`:280-294`)
- **ADV-2** `CaseOpenDecider.Decide` **无生产调用者**(死代码/孪生体),本文件 6 条测试打在它身上;真实路径 `CaseOpenWriter.TryOpen`(`:113-139`)**自行重实现**同一前置条件。全仓 grep 排除 Tests 后**仅剩注释引用**(`CaseOpenWriter.cs:17,18,65`)
- **ADV-3** story AC 文本「`Judgment` 三字段(含 freehand_text)」与 payload 两字段实现不一致;`CasePayloads.cs:1-3` 自陈「只落 sim 消费面三字段(freehand_text 住呈现装配)」
- **ADV-4** 断言重复:`:97-108`/`:111-118`/`:120-132` 与 `:342-360` 反复重测 `Decide` 同一分支,无新增信息量
- **ADV-5** `test_ac3723_revisionHistory_allInCaseStream` 的排序断言在已排序输入上做,属「重排恒等」

## 六·四 必答两条(测试面独立复核 —— 与代码面结论对照)

**B1 自述「测试原测自定义函数 → 改测 `CaseOpenDecider.Decide` / `CaseStreamQuery`」→ 部分成立,不完备**
- 成立:`Decide` 被调用 13 处(`:57,62,78,82,101,105,115,125,129,143,308,348,352`);`OpenCaseOf` 6 处;`LastJudgmentOf` 3 处;二者均定义在 `Sim/CaseOpenDecider.cs`;
- 不完备三处:① 残留自造函数(`:184-187` / `:331-334`);② **被测生产函数是孪生体**(ADV-2),形式成立但**语义仍与真实路径脱钩**;③ 改测后 AC-37-30 的关键断言(`:67-68`)恰因 `sourceKind` 被忽略而**恒真** —— 换了对象,断言仍不承担判据。

**B3 自述「AC-37-05 存新值 → 改测 `LastJudgmentOf` 取全序末条」→ 不成立(就其点名的测试)**
- 名为 `test_ac3705_storeNewValue_judgmentIsLastEvent` 的测试(`:168-195`)**完全没有调用** `LastJudgmentOf` —— 用测试自己的 `.Where().OrderBy().Last()`(`:184-187`)。**自述与该测试实际内容不符**,形态仍是测试自造函数;
- 生产函数**另有** 3 条测试(`:440-482`),但 `caseId` 零引用 + 输入单案已排序 ⇒ 「取全序最后一条」**从未被真正施压**;
- ⇒ **「存新值」语义未被判据覆盖**;缺口已对应真实代码路径(BLK-8 实害)。

> ⚠️ **与代码面结论的差异**:代码面(本文件 §二)就 B3 判「成立(有保留)」—— 因其看的是「生产 `LastJudgmentOf` 存在且另被测」;测试面判「不成立」—— 因其看的是「**点名的那条测试**根本没调它」。二者不矛盾,粒度不同。**综合口径**:生产函数存在且另被测(代码面真),但 AC 点名的测试仍自造(测试面真) ⇒ **修复面 = 三处自造点改调生产 + 补跨案/乱序夹具**,与代码面 BLOCKING-4 一致。

## 六·五 测试面验证命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# BLK-1/BLK-2:恒真断言
sed -n '53,89p' unity/Assets/Tests/EditMode/CaseSystem/case_open_and_judgment_test.cs
# BLK-3:AC-37-05 存新值测试是否自造(期望 OrderBy(...).Last(),无 LastJudgmentOf 调用)
sed -n '168,195p' unity/Assets/Tests/EditMode/CaseSystem/case_open_and_judgment_test.cs
# BLK-7:处置半边「无第二副本」是否存在于全仓(期望 0 命中)
grep -rn "第二副本" unity/Assets design production
# BLK-8:实害链
sed -n '110,125p' unity/Assets/Sim/CaseOpenDecider.cs          # caseId 零引用
sed -n '245,246p' unity/Assets/Gameplay.Boot/CompositionRoot.cs # stream.Events(全流)
sed -n '65p'      unity/Assets/Sim/EventStream.cs               # Events => _events(单列表)
# BLK-10:载体类型与目录
sed -n '1050p;1088p;1136p' design/gdd/case-system.md
ls tests/integration/                    # ⚠️ 代理未列此目录 ⇒ 下游缓解可能性未查证(见下)
# ADV-2:Decide 无生产调用者
grep -rn "CaseOpenDecider" --include=*.cs unity/Assets | grep -v "/Tests/"
# 借绿复核
grep -c "CaseOpenAndJudgmentTest" unity/Logs/s09_close_editmode_v2.xml   # 期望 24
```

## 六·六 未完成核查(如实标注 · 禁借绿)

1. **AC-37-05 / 23 在 `tests/integration/case-system/` 下是否已有集成件**:代理只确认 `./tests/integration` **目录存在**,**未列出其内容** ⇒ BLK-10 的**下游缓解可能性未查证完**。若该目录下已有集成件,BLK-10 应从「缺失」降为「本 story 证据文件类型错位」。**本轮不追认任一读法,登记待查**。
2. GDD 载体欠账表(`case-system.md:1130-1145`)其余 AC 的「目录不存在」**当下状态**未逐条复核 —— 本报告只对 story-002 的 6 条 AC 负责。
3. **未实际重跑测试**:借 `s09_close_editmode_v2.xml`(10-14)作旁证;该 log 晚于文件最后改动(10-06),故结论成立,但非本轮亲跑。

## 六·七 测试面结论

story-002 的声明证据 `case_open_and_judgment_test.cs` **可跑、可过、无借绿**,但测试面有 **10 条 BLOCKING** —— 核心问题是**断言形态错误而非测试缺失**(恒真 / 纯常量比较 / 测试内自造 J(c) / 只测生成路由表),以及**一条能证伪的真实生产缺陷**(`LastJudgmentOf` 忽略 `caseId`,而生产调用点传入全局混合流)**未被任何夹具覆盖**。
commit `c4e3708` 三条自述:**B1 形式成立、语义脱钩;B2(AC-37-30 入流义务)未在 AC-37-30 的测试内落实(路由断言被挪到 AC-37-05 的测试里,属跨条挪用);B3 与其点名的测试内容不符**。

**合取判定(story-002)**:代码面 4 BLOCKING + 测试面 10 BLOCKING,去重后**核心 4 类** —— ① AC-37-30 入流义务零验证(两面同证);② AC-37-05 判断半边扫错对象(两面同证);③ AC-37-05 处置半边断言全仓缺失(两面同证);④ `LastJudgmentOf` 忽略 `caseId`(测试面独家,且有生产实害链)。**Test Evidence 门不通过。**

---

# 七、勘误(2026-10-15 落盘后复核追加 —— 本段修正上文,不删原判定)

## 七·一 触发

落盘后执行「未完成核查」第 1 条(查 `tests/integration/case-system/` 是否存在集成件)时,
发现**补评存在系统性盲区**:两名代理均只核了 story §Test Evidence **声明的那个文件**,
却使用了「**零验证**」「**全仓零命中**」「**无处可依**」等**超出其取证范围**的表述。

实测:case-system 的测试件**远不止各 story 声明的那一个** —— 且有一批**晚于 2026-10-06**
(sprint-05/06/08,2026-10-10 ~ 10-13)的接线件。按补评口径「**评当下代码**」,它们**必须计入**。

**根因(两方各半,如实登记)**:① 调度方的评审 prompt 只指向声明件,未要求扫全树;
② 代理在仅核一件的前提下作出全域否定的**越权表述** —— 这是「补评不可追补」之外的**第二种评审失效模式**,
建议补入 `.claude/docs/review-workflow.md` 的教训段。

## 七·二 声明的证据面 —— 事实更正

`tests/integration/case-system/` **确实不存在**;但 `tests/README.md` 澄清:
`tests/unit|integration/` 只是**账本约定**,UTF 实际编译的是 `EditMode/` 与 `PlayMode/` 两棵树。
case-system 的实际测试面(全部,含基线运行证据):

| 件 | 位置 | 规模 | 关键判据 |
|---|---|---|---|
| `case_identity_routing_test.cs` | EditMode/CaseSystem | 13 | (001 声明件) |
| `case_open_and_judgment_test.cs` | EditMode/CaseSystem | 23 | (002 声明件) |
| **`case_open_writer_test.cs`** | EditMode/CaseSystem | **7** | **真组合根 + 真 EventStream**,`realBag_appendsExactlyOneCaseEvent_roundTrip` 断言「病例流恰增 1 条」+ 载荷池 round-trip + 流前缀查询可见 |
| **`judgment_writer_test.cs`** | **EditMode/Boot** | **4** | **真装配**,`record_onOpenCase_writesJudgment_caseStream` 断言「落笔 ⇒ 病例流多一条 + 尾事件 Kind = JudgmentRecorded」 |
| **`casebook_command_router_test.cs`** | **EditMode/Boot** | **8** | 含 `writesJudgmentRecordedSuspected` / `writesJudgmentRevisedConfirmed` |
| **`ink_age_test.cs`** | **EditMode/Boot** | **4** | 墨龄派生态(与 BLK-8 直接相关,见下) |
| **`case_opened_wiring_test.cs`** | **PlayMode** | **1** | **真启动序**(BootRoot → CaseOpenedDriver → TryOpen),断言病史流含 `CaseOpened` |
| `case_close_precondition_test.cs` | EditMode/CaseSystem | 33 | (003 声明件) |
| `case_pattern_detection_test.cs` | EditMode/CaseSystem | 23 | (004 声明件) |
| `case_replay_persistence_test.cs` | EditMode/CaseSystem | 10 | 含 `noFold_historicalRowsComplete` / `noFold_openCasePredicate_bothDirections` / 高水位三流并集 |
| `case_secrecy_discipline_test.cs` | EditMode/CaseSystem | 12 | 含 `ac37_20_noConsequenceOwnership` |
| `e2e_full_loop_test.cs` | PlayMode/SkeuomorphicUI | 1 | 七环节全链(走近→查体→对照→落笔→开方→施治→纸面) |

**运行基线**:EditMode `story009_editmode.xml`(10-14 20:09,`total=3326 passed=3279 failed=0`);
**PlayMode `story009_review_playmode.xml`(10-14 20:00,`total=186 passed=186 failed=0`)**。

## 七·三 逐条勘误

### ❌ 被推翻(4 条)

| 原判定 | 勘误 | 推翻证据 |
|---|---|---|
| **BLOCKING-1 / BLK-1 / BLK-4**(AC-37-30 入流义务「零验证」) | **推翻** —— AC-37-30 的入流义务**有真载体**,且是**真组合根 + 真流**级 | `CaseOpenWriterTest.test_caseOpened_realBag_appendsExactlyOneCaseEvent_roundTrip`(`_bag.Stream` 恰增 1、断言 `StreamRouting.Of(evt.Kind) == StreamId.Case`、载荷经池 round-trip);PlayMode `CaseOpenedWiringTest.test_caseOpenedDriver_opensCaseAtBoot`(`Assert.IsTrue(hasCaseOpened, "病史流应含 CaseOpened 事件")`,真 BootRoot 启动序) |
| **BLOCKING-2 / BLK-4**(AC-37-05 判断半边「扫错对象」) | **推翻** —— 判断入流有真载体 | `JudgmentWriterTest.test_record_onOpenCase_writesJudgment_caseStream`(真 `CompositionRoot.Assemble()`,`Assert.AreEqual(before + 1, _bag.Stream.Count)`,尾事件 `Kind == JudgmentRecorded`);`casebook_command_router_test` 8 测覆盖落笔/改写/擦除/越界四条路径 |
| **BLOCKING-3 / BLK-7**(AC-37-05 处置半边「全仓零命中」) | **推翻** —— 语义载体在 003 的件里 | 「处置发生后 37 跨流可查到」的**实现** = `CaseCloseDecider.HasTreatmentInWindow`(读病史流处置事件);其判据由 `case_close_precondition_test.cs:98/124/210`(真 `EmergencyTreatmentApplied` / `DrugTreatmentApplied` 事件)覆盖。「第二副本」这一**措辞**确实零命中,但那是**词面**零命中,**不是语义零覆盖** |
| **BLK-6**(AC-37-23「不折叠」零覆盖) | **推翻** | `CaseReplayPersistenceTest.test_case_noFold_historicalRowsComplete` + `test_case_noFold_openCasePredicate_bothDirections`(006 的件,2026-10-08 收口) |
| **BLK-10**(必交夹具类型错位 ⇒ 「缺失」) | **推翻「缺失」,保留「错位」** —— 集成件**存在**(PlayMode 接线件 + `e2e_full_loop_test`);真问题只是 story §Test Evidence 引的不是它们 | 见 §七·二 表 |

### ✅ 仍成立(净保留)

| 判定 | 为何不受影响 |
|---|---|
| **BLK-8 / ADV-1**(`LastJudgmentOf` 的 `caseId` 形参**零引用** + 生产实害链) | 这是**实现层**缺陷,与测试面无关。且新发现的 `ink_age_test.cs` 4 测**全部为单案夹具**(`countsSinceLastJudgment` / `rereadsFromLatestRewrite` / `zero_beforeWrite` / `clampsNonNegative`)⇒ **跨病人混淆仍未被任何件覆盖** ⇒ 实害链(`CompositionRoot.cs:245` 传全局混合流 ⇒ 墨龄取错病人)**成立且更可确证** |
| **BLK-1 的「声明件内恒真」部分** | `case_open_and_judgment_test.cs:67` 的 `decisionA == decisionB` 因 `CaseOpenDecider.Decide` 不读 `sourceKind` 而恒真 —— 这是**该声明件自身**的断言形态问题,别处的覆盖不改变它 |
| **BLK-2 / BLK-3 / BLK-5 / BLK-9**(测试自造函数 / 重复断言 / 负断言未按契约构造) | 同属声明件自身的断言形态,不受他件影响 |
| **ADV-2**(`Decide` 无生产调用者 = 孪生体) | `grep CaseOpenDecider` 排除 Tests 后仍仅注释命中 ⇒ 成立(但注意:真实路径 `CaseOpenWriter` **有**测试,故这是**实现冗余**而非**覆盖缺失**) |

## 七·四 净结论(修正后)

- **代码面 4 BLOCKING** → 保留 **1 类**:BLK-8(`LastJudgmentOf` 死参 + 实害链)。其余三条(AC-37-30 / AC-37-05 两半)系**声明证据与 AC 错配**,非无证据。
- **测试面 10 BLOCKING** → 保留 **5 条**(BLK-2/3/5/9 声明件断言形态 + BLK-1 的恒真半边);推翻 **5 条**(BLK-1 的「零验证」半边 / BLK-4 / BLK-6 / BLK-7 / BLK-10)。
- **门结论不变**:story-002 **Test Evidence 门仍不通过** —— 但性质从「AC 无证据」改为「**声明证据与 AC 错配 + 一条真实现缺陷**」。
- **修复面因此显著收窄**:核心 = ① `LastJudgmentOf` 用 `caseId` 过滤 + 补跨病人夹具;② 声明件断言形态订正;③ 把 `CaseOpenWriterTest` / `JudgmentWriterTest` / PlayMode 接线件**补进 §Test Evidence 的引用面**。
