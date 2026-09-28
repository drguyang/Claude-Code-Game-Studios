# Story 002: 掉落清单流派生与回捡

> **Epic**: 死亡与复活
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28
## Context

**GDD**: `design/gdd/death-and-respawn.md`(规则三 掉落:DropSpawned/DropClaimed · death_cell = 最后 ActorCellEntered · 清单由流派生 · 位置从 7a 快照 · 回捡必须走到死亡格)
**Requirement**: TR-death-003(death_cell 由 sim 整数格派生,禁读表现态连续位置) · TR-death-004(复活清单/掉落回收 = 事件流重放重建;位置不快照 —— 格锚点 + 确定性格内偏移)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-009(主): 掉落实体身份进流/位置表现;ADR-020: ActorCellEntered 写方 = 1(主机)
**ADR Decision Summary**: `DropSpawned {instance_id, spawn_anchor = death_cell, item_key, qty}`(+ 可选 `DropClaimed`)进世界流;清单真相 = `InventoryOf(player)` 读世界流(20 的 AC-20-03 口径),**位置永不进流**(7a 快照 = 格锚点 + BCL 整数哈希的确定性格内偏移,ADR-023 ⑦ 同族);**29 零铸造 instance_id**(发号 = 20/`IIdAuthority`,主机唯一 Append);回捡约束 = 必须走到死亡格,禁远程拾取(20 R7);P0 无位置标记(43 P1a)。B4 订正:death_cell 不读实时物理。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 逻辑纯整数(LOW);MEDIUM = 与 7a 快照/ADR-023 读档重建的集成面(Addressables 6.2+ 行为须实测,归 7a/23 epic)。

**Control Manifest Rules (this layer)**:
- Required: 逐件 DropSpawned(spawn_anchor 整数格);DropClaimed 走 4 的意图 + 判距 + 宽容半径(ADR-009 §七 拾取三段式同构);死亡格以外无法 claim 该批 instance_id
- Forbidden: Vector3/float 进 DropSpawned 载荷;29 侧自造 instance_id 或直写 20 的库存;远程/传送后隔空拾取
- Guardrail: ⚠️ AC-29-02/17 的「死亡时刻」输入挂 DeathCandidate —— BLOCKED-BY-9(与 story-001 同因);以 PlayerDied 桩时刻先行验证清单形状

---

## Acceptance Criteria

*From GDD `design/gdd/death-and-respawn.md`, scoped to this story:*

- [ ] AC-29-02(半边,⚠️ BLOCKED-BY-9):PlayerDied 时逐件写 DropSpawned,`spawn_anchor` = 该玩家世界流**最后一条 ActorCellEntered** 的格(非实时物理位置)
- [ ] 空背包 ⇒ 零 DropSpawned,但 PlayerDied 照有(story-001 AC-29-03 配对)
- [ ] 清单派生断言:掉落集 = `InventoryOf(player)@死亡tick` 的流派生,重放逐位重建(无独立「掉落清单」字段)
- [ ] instance_id 来源断言:29 代码路径零 `IIdAuthority.Next()` 调用(铸造归 20);载荷 id 与 20 库存行一致
- [ ] 回捡约束:玩家不在死亡格 ⇒ claim 拒绝;到格 ⇒ DropClaimed 入流、库存恢复(经 20)
- [ ] 位置非快照:存档载荷不含掉落连续坐标;读档后位置 = 格锚点 + 确定性格内偏移(两次读档逐位同)
- [ ] DropClaimed 幂等:同 instance_id 二次 claim 拒收

---

## Implementation Notes

*Derived from ADR-009/010 Implementation Guidelines:*

1. 死亡时刻编排:读世界流反查最后 ActorCellEntered → 取 InventoryOf → 逐件 Append DropSpawned(顺序 = item 槽序的确定性枚举,禁字典序)。
2. DropClaimed 由 4 的拾取意图触发、主机判距(当前格 == spawn_anchor ∧ 宽容半径),29 只提供「死亡掉落」这批 instance 的注册语义。
3. 性格内偏移 = BCL 整数哈希(instance_id, world seed 派生),**不引入第二随机源**(ADR-016 三源不变量;ADR-023 ⑦ 同款先例)。
4. 迁移/重放测试:新主机 CatchUp 后掉落集与旧主机逐位同(含 claim 后)。
5. 数值无(件数 = 背包容量面归 20 数值轮);P0 明确不做地图标记(43)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: PlayerDied 发射本身
- [Story 005]: 复活(掉落与复活解耦:死亡格物品不会因复活消失)
- [Story 006]: 掉落物视觉(表现态)
- 系统 20:库存/instance_id 铸造;系统 4:拾取意图;系统 7a:序列化/快照实现

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(death_cell 真源)**: 最后格 vs 实时位置
  - Given: 玩家 ActorCellEntered 序列 …(5,0,3),表现态位置在 (5.9,0,3.4)
  - When: 注入死亡
  - Then: spawn_anchor = (5,0,3)(整数格);夹具证明未读连续坐标(桩 Motor 位置给脏值不影响结果)
  - Edge cases: 从未发过 ActorCellEntered(开局即死?)→ 出生格兜底,登记 EC 分支
- **AC-2(重放重建)**: 新主机 CatchUp
  - Given: 含掉落+部分 claim 的流
  - When: 迁移重放
  - Then: 未 claim 集逐位同;claimed 件已回库存;性格内偏移两次相同
  - Edge cases: 迁移在途 claim → 判重键拒/收二选一,不得双收
- **AC-3(回捡距离)**: 隔格拒绝
  - Given: 玩家在死亡格邻格
  - When: 发 claim 意图
  - Then: 拒;移入死亡格后 claim 成功且入流
  - Edge cases: 宽容半径边界值(夹具钉符号化格距,实值归数值轮)

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/DeathRespawn/death_drop_stream_test.cs` — must exist and pass;AC-29-02/17 全链联调维持 BLOCKED-BY-9 标注直至 9 侧回写
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(PlayerDied 时刻);系统 20 库存读 + IIdAuthority(既有);系统 4 拾取意图管线(未就绪则 claim 以接口桩测)
- Unlocks: Story 005(复活后回捡动线联测);与 43 P1a 的标记预留接口(不实现)

---

## Completion Notes

*(留空 — story 关闭时回填)*
