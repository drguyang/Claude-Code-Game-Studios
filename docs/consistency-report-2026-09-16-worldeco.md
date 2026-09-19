# Consistency Check Report — #6 世界与生态区 落盘后

**Date**: 2026-09-16
**Scope**: `design/gdd/world-and-ecozones.md --since-last-review`
**Registry entries checked**: 0 entities · 0 items · **18 formulas** · **65 constants**（检查时点）
**GDDs scanned**: 13（`game-concept.md` / `systems-index.md` / `concept-benchmark.md` 与 GDD 无关,已排除）

> **`since-last-review` 的降级**:`design/gdd/gdd-cross-review-*.md` 不存在
> ⇒ 无法按上次报告日期切分,已退化为**全量 GDD 扫描**。上一份报告为
> `docs/consistency-report-2026-09-16.md`(系统 2 落盘后)。

---

### Conflicts Found（须在架构前解决）

#### 🔴 C-1 —— `LATTICE_SIZE` 的归属是**幽灵引用**（4 个文档互指一个不存在的出处）

| 出处 | 原文归属 |
|------|---------|
| `player-controller-and-movement.md:449` | `LATTICE_SIZE` … \| **ADR-015 §三** |
| `player-controller-and-movement.md:1198` | \| `LATTICE_SIZE` \| **ADR-015 §三** \| … |
| `player-controller-and-movement.md:1212` | 不等式的所有者是 `LATTICE_SIZE`（**ADR-015**） |
| `entities.yaml:597` / `:748` | 归 **ADR-015 §三** |
| `world-and-ecozones.md:129` / `:337` / R-6-12 | **6 是 `LATTICE_SIZE`(格边长)的所有者** |

**核实**:`grep -rn "LATTICE_SIZE" docs/architecture/adr-015-*.md` ⇒ **零命中**。
ADR-015 §三 通篇只定义 `WorldPos = (i32,i32,i32)` 与「格 = 工程定义的最小空间单位」,
**从未出现 `LATTICE_SIZE` 这个符号**。（该符号只在本轮之前出现于 ADR-009 `:455` / `:464`
与 ADR-020 `:566`,而两处都是**引用** 1 的 `F-1-1a`,不是定义。）

⇒ 一个**四方互指的归属**,其中「ADR-015 §三」这一方**不存在**。
两个 GDD 因此各自以为自己只是「引用方」:1 以为 6 只是在用它、6 以为 ADR-015 拥有它。

**风险等级**:不阻塞 P0 设计,但**阻塞 1 与 6 的任何一方改值** —— 归属不明确 ⇒
「谁负责复核 `SPEED_MAX × MAX_DT ≤ LATTICE_SIZE`」无答案,而该断言的失败模式是
**静默隧穿**(中间格永不进事件,不崩不报错)。

**Resolution（用户裁定 2026-09-16:取值与烘焙方 = 6）**:
- `world-and-ecozones.md` **一字不改** —— 其 `:129` / `:337` / R-6-12 与 F-6-1 的
  「所有权反转」表述**本来就是对的**,本轮是**反向对齐**（把 1 的引用改为指向 6）。
- `player-controller-and-movement.md` **五处**订正（`:430-436` / `:449` / `:986` /
  `:1198` / `:1212` / 评审史表）:归属改为「**6**（F-6-1;ADR-015 §三 只定义**格框架**）」。
- `entities.yaml` **三处**订正（`:597` 变量注释 / `:610-613` notes 与
  `:748-752` 的 `lattice_size_lower_bound` 引用）。
- `adr-015-world-geometry-fixed-world-lattice.md` §三 **就地补注**:明写本节拥有
  「格框架」（单一格存在 · `WorldPos` 形状 · 格尺寸须是单一同源常量）而**不拥有取值**;
  并显式声明「本节此前从未出现 `LATTICE_SIZE` 这个符号」,使**幽灵引用可被后续读者识破**。

> ⚠️ **不动 6 侧的理由**:6 的 F-6-1 是「所有权反转」这条纪律的**唯一正确登记点**
> （`player-controller-and-movement.md:430-436` 已就地订正过,但只订正了 1 侧的内部论述,
> 未回扫登记表）。若改 6 侧,等于把正确的表述改错。

#### 🔴 C-2 —— 6 的 GDD **内部**`SAFETY_MARGIN` 取值域自相矛盾（`≥ 1` vs `> 1`）

| 位置 | 数值域 |
|------|--------|
| `world-and-ecozones.md:340`（F-6-1 变量表） | `≥ 1` |
| `world-and-ecozones.md:345`（F-6-1 边界文） | ⇒ **`SAFETY_MARGIN` 必须 > 1** |
| `world-and-ecozones.md:609`（Tuning Knobs 组 1） | **`> 1`**（并注 `≤ 1` ⇒ 抬升即隧穿） |
| `world-and-ecozones.md:821`（`AC-6-05`） | `SAFETY_MARGIN > 1`，**取等号即失败** |

**3 : 1** —— 唯一的 `≥ 1` 在变量表里,而它恰好是最容易被引用的那一处
（`AC-6-04` 的判据、`O-6-6` 的议定对象都指向 F-6-1 的变量表）。

**Resolution**:**就地改为 `> 1`**（`world-and-ecozones.md:340`）。
判据:`AC-6-05` 是 BLOCKING 级且写死「取等号即失败」⇒ `≥ 1` 与之直接相抵。
**`≥ 1` 是危险的宽松口径** —— 它容许「无余量」的临界值,而临界值的失败模式是**静默隧穿**。

---

### Stale Registry Entries（登记落后于 GDD）

#### ⚠️ C-5 —— 4 条**跨系统常量**从未登记（登记缺口,非值冲突）

以下四者均满足 `entities.yaml` 的准入规则（**≥ 2 个系统的事实**），但此前**既不在
`constants:` 也不在 `formulas:` 中**:

| 常量 | 所有者 | 读方 | 登记前的可见性 |
|------|--------|------|----------------|
| `LATTICE_SIZE` | **6**（F-6-1） | 1（约束对象）· 52（间接） | ❌ 只以**变量名**活在两条公式的 `variables:` 里 |
| `SAFETY_MARGIN` | **6 + 1 共同议定**（`O-6-6`） | 6 · 1 | ❌ 同上 |
| `TERRAIN_TABLE` | **6**（F-6-2） | 1 · `systems-index.md:38` | ❌ 同上 |
| `K_TERRAIN_MAX` | **6 派生**（F-6-2） | 1 | ❌ 同上 |

**为什么这是缺口而非仅为整洁问题**:登记表是 `/design-system` 与 `/consistency-check`
的**唯一事前输入**。一条只以「公式的变量名」存在的常量,其**归属与不变量无处可查** ——
这正是 C-1 幽灵引用得以存活的原因（归属写在 `variables:` 的行内注释里,不在
`constants:` 的 `source:` 字段里,没有机械可判的落点）。

**Resolution**:`entities.yaml` 新增四条（constants 65 → **69**）,每条带
`value: ""`（数值硬约束留白）· `unit` · `constraint`（硬不变量）· `referenced_by`（全部读方）。

**同批**候选但**刻意不登记**（记录理由,防止下一轮被当成漏项）:
`SPEED_MAX` · `SPEED_MODE_MAX` · `K_CONTEXT_MAX` · `CONTEXT_TABLE`
—— 四者目前**只有 1 个读方**（`CONTEXT_TABLE` 的读方 24 尚无 GDD）⇒ 不满足 ≥ 2 的准入。
`CONTEXT_TABLE` 归 24,撰写时须**同法登记**（`terrain_table` 的 notes 已写明这条对照）。

#### ℹ️ C-3 —— `systems-index.md:38` 记 1 依赖「3」而非 6（**本轮不修,已登记**）

1 的 GDD `:1045` 与本 GDD `:274` / `:568` 三处**均已写明**这条边,且 6 侧已登记义务
**`O-6-1`**（「1 的依赖边须补 6」）。⇒ 这是**已登记的已知缺口**,不是新发现,
按 skill 纪律**不重复记账**;`O-6-1` 未兑现前,`TR` 层的双向性对这条边仍不成立。

---

### Unverifiable References（无冲突,信息性）

#### ℹ️ C-4 —— 两处「6 无 GDD」已陈旧（**已修**）

| 位置 | 原文 | 处理 |
|------|------|------|
| `player-controller-and-movement.md:1045` | \| 6 世界与生态区 \| `TERRAIN_TABLE` … \| ❌ **无 GDD** \| ⚠️ **6 必须在自己的 GDD 里交付这张表** \| | → ✅ **GDD 已成稿**（并补 `LATTICE_SIZE` 亦归 6） |
| `telemetry-analytics.md:426` | \| 6 世界与生态区 \| 世界流(ADR-009) \| ✅ Accepted(**6 无 GDD**) \| … | → ✅ Accepted · **GDD 已成稿** |

> ⚠️ 这两处的**方向与 2026-09-16 上一轮**（系统 2 落盘后）**相反**:
> 上一轮是 `skeuomorphic-ui.md` 漏了 2 的 GDD;本轮是 1 与 51 漏了 6 的 GDD。
> 同一纪律失效两次 ⇒ 见下方「通用判据」的加强版。

#### ℹ️ C-6 —— 生态区标识符**两个名**:`ecozone_id`（6 侧）vs `ecosystem`（21 侧）

- `world-and-ecozones.md`:F-6-3 返回 `ecozone_id`;`A-6-2` 明写「6 提供的是 **`ecozone_id`**」。
- `item-database.md:347` / `:592`:`| ecosystem | string | 产地生态区（**外键 → 6 世界与生态区**） |`。

**无值冲突**（6 侧从未给该枚举取值,21 侧只登记字段名）⇒ 不判 🔴。
但**同一事实两个名**会在实现期制造一次无谓的映射层。`item-database.md:797` 已双向登记该边
（「6 → 21 数据（外键引用）」）⇒ **依赖边本身是健康的**,只有标识符措辞分叉。

**另注（P1a 接口缺口,不判冲突）**:`random-events.md:443` 把 `BIOME_REGION` 的**解析归属**
记为「6 世界与生态区」,而 6 的 F-6-3 只交付**点 → 区**（`EcozoneOf`）;
**区 → 点采样**（「生态区全域取一点」）在 6 的 GDD 里**没有对应公式**。
⇒ P1a 撰写时须补;两侧均已标 P1a（52 的 `:962` 明写「P0 不可达」;6 的 `:137` 记 32 的解锁状态为缺口),**不阻塞 P0**。

---

### Clean Entries（逐条核验,零冲突）

✅ `PoiState` 三态枚举 —— `entities.yaml` ↔ 6 的 GDD `:153-155` / `:224-226` ↔ `architecture.yaml:states.poi_state`
✅ `SimEvent.Kind.PoiStateChanged` —— 载荷 `{poi_id, new_state}` · `Patient = PatientId.None` · 有界性 `2×|POI_DEF|`
✅ `poi_transition_bound` —— `2 × |POI_DEF|` ↔ 6 的 F-6-4「为什么是 2 而不是 3」
✅ `MAX_DT` —— 「位移预算不是数值稳定门」的同源口径 ↔ 6 的 F-6-1 / AC-6-06
✅ `sim_event_actor_cell_entered` —— 三字段整数载荷 · 主机唯一 Append ↔ ADR-020 Amendment B
✅ ADR-015 §一之补（`slopeLimit` / `stepOffset`）↔ 6 的 `AC-6-09`
✅ `ecozone_of` ↔ 52 的 `spawn_anchor` 解析（`random-events.md:429-451` / `:693-702`）
✅ `terrain_table` / `lattice_size_lower_bound` / `POI_DEF` ↔ 1 的 F-1-7 / F-1-1a / 52 的抽池前提
✅ `TR-worldeco-*` 9 条 —— 全部 `covered`,`gdd:` 指针已由 `systems-index.md` 改指本 GDD

---

### Verdict: **CONFLICTS FOUND**（2 条 🔴 · 1 条 ⚠️ · 3 条 ℹ️ —— **全部已在本轮处置**）

处置后复验:`entities.yaml` YAML 解析通过（0 entities / 0 items / 18 formulas / **69 constants**）;
`TR-worldeco-009` 状态翻转是本轮唯一 TR 变更,已确认。

---

## 通用判据（本轮加强）

> **上一轮的判据是**:「凡 grep 一件『应该存在』的登记项,零命中即为告警」。
> **本轮 C-1 展示了它的对偶形态** —— 不是「名字不存在」,而是
> **「名字存在、归属指向的名字不存在」**。
>
> ⇒ 加强为:**凡 GDD 写下一个归属（「归 X」）,须 grep 确认 X 的文档里真的出现该符号**。
> 归属是一个**可被机械验证的外部断言**,但在本轮之前,它被当作**散文**处理 ——
> 四处互指一个从不存在的出处,存活了整整两轮评审。
>
> **第二个判据**（C-4 是第二次命中）:**每次 GDD 落盘后,必须回扫「谁曾经声明我没有 GDD」**。
> 上一轮是 2,本轮是 6 —— 同一失效模式连续两轮 ⇒ 应固化为落盘清单的**最后一项**:
> `grep -rn "<系统名>无 GDD\|<系统名>.*无 GDD" design/gdd/ docs/`。
