# Story 002: F1 调用契约与四修正项供料(spy 可证)

> **Epic**: 炮制
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/processing.md`(规则一 · 规则四 · 规则五 F-18.1 · 规则九 · R-18-C/D-21-31 · AC-18-01/02/03/04/06/08/14)
**Requirement**: TR-processing-001(18 不发明结算,F1 唯一求解器,⚠️ partial)· TR-processing-002(18 路径零 Ceil/Round/clamp 结算算式)· TR-processing-003(SkillMod 传 Level 不传结果,⚠️ partial —— O-18-R7/D-21-33 newtype 前置)· TR-processing-006(四修正项输入侧:18 供料,21a 定曲线)· TR-processing-007(EnvMod 可负、原样透传)· TR-processing-008(调 F1 一次拿三出参,❌ gap —— 调用契约形状无 ADR)· TR-processing-013(18 不认识药,❌ gap)· TR-processing-014 / TR-processing-018(装载期防御与硬失败)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-014(主): 18 供料、21 定曲线的分工 = 数据管线单一出处纪律 · ADR-006(次): 定点边界(求和+clamp 唯一落点在 21a F1 正文,EnvMod_total)
**ADR Decision Summary**: F-18.1 只是**调用契约,零新数学**;18 调 F1 **恰 1 次**、拿 `ActualConsumed` / `OutputQty` / `OutputQuality` 三出参并原样交给 20(出参与 F1 返回值同一引用,零再加工 —— 原 AC-18-07 已并入 AC-18-01②);`SkillMod` 传**等级 int**(曲线归 21a,在 int 域预先除尽会压死曲线);`QualityMod` 输入 = 被点名实例集(18 供 `min(quality)` 语义由 21a 曲线定);`EquipMod` 读 24(只读,18 不合成数值);`EnvMod` = 两个**未钳制分量**原样透传(`EnvMod_climate` = 5 的 F-5.x 即时值@医馆房间格@点火 tick + `EnvMod_clinic` = 24 读数,求和+clamp 唯一在 21a,R-18-C/D-21-31)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 供料与调用面 = 门 A 纯 C#;AC-18-15(跨平台三出参逐位)归 ADR-012 矩阵(EXTERNAL,不在本故事),本故事钉 Mono 侧 spy 契约。

**Control Manifest Rules (this layer)**:
- Required: spy 夹具包裹 F1(记录入参/出参引用);`L` 入参恒等于同次 `QueryLevel(player, CRAFT_SKILL)` 返回值;两 EnvMod 分量逐位原样透传(零加法零 clamp);符号引用扫描(非 grep 字符串)三族:第二求解实现 = 零、`drug_profile`/`polarity`/`treatable_by` 读取 = 零、结算路径 `Ceil/Round/Clamp/定点乘除` = 零(边界:装载校验/呈现换算不算)
- Forbidden: 18 侧写任何 F1/F2 正文算式;把 `min_quality` 或等级结果预除后传入;对 EnvMod 分量做「防御性 clamp」(好心 clamp = 破 D-21-31 唯一落点);`[Serializable]` 供料中间态(进程态纪律,承 Story 004)
- Guardrail: `SkillLevel` newtype 未落地前以运行时恒等断言现测(O-18-R7);newtype 落地后升级为类型系统断言 —— 本故事两形态都预留接缝,不因前置未裁而搁置

---

## Acceptance Criteria

*From GDD `design/gdd/processing.md`, scoped to this story:*

- [ ] **AC-18-01**: spy 夹具下 ① F1 恰被调用 1 次;② 18 交给 20 的 `consumed[]`/`produced[]`/`outQuality` 与 F1 返回值为**同一引用**(零再加工);③ 18 程序集内第二求解实现 = 零(Roslyn/IL 符号引用扫描)
- [ ] **AC-18-02**: 18 结算路径上 `Ceil`/`Round`/`Clamp`/定点乘除调用 = 零(装载校验/呈现换算边界除外)
- [ ] **AC-18-03**: spy 拦截 18→F1 传参 ⇒ `L` 入参恒等于同一次 `QueryLevel(player, CRAFT_SKILL)` 返回值(传等级不传结果;前置义务 O-18-R7 未落,现以运行时恒等断言测)
- [ ] **AC-18-04**: 符号引用扫描 `drug_profile`/`polarity`/`treatable_by` 读取 = 零(规则九:18 不认识药)
- [ ] **AC-18-06**: 反例哨兵夹具(两 EnvMod 分量各自越界合成输入)⇒ 18 向 F1 传参时两分量各自原样透传(逐位相同)、18 侧零加法零 clamp
- [ ] **AC-18-14**: 装载期注入违反四 cap ↔ `QTY_MULT_MAX` 的合成配方表 ⇒ 18 装载路径**硬失败**(防御断言)。⚠️ 主执行 = 21a 烘焙管线(OQ-18-2 已裁[甲]),本条只断 18 的防御半边
- [ ] **AC-18-10b** [L]: 不可发起的配方在面板**不出现(非灰置)**。**⚠️ EXTERNAL · BLOCKED-BY-42** —— 载体 = 42 屏清单尚未认领炮制交互(`O-18-R6` 未回写),落地前不得记绿(规则十 / UI-18.2;本故事只保证机制侧供数无数字泄漏)
---

## Implementation Notes

*Derived from ADR-014 §一/§五(主)/ ADR-006:*

1. 供料结构体(全整数/引用,不落流):`{ RecipeId, L: int, consumedInstances(→QualityMod 集), EquipMod(读 24 只读接口), EnvMod_climate(读 5 即时值,锚点=医馆房间格@点火 tick,OQ-18-6 口径), EnvMod_clinic(读 24) }`。18 不定义任何曲线/cap 语义 —— 只组装参数包并调用 21a F1 单点。
2. `EnvMod_climate` 的取值时刻 = **点火 tick**(落流时序在 Story 003,本故事只钉「取一次、原样传、不缓存不重采样」的供料纪律;等待期不重读,完成侧派生 = 输入纯函数)。
3. spy 夹具:`RecipeSettlementSolver`(item-database story-003 件)外包记录层,断「恰 1 次 + 返回引用直传 20 入口」;`SameReference` 断言而非值相等(值相等允许了悄悄再加工)。
4. 扫描件复用与新增:第二实现扫描与 item-database story-003 AC-21a-6 同一扫描器(标识符口径白名单共享,零第二份扫描基建);新增 `drug_profile` 族与结算算式族两套标识符表,同扫描器驱动。
5. 18→20 交接 = Story 003/004 的原子 `Apply` 入口;本故事把交接面钉成「F1 返回值的直接转发」,任何中间加工(含排序/去重「好心」)都破 AC-18-01②。
6. 四 cap ↔ QTY_MULT_MAX(AC-18-14):18 装载路径读烘焙常量表做防御断言(不等式 Σ正的cap + max(0,ENV_MOD_MAX) ≤ QTY_MULT_MAX−1,与 21a AC-21a-3 同判据同常量,两处共用一处公式定义)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001:准入谓词(供料的「能不能烧」半边)
- Story 003:`Craft` 载荷十字与落流(供料结果如何持久化)
- Story 004:执行时序、单炉、无进程态、重放等值
- Story 005:完成调度、起货、音频/走查半边(AC-18-08 的走查执行依赖 42 面板,本故事只保证机制侧零数字源)
- item-database epic:F1/F2 曲线本体与 cap 主校验(AC-21a 族);`SkillLevel` newtype(O-18-R7 = 21a D-21-33 回写义务)
- 24 医馆即机器:EquipMod / EnvMod_clinic 的数值生产(18 只读)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-18-01**: 恰 1 次 + 同引用 + 零第二实现。
  - Given: spy 包裹 F1;合成夹具配方(GDD 注③:合成形状义务不豁免)。
  - When: 执行一次炮制结算。
  - Then: spy 计数 = 1;20 入口收到的三个出参 ReferenceEquals 于 F1 返回值;符号引用扫描 18 程序集:求解器标识符命中仅 = 对 21a 单点的调用。
  - Edge cases: 失败路径(Story 001 准入不过)⇒ F1 调用 0 次(不多烧);重试两次 = 两次独立调用(允许)。
- **AC-18-02**: 结算路径零算式。
  - Given: 18 程序集;边界清单(装载校验/呈现换算文件)显式登记豁免名单。
  - When: 扫描 `Ceil(`/`Round(`/`Clamp(`/定点乘除符号。
  - Then: 结算路径零命中;负样例(注入一处 `Clamp`)必红。
  - Edge cases: `Fix` 类型自身的 `Mul` 调用不算「18 写的算式」—— 判据按调用点所属路径(结算/供料 vs 装载/呈现),按 GDD 边界原文。
- **AC-18-03**: 传等级恒等。
  - Given: spy 拦截传参;同一次操作另取 `QueryLevel` 返回值。
  - When: 点火结算。
  - Then: 入参 `L` 与该返回值逐位相等(非「换算后接近」);同一 tick 双调用同值(纯读)。
  - Edge cases: `L=0` 与 `L=SKILL_CAP` 边界;newtype 落地前本断言即主判据(登记 O-18-R7 为升级路径)。
- **AC-18-04 / 06**: 不认识药 + 原样透传。
  - Given: 药引用清单扫描;EnvMod 越界合成夹具(climate=2×ENV_MOD_MAX, clinic=2×ENV_MOD_MIN)。
  - When: 传 spy 调用。
  - Then: 三符号零读取;两分量与输入逐位相同(负值保留,禁 clamp 到 0)。
  - Edge cases: 分量任一缺失 ⇒ 硬失败(Story 003/装载纪律),禁默认 0 填充。
- **AC-18-14**: 装载期防御硬失败。
  - Given: 合成破 cap 配方表(仅动 cap 常量镜像,不碰真实表)。
  - When: 18 装载路径求值。
  - Then: 具名 `throw`,启动终止该表;真实表路径不受影响。
  - Edge cases: 等号临界(Σcap = MAX−1)过;主执行(21a 烘焙)在另一侧,两边同夹具双跑(镜像 AC-21a-9/3 共用 `invalid_cap_sum.json` 先例)。
- **AC-18-10b** [L](EXTERNAL · BLOCKED-BY-42): 候选面板不可发起配方「不出现」。
  - Given: 42 炮制面板落地后;含 gate 不达/品质不足配方的场景。
  - When: 走查面板清单。
  - Then: 不可发起者不出现(非灰置);清单成员资格 = 本故事/Story 001 谓词输出(零第二判定)。42 未交付 ⇒ 保持未勾,禁以机制侧「供数正确」代走查绿(镜像 AC-18-18 纪律)。
  - Edge cases: 刚掉级/品级不足变化的即时性由 fold/谓词重答保证(面板刷新节奏归 42)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/Processing/f1_call_contract_test.cs` — must exist and pass(spy + 扫描器族)
- [L](AC-18-10b):走查证据归 42 面板交付轮(EXTERNAL 半边;机制侧供数测试在本文件)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(准入前置),item-database story-003(F1/F2 求解器,Complete —— spy 对象),item-database story-011(扫描件基建先例,Complete),24 侧 EquipMod/EnvMod_clinic 只读接口(clinic-machine epic,未就位时以接口桩先行)
- Unlocks: Story 003(供料结果进载荷),Story 005(完成派生所需输入集冻结)

---

## Completion Notes

*(placeholder — to be filled at story completion)*
