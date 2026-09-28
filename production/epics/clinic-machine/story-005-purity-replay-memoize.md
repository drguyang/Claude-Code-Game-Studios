# Story 005: 纯函数重放与 Memoize 失效

> **Epic**: 医馆即机器
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28
## Context

**GDD**: `design/gdd/clinic-machine.md`(24 无状态机铁律 · AC-24-04 同进程双求值逐位 · AC-24-11 · EC-24-05 表现层 Memoize)
**Requirement**: TR-clinic-002(24 无自有状态:加成输出 = 上游量的 Memoize 纯函数,失效键显式)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(主): 确定性模拟;ADR-016: 分层确定性(三源不变量)
**ADR Decision Summary**: 24 的全部输出 = 布局(23 的 structure_at)与烘焙表(版本化数据)的纯函数 ⇒ 满足 ADR-016 §一 三源不变量,无任何第四来源(墙钟/表现态/UnityEngine.Random)。缓存属**表现层** Memoize,键 = `Structure*` 事件(EC-24-05),sim 侧不留缓存。跨平台逐位一致归 ADR-012 黄金夹具矩阵(另批实测,禁借绿)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 同进程双求值/重放对拍 = 纯 C# 断言;不触 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 同一布局输入两次独立求值逐位相同(raw long 相等);缓存失效键 = 布局变更事件的显式全序键
- Forbidden: sim 程序集内任何静态/实例缓存(24 无状态);以「帧驱动轮询重算」替代事件失效;IL2CPP 侧有符号溢出未走 hi/lo 纪律
- Guardrail: 跨平台对拍 AC 保持 NOT-RUN 直至 ADR-012 矩阵回填,本 story 只承诺同进程面

---

## Acceptance Criteria

*From GDD `design/gdd/clinic-machine.md`, scoped to this story:*

- [ ] AC-24-04:同进程对同一布局双求值,room 析出 + 三乘子输出逐位相同(含 contexts[] 顺序确定)
- [ ] AC-24-11:布局经 23 变更(建造/拆除事件)后重算,旧缓存条目失效,新值 = 直接对新布局求值(无中间态残留)
- [ ] EC-24-05:Memoize 位于表现层,键 = `Structure*` 事件;sim 程序集反射扫描断言 24 类型无字段级可变状态(无状态 = 可断言,非口头约定)
- [ ] 纯函数输入封闭:求值函数签名仅收 (布局快照, cooked 表句柄),测试以类型面断言不收 tick 之外的时变量
- [ ] 重放等价:构造「先变更后建馆」与「直接以终态布局求值」两路径,输出相同(派生态可重建)

---

## Implementation Notes

*Derived from ADR-005/016 Implementation Guidelines:*

1. 双求值测试用同一 fixture 布局跑两遍(独立实例化,禁复用第一次的结果对象)。
2. contexts[] 输出顺序钉死为确定性序(表 ordinal 升序),防集合迭代序漂移(承 ADR-024/25 同款纪律)。
3. 缓存实现落 `Gameplay.Presentation`:监听 `Structure*` 事件流,键 = 事件全序键,值 = ClinicEnvDto 快照。
4. 「无状态」断言 = 反射扫描 24 的 sim 类型:无 mutable 字段、无 static;该断言入构建期或 EditMode 皆可,择一登记。
5. 提醒实现者:若日后需改 `TICK_SECONDS` 等全局量,24 不感知 —— 它的输入不含 tick 频率。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 002]: 析出算法本体
- [Story 003]: 求值公式本体
- [Story 004]: 交付契约
- ADR-012 批:三格矩阵跨平台逐位实测(NOT-RUN 直至该批复跑)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(双求值)**: 逐位相同
  - Given: fixture 布局 L
  - When: `Evaluate(L)` 两次(新实例)
  - Then: 全部输出 raw long 相等,contexts[] 序一致
  - Edge cases: 空布局(ROOM_NONE)双求值同哨兵
- **AC-2(事件失效)**: 变更后重算
  - Given: 表现层缓存已有 L₁ 条目
  - When: 注入 `Structure*` 变更事件(L₂ = L₁ + 一格家具)
  - Then: 键失效、重算得 L₂ 值;查询 L₁ 不返回陈旧命中
  - Edge cases: 同 tick 连两变更事件 → 只要求终态一致(幂等)
- **AC-3(无状态断言)**: 反射扫描
  - Given: 24 sim 程序集全部公开类型
  - When: 扫描字段
  - Then: 无 mutable/static 状态字段
  - Edge cases: readonly 结构体常量合法(非状态)

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/ClinicMachine/clinic_purity_replay_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002 + 003 + 004(全链稳定)
- Unlocks: Epic 验收(24 的确定性叙事闭合);系统 23 联动的集成测试可引用本夹具

---

## Completion Notes

*(留空 — story 关闭时回填)*
