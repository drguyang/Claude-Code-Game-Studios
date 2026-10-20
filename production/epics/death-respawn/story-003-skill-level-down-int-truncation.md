# Story 003: 技能掉级 F-29-1(整数截断)与调 30

> **Epic**: 死亡与复活
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28
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

- [ ] AC-29-04 [A]:静态检查掉级路径 —— 零浮点字面量、零 `Fix.Div`、零 `floor_Fix(Mul)`,用 int 截断除法(门 A 内)
- [ ] AC-29-05 [A] ⚠️ BLOCKED-BY-9(触发链):`Level ∈ {1,10,20,40,60}` → `{0,9,19,38,57}`(F-29-1 表逐位;触发输入以 PlayerDied 桩先行,联调不记绿)
- [ ] AC-29-14 [A]:掉级公式唯一出处 = 30 §4.3;29 零重复实现(检索断言);本 AC 不含豁免断言(豁免未裁)
- [ ] AC-29-16 [A] ⚠️ **BLOCKED-BY-7a 折叠实现**(原「BLOCKED-BY-OQ-7a-9」已于 `ADR-026 §⑦` 2026-09-23 结案):同一 `PlayerDied` + `SkillGrown` 序列两次喂入,`Level(s)@t` 逐位一致,且 29 零「掉级 Kind」Append 点 —— **裁决已定(`SkillGrown` 豁免折叠),实现(`Folded(p)` 生产谓词)未落前禁记绿**
- [ ] 判断类豁免行以「口径待裁」注释钉死在调用点,双分支 fixture 只测「全掉」与「全不掉」两极端的行为差异登记,不实现选择
- [ ] 同 tick 时序:掉级结算发生在 PlayerDied 发射之后、复活传送(story-005)之前的死亡结算段内

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
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(时机);skill-system story-005(30 §4.3 实现,已 Complete);7a 折叠面(仅 AC-29-16 联调需要 —— 裁决已定 = `ADR-026 §⑦` `SkillGrown` 豁免折叠,⚠️ **待 `Folded(p)` 生产谓词实现落地**)
- Unlocks: 与 30 的死亡-成长集成回归;story-005 复活时的等级一致性检查

---

## Completion Notes

*(留空 — story 关闭时回填)*
