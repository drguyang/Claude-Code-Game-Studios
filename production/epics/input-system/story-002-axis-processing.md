# Story 002: F-3.1 轴处理算术性质与装载期断言

> **Epic**: 输入与设备
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-25

## Context

**GDD**: `design/gdd/input-system.md`(§Formulas F-3.1 · §Tuning Knobs 一)
**Requirement**: ⚠️ **无专属 TR** —— 直引 GDD `AC-3-A9`(四轮新增,F-3.1 此前零 AC 覆盖)与公式 F-3.1 全文
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time;本故事判据的权威出处 = GDD AC 原文)*

**ADR Governing Implementation**: ADR-006(主): 定点域边界数据契约 · ADR-014(次): 数据管线与 JSON 解析器
**ADR Decision Summary**: ADR-006 —— 外部数据 → `Fix` 只经 `FixParse`(`Parse(string)` / `FromRatio(long,long)`),浮点字面量在 schema 层即被拒,运行期不存在 float→Fix 路径;单一舍入 `ROUND_HALF_AWAY_FROM_ZERO` 整数域内完成。ADR-014 —— 承载 `Fix` 的字段 JSON 写**字符串**(`"1/8"`),经 `FromRatio` 导入;全量校验失败 = 构建失败不是警告;装载期区间校验是常量的唯一机械门。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 纯整数/定点算术 + 装载断言,不触 post-cutoff API;MEDIUM 承 ADR-006/014 两件的 Knowledge Risk(编码器与烘焙管线须实测),本故事只消费其装载校验面。

**Control Manifest Rules (this layer)**:
- Required: 外部数据 → `Fix` 只经 `FixParse`(`FromRatio(long,long)`);浮点字面量 schema 层即拒(manifest Foundation · 定点域与边界)
- Required: 常量比值化经 ADR-014 管线 —— 本 GDD 不引入任何 `float` 字面量(用户裁定③)
- Guardrail: F-3.1 除装载期断言外**无其他机械门**(`AC-3-A9` 是它唯一的守门 —— GDD §Tuning Knobs 一)

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story:*

- [x] **AC-3-A9①(BLOCKING)**: **NaN / ∞ 与零点防护** —— `raw` 含 NaN/∞ **或** `m = 0` ⇒ 输出**恒为 `(0,0)`**(非 NaN、非异常)
- [x] **AC-3-A9②(BLOCKING)**: **满速可达** —— `m ≥ DZ_OUTER` ⇒ `t = 1` ∧ `g = 1` ⇒ 输出模长 = 1(**否定 `DZ_OUTER > 1` 型失效**)
- [x] **AC-3-A9③(BLOCKING)**: **静止归零** —— `raw = 0` ⇒ 输出恒为 `(0,0)`(**否定 `DZ_INNER < 0` 型「静止自走」**)
- [x] **AC-3-A9④(BLOCKING)**: **装载期断言** —— `0 ≤ DZ_INNER < DZ_OUTER ≤ 1` ∧ `CURVE_POW > 1/2` ∧ `CURVE_POW` 有限 —— **四组反例(越界常量)须使装载失败**(承 ADR-014)

> 分级:①②③④ 均为**算术性质** ⇒ Logic · BLOCKING(EditMode 单测);F-3.1 的**手感部分**(曲线前段过钝/过激)属 Visual/Feel ⇒ ADVISORY,**不在本 AC 内**。

---

## Implementation Notes

*Derived from GDD F-3.1 / §Tuning Knobs 一(primary spec)+ ADR-006 / ADR-014:*

- **公式照 GDD,数值待用户**:`m = ‖raw‖₂; t = clamp((m − DZ_INNER)/(DZ_OUTER − DZ_INNER), 0, 1); g = (3t² − 2t³)^CURVE_POW; out = (raw/m) × g`(当 `m > 0` ∧ 有限,否则 `(0,0)`)
- **常量比值写法**:`DZ_INNER` / `DZ_OUTER` / `CURVE_POW` 创作态写字符串(`"1/8"` / `"7/8"` / `"3/2"`),经 `FixParse.FromRatio(long,long)` 导入 —— **不引入任何 `float` 字面量**(GDD §Tuning Knobs 表头)
- **四条硬约束并列**(GDD §Tuning Knobs 一):`0 ≤ DZ_INNER` · `DZ_INNER < DZ_OUTER`(除零)· `DZ_OUTER ≤ 1`(否则满速永不触发 —— `m` 已归一,`> 1` 使 `t < 1` 恒成立)· **`CURVE_POW > 1/2`**(端点 C1 破缺,证明见 F-3.1 Ⓐ);**四条均为装载期断言**(走 ADR-014 区间校验),失败 = 硬失败非警告
- **`CURVE_POW` 有限**一并断言(拒 NaN/∞ 常量)—— 与 ① 的运行期防护是两道独立门
- **判据只断性质、零数值**(AC 原文):不写「输出 = 0.875」型期望值,只写模长/零点/失败性
- 感知域:本函数输出仍是**手感层 float**(供 1 / 2 消费),**不进流、不进 sim**;`Fix → float` 读路径许可归 1 / 2(GDD §Dependencies 五挂账),本故事只交付到归一化输出为止

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001:动作资产与单实例纪律(轴从资产读出,但资产结构归 001)
- Story 010:常驻轴读路径的零分配(GC.Alloc 判据归 E5)
- 手感调参(曲线前段钝/激)= Visual/Feel ADVISORY,数值归用户(项目铁律)
- `Fix → float` 读路径的跨系统许可:归 1 / 2 的 GDD(GDD §Dependencies 五登记)

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-3-A9①**: `raw` 含 NaN/∞ 或 `m = 0` ⇒ 输出恒 `(0,0)`。
  - Given: 常量表合法装载(`0 ≤ DZ_INNER < DZ_OUTER ≤ 1`, `CURVE_POW > 1/2`)。
  - When: 分别喂 `raw = (NaN, 0)`、`(∞, 1)`、`(1, −∞)`、`(0, 0)`。
  - Then: 每例输出恰 `(0,0)`(两分量都是 0,非 NaN);不抛异常。
  - Edge cases: 单分量 NaN `(NaN, NaN)`;`m` 极小非零但不 NaN(走正常路径,不被 ① 捕获)。
  - Negative fixture: 无(正向性质测试)。
- **AC-3-A9②**: `m ≥ DZ_OUTER` ⇒ 输出模长 = 1。
  - Given: 合法常量表。
  - When: 喂 `raw` 使 `m = DZ_OUTER`、`m = 1`、`m > 1`(越单位圆输入)。
  - Then: 每例 `t = 1` ∧ `g = 1` ∧ `‖out‖ = 1`(定点域断言,允许 ±1 ulp 级容差须在测试内明写并论证);**否定 `DZ_OUTER > 1` 失效** —— 若常量被错设为 `>1`(绕过装载断言的内存直改),此测红。
  - Edge cases: `m` 恰等 `DZ_OUTER`(边界闭侧);斜向输入(方向保持,只断模长)。
  - Negative fixture: 内存直改常量使 `DZ_OUTER = 1.01` ⇒ ② 红(证明断言链闭合)。
- **AC-3-A9③**: `raw = 0` ⇒ 输出恒 `(0,0)`。
  - Given: 合法常量表。
  - When: 喂 `raw = (0,0)`。
  - Then: 输出 `(0,0)` —— 静止不自走;**否定 `DZ_INNER < 0` 失效**(直改 `DZ_INNER = −0.1` ⇒ 静止点 `t > 0` ⇒ 此测红)。
  - Edge cases: 与 ① 的 `m = 0` 同点但独立断言(③ 断语义,① 断防护);`DZ_INNER = 0` 合法下界恰过。
  - Negative fixture: 直改 `DZ_INNER < 0` ⇒ 红。
- **AC-3-A9④**: 装载期四组反例须使装载失败。
  - Given: 四条夹具常量表:`DZ_INNER = −1` / `DZ_INNER = DZ_OUTER` / `DZ_OUTER = 1.01` / `CURVE_POW = 1/2`(及 `CURVE_POW = ∞`)。
  - When: 各自走 ADR-014 装载校验。
  - Then: 每例**装载硬失败**(显式异常/构建失败,非警告、非 clamp 后继续);合法表(`0 ≤ · < · ≤ 1`, `CURVE_POW = 3/2`)装载成功。
  - Edge cases: `DZ_OUTER = 1` 恰等上界(合法);`CURVE_POW` 恰等 `1/2`(拒)与 `1/2 + ε`(过);常量写 `float` 字面量而非字符串 ⇒ schema 层拒(交叉 ADR-006)。
  - Negative fixture: 四组反例即负例夹具(GDD AC 原文点名)。

**实现期登记(2026-09-25 code review;不改 spec,只把实际用例面与原文的出入落账)**:
- ③ Edge「`DZ_INNER = 0` 合法下界恰过」补测 `test_axisTuning_boundaryInnerZero_restStaysZero` —— spec 有、首版实现漏(review F1 / Q1 双报后补齐)。
- ④ Given 点名 `CURVE_POW = ∞` 之外,实际增夹具 `invalid_curve_pow_nan.json`(`"NaN"` 串同属「有限」断言的解析层拒)—— 忠实扩展(review F6)。
- 评审追加 2 测:`test_axisTuningLoader_divisionByZero_aggregatesWithFieldLabel`(F3:分母零聚合回归,④ 同族)· `test_simFixedPoint_fromRatio_shiftOverflow_throwsFormat`(F4:`FixParse.FromRatio` 超域守卫回归,住种子测试 `sim_fixedpoint_test.cs`,不计入本故事 ①–④ 分项)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/input_system/axis_processing_test.cs` — must exist and pass

**Status**: [x] Created + VERIFIED(2026-09-25 超算 batch)
- 真身落点注记:Unity 只编译 `unity/Assets/` 树 ⇒ EditMode 真身落 `unity/Assets/Tests/EditMode/InputSystem/axis_processing_test.cs`,文档路径 `tests/unit/input_system/` 为登记口径(反例夹具 `tests/unit/input_system/fixtures/*.json` 七组,README 落点说明同批)
- 执行 ✅ **VERIFIED 2026-09-25 超算 batch**:初版 507/507;评审修复(F1–F4)后复跑 **510/510 全绿 exit 0**(log `unity/Logs/build-story002-fixes2.log`);本故事 **25 用例全 Passed**(① 6 + ② 5 + ③ 3 + ④ 11 —— 分项按实际重算,原「① 5」为漏计 tinyMagnitude 的笔误;另 F4 回归 1 测住 `sim_fixedpoint_test.cs`,计入 510 总数、不入本故事分项)

---

## Dependencies

- Depends on: Story 001(轴值从唯一动作资产读出)
- Unlocks: Story 010(常驻轴读路径零分配含本函数的逐帧调用)

---

## Completion Notes
**Completed**: 2026-09-25
**Criteria**: 4/4 passing(AC-3-A9①②③④ 全过;traceability 零 UNTESTED)
**Deviations**: 3 条 ADVISORY ——
1. **范围外触达(code review 驱动,已修)**:`Sim.Contracts/FixParse.cs`(F4 超移位域守卫 —— `<<` 静默回绕会让错值伪装合法 raw 溜过装载断言)+ `sim_fixedpoint_test.cs`(F4 回归测的归属落点,FixParse 自己的种子测试);另首版批含 `AssemblyGates.cs` b3 Manifest 补登 `Gameplay.Input`(story-001 B1 拆装的潜伏清单缺口,门清单向 ADR-025 §① 同步,非新裁决)。
2. **binder schema 脚手架路径无测(QA Q3)**:schema_version 缺失/非整数/不匹配、未知键白名单、必填键缺失、null Fix、阶段1 词法失败 —— 均为 ADR-014 通用脚手架,本故事 AC 未点名;后续数据管线 story 或 tech-debt 轮登记,避免静默腐化。
3. **测试经 `[CallerFilePath]` 上溯仓库根读夹具/种子(QA Q4)**:违 test-standards「单测不依赖文件系统」字面;编译路径变动的失败方向是红(找不到夹具)不是假绿,Test Evidence 已有落点注记,接受。
**Test Evidence**: Logic —— 真身 `unity/Assets/Tests/EditMode/InputSystem/axis_processing_test.cs` **25/25 Passed**;全套 EditMode **510/510 全绿 exit 0**(2026-09-25 超算 batch,log `unity/Logs/build-story002-fixes2.log`);登记口径路径 `tests/unit/input_system/`(fixtures 七组 + README 同批)。
**Code Review**: Complete —— 会话内 /code-review 双代理并行(unity-specialist F1–F6 · qa-tester Q1–Q4);F1/F3/F4/F5 代码修 + F2/Q2 计数修 + F6/Q1 登记补测,残余 Q3/Q4 记上文 ADVISORY;verdict **APPROVED WITH SUGGESTIONS**(修复后复跑 510/510 绿)。
**Manifest**: v2026-09-21 一致(staleness PASS)。
