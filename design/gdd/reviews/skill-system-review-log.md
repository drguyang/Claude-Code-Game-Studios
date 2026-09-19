# 技能系统(系统 30)评审日志

> 评审史轨道:`design/gdd/reviews/[doc-name]-review-log.md`。每条评审按时间倒序追加。

## 结案追记(2026-09-18 · 用户裁定)—— ✅ **Approved**

用户裁定接受修订、**免二轮 → Approved**(承 37 / 42 / 51 先例)。**免二轮 = 显式风险接受,不是「已核对」**。
重开触发条件四者任一(全量见 `skill-system.md` 文末结案注):
① `P` / `NOVELTY_COOLDOWN` / 数值轮裁定落地后被实现证伪;② `SkillGrown` 登记兑现与 §3.2 冲突;
③ 25 的 A16 / A21「30 修订后复验」不过;④ 8 的 `EmitGrowth` / `K_difficulty` 入参口径被证伪。
**残留登记义务(兑现侧在外部)**:`entities.yaml` 追加 `SkillGrown`(病史流 Kind)+ `ADR-009 §三` 骨架补记。

## Review — 2026-09-18 — **首轮评审:NEEDS REVISION → 4 BLOCKING + 7 Recommended 全部落盘 → 待用户裁定是否 Approved**
Scope signal: **M**
Verdict path: **首轮 `/design-review`(lean · 独立会话,评审者独立于撰写上下文)** ——
`production/review-mode.txt` 不存在 ⇒ 默认 `lean`(全部阶段,不委托 specialist agent)。
判 **NEEDS REVISION**(4 BLOCKING + 7 Recommended)。3 项 BLOCKING 为口径级(浮点泄入 sim ·
`P` 取值集 · 冷却期 tick 化),1 项 BLOCKING(B3)为**机制级**,经用户裁定落盘。

**4 BLOCKING + 7 Recommended 全部落盘**:
- **B1(BLOCKING)** §4.1/§4.3/§4.4 公式用浮点字面量(`1.5`/`0.95`/`0.20`)且未声明求值域 ——
  sim 侧(ADR-005 门 A)出现浮点,IL2CPP 逐位性无从谈起。**修**:三处公式 + 旋钮全部补
  **Q16.16 定点域**注:常量 `FixParse`(`"3/4"` 式,拒浮点字面量),`Mul`/`Div` 中间积 int64,
  除法 `ROUND_HALF_AWAY_FROM_ZERO`(ADR-006 唯一舍入)。
- **B2(BLOCKING)** `P = 1.4` 违反 8 的 **G-1**(禁 libm 超越函数)—— `n^1.4` 须 `pow`,
  末位跨平台不可信;架构复核已点名,30 从未修。**修**:§4.2 表 + 规则三注 + Tuning Knobs 的 `P`
  全部标 ⚠️ `*待裁*`,取值集 = `{整数, 1/2}`(`{1, 1.5, 2}` 例值;`1.4` 作废);§8 新增 AC 断言
  `P ∈ {整数,1/2}`。
- **B3(BLOCKING · 机制级 · 用户裁定)** **成长事件无流载体**:30 定义 `EmitGrowth`,但
  **无任何 SimEvent.Kind 承载它** —— 成长事件在哪条流、写者是谁、51 如何离线重算 F4,全无登记;
  37 订阅的「成长事件」同样无处落地。**修**:§3.2 规则二新增 —— **成长事件以 `SkillGrown` 事件
  落病史流**(ADR-005 · 主机唯一 Append · 载荷 `(skill_id, object_id, novelty_class)` + `Level`)。
  承 37 / ADR-021「系统 GDD 追加 Kind」纪律;兑现 51 的 `AC-51-D2`;51 只折已发事件、不重算
  `XP_gain`(`AC-51-B11` 保持成立)。**残留**:`entities.yaml` 追加 `SkillGrown` 登记 +
  `ADR-009 §三` 骨架补记(兑现侧在外部)。
- **B4(BLOCKING)** §3.2 冷却期「默认 20 分钟」是**墙钟** —— 进 sim 判定引入不可重建的第四来源
  (违 ADR-016 §一 重建三源)。**修**:§3.2 注 + Tuning Knobs:`NOVELTY_COOLDOWN` 改 **tick 计**,
  墙钟分钟读作「该 tick 数」,tick 频率归 `ITickProvider`。
- **R1** §6 依赖表缺 37 / 51 / 25 三个下游消费方行(37 侧已登记义务,30 侧零承载)→ 补三行。
- **R2** `Level` 未携带 ⇒ 51 的 `AC-51-D2` 报「不可得」→ 规则二 + AC 写明 `Level` 随 `SkillGrown` 携带。
- **R3** 高频动作事件频率未受控 → 达 60 后锁定,事件仍发但经验丢弃(与 §5 边例对齐)。
- **R4** §4.4 `CombatPower` 与 25 的 A16(`Fix` 入参)未对齐 → §4.4 注 + AC 断言 `Fix` 传入。
- **R5** JSON 承载字段类型未声明 → Tuning Knobs 注补:`Fix` 字段写字符串 → `FixParse`;int 写整数。
- **R6** 新颖度判定的确定性载体未声明 → 30 侧 `(skill_id, object_id)` 字典 + 冷却水位(tick)判定。
- **R7** `K_difficulty` 是 8 推入的入参(8 → 30 已锁),无交叉引用 → §6 补显式入参注。

**登记进裁决堆(冻结纪律)**:机制与数值**零改动** —— 数值仍归用户:`P ∈ {1, 1.5, 2}` ·
`NOVELTY_COOLDOWN` tick 数 · `BASE[skill]`/`C`/`NOVELTY_FIRST`/`NOVELTY_DECAY`/`DEATH_LOSS`/
`MED_COMBAT_MOD`/`WeaponMultiplier[line]`(数值轮,与 `OQ-25-7` 同批)。

**登记为 30 侧的新增登记义务**(本轮落盘,兑现侧在外部):`entities.yaml` 追加 `SkillGrown`
(`SimEvent.Kind`,病史流)+ `ADR-009 §三` 骨架补记 + `tr-registry.yaml` 如需。

**附带核验(评审口径)**:架构复核 2026-09-15 的「全文零 ADR 引用 + `P=1.4` 违 G-1」**两条均被确认**;
`TR-skill-007` 现 partial 的另一半根因(30 自身修订)已由本修订消解,待 30 定值后转 covered。

Prior verdict resolved: **First review** —— 本条目为首轮。
