## Consistency Check Report
Date: 2026-09-17
Registry entries checked: 0 entities, 0 items, 19 formulas, 73 constants
Scope: since-last-review(7a persistence-service 修订后)
GDDs scanned: 18(全 design/gdd/*.md)

> 本轮 = 7a 首轮 `/design-review` 修订落盘 + 用户裁定 Approved 后的**复跑**,重点核对
> 修订引入的新物(规则六双触发 · 规则十四手动槽契约 · `EVENT_CHECKPOINT_ANCHORS` ·
> F-7a-3 空集约定与 ItemInstanceId 扫描域 · 校验和段置首 · wantsToQuit 钩子)与其 16 份
> 既有 GDD / ADR / registry 是否冲突。**数值层零冲突**;发现 1 处 registry 陈旧 + 4 簇
> 锚点登记缺口 + 3 处陈旧引用 + 2 处 ADR-GDD 漂移。

---

### Conflicts Found (must resolve before architecture)

**无 🔴 数值冲突。** 本轮核对全过:
- F-7a-4 豁免口径 ↔ 9 的 R3.6(`lethal = false ∧ plateau`)+ AC-36(`plateau` 型与 `relapse.*` 不折叠):
  7a 规则八与 9 的 `disease-simulation.md:313-314/340/416-428/562/1707` **一致**;心衰 / 伤寒 =
  `plateau ∧ lethal`(可达终态、可折叠),豁免类限定 `lethal = false` 正确,无误豁免。✅
- F-7a-3 空集约定(`max(∅) = −1`)与 `ItemInstanceId` 扫描域 = 三流并集:21a(`item-database.md`)
  **无**「世界流 Drop 窄域」残留;ADR-007 §四 `PatientId.None = −1` ✅
- AC-7a-08「恰三处」:7a 内文一致(125/129/480/571-572),全仓无第四入口声称 ✅
- AC 计数:20 条全 BLOCKING;systems-index「18 BLOCKING + AC-7a-18 全局浮点门 + AC-7a-19/20」
  = 18(01–18)+ 19/20 = 20 ✓ 自洽。14 条规则实存(规则一…十四)。
- `CHECKPOINT_INTERVAL` 5 min / 1–30:ADR-010 §六 · 7a 旋钮 · registry 三方一致 ✅
- `SAVE_EXIT_RETRY_TIMEOUT` / `WRITE_RETRY_DELAY` / `BAK_ROTATION`:7a 旋钮 ↔ registry 一致 ✅

---

### Stale Registry Entries (registry behind the GDD)

⚠️ **`next_patient_id`(entities.yaml:900,formulas 区)**
   Registry says: `constraint: "max ≥ 0 恒成立(三流全空仅含 None 时 max = −1 ⇒ next = 0)"`
   7a F-7a-3 现已改(2026-09-17 首轮评审 B1 修订):`{…} 非空时 max ≥ 0`;`max(∅) = −1`
   (空集约定,`max(∅)` 数学无定义钉成约定)。
   → **registry 仍保留被评审否掉的自相矛盾措辞**。更新 constraint 为「`{…}` 非空时 max ≥ 0;
   `max(∅) = −1`(空集约定);计数器永不复位 0;折叠行保留 patient_id」。

---

### Anchor 登记缺口(7a 修订引入的具名引用未闭环)

**S1 — 7a 内文锚点归属自相矛盾(行 54 vs 行 127)**
   `persistence-service.md:54`(Player Fantasy):「初稿锚点白名单归 **37/9/52**」;
   `:127`(规则六):「脉案落笔 / 病例结案(**37**)· 出诊启动(**52**)」。9 疾病模拟**不是任何
   一个锚点的产生者**(它产 onset / progress 病史事件,非叙事锚点);「脉案落笔」的产生者是
   **8 诊断**(词表 + 状态机,`diagnosis-system.md:337/740`)+ 持久化归 **39 脉案**。行 54 的
   「37/9/52」为**笔误**,应为「8/39(脉案落笔)· 37(病例结案)· 52(出诊启动)」。

**S2 — 锚点 ①「脉案落笔」双向登记缺失(8/39 方向)**
   7a 具名白名单 `EVENT_CHECKPOINT_ANCHORS = {脉案落笔 · 病例结案 · 出诊启动}`,其中
   病例结案(37)与出诊启动(52)在 7a §Interactions 有行;**「脉案落笔」对应的 8/39 无任何登记**:
   7a 依赖表无 8 / 39 行,`diagnosis-system.md` 无 7a / checkpoint 反向提及(8 只说
   「读数归 39、判断归 37、8 不拥有持久化」,未登记「落笔 = 7a checkpoint 锚点」);
   **39 脉案 GDD 全仓不存在**(`skeuomorphic-ui.md:642` 亦证 39 无 GDD)。规则六 / 规则十四
   (手动槽「叙事事件同步点」)均依赖此锚点 —— 依赖**单边**。

**S3 — 锚点 ③「出诊启动」在 52 侧无具名事件**
   7a 具名引用「**出诊启动**叙事事件」(`:367` / 规则六 / `EVENT_CHECKPOINT_ANCHORS`),
   但 `random-events.md` 全文**无「出诊启动」字面**(仅「出诊途中 / 出诊路径 / 出诊目标」,
   且 52 为 Needs Revision)。具名引用对方无此物 —— 与 ADR-022 批评的「引用却无登记」同型。
   7a 已声明「只出观察钩子形状,锚点归各系统」,故修复点在 52 侧:登记「出诊启动」事件名。

**S4 — `EVENT_CHECKPOINT_ANCHORS` 未入 registry(跨系统常量)**
   该白名单的成员 = 8/39/37/52 四个系统的具名叙事事件,是**真跨系统事实**(7a 触发 checkpoint
   的锚点集),却只住 7a 内部旋钮表、未登记 entities.yaml。7a 旋钮表现有 6 个旋钮,registry
   只登记了 4 个(CHECKPOINT_INTERVAL / SAVE_EXIT_RETRY_TIMEOUT / WRITE_RETRY_DELAY /
   BAK_ROTATION)。

---

### ADR-GDD 漂移(7a 修订后权威侧在 GDD,ADR 待回填)

⚠️ **ADR-010 §六 存档时机未回填双触发与 wantsToQuit**
   `adr-010:205-210` 仍写「定期 checkpoint 默认 5 分钟」+「退出保存同步(`OnApplicationQuit`
   …限时 join)」。7a 规则六已裁**事件触发为主 + 定时兜底**(`CHECKPOINT_INTERVAL` = 兜底间隔),
   规则十三已裁**`wantsToQuit` 为主、`OnApplicationQuit` 兜底**(后者 Editor 停播亦触发、
   崩溃 / 强杀不触发)。7a 为权威侧(其 GDD 首轮评审已 Approved),ADR-010 两处待回填注记。

---

### Stale「7a 无 GDD」References(7a 已落盘 + Approved)

ℹ️ **`telemetry-analytics.md:427`** —— 「✅ Accepted(**7a 无 GDD**)」→ 已过时(7a GDD 已
   Approved)。ℹ️ **`disease-simulation.md:7`** —— 「7a 持久化服务(**无 GDD**,契约已由 ADR-010
   收敛)」→ 过时。ℹ️ **`disease-simulation.md:1211/1216`** —— 「**契约(无 GDD)**」「7a 是唯一一条
   『无 GDD 的硬上游』」「7a 的 GDD 撰写时**必须回来对齐**本节的七个抽象点」→ 7a GDD 已落盘且
   已对齐(规则一 / 八 / 十二),承诺已兑现,措辞待改。
   正确不动的:`skeuomorphic-ui.md:642` 指 **7b** 无 GDD(7b 确实无)✅ · `architecture-review-
   2026-09-15.md:66` 为历史评审快照 ✅。

---

### Clean Entries (no issues found)

✅ 20/20 条 AC 编号连续(01–20,全 BLOCKING)✅ 14 条规则实存 ✅ F-7a-3/4 与 9/37 对拍一致
✅ 21a 扫描域无窄化残留 ✅ 全仓无第四存档入口声称 ✅ 7a 三触发点与 ADR-010 §六 三行相容
✅ `next_patient_id` 除外,其余 18 条公式 + 69 条常量与 GDD 无冲突。

---

Verdict: **PASS(数值层)· 4 处锚点登记缺口 + 1 处 registry 陈旧 + 3 处陈旧引用 + 2 处 ADR 待回填**

Resolution steps(按修复点):
1. `entities.yaml:900` — `next_patient_id.constraint` 改空集约定措辞(⚠️ STALE)
2. `persistence-service.md:54` — 「37/9/52」→「8/39 · 37 · 52」(S1 内文矛盾)
3. 7a §Interactions 补 8/39 行 + `diagnosis-system.md` 补反向注 + 52 侧补「出诊启动」登记(S2/S3)
4. `entities.yaml` constants 区补 `EVENT_CHECKPOINT_ANCHORS`(S4)
5. `telemetry-analytics.md:427` · `disease-simulation.md:7/1211/1216` 陈旧引用更新
6. ADR-010 §六 回填注记(双触发 + wantsToQuit)
