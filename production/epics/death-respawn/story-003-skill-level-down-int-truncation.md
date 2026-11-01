# Story 003: 技能掉级 F-29-1(整数截断)与调 30

> **Epic**: 死亡与复活
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-11-02
> **Last Updated**: 2026-11-01
## Context

**GDD**: `design/gdd/death-and-respawn.md`(规则四 掉级 · F-29-1 · F-29-4 流重建 · EC 表判断类豁免行)
**Requirement**: TR-death-005(掉级惩罚 = fold 投影,SkillGrown 事件须参与重放折叠)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-026(主): 技能成长定点化;ADR-006: 定点域边界
**ADR Decision Summary**: **计时归 29、公式归 30** —— 29 在死亡结算的同 tick 运行期调用 30 的 §4.3 掉级(`AC-29-14` 零重复实现);掉级语义的权威式 = `Level_new(s) = (Level(s) × 19) / 20` 的 **C# int 截断除法**(B2 定点陷阱:**禁 `Fix.Div`、禁 `floor_Fix(Mul)`** —— 定点化会把 1→0 的截断语义改错);掉级 = 两流的 fold 投影(F-29-4),**不落新 Kind**;`SkillGrown` 折叠豁免(ADR-026 ⑦ 案 1)使成长行在终态折叠后仍存续。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: int 乘除纯 BCL;不触任何引擎 API。

**Control Manifest Rules (this layer)**:
- Required: 输入 = PlayerDied 时刻;调用 30 §4.3;输出经 30 的既有成长面(不绕 `SkillGrown` 语义)
- Forbidden: 浮点字面量(AC-29-04 静态检查)/ `Fix.Div` / `floor_Fix(Mul)`;29 自持一份掉级公式;29 发「掉级信号」Kind 的 Append 点(F-29-4 防第二真源)
- Guardrail: ⚠️ 判断类技能(诊断/辨证)豁免 = **口径待裁(R-4-W-6)** —— 30 §4.3 无此豁免,29 侧不得按「既有规则」实现;⚠️ AC-29-16 的折叠前置 **已裁**(`ADR-026 §⑦` 案 1),阻塞降级为**实现待落** —— 见 AC-29-16 行内注
  > ✅ **2026-11-01 R-4-W-6 结案(原文不删)**:该口径**已于 2026-09-29 由用户裁定**(GDD 规则四撤销注):**判断类豁免撤销,全部技能统一掉级** —— 勘察漏检该注,误登记为「待裁」。生产装配 `isExempt: null` **恰为裁定值,零行为改动**;本行「口径待裁」作废;两分支夹具保留为机制登记面,裁定行为钉 = `test_judgmentSkills_exemptRevoked_downgradeAppliesToDiagnosis`。

> **📌 2026-10-20 陈旧阻塞订正(S-1)**:本节原写「AC-29-16 **BLOCKED-BY-OQ-7a-9**(折叠豁免待三方裁)」
> —— **该问题已于 2026-09-23 由 `ADR-026 §⑦` 结案**。原文逐字:
> 「**裁决:折叠豁免扩一类 Kind —— `SkillGrown` 不随病人折叠被抹除。**」「⇒ `OQ-7a-9` **结案**;
> 29 的 `AC-29-16` 与 51 的成长读数**解除 `BLOCKED`**。」(案 1 采纳,案 2 / 案 3 否决)
> 该义务同时已入 `ADR-010 §三` **义务 14**。
> ⚠️ **但阻塞并未归零,只是换了性质**:实测 `unity/Assets/` 全库 **`Folded(p)` 生产谓词零命中**
> —— `case_replay_persistence_test.cs:22` 自陈「7a `Folded(p)` 生产谓词不存在(grep 零命中)⇒
> 本测只验 37 侧判据接口」。
> ⇒ **裁决面已清,实现面未落**。AC-29-16 的完整绿须等 7a 折叠谓词落地;
> **不得**再以「等待裁决」为由挂起,**也不得**因「裁决已下」就记绿。

---

## Acceptance Criteria

*From GDD `design/gdd/death-and-respawn.md`, scoped to this story:*

- [x] AC-29-04 [A]:静态检查掉级路径 —— 零浮点字面量、零 `Fix.Div`、零 `floor_Fix(Mul)`,用 int 截断除法(门 A 内)
- [x] AC-29-05 [A] ⚠️ BLOCKED-BY-9(触发链):`Level ∈ {1,10,20,40,60}` → `{0,9,19,38,57}`(F-29-1 表逐位;触发输入以 PlayerDied 桩先行,联调不记绿)
- [x] AC-29-14 [A]:掉级公式唯一出处 = 30 §4.3;29 零重复实现(检索断言);本 AC 不含豁免断言(豁免未裁)
- [x] AC-29-16 [A] ⚠️ **BLOCKED-BY-7a 折叠实现**(原「BLOCKED-BY-OQ-7a-9」已于 `ADR-026 §⑦` 2026-09-23 结案):同一 `PlayerDied` + `SkillGrown` 序列两次喂入,`Level(s)@t` 逐位一致,且 29 零「掉级 Kind」Append 点 —— **裁决已定(`SkillGrown` 豁免折叠),实现(`Folded(p)` 生产谓词)未落前禁记绿**
  > 🔁 **2026-11-01 R-2 复核注(sprint-23 取件前)**:上句的「实现未落」**已过期** —— `FoldedPredicate` 已落(`Sim/FoldedPredicate.cs`,S18-2)+ 折叠接线(位置半 + 委托半 S19-1)+ 字节半(TD-persist-03 折叠段)+ 产出面变异反证(M7),**`TD-persist-01` 已于 2026-10-24 `closed`**(结案块见债册;G-2 门行残余 = 字节半亦已兑现)。⇒ **AC-29-16 自本注起转可判**(取件轮实现时按本 AC 验收,无需再等 7a 侧任何东西)。
- [x] 判断类豁免行以「口径待裁」注释钉死在调用点,双分支 fixture 只测「全掉」与「全不掉」两极端的行为差异登记,不实现选择
- [x] 同 tick 时序:掉级结算发生在 PlayerDied 发射之后、复活传送(story-005)之前的死亡结算段内

---

## Implementation Notes

*Derived from ADR-026/006 Implementation Guidelines:*

1. 调用面:`ISkillDown.Settle(playerId, tick)`(30 实现);29 只编排时机。skill-system epic 的 `story-005 死亡掉级` 已 Complete —— 29 消费之,公式不重写。
2. `×19/20` 写成 `(level * 19) / 20`(C# int 除法向零截断,等级非负 ⇒ 等价 floor);注释钉 B2 陷阱防「顺手定点化」。
3. 重放视图:`Level(s)@t = fold(SkillGrown ⊖ down 事件语义)` —— 折叠算子归 30/7a,29 不实现 fold。
4. **折叠面(2026-10-20 订正)**:`OQ-7a-9` 已于 `ADR-026 §⑦`(2026-09-23)裁为**案 1** ——
   折叠谓词**豁免 `SkillGrown` 行**,成长事件随流永久保留(该义务 = `ADR-010 §三` **义务 14**)。
   ⇒ 本 story 的 AC-29-16 现在**只缺实现、不缺裁决**:待 7a 的 `Folded(p)` 生产谓词落地后回补联测。
   ⚠️ 实测该谓词**当前零命中**(`unity/Assets/` 全库),故 AC-29-16 维持未勾 ——
   **这是「实现待落」,不是「裁决待定」**;两者在排期上的含义不同(前者可排,后者不可排)。
5. 掉级数不呈现(story-006 AC-29-07 管 DTO);叙事上是「手生」不是「遗忘」—— 无任何「损失 X 点」文案进本层。

> 🔁 **2026-11-01 实施判定回写(S23-3 · 原注记不删)**:上列第 1 条的「消费面」在本轮
> 兑付时发现**前提与权威式冲突** —— 30 的实际产物 `DeathPenalty.Apply`(skill-system
> story-005,2026-09-27)走的是 Fix 路径,其单测钉值 {56,37,18} 与本卡 AC-29-05 要求的
> F-29-1 表 {0,9,19,38,57} **差一**(death-and-respawn GDD F-29-1 已登记为「同源同错」
> 挂账义务,并裁「30 修正前以 F-29-1 为权威」)。**取「修 30 的权威面、29 照旧消费」**,
> 不取「29 侧自持一份 int 公式」(后者违 AC-29-14 / Manifest Forbidden「29 自持一份掉级
> 公式」):`DeathPenalty.Apply` 本体改整数截断(签名去无用 `deathLoss` 形参,零生产调用点),
> 30 侧三条单测值随权威式订正(story-005 卡同日注登记方向回转;skill-system.md §4.3 落注,
> **公式行文本订正仍挂 30 下一轮**)。29 侧新增 `DeathSkillDownSettlement`(编排面:
> 派生态重 fold 取等级 → 逐技能 Apply → 经 30 既有成长面 `IGrowthEmitter` 发携带新绝对
> 等级的 SkillGrown);**判断类豁免 = R-4-W-6 口径待裁**,生产装配 `isExempt: null = 全掉`
> (与 30 §4.3 现行为一致),两极端由双分支夹具登记,不实现选择 —— 裁定落地时只在
> CompositionRoot 一处注入谓词。✅ **2026-11-01 补记**:R-4-W-6 即日结案(2026-09-29 裁定
> 撤销豁免 → 全掉;勘察漏检 GDD 撤销注,误标待裁)—— 生产装配 `isExempt: null` 即裁定值,
> **不注入谓词**;裁定行为钉已落(`test_judgmentSkills_exemptRevoked_downgradeAppliesToDiagnosis`)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: PlayerDied 发射(时机源)
- [Story 002]: 掉落(与掉级正交)
- [Story 004]: 冷却(掉级不受冷却影响 —— 冷却内根本不入结算)
- [Story 006]: 呈现
- 30 侧:公式与成长事件本体(skill-system epic);7a 侧:折叠实现

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(截断表)**: F-29-1 五档
  - Given: Level = 1/10/20/40/60
  - When: 掉级结算(桩 PlayerDied)
  - Then: 0/9/19/38/57(⚠️ 全链绿 BLOCKED-BY-9,桩面绿可先记「29 侧半边」)
  - Edge cases: Level=0 稳定(不再减);恰 <20 的等级减到 0(1→0 合法,「手生到不会」为设计语义)
- **AC-2(禁定点误用)**: 静态检查
  - Given: 掉级路径源
  - When: AST/反射扫描
  - Then: 无 float 字面量 / Fix.Div / floor_Fix(Mul) 命中
  - Edge cases: 把 `19/20` 写成 `"19/20"` Fix 字符串 = 违例(该值不是 Fix 域量)
- **AC-3(零第二真源)**: Append 检索
  - Given: 29 全代码路径
  - When: 检索 IEventSink.Append 调用
  - Then: 无任何「掉级信号」Kind;掉级只经 30 的既有事件面表达
  - Edge cases: AC-29-16 的两次喂入一致性挂 **7a 折叠谓词实现**(原挂 `OQ-7a-9`,该题已由 `ADR-026 §⑦` 结案) —— 本断言先测「无 Kind」半边

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/DeathRespawn/death_skill_down_test.cs` — must exist and pass(桩半边);AC-29-05/16 的完整绿分别 BLOCKED-BY-9 / **BLOCKED-BY-7a 折叠实现**(`OQ-7a-9` 已由 `ADR-026 §⑦` 结案,阻塞性质由「裁决待定」改为「实现待落」)
> 🔁 **2026-11-01 R-2 复核注(sprint-23 取件前)**:上行「实现待落」对 AC-29-16 **已过期** —— 折叠实现全链已落且 `TD-persist-01` closed(见 AC-29-16 的同日注)。AC-29-05 的 `BLOCKED-BY-9` 维持:根因(9 侧判死谓词 + 死亡候选产出)**已大部落地**(`EntityDeathGates` / `DeathCandidateConsumer` 生产装配齐,`TD-sim-01` closed),门行 G-1 的结案归本 epic 的 001/004 收口核。
**Status**: [x] Created & passing(全量回归 `r4w6-em.xml` @ 2026-11-01 16:53:58 覆盖)

---

## Dependencies

- Depends on: Story 001(时机);skill-system story-005(30 §4.3 实现,已 Complete);7a 折叠面(仅 AC-29-16 联调需要 —— 裁决已定 = `ADR-026 §⑦` `SkillGrown` 豁免折叠,⚠️ **待 `Folded(p)` 生产谓词实现落地**)
- Unlocks: 与 30 的死亡-成长集成回归;story-005 复活时的等级一致性检查

---

## Completion Notes

*(留空 — story 关闭时回填)*
