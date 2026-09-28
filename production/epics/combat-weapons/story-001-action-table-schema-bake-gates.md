# Story 001: 动作表 schema 与烘焙构建门

> **Epic**: 格斗与武器线
> **Status**: Ready
> **Layer**: Core
> **Type**: Config-Data
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28
## Context

**GDD**: `design/gdd/combat-and-weapon-lines.md`(§一 动作表 schema · 规则二 Kind 登记面 · §九 构建断言 A1–A27 的表侧子集)
**Requirement**: TR-combat-010(combat_actions.json 唯一真源,maps_to_injury;21a inflicts_injury 降级为集合约束 A20;schema 容纳 P1a 三线) · TR-combat-003 的门半边(MAG_FLOOR > 0 构建期硬拒)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-014(主): 数据管线;ADR-024: Kind 单一登记真源
**ADR Decision Summary**: 作者态 `assets/data/combat_actions.json` 经两阶段工具链烘为确定性 `*.cooked`(`Fix` 字段 JSON 写字符串 → `FixParse`;int 计数写整数);Kind→StreamId 路由从 `entities.yaml`(单一真源)经 `tools/kindgen/` 生成,断言 A1–A5;25 只登记 Kind/载荷,**不重开路由**(ADR-021 骨架先行纪律)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 烘焙与断言为编辑期 .NET;不依赖 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 字段集 = {id, actor_class, attack_class, weapon_line, maps_to_injury, base_step(Fix 字符串), cooldown_ticks(≥1 int), unlock_level, range_override, duration_ticks, max_targets};兽/敌 Natural 的 actor_class/attack_class/range_override **必填**(A22/A23);`max_targets` P0 恒 = 1(A14)
- Forbidden: 浮点字面量入 JSON 的 Fix 字段;`animation_ticks/recovery_ticks` 进 sim 载荷(D-12 裁定:禁入 sim,只准表现层派生);maps_to_injury 引用 21a 集合外的伤情(A20 漂移门拒)
- Guardrail: 表侧构建断言失败 = 硬 `throw`(非 Debug.Assert);P1a 武器线行允许存在但标记非 P0 可执行

---

## Acceptance Criteria

*From GDD `design/gdd/combat-and-weapon-lines.md`, scoped to this story:*

- [ ] schema 校验通过:全部动作行字段闭集完整,类型 ∈ 整数域( Fix 字段为字符串分数,经 FixParse)
- [ ] A13a/b:`MAG_FLOOR > 0` 且 `MAG_FLOOR < MAG_CAP` 构建期硬拒 fixture(违例表被拒)
- [ ] A14:`max_targets = 1` 全表恒成立(出现 >1 即构建失败,P0)
- [ ] A20:每行 `maps_to_injury` ∈ 21a `inflicts_injury` 集合(漂移门;双 fixture:漂移前过、漂移后拒)
- [ ] A22/A23:Natural(兽)行的 actor_class/attack_class/range_override 必填;徒手/短兵行的 weapon_line 与解锁级合法
- [ ] A17/A18/A19 表侧半边:cooldown_ticks ≥ 1 硬门;weapon_line ⊆ 注册表闭集;attack_class 枚举闭集
- [ ] `InjuryOnset` / `EnemyInjuryOnset` / `InjuryStateChanged` 三支 Kind 在 `entities.yaml` 具名登记(stream/author/payload_schema 三必填字段齐;kindgen A1–A5 通过)—— R2 兑现,ADR-024 断言 A3 无重名

---

## Implementation Notes

*Derived from ADR-014/024 Implementation Guidelines:*

1. JSON → 阶段1 词法 → 阶段2 自研绑定 + 校验(白名单/schema_version/区间/长度);禁 `JsonConvert.DeserializeObject<T>`。
2. `entities.yaml` 三支 Kind 的 payload 字段全部整数域:`{actor_id, target_id, injury_id, magnitude, tick, dose_seq}`(magnitude = Fix raw long,禁浮点;ADR-024 断言 A2)。
3. A20 漂移门的对照源 = 21a 的 inflicts_injury 烘焙集;两表在**同一构建图**里比对,任一漂移即失败。
4. fixture 双张:`invalid_actions_*.json`(逐条违例各一张)入测试夹具目录;断言方向 = 「必被拒」。
5. 具体数值(base_step / cooldown_ticks / range_override 等)全归用户数值轮(OQ-25-7)—— 本 story 烘的是**机制与门**,夹具用符号化小值。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 002]: 求值点运行时逻辑
- [Story 004]: magnitude 公式求值(表值只被搬运,不被解释)
- [Story 006]: onset 事件写入与 F-25-8 平衡断言(乘积式断言归 006)
- 21a 侧:inflicts_injury 集合本身(本 story 只对照)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(FixParse)**: 浮点字面量拒绝
  - Given: 动作行 `"base_step": "0.25"`
  - When: 阶段2 绑定
  - Then: 构建失败(只接受 `"1/4"` 类分数/整数)
  - Edge cases: `"1"` 整数形式合法;`"-1/4"` 负 Fix 在 base_step 域被区间门拒
- **AC-2(A20 漂移)**: maps_to_injury 集合外引用
  - Given: fixture 行 maps 到 21a 集合外的 injury_id
  - When: 构建
  - Then: 失败,消息含行 id 与越界引用
  - Edge cases: 21a 集合收缩(另一表删项)同样触发漂移门
- **AC-3(Kindgen 登记)**: entities.yaml 三支 Kind 完整性
  - Given: 新增三支 Kind 条目
  - When: 运行 tools/kindgen
  - Then: A1 唯一流别 / A2 整数域 / A3 无重名 / A4 author 必填 / A5 双向差集 全过;`EnemyInjuryOnset`.stream = world、`InjuryOnset`.stream = history
  - Edge cases: 故意把两支写成同名 → A3 拒

---

## Test Evidence

**Story Type**: Config-Data
**Required evidence**: `unity/Assets/Tests/EditMode/Combat/combat_actions_schema_test.cs` — must exist and pass(含 invalid fixture 拒收断言 + kindgen 断言)
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: ADR-014 工具链与 ADR-024 kindgen 已可用(Tooling 层既有件);21a inflicts_injury 烘焙物(item-database epic)
- Unlocks: Story 002/003/004/005/006(全部运行期故事的表输入)

---

## Completion Notes

*(留空 — story 关闭时回填)*
