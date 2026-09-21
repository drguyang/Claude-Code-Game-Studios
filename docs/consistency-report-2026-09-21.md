## Consistency Check Report(批次三:批 19 + 批 20 后 · since-last-review)
Date: 2026-09-21
Registry entries checked: 3 entities, 2 items, 25 formulas, 103 constants(合计 133)
Scope: `since-last-review` —— 锚 = `design/gdd/gdd-cross-review-2026-09-20.md`
GDDs scanned: 16(batch19 的 15 件 + `paper-closeup-48.md`;`tutorial-and-onboarding.md` 两属)

- `audio-system.md` · `camera-and-viewpoint.md` · `casebook.md` · `case-system.md` ·
  `combat-and-weapon-lines.md` · `diagnosis-system.md` · `disease-simulation.md` ·
  `emergency-procedures.md` · `input-system.md` · `patient-ai.md` · `persistence-service.md` ·
  `skeuomorphic-ui.md` · `skill-system.md` · `systems-index.md` · `tutorial-and-onboarding.md` ·
  `design/ux/paper-closeup-48.md`

---

### 主结论

**注册表主体零冲突**:25 条公式 / 103 条常量 / 3 实体 / 2 物品在 16 件扫描面上**无一例值冲突**。
本批四值裁定(`TICK_SECONDS = 0.05` / `PATIENT_APPEARANCE_CAP = 24` / DOTS 门 100 & 8 ms /
病种预裁)在扫描面上**全部一致**,无残留 `待定` 状态断言 —— 上一批 S-2/S-3 型「常量刷了、复述点没刷」
**本轮零复发**。

`Checkpoint` 未登记为 `SimEvent.Kind`(ADR-024 白名单外)—— 与批 20 规则八为 `archive` 步开的
白名单例外**互相印证**,非缺陷。

**批 19 / 批 20 引入 5 处陈旧复述 + 1 处 V-5 加注漏网**,集中于三个已知失效模式的复发面:
① 「裁定只落地权威件,同文件的复述点未刷」(批 19 修了 `patient-ai.md:872` 的依赖行,漏了同一文件 :599 / :785);
② 「误载路径已订正两处、第三处漏网」;
③ 「V-5 加注按文件而非按命中点计数」。

> **修复状态(2026-09-21 同日)**:🔴 **C-1 / C-2 / C-3** 与 ⚠️ **S-3** **已就地修复**(共 7 处编辑,
> 跨 4 件:`patient-ai.md` ×5 · `accessibility-requirements.md` ×2 · `audio-system.md` ×1,
> 含 `consistency-failures.md` 账本追加)。⚠️ **S-1 / S-2 / S-4** 归 `/architecture-review` 轮
> (注册表正文 + 测试脚手架附属件,非本轮职权 —— 承「不动 `tr-registry.yaml`」纪律)。
> **机制数值零变动**;**`AC-13-F3` 承「引用 ≠ 验收」仍不记绿**。

---

### Conflicts Found(须修)

🔴 **C-1 · `patient-ai.md` 两处仍称联机音频精度「取主机技能」—— 与 ADR-018 §五(2026-09-18 裁定 D-A)相抵**
   - 事实源:`adr-018-audio-architecture.md:276`(「### 五、联机音频 —— **各设备按本机技能档**(2026-09-18 修订)」)· `:290`(`SetTier(TierSource.Local)` 权威句)· `:603`(`AC-44-07` 重写)· `audio-system.md:858`(「新判据 = 各按本机技能」)。
   - 冲突侧(现在时为假):
     - `patient-ai.md:599` —— 「(单机 = 本地技能 / 联机 = **主机技能**,ADR-018 §五)」
     - `patient-ai.md:785` —— 「| cue 的听诊精度档 | 由 44 取**主机技能**(ADR-018 §五)……|」
   - **本文件内自相矛盾的证据**:`patient-ai.md:872` 的 ADR-018 依赖行**已被批 19 刷对**(「§五 联机**各设备本机技能档**(⚠️ 2026-09-18 裁定 D-A:原「取主机技能」已作废)」)⇒ 同一文件上下两口径并存。
   - → Resolution:**✅ 已修(2026-09-21,本轮)** —— 两处按 `:872` 同款改口(「联机 = **各设备本机技能档**」+ 一条 2026-09-18 裁定 D-A 注,`:599` 与 `:785` 各带 HTML 引据订正注)。

🔴 **C-2 · `design/accessibility-requirements.md:174` 仍称「精度取主机技能」—— 无障碍权威件携带失效口径**
   - 冲突侧:`:174`(「45 联机(1-4 coop)| …… | 精度取主机技能(ADR-018 §五)⇒ **听障玩家的远端读数不受影响,分叉的只有音** | Partial |」)。
   - 该行的**结论**(「分叉的只有音」)在改判后**依然成立**,但**引据字符串为假** —— 且它引的正是无障碍档位的权威件,下游 `AC` 若抄此句会复制错误引据。
   - → Resolution:**✅ 已修(2026-09-21,本轮)** —— 引据改「各设备**本机技能档**(ADR-018 §五,2026-09-18 裁定 D-A)」;结论句不动。同批把该行的 `ADR #2(边界程序集)` 顺手改为 **ADR-025 的 `Gameplay.Presentation`**(同属陈旧的「Required ADR #2 未兑现」口径)。

🔴 **C-3 · `patient-ai.md` 三处 accessibility 误载路径 + 已兑现义务仍写「成文时须」**
   - 事实源:`design/accessibility-requirements.md:21`(「早期交接模板中出现的 `design/ux/accessibility-requirements.md` 为**误载路径**,该文件**不存在,也不创建**」)· `design/CLAUDE.md`(2026-09-20 就地订正为根 `design/`)· 批 19 已修 `input-system.md:1309` · `case-system.md:933` **已被批 19 修对**(现带「原引 `design/ux/` 误载路径」注 + 「NOT-RUN 改判为『已裁:不承诺』」)。
   - 冲突侧(**双重缺陷:路径错 + 义务已兑现却仍记未兑现**):
     - `patient-ai.md:1077` —— `AC-13-F3` 行:「且 `design/ux/accessibility-requirements.md` **成文时须**引用本条」
     - `patient-ai.md:1147` —— 验收清单同条同文
     - `patient-ai.md:1168` —— `OQ-13-4` 行「何时」列仍写 `design/ux/accessibility-require…` 成文
   - **义务侧已闭合的证据**:`design/accessibility-requirements.md:159` 明写「**引用义务已兑现**:13 病人 AI 的 **`AC-13-F3`** 显式非目标」并点名 `patient-ai.md:1077/1147` ⇒ 三条 AC 的**文档判据已达成**,当前措辞(未来时)是假状态断言。
   - 附带:该文件 `:159` 自陈「`case-system.md:933` …… **回写归 `/architecture-review` 轮**」—— 但批 19 **已经回写了** `case-system.md:933`。属**同轴的同型滞后**(双向:13 侧未刷 + 无障碍侧以为未刷)。
   - → Resolution:**✅ 已修(2026-09-21,本轮)** —— 三处改根路径 `design/accessibility-requirements.md` + 义务改「**已兑现 2026-09-20**」(承「引用 ≠ 验收」纪律:`AC-13-F3` 仍**不记绿**,只改时态);`:159` 的「回写归 `/architecture-review` 轮」加「⚠️ 2026-09-21 订正:批 19(`3bcb78e`)已就地完成」注。

---

### Stale Registry / Stale Reference(建议修)

⚠️ **S-1 · `tr-registry.yaml` `TR-diag-024` 的 `requirement` 字段仍是改判前口径**
   - `:1310` —— `requirement: "D-8-8 硬需求④:联机时精度统一取主机技能"`
   - 同条目 `:1315` 的 `note` **已写清**「原口径……**已撤销**」,`revised: "2026-09-18"` —— **条目的 requirement 与 note 自相矛盾**,而 `requirement` 是 RTM 的展示列(`requirements-traceability.md` 由此生成)。
   - → Resolution:**⏸ 未修(归 `/architecture-review` 轮)** —— `requirement` 改「D-8-8 硬需求④:联机时**各设备按本机技能档**」(note 已含完整裁定史,**不重写 note**)。**归属**:`tr-registry.yaml` 的正文改归 `/architecture-review` 轮(承批 20 已登记的「不动 tr-registry」纪律)。

⚠️ **S-2 · `tr-registry.yaml` `TR-patient-018` 的 `note` 仍留同款旧口径**
   - `:1814` —— 「……由 `SetTier(TierSource)` 持有(单机 = 本地技能 / **联机 = 主机技能**),**与距离无关**」
   - 与 `patient-ai.md:599` 是**逐字同一句**(来源同源)⇒ C-1 的注册表侧孪生。同条 `revised: "2026-09-15"`(早于改判日)亦未更新。
   - → Resolution:**⏸ 未修(归 `/architecture-review` 轮)** —— 同 C-1 改口 + `revised` 刷 2026-09-21。

⚠️ **S-3 · `audio-system.md:971` —— ADR-025 V-5 加注漏网(按文件计数掩盖的第三处)**
   - `adr-025` §Validation `V-5` 判据:「全库 grep『门面程序集 / 独立契约程序集』命中处**均带「现名」注**(回写完成判据)」。
   - `:971` —— 「| 8 | **DTO 引用 sim 类型 / AC-B1 grep 退步** | 定型**独立契约程序集**(仅 BCL);AC-44-B1 升为 asmdef 白名单 + IL 扫描 |」—— **无「现名」注**。
   - 同一文件的 `:242` **已带注** ⇒ 按文件统计(本文件 3 命中 / 1 现名)显示「有注」,**按命中点统计才发现第三处**。这正是 V-5 写「命中**处**」而非「文件」的原因。
   - → Resolution:**✅ 已修(2026-09-21,本轮)** —— 该行补一条 ADR-025 回写加注(现名 = `Sim.Contracts`;明写 `AudioCueDto` + `IAudioCueSink` 在 ADR-025 §① 成员列内,本行「仅 BCL」判据不变,AC-44-B1 的 asmdef 白名单 + IL 扫描落 `Sim.Contracts` 程序集)→ 与 `:243` 同款。
   - **复核**:`adr-005:229` / `disease-simulation.md:772` / `emergency-procedures.md:155` **均已带注** ✓;`architecture.md:324/325/375` 与 `systems-index.md:858` 是**作废声明本体**(非漏网)✓。
   - ⚠️ **`边界程序集` 不属作废集**:`adr-025:113-114` 只作废「门面程序集」(ADR-005:228)与「独立契约程序集」(44 GDD:242)两名;**「边界程序集」是仍有效的 L4 层名**(`disease-simulation.md` 6 处 / `world-and-ecozones.md` 6 处 / `death-and-respawn.md:402` 等**均不需加注**)。本报告已按此重切扫描面。

⚠️ **S-4 · `tests/README.md:40` 陈旧 —— 声称程序集命名「未裁决」而 ADR-025 已于 2026-09-20 落盘**
   - `:38-41` —— 「程序集**名称与清单**是 `architecture.md` §Required ADRs **#2** 的裁决对象。全案目前只有 `Sim.asmdef` 一个名字被定义过(其余『门面程序集』/『独立契约程序集』/『边界层 codec 程序集』被 4 处引用却无定义)。**先命名后生成,否则是替 ADR #2 抢拍板。**」
   - Required ADR #2 **已兑现** = **ADR-025 Accepted 2026-09-20**(六装配清单具名)。
   - 该件「**刻意不生成 `.asmdef`**」的**决定本身仍然正确**(脚手架归实现轮),但**理由句已过期** —— 现读者会以为契约仍未裁。同时此处也是「门面程序集 / 独立契约程序集」的**第五处命中**,V-5 未覆盖它(该 grep 只扫 `design/` + `docs/`)。
   - → Resolution:**⏸ 未修(归 `/test-setup` 轮)** —— 理由句改为「**ADR-025 已具名六装配清单(2026-09-20 Accepted)⇒ 命名阻塞已解除**;`.asmdef` 仍刻意缺席(工程本体不存在,归实现轮)」。**改后不生成任何文件**(决定不变)。
   - 归属:该文件不在 GDD 面内,归 `/test-setup` 轮的附属件 —— 与 ADR-025 同为 2026-09-20 批,**批 19 未回刷**。

---

### Unverifiable References(无冲突,仅登记)

ℹ️ **`case-system.md:537` 的 P0 三案链 Example 用 `DIS_MALARIA`,注册表实体登记为 `INJ_HEMORRHAGE`**
   - `entities.yaml:112` —— `三案·甲(教书先生) / injury: INJ_HEMORRHAGE(战伤失血)`
   - `case-system.md:537` —— 「**Example(P0 三案链):** 三例 `DIS_MALARIA` 在 Tick 400 / 447 / 463 结案 →」
   - **不是新冲突**:这正是 **W-1** 本人(`case-system.md:612-620`:三案成员伤情 = 纯 `INJ_HEMORRHAGE`,而 `F-37.3`/`AC-37-12` 明写「纯伤情案不入 F-37.1」⇒ `PatternRecognized` 可能永不触发;须 **9 + 37 会签二选一**;用户已**显式风险接受**)。该 Example 走的是 **W-1 选项 ①**(三案各挂同一 `DIS_*`)的形态 ⇒ 是**待裁定分支的示例**,非独立缺陷。
   - 登记理由:实现期读 `:537` 的人若无 W-1 上下文,会误以 `DIS_MALARIA` 为既定值。**W-1 结案时须一并刷此例**。
   - → 无本轮行动;W-1 会签时作验收项。

ℹ️ 其余命中均为**纯引用**,无可比值:
   - 公式名 25 条中 9 条在扫描面出现(`event_roll` / `rebuild_cost` / `sign_projection` / `threshold_transition` /
     `lattice_size_lower_bound` / `poi_transition_bound` / `POI_DEF` / `k_context_max`),**全部为「某系统读某系统」的接口引用,零值/域断言** ⇒ 无可比项。
   - 常量 50 条命中:已裁者(`TICK_SECONDS` / `PATIENT_APPEARANCE_CAP` / `PATTERN_THRESHOLD` / `SKILL_CAP`)与注册表**逐字一致**;
     `*待定*` 者(`PITCH_MIN` / `PITCH_MAX` / `MAX_DT` / `FOV_v` / `LATTICE_SIZE` / `TREND_EPS` / `RECOVER_THRESHOLD` /
     `MAX_SCAN_STEPS` / `scan_step` / `EPS_OFFSET` / `EPS_PRUNE` / `CARRY_CAP` / `WINDOW_SIZE` / `REP_CAP` /
     `SALVAGE_RATE` / `tier_quota_ratio` / `reputation_multipliers` / `TICKS_PER_DAY`)**两侧同为待定**,一致(承「数值用户自己调」)。
   - `tests/README.md` 的 `.asmdef` 缺席决定:`S-4` 已覆盖,此处仅记其**判据不变**。

---

### Clean Entries(无问题)

✅ **29 条注册表条目在扫描面上逐字核验通过**,其中本批高风险面全绿:
   - **四值裁定批**(4/4):`TICK_SECONDS = 0.05`(`disease-simulation.md:1464` / `systems-index.md`)·
     `PATIENT_APPEARANCE_CAP = 24`(9 侧 6 处 + `entities.yaml`)· DOTS 门 100 & 8 ms · 病种预裁 ——
     **上一批 S-2/S-3「常量刷了、GDD 复述点没刷」本轮零复发**。
   - **批 19 声称已结清者(逐条实测)**:`input-system.md:1309` 误载路径已修 ✓ · `case-system.md:933` 已修 ✓ ·
     `skill-system.md` 的 `K_growth = 0` 改口已落且**未借绿**(勾选项未翻)✓ ·
     `combat-and-weapon-lines.md` 的 `Down(int actorId)` 构建期断言义务已落且**明写不结 `OQ-25-3`** ✓ ·
     `art-bible` AB-5 支柱注释两处已在 `camera-and-viewpoint.md` / `casebook.md` 对齐 ✓ ·
     `adr-025` 回写加注在 `disease-simulation.md` / `emergency-procedures.md` / `persistence-service.md` 到位 ✓。
   - **批 20**(`48_tutorial_content.json` + 规则八):`archive` 步的 `Checkpoint` 例外与
     `entities.yaml` 白名单**互证**(`Checkpoint` 确非已登记 `Kind`)✓;`papers[].id` 的跨件引用面(`paper-closeup-48.md:229`)一致 ✓。

---

**Verdict: CONFLICTS FOUND → 4/7 已修(同日)** —— 3 🔴 + 4 ⚠️;其中 C-1 / C-2 / C-3 / S-3 **已落地修复**,
残留 S-1 / S-2 / S-4 待 `/architecture-review` 轮。
全部分类为「裁定已落地、同文件的复述点/引据未刷」与「V-5 按命中点而非按文件计数」两种已知模式,**无设计分歧、无数值分歧、无所有权冲突** —— 修法唯一且机械(改引据 + 补注),不涉机制重裁。

**建议修序(已执行前半)**:~~C-1 → C-2~~(同一改判的两个失效面,一次改完 ✅)· ~~C-3~~(同轴第三处 + 时态订正 ✅)· ~~S-3~~(V-5 收口,ADR-025 自己有判据可挂 ✅)· **S-1 / S-2 / S-4**(账本与附属件,并入 `/architecture-review` 轮 —— 未动)。

**越权边界声明**:本报告**未改动** `tr-registry.yaml` / `entities.yaml` / 任何机制数值 / 任何 AC 的绿/红勾选
—— S-1/S-2 的注册表正文改属 `/architecture-review` Phase 8(承批 20 已登记的「不动 tr-registry」纪律);
S-4 的 `tests/README.md` 属 `/test-setup` 轮附属件。**本报告未 commit**(无提交指令)。
