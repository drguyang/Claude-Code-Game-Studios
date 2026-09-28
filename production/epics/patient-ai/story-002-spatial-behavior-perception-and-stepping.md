# Story 002: 空间行为 —— 感知格距、HomeRegion 寻医与定点累加器步进

> **Epic**: 病人 AI 与行为
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/patient-ai.md`(§Detailed Rules 规则四 行为模拟范围 = 在场病人 · §Formulas F-13.2 `KnowsClinic / HomeRegion = EcozoneOf(spawn_anchor(p))` · F-13.3 `Perceives = Δx²+Δy²+Δz² ≤ PERCEPT_R²`(禁 sqrt)· F-13.4 LOD 按 d² 三档 · F-13.7 逻辑格步进(定点累加器 `acc`,`Moving(p)`, `PATIENT_SPEED < FIX_ONE`)· §States `Seeking{EnRoute/AtClinic}` · B/E 组 AC-13-B2/B3/B4/E2/E3/E4/E5)
**Requirement**: TR-patient-006(行为模拟范围 = 在场病人,消费 9 的在场判定,不自建在场定义)· TR-patient-009(感知输入 = 粗粒度整数格;禁 Transform/Raycast/NavMesh;格距整数平方和禁 sqrt)· TR-patient-010(空间量一律 `WorldPos` 整数格;烘焙行为数据禁 float)· TR-patient-013(病人出现率上限归 9;13 不生成/不删除病人)· TR-patient-016(sim 走整数导航格;NavMesh 仅表现)· TR-patient-017(决策 LOD 节流/远区冻结;现行三条 = 同配置逐位一致 + 判据白名单 + 解冻不补算)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-016(主): AI 架构 §三/§五/§九;ADR-015: 单一整数格
**ADR Decision Summary**: §三 感知 = 粗粒度整数格(禁读表现态位置);§五 sim 走整数导航格 A*,NavMesh 仅驱动表现位移,漂移处置单向;§九(2026-09-17 就地修订)冻结判据**只用 sim 量**(删「离屏」)、`LogiPose`/`acc`/`path_cursor` 是**积分量**,跳过 step 就是少积分 ⇒ 冻结确实改变位姿,保证收窄为「可由三源重放复现」+「冻结须同冻 `acc`」。**13 的范围裁定 = 在场才模拟**(承 9 的 OQ-8:CAP=24 硬上限,由 9 守;13 只消费在场判定)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 13 侧空间决策住边界层(读 float DTO)但**几何量本身是整数格**(`WorldPos` / d² / acc 均整数或 Fix);格步进的 `acc` 用 Q16.16 定点(承 ADR-006);无引擎 API。

**Control Manifest Rules (this layer)**:
- Required: `Moving(p) := ¬Frozen ∧ BehaviorState ∈ {Seeking}`(Bedridden/Idle 不推进);`acc` 只在 `Moving` 为真的 tick 前进、转假保持不归零;`PATIENT_SPEED < FIX_ONE` 构建期断言(防跳格隧穿,与 27 F-27-4 同型)
- Forbidden: `sqrt` / `Physics` / `Transform.position` / NavMesh 采样进判定;「离屏」/ `Time.deltaTime` / 帧号进 LOD 判据(AC-13-E4 反射白名单);13 自建在场定义
- Guardrail: `HomeRegion` 锚定 = `EcozoneOf(spawn_anchor(p))` —— 读 6 的静态定义,不读 POI/动态状态;同 tick 内多病人求值顺序钉死(承 27 同型纪律:`actor_id` 升序,读上 tick 快照,无链式反应)

---

## Acceptance Criteria

*From GDD `design/gdd/patient-ai.md`, scoped to this story:*

- [ ] `Bedridden` 态**不产生逻辑位移**:该态下 `Moving(p)==false` 且 `acc` 不变(AC-13-B2 —— 注意断言对象是逻辑量,13 不拥有表现态位置)
- [ ] `Perceives` 与 LOD 分档输入**恰 ⊆ {d², 常数}**(整数平方和,禁 sqrt);反射断言无相机可见性/墙钟/帧号(AC-13-E3/E4)
- [ ] 同配置逐位一致:同一份 LOD 配置跑同一事件流两遍 ⇒ 决策轨迹哈希逐位相同(AC-13-E2);**不**比较「LOD 开 vs 关」(物理上不该相同)
- [ ] 解冻不补算:冻结 Δ tick 后解冻,`acc` 冻结期不变(`0 ≤ acc < FIX_ONE` 恒成立),解冻 tick 从三源重新求值,不追加 Δ 次推进;解冻态 ≠ 从不冻结态且差异可重放复现(AC-13-E5 / F-13.7)
- [ ] 性质测试:任意合法 `PATIENT_SPEED < FIX_ONE` 与任意 `Moving` 真假序列下,每 tick 位移 ≤ 1 格、`LogiPose.Cell` 整数性恒成立(承 27 AC-27-19 同判据)
- [ ] `Seeking` 双相:目标医馆可达 → `EnRoute`;到位 → `AtClinic`;`HomeRegion` 只在初始化时经 `EcozoneOf(spawn_anchor)` 求一次(静态定义,状态变化不回改)
- [ ] 13 零 spawn/despawn 调用,在场判定消费 9 的输出(含 CAP=24 场景:第 25 个在场病人不进 13 的决策集,TR-patient-006/013)
- [ ] 行为决策可重建:同事件流 + 同烘焙数据 ⇒ 同决策序列;重建后 `p.Cell` 与 `acc` 由格+锚点**重新播种**(`acc := 0`),不承诺与冻结前连续(AC-13-B3/B4)

---

## Implementation Notes

1. 求值序:每 tick 按 `actor_id` 升序逐病人求值(纯函数读上 tick 快照)—— 与 27 的钉死序同构,写进测试(打乱字典序输入不变)。
2. `acc` 住 13 的派生态字典(边界层内存),冻结/Bedridden/Idle 期间**保持不归零**;路径为空时不动(禁原地转圈假推进)。
3. 寻路复用 27/23 的整数导航格 A* 基础设施(共享 LOD 口径,承 technical-preferences「13 与 27 的 BEHAVIOR LOD 同批核」)—— 本 story 只立消费点,不重实现 A*。
4. 在场进入时机:病人进入在场范围 ⇒ 行为自进入 tick 起跑,位置由 `WorldPos` 格 + 烘焙锚点播种(§Rules 表「病人进入在场范围」行)。
5. LOD 三档 `NEAR_R/FAR_R` 与 `PATIENT_SPEED` 值归用户数值轮;`NEAR_R<FAR_R` 为装载断言。

---

## Out of Scope

- [Story 001]: Map/滞回/SessionState(本 story 消费 BehaviorState)
- [Story 003]: ViewState 投影 / cue 发射 / Material 映射(本 story 的 LogiPose 是其输入)
- [Story 004]: 联机主机单跑、写路径归 10、重建端到端联调
- 27 的 A* 本体与 23 的 `EffectiveWalkable`(归各自 epic)
- 9 的在场判定实现与 CAP 守恒(归 disease-sim epic)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | Seeking 病人,速度 = 3/4 格/tick | 连跑 4 tick | 恰走 3 格(第 4 tick 走第 3 格 + acc 余 1/4),每 tick ≤1 格 |
| TC-2 | 切 Bedridden 10 tick 再切回 | 观察 acc | 冻结期不变;恢复即续用旧 acc(不瞬移) |
| TC-3 | 冻结 100 tick 后解冻 | 对比「从不冻结」 | 位姿差 = 缺失的积分,可重放复现;无补算尖峰 |
| TC-4 | 同事件流同配置两跑 | 轨迹哈希 | 逐位一致(AC-13-E2) |
| TC-5 | 在场集 24 人 + 第 25 人入场 | 决策 tick | 第 25 人不进决策集(消费 9 的判定,13 无自主增减) |
| TC-6 | spawn_anchor 在无生态区命中格 | HomeRegion | 取 NONE=−1 哨兵口径(承 worldeco Story 002),无异常 |
| TC-7 | 字典插入序打乱的同一病人集 | 求值 | 结果不变(升序 id 钉死) |

**Edge cases**: 目标医馆不可达(`PathFailed`)时 Seeking 的停留口径;`PERCEPT_R²` 溢 int64(承量程断言);读档重播种后首 tick 的行为连续。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/PatientAI/spatial_behavior_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

**Depends on**: Story 001(BehaviorState)· 系统 6 的 `EcozoneOf` / `spawn_anchor`(world-ecozones Story 002/003)· 9 的在场判定接口 · 整数导航格数据(23/27 基础设施,消费级)
**Unlocks**: Story 003(ViewState 与 cue 的格/相位输入)· Story 004(重建与联机单跑验证的判据对象)

---

## Completion Notes
