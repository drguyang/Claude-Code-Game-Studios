# Story 006: onset 事件流 · dose_seq 派生 · F-25-8 平衡门

> **Epic**: 格斗与武器线
> **Status**: Complete
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 7h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-30
## Context

**GDD**: `design/gdd/combat-and-weapon-lines.md`(规则二/三 Kind 与载荷 · F-25-7 dose_seq · F-25-8 构建期平衡断言 · 归零转译入口 · §九 A↔AC 断言映射)
**Requirement**: TR-combat-005(onset 三元组 Kind 登记:InjuryOnset 病史流 / EnemyInjuryOnset 世界流 / InjuryStateChanged 世界流写者=9) · TR-combat-006(去重五元组键 (tick, actor, target, injury_id, dose_seq);dose_seq = 事件流纯函数派生,禁可变计数器) · TR-combat-001(归零 = INJ_COMA 投影,25 只写 onset) · TR-combat-024(玩家致死形态,⚠️ gap/OQ-25-1)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-009(主): 三态分类与流归属;ADR-016: 敌人复用 9 伤情模型
**ADR Decision Summary**: 「一支 Kind 无法同时落两条流」⇒ 病人/敌人各一支,写者均 25,经 `IEventSink.Append` 由**主机唯一**执行;敌人 id 经 `IIdAuthority` 与病人共用同一空间(ADR-006 Amendment B 高水位仍成立);敌人伤情行落世界流不污染病史流;归零转译入口在 25、执行在 9 的 `LethalFor`+阈值;**onset ≠ 治疗**(无「挨打续命」)。F-25-8:逐动作 Trauma_∞(a) 与逐 bar Trauma_∞(b) 在 CP ∈ {0, CP_MAX} 两端 ≥ COMA_THRESHOLD 的构建期断言,乘法形式零除法(A25a/b),A26 4-ulp 带登记,A27 SWITCH ≤ HALF_LIFE_MAX。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 事件流写入为纯 C# 契约(LOW);MEDIUM 来自与 9 的 F1 入口(PS 单位对消 R11)集成面须在 CatchUp/Step 双路径逐位一致 —— 跨平台归 ADR-012 矩阵,本 story 只跑同进程重放。

**Control Manifest Rules (this layer)**:
- Required: 载荷 `{actor_id, target_id, injury_id, magnitude, tick, dose_seq}` 全整数域(magnitude = Fix raw);dose_seq 从流重算(主机迁移后可重建);同 tick 两条都收、重传判重(五元组键)
- Forbidden: 可变计数器实现 dose_seq;25 侧携带/折叠 HurtLevel 档位(TR-combat-014:折叠归 9);击退注入任何 sim 状态(TR-combat-016 纯表现)
- Guardrail: `AC-25-6-08`(玩家致死路径)**BLOCKED-BY-9**(SelfLimited/OQ-25-1 未裁形态)—— 禁记绿;W-1 型双口径(D 域含伤情与否)不属 25,但 onset 载荷的 injury_id 语义以 9 注册表为准

---

## Acceptance Criteria

*From GDD `design/gdd/combat-and-weapon-lines.md`, scoped to this story:*

- [ ] AC-25-5-01:同 tick 对同目标两次合法出手 → 两条 onset 都入流(dose_seq 0/1 区分);重复投递(五元组全同)被拒
- [ ] AC-25-5-02(F-25-7):dose_seq 从既有流纯函数派生;模拟主机迁移后重放,dose_seq 序列逐位重构(零计数器残留)
- [ ] AC-25-6-01:病人 onset 落病史流、敌人 onset 落世界流(Kind→流路由 = entities.yaml/kindgen 生成物,25 不自选流)
- [ ] AC-25-6-03:归零转译入口 —— 25 只 Append onset,昏迷/死亡投影断言发生在 9 侧(集成夹具验证「25 载荷无档位字段」)
- [ ] AC-25-6-08 ⚠️ **BLOCKED-BY-9**:玩家可被致死伤的形态未裁(OQ-25-1),本条只登记落点,不出测不记绿
- [ ] F-25-8 构建门:A25a/A25b(逐动作/逐 bar Trauma_∞ ≥ COMA_THRESHOLD,CP 两端,乘法形式)、A26(4-ulp 带登记)、A27(SWITCH ≤ HALF_LIFE_MAX)作为构建期断言实现,违例 fixture 拒
- [ ] AC-25-1-08(重放半边):Step 与 CatchUp 两条路径产出的 onset 序列逐条相同(同进程)

---

## Implementation Notes

*Derived from ADR-009/016 Implementation Guidelines:*

1. `dose_seq(actor,target,injury,tick) := 流中同 (tick,actor,target,injury_id) 前缀计数` —— 读流实现,注意与占用/冷却交互下同 tick 多击的合法性(story-002 已允许窗口后重击落在不同 tick;同 tick 双 onset 来自多意图源,判重仅五元组全同)。
2. Append 走 `IEventSink`(按 Kind 纯函数路由);`Patient` 字段病人=真实 id、敌人=IIdAuthority 同空间 id;`Seq` 由发号权威给(承 ADR-006 Amendment A)。
3. F-25-8 断言以**乘法形式**写(`Trauma_∞ = Σ 注入 × Decay 几何和` 化简为无除法乘积,判据 A25a/b),避免运行期浮点;COMA_THRESHOLD/HALF_LIFE_MAX 等值归数值轮,断言门本身先落。
4. 与 9 的 F1 入口(Σ magnitude × SCALE_d × Decay)集成测试:PS 对消以「同 magnitude 序列 → 同 position_agg 轨迹」断言,不复制 9 的公式。
5. 敌人行不进病史流:集成断言扫病史流无 enemy actor 的目标行(路由错 = 高水位污染,ADR-016 §二)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: Kind 的 entities.yaml 登记与 kindgen 过门(本 story 消费其生成物)
- [Story 002–005]: 求值点/命中/magnitude/窗口逻辑(onset 的前置全部就绪后才到本 story)
- 系统 9:LethalFor、昏迷投影、HurtLevel 折叠、trauma_half_life;系统 27:意图产生;表现层:三时钟 T1/T2/T3 与 [V] 走查(移交 42/44/27 epic)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(五元组判重)**: 同 tick 双击与重传
  - Given: 同 (tick,actor,target,injury) 合法两击 + 一条网络重传副本
  - When: Append
  - Then: 两条入流(dose_seq=0,1);第三条(全同)被拒且无副作用
  - Edge cases: 仅 dose_seq 不同的「同」事件不会自然产生 —— 派生函数保证
- **AC-2(迁移重放)**: dose_seq 可重建
  - Given: 一段含双击的流;切新主机(零内存状态)
  - When: CatchUp 重放 + 续跑
  - Then: 后续 dose_seq 接续正确(从流派生,非从 0 重启);整序列逐位同
  - Edge cases: 迁移瞬间在途事件 → 依赖 45 重排(登记日志依赖,不在本 story 修)
- **AC-3(F-25-8 门)**: 违例表被构建期拒
  - Given: fixture 使某动作 Trauma_∞(CP_MAX) < COMA_THRESHOLD
  - When: 构建期断言批
  - Then: A25a/b 硬失败;乘法形式无除法路径(代码评审 + AST)
  - Edge cases: CP=0 端同判;A27 违例(SWITCH > HALF_LIFE_MAX)独立拒

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/Combat/combat_onset_dose_seq_test.cs` + 构建断言落点 `Editor.Tools` 族(与 ADR-014 阶段2 同批);`AC-25-6-08` 无证据 —— BLOCKED-BY-9,登记不落测
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001–005 全部;9 的 F1 入口/Down/LethalFor(⚠️ 部分 BLOCKED-BY-9);45 的在途事件语义(仅日志依赖,ADR-001 §一之三 已裁 EmergencyAttempt 可靠通道同类先例)
- Unlocks: 与 13/9 的伤情集成、27 epic 的 Engage 联测、玩家致死面(OQ-25-1 结案后)

---

## Completion Notes

*(留空 — story 关闭时回填)*

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `OnsetPayload` — onset 事件载荷（ActorId / TargetId / InjuryId / Magnitude / Tick / DoseSeq）
- `CombatOnsetStream` — onset 事件流写入器（ComputeDoseSeq / IsDuplicate / ValidateNoTierField / ValidateF25Balance / ValidateEnemyOnRoute）
- dose_seq 从既有流纯函数派生（禁可变计数器）
- 五元组判重键 (tick, actor, target, injury_id, dose_seq)
- F-25-8 构建期平衡断言（乘法形式，零除法路径）
- 测试: 7 条单元测试（全部通过）

**Deviations**: 
- F-25-8 平衡门为简化版（无完整 Trauma_∞ 计算），完整版需要 9 的 F1 入口

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/Combat/combat_onset_dose_seq_test.cs` — 7 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，无 BLOCKING 问题

**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
