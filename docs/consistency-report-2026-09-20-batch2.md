## Consistency Check Report(批次二:20 Hz / CAP 24 四值裁定回扫)
Date: 2026-09-20(second run,同日批次验证)
Registry entries checked: 3 entities, 2 items, 25 formulas, 94 constants(实测计数;`/consistency-check` 首查回填后口径)
Scope: full —— 重点靶 = 本日四值裁定批次(`TICK_SECONDS=0.05` / `TICK_PERIOD=50 ms` / `PATIENT_APPEARANCE_CAP=24` / DOTS 门 100 & 8 ms / 病种预裁 16-20 / `OQ-10-4` 两动作具名)
GDDs scanned: 35(design/gdd/*.md,另含 content/ 两件与评审报告为参照)

---

### 主结论

**四项裁定值本身零冲突**:全案 grep 未发现任何与 `0.05 / 20 Hz / 50 ms / CAP 24 / 100 实体 / 8 ms / P0=8 病种` 相抵的读数;
无残留 `BLOCKED-BY-OQ-8/OQ-25-8` 活标记(processing 两处为「已解除」历史注,gdd-cross-review 为冻结报告 + 结案注,均合法);
`24 000 ticks`(30 的 NOVELTY_COOLDOWN)与 `5 tick = 250 ms`(5 的 WEATHER_BLOCK_TICKS)换算与 50 ms 一致;
`550/50 = 11×`(10 的 O-10-3)与 `TICK_PERIOD ≪ CPR 周期` 判据一致;
172800 / 其他 TICKS_PER_DAY 数值泄漏 = **零**(该值故意无值,归 5,非缺陷)。

发现 **4 处批次漏网**(2 处文档内自相矛盾 + 2 处陈旧复述),按形态全是已知失效模式「裁定只落地引用侧/汇总侧,正文复述点未刷」的复发。

---

### Conflicts Found(须修)

🔴 **C-3 · `campaign-arc.md` 正文四处仍称 `OQ-10-4` 未裁 —— 与本文件 footer 自相矛盾**
   - 事实源:`emergency-procedures.md:60-61 / :1035`(✅ 2026-09-20 结案:止血包扎 + 节奏型通气动作)+ 本件 footer ④ 已记账「OQ-10-4 结案」。
   - 冲突侧:`campaign-arc.md:33`(「哪两个**待 OQ-10-4 裁定**」)、`:46`(「以 OQ-10-4 终裁为准」)、`:268`(「2 动作待 OQ-10-4」)、`:298`(「🔶 动作名**不写死** —— OQ-10-4 未裁」)。
   - `:298` 的「未裁」是**现在时为假的状态断言**;`:33/:46/:268` 的措辞(候选/以终裁为准)在裁定后语义上仍可坍缩到正确值,但会误导实现期读者。
   - → Resolution:四处注块改「✅ 已裁 = 止血包扎 / 节奏型通气动作(年代措辞归 40;`OQ-10-6` 枚举归属仍 open)」;保留 OQ-10-6 / 40 复核的开放标注。

🔴 **C-4 · `adr-017-dots-decision.md` 两处正文仍留「阈值待用户裁定」—— 与本 ADR 其余六处已刷口径相抵**
   - 事实源:同文件 `:13 / :43 / :186 / :192 / :430`(✅ 100 / 8 ms 已裁定并回刷)。
   - 冲突侧:`:140-141`(Decision 裁决段:「阈值待用户裁定」无删除线无 ✅)、`:240`(ASCII 图框内「[阈值待用户裁定]」)。
   - Decision 段是 ADR 的**权威裁决句**,复评门阈值是其组成部分 —— 此处未刷 = 读者从裁决段本身拿到的是裁前状态。
   - → Resolution:两处按同文件既有格式改口(`~~阈值待用户裁定~~ → ✅ 2026-09-20 用户裁定:≥100 / ≥8 ms`);`:240` 图框受列宽约束,可写「阈值 ✅ 100/8ms」。

---

### Stale Registry / Stale Reference(建议修)

⚠️ **S-3 · `telemetry-analytics.md:318` 引用 `PATIENT_APPEARANCE_CAP`(9,`*待定*`)—— 值源已于本日裁定为 24**
   - 注册表侧 `Dens_s` 注已同步(「✅ 2026-09-20 已标定 = 24」),但 51 的 GDD 正文这一处复述漏刷。
   - → Resolution:改 `*待定*` → `✅ 24(2026-09-20 裁定)`。51 不自定义上界的纪律不变。

⚠️ **S-4 · `entities.yaml` 公式 `env_mod` 的 expression 仍写 24 侧 `clamp(..., ENV_MOD_MIN, ENV_MOD_MAX)` —— 与 D-21-31 现行口径冲突**
   - 现行权威:24 输出**未钳制** `env_score`(clinic-machine `AC-24-01` 2026-09-020 回刷 + `:251` 「不再 clamp,唯一钳制点在 21a F1」);求和后统一钳制落 21a。
   - 注册表条目 added/revised = 2026-09-17,系 clinic 侧回刷**之前**的口径;`output_range: ["ENV_MOD_MIN","ENV_MOD_MAX"]` 同理是「钳制后域」,现在 24 的产出域是未钳制的。
   - 注意:这不是值冲突,是**钳制点归属**过期 —— GDD 三件(24/18/21a)全部正确,注册表这一条落后于源件。
   - → Resolution(需用户放行):expression 改为两行:`env_score := base + Σ + adj(24,未钳制)` + `EnvMod := clamp(EnvMod_climate + EnvMod_clinic, ENV_MOD_MIN, ENV_MOD_MAX)@21a F1`,revised 注 was: 2026-09-17 单点式。

### 信息项(不修,记账)

ℹ️ `traceability-index.md:947`(注⑩「阈值待用户裁定」)—— 该注是 **2026-09-15 ADR-017 落盘时点的状态记录**,同文件 09-20 变更行已裁阈值;**冻结证据不回改**。
ℹ️ `emergency-procedures-review-log.md:98/:128` 列 OQ-10-4 为开放 —— 评审日志为时点快照,同上纪律。
ℹ️ `gdd-cross-review-2026-09-20.md:91/:94/:143` 的 BLOCKED-BY 表述 —— 冻结报告 + 已有结案注。
ℹ️ 草木灰/止血草 `item_key: 待定` = 刻意「不造数」纪律(归 21a 实现轮),**非** stale-registry 缺陷。
ℹ️ `TICKS_PER_DAY / TICKS_PER_SEASON` 无值 = 用户裁定「DAY_SECONDS 归 5,本表不代填」,**非**缺陷。
ℹ️ 时钟混用面(7a 墙钟 5 分钟 / 3-2 毫秒)—— 换算可能性已立(tick↔秒),ADR-005 已注记,无待办。
✅ 其余注册表条目(SKILL_CAP=60、PATTERN_THRESHOLD=3、L_INPUT_TO_PIXEL_MAX=50、ROUND_MODE、LeaseSource 三值、CHECKPOINT_INTERVAL 默认注、三案 entity 行、25 条公式)与 SimEvent.Kind 25 支在三流/ADR 侧引用一致,未发现读数分叉。

---

Verdict: **CONFLICTS FOUND**(4 处,全部属「裁定已下、复述点未刷」同型失效;无一个数值互相打架)

> **修复注(2026-09-20 同日,用户放行 [A] 全修)**:C-3(campaign-arc :33/:46/:268/:298 四处)· C-4(adr-017 Decision 段 + ASCII 图两处)·
> S-3(telemetry :318 `*待定*` → 24)· S-4(entities.yaml `env_mod` 表达式改两阶段单一钳制点,YAML 解析复验通过)
> —— **4/4 已落地**。信息项(traceability-index:947 / review-log / cross-review 冻结件)= 不回改,纪律照旧。
> 残余 sweep 仅命中「删除线 + 已裁」合法形态,零活残留。

> 与首查(同日 C-1/C-2)的共同 pattern:批次的**汇总侧落点**(footer / 索引行 / 注册表)总是先被刷新,
> **正文状态句**(「待 X 裁定」「未裁」「*待定*」)是稳定漏网位。⇒ 通用判据:每落一项裁定,
> 除 grep 常量名外,**再 grep 裁定编号本身**(如 `OQ-10-4`)+ 状态词(未裁|待定|待用户),命中件全部过一遍。
