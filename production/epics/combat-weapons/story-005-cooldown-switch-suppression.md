# Story 005: 冷却 / 切换 / 压制(S-1/S-2/S-3 与 IsSuppressed)

> **Epic**: 格斗与武器线
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28
## Context

**GDD**: `design/gdd/combat-and-weapon-lines.md`(F-25-3 冷却 · F-25-4 切换 · F-25-5 压制 · 三状态 S-1/S-2/S-3 · AC-25-3-* / AC-25-4-*)
**Requirement**: TR-combat-018(冷却判据 cd_eff(d)=min 逐动作 + 逐 bar 组合上界断言;F-25-8 乘法形式判据,零除法路径) · TR-combat-008(IsSuppressed 公开只读查询;禁轮询;生效时刻 = 求值 tick) · TR-combat-009(25 = MotorSuppressed 唯三调用者之一;只锁走位/Jump)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(主): 确定性;ADR-020: 玩家控制器
**ADR Decision Summary**: 冷却 ≥1 tick 硬门;落空(whiff)照常耗冷却(AC-25-3-02/03);切换冷却语义 = **仅当前线动作可执行**(可执行性拒绝,不是给旧线动作「推冷却」,AC-25-3-06/07);压制释放判据 `t ≥ until ∨ Down(victim)`(Down 查询归 9);**压制态永不进流**(双测试:grep entities.yaml Kind 枚举 + 扫流内无压制载荷,AC-25-4-04);`IsSuppressed` 公开只读 —— 读者仅表现层与调试,**sim 决策禁用**(压制锁走位不锁出手,AC-25-4-03);`MotorSuppressed` 的 25 调用点走 ADR-020 白名单 {4,10,25}。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯整数状态派生 + 只读查询;`ITeleportCommandSink`/Motor 接口调用面为已登记形状,不触 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 压制刷新 = **max 不 sum**(AC-25-4-01/02);压制生效时刻 = 求值 tick;三态机 S-1(占用窗)/S-2(压制窗)/S-3(切换窗)全由事件流 + tick 派生
- Forbidden: 压制写任何流事件;sim 决策读 IsSuppressed(仅呈现/调试);duration 相加式刷新
- Guardrail: SWITCH_COOLDOWN ≤ HALF_LIFE_MAX 的构建期联动断言(A27,乘积式,归 story-006 的 F-25-8 批同跑)

---

## Acceptance Criteria

*From GDD `design/gdd/combat-and-weapon-lines.md`, scoped to this story:*

- [ ] AC-25-3-02/03:落空动作照常进入占用/冷却窗(whiff 不豁免);cooldown_ticks=1 为下界硬门(表门在 story-001,行为面在此)
- [ ] AC-25-3-06/07:切换武器线后 SWITCH_COOLDOWN 窗内,旧线动作 = **不可执行**(非推迟执行、非推冷却);新线动作不受旧线占用牵连
- [ ] AC-25-4-01/02:同目标叠加压制时 until = max(两窗上界),**不 sum**;第二次压制不延长总时长超过单窗
- [ ] AC-25-4-03:被压制者仍可出手(锁移动不锁攻击)—— 与 story-002 AC-25-0-02 双侧复验
- [ ] AC-25-4-04:压制零入流 —— entities.yaml Kind 枚举中无压制类 Kind + 重放流中无压制载荷(双测试)
- [ ] `IsSuppressed(actor)` 只读查询在 t ∈ [onset_tick, until) 返回 true;`Down(victim)` 提前释放路径可测
- [ ] 25 的 MotorSuppressed 调用点存在且在白名单内(接口消费面断言,实现归 1 的联动)

---

## Implementation Notes

*Derived from ADR-005/020 Implementation Guidelines:*

1. 三态均为**派生窗口谓词**(非存储):`Occupied(a)@t`(story-002 定义)、`Suppressed(v)@t := ∃ 压制 onset o: o.tick ≤ t < max-until(o) ∨ Down(v)@t`、`SwitchLocked(a, line)@t`。
2. 压制来源 = 动作表的 attack_class/suppression 标记(徒手「压制」动作);载荷含 duration_ticks(表值,归数值轮)。
3. 刷新语义实现为「同 (actor,victim) 取 max 释放界」的纯函数折叠,注意多条历史压制共存。
4. IsSuppressed 的暴露面 = 边界层只读查询(表现层/UI/调试);加编译期护栏:Sim 程序集内部无任何调用点(引用扫描)。
5. PlayMode 侧 AC-25-4-03 标 ★(压制中出手),EditMode 以窗口谓词等价断言,PlayMode 归 story 验收批。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 002]: 占用门的出手拦截逻辑(本 story 只加切换窗的可执行性面)
- [Story 003]: 命中合取项
- [Story 004]: magnitude
- [Story 006]: onset 写入与构建期平衡断言批(A25a/b、A26、A27 的乘积式)
- 系统 1:MotorSuppressed 的实现与相机/移动锁定本体;系统 9:Down 真相

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(whiff 耗冷却)**: 打空不豁免
  - Given: 目标在范围外,意图合法
  - When: 求值 → 落空
  - Then: 无 onset,但占用窗照常开启(下一意图在窗内被丢)
  - Edge cases: 解锁拒绝(不可执行)不耗冷却 —— 与落空路径严格区分
- **AC-2(压制 max 刷新)**: 叠加压制
  - Given: v 在 t₀ 被压制(duration=10),t₀+3 再被压制(duration=10)
  - When: 查询 Suppressed(v)@t
  - Then: 释放界 = max(t₀+10, t₀+13)=t₀+13(非 t₀+23);Down(v) 于 t₁ 提前释放
  - Edge cases: 同 tick 双压制 → max 退化等值;流重放逐位同
- **AC-3(零入流)**: 压制不写流
  - Given: 多次压制场景跑完
  - When: 扫该段三流载荷 + entities.yaml Kind 枚举
  - Then: 无「Suppressed」Kind;onset 事件之外无压制相关事件
  - Edge cases: 压制由 onset 的 attack_class 派生可读 —— 派生≠入流

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/Combat/combat_cooldown_switch_suppress_test.cs` — must exist and pass;AC-25-4-03 的 ★PlayMode 面 `unity/Assets/Tests/PlayMode/Combat/combat_suppress_attack_playmode_test.cs`(另批,未跑记 NOT-RUN)
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002(占用门合流)、Story 003(Down 查询消费面);9 的 Down(⚠️ BLOCKED-BY-9 的提前释放分支 —— 桩测可先行,联调不记绿)
- Unlocks: Story 006(压制 onset 的载荷面)、1 的 MotorSuppressed 联测

---

## Completion Notes

*(留空 — story 关闭时回填)*
