# 采集 (Foraging & Gathering, #17) — 评审日志

> 本文件记录 `design/gdd/foraging.md` 的每一次 `/design-review` 结论、
> 阻断项清单与后续修订处理。**新增条目一律追加在文件末尾。**

---

## Review — 2026-09-19 — Verdict: MAJOR REVISION NEEDED

Scope signal: **L**(边界偏 XL —— 触及 ADR-009 骨架、ADR-001 通道缺口、4/21a/20/42 四系统接口)
Specialists: game-designer · systems-designer · qa-lead · economy-designer · ux-designer · unity-specialist · creative-director(串行终审)
Blocking items: **7**(根因 P0-1…P0-7)| Recommended: **14**
Summary: 三态分类中 17 的「资源点」被**混用**(定义 = 派生态 / 余量 = 派生态),而 17 对外只发 `DropSpawned` / `DropClaimed` —— 于是 (a) `ResourceHarvested` 在 ADR-009 §三 骨架里**孤悬无写者**,(b) §二 分类被**静默改写**(避涟漪式改判),(c) 采集事实(`node_id` / `out_quality`)无处承载。幻想层亦被 17 **代持**(第一层归 21a+42、第二层归 18)。终审对锚点问题「幻想是否交付」的回答是**否**。
Prior verdict resolved: **First review**

### 七条根因与落点

| 根因 | 内容 | 落点 |
|------|------|------|
| **P0-1** 「避涟漪式改判」(**新变体**) | 为少改 ADR 而孤立一个 Kind + 静默改判已 Accepted 的 ADR-009 §二 + 丢失自身所需事实 | 用户裁定 **[B] 复活 `ResourceHarvested`** → **ADR-009 Amendment K**;§三 骨架注 + §六 有界性同步;`entities.yaml` 登记 |
| **P0-2** 「外抛即结案」(第 4 次复发) | ADR-001 无**上行意图通道**;10 / 20 / 4 / 18 均已登记,17 登记为零 | **`OQ-17-6`** 登记,与 `OQ-10-9` / 20-BL-4 **同批裁定**(用户裁定:登记并并批) |
| **P0-3** 「幻想代持」(**新**) | 17 把三层幻想全写在名下;其中第 1 层归 21a+42、第 2 层归 18,17 **只拥有第 3 层**(隐性熟练度),且该层原**不可证伪** | §Player Fantasy 重写为「17 拥有面」;两层显式 **EXTERNAL**;第 3 层补**推断回路**(药 → 18 → 服 → 见效 → 逆推品级低)+ 三必要条件 + `min(quality)` 链断前置(`OQ-17-12`) |
| **P0-4** 定点域口径未闭合 | F-17-1 `U` 为实数、CDF 边界含糊、`quality_distribution[]` 类型未定 | F-17-1 重规格:`U = h >> 16`(整数)· CDF 边界严格 `<` · 分布数组**定 `int`**(D-21-17)· 装载期 `Σw ≥ 1` 校验 |
| **P0-5** 「借来的绿」+ 记账面 | AC 依赖未回写的 Rn / 未定的 OQ,却按绿计 | AC 加**级别列**(BLOCKING / ADVISORY / **EXTERNAL 3**)· `AC-17-02` 标 **`EXTERNAL · BLOCKED-BY-ADR-012`** · `AC-17-05` 改**反射断言**(非 grep) |
| **P0-6** 资源点驻留面未登记 | 分块流式下「未驻留 chunk 的资源点」语义无值源 | **`OQ-17-7`** 登记(与 `OQ-6-8` 同源) |
| **P0-7** 呈现事实 + 拒绝反馈不可分辨 | 「采到了」「没采到」「这块采空了」三者反馈同形;且 17 原稿把**准星**当光标 | 用户裁定:**登记 42 的阻断呈现规格**(补可读**采前**线索)· §UI 承 4 的「脚与身位即光标」纠正「准星」· §Edge Cases 补**失败收敛**(三命名失败 × 三载体)× `AC-17-07b` / `07c` |

### 关键裁决(用户 2026-09-19,四项均照准)

1. **下一步**:[A] 现在就修订 —— 一并处理全部阻断项
2. **ADR-009 形态**:**[B] 复活 `ResourceHarvested`**(终审推荐)
3. **幻想第 1 层 / P0 首动词**:登记 42 的**阻断呈现规格**(补可读采前线索)
4. **ADR-001 欠债**:登记并**并批**(与 `OQ-10-9` / 20-BL-4 同一修订)

### 三条事件(采集一次动作 = 三条世界流事件)

```
ResourceHarvested { instance_id, node_id, gather_seq, qty, out_quality }   // 采集事实 —— 17 的 Kind
DropSpawned       { instance_id, spawn_anchor = node.cell, item_key, qty } // 身份出生
DropClaimed       { instance_id, claimer = 采集者 }                         // 立即归属
```

- **`raw_quality` 不进流**(由 `out_quality` + `skill` + seed 可重算);**`out_quality` 必须进流**。
- **`gather_seq` 只计 `ResourceHarvested`**(不被无关掉落污染)。
- ADR-009 §六 **有界性就地修正**:资源点**可再生** ⇒ 消耗事件数上界 = **玩家动作率 × 会话时长**,**不是**资源点数(原写「≤ 资源点数」是陈旧口径)。

### 涟漪(六件,均已落盘)

| # | 文件 | 变更 |
|---|------|------|
| 1 | `docs/architecture/adr-009-world-state-event-boundary.md` | 新增 **Amendment K**(§三 骨架注 · §六 有界性 · 全文裁决块)|
| 2 | `design/registry/entities.yaml` | 新增 `SimEvent.Kind.ResourceHarvested` 条目 |
| 3 | `design/gdd/inventory-and-items.md` | fold 谓词并入 `ResourceHarvested`(否则 `(item_key, quality)` 堆叠键重建不出 quality)· **R11 标记已闭合** |
| 4 | `design/gdd/skill-system.md` | novelty 表采集行 **2 → 3**(补金鸡纳树皮)+ 订正注 |
| 5 | `design/gdd/systems-index.md` | 行 17 全面重写 · 批量行 517 · 计数块 |
| 6 | `design/gdd/foraging.md` | 本体全量重写(12 OQ / 27 AC 四组 / 4 公式)|

### 后续结案

- **2026-09-19 用户裁定「接受修订、免二轮」⇒ ✅ Approved(显式风险接受结案)**。
- **⚠️ 免二轮 = 显式风险接受,不是「已核对」**。重开触发条件四条:
  1. `OQ-17-1`(深水线 `Capacity` / `RegrowWindow` / `SEASON_MULT[]` **值**)用户裁定落地;
  2. `OQ-17-6`(ADR-001 意图上行通道)由 45 的 GDD 轮裁定(与 `OQ-10-9` / 20-BL-4 同批);
  3. 42 的**采前线索呈现规格**在实现期证明不可读(根因 P0-7);
  4. `OQ-17-12`(`min(各输入 quality)` 链式截断)在 21a 侧被证伪。

### 二轮抽查点(如重开,优先复核)

- `AC-17-02` **EXTERNAL** 不得记绿(仍 `BLOCKED-BY-ADR-012` 三格 CI + F7 spike)。
- `AC-17-05` 须确为**反射断言**而非 grep(F-1-6 跨格检测必然读 `Vector3`)。
- F-17-3 的 `YieldDecay` **单调非增**是否在 `DECAY_FLOOR` 处被误写为「可恢复」。
- `ResourceHarvested` 与 20 的 fold 谓词是否**双向**一致(17 发出 / 20 消费同一字段名)。
