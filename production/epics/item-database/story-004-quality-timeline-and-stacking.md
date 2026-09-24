# Story 004: F4 堆叠重量与 F5 品级时间轴

> **Epic**: 物品与配方数据库
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-24

## Context

**GDD**: `design/gdd/item-database.md`
**Requirement**: TR-itemdb-010(药物 potency / half_life 字段定义 · partial,与 Story 002 共担 —— 字段定形归 002,本故事覆盖 F5 施加与数组长度/地板校验)· TR-itemdb-015(可感知底线 vs 9 噪声带对齐)◆
◆ = `status: no-adr-by-design`,归属件 = GDD 自身;永不因补 ADR 转 ✅。
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-006: 定点域边界数据契约
**ADR Decision Summary**: 数据文件里 `Offset` / `τ_half` / `axis_offset_by_quality[]` 写整数字面量或 `分子/分母`,导入期一次性转 Q16.16;全部舍入 `ROUND_HALF_AWAY_FROM_ZERO` 整数域内完成;`weight` / `stack_max` 是 `int` 计数不是 `Fix`。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: ADR-006 Engine Knowledge Risk MEDIUM(不依赖 post-cutoff API)。

**Control Manifest Rules (this layer)**:
- Required: 全部模拟数学在整数定点域;数据文件 Fix 字段导入期一次性转 Q16.16;`weight` / `stack_max` 是 `int` 计数
- Forbidden: float 中转;`weight` 走 FixParse
- Guardrail: 不得替用户拍数值 —— PERCEPTIBLE_FLOOR / MAX_QUALITY 等只登记旋钮与范围,测试参数化注入、不断言具体定值

---

## Acceptance Criteria

*From GDD `design/gdd/item-database.md`, scoped to this story:*

- [ ] **AC-21a-32**: 两件同 item_key 不同 quality 的实例尝试堆叠 ⇒ 不合并(StackKey = (item_key, quality))
- [ ] **AC-21a-33**: 堆叠到 stack_max 后继续加入 ⇒ 溢出到新实例,总量守恒
- [ ] **AC-21a-36**: axis_offset_by_quality[] 与 quality_axis 施加于 F5 ⇒ Axis_effective = Axis_base + axis_offset_by_quality[quality − 1],且为 Fix(Q16.16 整数域,无浮点中间量);P0 下 quality_axis 必须 = half_life(否则构建期拒 —— 交叉 AC-60)
- [ ] **AC-21a-37**: axis_offset_by_quality[] 至少一档非零,且每个非零档 |offset| ≥ 可感知地板(> 9 的病史噪声带,D-21-24)。「全零 ⇒ 通过」不是合格结果
- [ ] **AC-21a-38**: 任意品级计算 F5 ⇒ 仅 quality_axis 所指的那一条轴按档偏移,其余三条时间轴(onset/peak/elimination)逐位不变(P0 下其余三轴无落点;21a 不拥有 polarity/tau_half 语义)
- [ ] **AC-21a-38b**: 任意合法 drug_profile 校验 ⇒ Axis_base + min(axis_offset_by_quality) > 0 —— 否则 9 的衰减用 half_life 作除数会除零/反向衰减(域钳制)
- [ ] **AC-21a-50**: drug_profile.axis_offset_by_quality[] 长度 ≠ MAX_QUALITY ⇒ 构建期硬失败
- [ ] **AC-21a-50b**: gather_profile.quality_character[] 长度 ≠ MAX_QUALITY ⇒ 构建期硬失败(非空时)
- [ ] **AC-21a-60**: drug_profile.quality_axis 在 P0 期取值 ∉ {half_life} ⇒ 构建期硬失败(D-21-23 收窄落盘)
- [ ] **AC-21a-61**: axis_offset_by_quality[] 任一非零档 |offset| < 可感知地板 ⇒ 构建期硬失败(D-21-24 第二半;地板数值待与 9 的噪声带宽一起定)
- [ ] **AC-21a-62**: drug_quality_character[] 非空且长度 ≠ MAX_QUALITY ⇒ 构建期硬失败(成药侧,与 AC-50b 同型)
- [ ] **AC-21a-64**: 配方与实例的最大 weight / 最大 stack_max / 最大 MAX_QUALITY 同时取满,求 Σ(w × InstanceWeight) ⇒ 不溢出 int64(守恒律整数域求值的上界前提)

---

## Implementation Notes

*Derived from ADR-006 §Decision:*

- 数据文件里 `Offset` / `τ_half` / `axis_offset_by_quality[]` 写**整数字面量或 `分子/分母`**,**导入期**一次性转 Q16.16 —— ADR-006 §Decision 一
- 全部舍入用 `ROUND_HALF_AWAY_FROM_ZERO`,**在整数域内完成** —— ADR-006 §Decision 三(AC-64 / F5 中间量不得经浮点)
- `weight` / `stack_max` **是 `int` 计数,不是 `Fix`**(D-21-17)—— ADR-006 §Decision 一(AC-64 的 Σ(w × InstanceWeight) 里 weight 是 int)
- 执法体统一形态 = **构建期断言**(AC-37/38b/50/50b/60/61/62/64 全部显式 `throw`,非 `Debug.Assert`)—— manifest 元规则
- GDD §Formulas F5(`:688-748`)是本故事直接规格:P0 `quality_axis = half_life`,地板断言、域钳制、数组长度逐条照 GDD;数值(PERCEPTIBLE_FLOOR / MAX_QUALITY / 各档 offset)归用户数值轮,测试以常量表注入为参
- D-21-34 张力注记(11 侧 `≥ MIN_USABLE_HALF_LIFE` vs 本侧 `> 0`)—— 测试**不得就地收紧**为 ≥MIN,只按 GDD 现文 `> 0` 断言并注记 D-21-34 open

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: drug_profile / gather_profile 字段类型定形(本故事消费字段,不建 schema)
- Story 005: 守恒律聚合式与极值式门(AC-8/39/40/56/65)
- Story 010: 容器 children 闭包与容器守恒(AC-34/58)—— 本故事 AC-33 只管溢出新实例的 qty 守恒
- Story 012: 品级的拟物呈现(数字/刻度禁令走 42)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-21a-32**: 两件同 item_key 不同 quality 的实例尝试堆叠 ⇒ 不合并(StackKey = (item_key, quality))。
  - Given: 实例 A(item_key=K, quality=q1)、B(K, q2), q1≠q2, 均未达 stack_max。
  - When: 堆叠判定。
  - Then: 不合并,保持两实例;同 K 同 q 且未达上限 ⇒ 可合并。
  - Edge cases: q1=1,q2=MAX_QUALITY;容器实例(qty/quality 恒 1)不参与堆叠;stack_max=1 永不合并。
  - Negative fixture: 无。

- **AC-21a-33**: 堆叠到 stack_max 后继续加入 ⇒ 溢出到新实例,总量守恒(见 AC-34 计数)。
  - Given: 堆 S 当前 qty=c, stack_max=M,加入 Δqty。
  - When: 执行堆叠。
  - Then: c+Δqty > M 时生成新实例,cap 后溢出部 qty = c+Δqty−M(逐次可多段溢出);Σqty 全容器守恒。
  - Edge cases: 恰好 c+Δqty=M(不溢出);c=M 满堆再加(全量新实例);Δqty 巨大(多段溢出);stack_max=1(全走新实例)。
  - Negative fixture: 无。

- **AC-21a-36**: axis_offset_by_quality[] 与 quality_axis 施加于 F5 ⇒ Axis_effective = Axis_base + axis_offset_by_quality[quality − 1],且为 Fix(Q16.16 整数域,无浮点中间量);P0 下 quality_axis 必须 = half_life(否则构建期拒 —— 交叉 AC-60)。
  - Given: 合法 drug_profile,quality_axis=half_life(P0);quality ∈ [1, MAX_QUALITY]。
  - When: 计算 F5。
  - Then: Axis_effective(Fix raw)= Axis_base(raw) + offsets[quality−1](raw),全程整数加法,无浮点中间量;结果即进入 9 事件流的值。
  - Edge cases: quality=1(index 0)、quality=MAX_QUALITY(index len−1);负 offset;quality 越界读防护(长度由 AC-50 保证);Axis_base 极小 + 大负 offset(域钳制归 AC-38b)。
  - Negative fixture: 无(正向;拒收类归 AC-60 夹具)。

- **AC-21a-37**: axis_offset_by_quality[] 至少一档非零,且每个非零档 |offset| ≥ 可感知地板(> 9 的病史噪声带,D-21-24)。「全零 ⇒ 通过」不是合格结果。
  - Given: 常量表 axis_offset_by_quality[] 全长;PERCEPTIBLE_FLOOR(值待用户与 9 的噪声带宽一起定,§Tuning Knobs 跨系统常量)。
  - When: 构建期校验。
  - Then: 存在至少一档 offset ≠ 0,否则硬失败;∀k: offset[k]≠0 ⇒ |offset[k]| ≥ PERCEPTIBLE_FLOOR,否则硬失败。
  - Edge cases: 恰一档非零且 =FLOOR(过);|offset| = FLOOR − 1 个最小单位(拒);全部非零档恰在地板上(过);地板数值未定时 —— 测试以常量表注入的 FLOOR 为参,不断言具体数(冻结令)。
  - Negative fixture: 全零场景 = `invalid_offset_floor.json` 同族(GDD 为 AC-61 命名;全零变体同文件子记录,不新增名)。

- **AC-21a-38**: 任意品级计算 F5 ⇒ 仅 quality_axis 所指的那一条轴按档偏移,其余三条时间轴(onset/peak/elimination)逐位不变(P0 下其余三轴无落点;21a 不拥有 polarity/tau_half 语义)。
  - Given: drug_profile 四条时间轴字段齐全(Fix);quality_axis=half_life;quality 遍历全档。
  - When: 逐档计算 F5。
  - Then: half_life 生效值随档变化;onset、peak、elimination 的 Fix raw 与 Axis_base 逐位相等(全程不变);本式不读写 polarity(结构事实)。
  - Edge cases: quality=1 与 MAX 两极;offset 表含负档;P1a 后换轴场景(P0 仅断言 half_life 为唯一作用轴,交叉 AC-60)。
  - Negative fixture: 无。

- **AC-21a-38b**: 任意合法 drug_profile 校验 ⇒ Axis_base + min(axis_offset_by_quality) > 0 —— 否则 9 的衰减用 half_life 作除数会除零/反向衰减(域钳制)。
  - Given: 合法 drug_profile;offset 表取各档值。
  - When: 构建期域钳制断言。
  - Then: Axis_base + 全档最小 offset > 0 才通过;=0 或 <0 硬失败。
  - Edge cases: 和恰 = 0(拒);和 = 1(过 —— 注意 D-21-34 登记的张力:11 侧按 ≥ MIN_USABLE_HALF_LIFE 写,升格属改机制须走 21a 重开流程,测试**不得就地收紧**为 ≥MIN,只按 GDD 现文 `> 0` 断言并注记 D-21-34 open);全部 offset 为正(恒过);单档大负使和为负(拒)。
  - Negative fixture: AC 表未命名(构建期断言,可由 `invalid_offset_floor.json` 家族扩展记录;不擅造文件名)。

- **AC-21a-50**: drug_profile.axis_offset_by_quality[] 长度 ≠ MAX_QUALITY ⇒ 构建期硬失败。
  - Given: offsets 长度 = MAX_QUALITY−1 / MAX_QUALITY+1 / 0。
  - When: 构建期长度校验。
  - Then: 三者均硬失败;长度 = MAX_QUALITY 通过(MAX_QUALITY 数值待用户定,以常量表实际值为参)。
  - Edge cases: 合法 drug 空数组(空数组由 D-21-6「字段必须在,P0 可空」放行 —— 长度校验仅在数组非空时生效,与 AC-62 同口径)。
  - Negative fixture: `invalid_drug_offset_len.json`

- **AC-21a-50b**: gather_profile.quality_character[] 长度 ≠ MAX_QUALITY ⇒ 构建期硬失败(非空时)。
  - Given: quality_character[] 非空且长度偏离 MAX_QUALITY。
  - When: 构建期长度校验。
  - Then: 硬失败;空数组(P0 可空,D-21-16)与恰长通过。
  - Edge cases: 长度 0(过,P0 正常态);长度 1 当 MAX_QUALITY>1(拒);原料侧路径 ≠ drug_profile(原 AC-50 指错块已拆分,勿混)。
  - Negative fixture: `invalid_gather_char_len.json`

- **AC-21a-60**: drug_profile.quality_axis 在 P0 期取值 ∉ {half_life} ⇒ 构建期硬失败(D-21-23 收窄落盘)。
  - Given: quality_axis 依次取 onset / peak / elimination(P1a 标记值)。
  - When: 构建期枚举收窄校验。
  - Then: 三者均硬失败;=half_life 通过;枚举外字面量由 AC-22 拒。
  - Edge cases: 字段为 null(P0 可空 —— 空时无 F5 作用,按 D-21-6 字段在即合法,不触发本条;实现须与「字段必须在」区分);P1a 解锁须改本校验(非本 spec 范围)。
  - Negative fixture: `invalid_axis_p0.json`

- **AC-21a-61**: axis_offset_by_quality[] 任一非零档 |offset| < 可感知地板 ⇒ 构建期硬失败(D-21-24 第二半;地板数值待与 9 的噪声带宽一起定)。
  - Given: PERCEPTIBLE_FLOOR 注入常量表;某非零档 |offset| 低于地板一个最小单位。
  - When: 构建期地板校验。
  - Then: 硬失败;=地板(过);零档豁免地板但仍受 AC-37「至少一档非零」约束。
  - Edge cases: 负 offset 取绝对值比较;仅一档违规;地板值未定时测试参数化,不断言具体数。
  - Negative fixture: `invalid_offset_floor.json`

- **AC-21a-62**: drug_quality_character[] 非空且长度 ≠ MAX_QUALITY ⇒ 构建期硬失败(成药侧,与 AC-50b 同型)。
  - Given: 成药 category=drug 且 drug_quality_character[] 非空、长度 ≠ MAX_QUALITY。
  - When: 构建期长度校验。
  - Then: 硬失败;空数组(P0 可空)与恰长通过。
  - Edge cases: 原料侧 quality_character[](AC-50b)勿混路径;长度 0;恰 MAX_QUALITY 过。
  - Negative fixture: `invalid_drug_char_len.json`

- **AC-21a-64**: 配方与实例的最大 weight / 最大 stack_max / 最大 MAX_QUALITY 同时取满,求 Σ(w × InstanceWeight) ⇒ 不溢出 int64(守恒律整数域求值的上界前提;先乘后比须先证不溢出)。
  - Given: 常量表允许的最大 weight、stack_max、MAX_QUALITY 组合(上限值 = 数值旋钮,TR-itemdb-029 归 21a 数值轮,用户自调 —— 测试以上界公式参数化,不写死数)。
  - When: 计算 Σ(weight × ItemDef.weight × Qty) 的最坏上界。
  - Then: 上界 ≤ long.MaxValue(整数域先乘后比,无溢出回绕);超声明上界的组合在构建期硬失败(夹具)。
  - Edge cases: 单条恰在上界(过)/超一档(拒);多条累加;负 weight(由 AC-15 前置拒);Q16.16 中间量不参与本式(weight 为 int 计数,D-21-17)。
  - Negative fixture: `invalid_weight_overflow.json`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/item_database/quality_timeline_stacking_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(FixParse),Story 002(drug_profile / gather_profile 字段类型)
- Unlocks: Story 005(守恒律门的溢出上界前提),Story 009(F5 结果进事件流)
