# Story 024: M2 形态件④(一条真实状态反馈通道 —— 体征词条 → 五通道分发 · 通道值区形态 · 真表端到端)

> **Epic**: 拟物 UI 框架
> **Status**: **Complete ✅**(2026-10-09 立 · 用户指令「开始做形态件④」;**同日三步全交付** —— ① 分发纯函数 · ② 通道值区形态对 · ③ 真表端到端判据)
> **Layer**: Foundation
> **Type**: UI / Visual-Feel
> **Estimate**: 1.5 人日
> **Manifest Version**: 2026-10-09
> **Last Updated**: 2026-10-09

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`(:64-65「状态反馈全部压在质感、纸声、焦点反馈上……
『我到底看懂了没有』必须由**纸面的物理形态**回答」· 记号登记表 · 五通道区)
+ `design/gdd/diagnosis-system.md`(**规则五 四态语义** · **F-8.2 空档回退**(:1003-1011) ·
§Edge Cases(:1178-1186 阴性形态/并列不相斥) · `:329`「未查 = 空行,不是 badge」)
**Requirement**: `production/milestones/README.md §三` M2「4 项形态件」**④ 一条真实状态反馈通道** ——
「playtest 要测『玩家读懂了没有』,灰盒无反馈通道可读」(`milestones:87`)
**ADR Governing Implementation**: ADR-013(拟物 UI 框架 · 42 只渲染)· ADR-014(真表为版本化数据)
**判据权威**: **8 的映射真源 `SignLexemeRow`**(`Gameplay.Presentation/Diagnosis/DiagnosisSignTable.cs` ——
`Channel` 五通道归属 + `DisplayWords` 三档词 + `Polarity` 极性,**7 字段已过冻结轮**)
+ 上述 GDD 四态语义。**通道 = 病人体征状态(已查词条)→ 纸面脉案通道行** —— M2 playtest
核心循环(1 病人 1 诊断)里玩家要读懂的第一状态。

**Engine**: Unity 6.3 LTS | **Risk**: LOW(纯 C# 纯函数 + USS + EditMode;零 post-cutoff API)
**Engine Notes**: `Gameplay.UI.asmdef` references 已含 `Gameplay.Presentation` ⇒ 分发器直接消费
`SignLexemeRow`,零跨层新接缝;真表 `assets/data/diagnosis_signs.json`(34 行)为版本化 fixture。

**Control Manifest Rules (this layer)**:
- Required: 分发 = **纯函数**(零状态零副作用);通道/词/极性**全消费 `SignLexemeRow`**(42 零自定义映射);
  值区形态走主题变量(C2);真表端到端判据(34 行驱动)
- Forbidden: 42 定义任何「体征 → 通道/词」映射(归 8);「未查」徽章/灰字/占位符(`:329` 硬禁);
  为过判据改真表;新贴图
- Guardrail: 本 story 只交**通道半** —— 运行时绑定(查体 revealed 集的产生、挂到脉案行)归
  9 实现轮 + 数据绑定轮(见下方边界裁定)

### 边界裁定(2026-10-09 勘察轮定,不再逐条问用户)

1. **只交通道半** —— 分发纯函数 + 值区形态 + 真表判据归本 story;
   **运行时绑定**(哪些体征 revealed = 9 查体链;挂行 = `AddChannel` 值区元素创建)
   归 **9 实现轮 + 数据绑定轮** —— 今日查体链生产路径未立,**零施加点**(同 story-022/023 先例;
   `CasebookScreen.AddChannel` 本轮**不改**)。
2. **映射归 8 不越界** —— `Channel` / `DisplayWords` / `Polarity` 全部来自 `SignLexemeRow`
   (8 的表);分发器只做**分流 + 档位取词 + F-8.2 回退**,零自定义语义。
3. **文案归 8/叙事** —— 「已查 · 无异常」「未查」等文案不由本 story 定;
   **未查 = 空行**(复用 story-021 `.empty-row`,`:329` 明文非徽章)—— 本轮**零新增未查态类**。
4. **把握不足/墨色笃定度曲线归数值轮**(V-8.3)—— 本 story 只出**阴性形态类对**
   (墨色降档基线 `--skeuo-ink-faded`);`:1185`「把握不足不以文字或标记出现」由类对 + 零徽章断言守。
5. **真表数值不动**(`neg_weight` 占位承 8)—— 端到端判据只读不写,fixture 为版本化源 JSON。

## Tasks

### 步① 通道分发纯函数(F-8.2 + 四态语义机械化)

- [x] **AC-024-1** ✅ **2026-10-09**: 新 `SignChannelBinder.cs`(`Gameplay.UI/Skeuomorphic`)——
      `Bind(IEnumerable<SignLexemeRow> revealed, int slot) → IReadOnlyDictionary<SignChannel, ChannelReading[]>`:
      ① 按 `Channel` 分流(五通道闭集);② 档位取词 = **从 slot 起向数组尾找第一个非 null**
      (F-8.2 `:1003` 字面:粗档无词 → 中档);③ 全 null ⇒ `Word = null`(**读不出 → 阴性形态**,
      `:1005` 非阳性);④ 多词条**并列保序**(`:1183` 不相斥);⑤ 输入序透传;
      纯函数零状态。`test_ac024_1_…binder_dispatch` 夹具词条全分支覆盖 + 越界 slot fail-loud。

### 步② 通道值区形态对(阳性墨字 / 阴性降档)

- [x] **AC-024-2** ✅ **2026-10-09**: `SkeuoPaper.uss` +`.channel-reading`(阳性:ink-fg 墨字)+
      `.channel-reading-negative`(阴性形态:ink-faded **墨色降档**,零标注零徽章——`:1185` 承载形态
      即把握不足);未查**不新增类**(复用 `.empty-row`,`:329` 反 badge);
      头注锚四态与归 8 边界。
      `test_ac024_2_…value_area_forms` 锁:两类存在 + 全 var + 阳性/阴性**色档可分**
      (fg vs faded 同族不同档)+ **全库零「未查徽章」类**(`unchecked|unread|no-data` 命名面)+
      块内零数字。

### 步③ 真表端到端判据(34 行驱动 · 通道闭环可证伪)

- [x] **AC-024-3** ✅ **2026-10-09**: `test_ac024_3_…real_sign_table_roundtrip` ——
      读真表 `assets/data/diagnosis_signs.json`(34 行,版本化 fixture,门式读仓)→
      中文 `channel` → `SignChannel` 映射 → 构造 `SignLexemeRow` → `Bind`:
      ① 34 词条全落合法通道(五枚举闭集,零漏零溢);② 真表**零空串词**(GDD 禁空串,
      空串与阴性形态呈现层同构);③ 同通道多词**并列保序**;④ 档位取词三 slot 抽查 +
      空档夹具**向尾回退**;⑤ 全空档夹具 ⇒ `Word = null`(读不出)。

## Dependencies

- **Blocked-by**: story-011(五通道区渲染壳)✅ · story-021(`.empty-row` 未查态载体 + `.ruled` 行)✅ ·
  8 的 `DiagnosisSignTable` 真表(冻结轮过)✅
- **Enables**: M2 Exit Criteria「4 项形态件」条 ④ 本体 · M2 playtest「玩家读懂了没有」的
  **第一可读状态**(病人体征 → 脉案)转可验 · 9 查体链实现轮的 42 侧接点(分发器)
- **不阻塞**: 形态件清单就此 4/4 齐;9/数据绑定轮的运行时接线

## Acceptance Criteria

- [x] 三步 AC 全勾,测试过滤绿 + 变异恰红 ✅ **2026-10-09**(过滤 **239/226/0 红/13 跳** = 基线 236/223 +3;
      变异 **8 发全中**;全量 EditMode + PlayMode 零回归归 DoD 留痕,照 story-023 时序口径)
- [x] 文档同批回刷:`milestones:131` 形态件④ ◐ 注(整条转勾评估)· sprint-04(形态件 3/4→**4/4**)·
      EPIC(024 行 + Counts 23→24)+ index + active.md ✅ **2026-10-09**
- [x] **零新贴图 · 零施加点**(`AddChannel` 不改)· 零自定义映射(全消费 `SignLexemeRow`)·
      零内联色(C4/C2 绿)· 边界裁定五条齐 ✅ **2026-10-09**

## DoD

- [x] 过滤测试绿 + 变异注入恰红 ✅ **2026-10-09** —— 过滤 239/226/0 红/13 跳(基线 +3);
      变异 **8 发全中**(6 初轮 + 2 修复补验:极性硬编码 MUT-7 / 空串按 null MUT-8),逐发反向恢复零残留
- [x] 全量 EditMode + PlayMode 留痕 ✅ **2026-10-09** —— EditMode **3016/2969/0 红/46 跳/1 inc**;
      PlayMode **98/97/0 红/1 跳**(均 = 基线 +3,零回归)`editmode_full_20261009_form04.xml` /
      `playmode_full_20261009_form04.xml`
- [x] **双代理评审(恰一轮)→ 修复 → 复跑绿** ✅ **2026-10-09** ——
      评审原件 `production/qa/evidence/review-story-024-form-item-4-2026-10-09.md`
      (代码面 FIX-THEN-APPROVE 1B+2A · 测试面 APPROVE 2A ⇒ **去重 5 项同批修复全落**;
      BLOCKING = AC-024-3「全落合法通道」由 switch 保证与 `Bind` 守卫结构不等价 ⇒ 增真表原文值集
      + Bind 产物键集双独立断言;补 MUT-7/8 恰红实证)⇒ 转 APPROVE
- [ ] 用户指令后提交(用户流程明示「收口提交推送」)
