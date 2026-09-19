## Consistency Check Report
Date: 2026-09-18
Registry entries checked: 0 entities, 0 items, 25 formulas, 87 constants
Scope: full(全 design/gdd/*.md)
GDDs scanned: 20

> 本轮 = 系统 30 技能与熟练度 首轮 `/design-review` 修订落盘 + 用户裁定免二轮
> **Approved(2026-09-18)** 后的复跑,重点核对 30 修订引入的新物与其 19 份既有
> GDD / registry 是否冲突:`SkillGrown` 事件(落病史流)· `NOVELTY_COOLDOWN` tick 化 ·
> `P` 取值集(`{整数, 1/2}`)· `CombatPower` 以 `Fix` 传入 25 · `QueryLevel` 双向登记 ·
> `K_difficulty` 入参。**数值层零冲突**;发现 4 处过时状态叙述(30 已 Approved,25/51
> 仍写 Needs Revision)—— 经用户批准已就地修复。

---

### Conflicts Found (must resolve before architecture)

**无 🔴 数值冲突。** 本轮核对全过:
- `SKILL_CAP`(= 60):item-database / diagnosis-system / disease-simulation / combat /
  random-events 五处引用**全部一致**,无冲突。✅
- `SkillGrown`:仅存在于 `skill-system.md`(§3.2 规则二,落病史流)。`entities.yaml` 追加
  `SkillGrown` Kind 为**已登记的残留登记义务**(兑现侧在外部),不构成冲突。✅
- `NOVELTY_COOLDOWN`:仅 `skill-system.md`(tick 计,墙钟分钟读作「该 tick 数」),无第二
  来源;tick 频率归 `ITickProvider`(承 `OQ-8` / `OQ-25-8` 同批标定前置)。✅
- `QueryLevel`:30 定义(§3.2 规则二 + §6 依赖表);37 消费(`case-system.md:838` 依赖表)。
  **2026-09-18 复核确认**:30 侧此前零承载(`grep "37"` = 0)⇒ 评审回填 §6 下游表一行
  (「2026-09-18 评审回填」)—— **37 侧登记义务已兑现,`case-system.md:838` 本注划结**。✅
- `CombatPower`(以 `Fix` 传入 25):25 的 A16/A21 契约已随 30 Approved 解除暂定;三条
  复验标注已就地更新(见下方状态叙述修复)。✅
- `K_difficulty`(30 / 8 / 51 三处一致):口径统一,无漂移。✅
- **墙钟残留**:30 之外未发现「冷却 / 冷却期按墙钟计」残留(其余「墙钟」类引用属合法
  调度 / 预算语境,已核实)。✅

---

### Stale Status Statements(30 已 Approved,他处仍写 Needs Revision — 已修复)

⚠️ 4 处过时状态叙述,均非数值冲突;经用户批准(2026-09-18)[A] 就地修复:

1. `combat-and-weapon-lines.md:1272`(依赖表 30 行)——「⚠️ 30 现为 **Needs Revision**」
   →「✅ 30 已 **Approved**(2026-09-18 首轮修订结案)」
2. `combat-and-weapon-lines.md:1279-1281`(阻塞风险区块)——「30 为 **Needs Revision**
   ⇒ 25 的 `CP` 输入契约**在 30 修订前是暂定的**」→「30 已 **Approved** ⇒ 25 的 `CP`
   输入契约**已解除暂定**」+ 新增「数值轮若改 `SKILL_CAP` / `WeaponMultiplier` /
   `MED_COMBAT_MOD` 中任一,`CP_MAX` **须同源重算**(禁手填)」
3. `combat-and-weapon-lines.md:2086-2087`(AC-25-6-02 复验标注)——「30 是硬前置,
   契约暂定」→「30 已于 2026-09-18 Approved,CP 契约暂定已解除;复验理由更新为
   数值轮落地后复核 `QueryLevel` / 求值链可达输入集与 `CP_MAX` 派生仍一致」
4. `telemetry-analytics.md:458`(依赖表 30 行)——「✅ GDD 已成稿(**Needs Revision**,
   30 复审前暂按现状)」→「**Approved(2026-09-18)**」+ 明确「30 已携带 `Level`
   (`SkillGrown` 载荷,§3.2),**`AC-51-D2` 义务已兑现**」

另: `case-system.md:838`(37 依赖表 30 行)——「30 侧零承载 ⇒ 30 须回填」→
**「✅ 2026-09-18:30 侧已回填 `QueryLevel` 的调用方一行 —— 本注义务兑现,划结」**
(此为非过时状态,是登记义务的闭环,同批落盘)。

---

### Clean Entries (no issues found)

✅ 25 条公式 + 87 条常量本轮涉及的注册名(`SKILL_CAP` / `K_difficulty` /
`NOVELTY_COOLDOWN` / `CombatPower` / `SkillGrown` / `QueryLevel`)与 20 份 GDD
对拍无数值冲突 ✅ 30 修订引入的新物全部单源 ✅ 无墙钟冷却残留。

---

Verdict: **PASS(数值层)· 4 处过时状态叙述已修复 + 1 处登记义务闭环**

Resolution steps(已完成):
1. ✅ `combat-and-weapon-lines.md:1272/1279-1281/2086-2087` — 30 状态叙述三处修复
2. ✅ `telemetry-analytics.md:458` — 30 状态叙述修复 + `AC-51-D2` 义务兑现注
3. ✅ `case-system.md:838` — 30 回填义务划结注

残留登记义务(兑现侧在外部,非本轮范围,已在 `systems-index.md` / `active.md` 记账):
- `entities.yaml` 追加 `SkillGrown`(病史流 Kind)+ `ADR-009 §三` 骨架补记
- `TR-skill-007` 待 30 数值轮定值后转 covered
