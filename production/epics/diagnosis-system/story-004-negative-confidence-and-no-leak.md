# Story 004: F-8.3 阴性把握度与不泄漏不变量

> **Epic**: 诊断与体征揭示
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 8h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/diagnosis-system.md`(§F-8.3 C_neg / 把握度 / 构成排除 / L*_j · §F-8.4 无随机三理由 · §F-8.5 不泄漏 · AC-8-F1…F5 公式级回归锚 · C-3/C-4/C-7)
**Requirement**: TR-diag-016(读数存档归属 39 或病例流 —— 8 侧只产意图,不持存)· TR-diag-018(AC-8-F5 表现层 float 跨平台一致)· TR-diag-012(D-8-5 登记项的阴性族半边)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-006(舍入;`C_neg` 线性项显式 Mul/Add 防 FMA,承 G-4)· ADR-012(表现层 float 跨平台一致 = 三格矩阵判据)· ADR-005(F-8.4「无随机」与确定性同族纪律)· ADR-008(把握度**不进数值公式**、判断记录 Kind 走病例流 —— 落笔侧归 story 005)
**ADR Decision Summary**: F-8.3 `C_neg = NEG_CONF_0 + (NEG_CONF_CAP − NEG_CONF_0) × s^NEG_GAMMA`;`W_j = NEG_WEIGHT_j(阴性)/ NEG_WEIGHT_FALLBACK(阳性兜底)`;`把握度 = clamp(C_neg × W_j, 0, 1)`;`构成排除 ⟺ 把握度 ≥ EXCLUDE_CONF_MIN`;`L*_j = min{Skill : 把握度_j(Skill) ≥ EXCLUDE_CONF_MIN}`。F-8.5 不泄漏:`C_neg` **仅** `(Skill, sign_id)` 之函数,**不随 `Sign_j` 浮动**。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(求值 LOW;AC-8-F5 跨平台 float 逐位一致受 ADR-012 矩阵前置 —— 该条 NOT-RUN 直至矩阵,禁借绿)
**Engine Notes**: `s^NEG_GAMMA` 与 story 003 同法预计算定表(G-1);运行期 float 仅在门面层,G-4 位宽 `System.Single` 固定。

**Control Manifest Rules (this layer)**:
- Required: 双族参数正交(C-4):阴性族 `{NEG_*}` 只进 F-8.3,阳性族 `{READ_*, READ_GAMMA}` 只进 F-8.1;`L*_j ≤ SKILL_CAP` 全表恒成立(C-3,「会静默坏掉的约束,必须逐条算、必须报警」)
- Forbidden: PRNG 调用点(零随机);把握度进任何数值/疗效公式(规则八);把把握度做成「置信度条」UI(构成排除是阈值判定不是连续值)
- Guardrail: 阈值族数值(`NEG_CONF_0/CAP/GAMMA/EXCLUDE_CONF_MIN`)归用户数值轮 ⇒ 以合成旋钮判形状;`L*_j` 具体档值待数值轮,存在性/不等式即判

---

## Acceptance Criteria

*From GDD `design/gdd/diagnosis-system.md`, scoped to this story:*

- [ ] **AC-8-10**[L] BLOCKING:C-3 阴性不得是死内容 —— 遍历 R-8.2 全部阴性条目,逐条算 `L*_j`,断言 `∀ j: L*_j ≤ SKILL_CAP`;任一超标 ⇒ 测试失败并点名条目(含 C-6 的 `neg_weight > 0` 前提联动 story 002)
- [ ] **AC-8-11**[L] BLOCKING:C-7 `L*_j ≥ tier_named_j`(说不出的话谈不上算数);阴性组 `tier_named=Lv20` 与 `AC-8-F1` 的 `L*_j ∈ (15,20]` 联立 ⇒ `L*_j = 20`,两处不等即规格自相矛盾 ⇒ 失败
- [ ] **AC-8-12**[L] BLOCKING:C-4 两族正交 —— 仅改 `READ_GAMMA` ⇒ 全部阴性把握度**逐位不变**;仅改 `NEG_GAMMA` ⇒ 全部阳性可读性**逐位不变**;`UC-8-F1` 只随阴性族变化
- [ ] **AC-8-14**[L] BLOCKING:多阴性证据不合并 —— 两条同时达标 + 第三条不够格 ⇒ 各自独立判定,**不相乘不相加**;系统不存在任何聚合量/置信度条;调低第三条不改变前两条判定(逐位)
- [ ] **AC-8-15**[L] BLOCKING:`neg_weight` 只影响排除路径 —— 阳性条目误填 `neg_weight` ⇒ 其可读性只走 F-8.1 与该值无关、不进任何排除判定(无副作用;构建期报警在 story 002)
- [ ] **AC-8-16**[L] BLOCKING:F-8.4 无随机 —— 同 `(病人, tick, Skill, 动作序列)` 跨两独立进程 N ≥ 10⁴ 次:词/四态/把握度/是否构成排除 四项每次逐位相同;8 程序集零 PRNG 调用点(反射+IL)
- [ ] **AC-8-17**[L] BLOCKING:F-8.4 语义边界 —— 低熟练度 = 「读得粗/读不出」,**不是读错**:`Sign_j` 客观极性不因熟练度翻转(无假阳性、无真值改写);低档唯一退化方向 = 精度降/读不出
- [ ] **AC-8-18**[L] BLOCKING:F-8.5 不泄漏(公式层)—— 固定 `(Skill, sign_id)`,A/B 两病人 `Sign_j` 差异极大 ⇒ `C_neg(A) == C_neg(B)` 逐位;反例断言:把握度随 `Sign_j` 浮动 ⇒ 失败;实现级 = 断言 `C_neg` 实参表不含 `Sign_j`(反射)
- [ ] **AC-8-F1**[L] BLOCKING:回归锚 —— 伤寒/痢疾 Lv15/Lv20 用例(`sign_abd_soft`:`L*_j ∈ (15,20]` ∧ `=20`)常驻套件(AC-8-50 守门的 F1 条)
- [ ] **AC-8-F2**[L] BLOCKING:双参数可分离(阳性族与阴性族在回归用例上可独立复算)
- [ ] **AC-8-F4**[L] BLOCKING:不泄漏回归(阳性/阴性各一条)常驻套件
- [ ] **AC-8-F5**[I]:表现层 float 跨平台逐位一致(三格矩阵)—— **NOT-RUN 直至 ADR-012 矩阵实跑,禁借绿**;定表哈希的 Mono 侧自洽先行
- [ ] **AC-8-50**[L] BLOCKING:回归锚存在性 —— F1…F5 五条全部常驻回归套件(本 story 落 F1/F2/F4/F5 四条套件入口,F3 归 story 003)

---

## Implementation Notes

*Derived from F-8.3/8.4/8.5 + ADR-012:*

1. `C_neg` 定表 `[0..60]`(阴性族)与 story 003 的 `READ_FLOOR_TABLE`(阳性族)**分表分文件**,正交性由「改 A 表哈希不影响 B 表消费路径」的测试物化(不只是代码审查)。
2. `W_j` 取用:阴性用条目 `NEG_WEIGHT_j`;阳性走 `NEG_WEIGHT_FALLBACK` —— 兜底值住全局旋钮(数值轮),表里不重复存。
3. `clamp(…, 0, 1)` 在 float 门面域执行(G-4 位宽钉 Single,显式 Mul/Add 防 FMA,承 story 001 检查单)。
4. `L*_j` 求解 = 对定表单调扫描(int 档步进),禁浮点求根;`EXCLUDE_CONF_MIN` 恰值边界用「把握度 == 阈值」的可达档测(阈值不可达 ⇒ C-3 报警,不是默认失败)。
5. 万次无随机测用同一输入表驱动(承「固定样本穷举,禁随机抽样」纪律,同 9 的 AC-10 口径)。
6. 10⁴ 次的执行成本若超 EditMode 预算,降为 10³ + CI nightly 10⁴ —— 该降级须在本 story 完成时登记,不静默改判据。

## Out of Scope

- [Story 003]: 阳性族(F-8.1/8.2)
- [Story 005]: 四态/判断三态状态机、`threshold_transition` 订阅、RECHECK 窗口
- [Story 006]: 表现层像素级不可区分(AC-8-19 的截图半边)
- TR-diag-021…025 音频四条(audio-system epic)

## QA Test Cases

*Written at story creation(lean mode).*

- **C-3 报警**: 把某阴性条目 `neg_weight` 压到 `L*_j > 60` 的合成旋钮 ⇒ 测试红且点名;恢复 ⇒ 绿。
- **正交双向**: 改 `READ_GAMMA` 重跑阴性集哈希 ⇒ 不变;改 `NEG_GAMMA` 重跑阳性可读集 ⇒ 不变(AC-8-12 两半)。
- **不合并**: 三阴性夹具(AC-8-14)+ 反射断言全局不存在「聚合置信度」类型字段。
- **实参洁净**: `C_neg` 的 IL 参数表 ∌ 任何来自 `VitalsDto.Sign` 的值(AC-8-18 实现级)。
- **极性不翻**: 同 Sign 六档 Skill ⇒ 极性输出集 ⊆ {同值}。
- **套件锚**: F1/F2/F4/F5 四测常驻(存在性断言,AC-8-50)。

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/DiagnosisSystem/confidence_leak_test.cs` — must exist and pass(AC-8-F5 列 NOT-RUN-BLOCKED-BY-ADR-012)
**Status**: [ ] Created — NOT STARTED

---

## Dependencies

- Depends on: Story 001(浮点门面与 PRNG 扫描)、Story 002(`neg_weight`/`tier_named`)、Story 003(阳性族对偶与哨兵)、disease-simulation epic story 004(`Sign_j`/σ fixture)
- Unlocks: Story 005(「构成排除」进判断态)、Story 006(墨色/笔迹承载「把握不足」—— 承 AC-8-13 的「不加文字标记」)、review-all-gdds 的 F 套件锚

## Completion Notes
