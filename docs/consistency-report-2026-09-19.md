## Consistency Check Report
Date: 2026-09-19
Registry entries checked: 0 entities, 0 items, 25 formulas, 90 constants
Scope: full(全 design/gdd/*.md)
GDDs scanned: 31

> 本轮 = 系统 39 脉案 `/design-review` 结案(✅ Approved 2026-09-19)后的全量复跑。
> 重点核对 39 修订引入的新物与其全部既有 GDD / registry 是否冲突:
> `author_player_id`(ADR-008 载荷窄修订)· 分册态段(ADR-010 义务 13)
> · `CasesOf` 具名接口待办(37 修订轮)· 病史流三新 Kind
> (`EmergencyAttempt` / `EmergencyTreatmentApplied` / `DrugTreatmentApplied`,承 10 首轮评审)。
> **数值层零冲突**;发现若干 registry `referenced_by` 注释把已成稿的 GDD 仍写作「尚未撰写」,
> 属**文档时效滞后**(⚠️,均已实证为「文件在、正文有引用」),无值改动、无删除候选。

---

### Conflicts Found (must resolve before architecture)

**无 🔴 数值冲突。** 本轮核对全过:

- **`author_player_id`**(ADR-008 §三 Kind 表 / 载荷 struct / `J(c,p)` 按作者分轴):`adr-008` /
  `casebook.md`(F-39.1 · 规则二)/ 39 review-log 三处口径一致,**无第二来源冲突**。✅
- **`CasesOf` 具名接口**:仅在 39(`F-39.1` 引用)与 37(review-log 登记为修订轮待办)出现,
  均标「待 37 修订轮补」—— **共识的待办,非冲突**。✅
- **病史流三新 Kind**(`EmergencyAttempt` / `EmergencyTreatmentApplied` / `DrugTreatmentApplied`):
  `disease-simulation.md:173` 调用者表(10 / 11 / 主机物化)+ `entities.yaml` SimEvent.Kind
  登记 + ADR-009 Amendment I 就地同步 —— **三处一致**。✅
- **`ChannelKind` / 通道位域**(`channel_mask`):位域 = 纯数据契约(9 登记 · 8 拥有通道集合 ·
  13 / 44 表现),`disease-simulation.md:1610-1615` 与 R-8.1(`diagnosis-system.md:388`)口径一致。✅
- **`CareApplied` 写者归属**:`disease-simulation.md:179/:207-208`「由 24 以入向事件写入」
  =(转写,写者仍是玩家主动动作),25 / 53 均为**消费侧**(25 不写 / 53 只读输入集)—— 无冲突。✅
- **常量值横查**(带值项):`SKILL_CAP=60`(skill:489)· `PATTERN_THRESHOLD=3`(case:441/472)·
  `DEMOLISH_REFUND_RATIO P0=1`(modular:282/414)· `SAFETY_MARGIN>1`(world:486)·
  `PITCH_CASEBOOK ≤ PITCH_MAX < 90°`(camera:846)· `EVENT_CHECKPOINT_ANCHORS`
  {脉案落笔·病例结案·出诊启动}(persistence:392)· `TICKS_PER_DAY 5`(52 共享契约)·
  `L_INPUT_TO_PIXEL_MAX=50`(input:16-104)· `ROUND_MODE=ROUND_HALF_AWAY_FROM_ZERO`
  —— 各来源与消费方**全部一致**。✅
- **公式登记名 ↔ GDD 段号映射**:registry 用 slug(`recipe_settlement` / `threshold_transition` …),
  GDD 用 `F-number` 段标题 —— 既有已接受一致模式(`difficulty_curve_window_density` =
  `telemetry-analytics.md:301-506` 命中;`sign_projection` = `diagnosis-system.md:84`;
  `threshold_transition` = `diagnosis-system.md:696/774/1133/…`)。**零名称冲突**。✅

---

### Stale Registry Comments(registry 注释滞后 —— 文档时效,非值冲突)

⚠️ 以下 `referenced_by` 注释把**已成稿的 GDD** 仍写作「尚未撰写 —— 撰写时须回溯追加」。
已逐条实证:目标文件存在且正文**实际引用**了该随录的注册名。**不构成值冲突**,仅注释时效滞后。

| # | 条目 | 注释位置 | 断言 | 实证 |
|---|------|---------|------|------|
| 1 | `recipe_settlement` | `entities.yaml:254` | 「18 / 19 GDD 尚未撰写」 | 18 ✅ `processing.md:151` `= F1(…)`;19 = P1a 未开始,合法留口 |
| 2 | `quality_transmission` | `:291` | 「18 炮制 GDD 尚未撰写」 | 18 ✅ F-18.1 调用契约(`processing.md:7/:23/:32`) |
| 3 | `event_roll` | `:367` | 「27 敌人 AI GDD 尚未撰写」 | 27 ✅ `enemy-ai.md:37/:85/:168` 读 52;`event_roll` 是 52 的公式,27 为内容归属侧 |
| 4 | `rebuild_cost` | `:418` | 「23 建造 GDD 尚未撰写」 | 23 ✅ `modular-building.md:10/:281/:405`(F-23-3) |
| 5 | `disease_progression` | `:451` | 「11 处方用药 / 24 医馆 GDD 尚未撰写」 | 11 ✅ `prescription-and-medication.md:15/:19/:39/:88`(F5 求值点);24 ✅ `clinic-machine.md:10`(EnvMod/EquipMod 运行时入参) |
| 6 | `sign_projection` | `:512` | 「8 诊断 GDD 尚未撰写」 | 8 ✅ `diagnosis-system.md:84`(`sign_projection` 显名 + SKILL_CAP) |
| 7 | `threshold_transition` | `:575` | 「34 公卫 GDD 尚未撰写」 | **34 = P1a 未开始,注释合法保留** |
| 8 | `difficulty_curve_window_density` | `:636` | 「5 / 17 GDD 尚未撰写」 | 5 ✅ `time-and-weather.md:210/:275/:287`(OQ-5-1 结清,表归 17);17 ✅ `foraging.md:39-40/:249/:407` |
| 9 | `CARE_GAP` | `:1516` | 「24 医馆 GDD 尚未撰写 —— 护理时基须同刻度」 | 24 ✅ 无 tick 刻度词汇(24 不含时间语义 —— 见 ℹ️);**注释语义仍待兑现,理由见下** |
| 10 | `CARE_EVENTS_PER_TICK` | `:1533` | 「24 护理动作事件密度约束」 | 24 ✅ 无密度词汇(24 与 1 的密度契约未被承接) |
| 11 | `channel_mask` | `:1571` | 「8 读通道位做门槛」 | 8 ✅ `diagnosis-system.md:388`(R-8.1 `channel` 字段)+ `:412`(通道清单归 9) |
| 12 | `SimEvent.Kind.CareApplied` | `:1868` | 「24 = 入向事件,须发本 Kind」 | 24 ✅ `clinic-machine.md:10` 契约行;`disease-simulation.md:179/:207` 已明确写者 = 24 转写 |

**#9 / #10 有真实残留**:24 的 GDD 尚未把「护理动作」的**时基(tick 刻度)**与**密度约束
(`CARE_EVENTS_PER_TICK`)**两个字面**收紧到自己文档里**(`clinic-machine.md` 零 tick 词汇)。
归属已定(写者 = 24 转写),但**数量 / 时基契约的载体**仍未在 24 侧落文本 —— 这是**内容缺口,不是冲突**:
9 已把载荷校验(`t(止) ≥ t(起)` · `K ≤ |护理动作表|`)写进写入期校验(`disease-simulation.md:343`),
24 侧只需在自身 GDD **回填引用**即可封口(referenced_by 注释可就此划结)。

---

### Unverifiable References(no conflict, informational)

ℹ️ 以下为**特征引用**(GDD 正文显名调用但未逐一列出可比属性),既无值、亦非冲突 —— 仅记录:

- 5 / 17 未显式写 `difficulty_curve_window_density` 字样(5 侧是**割据边界**:季节修正表归 17、
  17 判定为采集衰减 + 季节修正)—— 与 registry「5/17 复用同一形状」的**意图登记**一致。✅
- `event_roll` 无 27 侧正文命中:`enemy-ai.md:37` 明写「来不来归 52,来了怎么动归 27」——
  27 **不消费** `event_roll`,注释「27 决定来了怎么动」= 内容归属意,与 ADR-016 §八一致。✅
- `CONTEXT_TABLE`(`:2424` 注,24 无 GDD 且读方仅 1):现在 **1 侧已消费**(`player-controller-and-movement.md:448/:464`,
  `K_CONTEXT_MAX` = 24 的表 → 1 派生)—— 读者已 ≥ 2(24 作者 + 1 消费),且 `clinic-machine.md` F-24-3
  已产出 `CONTEXT_TABLE` / `K_CONTEXT_MAX` 登记(`entities.yaml:672/:677` 注)。**该注的「不满足 ≥ 2 读方」
  前提已过时**,但**结论(24 撰写时须同法登记)已兑现** —— 无行动项。

---

### Served as Reference(过时状态叙述,已被历次修订就地处决)

- `skeuomorphic-ui.md:937/:1648`「39 无 GDD」→ 已由 39 结案修订更新(OQ-42-5 闭合)。✅
- `persistence-service.md:377`「39 无 GDD」行仍存,但 39 已交 ADR-010 义务 13 ——
  该行为「脉案落笔 = 事件触发锚点」**数据行,指向机制而非身份**,非冲突(39 的写路径契约已闭环)。✅
- `case-system.md:1082-1087/:1117`「39 / 53 无 GDD」→ 均已于 2026-09-19 就地订正。✅
- `inventory-and-items.md:8/:330/:331`「11 / 18 未写」→ **已过时**(11 / 18 均已成稿),
  但因是**文件头 / 依赖表的状态行而非引用注册名的正文**,不属 `/consistency-check` 的 registry 对拍范围
  —— 登记为跨系统文档时效(随系统各自修订轮顺手订正,推荐优先：#11 首轮、#18 首轮)。⚠️

---

### Clean Entries (no issues found)

✅ **25 条公式 + 90 条常量**本轮涉及的注册名(**全部带值常量 + 关系常量 + 全部随录实体**)
与 31 份 GDD 对拍**无数值冲突** · 三新 Kind 三源一致 · 作者轴窄修订三处一致 ·
`CasesOf` 为共识待办。✅

---

Verdict: **PASS(数值层)· 12 处 registry 注释时效滞后 + #9/#10 内容缺口(载体未落 24 侧)+ 1 处登记前提过时**

Resolution steps(已完成 / 待用户批准):
1. ✅ 复核确认无 🔴 冲突 —— 无 `consistency-failures.md` 追加。
2. ⚠️ 12 处 `referenced_by` 注释措辞滞后(「X 尚未撰写」→ 已成稿)—— 建议 Phase 6 修文本。
3. ⚠️ `CARE_GAP` / `CARE_EVENTS_PER_TICK` 的**载体回填义务**仍在 24 侧(内容缺口,非冲突)。
4. ⚠️ `CONTEXT_TABLE` 注「读方仅 1」前提过时 —— 结论已兑现,仅注文更新。
5. ⚠️ `inventory-and-items.md:8/:330/:331`「11 / 18 未写」状态行过时 —— 归系统修订轮。

残留登记义务(兑现侧在外部,非本轮范围):`player_id` 发号 → 45 GDD 轮 · `CasesOf` 具名接口
→ 37 修订轮 · `OQ-39-6` 落笔输入 → 与 11 同批 · `AC-39-05` 载体 → 37 侧 `NOT-RUN → RUN` 回刷。