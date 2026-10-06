# Story 004: Prescribe 流程 —— 域检查、原子扣减与成长门

> **Epic**: 处方用药
> **Status**: Complete ✅ 2026-10-06
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 10h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/prescription-and-medication.md`(规则十 五步流程 Pseudocode + `dose` ≠ `portions` 解耦 · 规则五 不拦不扣 · F-11.4 恒 Applied · F-11.5 成长门 `GateHit` · §Edge Cases 库存不足/对象离场/混堆/同 tick 两剂/`K_difficulty` 缺失 · 11-DC DC-1…DC-7 运行期对应面)
**Requirement**: TR-prescription-002(11 不发明结算 —— 流程只读 21a 表)· TR-prescription-008(剂量整数档、不读严重度替玩家调剂量)· TR-prescription-011(indications/contraindications 只呈现不拦不扣)· TR-prescription-012(与 20 的原子性:先验再扣、无货不发事件)· TR-prescription-013(共享面收窄:零第二份 SkillMul/ResultMul/JudgeResult)· TR-prescription-017(熟练度 P0 出口 = 省料+解锁;省料唯一出处 = 21a EFF;30 只拥「等级→EFF」映射,BL-6 前置)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(主机唯一 Step/Append;客户端上行意图)· ADR-009 §七(意图→权威结算的一般形状;11 与 10 的区别 = 无判定步,意图即效果)· ADR-026(`SkillGrown` 落病史流、折叠豁免;**省料不放大药效**)· ADR-006(全序键;`Seq` 主机发号)· ADR-020 `AC-20-03` 先例(判据=反射断言非 grep —— 用于「11 不读病种级布尔」)
**ADR Decision Summary**: 五步 = ① 域检查(剂量合法 ∧ 换算份数有货 ∧ 对象在场)失败⇒**不进入** ② 算 F-11.1/F-11.2 + 查 `D-21-29` 换算表得 `portions` ③ 交 20 `Apply(消耗 portions)`,多份混堆 `quality` = **确定性最低档纯函数**(禁读列表序) ④ **无条件**发 `DrugTreatmentApplied` ⑤ `EmitGrowth` **须先过 `GateHit`**(= 剂量域内合法 ∧ 该药 ∈ 已解锁子集 —— **只读 11 自己持有的量,不读病人病种级布尔**,BL-3 改判后的现口径)。`K_difficulty` 无主(`OQ-11-13`)⇒ 缺入参不发成长+断言硬失败(不静默跳过)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(流程编排纯 C#;与 20/30/45 的接缝面依赖兄弟 epic)
**Engine Notes**: 对象在场判定经 9 的 `IPresenceQuery`(disease story 002 承载),**不读表现态位置**(ADR-016 §三);库存经 20 的接口(inventory-items epic),11 不自持账本。

**Control Manifest Rules (this layer)**:
- Required: 五步顺序不可调(先验再扣);无货 ⇒ 零事件零成长;恒 Applied(流程内零失败分支);同 tick 多剂各得不同 `Seq` 互不判重
- Forbidden: 运行期 clamp(超档物理不可能,限位在 42 侧 AC-11-18);读 `disease_id`/`tier_named`/`treatable_by` 布尔(BL-3 改判);`portions` 换算公式住 11(`D-21-29` 归 21a/17/18 产出侧);第二份省料系数(唯一出处 21a EFF)
- Guardrail: 非主机玩家开方 ⇒ 客户端上行、主机 Append(承 ADR-005);`K_difficulty` 缺失路径必须**响**(硬失败断言),禁静默

---

## Acceptance Criteria

*From GDD `design/gdd/prescription-and-medication.md`, scoped to this story:*

- [ ] **AC-11-04**[A] BLOCKING:库存不足整体拒绝 —— `GIVEN` 换算后 `portions` 无货,`WHEN` 请求给药,`THEN` **零事件发出、零成长发出、库存零变化**(无部分给药/无半剂);账(库存)与事件流一致性回归:拒绝路径后重放,库存读数不变
- [ ] **AC-11-05**[A] BLOCKING:给错药三效果 —— `GIVEN` 该药不在病人 `treatable_by(d)`(夹具经 9 侧注册表构造,11 读不到),`WHEN` 结算,`THEN` ① 事件**照发**(恒 Applied 无分支,与 story 003 对偶);② 11 侧**零惩罚**代码路径(反射断言:失败/减效类型不在 11 公开签名);③ **`SkillGrown` 零发出**(`GateHit` 不因「不在 treatable_by」而判 —— 该布尔 11 读不到;门的形状 = 域内合法 ∧ 已解锁)
- [ ] **AC-11-06①**[A]:禁忌不拦不扣 —— `GIVEN` `contraindications[]` 命中,`WHEN` 检索流程,`THEN` 零拦截、零减效路径(判断归 9 的和式,TR-prescription-011;本条 ② 双表极性已在 story 001 DC-4 构建期判)
- [ ] **AC-11-17**[A] BLOCKING:`dose_range` 空/缺字段 ⇒ 整剂给药 `dose := 1`、`dose_potency = drug_potency`,**零报错零 clamp**(21a D-21-6 P0 可全空 ⇒ 必须有确定读法);该路径**同时是 `GateHit` ① 的「另判」点**(域内合法性在整剂路径下的定义 = 恒真,显式测试锁定)
- [ ] **AC-11-16**[A] BLOCKING —— **NOT-RUN,双成因,禁借绿**:命中 `GateHit` ⇒ 恰一条 `SkillGrown`(经 30 `EmitGrowth` 入口);未命中 ⇒ 零条。**甲**(入口登记)✅ 已闭(`entities.yaml` 已登记 `SkillGrown`,2026-09-21 二十七批);**乙**(入参无主)`K_difficulty` 代入方未裁(`OQ-11-13`,4 已驳回移交)⇒ 缺入参路径**永不进入发成长** ⇒ 整条判据现不可执行。本 story 交付:调用点形状 + 「缺参 ⇒ 断言硬失败非静默」的机制化负测(注入空 `K_difficulty` ⇒ 响);正式对拍 BLOCKED-BY-OQ-11-13
- [ ] **AC-11-10**[A] BLOCKING(程序集面):11 引用集零自订 `SkillMul`/`ResultMul`/`JudgeResult`(承 story 003 断言,本 story 判流程侧:五步内无判定分支、无乘子消费点);对拍:11 名义路径与 10 显式路径同输入输出逐字段相等(名义 1.0 不链入载荷)
- [ ] **AC-11-08 流程侧**[A]:F5 求值只在步骤②被调用一次(每事件一次,无重算);`quality` 读取形状 = 步骤③ 的混堆最低档纯函数
- [ ] **混堆确定性**[L] BLOCKING:`portions` > 1 且实例品级不一 ⇒ `quality = min(被耗集合)` —— **同集合乱序 ⇒ 结果相同**(禁读列表序);被耗集合本身的选择亦须确定性规则(20 侧接口约定,见 impl note 3)
- [ ] **TR-prescription-012**[I] BLOCKING:原子性 —— 验货与扣减之间无事件写入;扣减失败(并发场景夹具)⇒ 零事件;两玩家同 tick 各开一方 ⇒ 各自完整五步(全序键定序,互不判重,`Seq` 各不同)
- [ ] **AC-11-15 流程半边**[I]:重放比对扩展到「载荷 + 余料」两处(等级不进载荷、只经省料改库存 —— 省料数值未定值,BL-6 登记:21a EFF 唯一出处、30 拥映射;夹具用合成 EFF 表);三格子句 NOT-RUN(BLOCKED-BY-ADR-012)
- [ ] **非主机分支**[A]:客户端上下文 ⇒ 上行意图、本地零 Append(与 emergency story 004 同构但无预表现判定 —— 11 无判定步);该子句的传输半边 BLOCKED-BY-45(网络 epic)

---

## Implementation Notes

*Derived from 规则十 pseudocode + F-11.5 + Edge Cases:*

1. `Prescribe(itemKey, dose, patientId)` 五步顺序照 GDD 写死;步骤① 的三个合取项分函数(域检查/份数有货/在场),失败原因**不回传呈现层**(零提示纪律,AC-11-12 的流程侧保障)。
2. 换算表 `D-21-29` 读 21a 的烘焙数据(「一株→几剂」字段);**该字段现无主登记**(OQ-11-10,候选 21a/17/18)⇒ 本 story 以**影子 schema** 走通(合成换算表 fixture),真实产出方落位后仅换 IDataProvider 键名 ⇒ 该负测登记 BLOCKED-BY-OQ-11-10,机制本身可绿。
3. 混堆最低档:`min(quality)` 在被耗实例集上;「被耗集合」的选择策略(先进先出?按档?)归 20 侧接口 —— 与 inventory-items epic 的 story 对齐时把「11 只声明:结果 = f(多重集),与顺序无关」写进双方 Dependencies(11 侧断言用乱序夹具)。
4. 步骤④ 在步骤③ 成功之后、且在 `EmitGrowth` 之前 —— 成长失败(缺参硬断言)**不回滚**事件(事件已进流 = 真源;成长不发是 F-11.5 记价的显式后果)。
5. `GateHit` 的「已解锁子集」读 30 的解锁查询(skill-system epic;`skill-system.md:60` 的 R-2 涟漪回写为前置);整剂路径的域内合法性单测锁定(impl note 1 另判点)。
6. 数值全合成:`DOSE_BASE`、EFF 表、换算表、解锁门槛均以 fixture 注入,机制不等数值轮。

## Out of Scope

- [Story 001–003]: 表、求值、事件构造(本 story 只编排)
- [Story 005]: 戥子输入与方笺呈现(限位/焦点/读数元件)
- 9 的离牌门与和式消费(disease epic story 004)
- 20 的扣减实现本体(inventory-items epic;本 story 消费其接口)
- 省料公式实现(唯一出处 21a EFF + 30 映射;11 零第二份)
- 超量/中毒后果(归 53,OQ-11-4)

## QA Test Cases

*Written at story creation(lean mode).*

- **无货全拒**: portions 缺 1 ⇒ 事件计数 0、成长计数 0、库存不变(AC-11-04)。
- **错药三效果**: 夹具病种不含该 action ⇒ 事件恰 1(恒 Applied)+ **成长照发**(门不读 `treatable_by`,F-11.5 BL-3 改判;2026-10-06 订正)+ 零惩罚符号(AC-11-05)。
- **整剂路径**: 空 `dose_range` 药 ⇒ `dose=1`、potency=表值、GateHit① 真(AC-11-17)。
- **混堆乱序等值**: 同多重集 6 种排列 ⇒ quality 相同、载荷相同(混堆确定性)。
- **同 tick 双玩家**: 两笔事件不同 `Seq`、全序键可判序、互不吞并(TR-prescription-012)。
- **缺参硬失败**: `K_difficulty`=null 哨兵 ⇒ 断言响 + 成长 0 + 事件不回收(AC-11-16 机制半边;正式对拍 NOT-RUN)。
- **客户端零写**: 非主机上下文 ⇒ Append 计数 0(上行 1)(传输 BLOCKED-BY-45,仅本地分支)。
- **影子换算表**: 合成 D-21-29 表跑通步骤②③,替换键名零代码改动(BLOCKED-BY-OQ-11-10 机制半边)。

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/PrescriptionMedication/prescribe_flow_test.cs` — must exist and pass(AC-11-16 正式对拍 / AC-11-15 三格 / 换算表真源 / 省料数值 / 客户端传输:五处 BLOCKED/NOT-RUN 显式列于证据文件头)
**Status**: [x] Done 2026-10-06 — 真身 `unity/Assets/Tests/EditMode/PrescriptionMedication/prescribe_flow_test.cs`(53 测,commit `db369d0`);评审原件 `production/qa/evidence/review-prescription-story-004-2026-10-06.md`

---

## Dependencies

- Depends on: Story 001/002/003(表、求值、事件构造)、inventory-items epic(20 的验货+原子扣减接口 + 混堆耗集策略对齐)、skill-system epic(解锁查询 + `EmitGrowth` 入口/`SkillGrown` Kind,承 ADR-026)、disease-simulation epic story 002(`IPresenceQuery`/`IEventSink`/`Seq`)、item-database epic(`D-21-29` 换算字段真源,OQ-11-10 待认领)、45 network epic(上行传输,与 emergency story 004 同一前置)
- Unblocks: EPIC DoD;playtest「试药枚举」观察点(F-11.5 防刷强度下降的显式记账项)

## Completion Notes
