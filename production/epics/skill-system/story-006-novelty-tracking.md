# Story 006: 新颖度追踪与冷却

> **Epic**: 技能与熟练度
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skill-system.md`(§3.2 成长规则 · §5 边界情况)
**Requirement**: —(无专属 TR;验收标准来自 GDD §3.2 新颖度加权规则与 §5 边界情况)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-026(主): 技能成长的定点化与持久化契约 · ADR-009: 世界状态的事件化边界(三源不变量)
**ADR Decision Summary**: 新颖度追踪 = 30 侧 (skill_id, object_id) 字典 + 冷却水位(tick 计,NOVELTY_COOLDOWN tick 数,非墙钟);冷却期按 tick 计,不进墙钟(ADR-016 §一 重建三源不变量);按玩家独立(联机不共享);同源可重建。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯整数字典 + tick 比较,不触及任何 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 新颖度字典键 = (skill_id, object_id),按 actor_id 独立;冷却水位 = tick 计(非墙钟);novelty_class 三值枚举 ∈ {First, Stale, Normal}
- Forbidden: 墙钟(DateTime/Time.time)出现在新颖度判定;新颖度按玩家共享(联机)
- Guardrail: NOVELTY_COOLDOWN 以 tick 为单位(20 min → tick 数由数值轮裁定);冷却期判定 = 当前 tick - 上次遇见 tick ≥ NOVELTY_COOLDOWN

---

## Acceptance Criteria

*From GDD `design/gdd/skill-system.md`, scoped to this story:*

- [ ] 新颖度字典可追踪 (skill_id, object_id) 对,按 actor_id 独立
- [ ] 首次遇见 → novelty_class = First(×3.0)
- [ ] 冷却期内再次遇见 → novelty_class = Stale(×0.2)
- [ ] 冷却期外再次遇见(已见过但冷却已过) → novelty_class = Normal(×1.0)
- [ ] 冷却期判定 = 当前 tick - last_seen_tick ≥ NOVELTY_COOLDOWN(以 tick 计,非墙钟)
- [ ] 联机中队友采走玩家未见过的药材 → 该玩家不得 First 加成(必须亲手采过)
- [ ] 同一病例多名玩家接力 → 每人按自己参与部分判定新颖度
- [ ] 格斗技能的新颖度对象 = 对手类型(兵痞/地痞/野兽/山匪),不是单次命中

---

## Implementation Notes

*Derived from ADR-026 Implementation Guidelines:*

1. **`NoveltyTracker`** 类 —— 持有 `Dictionary<(int actorId, (int skillId, int objectId)), int lastSeenTick>`。
2. **`QueryNovelty(int actorId, int skillId, int objectId, int currentTick)`** → `NoveltyClass` 枚举。
   - 字典未命中 → First(×3.0),记录 `lastSeenTick = currentTick`
   - 字典命中,`currentTick - lastSeenTick ≥ NOVELTY_COOLDOWN` → Normal(×1.0),更新 `lastSeenTick`
   - 字典命中,`currentTick - lastSeenTick < NOVELTY_COOLDOWN` → Stale(×0.2),不更新 `lastSeenTick`(冷却期锁定)
3. **按 actor_id 独立**:同一 (skill_id, object_id) 对,不同 actor_id 有各自独立的 lastSeenTick。
4 **不共享**(联机):NoveltyTracker 是 30 的内部状态,不跨网络同步 —— 每个客户端/主机独立维护自己的字典(或由主机维护后通过 45 下发,但新颖度本身**不进三流**)。
5. **对象粒度由技能决定**:调用方传入 object_id —— 诊断传病名、采集传药用植物品种、格斗传对手类型。30 不定义「什么是一个对象」,只提供字典服务。
6. **确定性**:lastSeenTick 来自 `ITickProvider`(tick 序列),不来自墙钟。这意味着「冷却期 20 分钟」在 GDD 里的描述 = 对应 tick 数(由 NOVELTY_COOLDOWN 旋钮控制,数值归用户)。

---

## Out of Scope

- [Story 002]: XP_gain 消费 noveltyClass(本 story 只产出 noveltyClass)
- [Story 007]: SkillGrown 事件发射(本 story 不触发事件,只判定 noveltyClass)
- [Story 045]: 联机同步(新颖度字典按玩家独立,不共享;45 不负责同步 novelty)

---

## QA Test Cases

**[Logic story — automated test specs]:**

- **AC-1**: 首次遇见判定
  - Given: actorId=1, skillId=诊断(0), objectId=大叶性肺炎(1), currentTick=100, 字典为空
  - When: QueryNovelty(1, 0, 1, 100)
  - Then: 返回 First(×3.0);字典记录 lastSeenTick=100
  - Edge cases: 同一 actor 同一对象第二次调用 → 不再 First

- **AC-2**: 冷却期内再次遇见
  - Given: actorId=1, skillId=0, objectId=1, lastSeenTick=100, NOVELTY_COOLDOWN=24000(20min@20Hz), currentTick=101
  - When: QueryNovelty(1, 0, 1, 101)
  - Then: 返回 Stale(×0.2);lastSeenTick 不更新(仍=100)
  - Edge cases: currentTick=100+24000 → 恰好等于冷却期 → Normal(≥ 语义)

- **AC-3**: 冷却期外再次遇见
  - Given: lastSeenTick=100, NOVELTY_COOLDOWN=24000, currentTick=24100
  - When: QueryNovelty(...)
  - Then: 返回 Normal(×1.0);lastSeenTick 更新为 24100
  - Edge cases: currentTick=24399 → 仍在冷却内 → Stale

- **AC-4**: 按 actor_id 独立
  - Given: actorId=1 lastSeenTick=100, actorId=2 未见过同一对象
  - When: QueryNovelty(2, 0, 1, 101)
  - Then: actorId=2 返回 First(×3.0)(独立追踪)
  - Edge cases: actorId=1 → Stale,actorId=2 → First,同一 tick 同一对象

- **AC-5**: 联机不共享
  - Given: actorId=1(本地玩家)见过 objectId=1; teammate(actorId=2)也见过
  - When: actorId=1 QueryNovelty(对象=1)
  - Then: 只查 actorId=1 的字典,不查 actorId=2 的
  - Edge cases: teammate 采走玩家未见过的药材 → 玩家仍为 First

- **AC-6**: 格斗技能对象 = 对手类型
  - Given: skillId=徒手, objectId=兵痞类型
  - When: QueryNovelty(actorId, 徒手, 兵痞, tick)
  - Then: 按兵痞类型判定,不是按单次命中
  - Edge cases: 同一兵痞类型第二次 → Stale(冷却内)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/skill-system/novelty_tracker_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(调参表加载 NOVELTY_COOLDOWN)
- Unlocks: Story 002(XP_gain 消费 noveltyClass)
