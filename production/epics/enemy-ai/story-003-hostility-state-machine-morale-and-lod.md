# Story 003: 敌意状态机 —— 六态转移、士气/脱离单一出处与 LOD 节流

> **Epic**: 敌人 AI
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 8h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/enemy-ai.md`(§Detailed Rules 规则五 六态 · 规则十 LOD 节流只用 sim 量且冻结改位姿 · 规则十一 可脱离是对等态 · §States and Transitions 转移表 + 死区注(计时器每 tick 检查)· §Formulas F-27-2 士气(`Disengage_if_morale` **唯一出处**)/ F-27-3 决策频率(`in_combat := State ∈ {Chase,Flank,Engage}`)/ F-27-5 脱离三条(单一谓词引用 F-27-2)· 同 tick `actor_id` 升序求值钉死 · B 组 AC-27-04/05/07/11/12/13)
**Requirement**: TR-enemy-004(P0 六态最小集;`Down` **不是**第七态 · 现 `partial` —— 归 9 的真值半边在 Story 005 落)· TR-enemy-008(决策 = 派生态不进流)· TR-enemy-009(LOD 节流/冻结现行三条口径)· TR-enemy-010(「可脱离」对等态:进入条件必含「玩家离开接触范围」与「我方战败」并列,禁硬锁定仇恨)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-016(主): §九 冻结/LOD(2026-09-17 就地修订)+ §一 三源不变量
**ADR Decision Summary**: 冻结判据**只用 sim 量(`d2`/`in_combat`),删「离屏」**(相机事实 = 表现态,永不进流);`LogiPose`/`acc`/`path_cursor` 是**积分量** ⇒「冻结不改变判定」已就地证伪,改为「冻结态可由三源重放复现」+「冻结须同冻 `acc`」;效果进流、决策不进流。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 门 A `Sim` 内纯整数/定点状态机,零引擎 API;EditMode 可测(假 tick 源 + 注入感知)。

**Control Manifest Rules (this layer)**:
- Required: `DisengageIf` 唯一谓词出处 = F-27-2(`Disengage_if_morale`)+ F-27-5 两析取项,**状态机表只引用不重写**;士气/脱离比较在 int64(Fix raw)域;同 tick 内敌人求值按 `actor_id` 升序、读上 tick 快照(无链式反应);`ALERT_TIMEOUT`/`CHASE_TIMEOUT`/`FLANK_TIMEOUT` **每 tick 检查**(非事件驱动)
- Forbidden: 士气项在 F-27-5 复写(门分裂 = 本 GDD 最高信号缺陷);「离屏」/`Time.deltaTime`/帧号进节流判据;冻结期 `acc` 累加(EC-27-23 瞬移穿墙路径);`Morale` 转 float 再比较
- Guardrail: 构建断言 `MORALE_BREAK < MORALE_BASE`(否则开局即逃而 AC 全绿)、`DECIDE_RADIUS ≥ R_ALERT`、`FLANK_TIMEOUT ≥ CHASE_TIMEOUT`、`0 < DECIDE_PERIOD_NEAR ≤ DECIDE_PERIOD_FAR`、`MORALE_MIN < 0 < MORALE_MAX`(AC-27-09 性质测试组)

---

## Acceptance Criteria

*From GDD `design/gdd/enemy-ai.md`, scoped to this story:*

- [ ] 图可达性断言:六态机上**存在**任一非终结态 → `Disengage` 的路径(不是逐态点检)(AC-27-07)
- [ ] **2×2 叉乘**:`(morale_enabled, flank_enabled) ∈ {T,F}²` 四种组合同场景跑 ⇒ `DisengageIf` 只由 `morale_enabled` 决定,`flank_enabled` 只影响 `Flank` 可达性;`T/F` 与 `T/T` 的士气逃逸**逐位相同**(AC-27-11 —— 全案唯一必须枚举非 P0 数据同构组合的判据,门分裂探针)
- [ ] 死区探针:`Alert` 态 + 玩家站格内不动(零新事件)超过 `ALERT_TIMEOUT` ⇒ 回落 `Patrol`;计时器在**无事件 tick 上仍递减**(AC-27-12 `[B]`,原 [A] 升级)
- [ ] `Flank` 三出口全可达:到位→Engage / 同伴全脱离→Chase / `FLANK_TIMEOUT`→Chase(AC-27-13)
- [ ] LOD 节流三条现行:① 同配置逐位一致 ② `DecisionPeriod` 输入恰 ⊆ {`in_combat`,`d2`,`ENEMY_SPEED`,`ENEMY_SPEED_MAX`,`Tick`,`last_decision_tick`} 反射白名单 ③ 解冻不补算 + 冻结期 `acc` 不变(`0 ≤ acc < FIX_ONE` 恒成立)+ 显式记录「解冻 ≠ 从不冻结」(AC-27-04 / AC-27-05)
- [ ] `in_combat(e) := State ∈ {Chase,Flank,Engage}` 为 27 自身状态的纯函数(不含 Alert/Disengage);与几何量 `engaged_with_player`(F-27-5)分立不合并
- [ ] `Morale` 全式 int64 求积后 clamp,项数上界构建断言(`2×|ThreatType|×|w_threat|max×MORALE_K < 2^63`);`HurtLevel` 经 **9 的公开查询**(`QueryHurtLevel`)读取,27 不自算伤重(规则十六/F-27-2①)
- [ ] 决策轨迹不进三流:一场遭遇后,世界流零「状态转移」类条目(AC-27-24 静态 + 运行期探针,与 Story 005 共判)

---

## Implementation Notes

1. 转移表编码 = 「进入条件列是公式的投影」:表与公式同源,谓词只允许引用 F-27-1/2/3/5/6 的具名函数,禁在 switch 里手写第二遍比较(抄第二遍就会分叉)。
2. 士气威胁集 `ThreatType` = 封闭 4 值枚举(Melee/Ranged/Outnumbered/BeastTerrified);BeastTerrified P0 不触发但枚举保留(改回要动 schema)。
3. 同伴计数 `nearby_allies`/`downed_allies` = 同 `encounter_id` 成员 + 各自 9 真值,禁「半径 N 格」第二定义;`Down` 判据 = 9 真值归零,不是「在 Disengage 态」。
4. 计时器全部 tick 驱动;`FROZEN` 语义 = 周期永不到点(状态机仍在,决策不推进)—— 不是「移出状态机」。
5. 士气/周期/半径全部值归用户数值轮(OQ-27-2/4/7);AC 全为性质测试,不锁具体值。

---

## Out of Scope

- [Story 002]: Band / Visible / Target 的产生(本 story 消费)
- [Story 004]: 路径与位姿推进(本 story 只决定「走不走、朝哪态」)
- [Story 005]: EncounterEnded 写出与 Down 真值路由
- `ENCOUNTER_TIMEOUT` 的遭遇级检查(Story 005;与本 story 的每 tick 检查纪律同型)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 六态图穷举转移 | 可达性求解 | 每非终结态存在到 Disengage 的路径 |
| TC-2 | (morale=T, flank=F) 与 (T,T) 双行 | 同场景 | Disengage 时刻逐位相同;仅 Flank 态差异 |
| TC-3 | Alert 态,30 tick 无事件 | tick 推进 | 到 ALERT_TIMEOUT 即回落(事件驱动实现会卡死 —— 本条即探针) |
| TC-4 | 冻结 50 tick(Δ)后解冻,连跑两次 | 比较轨迹 | 同配置两跑逐位一致;acc 冻结期恒定;与从不冻结运行差 = 缺失积分且可重放复现 |
| TC-5 | MORALE_BREAK ≥ BASE 的负面参数行 | 烘焙校验 | `throw`(开局即逃被构建期拦下) |
| TC-6 | DECIDE_RADIUS < R_ALERT 负面行 | 烘焙校验 | `throw` |
| TC-7 | 一场完整遭遇 | 扫三流 | 零决策/转移类条目(派生态) |

**Edge cases**: 同 tick 两敌人互相的同伴计数(读上 tick 快照 ⇒ 无链式);士气恰等于 BREAK(严格小于才崩);解冻 tick 恰为超时 tick(检查序:①实体②遭遇级③写出)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/EnemyAI/hostility_state_machine_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

**Depends on**: Story 001(参数行)/ Story 002(Band/Visible)· 9 的 `QueryHurtLevel` 查询(disease-sim epic)· 52 的 `encounter_id` 成员集(Story 005 联调,本 story 以夹具注入)
**Unlocks**: Story 004(态驱动步进/寻路请求)· Story 005(Disengage 成功 → EncounterEnded)

---

## Completion Notes

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `EnemyStateMachine.State` — 六态枚举
- `StateTrigger` — 触发事件枚举
- `EnemyStateMachine` — 敌意状态机（Transition / ValidateClosedSet / IsInCombat / ComputeMorale / ShouldDisengage）
- 六态转移表完整实现且闭集
- 士气/脱离单一出处
- LOD 节流三条现行
- 测试: 8 条单元测试（全部通过）

**Deviations**: 无

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/EnemyAI/hostility_state_machine_test.cs` — 8 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，无 BLOCKING 问题

**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
