# Consistency Check Report

**Date**: 2026-09-16
**Mode**: `since-last-review`(仓内无既有交叉评审 / 一致性报告 ⇒ 实际执行**全量扫描**)
**Registry**: `design/registry/entities.yaml` — 0 entities · 0 items · **13 formulas** · **61 constants**
**GDDs scanned**: 13 —— `audio-system.md` · `camera-and-viewpoint.md` · `case-system.md` ·
`diagnosis-system.md` · `disease-simulation.md` · `input-system.md` · `item-database.md` ·
`patient-ai.md` · `player-controller-and-movement.md` · `random-events.md` · `skeuomorphic-ui.md` ·
`skill-system.md` · `telemetry-analytics.md`

**触发原因**:系统 2「摄像机与视角」GDD 落盘,引入三件**跨系统承载物** ——
`YawBasis` 基础契约(交付系统 1 的 `O-8`)· `MAX_DT` 共享同源约束 · `PITCH_MAX` 上界。
三件都会涟漪到系统 1 / 42 / 追踪件,故在 `/design-review` 之前先行扫掠。

**Verdict: CONFLICTS FOUND** —— 5 类,**全部已修**(本报告即修复后的终态记录)。

---

## 结论表

| # | 类别 | 严重度 | 涉及文件 | 处置 |
|---|------|--------|----------|------|
| 1 | `O-8` 陈旧未结(与已翻转的 `tr-registry.yaml` 矛盾)| 🔴 CONFLICT | 1 侧 6 处 + `traceability-index.md` 5 处 | ✅ 已修 |
| 2 | 双向依赖断裂(42 仍写「2 无 GDD」)| 🔴 CONFLICT | `skeuomorphic-ui.md` 2 处 | ✅ 已修 |
| 3 | `FOV_v` 空壳契约(有义务、无实体)| 🔴 CONFLICT | `camera-and-viewpoint.md` + `entities.yaml` | ✅ 已修 |
| 4 | `TICK_PERIOD` / `TICK_SECONDS` 一物两名 | ⚠️ STALE REGISTRY / 命名 | `entities.yaml` | ✅ 已登记 |
| 5 | `EPS_*` / `tier_quota_ratio` / `CARRY_CAP` 命名与归属口径 | ⚠️ 域内自洽 | `entities.yaml` | ✅ 已登记 |

---

## 🔴 CONFLICT 1 —— `O-8` 陈旧未结

**Registry / 追踪件立场**:`tr-registry.yaml` 的 `TR-camera-006` 与 `TR-player-005`
**已翻转为 `covered`**;`traceability-index.md:329` 已写「`O-8` 兑现」。

**矛盾方**:`player-controller-and-movement.md` 在 6 处仍把 `O-8` 标为 🔴 未结
(`:354` 上下游表 · `:640` · `:800` · `:1039` 契约表 · `:1102` 义务表 · `:1110` / `:1577` / `:1589`),
`traceability-index.md` 另有 4 处(`:15` · `:340` · `:348` · `:469`)仍以 `O-8` 为**未结根因**叙述。

**→ Resolution**:1 侧 6 处划除并注明「✅ 2026-09-16 已落盘 —— 2 交付 F-2-2 + `AC-2-07…10`」;
`traceability-index.md` 4 处转为「`partial → covered`」并注残留反向义务 `O-11` / `O-14`。
**未删除 `O-8` 的历史叙述** —— 按 `tr-registry` 纪律「禁重编号、只追加」,`O-8` 仍留于义务表,标为已结清。

## 🔴 CONFLICT 2 —— 双向依赖断裂

**规则**:`design/CLAUDE.md` / `.claude/rules/design-docs.md` ——
「Dependencies must be bidirectional — if system A depends on B, B's doc must mention A.」

**矛盾方**:系统 2 的 GDD 下游表**已列**「42 拟物 UI」边并登记反向义务 **`O-15`**;
但 `skeuomorphic-ui.md` 的 §二上游表(`:635`)与 §三双向一致性表(`:665`)仍写
「⚠️ 仅有 ADR-020,**无 GDD** / ❌」。**更重的是**:2 的 `AC-2-22(BLOCKING)` ⑤
**要求** 42 侧存在这条反向引用 —— 缺失即 `AC-2-22` 不可签署。

**→ Resolution**:两处改为「⚠️ **半**」,写明「2 侧已列本边并登记 `O-15`;
`FOV_v` **已于同日补齐**(2 的 Tuning Knobs 组 2 / 组 5 + `entities.yaml`,
42 那条 `referenced_by` 标为**条件边**);
**唯一残留** = 本边成立与否取决于 `OQ-42-1`(裁「恒定世界尺寸」则该边不存在)」。

## 🔴 CONFLICT 3 —— `FOV_v` 空壳契约

**事实**:系统 2 的 `§Visual/Audio` 把交付物描述为「`Camera` 的位置 / 朝向 / `fieldOfView`」,
并在下游表宣称 42「**只引用**」、该值「**归 2**」,据此在 42 侧登记了 `O-15`。
但 **`FOV_v` 这个名字在系统 2 的正文里一次都没出现过** —— 既不在 §Formulas,也不在 §Tuning Knobs。

**⇒ 一个「有下游义务、无上游实体」的契约**:42 撰写时按 `O-15` 回扫系统 2,
`grep FOV_v` **零命中**(正是下方「通用判据」所警告的形状)。

**→ Resolution**(三处):
① 2 的 Tuning Knobs **组 2**(臂与越肩)新增 `FOV_v` 行 —— 定义为「42 的唯一读取项」;
② 2 的 Tuning Knobs **组 5**(档位参数表,「换档 = 只改参数」的唯一清单)新增行 ——
明写 `FOV_v` **逐档可不同**(`FOV_v_EXPLORE` / `FOV_v_TREATMENT`,`Casebook` 档**冻结于进入值**),
且「42 若采用屏幕恒定解,**须声明它读的是哪一档**」;
③ `entities.yaml` 新增 `FOV_v` 常量条目(`value: *待定*` · `unit: 度(°)` · 逐档约束 ·
`referenced_by` = 2 + 42,**并标记 42 那条为条件边,依 `OQ-42-1`**)。

## ⚠️ CONFLICT 4 —— `TICK_PERIOD` ↔ `TICK_SECONDS` 一物两名

**事实**:registry 只有 `TICK_SECONDS`(系统 9 的命名);系统 1 的旋钮表与
`adr-009:455` 的 Amendment G 订正段在正文里称它为 **`TICK_PERIOD`**。

**→ Resolution**:**不新增第二条 registry 条目** —— 那会造出两个实体,
正是 `MAX_DT` 条目注释所警告的静默失配(`TICK_SECONDS` 而已)。
改为在条目内加「⚠️ 一物两名」注,写明「以本名为准;实现期若同时出现两个标识符即为缺陷」,
并把 `referenced_by` 从 1 条扩到 **5 条**(9 F3 / 1 F-1-1b / `adr-009` Amendment G / 8 D-8-7 / 44 呼吸层)。

## ⚠️ CONFLICT 5 —— 命名与归属口径(域内自洽,无值冲突)

三条同型,均**不是值冲突**,故只登记、不改数值:

- **`EPS_OFFSET` / `EPS_PRUNE` / `EPS_MIN`**:registry 全 ASCⅡ(60 条常量皆然,含 `TREND_EPS`);
  系统 9 的正文把其中三条写作希腊字母 `ε_OFFSET` / `ε_PRUNE` / `ε_MIN`。
  **9 自身亦不一致**(同文档内 ASCⅡ `TREND_EPS` 与希腊 `ε_*` 并用)—— 属 9 的表层瑕疵,
  9 已 Approved,**本轮不修**,仅登记双向名。
- **`tier_quota_ratio`**:52 的正文写作 **`档占比[威胁/机会/反应/灾难]`**(`random-events.md:1022`);
  registry 用 ASCⅡ slug。同一旋钮,两名并存。
- **`CARRY_CAP` 归属口径**:`random-events.md:844` 的**算例**写 `CARRY_CAP = 500` ——
  该文件 `:840` **明写**「下列数值只为演示算式走向,**不是标定值;真值归用户**」,
  其旋钮表(`:1026`)亦标 *待定*。⇒ **不构成值冲突**;registry 的 `source` 仍为 20(52 只是消费方),
  并注「**20 撰写时须承接定值权**」。

---

## 已复核并判为非缺陷的两条(记录以免重复告警)

| 项 | 初判 | 复核结论 |
|----|------|----------|
| `MAX_QUALITY` → `diagnosis-system.md` 「超范围引用」 | registry 未登记 8 对档数的依赖 | `entities.yaml` 该条**已带** `# 【待确认】8 的读数门槛是否依赖档数` —— **已知的待确认项,非缺陷** |
| `ENV_MOD_MIN` → `random-events.md` 「超范围引用」 | 同上 | 亦**已带** `# 【待确认】5 时间天气的输入源,撰写时回溯` —— **同上** |

---

## 复核后终态校验

- `entities.yaml` 解析通过;section 计数 = **0 entities / 0 items / 13 formulas / 61 constants**
  (`FOV_v` 为第 61 条);常量名与公式名**无重复**。
- 13 份 GDD 中系统 2 的 `camera-and-viewpoint.md` 已无 `[To be designed]` 占位。
- `O-11`…`O-15` 为系统 1 侧**未占用**的义务编号(1 只编到 `O-10`)
  ⇒ 反向登记为**真扩展,非编号碰撞**。

---

## 通用判据(本轮新增)

> **凡 grep 一件「应该存在」的登记项,零命中即为告警**,不得读作「大概在别处」。

CONFLICT 3 是该判据的正面实例:42 侧**按义务回扫**系统 2、grep 到零命中 ——
如果当时把零命中读作「大概在别处」,`FOV_v` 会一直空壳到实现期。

---

## 下一步

1. **`/design-review design/gdd/camera-and-viewpoint.md` 必须在新会话跑**
   —— `design-system` 技能明令评审者须独立于撰写上下文(`/clear` 后)。
2. 系统 1 / 系统 2 的复审同批在新会话进行。
3. 遗留开放项见 `production/session-state/active.md` 的「开放项」段
   (`OQ-1-12` 落地模型 spike 是系统 1 唯一的 P0 开工阻塞级未决项)。
