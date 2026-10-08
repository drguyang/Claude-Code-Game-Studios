# Story 004: 确定性移动与寻路 —— 定点累加器步进、整数 A* 与路径重规划

> **Epic**: 敌人 AI
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 8h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/enemy-ai.md`(§Detailed Rules 规则十二 寻路输入 = 23 的 `EffectiveWalkable` · 规则十三 轮询重规划、23 无 push · 规则十四 sim 整数 A* / NavMesh 仅表现、漂移单向 snap · 规则十五 LogiPose 禁反推 · §Formulas F-27-4(累加器 + `Moving(e)` 定义)/ F-27-7(A* 全规格:堆键 `(f,h,Z,Y,X)` 全序、OCTILE 整数启发式、`NODE_BUDGET`、`path_cursor` 重算保位)· C 组 AC-27-06/16/17/18/19/20/21)
**Requirement**: TR-enemy-011(寻路输入 = `EffectiveWalkable(cell) := Nav ∧ ¬Overlay.blocked`,不读 `Nav` 原件)· TR-enemy-012(重规划由 27 轮询触发;不自建 blocked 副本、不监听 `Structure*`)· TR-enemy-013(sim 整数 A*,NavMesh 仅连续位移;漂移处置单向)· TR-enemy-014(`LogiPose=(WorldPos, byte Facing)` 逐 tick 整数推进;禁从 Transform 反推格)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-016(主): §五 整数导航格寻路;ADR-015: 单一整数格
**ADR Decision Summary**: sim 走整数导航格确定性 A*,NavMesh 只把逻辑路径变好看;表现层偏离逻辑路径 ⇒ 表现层 snap,**禁 sim 跟随表现层**(float 入 sim)。导航格由 ADR-022 关卡工具切片产出(`world_nav_{chunk}`);P0 `cost(step)` = 常量(`O-27-9` 对账:6 侧「代价」字段以 27 口径为准)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(sim A*)/ MEDIUM(表现层 NavMesh tile 动态更新成本,ADR-016 唯一 Engine Risk 来源 —— 归实现期实测)
**Engine Notes**: A* 本体纯整数(BCL);NavMesh 侧为长期稳定 API,但 6.3 动态 tile 成本须 profile(不阻塞本 story 的 sim 判据)。

**Control Manifest Rules (this layer)**:
- Required: 堆键 = 5 元整数元组 `(f,h,Z,Y,X)` 全序(扩展顺序唯一);`h = OCTILE × STEP_COST` 整数近似禁 sqrt;`expand_limit = NODE_BUDGET` 超限返回**空路径**(= PathFailed,非异常);`acc` 只在 `Moving(e)` 真的 tick 前进、转假保持;构建断言 `ENEMY_SPEED < FIX_ONE` 与 `0 ≤ acc < FIX_ONE` 恒成立
- Forbidden: `sqrt` / float 启发式 / 读 `Nav` 原件绕过 23 合成点 / 监听 `Structure*` 维护本地 blocked 副本(第二真源)/ 重算时 `path_cursor` 清零(= 把敌人瞬移回起点)
- Guardrail: 目标不变 ∧ 路径未失效 ∧ 未到 `REPLAN_PERIOD` ⇒ 零 A* 调用;`NODE_BUDGET` 有正确性面(OQ-27-7 半规格)—— 量级须在写 A* 前定(`24 实体 × 20 Hz` 定标基础已标定)

---

## Acceptance Criteria

*From GDD `design/gdd/enemy-ai.md`, scoped to this story:*

- [ ] A* 确定性:固定起终点重复求解逐格相同;堆上不存在两个 `cmp == 0` 元素(全序断言)(AC-27-06)
- [ ] 寻路读取来源**恰 = {`EffectiveWalkable(cell)`, `world_nav_{chunk}` 烘焙产物}** 正面白名单;`Nav` 原件不在白名单(AC-27-16)
- [ ] 轮询重规划:路径中段被玩家放置模块封格 ⇒ **下 tick** 重规划且新路径不含 blocked 格;拆墙后下一 tick 自然恢复,无恢复通知、无事件订阅(AC-27-17,EC-27-06/09)
- [ ] 被围死:`PATH_FAIL_GRACE` 到点进 `Disengage`,期间**零瞬移**(逻辑格连续)(AC-27-18);当前格恰被封时不推出墙外(EC-27-08)
- [ ] 步进性质测试:任意合法 `ENEMY_SPEED < FIX_ONE` × 任意 `Moving` 真假序列(含冻结/Alert/Engage/Down 进出)下,`0 ≤ acc < FIX_ONE`、`LogiPose.Cell` 整数性、每 tick 位移 ≤ 1 格三者恒成立(AC-27-19 —— 原判据漏 Moving 面已补)
- [ ] 重算保光标:新路径游标 = 离当前格最近节点 ≠ 0;新路径首段不经过已走过的格(AC-27-20);重算失败时 `path_cursor` 不动、走完旧路径余段
- [ ] A* 假阳性防护:完全无阻挡开阔地形 + 任意远合法目标 ⇒ 返回非空路径(性质测试,对任意 `NODE_BUDGET ≥ 1` 在有解图上)(AC-27-21)
- [ ] 禁反推:代码不存在由 `Transform.position` 推逻辑格的路径(反射 + 负面夹具,AC-27-02 扩面)

---

## Implementation Notes

1. `Moving(e) := ¬Frozen ∧ ¬Down ∧ State ∈ {Patrol,Chase,Flank,Disengage}` —— Alert/Engage 不推进逻辑位移且 `acc` 保持(打十 tick 脱离即瞬移一格的根因)。
2. `OCTILE`: `m1/m2/m3` 排序 + `(m3 + D2·m2/1024 + D1·m1/1024)` 整数链,D2/D1 取整数比例常量(GDD 给 1414/1000 量级示意,定值归数值轮);全 int64 求值。
3. `path_cache` 住敌人实体字段;失效判据 = 路径格集 ∩ Overlay 新 blocked ≠ ∅(轮询 23 的布尔查询)。
4. A* 单次求值预算与 `REPLAN_PERIOD` 的量级按「24 实体 × 20 Hz」定标(OQ-27-7 的正确性半边,写码前定;值仍归数值轮)。
5. 表现层 NavMeshAgent snap 逻辑归表现程序集;本 story 只交付 sim 侧与 `LogiPose` 输出(Story 005 的 DTO 消费)。

---

## Out of Scope

- [Story 003]: 态机决定「走不走」(本 story 执行位移)
- [Story 005]: `EnemySignalDto` 投喂 `path_next`、遭遇生命周期
- 23 的 `EffectiveWalkable` 合成本体(归 building epic;此处只读其接口)
- NavMesh tile 动态重烘成本实测(实现期 profile,登记不阻塞 sim 判据)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 同一起终点 100 次求解 | 比对路径 | 逐格相同;堆全序检查无等键元素 |
| TC-2 | 敌沿路径走,玩家中段盖墙 | 下 tick | 重规划,新路径绕开该格,无瞬移 |
| TC-3 | 敌恰站在刚被封的格内 | 连续 tick | 不推出、不消失;PathFailed 计时→宽限→脱离 |
| TC-4 | speed=7/8 格/tick,走 8 tick | 记录 Cell | 恰 7 格(第 8 tick 无位移,acc 余),每步 ≤1 |
| TC-5 | Engage 20 tick(不 Moving)后转 Chase | 首 tick 位移 | ≤ 1 格(acc 保持未攒爆) |
| TC-6 | 开阔地 200 格外目标,NODE_BUDGET 任意合法值 | A* | 非空(无假阳性) |
| TC-7 | 重算发生在敌已走半程 | path_cursor | ≠ 0,指向新路径最近节点 |

**Edge cases**: `NODE_BUDGET` 触顶在**有解窄迷宫**(返回空 = 设计内,PathFailed 接住);目标格本身 blocked;两敌人同 tick 重规划(预算互不挤占由 tick 步相位保证)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/EnemyAI/deterministic_pathing_test.cs` — must exist and pass
**Status**: [x] Complete — test file exists (`unity/Assets/Tests/EditMode/EnemyAI/deterministic_pathing_test.cs`)

---

## Dependencies

**Depends on**: Story 003(`Moving`/冻结/态语义)· Story 001(`STEP_COST`/`ENEMY_SPEED` 参数行)· 23 的 `EffectiveWalkable` 接口(building epic;假表可先行)· `world_nav_{chunk}` 烘焙(ADR-022 工具,夹具可先行)
**Unlocks**: Story 005(`LogiPose`/`path_next` 进 DTO)· 表现层 NavMesh 接线 · 「玩家盖墙真的挡住人」基础设施层幻想的 sim 半边

---

## Completion Notes

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `PathNode` — 路径节点
- `PathResult` — A* 寻路结果
- `EnemyPathing` — 确定性 A* 寻路器（FindPath / ComputeOctileHeuristic / StepAccumulator / ValidateAccumulator）
- 堆键 = 5 元整数元组 (f,h,Z,Y,X) 全序
- OCTILE 整数启发式
- NODE_BUDGET 超限返回空路径
- path_cursor 重算保位
- 测试: 7 条单元测试（全部通过）

**Deviations**: 
- A* 为简化版（6 方向邻居），完整版需要 26 方向或导航格切片

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/EnemyAI/deterministic_pathing_test.cs` — 7 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，无 BLOCKING 问题

**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
