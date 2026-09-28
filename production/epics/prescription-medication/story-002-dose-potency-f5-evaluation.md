# Story 002: F-11.1 剂量舍入除与 F-11.2 F5 求值点

> **Epic**: 处方用药
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 8h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/prescription-and-medication.md`(规则六 F5 求值点=11 · 规则七 剂量→药效 · AC-11-11 四子句 · §F-11.1/F-11.2/F-11.4(名义路径))
**Requirement**: TR-prescription-006(11 = F5 唯一求值点,兑现 D-21-11)· TR-prescription-007(`Axis_effective ≥ MIN_USABLE_HALF_LIFE` 下界,21a 侧升格断言 = BL-1 前置)· TR-prescription-009(单次舍入 HALF_AWAY_FROM_ZERO;中间域保持 Q16.16 原始整数)· TR-prescription-010(`single_dose_max` 烘焙期派生零手填)· TR-prescription-019(可感知地板量纲一致,BL-2 前置)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-006 §三(`ROUND_HALF_AWAY_FROM_ZERO`,禁 `Math.Round` ties-to-even,禁 C# `/` 向零截断)· ADR-005 Amendment G(128 位中间结果 = 手工 hi/lo;宽度条件式见下)· ADR-012(单元级黄金夹具:F-11.1 舍入除)· ADR-014(派生常量在烘焙期产出)
**ADR Decision Summary**: F-11.1 `dose_potency = ROUND_HALF_AWAY_FROM_ZERO(drug_potency × dose / DOSE_BASE)`(先乘后除、**只舍一次**);F-11.2 `Axis_effective = Axis_base + axis_offset_by_quality[quality − 1]`(唯一求值点,`half_life := Axis_effective`);**中间积宽度条件式**(AC-11-11④):21a 声明域 ≤ 2⁴⁷ 且 dose ≤ 2¹⁶ ⇒ int64 直乘合法,**否则须 hi/lo 128 位**(BL-7:声明域未出 ⇒ 本 story 无条件走 hi/lo,承 AC-10-04a 同款「不留条件分支」纪律);`single_dose_max = max over(全部药 × dose_range.hi) of |dose_potency|` 烘焙期派生。P0 剂量 = 资源刻度非手艺。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(求值本体 LOW;IL2CPP F7 溢出 UB 前置承 ADR-012 —— 三格对拍 NOT-RUN 直至矩阵)
**Engine Notes**: 复用 disease-simulation story 001 的 `Fix`/hi-lo/舍入原语(同整数域,零重复实现 —— AC-11-02「11 不发明」同样约束计算原语层)。

**Control Manifest Rules (this layer)**:
- Required: 全式整数定点(Q16.16);舍入只在输出处一次;F5 求值点唯一(9 的 F1 消费 `half_life` 不再二次求值)
- Forbidden: 运行期 clamp `Axis_effective`(下界由 21a 构建期断言保证,BL-1 未落 ⇒ 该保证登记 NOT-RUN,**11 不重复 clamp**);浮点中间量;第二份 `Decay`/`Exp`(归 9 库)
- Guardrail: `quality_axis` P0 唯一 = `half_life`(其余轴为 P1a 预留);`drug_potency` 静态不受品级调制(D-21-22)

---

## Acceptance Criteria

*From GDD `design/gdd/prescription-and-medication.md`, scoped to this story:*

- [ ] **AC-11-08**[A] BLOCKING:F5 唯一求值点 —— `Axis_effective = Axis_base + axis_offset_by_quality[quality−1]` 只在 11 出现;反射/IL 断言:9 与 10 程序集零 `axis_offset` 消费点;`axis_offset_by_quality[]` 长度 = `MAX_QUALITY`(21a 侧)且越界访问不可能(索引域断言)
- [ ] **AC-11-11① ② ③**[L] BLOCKING:单次舍入除 —— ①中间积保持 Q16.16 原始整数;②除法唯一:`dose_potency = HALF_AWAY(drug_potency×dose/DOSE_BASE)`;③`.5` 落点夹具(合成 `(X×16384)÷65536` 型半值)⇒ 远离零舍入(8193 型),非 C# `/` 的 8192;负值域对称(−8193)
- [ ] **AC-11-11④**[L] BLOCKING(宽度子句,**NOT-RUN 半边**):中间积宽度条件式 —— BL-7(21a 未声明 `drug_potency` 取值范围)⇒ 本 story 无条件走 hi/lo 128 位;「int64 直乘合法」分支**不启用**,该分支判据 BLOCKED-BY-O-11→21a(BL-7),禁借绿
- [ ] **AC-11-09**[L] BLOCKING:`single_dose_max` 烘焙期派生 —— 对全部药 × `dose_range.hi` 求 `max|dose_potency|`,**零手填**;改任一源字段 ⇒ 派生值自动重算且哈希进 `ConfigVersion`;供 9 的 F1 clamp(`MAX_ACTIVE_DOSE × single_dose_max`)消费(消费侧回归 = disease story 004)
- [ ] **TR-prescription-007(11 侧半边)**[A]:11 侧声明 `Axis_effective ≥ MIN_USABLE_HALF_LIFE`(tick 计)为**21a 构建期断言的义务**(BL-1:该断言现仅 `>0`,升格未落)⇒ 11 不重复 clamp,机制化测试:极小 `Axis_effective` fixture ⇒ 11 照常求值不修正(证「不 clamp」),21a 侧断言列 **BLOCKED-BY-O-11→21a NOT-RUN**
- [ ] **AC-11-19**[L] **NOT-RUN(BLOCKED-BY-O-11→9)**:可感知地板 `dose_potency(d+1) − dose_potency(d) ≥ NOISE_BAND_9` —— 9 侧无该常量(BL-2);量纲一致性条款(TR-prescription-019):**不得**把 9 的 σ(Progress 域)或 21a 品级地板(tick 域)数值挪用到药效幅值域;本 story 交付比较器骨架 + 差值序列可导出(为数值轮备好判据),断言本身 NOT-RUN
- [ ] **AC-11-15**[I] 跨平台重放半边(求值侧):同 `(WorldSeed, 药, 剂, 实例集, 玩家)` ⇒ `dose_potency/half_life` 逐位;**输入集刻意不含技能等级**;三格矩阵子句 NOT-RUN(BLOCKED-BY-ADR-012),双进程 Mono 自洽先行(禁借绿)
- [ ] **D-21-22**[A]:品级**不**调制 `drug_potency`(同药同剂不同 quality ⇒ potency 恒等;quality 只经 F5 进时间轴)—— 两层调制禁止回归测

---

## Implementation Notes

*Derived from 规则六/七 + AC-11-11:*

1. `dose_potency` 求值链:`Mul(drug_potency, Fix.FromInt(dose))` → `Div(..., Fix.FromInt(DOSE_BASE))`,中间一次 `RoundFix`;舍入实现复用 story 001 of disease(不复制粘贴 —— 同程序集符号)。
2. hi/lo 无条件路径(承 AC-10-04a/ADR-005 AmG「不留条件分支」):宽度条件式作为**注释+构建期登记**存在,BL-7 解决后走 ADR 修订再评估,实现期不开分支。
3. `Axis_base` 源 = 21a `drug_profile.half_life`(基准档);偏移表按 `quality−1` 索引;两者皆烘焙输入,运行期零解析。
4. 派生常量 `single_dose_max` 落 `prescription.derived.cooked`(或并入主表头部,实现期定),阶段 2 生成;哈希入 `ConfigVersion`(联动 AC-11-09 与 ADR-010 §七)。
5. `DOSE_BASE` 值归数值轮;测试表驱动覆盖 dose ∈ {0, 1, hi, hi+1 越界(应被域检查拒,归 story 004)} 与舍入落点。

## Out of Scope

- [Story 003]: 载荷构造/落流(本 story 只产两个整数)
- [Story 004]: 域检查与原子扣减(dose ∈ 域 的判定在此)
- 9 的 F1 消费侧(离牌门/Decay,disease epic story 004)
- BL-1/BL-7 的 21a 侧实现(item-database epic 回写)

## QA Test Cases

*Written at story creation(lean mode).*

- **8193 型夹具**: 半值落点正/负各一 ⇒ 远离零(AC-11-11③);C# `/` 退化 ⇒ 红。
- **唯一求值点**: grep/IL 三层(9/10/其他)零 `axis_offset` 读;11 恰一消费点(AC-11-08)。
- **品级不碰 potency**: quality 1…MAX 扫 ⇒ `dose_potency` 恒等、`half_life` 随表变(D-21-22)。
- **派生量联动**: 改一药 `dose_range.hi` ⇒ `single_dose_max` 重算 + `ConfigVersion` 变(AC-11-09)。
- **不 clamp**: 极小 `Axis_effective` fixture ⇒ 输出原值(证 11 侧无修正路径);21a 断言缺席登记 NOT-RUN。
- **地板骨架**: 差值序列导出器对合成表可运行,断言开关 gated-off(AC-11-19 NOT-RUN 形态)。

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/PrescriptionMedication/dose_potency_test.cs` — must exist and pass(AC-11-11④ 分支判据 / AC-11-19 / AC-11-15 矩阵子句 / TR-prescription-007 的 21a 半边:四处 NOT-RUN 显式列于证据文件头)
**Status**: [ ] Created — NOT STARTED

---

## Dependencies

- Depends on: Story 001(表/cooked)、disease-simulation epic story 001(Fix/hi-lo/舍入原语)、item-database epic(21a 字段与 `MAX_QUALITY`;BL-1/BL-7 回写为判据解锁条件)
- Unlocks: Story 003(载荷两整数入项)、Story 004(域检查的换算份数与 dose 语义)、disease epic story 004(F1 clamp 消费 `single_dose_max`)

## Completion Notes
