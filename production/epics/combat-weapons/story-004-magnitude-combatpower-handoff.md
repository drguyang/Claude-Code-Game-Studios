# Story 004: magnitude 装配 F-25-2 与 CombatPower 交接

> **Epic**: 格斗与武器线
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-30
## Context

**GDD**: `design/gdd/combat-and-weapon-lines.md`(F-25-2 magnitude 式 · 规则〇 CP_MAX 派生 · PS 单位 · R11 对消)
**Requirement**: TR-combat-003(magnitude 为 Fix 域整数(PS 单位),clamp 带 [MAG_FLOOR, MAG_CAP]) · TR-combat-011(战斗效能公式定义权归 30,25 只消费其输出档) · TR-combat-012(战伤以 Σ magnitude × SCALE_d × Decay 入 9 的 F1,PS 单位使 SCALE 对消)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-026(主): 技能成长定点化;ADR-005/006: 定点域
**ADR Decision Summary**: `CombatPower = (格斗等级 × WeaponMultiplier) × (1 + 医术修正)` 由 **30 定义、以 Fix 传入 25**(单一交接点,AC-25-6-02 边界半边);25 内 `CP_MAX := SKILL_CAP(60) × max_line(WeaponMultiplier,含 P1a 线) × (1+MED_COMBAT_MOD)` 为**派生量**,AST 级「无裸字面量」断言(AC-25-2-04);`magnitude = clamp(MAG_FLOOR + base_step × CP/CP_MAX, MAG_FLOOR, MAG_CAP)` 全 Q16.16;除法中间量走 hi/lo(ADR-005 Amendment G);敌/兽 CP:=0 退化式 = `MAG_FLOOR + base_step`,**不引入第二公式**(AC-25-2-09)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 纯整数乘除(LOW 面),但 `FixDiv` 的舍入一致性(ROUND_HALF_AWAY_FROM_ZERO)与 IL2CPP 溢出 UB 面须守 ADR-006/012 纪律;跨平台逐位归黄金夹具矩阵另批,禁借绿。

**Control Manifest Rules (this layer)**:
- Required: magnitude 输出 = 定点 raw long(PS 单位);clamp 双侧显式;TRAUMA 侧的单侧 clamp 在 9(本 story 不做)
- Forbidden: `System.Int128` / `BigInteger` / 浮点;enemy_cp 第二旋钮;把 CP_MAX 写成手拍常量
- Guardrail: CP > CP_MAX 的输入(异常)被 clamp 吸收不抛运行期异常;CP=0 与敌/兽退化式逐位一致(同一代码路径)

---

## Acceptance Criteria

*From GDD `design/gdd/combat-and-weapon-lines.md`, scoped to this story:*

- [ ] AC-25-2-01:`magnitude` 按 F-25-2 逐项求值,与手工定点期望逐位相等(CP ∈ {0, 中间档, CP_MAX} 三夹具)
- [ ] AC-25-2-04:CP_MAX 为派生式 —— AST/反射级断言无裸数字字面量(改 SKILL_CAP 或任一 WeaponMultiplier,CP_MAX 随动)
- [ ] AC-25-2-09:敌/兽(actor CP:=0)退化 = `MAG_FLOOR + base_step[act]`;与玩家 CP=0 路径逐位同值(无第二公式)
- [ ] clamp 边界:MAG_FLOOR 钉底(CP=0 时若 base_step×0=0 仍 ≥ FLOOR)、MAG_CAP 钉顶(超界输入不越)
- [ ] AC-25-6-02 边界半边:`CombatPower` 只经 30→25 的**单一交接点**(Fix 参数)进入 25;25 内部无第二处读 30 等级/系数的路径(引用扫描断言)
- [ ] 舍入唯一性:`FixDiv` 结果与 ROUND_HALF_AWAY_FROM_ZERO 手算期望一致(中点夹具)

---

## Implementation Notes

*Derived from ADR-026/005/006 Implementation Guidelines:*

1. 交接点签名:`IEffectQuery.GetCombatPower(actorId) → Fix`(30 侧实现;25 只读)。技能等级、WeaponMultiplier、医术修正的组装全在 30,25 **不得**重算。
2. F-25-2 内部:`term = FixMul(base_step, FixDiv(CP, CP_MAX))`,`FixDiv` 的 128 位中间量 = hi/lo 双 ulong 带进位(禁有符号右移;承 ADR-012 F7 教训)。
3. 敌/兽的 CP:=0 在**意图注入处**定(27 的 Engage 意图无数值,CP 语义对敌不存在 ⇒ 注入 0),退化式与玩家共用同一函数 —— 代码评审核对「无 if(enemy) 分支公式」。
4. MAG_FLOOR/MAG_CAP/base_step 具体值归用户数值轮(OQ-25-7);本 story 以符号化夹具验证机制形状与门。
5. PS 单位的文档义务:magnitude 语义注释钉「Σ magnitude × SCALE_d × Decay(Δ) 在 9 F1 入口,SCALE_d 对消(R11)」—— 防实现把 25 的 magnitude 再乘一遍标度。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: 表值烘焙(base_step 解析)
- [Story 003]: 命中成立判定(magnitude 的前置)
- [Story 005]: 冷却/压制
- [Story 006]: onset 载荷写入与 F-25-8 平衡断言(CP×系数乘积式断言在 006)
- 30 侧:CombatPower 组装本体(skill-system story-004 已 Complete,消费其输出)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(三点求值)**: CP 全域
  - Given: base_step、CP_MAX 符号化夹具;CP ∈ {0, CP_MAX/2, CP_MAX}
  - When: F-25-2
  - Then: 三输出与手算定点期望逐位等;单调不减
  - Edge cases: CP>CP_MAX ⇒ 钉 MAG_CAP;负 CP 不可能(类型面 unsigned 语义或断言)
- **AC-2(CP_MAX 派生)**: 旋钮随动
  - Given: 修改某 WeaponMultiplier fixture 值
  - When: 重算 CP_MAX
  - Then: 派生式随动,无「常量没跟上」的残值;AST 扫描无裸字面量
  - Edge cases: 加入 P1a 线更高系数 → CP_MAX 抬升(公式覆盖 P1a 线的裁定)
- **AC-3(退化式同构)**: 敌伤 CP 不变性
  - Given: 同一 act 的 玩家(CP=0)与 兽(CP:=0)
  - When: 求 magnitude
  - Then: 逐位同值(断言 = AC-25-2-09)
  - Edge cases: 兽 range_override 不影响 magnitude(只影响 003 的距离项)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/Combat/combat_magnitude_f252_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(base_step)、Story 003(命中成立);30 的 `GetCombatPower` 交接点(skill-system epic ✅ Complete,本 story 消费)
- Unlocks: Story 006(onset 载荷含 magnitude)、F-25-8 平衡断言批

---

## Completion Notes

*(留空 — story 关闭时回填)*

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `MagnitudeParams` — magnitude 装配参数
- `CombatMagnitude` — magnitude 装配 F-25-2（ComputeMagnitude / ComputeDegradedMagnitude / ValidateCPMaxDerived / ValidateRounding）
- magnitude = clamp(MAG_FLOOR + base_step × CP/CP_MAX, MAG_FLOOR, MAG_CAP)
- 敌/兽退化式 = MAG_FLOOR + base_step
- 先舍入后钳制
- 测试: 8 条单元测试（全部通过）

**Deviations**: 无

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/Combat/combat_magnitude_f252_test.cs` — 8 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，无 BLOCKING 问题

**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
