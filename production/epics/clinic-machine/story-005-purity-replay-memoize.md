# Story 005: 纯函数重放与 Memoize 失效

> **Epic**: 医馆即机器
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-10-15  <!-- 2026-10-25 重写轮:自 2026-10-02 刷至现行版(承 S20-1 用户裁定 [A] 先例;增量仅 Cross-Cutting 之七/之八两节) -->
> **Last Updated**: 2026-10-25
## Context

**GDD**: `design/gdd/clinic-machine.md`(24 无状态机铁律 · AC-24-04 同进程双求值逐位 · AC-24-11 · EC-24-05 表现层 Memoize)

> 🔁 **2026-10-25 重写轮(用户裁定「先重写 story 卡再开工」)—— 卡面三处编号/口径订正(原文均不删)**
>
> 开工前对 GDD 逐条实测,查出三处**卡面与 GDD 现行文本不符**:
>
> | # | 卡面原文 | GDD 现行 | 处置 |
> |---|---|---|---|
> | ① | AC 列表把「布局变更后旧缓存失效」记作 **`AC-24-11`** | **`AC-24-11`(`:411`)= 外壳格 / 空地板格不计分、不参与连通**;「缓存失效」的权威出处是 **`EC-24-05`(`:291`)+ States 注(`:123-127`)**,GDD **没有任何 AC 编号**承载它 | 本条 AC 改标 **`EC-24-05`**;AC-24-11 的语义**已由 story-002 完成**(其 AC 列表第 3 条逐字:「外壳格与空地板:不产生房间成员、不阻断两簇判定」)—— 本卡不再重复承担 |
> | ② | Context 与 QA 对齐注写「AC-24-11 布局源用真 `World`」 | 同上 —— 该布局源判据属 **EC-24-05** | 全文该处一律读作 `EC-24-05` |
> | ③ | QA 对齐注与 sprint-20 卡面写「AC-24-10 / 24-11 逐条判定」 | **`AC-24-10`(`:410`)= ≥2 个互不连通家具簇 ⇒ 输出逐位 (0,0)** —— **已由 story-002 完成**(其 AC 列表第 1 条);本卡 Context 原写「AC-24-11」而非 24-10 | 本卡面**不含 AC-24-10**;GDD `:404` 的 **AC-24-04**(双求值对拍)才是本卡第一条判据 |
>
> **本卡重写后的判据集(四条)**:`AC-24-04`(双求值逐位)· **`EC-24-05`**(缓存失效键 + 缓存前后逐位一致)· 无状态反射扫描(24 sim 类型零 mutable/static)· 纯函数输入封闭(三源,ADR-016 §一)。
> ⚠️ **AC-24-10 / AC-24-11 的覆盖归属 = story-002(已 Complete)**,本卡不重复;若 `story-done` 核覆盖矩阵时发现 002 的覆盖不足,那是 **002 的复核项**,不在本卡扩面。
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
- [ ] ~~AC-24-11~~ 🔁 **改标 `EC-24-05`(2026-10-25 重写轮,见上方订正表 ①)**:布局经 23 变更(建造/拆除事件)后重算,旧缓存条目失效,新值 = 直接对新布局求值(无中间态残留)
- [ ] EC-24-05:Memoize 位于表现层,键 = `Structure*` 事件;sim 程序集反射扫描断言 24 类型无字段级可变状态(无状态 = 可断言,非口头约定)
  - 🔁 **2026-10-25 重写轮**:本条与上一行的关系 = **上一行是本条的「失效键行为」半边**,本条是「位置归属 + 无状态可断言」半边 —— **同一 EC 的两面,合并验收,不拆卡**。
- [ ] 纯函数输入封闭:求值函数签名仅收 (布局快照, cooked 表句柄),测试以类型面断言不收 tick 之外的时变量
- [ ] 重放等价:构造「先变更后建馆」与「直接以终态布局求值」两路径,输出相同(派生态可重建)

---

## Implementation Notes

*Derived from ADR-005/016 Implementation Guidelines:*

1. 双求值测试用同一 fixture 布局跑两遍(独立实例化,禁复用第一次的结果对象)。
2. contexts[] 输出顺序钉死为确定性序(表 ordinal 升序),防集合迭代序漂移(承 ADR-024/25 同款纪律)。
3. 缓存实现落 `Gameplay.Presentation`:监听 `Structure*` 事件流,键 = 事件全序键,值 = ClinicEnvDto 快照。
   > 🔁 **2026-10-26 评审回写(W-2,原文不删)**:本条**后半句未按原样实现** —— 实现缓存的是
   > `Sim.Clinic.ClinicEnvEvaluation`(24 的纯输出),**不是** `ClinicEnvDto`。原因是 DTO 投影需要
   > 两处**全库未裁**的映射:① `RoomNameId` 的 int 房号 ↔ `ClinicRoomId(High,Low)`;
   > ② `ContextIds` 词表 ordinal ↔ `ClinicContextRow.ContextId` 的 string(词表 size 未定)。
   > 发明映射 = 造第二真源 ⇒ **未擅自发明**,投影归装配轮(证据件 §六-④ 已登记 NOT-RUN)。
   > 实现落点:`Gameplay.Presentation/ClinicEnvironmentMemo.cs`。评审原件
   > `production/qa/evidence/review-s20-2-clinic005-2026-10-26.md` §二 W-2。
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

> **QA plan 对齐(2026-10-25 /qa-plan sprint-20)** —— 证据件:`unity/Assets/Tests/EditMode/ClinicMachine/clinic_purity_replay_test.cs`;估 ~6 条。AC-1 即 AC-24-04(contexts[] 序钉表 ordinal 升序,输入顺序无关 ⇒ 输出逐位同);AC-2 即 AC-24-11(Memoize 键 = Structure* 事件,失效重算结果与直算**逐位一致**,另测重放等价);AC-3 即无状态反射扫描 + 纯函数输入封闭(三源,ADR-016 §一)。Edge 补:同 tick 多条 Structure* 事件(失效批量)。**TD-building-01 残余两半就地裁定**(sprint-20 S20-2 note):补真 `World` 端到端(另立 S20-2a)或继续 Fake(**AC-24-11 不得记绿**)—— 二选一,不得默认扩面。跨平台逐位 NOT-RUN 归 ADR-012 批,本件不重复承担。QA plan 全文:`production/qa/qa-plan-sprint-20-2026-10-25.md`。
>
> 🔁 **裁定已出(2026-10-25 · 用户裁「两半全补」)**:`TD-building-01` (a)+(b) 两半全还,载体 = **`S20-2a`(先行)** ⇒ **本卡 `AC-24-11` 布局源用真 `World`(可记绿,不走 Fake 挂标路径)**;上方「二选一」原文不删。开工前置 = `S20-2a` 完成。
>
> ✅ **开工前置已满足(2026-10-25,`S20-2a` 已 done · `2771c61`)**:`world_end_to_end_test.cs` 已证真 `World` 端到端(Place → `IsEffectivelyWalkable == false` / Remove → `true`);`TD-building-01` 与 `TD-itemdb-01` 双 closed(债册三十五订)。
> 🔁 **同句的编号订正(重写轮 ②)**:上文两处 `AC-24-11` **一律读作 `EC-24-05`** —— 布局源判据属缓存失效面,非「外壳格/空格不参与连通」面。

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
