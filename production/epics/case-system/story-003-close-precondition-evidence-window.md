# Story 003: 结案前置 —— 处置证据窗口与快照(复诊后门关闭)

> **Epic**: 病例系统
> **Status**: In Review ⚠️ 2026-10-15 —— 补评门不通过(原 `Complete ✅ 2026-10-06(31e3f9b)` 判定不成立;原件为补评件)
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-15
## Context

**GDD**: `design/gdd/case-system.md`(规则六 结案唯一出口 · F-37.2 已处置/可结案 + 2026-09-17 方向订正 · F-37.3 一案多病种 · EC-37-3)
**Requirement**: TR-case-016(规则六:结案为唯一出口、玩家显式、单例、不可撤销,原 gap —— 本 story 兑现机制层) · TR-case-023(可结案 = 病史流 ≥1 处置事件(窗口内)∧ 玩家勾选) · TR-case-006/007(CaseClosed.disease_set 集合 + treated 快照) · TR-case-026(重复结案幂等拒收)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-008(主): 处置证据窗口化 + 快照
**ADR Decision Summary**: 「已处置」= **处置事件为证**(非玩家自述复选框 —— 否则空手连关三例触发 Boss,与支柱四冲突,ADR-008 B-2 裁定);**2026-09-17 方向订正**:窗口 = `e.Tick ∈ [c.opened_tick, c.CloseTick]`(旧式把区间写反 ⇒ 复诊第二例凭首诊处置通过前置的后门**实际未堵**,本轮才在公式层真正关闭;错误源头 ADR-008:156 已同批订正);结案四铁律:唯一路径 = 玩家 39 显式 / 前置 = 已处置 ∧ 勾选(UI 仪式不产生独立事件)/ 单例(禁全选)/ 不可撤销 + 重复幂等拒收(判定 = 流前缀纯函数);结案时:8 读数态清除 + 判断冻结交 53(AC-8-47 落点)→ `CaseClosed` 快照 `disease_set`(可空,F-37.3 纯伤情案空集)+ `treated`;病名可空照样结(《他回来了》收尾条件);系统**永不自动结案**(规则七)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 谓词 = 跨流读 + 前缀纯函数,纯 C#。

**Control Manifest Rules (this layer)**:
- Required: 窗口方向 = `e.Tick ∈ [CaseOpened.Tick, CloseTick]`;结案写路径仅玩家意图(经 45 上行、主机 Append);单例参数校验
- Forbidden: 「全选结案」入口;自动结案计时器;勾选复选框产生独立事件;把处置内容对错判定塞进 37(AC-37-31:正确性只存在于 53 输入侧)
- Guardrail: 病人死亡/离场不影响结案可行性(状态正交);无处置事件 ⇒ 拒绝路径必须可测(AC-37-21)

---

## Acceptance Criteria

*From GDD `design/gdd/case-system.md`, scoped to this story:*

- [ ] AC-37-04:未勾「已处置」⇒ 结案拒绝(无事件)
- [ ] AC-37-21:已勾选但窗口内**无**处置事件 ⇒ 拒绝(复诊后门正向封堵:首诊处置不满足第二例窗口)
- [ ] AC-37-03:同一 case_id 第二条 CaseClosed 幂等拒收,不产生新事件
- [ ] 窗口边界:`e.Tick == CaseOpened.Tick`(立案即处置)与 `== CloseTick` 均计入(闭区间夹具)
- [ ] `CaseClosed` 载荷 = `{disease_set(集合,可空), treated}` 快照;结案后病人新发病**不追溯**改写旧案(EC-37-2 行)
- [ ] AC-37-18:病人死亡但案未结 ⇒ 病例仍「开」、可正常结案(零自动转移边);病名为空可结案(空是合法终态)
- [ ] 结案副作用序列:读数清除信号(→8)+ 判断冻结(→53)+ CaseClosed 入病例流;37 自身不产生后果事件(后果归 53 订阅)

---

## Implementation Notes

*Derived from ADR-008 Implementation Guidelines:*

1. `已处置(c) := ∃ e ∈ 病史流: e.Patient=c.patient_id ∧ e.Kind ∈ 处置集 ∧ e.Tick ∈ [CaseOpened.Tick, CloseTick]`;
   **处置集(`treatment_set`)现已定权威** —— 本条原写「`InjuryOnset` 是否算处置 → 以 ADR-008 §四 原文与 53 GDD 为准,
   实现前核对,不自行扩集」,而当时该 ADR **无枚举可依**,遂被实现自行其是(见补评 B5):
   - **权威 = `adr-008` §四 `:216-249`**(🟢 2026-10-15 补立):
     `treatment_set := { EmergencyTreatmentApplied(10), DrugTreatmentApplied(11), CareApplied(8) }`;
   - **明确排除** `EmergencyAttempt`(判定输入 ≠ 结算,ADR-009 Amendment I)与
     `InjuryOnset`(25 的战斗伤害结算 —— 收它 = 把「打死」与「治好」等价);
   - **闭环纪律**:增删成员须**同时**改 `adr-008` §四 + `case-system.md` F-37.2 `:666`/`:679` +
     规则六 `:254` + 规则八 `:280` —— **四处口径一致,禁止任一处单独扩集**。
   - **实现落点** = `Sim/CaseCloseDecider.cs` 的 `TreatmentKinds`(**已按上述权威对齐**,S11-10)。
2. `可结案(c) := state=开 ∧ 已处置(c) ∧ 勾选` —— 勾选是意图载荷字段,非事件(仪式语义)。
3. 幂等判据 = 「该案已有 CaseClosed」前缀检查,非锁非标志位。
4. `disease_set` 快照口径 = 病程 overlap(结案时算 F-37.3 的 CaseDiseases),存进载荷后流即真相;重放**永不**跨流再查(treated 同理)。
5. 单例 = 意图参数只容一个 case_id;批量入口在 37 侧类型面不存在(非运行时拒绝)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 002]: 立案/判断记录(本 story 的开案前提)
- [Story 004]: 结案触发的同源评估(CaseClosed 入流后由 004 消费)
- [Story 005]: 「处置对错不进 37」的类型面断言批(AC-37-31/32 归守密 story)
- 系统 39/42:结案页勾选 UI;53:后果订阅

> ⚠️ **37 结案写者延后 —— 正式登记(2026-10-15 用户裁定)**
> **`CaseClosed` 写者未落**,由 `Editor.Tools.Gates/WriterExistenceGate.cs:87` 登记为**具名豁免债**
> (原文:「37 结案写者未落 …… `CaseCloseDecider` 现为纯函数(2026-10-10)」)。
> 裁定 = **接受延后并正式登记**,不复述为「已完成」:
> - **影响面**:补评判定 **B2(快照半场零行为覆盖)** 由此**无载体可测** —— 「快照计算」在生产侧不存在,
>   5 处反射字段存在性检查是该 story 当时的**全部**证据;
> - **证据面订正**:B2 **不由本 story 的既有证据承担**;其闭合**前置 = 结案写者落地**,
>   届时随写者 story 补行为断言(快照 `{disease_set(集合,可空), treated}` 真的被算出并写入载荷),
>   本轮**不虚记**;
> - **保住的部分**:`CaseCloseDecider` 的**前置判定**(`Decide` / `HasTreatmentInWindow` / 窗口方向)
>   有真判据,且是本 story 的技术核心 —— 该部分**不受写者缺失影响**。
> - ⚠️ 与之并行的 **B-1(`IsClosed` 按 `Patient` 匹配)** 是**独立的实现缺陷**,**不因延后而豁免** ——
>   ✅ **已由 S11-8 闭合(2026-10-15)**,含「改回旧实现即转红」的 7 条反证(详见下方 🟢 登记块)。
>   ⚠️ 但**写者缺席**本身**未解**:B-1 修好的是**契约层**,复诊案能否真的结掉仍取决于
>   写者 story 落地后**消费** `IsClosed` —— 在写者落地前,§Out of Scope 的延后登记**继续有效**。

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(方向订正回归)**: 复诊后门必须关
  - Given: 病人 p:首诊处置@t₁,首诊结案@t₂,复诊立案@t₃(t₁<t₂<t₃),复诊无新处置
  - When: 复诊勾选结案
  - Then: **拒绝**(t₁ ∉ [t₃, now])—— 旧式会通过的回归哨兵
  - Edge cases: 复诊窗口内补一条处置 → 放行
- **AC-2(空名结案)**: S-8.3 合法终态
  - Given: 病名栏空 + 有处置事件 + 勾选
  - When: 结案
  - Then: 成功;disease_set 按病程快照(可非空 —— 病名 ≠ 病种)
  - Edge cases: 纯伤情案(无病种)→ disease_set 空集,结案合法(不入检测归 story-004)
- **AC-3(幂等)**: 连点两次结案
  - Given: 案 c 已结
  - When: 再发结案意图
  - Then: 流零新增;副作用不重复(读数清除/冻结信号不再发)
  - Edge cases: 同 tick 双意图 → 恰一条 CaseClosed

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/CaseSystem/case_close_precondition_test.cs` — must exist and pass
**Status**: [ ] In Review — 测试文件存在且 **39/39 绿**(2026-10-15 S11-8 + S11-10 修复后),但补评判定 **Test Evidence 门不通过**(见下方登记块;门内 4 类中 **① B-1 与 ⑤ B5 已修复并各带反证**,余 ②③ 见登记)

> ⚠️ **2026-10-15 补评件登记(S11-2 后续批 · QA 核查)** —— 原评审原件缺失(承 `review-case-system-001-004-gap-audit-2026-10-15.md` §二:003 判定分级未能确证),按义务第 3 条**补做评审(评当下)**,不追认 `31e3f9b` 的历史判定。
> **补评原件**:`production/qa/evidence/review-case-system-story-003-retro-2026-10-15.md`
> **判定(⚠️ 已按 §七 勘误修正 —— 见下)**:代码面 **1 BLOCKING** + 测试面 **4 BLOCKING**(原判 5,§七·一 推翻 B4);核心 4 类 ——
> ① **`CaseCloseQuery.IsClosed` 按 `Patient` 而非 `case_id` 匹配**(`CaseCloseDecider.cs:135`)⇒ 复诊新案被判「已结」,`IsOpen=True ∧ IsClosed=True` 自相矛盾 ⇒ **复诊案永远无法结案**(代理已用 dotnet 8 行为证伪;当前无生产调用点,属**契约层缺陷**);
> ② 快照半场零行为覆盖(5 处反射字段存在性检查 ⇒ 借绿);
> ③ **AC-37-18 夹具不含死亡输入** ⇒ 恒真;
> ~~④ 结案副作用序列三件中两件(→8 读数清除 / →53 判断冻结)**零测试**;~~ ❌ **§七·一 推翻** ——
> 两件的判据在 **8 自己的件**里(`EditMode/DiagnosisSystem/reading_state_test.cs:395-411`:
> `OnCaseClosed` 全状态清空 + `FrozenJudgment.Frozen` + `HandoffTo53`),只是不在 37 的证据文件里;
> ⑤ **`TreatmentKinds` 收录 `InjuryOnset` 无任何权威件授权**(ADR-008 §四 / GDD F-37.2 / registry 三处皆不枚举),语义 = 25 的战斗伤害结算 ⇒「病人挨了打」被计入「已被处置」,正面击穿 GDD `:567`/`:839`。
> 正面:窗口方向 ✅ **正确**(`:108` 为订正式,未复现 ADR-008 §四 旧式反写);AC-37-21 复诊后门负例是**有效回归哨兵**。
> **Test Evidence 门:不通过。** 修复归**新一轮**;状态**已裁定(2026-10-15,用户)转 `In Review`**。

> 🟢 **2026-10-15 S11-8 修复落盘 —— 承本登记块 ① B-1**(用户裁定「同批重构 `IsOpen`」)
> **修复证据原件**:`production/qa/evidence/fix-s11-8-case-system-story-003-b1-2026-10-15.md`
> —— 含**逐字可复跑**的三段命令、三段输出原文、7 条红测逐条消息。
> ⚠️ **不可只引日志**:`unity/Logs/` 在 `.gitignore:196`,是易失中间产物 ⇒ 本块下方所引日志路径
> 是**指针**,权威载体是上述原件。
> **改动**(`unity/Assets/Sim/CaseCloseDecider.cs`):
> ① `CaseCloseQuery.IsClosed` 由「按 `Patient` 匹配」改为 **流序扫描关联** —— `current` 随该病人
> `CaseOpened` 更新,遇该病人 `CaseClosed` 时**仅当 `current == caseId`** 才判已结,随后 `current` 清空;
> ② `IsOpen` 统一为 `OpenedInStream(c) ∧ ¬IsClosed(c)`,与 `IsClosed` **共用同一输入契约**(消 A-6 —— 两函数
> 此前对同一形参 `caseEvents` 隐含不同契约,正是 B-1 的土壤);
> ③ `CaseCloseDecider.Decide` **两判序对调**(`isClosed` 先于 `isOpen`)—— 流派生口径下已结案的 `IsOpen` **恒假**,
> 旧序会把已结案报成 `RejectNotOpen`(**错因码**),且 `(isOpen=true ∧ isClosed=true)` 在原序下
> **只可能来自硬编码**(即 A-3 的不可达状态);对调后该组合**真正不可达**,结果面**零变化**(两分支皆为拒绝且不写事件)。
> **⚠️ 与补评建议的偏离(已如实报告并获批)**:补评 §二 与 `CaseCloseDecider.cs:129` 作者注释**均建议**
> 「经 `PayloadRef` 解码 `CaseClosedPayload.CaseId` 判等」—— **该路径在 `Sim` 侧结构上不可行**:
> `PayloadCodec` 住 `Sim.Codec`,而 `Sim` 引用集**恰 = {BCL, `Sim.Contracts`}**(ADR-025 §① / b2 门),
> `PayloadRef` 只有 `BlobId/Offset/Length`。故改走**流序扫描**(与 `CaseStreamQuery.OpenCaseOf` 同口径)。
> **代价已登记在实现注释里**:该口径下 `CaseClosed` 的归属是**推断**而非**读取**;与载荷解码
> **在合法流上等价**,仅在违反「同一病人同时至多一个开案」(GDD 规则二)的**非法流**上分歧 ——
> 而该流下写者行为本身未定义 ⇒ 分歧面不在契约内。
> **测试面同批**(`case_close_precondition_test.cs`,33 → 35 测):
> ① B-1 回归哨兵 `test_isClosed_revisitNewCase_notClosed`(含**反向对照**:首案自身须仍判已结,防「一律 false」假修);
> ② **端到端组装用例** `test_e2e_revisitClosePath_deriveBothFlagsFromStreams` —— 补补评 **A-3**:
> 此前全文件**无一条**把**两条流**(病例流 + 病史流)接进 `Decide`,这正是 B-1 在测试面**结构性隐形**的根因;
> ③ 解除四处**硬编码状态标志**(`isClosed:false` ×3 + `isOpen:true` ×1)改为**由流派生** —— A-3/A-6 的假绿土壤。
> **证伪链(双信号一致,承 sprint-11 DoD「改回旧实现即转红」)**:
> - **修复前**(旧实现 + 新哨兵):退出码 **2** · `total=34 passed=33 failed=1` —— 唯一红 = 哨兵(`Expected: False / But was: True`),
>   即 **B-1 先被现场证伪、后修**;
> - **修复后**:退出码 **0** · `total=35 passed=35 failed=0`(`unity/Logs/s11_s8_after.txt`);
> - **改回旧实现**:退出码 **2** · `28/35 passed, failed=7`(`unity/Logs/s11_s8_revert.xml`)—— 红的恰为**两类**:
>   **B-1 粒度 4 条**(其中 `test_ac3721_edge_revisitWithNewTreatmentInWindow_allows` 由 `CanClose` 退化为
>   **`RejectAlreadyClosed`** = 「**复诊案结不掉**」这一生产症状的**本体**;另 `test_isClosed_revisitNewCase_notClosed` /
>   `test_e2e_revisitClosePath_…` / `test_ac3721_revisitBackdoor_…`)+ **判序 3 条**(`test_ac3703_alreadyClosed_…` /
>   `test_ac3703_edge_sameTick_…` / `test_fullFlow_open_treat_check_close_idempotent`,由 `RejectAlreadyClosed` → `RejectNotOpen`);
> - **全量无回归**:EditMode `total=3330 passed=3284 failed=0 inconclusive=0 skipped=46`(退出码 0)· PlayMode
>   `total=186 passed=186 failed=0`(**逐位等于 M2 基线** `story009_review_playmode.xml`)。
>   较 10-14 EditMode 基线 +4 测,**逐名 diff 确认零条消失**,增量为「2 条本 story 新增 + 2 条**已提交**的
>   `ExamineDriverTest` 多病人隔离用例(`47e0fc4`)」。
> **AC-37-03 不整条转判**:其**流层半边**(幂等判据)已由流派生夹具覆盖;而「**不产生新事件**」半边
> **仍无载体**(写者未落,承 §Out of Scope 延后登记)⇒ 只覆盖到 `Decide` 的返回码,未断言事件数 = 0。
> ✅ **B-1 结案。** 本 story **维持 `In Review`** —— ② 快照半场(写者未落)· ③ AC-37-18 恒真化(A-4)· ⑤
> `TreatmentKinds` 收 `InjuryOnset`(**已排 S11-10**;枚举权威已于 2026-10-15 落 `adr-008` §四)+ ADVISORY
> A-1(`Set` 为可变 `public static readonly HashSet`)· A-5(副作用序列无判据)未清。
> ⚠️ **A-1 与 S11-10 同址**:两者都改 `TreatmentKinds` 那几行 ⇒ S11-10 应**顺批**把 `Set` 收为不可写出口
> (`private` + `Contains`,或暴露 `IReadOnlySet`),否则该确定性地雷会被下一个人原地绕过。
> ✅ **已随 S11-10 顺批闭合**(见下)。

> 🟢 **2026-10-15 S11-10 修复落盘 —— 承本登记块 ⑤ B5 + ADVISORY A-1**
> **修复证据原件**:`production/qa/evidence/fix-s11-10-case-system-story-003-b5-2026-10-15.md`
> **改动**(`unity/Assets/Sim/CaseCloseDecider.cs` · `TreatmentKinds`):
> ① **撤 `InjuryOnset`**(25 的战斗伤害结算 ⇒ 「病人挨了打」被计入「已被处置」,玩家把病人打到即可
> 满足结案前置,**正面击穿** `case-system.md:567`/`:839`「玩家空手连关三例 ⇒ **不可能**」);
> ② **补 `CareApplied`**(8 的护理动作,病史流)—— 权威枚举第 3 员,原实现**漏收**,取舍方向与语义**完全相反**;
> ③ **顺批 A-1**:原 `public static readonly HashSet<EventKind> Set` ⇒ 改 `private` 真源 + `Contains` 出口 +
> 新增 `MembersSnapshot()`(**返独立副本**)。原形态 `readonly` 只锁**引用**,内容**全仓可写** ⇒
> 任一处 `Set.Add(...)` 即**静默扩集**,破坏 ADR-005 的确定性纪律。
> **权威对齐(闭环纪律四处已核)**:`adr-008` §四 `:229` · `case-system.md` 规则六 `:255` ·
> 规则八 `:283` · F-37.2 `:671`/`:674`/`:686`/`:709-736` —— **四处口径一致**,代码现已等于该枚举。
> **测试面**(37 → 39):① 撤 `test_treatmentKinds_containsInjuryOnset` —— 该测**反向断言了缺陷**
> (`Contains(InjuryOnset) is True`),是 B5 逃过测试面的**直接原因**;② 新增 `containsCareApplied` ·
> `excludesInjuryOnset` · `excludesEmergencyAttempt`(守住原有的**正确排除**,不回归);
> ③ **双向穷尽** `equalsAuthorityExactly` —— 期望值**逐字引自 `adr-008` §四**(不引生产默认值,S11-10 AC),
> 且**恰等**判据正是单向 `Contains` **抓不到**「偷偷多收一员」的那半边;
> ④ `snapshotIsIsolatedFromSource` —— 污染快照后真源须纹丝不动(A-1 的经验判据)。
> **证伪链(双信号一致)**:
> - **修复前**(旧成员表 + 新哨兵):退出码 **2** · `37 测 35/2 红` —— 恰为 `containsCareApplied` 与
>   `excludesInjuryOnset`(而 `excludesEmergencyAttempt` **通过** ⇒ 原有正确排除未动);
> - **修复后**:退出码 **0** · `39 测 39/0`;
> - **回退成员表即转红**:退出码 **2** · `39 测 35/**4 红**`(`Logs/s11_s10_revert.txt`)——
>   含 `equalsAuthorityExactly` 打印 `实际 = {EmergencyTreatmentApplied, DrugTreatmentApplied, **InjuryOnset**}`;
> - **全量无回归**:EditMode `total=3334 passed=3288 failed=0 inconclusive=0 skipped=46`(退出码 0;较 S11-8 后
>   **+5 新增 / −1 移除**(移除的恰是那条反向断言缺陷的旧测),**逐名 diff 零意外消失**)· PlayMode
>   `186/186/0`(**逐位等于 M2 基线**)。
> ✅ **B5 结案 · A-1 结案。** 本 story **维持 `In Review`** —— 补评 4 类中余 **② 快照半场**(写者未落,
> 无载体可测)· **③ AC-37-18 恒真化**(A-4)未清;ADVISORY A-5(副作用序列无判据)亦未清。
> ⚠️ **登记一处结构缺口**:B5 是 **BLOCKING 级**判定,却**不对应任何 `AC-37-xx`** —— GDD 36 条 AC
> **无一条枚举处置集**,故该缺陷的守卫**只存在于测试件 + ADR 闭环纪律**里,而不在 AC 面上。
> 是否需要为「处置集 == 权威枚举」补一条显式 AC(或将其挂到支柱四不变量名下),**归用户裁**;
> 本轮**不自行加 AC**(AC 面归 GDD)。

---

## Dependencies

- Depends on: Story 001(载荷)、002(开案);9/10/11/25 处置事件在病史流(既有 Kind,任一在场即可测;缺则 fixture 直写流)
- Unlocks: Story 004(CaseClosed 是同源检测唯一输入)、Story 006(结案态重放)

---

## Completion Notes

*(留空 — story 关闭时回填)*
