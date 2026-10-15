# Sprint 11 — 判断回路前台化 + 治理收口(2026-10-14 规划)

> **规划依据**:三专家研判(技术总监 / 创意总监 / QA 负责人,2026-10-14 Opus 级并行研判)
> **头条订正**:四系统(13/8/37/11)epic 均已于 2026-10-06→10-08 story-Complete ——
> M2 README「16/31」为 2026-10-03 旧快照,实测 ≈**22/31 epic Complete**。
> **sprint-11 实为「补体验 + 治理收口」,非「照 GDD 实现新系统」。**
> **用户裁定(2026-10-14)**:范围 = 草案全收(Must×3 + Should×1 + Nice×2);
> S11-3 处置方向 = **修 8 测**(CI 门保持「退出码 = 0」严格口径)。

## Sprint Goal

把判断回路从「流里通」变成「玩家眼前通」——9 查体 revealed 接线让脉案值区运行期非空
(支柱一「判断为骨」首次可玩),同时清掉三笔承重治理债(W-1 会签 / 退出码 2 / 处方 DoD 残留)。

**补评修复批(2026-10-15 用户裁定并入)**:清 `case-system` story-001~004 补评认定的
**两处真实现缺陷 + 一处免责失效项 + 一处设计冲突**(S11-7…S11-10),外加一项零成本订正(S11-11);
**断言形态整批顺延 sprint-12**(见 DoD「顺延面显式登记」)。

## Capacity

- Total days: 8(solo)
- Buffer (20%): 1.6 天
- Available: ≈6.4 天(草案 8-11pd,超容部分自然顺延 sprint-12)
- 已用:S11-1 + S11-2 + S11-3 = **3.75 天**(三件均 done)⇒ **余量 ≈2.65 天**
- ⚠️ **既有 backlog 已超容**:S11-4(3.5)+ S11-5(1.5)+ S11-6(0.75)= 5.75 天 ⇒ 未加新活已超 **3.1 天**
- **补评修复批追加 5 项 ≈2.85 天**(S11-7…S11-11)⇒ 本轮合计超容 ≈5.95 天
- **处置(用户裁定「必须项优先」)**:S11-6(nice,0.75 天)**顺延吸收**;S11-4(should)/ S11-5(nice)保留不动;
  修复批按「真缺陷优先」切,断言形态整批顺延 sprint-12 —— **不做超额承诺**

## Tasks

### Must Have (Critical Path)

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|--------------|---------------------|
| S11-1 ✅ **DONE 2026-10-15** | **9 查体 revealed 接线 → 8 前台回路**(脉案值区运行期非空;顺手结 O-13-7 `OnExamSessionChanged` 发出侧登记) | unity-specialist + Claude | 2-3 | M2 链已闭(e2e_full_loop 绿) | 脉案值区运行期显示查体产出(`RevealedRows` 非空);`e2e_full_loop` + 五 wiring 测零回退;O-13-7 结案登记 脉案值区运行期显示查体产出(`RevealedRows` 非空);`e2e_full_loop` + 五 wiring 测零回退;O-13-7 结案登记 | **QA**:Integration 测 `examine_revealed_wiring_test`(~4-6 测)+ 人工连走(查体→显示→落笔→开方) |
| S11-2 ✅ **DONE 2026-10-15** | **W-1 会签裁决**(ScriptedChain 三案链 9+37 二选一;图样可辨识度 × 8 可读性互斥性验证) | game-designer + user | 0.5 | 无(纯决策轮) | 裁决落盘(case-system.md W-1 行 + 决策记录);「不裁不得实现联调」解除;若裁互斥 ⇒ 支柱四 P0 落点重裁 裁决落盘(case-system.md W-1 行 + 决策记录);「不裁不得实现联调」解除;若裁互斥 ⇒ 支柱四 P0 落点重裁 | **QA**:会签记录(互斥性可证伪结论;user+designer+qa-lead 三签) |
| S11-3 ✅ **DONE 2026-10-15** | **EditMode 退出码 2 处置 = 修 8 测**(8 个旧测试 `new MonoBehaviour` ⇒ 真夹具) | Claude | 0.5-1 | 无 | 全量 EditMode **退出码 = 0** 且 XML failed=0 双达标;判决口径在 sprint 文件登记(两信号不再混读) 全量 EditMode **退出码 = 0** 且 XML failed=0 双达标;判决口径在 sprint 文件登记(两信号不再混读) | **QA**:XML 实证订正 = 7 测/2 文件/8 处 new(host_authority + o6_actor_cell_payload);纯测试面修复,禁动产品代码 |

### Must Have — 补评修复批(case-system 001–004 · 2026-10-15 用户裁定并入)

> **来源**:`production/qa/evidence/review-case-system-story-00{1..4}-retro-2026-10-15.md` +
> `review-case-system-001-004-gap-audit-2026-10-15.md`(含 §七 勘误轮)。
> **净保留判定 22 条 → 去重 ≈13 类**;本批只取「**真缺陷 / 免责失效 / 设计冲突 / 零成本**」四类,
> 断言形态整批顺延 sprint-12。**修复一律归新一轮**,不得声称「原判定如此」(义务第 3 条)。

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|--------------|---------------------|
| S11-7 | ✅ **DONE 2026-10-15** —— **story-002 BLK-8:墨龄取错病人(真实现缺陷)** —— `CaseStreamQuery.LastJudgmentOf` 的 `caseId` 形参**零引用**(只取「传入列表末条判断事件」);生产唯一调用方传 `EventStream.Events` = **全部混合流** ⇒ `InkAgeTicks` 取到**别的病人**的判断事件。⚠️ **性质厘清**:不是「调用方传错」,是**接口本身失守** —— 旧契约「调用方传该案子集」**构造不出来**(`Sim` 够不着 `Sim.Codec`,ADR-025 §①)。⚠️ **处方订正**见下方订正框:`CompositionRoot` **无须改动** | Claude | 0.75 | 无 | 已交付:归属判据改**流序关联**(与既有正确范式 `OpenCaseOf` 同构);**新增 3 条判别夹具**(跨病人交错 / **同病人复诊** / 端到端墨龄)+ 改写 3 条旧夹具(原夹具**不含 `CaseOpened`** = 「已切片」形状,正是逃过测试面的直接原因);**回退旧实现即转红**:25 测 23/**2 红** + 5 测 4/**1 红**,失败消息逐字命中实害描述(「墨龄取错病人」);修复后 25/25 · 5/5;零回归 EditMode `3337/3291/0` · PlayMode `186/186/0` |
| S11-8 | ✅ **DONE 2026-10-15** —— **story-003 B-1:复诊案永远无法结案(真实现缺陷)** —— `CaseCloseQuery.IsClosed` 改按 `CaseId` **三元组**精确匹配(原 `CaseCloseDecider.cs:135` = `e.Patient.Value == caseId.Patient`,粒度从 `case_id` 退化为 `Patient`)+ **统一 `IsOpen`/`IsClosed` 输入契约**。⚠️ **本条两处计划口径已就地改判**(见下方订正框):判等路径不用 `PayloadRef` 解码,契约取「整条全序流」而非「该案子集」 | Claude | 0.75 | 无 | 流 `[Opened(100,1,0), Closed(200,1,1), Opened(300,1,2)]` 上 `IsClosed(newCaseId) == false`(**已交付**:35/35 绿;改回旧实现 ⇒ 退出码 2 · 28/35 · **7 红**,含复诊案由 `CanClose` 退化为 `RejectAlreadyClosed` 的现象本体);**同批补流层判据**(已交付:端到端组装用例 —— 此前全文件无一条把两条流接进 `Decide`);`case_close_precondition_test.cs:133` 硬编码 `isClosed:false` 的假绿解除(已交付:含 `:146`/`:162`/`:510`,共 4 处) |
| S11-9 | ✅ **DONE 2026-10-15** —— **story-004 CB-2/TB-5:AC-37-29 零覆盖 + `ChainEligible`/`ScriptedChain` 全库零命中** —— 落地 GDD F-37.1b 的**机制层**:`ScriptedChain`(**纯机制载体,不含任何具体 `patient_id`** —— 承 GDD「本 GDD 只登记机制与判定域,**不落数值**」)+ `ChainEligible.IsEligible/TryChainOf` + `Evaluate` **逐 D 二选一**定义域收窄 + `NextEmissions`(发射决策层)。⚠️ **写者仍未落**(承 2026-10-15「接受延后」裁定,`NextEmissions` 不触 `IEventSink`);⚠️ **CB-3 未触及**(归 sprint-12) | Claude | 0.75 | 无(W-1 已解除) | 已交付:二符号从**全库 `.cs` 零命中**转为**有实现 + 有测试**,测数 **23 → 30**;**反证① 停用定义域收窄 ⇒ XML `failed=3`**(`chainQuota`/`chainComplete`/`chainAndNaturalCoexist`);**反证② 停用「一次性」门 ⇒ XML `failed=1`**(`firesOnce_neverRefires`);修复后 **30/30**;零回归 EditMode **`3344/3298/0`**(较 S11-7 后 **+7**,恰为本轮新增)· PlayMode **`186/186/0`**(逐位等于 M2 基线) |
| S11-10 | ✅ **DONE 2026-10-15** —— **story-003 B5:处置集收录 `InjuryOnset`(设计冲突)** —— **撤 `InjuryOnset`**(「病人挨了打」被计入「已被处置」⇒ 玩家把病人打到即可满足结案前置,正面击穿「空手连关三例不可能」)· **补 `CareApplied`**(8 的护理动作,病史流,原**未收**,取舍方向与语义相反)· 处置集枚举**回填权威**:`adr-008` §四(**已于 2026-10-15 先行补立**)+ `case-system.md` F-37.2 `:671`/`:674`/`:686` + 规则六 `:255` + 规则八 `:283`(**闭环四处已核一致**) · **顺批 ADVISORY A-1**:`Set` 收为 `private` 真源 + `Contains` + `MembersSnapshot()`(返副本) | Claude | 0.5 | 无 | 已交付:39/39 绿;**回退成员表即转红**(退出码 2 · 39 测 **4 红**,含 `equalsAuthorityExactly` 打印出两个集合);**双向穷尽**判据 = 期望值**逐字引自 `adr-008` §四**(不引生产默认值);`EmergencyAttempt` 的**正确排除**保持(ADR-009 Amendment I) |
| S11-11 | ✅ **DONE 2026-10-15** —— **story-001 B-3:测试头 AC 号一号两义(零成本)** —— `case_identity_routing_test.cs` 自填 `AC-37-03/04/05/06`,与 GDD 权威编号**指向完全不同的需求**(GDD:`03` = 已结案再结幂等拒收 · `04` = 未勾「已处置」拒绝 · `05` = 判断记录落病例流 · `06` = 迁移重放一致),且已被姊妹测试占用并声称覆盖;story 那 4 条 bullet **本就无编号**,测试头是自行编号。⚠️ **范围就地订正**(见下方订正框):retro 只点名**测试头 `:5-8`**,实测误编号有**三层 25 处** | Claude | 0.1 | 无 | 已交付:全文件去号(头 4 + 体注释 14 + **方法名 10**),保留 `AC-37-01/02`(与 GDD 一致)与 `AC-37-14`(合法);`13/13 绿 · 退出码 0 · error CS = 0`,**测数 13 → 13**;判据 = 同号前缀下方法数 `test_ac3703` **7→2** / `ac3704` **4→2** / `ac3705` **5→3** / `ac3706` **1→0**,修复前 7 个方法同挂 `AC-37-03` 而 5 个与该 AC 无关 ⇒ 重复计数逐字成立 |

> ⚠️ **S11-8 计划口径就地订正(2026-10-15 执行期 · 两处,均已报用户并获批)**
> **① 「经 `PayloadRef` 解码 `CaseClosedPayload.CaseId` 判等」= 结构上不可行。** 该建议同时出现在
> 补评件 §二 与本 story 实现注释 `CaseCloseDecider.cs:129`(作者自承「实际生产代码应通过 PayloadRef 解码」),
> 但 `PayloadCodec` 住 **`Sim.Codec`**,而 `CaseCloseDecider` 住 **`Sim`** —— `Sim` 引用集**恰 =
> {BCL, `Sim.Contracts`}**(ADR-025 §① / b2 门),`PayloadRef` 只有 `BlobId/Offset/Length`,**物理上够不着**。
> ⇒ 改走 **流序扫描关联**(与既有正确范式 `CaseStreamQuery.OpenCaseOf` 同口径)。
> **代价已登记在实现注释**:该口径下 `CaseClosed` 的归属是**推断**而非**读取**;与载荷解码**在合法流上等价**,
> 仅在违反「同一病人同时至多一个开案」(GDD 规则二)的**非法流**上分歧 —— 该流下写者行为本身未定义。
> **② 契约取「整条全序流」,不取「该案子集」。** 原计划写「统一为『该案子集』」;但两函数**签名都收整条
> `caseEvents`**,而 `Sim` 侧**没有**「按 case_id 切片」的合法手段(同 ① 的引用集约束)——写者/调用方
> 也无从构造该子集。⇒ 统一口径改取**「入参 = 病例流全序前缀」**,由同一扫描派生两值,结构上不可再分叉。
> **两处订正均属「计划写了一个做不到的形状」,非范围缩减**;story-003 的 🟢 登记块有完整记录。
> 📄 **修复证据原件** = `production/qa/evidence/fix-s11-8-case-system-story-003-b1-2026-10-15.md`
> (自洽可复核;因 `unity/Logs/` 被 gitignore,日志不可充当交付证据)。

> ⚠️ **S11-11 计划口径就地订正(2026-10-15 执行期 · 已报用户并获批「全文件去号」)**
> **retro B-3 只点名「测试头 `:5-8`」,实测该误编号在文件内有三层、共 25 处**:
> ① 测试头 4 处 · ② 文件体注释 14 处 · ③ **方法名 10 处**(`test_ac3703_a1_…` 等)。
> **只改测试头等于没改** —— 按 AC 号做的覆盖矩阵会扫**文件任意位置**(含方法名),重复计数照旧产生。
> ⇒ 按用户裁定取 **①+②+③ 全去**,保留 `AC-37-01/02`(与 GDD 一致)与 `AC-37-14`(GDD `:1110`,本文件是其载体半边)。
> **改名安全性 = 实测**:全仓 grep 确认被改名方法**零外部引用**;UTF 按 `[Test]` 特性发现,`-testFilter` 走类全名。
> ⚠️ 另有一处**计划文档自身的引用偏差**:S11-11 行原写 GDD 权威编号在 `case-system.md:1036-1051`
> —— 实测该区间是**「信息边界」节**,AC 表在 **`:1075-1092`**。已就地订正,登记于此备查。
> 📄 **修复证据原件** = `production/qa/evidence/fix-s11-11-case-system-story-001-b3-2026-10-15.md`。

> ⚠️ **S11-7 计划口径就地订正(2026-10-15 执行期 · 已报用户)**
> S11-7 行原写「`CompositionRoot.cs:245-246` **改传该案子集**(现传 `stream.Events` = 全局混合流)」
> —— **该处方基于错误前提**。实测:① 传整条流**本就是正确用法**,错在**被调用方**没用 `caseId`;
> ② 「该案子集」**构造不出来** —— `SimEvent.Payload` 是 `PayloadRef`,解码归 `Sim.Codec`,
> 而 `Sim` 引用集恰 = {BCL, `Sim.Contracts`}(ADR-025 §① / b2 门)⇒ 装配层要切片只能**自己解码载荷**,
> 即把 37 的查询语义复制进装配层 = **更差的架构**。
> ⇒ **修复落在 `LastJudgmentOf` 内部,调用点(含 `CompositionRoot`)一字未改**;
> 传全局流**从此成为正确行为**。**属「计划了一个做不到、也不需要做的形状」,非范围缩减。**
> ⚠️ **同族纪律(本 sprint 第二次)**:S11-8 的两处订正也是「计划写了一个**做不到的形状**」——
> 根因同一条 = **`Sim` 侧的引用集约束**(够不着 `Sim.Codec`)在校验处方时未被前置检查。
> 📄 **修复证据原件** = `production/qa/evidence/fix-s11-7-case-system-story-002-blk8-2026-10-15.md`。

> ⚠️ **S11-9 语义厘清 + 两项裁定(2026-10-15 执行期 · 已报用户并获批)**
> **① 一处语义厘清(非判定推翻)**:补评件 TB-4/CB-1 记「发射语义无生产载体」—— **观察成立,指向须收正**。
> `Evaluate` 返回**前缀属性**(达标后第 4/5/6 例仍 `Fired=true`)—— **这本身不是缺陷**:
> AC-37-07/08 测的就是该层,现有 `test_ac3709` 断言「第 4 例仍触发」是**对的**。
> 缺的是「**本 tick 真正应发射几条**」(AC-37-09/29 的「不重发」属该层)⇒ 准确表述是「**缺一层**」,不是「`Evaluate` 有问题」。
> **② 用户裁定(两问同批)**:㈠「一次性」的载体 = **新增纯函数决策入口**(`NextEmissions`)——
> 若让夹具自己写 `if (!IsFired(...))`,那**正是 TB-4 所斥的「未写、未测的调用方契约」的复制** ⇒ 判据不可证伪;
> ㈡ 链内**声明序不消费** —— GDD 同句「触发仍走 F-37.1 的**同一算式**……**不需要第二套公式**」,而该算式按全序键定序
> ⇒ 「有序」只作内容语义(叙事先后 甲→乙→丙),以 `test_ac3729_declarationOrder_notConsumed` 钉死。
> **③ ⚠️ 证伪轮抓出本件自身的一条假绿夹具(已修正 · 须登记)**:`test_ac3729_chainAndNaturalCoexist_perDisease`
> **首版**里 D=0 的**链外病例数 = 0** ⇒ 收窄与否结果相同 ⇒ **对「收窄」变异毫无判别力**(首轮反证①只红 2 条才暴露)。
> 已补入链外病例修正,反证①恰红 3 条。**它若不经证伪轮,会以「绿」留下却什么都没测** —— 与补评件 **TA-6** 元规则同型。
> **④ 边界(防误读)**:写者**仍未落** —— `NextEmissions` **不触 `IEventSink`/不 `Append`/不发号 `Seq`**,
> 本件**不得**被引为「写者已落」;**CB-3 未触及**(归 sprint-12);**脚本链内容数据仍不存在**
> (`patient_id` 绑定归内容轮 + ADR-014 烘焙 —— GDD 明文的归属划分,**不是缺口**)。
> 📄 **修复证据原件** = `production/qa/evidence/fix-s11-9-case-system-story-004-cb2-tb5-2026-10-15.md`。

**本批不取(整批顺延 sprint-12,须在四件 story 内显式登记)**:
story-001 B-1(AC-37-06 判据**算术上不可证伪**,须退设计重述)· B-2(kindgen A2 空跑)· B-4(kindgen 借绿);
story-002 的 5 条断言形态(BLK-2/3/5/9 + BLK-1 恒真半边);
story-003 B2(快照半场 —— **写者未落,无载体可测**,见 DoD 写者延后条)· B3(AC-37-18 恒真夹具,须澄清「病人死亡可否表态」);
story-004 的其余 7 条(TB-1 定序判别力 / TB-2 成员冻结 / TB-3 真空夹具 · CB-1/CB-3 实现侧 · 及 `TA-6` 元规则「定序类 AC 夹具须至少一处 输入序 ≠ 期望序」)。

### Should Have

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|--------------|---------------------|
| S11-4 ✅ **DONE 2026-10-15** | **prescription DoD 残留**(AC-11-22 7a 白名单本体 + DC-2 `action_id` master 登记(OQ-11-2) + DC-6 `NOISE_BAND_9` 复评——9 侧常量 10-07 已立,OQ-11-13 10-06 已裁,前置已解锁) | Claude | 3-4 | S11-3 顺(同仓测试基线) | prescription EPIC §Epic Status 三项逐项闭合或显式结转;若动 7a 须带 case↔7a 折叠语义集成夹具(TD 风险 1 缓解) prescription EPIC §Epic Status 三项逐项闭合或显式结转;若动 7a 须带 case↔7a 折叠语义集成夹具(TD 风险 1 缓解) | **QA**:EditMode 断言 ×3(AC-11-22 IL 扫描/DC-2 闭集 fail-loud/DC-6 引用链闭环) |

> **✅ S11-4 完成订正框(2026-10-15)** —— 证据原件:`production/qa/evidence/fix-s11-4-prescription-dod-residuals-2026-10-15.md`
> **① 任务书前提被推翻(如实登记)**:S11-4 标 3.5–4 天、措辞为「**DoD 残留**」暗示需实现。
> 勘察证明 **DC-2 / DC-6 的实现侧早在 2026-10-07(`2a7764f`)已闭环**,残留**仅**在登记散文 ——
> 四处**同文件内自相矛盾**可观测(`EPIC.md:105` vs `:128` · `PrescriptionActionIdRegistry.cs:4` vs `:168` ·
> `dose_potency_test.cs:9` vs `entities.yaml:1484` · GDD `:763` vs `:783`)。**实际工作量远低于估算。**
> **② 三项判决**:DC-2 → **登记回刷后闭合** · DC-6 → **登记回刷后闭合**(带真残余)· AC-11-22 半边② → **落判据后闭合**。
> **③ AC-11-22 半边② 是「载体识别」不是「AC 收窄」**:AC 原文写「**7a 的序列化器**两个 Kind 均在**白名单**内」,
> 而 7a **从未有过**独立的「白名单**表**」(`ADR-010` / `persistence-service.md` 全文无) ——
> 序列化器的白名单在实现里**天然是** `EventKind` 35 支闭集 ∩ `PayloadCodec`/`PayloadEncoder` 分派表。
> **AC 文本一字未动**,故**不越** `EPIC.md:109` 的「本 epic 不自行改 AC 文本」边界。**该读法待 producer/TD 复议。**
> **④ ⚠️ 本件未动 7a** ⇒ S11-4 AC 的「若动 7a 须带 case↔7a 折叠集成夹具(TD 风险 1 缓解)」**不触发**;
> **不得**把本件读成「TD 风险 1 已缓解」(见 sprint §Risks 该行)。
> **⑤ 可证伪**:反证(停用 `PayloadCodec.cs:59` 的 `EmergencyTreatmentApplied` case)⇒ 退出码 **2** · XML **`failed=2`**,
> 恰红两条新测且失败消息**精确点名**被移除的 Kind ⇒ 判别力确认(非假绿夹具)。还原经 `grep` + `git status` 三重复核。
> **⑥ 零回归**:全量 EditMode **`3346 / 3300 / 0`**(较 S11-9 后 **+2 passed**,恰本轮新增)· `error CS` = 0 · 无 asmdef 改动。
> **⑦ 真残余(未消解,禁借绿)**:DC-6 对当前数据集**零求值**(`salicylic_acid.dose_range = null`)·
> BL-7(21a `drug_potency` 域未声明)· ~~AC-11-22 半边甲(文本背离,归 producer/TD)~~ ⇒ **见下 ⑧** ·
> **EPIC 仍 `5/5 Complete` 但 DoD 未达**(本件不改变该状态)。

> **✅ S11-4 尾巴 B 结项框(2026-10-15 同日续办)** —— 裁定记录:`production/qa/evidence/td-ruling-ac11-22-narrowing-2026-10-15.md`
> **① 上半(producer / TD 裁半边甲)—— 已闭,零代码**。TD 裁 **(甲) 收窄 AC 文本**;
> 核心理由 = 载荷 `DrugTreatmentAppliedPayload` 是 `readonly struct` + **唯一 ctor** ⇒ **任何解码器都必须调用它**
> ⇒ AC 原文「构造点仅存在于 11」对**任何合法实现恒假** —— 属**「判据对合法输入类误判」**(与 BL-7 同族),**不是实现缺陷**。
> **四件同批订正**:GDD `AC-11-22` 表行 + 规则三 BL-4 注 · `tr-registry.yaml:TR-prescription-018` ·
> `traceability-index.md` · 门 `PrescriptionWriterGates.cs`(文件头 / `DecoderCtorSites` 注 / `RunAll` §⑤ 摘要)。
> **口径三改**:① 「反射 / 引用集断言」⇒「**IL / metadata 扫描**」(反射看不见 `newobj`,引用集只到程序集粒度);
> ② 「构造点」⇒「**写入路径**构造点」;③ 解码豁免**具名**为 `Sim.Codec.PayloadCodec`。
> **producer 复核(同轮)**:⚙ **排期纠正** —— 半边甲实为 **≈0.25 天登记活**,**不是** S11-4 那种 3.5–4 天量级;
> ⚙ **`Manifest Version` 不触发**(实测 `control-manifest.md` 对「构造点」零命中,`TR-018` 的 `adr: null`)⇒ 207 份 story 不受影响;
> ⚙ **story 状态不回退**(TD 裁甲 ⇒ 代码没错、文本错 ⇒ `story-003` 维持 `Complete`,`:36` 的 `[x]` **不算借绿**)。
> **② EPIC 状态表述已修正**:`EPIC.md:6` 补入「**story 层 5/5 Complete 与 epic DoD 未达是两件事**」口径
> (同 `emergency-procedures/EPIC.md` §未闭项登记先例),消同文件自相矛盾。
> **③ 计划件就地加注**:`qa-plan-sprint-11:62` 按先例(`e67b33b` / `b1f32ca`)**原文保留 + 加订正注**,
> 指明其**两半皆不成立**(①「白名单表」不存在;②「无白名单外**字段**」是**字段级**,与 AC 的 **Kind 级**非同一命题)。
> **④ 过程面观察已沉淀**(两条,均**不做 hook** —— 机械 hook 判不可靠 = 假门):
> ⑦-a `/sprint-plan` skill Phase 1 增硬前置(纳入 sprint 的 epic 须**重读实况**,不得沿用旧快照);
> ⑦-b `review-workflow.md` 增**义务 5**(回刷收口判据:改完对同一关键词**全仓 grep**,逐条判「动 / 不动」并落纸)。
> **⑤ 仍未闭(结构性归属外部件,非本 epic 可闭)**:DC-6 零求值(21a 内容轮)· BL-7(21a)·
> 影子装配注入半边 · tag 序判据 · AC-11-15 三格矩阵(ADR-012)· AC-11-16 正式对拍(BLOCKED-BY-45)· AC-11-12/13/21 人工走查(S11-6)。
> **epic DoD 第 2 / 第 4 条仍不绿**,且**不因本轮改变**。

### Nice to Have

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|--------------|---------------------|
| S11-5 ✅ **DONE 2026-10-15** | **53 medical-consequences 起首 story**(支柱四载体,机械自足 P0 水龙头,纯 C# + SplitMix64,Engine Risk LOW) | Claude | 1-2 | 9+37 已 Complete ✓ | story-001 入向闭集与跨流聚合**已实现**(`Inputs53` 五路闭集 + 全序聚合 + 20 条测);story 状态转 In Review | **QA**:EditMode 20 条(6 条 AC 全覆盖)· 反证 ×2 · 零回归 `3366/3320/0` |
| S11-6 | **11 人工走查 AC-11-12/13/21**(前置:先修 Story009WalkthroughDriver `passed` 汇总 bug) | user + Claude | 0.5-1 | Driver bug 修复 | 三 AC 走查证据落 playtests/;Driver 汇总判定修复登记 三 AC 走查证据落 playtests/;Driver 汇总判定修复登记 | **QA**:人工走查三签(主创/医学/音频);Driver passed 汇总修后自证 |

> **✅ S11-5 完成订正框(2026-10-15)** —— 证据原件:`production/qa/evidence/s11-5-medcons-story-001-aggregation-2026-10-15.md`
> **① 任务书前提被推翻(同 S11-4 的同类失效,如实登记)**:S11-5 逐字写「**story 立件** + 判据面成型」,
> 而 4 个 story 件**早在 2026-09-28 由 `/create-stories` 立好**(全 `Ready`,`:6` 逐字 `Status: Ready`)——
> **立件从未缺席,缺席的是实现**:`WriterExistenceGate.cs:117` 具名登记「53 医疗后果与责任**未实现**」。
> ⇒ 本轮是**真活**(需实现,不是立件),工作量与 3h 估算同量级。
> ⚠️ **过程面观察**:本 sprint 的 should-have(S11-4)与 nice-to-have(S11-5)**两项任务书前提均已过时**,
> 两项描述均写于 2026-10-14,而相关产物形成于 10-03 / 10-07 / 09-28 ⇒ **sprint 计划时未回扫 epic 现状**。
> **② 交付**:`Sim/Consequences/ConsequenceInputAggregator.cs`(生产件 · 门 A 内零引擎 API)+
> `Tests/EditMode/MedConsequences/medcons_aggregation_test.cs`(20 条测)—— **两个新目录,均为本 story 首件**。
> **③ 五处偏离批准草案(均以卡面/AC 为准,如实登记)**:结构名 `PerCaseInputs` → **`Inputs53`**
> (卡面 AC-1 **逐字**点名 `Inputs53`,而 §Implementation Notes 1 同一物写作 `PerCaseInputs` ——
> **同文件内异名**,按「AC 是判据」取 AC 名) · 测试路径改从卡面 §Test Evidence 点名值 ·
> 第五路类型 `IDiseaseProjection` → `DiseaseProjectionSnapshot`(接口引用逐组重复无语义) ·
> AC-5 改用 `Assembly.GetReferencedAssemblies()` 元数据(不读文件系统,且查**实际**引用集更严) ·
> 黑名单扫描面 = `DaYiJingCheng.Sim.Consequences` 命名空间(**53 无独立程序集**;
> **刻意不扫全 `Sim`** —— 会把 9/25/37 的字段纳入 53 判据 = 越权)。
> **④ 两处张力不自行裁定(承批准口径)**:① 处置 Kind 集 ⇒ `careKinds` **显式参数**,
> 测试**只**钉「参数被尊重」(窄集/宽集/null 三输入 ⇒ 三结果),**不**钉该集该是什么;
> ② 处置窗口下界 ⇒ 取 `Tick ≤ 结案 tick`(无下界),测试钉**当下口径**并注明待裁。**两者归 53 的 GDD 轮。**
> **⑤ 可证伪(反证 ×2,双信号口径)**:① 停用 `ordered.Sort` ⇒ 退出码 **2** · `failed=2`,
> 恰红**两条排序专向探针**(置换不变 + 同 tick Seq 定序),消息逐字指向排序语义;
> ② 注入第六路字段 ⇒ 退出码 **2** · `failed=1`,恰红五路闭集测且消息**逐字点名** `SixthLaneProbe`。
> 两次还原经 `grep` 残留 0 + 关键符号完好 + `diff` identical + md5 与备份一致 **四重复核**。
> **⑥ 零回归**:全量 EditMode **`3366 / 3320 / 0 / 46`**(较 S11-4 后 **+20 passed**,恰本轮新增)·
> `error CS` = 0 · **无 asmdef 改动**(门 A / b2 门 / ADR-025 §① 全成立)。
> **⑦ 真残余(未消解,禁借绿)**:story-001 件 6 条 AC 复选框**未勾**(归 `/story-done`)·
> `WriterExistenceGate.cs:117` 的「53 未实现」**不应因本件改动** —— 本 story 只做**聚合器**(输入侧),
> **不产出** `ConsequenceResolved`(归 story-003)· P/C 的**内容**(病名/置信度/处置适切性)归 story-002,
> 本件**零载荷解码**(门 A 够不着 `Sim.Codec`)· 张力 ①② 归 53 的 GDD 轮。

## Carryover from Previous Sprint

| Task | Reason | New Estimate |
|------|--------|--------------|
| 用户数值轮(8 门槛族 / 37 半径/就诊触发 / 11 EFF 逐档 / 13 分档) | 承「数值用户自己调」——四系统旋钮批成一轮,不逐一阻塞 | 未估(用户轮) |
| story-008 资产半(止血/CPR 素材 + 手势 VFX) | BLOCKED-BY batch-03 外部链,维持 P2 | — |
| 六屏 UX 走查余五屏 | 桌面集中轮缓办(AC-42-F1 NOT-RUN 已登记);脉案屏随 S11-1 顺带 | — |
| B-6-2 焦点可见性(独立 story) | sprint-08 唯一 open,未入本轮排期 | — |

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| case↔7a 折叠语义集成未验证(TD 风险 1,LIVE) | 中 | 存档正确性/高水位重构 | S11-4 动 7a 必带集成夹具(未结案存档折叠 + 三流并集 max 验证) |
| UI Toolkit 焦点桥假设 6 半可信 | 中 | S11-6 走查面失真 | 走查前小 spike;失败则登记另裁 |
| case-system 001-004 无评审原件 | 低 | 治理缺口(不可追补条款) | QA 计划前置批核:核当年有无 BLOCKING 判定,有则补做评审,无则登记结论 |
| **case-system 001–004 顺延面未清(≈15 条断言形态)** | **高** | 四件**不得转 `Complete`**,EPIC 维持 4/6 In Review;若 sprint-12 再度被挤占,治理债跨 sprint 累积 | 本 sprint 只切「真缺陷优先」四项;顺延面**逐条带状态+归属**登记进四件 story 与 EPIC;若 sprint-12 无法承接,回到用户裁定(不自行降级为「接受」) |
| **S11-7 / S11-8 触及生产代码**(前几批治理轮均为纯测试面) | 中 | 可能破 M2 已闭链(e2e_full_loop / vertical_slice / 五 wiring) | 两处修改**须先跑** M2 链基线;`Sim` 侧改动过门 A + ADR-029 + kindgen 三关;禁动 `Sim` 引用集 |
| W-1 裁决出「互斥」 | 低 | 支柱四 P0 落点重裁(设计层) | S11-2 预案:互斥则升级用户裁定,不擅自改设计框架 |

## Dependencies on External Factors

- 无外部阻塞(ADR-023 S1-S7 与 45 联机夹具经 grep 证实对四系统 P0 机械面**零阻塞**——TD 研判 C 节)
- batch-03 外部素材链(story-008 资产半)持续 BLOCKED,不在本轮解除面

## Definition of Done for this Sprint

- [x] All Must Have tasks completed
- [ ] All tasks pass acceptance criteria —— ⚠️ **条件**(签核件 §五 #2):S11-1 自动面 COVERED(**换轨载体**),
  **人工连走半 `NOT RUN`**(C2);S11-6 已按用户裁定顺延并登记
- [x] QA plan exists (`production/qa/qa-plan-sprint-11.md`)——**开工前必落**
- [x] All Logic/Integration stories have passing unit/integration tests
- [ ] Smoke check passed (`/smoke-check sprint`)—— ⚠️ **条件**(签核件 §五 #5):
  `smoke-2026-10-15.md` 已落,Verdict = **PASS WITH WARNINGS(仅自动面)**;
  人工 Batch 1–3 **全 `NOT RUN`** ⇒ **不构成「冒烟通过」**
- [x] QA sign-off report: APPROVED or APPROVED WITH CONDITIONS (`/team-qa sprint`)
  —— ✅ `production/qa/evidence/qa-signoff-sprint-11-2026-10-15.md` = **APPROVED WITH CONDITIONS**
- [x] No S1 or S2 bugs in delivered features(范围内 —— 见签核件 §三末「依据范围声明」)
- [ ] Design documents updated for any deviations —— ⚠️ **基本**(签核件 §五 #8):已落 W-1 ·
  `adr-008` §四 · AC-11-22 四件同批 · EPIC 状态口径;**未落** S11-1 换轨与前提过时登记
  (⇒ C7,本轮已在**台账**补登;⚠️ 是否足以闭本行**不自转绿**,归下一轮复核)
- [x] Code reviewed and merged
- [x] **M2 链零回退**(sprint-11 特有):e2e_full_loop + vertical_slice + 五 wiring 测全绿——四系统正压在已闭 M2 Exit 链上,破链 = 重开 M2
- [ ] **NOT-RUN 显式结转**(sprint-11 特有)—— ⚠️ **部分**(签核件 §五 #11):多数已逐项登记;
  **S11-1 人工半 / S11-6 顺延**的载体登记不闭合(F2/F5)⇒ 承 C4(✅ 本轮已落)/ C7(✅ 本轮已补)
- [x] **退出码口径结清**(sprint-11 特有):EditMode 判决以「退出码 + XML failed」双信号一致为准,S11-3 后两信号不得再混读
- [x] **补评修复批五项闭合并带判据**(sprint-11 特有):S11-7…S11-11 各自的 AC 须有**可证伪断言**——
  「改完即算」不成立;S11-7/S11-8 的修复须给出**改回旧实现即转红**的反证
- [x] **写者延后正式登记**(sprint-11 特有 · 用户裁定 2026-10-15):story-003 / story-004 的 §Out of Scope
  增列「37 结案写者 / `PatternRecognized` 写者延后」并**同步订正 AC-37-08/09 的证据面** ——
  现 story 自认含 `Append` 步却无写者(`WriterExistenceGate.cs:87-88` 具名豁免),
  是 `Complete` 里的**悬空**,不是可静默的欠账
- [x] **顺延面显式登记**(sprint-11 特有):001 的 B-1/B-2/B-4 · 002 的 5 条断言形态 · 003 的 B2/B3 ·
  004 的其余 7 条 —— 整批**带状态 + 归属**结转 sprint-12;四件 **维持 `In Review`**,**禁借绿**

### DoD 终局(2026-10-15 · 依 `qa-signoff-sprint-11-2026-10-15.md` §五)

> **11 ✅ · 4 ⚠️ 条件(DoD 2 / 5 / 8 / 11)· 0 ❌** ⇒ **sprint-11 = 条件性通过**
> (`APPROVED WITH CONDITIONS`)。
>
> ⚠️ **与签核件 §五尾「✅ 10」的差 = 该处计数笔误** —— 其**表内**实为 **11 条 ✅ + 4 条 ⚠️ = 15**。
> 本清单按**表内逐条判定**打勾,已在该分母上登记。
>
> **4 条条件全部是「需真人 / 需外部件」,无一为开发方未尽** —— 这正是本 sprint 判
> `WITH CONDITIONS` 而非 `NOT APPROVED` 的理由(签核件 §六)。
>
> **C4 已闭**:S11-6 的顺延已于 2026-10-15 落 `sprint-status.yaml`(`blocker` 写明「用户裁定
> 『必须项优先』· 本文 `:27`」+ owner 补齐)。⚠️ 签核件把 C4 记为「仅登记于 sprint 文件」,
> 是**读到了改前的版本**;现该条件**已闭**。
> **C7 已补登**:S11-1 的三处登记欠账就地补登,见下框。
> ⚠️ **本轮不自转绿** —— 补登的是**台账登记面**;DoD 8 / 11 的 ⚠️ 仍按签核当时保留,
> 是否足以闭合该两条归**下一轮复核**裁。**「补登」≠「判定改写」**(承 `review-workflow.md` 义务 5)。

### ⚠️ S11-1 的三处登记欠账(2026-10-15 补登 · 承签核件 F1 / F2 / C7)

> 由 sprint-11 QA 签核发现,如实补登。**不改变** S11-1 的 AC 覆盖判定。
>
> **① 计划点名的载体不存在(换轨)** —— QA 计划 `qa-plan-sprint-11-2026-10-14.md:17` 点名
> `unity/Assets/Tests/PlayMode/Boot/examine_revealed_wiring_test.cs`;**该文件与目录均不存在**。
> 实际走的是**换轨载体**:EditMode `Boot/examine_driver_test.cs`(13 测)
> + PlayMode `SkeuomorphicUI/e2e_full_loop_test.cs`(真 `CompositionRoot.Assemble()`,
> `:428` 断言 `RevealedRows.Count > 0`)。⇒ 自动面**有覆盖**,但**计划路径零命中**须留痕。
>
> **取证(可复核 · 承 `review-workflow.md` 义务 4「全域否定须给命令与结果」)**:
> ```bash
> grep -rln "examine_revealed_wiring\|ExamineRevealedWiring" unity/Assets/Tests/ tests/
> # ⇒ 零命中(退出码 1);两棵树均已扫
> ls unity/Assets/Tests/PlayMode/Boot/
> # ⇒ No such file or directory —— 该目录不存在
> ```
>
> **② 无独立证据原件** —— `production/qa/evidence/` 下**无** S11-1/2/3 的专属原件
> (S11-4 / S11-5 有),三行亦无完成订正框。本轮已补三行 `✅ DONE 2026-10-15` 标记。
>
> **③ 任务书前提过时 —— 本 sprint 的第三次同型失效** —— S11-1 本 sprint 的实际交付**仅 2 测 + 1 登记**;
> 底层 revealed 接线早在 **sprint-06**(`e9af520`)已完成。这与 **S11-4**(判「DoD 残留需实现」,实为**登记回刷**)·
> **S11-5**(判「story 立件」,实为**实现**)是**同一种失效**:**sprint 计划时未回扫 epic 现状**。
> ⇒ 该失效模式已于本轮沉淀为 **`/sprint-plan` skill Phase 1 第 5 步(硬前置)**;
> 本行是该失效的**第三个实例**的登记(前两例见 `s11-5-…-2026-10-15.md` §2.1)。

### ⚠️ 2026-10-15 范围变更:case-system 001–004 断言形态批**就地闭合**(不再顺延 sprint-12)

> **用户裁定(2026-10-15)**:**「在 sprint-11 完成这些,不带入下个 sprint」**。
> ⇒ **推翻** DoD「顺延面显式登记」条所依据的顺延安排;该条的 `[x]` 随之失效(见下方订正)。
> 四项子裁定(同轮,**均为扩围**):① **解冻两个写者**(003 ② 快照写者 / 004 CB-1 `PatternRecognized` 写者)
> 在 sprint-11 内实现;② **授权收窄 GDD 文本**(AC-37-06);③ **裁「病人死亡不进事件流」**+ 改 AC-37-18 判据;
> ④ **授权改 GDD 两处**(F-37.1b 加注 + `:738`/`:718` 行号漂移订正)。

#### 分母钉死:未闭 = **20 条**(经清点件复核,非 DoD 原写的 17)

⚠️ **三处既有数字互不一致,均以此处为准**:DoD `:251` 写「17」· 风险表 `:215` 写「≈15」·
各 story 自写数字(`004` 写 6 却只列 5 个名 —— 漏 `CB-3`;`002` 把 `BLK-9` 计两次)。

**A 组 —— 可在 sprint-11 内闭合(14 条)**

| # | story | ID | 缺陷 | 性质 |
|---|---|---|---|---|
| A1 | 001 | `B-2` | kindgen A2 内层 `foreach` 空跑(0 次迭代),且扫 registry 散文而非 struct 字段 | 归 kindgen |
| A2 | 001 | `B-4` | 测试从不运行 kindgen(删掉它 13 测照样全绿)⇒ 生成物自述充当证据 | 归 kindgen |
| A3 | 002 | `BLK-2` | 同 tick 边界 `caseIdA != caseIdB` 由测试字面量构造 ⇒ 恒真 | 断言形态 |
| A4 | 002 | `BLK-1` 恒真半边 | `decide(A)==decide(B)` 因 `Decide` 不读 `sourceKind` 而构造上恒真 | 断言形态 |
| A5 | 002 | `BLK-3` | 自造 `J(c)`(`.Where().OrderBy().Last()`)不调生产 `LastJudgmentOf` | 断言形态 |
| A6 | 002 | `BLK-5` | 两条测同一事实(重复断言)+ S-8.3 未验 | 断言形态 |
| A7 | 002 | `BLK-9` | 药材短缺负断言未按 AC 契约构造(无点名 fixture) | 断言形态 |
| A8 | 002 | `BLK-10` | 证据面错配(AC-37-05/23 是 `[I]` 集成,却指 EditMode 单测) | 断言形态 |
| A9 | 004 | `TB-1` | 夹具期望序 ≡ 插入序 ⇒ 删掉 `PatternDetector` 的 Sort 断言仍绿 | 断言形态 |
| A10 | 004 | `TB-2` | AC-37-09/10 只断 `MemberSet.Count` ⇒ 成员身份不可证伪 | 断言形态 |
| A11 | 004 | `TB-3` | AC-37-12 真空夹具(内层 `foreach` 零迭代) | 断言形态 |
| A12 | 004 | `CB-3` | `memberSet` 按 `Dictionary` 迭代序拼装(**违 AC-37-27**)+ 扁平结构 | **产品代码** |
| A13 | 004 | `TA-6` | 全文件输入序 == 期望序 ⇒ 定序类断言系统性丧失判别力 | 断言形态 |
| A14 | 003 | `A-5` | 副作用序列在 37 侧无判据(⚠️ 先判是否已实质闭合 —— 与已推翻的 B4 面重叠) | 断言形态 |

**B 组 —— 原卡在裁定,现已解冻(6 条)**

| # | story | ID | 缺陷 | 裁定 |
|---|---|---|---|---|
| B1 | 003 | `② B2` | 快照半场零行为覆盖(5 处反射字段存在性检查 = 借绿);**生产侧无快照计算** | ① 解冻写者 |
| B2 | 004 | `CB-1`/`TB-4` | `PatternRecognized` 发射语义无生产载体;`IsFired` 零生产调用者 | ① 解冻写者 |
| B3 | 001 | `B-1` | `AC-37-06` 判据算术上不可证伪;其 GDD 原文要的是「存档→主机迁移重放」⇒ 依赖 7a+45 | ② 授权收窄 GDD |
| B4 | 003 | `③ B3` | `AC-37-18` 夹具不含死亡输入 ⇒ 恒真 | ③ 裁「不进流」+ 改判据 |
| B5 | 004 | `④` | F-37.1b「有序」张力加注 + `:738`/`:718` 行号漂移 | ④ 授权改 GDD ✅ **已落** |
| B6 | 004 | `⑥` | F-37.1b 注块「先触发歧义」仍写「归 37 实现轮」 | ④ 授权改 GDD |

#### 执行分批

| 批 | 内容 | 状态 |
|---|---|---|
| **1** | 两个写者:`CaseCloseWriter` + `PatternRecognitionWriter`(含测试 **12+9=21 测** + 撤 `WriterExistenceGate.cs:87-88` 豁免) | ✅ 2026-10-15 |
| **2** | A 组 14 条 + B 组 6 条(**共 20 条,全闭**) | ✅ 2026-10-15 |
| **3** | GDD 三处(AC-37-06 收窄 · F-37.1b 加注 · 行号漂移) | ✅ **3/3** |
| **4** | 登记回刷 + 门验证(EditMode/PlayMode 全量)+ 四件 story 状态复核 | ✅ 本批(证据原件 3 件同批) |
| **5** | **postfix 复评残余闭合**(用户裁定「本轮跑一次新评审」所触发)—— story-001 **B-1 测试半边** + story-003 **AC-37-18 另两条恒真测试** | ✅ 本批(证据原件 `fix-s11-16-…`) |

**批 1 说明**:写者**类 + 测试**已落;**生产触发点接线未做,也不该做** —— ① 结案是玩家显式动作,
「结案页宿主」意图源**尚不存在**;② 解码 delegate 的真实现要落 `Sim.Codec`(跨装配面);
③ `EventStream` **无订阅机制**(而 `PatternRecognized` 要求「`CaseClosed` 入流后同 tick 触发」)。
先例 `CaseOpenWriter` 同样未接线而已被 `WriterExistenceGate` 接受为「有写者」。触发点归装配轮。

**批 2 说明 —— 20 条逐条落点**:

- **A 组 14**:A1/A2(kindgen B-2/B-4 → **CI 为唯一载体**,测试内复刻已清)· A3–A8(story-002 六条)
  · A9–A13(story-004 五条)· A14(story-003 **A-5 结案** —— 判据在 8 / 53 / 20 三处,**不补测试**)
- **B 组 6**:B1(003 ②B2 快照 → 由 `CaseCloseWriter` 自算 `treated` 窗口证据闭合)·
  B2(004 CB-1/TB-4 → `PatternRecognitionWriter` 落地)· B3(001 AC-37-06 → GDD 收窄)·
  **B4(003 ③B3 AC-37-18 → 本批就地闭合**:GDD 加注 + 夹具判别化,**变异探针已证可捕获**)·
  B5 / B6(GDD 两处,批 3)
- ⚠️ **两条「部分闭合」如实登记(禁借绿)**,详见 story-002 §Test Evidence ——
  **BLK-5** 的 `freehand_text = ''` 半边(sim 层结构上不可验,该字段住 blob 变长段)·
  **BLK-10** 的「无第二副本」半边(**语义结构性满足,无点名断言**)

**批 4 门验证(2026-10-15 实测 · 双信号一致)**:

| 平台 | 现值 | 基线 | Δ |
|---|---|---|---|
| EditMode | `3387/3341/0/46` 退出码 **0** | `3366/3320/0/46` | **+21 = 两个新写者测试件**(12+9) |
| PlayMode | `186/186/0` 退出码 **0** | `186/186/0` | **逐位一致** |

两日志 `error CS` = **0**。

> ⚠️ **本轮另回退一处越权改动(如实登记)**:004/003 轮曾自行给 `SortBySaltedKey` 加 **ordinal 次级键**,
> 自陈「GDD F-37.1 **逐字**要求」。**引据经复核不成立** —— GDD `:828-830` 只写「按 `salted_key` 的
> 整数升序依次写入」,全文无 ordinal 次级键;且该改动**无夹具可证伪**、**超出 20 条清单**。
> 按用户裁定 2026-10-15 **回退**(`PatternDetector.cs` 该函数与 HEAD 逐字一致)。
> **回退后双平台测数逐字不变** —— 实证该改动在全部夹具上**行为不可观测**。

> **DoD 订正(承本次范围变更)**:`- [x] **顺延面显式登记**` 条的**依据已灭** ——
> 该条要求的是「带状态 + 归属**结转 sprint-12**」,而本轮裁定改为**就地闭合**。
> ⇒ 该行**改判**:由「顺延登记已落」改为「**闭合进度**」,在批 4 结束时重新判定。
> ⚠️ **四件 story 在 20 条全闭前维持 `In Review`,禁借绿** —— 但**闭合的理由**已从
> 「顺延」变为「收口进行中」。
> ✅ **2026-10-15 结清**:四件已转 `Complete` —— 出 `In Review` 的路径由用户裁定 =
> 「**本轮跑一次新评审(当下状态)**」。回件:001 **不通过**(残余 1)· 002/003/004 **通过**;
> 001 的残余经批 5 修复后另交**第三次窄口径评审**(**通过**)⇒ 四件全部转 `Complete`。

### 批 5 —— postfix 复评残余(2026-10-15)

**触发**:用户裁定「**本轮跑一次新评审(当下状态)**」⇒ 四件 story 各跑一次新评审。
**回件**:001 **门不通过**(残余 1 条)· 002 / 003 / 004 **门通过**(003 另抓一条 ADVISORY)。
⇒ **评审查出的两条残余必须就地闭合**(否则即「带入 sprint-12」,违背本轮裁定)。

| 残余 | 出处 | 处置 |
|---|---|---|
| story-001 **B-1 测试半边** —— `test_highWaterMark_sentinelMinusOne_noPollution` 逐字自造 `Where().Select().Max()`,生产 `ReconstructPatientHighWater` **零调用** | `review-…story-001-postfix` | **改打生产载体**(3 处可证伪断言:真 id 累加 / 全哨兵不回退 / 溢出硬失败) |
| story-003 **AC-37-18 另两条测试恒真** —— `emptyDiseaseName` / `pureInjuryCase_emptyDiseaseSet` 只喂字面量给 `Decide`(其形参**无** `disease_set`) | `review-…story-003-postfix` | **两条删除**;空病种集真覆盖**迁写者层**(真装配 + 载荷回读);「病名」半边登记 **NOT-RUN** |

**批 5 门验证(2026-10-15 实测 · 双信号一致)**:

| 平台 | 现值 | 批 4 值 | Δ |
|---|---|---|---|
| EditMode | `3386/3340/0/46` 退出码 **0** | `3387/3341/0/46` | **−1**(删 2 条恒真 + 增 1 条真装配) |
| PlayMode | `186/186/0` 退出码 **0** | `186/186/0` | 逐位一致 |

> ⚠️ **一次中间红已如实登记**:本批 EditMode 首跑 `failed=1`,唯一失败 = 本批**新增的那条**测试
> (首稿误用 `EventKind.InjuryOnset` 当处置事件,而它**不在** `TreatmentKinds` 权威集
> {`EmergencyTreatmentApplied`, `DrugTreatmentApplied`, `CareApplied`})⇒ 结案被 `RejectNoTreatment` 拒。
> 改 `CareApplied` 后复跑转绿。**其余 3339 条同期全绿 ⇒ 非既有回归。**
> 该红顺带**实证了处置集的真源边界**。

> ⚠️ **story-001 的 B-1 闭合本身另交第三次窄口径评审**(`review-…story-001-b1-postfix`)——
> 按 `review-workflow.md:43`「**评审不可事后追补**」,批 5 **不自行宣告其通过**。
>
> ⚠️ **本批同时订正 5 处陈旧登记**(如实,防「原件变强了、登记没跟上」):
> story-001 `:92`(13→9 测)· `:99`(误记 AC 号)· story-003 `:214`(「③ 全部已清」→ PARTIALLY-CLOSED)·
> story-004 `:116`(30→34)· story-004 测试件 `:165`(「写者未落」已陈旧)。

## 三专家研判归档

- 技术总监:四系统全 Complete 的头条订正 + 轨 A 治理/轨 B 53 双轨建议 + 风险 top3(7a 折叠/W-1/处方 DoD 歧义)
- 创意总监:8 查体前台回路为体验主轴(`RevealedRows` 恒空 = 支柱一不可玩)+ W-1 互斥性承重债 + 体验债分级进
- QA 负责人:测试债清单(退出码 2/Driver bug/case 评审原件核查)+ story-009 当日模式 + 整 sprint 一份 QA plan + DoD 三条特有判据
