# Consistency Check Report — 2026-09-17 (系统 23 修订涟漪复跑)

> 触发:`/consistency-check`(验证 23 模块化建造评审修订 B1–B8 + 4 项用户裁定的跨文件涟漪)。
> 范围:**since-last-review 定向** —— 23 评审修订涉及的全部文件:
> `modular-building.md`(主体)· `adr-016`(§五 补注)· `adr-022`(C2' + BakedInitial 契约)· `adr-010`(义务 12)· `entities.yaml`(3 条目)· `patient-ai.md`(13 回填)· `systems-index.md`(Approved 落账)。

## Verdict: PASS(1 陈旧修定 + 1 计数码修正,无 🔴 冲突)

Registry loaded: 3 公式(effective_walkable / structure_placeable / structure_refund)+ 1 常量(DEMOLISH_REFUND_RATIO)
GDDs scanned: modular-building.md · patient-ai.md · world-and-ecozones.md · item-database.md
ADR scanned: adr-015 · adr-016 · adr-022 · adr-010

---

### Conflicts Found (must resolve before architecture)

**无 🔴 冲突。**

### Stale Registry Entries (registry behind the GDD) — 1,已修定

⚠️ **DEMOLISH_REFUND_RATIO**(entities.yaml,常量)
   Registry 旧: `value: "*待定*(Q16.16 · 0 < R ≤ 1 · P0 默认 < 1 —— 定值归用户)"` + `constraint: "R = 1 时与「免费撤销」语义冲突,须另裁"`
   Source GDD(modular-building.md 规则九 / F-23-3 / AC-23-08): **P0 恒 = 1**(2026-09-17 评审裁定,重排净零材料损耗,代价走拟物轴 BUILD_TIME / 停诊);P1a 恢复 < 1。
   → **已修定**:value / constraint 更新为「P0 恒 = 1 · P1a < 1」,`revised: 2026-09-17` 注明旧口径过期。YAML 校验通过。

### Counter-mismatch Fixes (计数口径,非数值冲突) — 已修正

⚠️ **边例数「12」→「13」**(systems-index.md :591 §11 行 + :694 设计序行)
   GDD 实有 EC-23-01…13(13 条,含评审新增 EC-23-13 开档基线重放),旧账两处写「12 边例」。
   → **已改 13**,与 GDD / review-log 一致。

### Verified Clean (本轮重点锚点,全绿)

✅ **缝一合成双向一致**:`EffectiveWalkable(cell) := Nav[cell].walkable ∧ ¬Overlay.blocked(cell)`
   — 23 GDD(F-23-1 / 规则三)· 6 GDD(`world-and-ecozones.md:289-293` O-6-10)· ADR-016 §五 补注 · entities.yaml(expression 同式)四方同文。
✅ **读方 = 27 / 13,玩家不读**:B2 裁定后 —— 23 GDD / entities.yaml(注释)/ ADR-016 补注 / **patient-ai.md(13 共享基础设施表补注,本次回填)** 一致;`player-controller-and-movement.md` 引用已从 registry 移除。
✅ **`Overlay(0) := BakedInitial`**:23 GDD 规则三 / EC-23-05 / EC-23-13 · ADR-022 `world_buildslots.json` 契约 · ADR-016 补注三处一致(派生态,ADR-015 §二 第二类源;外壳必须走初始占用路径)。
✅ **判定五条件**:F-23-2(①锚点类型 ②占用空 ③占用格 ∈ BuildSlotRegion ④骨架未变 ⑤库存 + 实体空 `¬EntityOnCell`)+ `(0,0) ∈ OccupiedCells_local` 目录校验 —— GDD 与 entities.yaml expression 逐项对应(B5 / B4 兑现)。
✅ **Q16.16 舍入例外**:`⌊cost × R⌋ = (cost × rawR) >> 16` + `⌊⌋` 向 −∞ 截断 ≠ ADR-006 ROUND_HALF_AWAY_FROM_ZERO —— GDD F-23-3 / entities.yaml structure_refund 均显式注为例外(B6)。
✅ **ADR-010 义务 12**:`structure_id` 高水位共用 `IIdAuthority`(与义务 6 同一机制)+ 折叠谓词不适用结构行 —— 表内登记与 GDD 规则四/七源字段一致。
✅ **ADR-022 C2'**:模板 collider 足迹同源(硬失败)+ Validation 复选框 —— 承 23 规则八 / `O-23-9`,与「玩家不读整数格 → 两套可走性必须一致」因果贯通。
✅ **AC 计数**:AC-23-01…18(18 条唯一编号 = 16 BLOCKING + 2 ADVISORY);EC-23-01…13;规则一…十;公式 F-23-1/2/2b/3 —— 与四处声明一致。

### Unverifiable References (no conflict, informational)

ℹ️ 27 敌人 AI **无 GDD**(系统尚未落盘):EffectiveWalkable 消费方之一由 ADR-016 补注与 23 GDD 注册,实际读方待 27 GDD 撰写时回溯追加 —— 已在 entities.yaml / 23 GDD 注明,不阻塞。

---

## 处置汇总

| # | 发现 | 类型 | 处置 |
|---|------|------|------|
| 1 | DEMOLISH_REFUND_RATIO 常量「P0 默认 < 1」过期为「P0 恒 = 1」 | ⚠️ 陈旧 registry | ✅ 已修定(entities.yaml) |
| 2 | 边例数「12」vs 实有 13 | ⚠️ 计数口径 | ✅ 已改(systems-index 两处) |

**Verdict: PASS** —— 评审修订四处涟漪(ADR-016 / 022 / 010 + 23 GDD / entities.yaml / 13 回填)互相一致,仅 2 处口径修正,无架构级冲突。