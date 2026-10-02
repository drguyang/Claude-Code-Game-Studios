# Story 001: PlayerDied 登记与死亡结算入口

> **Epic**: 死亡与复活
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 5h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28
## Context

**GDD**: `design/gdd/death-and-respawn.md`(规则二 触发 = 9 的 DeathCandidate · 规则三 PlayerDied 无条件发射 · 装配三分:结算 sim / 命令边界 / 呈现表现层)
**Requirement**: TR-death-001(29 只消费 DeathCandidate 判定输入,不自行判死) · TR-death-002(PlayerDied 世界流事件,不落病史流) · TR-death-007(死亡线三半边落点)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-009(主): 三态分类与流归属;ADR-024: Kind 单一登记真源
**ADR Decision Summary**: `PlayerDied` 属世界流(模拟态进流),经 `entities.yaml` 具名 + kindgen 路由;主机唯一 `Append`(承 ADR-005);触发判据 = 9 的 `DeathCandidate` 经 `SelfLimited(entity,d)` 逐实体门(OQ-25-1 路甲)—— **29 不实现任何「生命归零」判定**;移动禁用 = 1 读 PlayerDied(29 不在 MotorSuppressed 白名单 {4,10,25})。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 事件登记/路由/消费均为纯 C# 契约;不触 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: `PlayerDied {player_id, tick}`(整数域);空背包也照发(无条件发射);IsDowned 经 ADR-001 第二 QoS 供表现层
- Forbidden: 29 侧判死公式;把 PlayerDied 写进病史流;29 直接调 Motor 锁移动
- Guardrail: ⚠️ AC-29-01/02/05/08/10/17 依赖 9 的 `SelfLimited` 落地 —— **BLOCKED-BY-9**,联调前禁记绿(本 story 以接口桩验证 29 侧消费形状)

---

## Acceptance Criteria

*From GDD `design/gdd/death-and-respawn.md`, scoped to this story:*

- [ ] AC-29-01(半边,⚠️ BLOCKED-BY-9 整链):29 收到 `DeathCandidate` 信号即发 `PlayerDied`;**夹具以桩注入 DeathCandidate** 验证消费形状;与 9 真件联调前本条不记绿
- [ ] AC-29-03:空背包玩家死亡仍产生 PlayerDied(无条件路径;掉落清单可为空集)
- [ ] AC-29-13:29 输入 Kind 集 ⊆ {DeathCandidate} 正向白名单;零非 DeathCandidate 输入订阅(断言 29 的事件源注册表仅含 DeathCandidate)
- [ ] PlayerDied 路由断言:落世界流、不落病史流(扫流内 Kind 分布;kindgen 生成物核对 entities.yaml)
- [ ] 装配三分面:结算代码住 `Sim`(门 A 反射断言零 UnityEngine)、PlayerDied→1 的消费信号住边界、呈现订阅在 `Gameplay.Presentation`
- [ ] 主机唯一 Append:客户端侧无 PlayerDied 铸造路径(29 的上行 = 格/意图,非结算)
- [ ] 移动禁用接线:1 读 PlayerDied 的依赖行存在且 29 零 MotorSuppressed 调用(负断言)

---

## Implementation Notes

*Derived from ADR-009/024 Implementation Guidelines:*

1. `entities.yaml` 增 `PlayerDied`(stream: world;author: 29;payload 整数域)—— kindgen A1–A5 过门。
2. 触发接口:`IDeathCandidateSource`(9 侧提供)→ 29 的 `OnDeathCandidate(playerId, tick)`;29 内部只有「转发 + 发射 + 编排后续 story-002/003/004/005」,无生命值读取路径。
3. DeathCandidate 与 PlayerDied 的去重:同一 candidate 只铸一条 PlayerDied(判据 = 流前缀纯函数;冷却在 story-004,不在本层混入)。
4. 9 未就绪期间:桩 = 手动注入 candidate 的测试 harness;任何「等 9 好了再说」的绿都不许出现(承「No Borrowed-Green」纪律)。
5. 存档面:PlayerDied 是世界流事件,序列化归 7a(ADR-010 义务表),本 story 不建 codec。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 002]: 掉落(DropSpawned 编排)
- [Story 003]: 掉级
- [Story 004]: 死亡冷却
- [Story 005]: 复活传送
- [Story 006]: 呈现白名单
- 系统 9:DeathCandidate/SelfLimited 本体(⚠️ 未落地 = 本 epic BLOCKED 清单根因);系统 1:移动禁用实现

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(无条件发射)**: 空背包死亡
  - Given: 玩家背包空集,注入 DeathCandidate
  - When: 结算
  - Then: 世界流恰新增一条 PlayerDied;无其他事件
  - Edge cases: 同 candidate 重注入 → 幂等(前缀判据);无背包系统在场时仍过
- **AC-2(流别正确)**: 路由扫描
  - Given: 跑一段含死亡的场景
  - When: 扫三流
  - Then: PlayerDied 只出现在世界流;病史流零该行
  - Edge cases: PatientId 字段用玩家 id 空间(共用 IIdAuthority,ADR-006 Amendment B 扩大面)
- **AC-3(零判死)**: 29 无生命值路径
  - Given: 29 的 sim 类型集
  - When: 引用扫描
  - Then: 无 IVitalsQuery.生命类读取(判死输入只有 DeathCandidate)
  - Edge cases: 调试视图不算(呈现侧,门 A 外)

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/DeathRespawn/player_died_settlement_test.cs` — must exist and pass(桩 harness);AC-29-01 全链联调另挂 ⚠️ BLOCKED-BY-9 标注
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 无前置(epic 入口);9 的 DeathCandidate 契约(⚠️ 未落地,桩先行);kindgen(既有)
- Unlocks: Story 002/003/004/005/006(全部挂 PlayerDied 时刻)

---

## Completion Notes

*(留空 — story 关闭时回填)*
