# 存档位 UI (Save Slot UI, #7b) — Review Log

## ✅ 结案 — 2026-09-19 — **用户裁定 [B]:接受修订、免二轮 ⇒ Approved**

**本节与本文件下方 2026-09-19 评审条目构成同一次处置的两端**:下方记录「首轮评审判 MAJOR REVISION NEEDED
⇒ 用户裁定 [A] 当日整体修订,6 条阻断已全部落盘」,本节记录「修订完成后用户裁定 [B] 接受修订、免二轮 ⇒ 结案」。

**🔴 免二轮 = 显式风险接受,不是「已核对」。** 6 条阻断的修法**自始未经第二双眼睛**(单会话内
撰写 + 修订 + 回填涟漪)。本文件下方条目已记录的「⚠️ 本轮 = 未复核的修订」警告**不因结案而撤销** ——
它随结案**转化为下列重开条件**。

**重开触发条件(任一命中 ⇒ 重启 `/design-review`,并 **须新会话 · 评审者独立于撰写上下文**)**:
1. **7a 的 `OQ-7a-8`(槽位上限 / keep-N 最旧裁剪)在实现期被篡改** —— 本登记「裁剪只丢旧断点入口,
   不违只进不退」是 7b 呈现「空间耗尽 / 裁剪」语义的前提;若 7a 以「无上限」结案(7a 规则十四 + 
   `OQ-7a-8` 不裁),则 7b 的 Edge Case 行判据失效,须重启;
2. **载入安全上下文门与 AC-7a-19 的实现冲突** —— 本门「未决医疗动作中载入 = 7a 拒绝」是 **7b 全部
   `Loadable` 投影的语义源**;若 7a 实现时观察钩子判定 != 7b 的「读因」投影,或 `Gating` 状态被撤销,
   7b 的 F-7b.2 / UI-7b.2 须重启;
3. **`OQ-7b-6`(合作 1–4 人槽位册归属)在 45 的 GDD 轮被改判** —— 7b 的「中性呈现(`SaveSlot` 列表
   来源唯一,无本地按 player 分册逻辑)」是对 45 的**登记**,若 45 以多册 / 按 player 分页结案,7b 的
   槽位列表语义须重启;
4. **`AC-7b-03` / `AC-7b-09` / `AC-7b-13` 载体未兑现** —— 载体欠账表登记的载体(ADR-017 门 A 白名单 /
   audio-system.md 事件名闭集 / 7a 损坏注入夹具)到期未出现,判据即失效,须重启。

**残留未结项(不阻塞,已随结案带入实现期)**:
`OQ-7b-1`(册页容量,外推 `/ux-design`)· `OQ-7b-2`(拒绝文案归属,`/ux-design` + narrative)·
`OQ-7b-4`(损坏回退呈现形态)· `OQ-7b-5`(独立系统 vs 界面,与 `OQ-39-1` 同型)·
**`OQ-7b-6`(合作分册 → 45 的 GDD 轮,ADR-001 窄修订同批)** · 7a 的 **`OQ-7a-8`**(槽位上限 / keep-N)。

**权威状态**:`design/gdd/systems-index.md` row 7b = **✅ Approved(2026-09-19)**。

---

## Review — 2026-09-19 — Verdict: **MAJOR REVISION NEEDED** → **用户裁定 [A] 当日整体修订,6 条阻断已全部落盘**(随后由用户裁定 [B] 免二轮结案)

Scope signal: **M**(四项三类阻断 —— BL-1 落 ADR-010 §六 + 7a 规则十四 · BL-3 落 ADR-010 §Key Interfaces + 7a 回填 · 其余落 7b 自身;不新增运行期系统、不新增 ADR —— **按 39 结案模型,免二轮与否由用户显式裁定**)
Specialists: game-designer · ux-designer · unity-ui-specialist · systems-designer · qa-lead(×5 报告,haiku 并行)· creative-director(opus 串行综合)
Blocking items: **6** | 用户设计裁定: **3(已全部裁定)**
Same-batch fixes: **2(48 断锚 · ≤1 帧 删数)**
专家主张被主评审下调: qa-#1 降 ADVISORY(附走查清单)· systems-M4 升格并入阻断
Prior verdict resolved: **First review**(本文件首条 —— 无前轮)

### Summary(creative-director 终裁)

**CD 判词:39 的病 = 呈现层文档承担存档层职责;7b 正相反 —— 一份呈现层文档替存档层承诺了存档层从未答应的不逆性。**
7a 规则十四的三道反 save-scum 守**全部在写侧**(只进不退 / 读档即锁 / 叙事事件上下文),**读侧本身没有闸**:
存 A → 坏判断 → 读 A(被锁)→ 此刻仍无未决动作 ⇒ 存 A+1(合法)→ 再坏 → 读 A+1……
**每页 = 一次免费重试**;且跨会话锁复位 —— CD 修正原述:绕行**不必退出重启**。

**判定过程核心** —— 6 条阻断全部是「读侧缺口 / 契约 / 投影 / 判据」四类,不是设计错误:
7b 的**呈现内容本身**(册子 · 只进不退 · 拟物 · 零弹窗 · 反幻想)**无一被推翻**;
真正的病灶是「读侧 scum 通道」与「7b 承诺了 7a 从未答应的跨会话不逆性」。

### 🔴 三项用户裁定(均已落盘)

| # | 问题 | 裁定 |
| --- | --- | --- |
| **①** | **BL-1 读侧 scum 通道怎么堵?**(写侧三道守拦不住「读 A → 存 A+1 → 读 A+1」交替绕行;跨会话锁复位) | **[A] 载入安全上下文门** —— 读档与写同钩:手动载入与手动存共用同一观察钩子,都须在**无未决医疗动作**上下文;未决动作中载入 = **7a 拒绝**。锁仍 per-slot 会话内;**锁字段不进存档体**(ADR-010 §六) —— 跨会话复位,防 scum 靠「载入安全上下文门」而非格式锁。7b 辞措收窄:不问「回不回得到判断之前」,问「**能不能洗掉正在处理的这单判断**」 |
| **②** | **BL-2/OQ-7b-3a:打开存档册时抑制移动吗?** | **[A] 不抑制,对齐 39** —— 承 `EC-2-8` 先例;1 的白名单**不加 7b**;「移动仍可,玩家自行脱战」 |
| **③** | **BL-2/OQ-7b-3b:册子是什么形态 / 怎么露面?** | **[A] 42 屏幕空间模态** —— 7b = 42 六屏闭集成员(`ModalId.SaveSlot7b`,`AC-42-F1`),UI Toolkit 屏幕空间,零相机档;开册 = 42 模态(世界交互经 4 冻结,移动仍可);**不发档位意图**(承 39 的 `O-12` 锁 —— 不新开第四档) |

### 🔴 六条阻断级 → 落点

| # | 失效模式 | 内容 | 落点 |
| --- | --- | --- | --- |
| **BL-1** | **读侧 scum 通道(CD 终裁第 1 位)** | 三道守全在写侧;「无未决动作窗口」恰好覆盖出诊前后 ⇒ 存 A → 读 A(锁)→ 存 A+1 → 读 A+1… 每页一次免费重试;跨会话锁复位 | **用户裁定 ① 载入安全上下文门** —— **7a 规则十四**(补 bullet + header 注)· **ADR-010 §六**(锁字段不进存档体)· 7b 规则三/四 + Player Fantasy + Summary + ⭑ 修订块 |
| **BL-2** | **OQ 双问未裁(移动抑制 + 形态)** | 开册是否抑制移动 / 是否发档位意图 / 册子什么形态 —— 全部未裁 | **用户裁定 ②③**:不抑制对齐 39(1 白名单不加 7b)· 42 屏幕空间模态(`ModalId.SaveSlot7b`)· 不发档位意图。**7b Boundary 表 + 规则七 + Edge Cases + Overview + OQ-7b-3 闭合** |
| **BL-3** | **`SaveSlot` 引用却无登记(ADR-022 同款)** | `ISaveService` 签名三处漂移(`Checkpoint(slot)` vs `Checkpoint(SaveSlot)` vs `Checkpoint`);`SaveSlot` struct 无定义处 | **ADR-010 §Key Interfaces**:补 `struct SaveSlot { uint slot_seq; }` + **签名回填**(`Checkpoint(SaveSlot)` 唯一写入口 + `SaveOnExit()` 空参退出钩子豁免反射断言)· **7a `:147` 断言**同步 · 7b 规则五 + 注② 回补 |
| **BL-4** | **单投影失效(`¬Locked` 死项)** | 原 `Writable` 一式把锁写进写侧,而锁管 reads;「不可读」无独立投影 | **F-7b.2 拆两投影**:`Writable` / `Loadable` 分离,`Loadable` 由载入安全上下文门门控;**页级因由三义分通道**(折角 / 笔迹 / 墨色)承载 · UI-7b.2 拆「不可写 / 不可读」两行 |
| **BL-5** | **合册动画 gate 写入(语义错位)** | 原「合册动画路径纳入数值预算」把**呈现**动线写成**写入门** | **Game Feel 修订**:合册 = 呈现的 D 段落,写入是 7a 异步,由合册动画**编排呈现,不 gate 写入**;Input Responsiveness **删 ≤ 1 帧 数值预算**(承 39 先例) |
| **BL-6** | **AC 体系欠账 + 越界判据(qa-#1 + systems 口径)** | 闭集量词禁开式;`AC-7b-10`「由 7a 拒绝」偷测 7a = `AC-7a-19` 真子集;无 [I] 级列;AC 载体无欠账表(照 39 BL-11 先例) | **AC 表重写(13 条)+ 级列举证(含 `[I]`)** · `AC-7b-10` 改 7b 侧投影判据 · 闭集点名(界面元素 `VisualElement` / 面板子集 / 音频事件名枚举)· **AC 载体欠账表**· Edge Cases 补「未决动作外载入 = 7a 拒绝」 |

### 同批必修(非阻断,照 39 结案同日完成模型)

- **48 断锚修复** —— 48 的 GDD 下游表**零 7b 行**(48 = 存档册教学入口,断锚)+ 注① 双向性缺失 ⇒ `tutorial-and-onboarding.md` 下游表补 7b 行 + 注① 正反向照应。
- **≤ 1 帧 删数** —— Game Feel Input Responsiveness 的「≤ 1 帧 开册预算」删(承 39 先例:打开 / 关闭即时,无数值预算)。

### 未结(随结案带入实现期)

`OQ-7b-1`(册页容量 → `/ux-design`)· `OQ-7b-2`(拒绝文案归属 → `/ux-design`)· `OQ-7b-4`(损坏回退呈现形态)· **`OQ-7b-6`(合作 1–4 人槽位册归属 → 45 的 GDD 轮,ADR-001 窄修订同批)** · **7a 的 `OQ-7a-8`**(槽位上限 / keep-N 最旧裁剪 —— 承 systems-M4 / ux-#3;裁剪只丢旧断点入口,不违只进不退)。

> **权威状态**:`design/gdd/systems-index.md` row 7b —— 结案前为 🟡 **In Review**(首轮完成 · 修订已落盘 · 结案待裁)。

---

## 相关文件落盘清单(2026-09-19 修订轮)

| 文件 | 变更 |
| --- | --- |
| `design/gdd/save-slot-ui.md` | header(Status / Implements Pillar / 上游 / 横向修正)· Summary(三条硬语义 + ⭑ 修订块承诺收窄)· Overview(42 屏幕空间模态)· Boundary 表(相机 / 移动抑制不归 7b · 禁载语义 row)· Player Fantasy(载入安全门 + 跨会话不承诺)· 规则三/四/五/七 · **F-7b.2 两投影** · Edge Cases(载入安全门 / `OQ-7a-8` / 受攻击 row)· Dependencies(删 1 玩家控制器行 · 2 相机 row)· Game Feel(合册非写入门 · 删 ≤1 帧)· UI-7b.2(不可读独立投影)· **AC 13 条重写 + 载体欠账表** · OQ-7b-3 闭合 + **OQ-7b-6** |
| `design/gdd/persistence-service.md` | 规则十四补**载入安全上下文门 bullet** + header 注 · `:147` 反射断言回填 · AC-7a-08/09 同口径 · **加载状态机补 `Gating`** · Interactions 7b row · **`OQ-7a-8`** 新增 |
| `docs/architecture/adr-010-persistence-save-format.md` | §Key Interfaces:`struct SaveSlot { uint slot_seq; }` + `ISaveService` 签名回填(`Checkpoint(SaveSlot)` / `SaveOnExit()` 豁免注)· §六 7b 手动槽 row 补载入安全门 + 锁字段不进存档体 · Implementation guideline 6 重写 |
| `design/gdd/tutorial-and-onboarding.md` | 下游表补 7b 行 + 注① 双向性 |