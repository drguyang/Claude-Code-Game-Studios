# Story 004: 死亡冷却 F-29-2(从流重算 · 零计数器)

> **Epic**: 死亡与复活
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-21(仅订正两处过期登记,未改任何 AC 语义 —— 见 `:17` / `:38` 的 🔁 加注)
## Context

**GDD**: `design/gdd/death-and-respawn.md`(F-29-2 · EC 表「DEATH_COOLDOWN 内再次致死」行 · OQ-29-4/OQ-29-5)
**Requirement**: TR-death-002 的消费面(PlayerDied 既是产物也是冷却唯一真源)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(主): 确定性模拟(状态从流重算)
**ADR Decision Summary**: `DeathAllowed(actor,t) ⟺ t − last_death_tick(actor) ≥ DEATH_COOLDOWN`;**`last_death_tick` 唯一来源 = 世界流 `PlayerDied` 重算,零独立计数器**(AC-29-15);`DEATH_COOLDOWN` **未定(OQ-29-5,用户平衡轮;须 > 0,= 0 即「自杀清负重」掉落经济漏洞)**;冷却内再次致死 ⇒ **不结算代价、不发 PlayerDied、不传送**(回合继续,EC 行);冷却内二杀语义 = OQ-29-4 **实现期另裁**,本 story 只登记。 🔁 **2026-10-21 订正(原文不删,就地加注)** —— 本句**两处已过期**:① `DEATH_COOLDOWN` **已于 2026-10-06 代拍裁定 = `2400` tick(= 120 s @20 Hz,值可调)**,见 `design/gdd/death-and-respawn.md:280`(逐字:`✅ **2400**(= 120 s @20 Hz · 2026-10-06 代拍,值可调);**须 > 0**,否则「无限刷死」= 掉落经济漏洞`)与 `:535`(`OQ-29-5` 结案行);② 冷却内二杀语义 **已于 2026-10-07 裁定**,见 `:336`(EC 表本行)与 `:534`(`OQ-29-4` 结案行)= **零事件,含不发 `PlayerDied`;「强制传送」候选因制造无意图传送(第四来源,违 ADR-016 §一 三源不变量)被否决** ⇒ 不再是「实现期另裁」,而是**已定且可判**的语义。⚠️ 原文的「须 > 0」硬约束**仍然有效**(它约束取值域,不随取值确定而失效)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: int tick 减法,纯 BCL。

**Control Manifest Rules (this layer)**:
- Required: 谓词为流前缀纯函数;判据 `≥`(恰界即放);每个入结算的 candidate 先过 DeathAllowed
- Forbidden: 字段/计数器存 `last_death_tick`;墙钟/渲染帧参与冷却;在冷却拒绝路径发任何事件
- Guardrail: DEATH_COOLDOWN 以接口注入(值归数值轮),构建/测试夹具用符号化值;若注入 ≤0 ⇒ 显式配置错误(非静默)

---

## Acceptance Criteria

*From GDD `design/gdd/death-and-respawn.md`, scoped to this story:*

- [ ] AC-29-15 [A]:`last_death_tick` 唯一来源 = 世界流 PlayerDied 重算;反射/引用扫描断言 29 无独立计数器字段
- [ ] AC-29-08 [A] ⚠️ BLOCKED-BY-9(完整链):冷却内再次致死 ⇒ 不结算代价、**不发 PlayerDied**;桩 candidate 面测谓词半边,联调不记绿
- [ ] AC-29-10 [A] ⚠️ BLOCKED-BY-9:复活来源的 `ActorCellEntered` 条数上界 = `玩家数 / DEATH_COOLDOWN`(有界性;与 story-005 合验)
- [ ] 谓词边界:`t − last == DEATH_COOLDOWN` 放行;`−1` 拒绝(夹具符号化)
- [ ] 空流(首死):`last_death_tick = −∞` 语义 ⇒ 首个 candidate 恒允许(不发哨兵事件)
- [ ] OQ-29-4(冷却内二杀的确切呈现语义)在代码与文档双侧标「待裁」,不做行为发明 🔁 **2026-10-21 订正(原文不删,就地加注)** —— **`OQ-29-4` 已于 2026-10-07 裁定**,`design/gdd/death-and-respawn.md:534`(结案行)+ `:336`(EC 表本行)逐字 = **免罚但继续:零事件,连 `PlayerDied` 都不发(`last_death_tick` 不变),回合继续;「强制传送」候选被否决**(理由 = 无意图的位置变更属第四来源,违 ADR-016 §一 三源不变量)。⇒ 本条 AC **不再是「标待裁、不做行为发明」,而是已定且可判**:判据 = `DeathAllowed == false` 期间,世界流中该 actor 的 `PlayerDied` 条数**不增**(`:534` 末段自陈的可证伪判据),且**零代价结算、零传送**。**照原文实现会把一个已裁语义做成未裁** —— 那是反向借绿(该绿的做成不该绿的)。

---

## Implementation Notes

*Derived from ADR-005 Implementation Guidelines:*

1. `LastDeathTick(actor) := max{ e.tick | e ∈ 世界流 ∧ e.Kind = PlayerDied ∧ e.actor = actor }`(空集哨兵处理);读流,不缓存(缓存归表现层 Memoize,与 24 同纪律)。
2. 入结算管线顺序(story-001 编排):DeathCandidate → **DeathAllowed 门** → PlayerDied → 掉落/掉级/复活编排 —— 门在任何写流之前。
3. 联机多玩家:逐 actor 谓词天然并行(各自读流),无全局冷却字段。
4. 迁移重放:新主机 CatchUp 后 DeathAllowed 取值逐位不变(纯函数性质测试)。
5. 值注入:`DEATH_COOLDOWN_TICKS` 走配置(ADR-014 int 字段,JSON 整数,非 Fix —— 计数类量移出 Fix 解析集,D-21-17 口径)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: PlayerDied 发射与结算编排(本 story 是其前置门)
- [Story 002]: 掉落
- [Story 005]: 复活传送(「不传送」分支的效果在此登记,实现归 005 的调用点收敛)
- 系统 9:DeathCandidate 产生(与 29 冷却无涉)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(谓词边界)**: 恰界放行
  - Given: 符号化 CD=10;last=100
  - When: candidate@109 / @110
  - Then: 109 拒(无 PlayerDied);110 放
  - Edge cases: 首死(无历史 PlayerDied)任意 t 放行
- **AC-2(零计数器)**: 状态面扫描
  - Given: 29 的 sim 类型
  - When: 反射扫描字段
  - Then: 无 last_death/cooldown 类存储字段;值皆由流算出
  - Edge cases: 表现层计时器不受本断言管(门 A 外)
- **AC-3(拒绝路径静默)**: 冷却内二杀
  - Given: 刚死亡(t=100,CD=10),t=105 再注入 candidate
  - When: 结算管线
  - Then: 世界流零新增;代价不结;OQ-29-4 标注在案(行为 = 静默 continue,不发明) 🔁 **2026-10-21 订正** —— 本条是同一处过期登记的**第三个文本落点**(另两处 `:17` / `:38`)。行为「静默 continue」**与裁定一致**(`:336` = 零事件、不发 `PlayerDied`、不传送),但「标注在案 / 不发明」的**理由已不成立** —— `OQ-29-4` 已于 2026-10-07 裁定。⇒ 夹具的 Then 应写成**可判断言**:该 actor 的 `PlayerDied` 条数不增 + 零代价结算 + 零传送;而不是「已标注待裁」这类元断言。
  - Edge cases: 多玩家并行冷却互不影响

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/DeathRespawn/death_cooldown_test.cs` — must exist and pass(谓词半边);AC-29-08/10 全链 BLOCKED-BY-9
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(PlayerDied Kind 与流存在)
- Unlocks: Story 005(复活率上界合验)、联机压测夹具

---

## Completion Notes

*(留空 — story 关闭时回填)*
