# Story 023: M2 形态件③(急救零数字 + 可跳过 —— 跳过入口元件 · 零数字角标门 · 焦点落点形态)

> **Epic**: 拟物 UI 框架
> **Status**: **Complete ✅**(2026-10-09 立 · 用户指令「开始做形态件③」;**同日三步全交付** —— ① 跳过入口元件形态 · ② 零数字角标门(G2 机械化) · ③ 焦点落点形态)
> **Layer**: Foundation
> **Type**: UI / Visual-Feel
> **Estimate**: 1.0 人日
> **Manifest Version**: 2026-10-09
> **Last Updated**: 2026-10-09

## Context

**GDD**: `design/gdd/emergency-procedures.md`(规则六 跳过路径 · 规则六之甲 跳过入口位置 · `O-10-1` 跳过入口 owner = 42)
**Requirement**: `production/milestones/README.md §三` M2「4 项形态件」**③ 急救零数字 + 可跳过** ——
「反数值化是 pillar;数字角标会绕开要证的东西」(`milestones:84`)
**ADR Governing Implementation**: ADR-013(拟物 UI 框架 · §三 呈现归 42 · 跳过入口 owner)
**判据权威**: `design/art/art-bible.md` **G2**(「反数值化」是否真的成立,:1234):
「无血条无小地图时,玩家是否仍能找到信息 …… 灰盒本身就是『色块 + 数字』的形态 ⇒ 它自证『反数值化不需要做』,
恰恰绕开要证的东西」+ `emergency-procedures.md:192`(分值 = 数字 = 违反反幻想)·
规则六之甲(:301-325:跳过入口 = 候选动作列表上的一项,**10 不拥有任何 UI 元素,10 向 42 提规格**)

**Engine**: Unity 6.3 LTS | **Risk**: LOW(纯 USS + EditMode 测试,零 post-cutoff API)
**Engine Notes**: 不涉引擎新 API;焦点落点判据复用 story-021 的 `--skeuo-shared-row-height` 变量基建。

**Control Manifest Rules (this layer)**:
- Required: 跳过入口**走主题变量**(C2 门);零数字角标机械化为 EditMode 判据(G2);
  跳过入口作为焦点落点满足 AB-3(≥44×44 的高半)
- Forbidden: USS 内联色值;跳过入口带 `background-image`(焦点单槽铁律);新贴图;新主题变量(全复用)
- Guardrail: 本 story 只交**形态半** —— 施加点 / 候选列表载体 / `Idle` 态时序归 10 实现轮(见下方边界裁定)

### 边界裁定(2026-10-09 勘察轮定,不再逐条问用户)

1. **只交形态半** —— 跳过入口**元件形态**(类 + 焦点落点 + 零数字)归本 story;
   **施加点 / 候选动作列表载体 / `Idle` 态可达时序**(规则六之甲的状态机)归 **10 实现轮 + 数据绑定轮**
   (本 story 零施加点,同 story-022「形态对先立」先例)。
   ⚠️ **AB-3 宽半(列表项宽 ≥44)由 10 轮候选动作列表载体锁定** —— 023-3 只锁高半
   (min-height),宽度是载体布局属性,登记防责任真空(本 story 不新增测试)。
2. **零数字 = 呈现结构面** —— `JudgeResult` 枚举非分值(`emergency:192/:563`)由 10 的 GDD 已裁、归 10 实现;
   本 story 机械化**呈现侧**数字角标载体(USS `content:` 数字 / `X/N` 形态)——
   `AC-42-F3`「禁退化为数字角标 / `X/N`」的字面落成门。
3. **VR 急救 UI = P1b 不进本批** —— 本批为**平面侧**形态预备(`game-concept:723` 范围阶梯;42 规则十二)。
4. **`.skip-entry` 不进 Registry** —— 语义样式类(无贴图槽),同 `.ruled`/`.empty-row` 先例;
   变体机制与状态类机制均不适用。
5. **焦点单槽铁律(承 `SkeuoFocusVisible.uss:27-35`)** —— 跳过入口用 `background-color` 纸底、
   **零 `background-image` / 零 `border-*`**(与 `.focus-visible` 声明属性集**零交集**;
   `.ruled` 的 border 冲突是 story-021 已登记的别案,**本类不得新增同类**)。

## Tasks

### 步① 跳过入口元件形态(`O-10-1` 规格的 42 侧形态半)

- [x] **AC-023-1** ✅ **2026-10-09**: `SkeuoPaper.uss` +`.skip-entry`(纸签形态:aged 纸底 +
      ink 墨字 + 行高/缩进全走 var,**零新变量零新图**);头注锚 `O-10-1`(10 向 42 提规格)/
      规则六之甲(`Idle` 态可达归 10 轮)/ 零施加点;
      `test_ac023_1_skip_entry_element_declared_with_theme_discipline` 锁:类块存在 +
      **属性集 ⊇ 点名六属性 + 逐声明值 `var(--skeuo-`**(评审 BLOCKING 修复:原「≥5 计数」对
      删行/具名色/异命名空间三分支假绿)+ 紧邻头注锚(O-10-1 / owner / 10 实现轮三关键词)+
      **Registry 零注册守卫**(边界④)+ **全库声明块恰一**(后置覆盖复活面)
      **交付**: `SkeuoPaper.uss` `.skip-entry` 声明块 + 测试

### 步② 零数字角标门(art-bible G2 反数值化机械化)

- [x] **AC-023-2** ✅ **2026-10-09**: `test_ac023_2_emergency_presentation_zero_numeric_badge`
      —— **剥注释后**扫全库 Skeuo `*.uss`:① 零 `content:` 含数字(数字角标的唯一 USS 载体;
      现状全库零 `content:` ⇒ 新增即红)② 零 `X/N` 形态(`\d+\s*/\s*\d+`;注释内
      「40/39/43/43」等实测记录剥除后基线零命中 —— 故**必须剥注释**,否则误红);
      ③ `.skip-entry` 块内双模式复查。G2 语义头注:灰盒「色块+数字」自证反数值化
      不需要做 ⇒ 本门让「数字进急救呈现」从静默变红。
      ⚠️ 百分比不扫(`width: 100%` 是布局非角标;`%` 角标载体在文本内容层,归 UXML/C# 侧后续轮)

### 步③ 焦点落点形态(可跳过的可达性形态半)

- [x] **AC-023-3** ✅ **2026-10-09**: `test_ac023_3_skip_entry_focus_landing_compatible` ——
      ① `min-height` 锚 `var(--skeuo-shared-row-height)`(= 44 = AB-3 焦点落点 ≥44×44 高半,
      承 story-021 变量;跳过入口是**列表项焦点落点**,可达形态的尺寸承诺);
      ② 零 `background-image`(焦点单槽铁律 —— 环图整槽替换会顶掉纸纹);
      ③ **`.skip-entry` 声明属性集 ∩ `.focus-visible` 声明属性集 = ∅**
      (动态解析两块属性集比交集;消息点明「`.ruled` 冲突是 story-021:52 已登记别案」——
      本断言守的是**本类不新增同类**,非追溯 ruling 旧案)

## Dependencies

- **Blocked-by**: story-021(`--skeuo-shared-row-height` 变量基建)✅ · story-022(形态件②先例:状态/形态半边界裁定法)✅
- **Enables**: M2 Exit Criteria「4 项形态件」条 ③ 本体 · art-bible **G2** 转可验(USS 结构面半;文本内容层归 UXML/C# 后续轮)·
  `O-10-1`(10→42 跳过入口规格)的呈现侧预备
- **不阻塞**: 形态件④;10 急救实现轮(施加点/候选列表/Idle 时序)

## Acceptance Criteria

- [x] 三步 AC 全勾,测试过滤绿 + 变异恰红 ✅ **2026-10-09**
      (过滤 236/223/0 红/13 跳 = 基线 +3;变异 4+2 发全中;**全量 EditMode + PlayMode 零回归归 DoD 留痕**
      —— 评审 BLOCKING 修复:原勾「全量零回归」早于实际全量跑,时序虚报收窄;
      ✅ 全量已跑:3013/2966/0 + 98/97/0,见 DoD)
- [x] 文档同批回刷:`milestones:131` 形态件③ ◐ 注 · sprint-04(形态件 2/4→**3/4**)·
      EPIC(023 行 + Counts 22→23)+ index + active.md ✅ **2026-10-09**
- [x] **零新图 · 零新主题变量**(全复用)· 零内联色值(C4/C2 绿)·
      边界裁定五条齐(不冒充 10 轮的实现面)✅ **2026-10-09**

## DoD

- [x] 过滤测试绿 + 变异注入恰红 ✅ **2026-10-09** —— 过滤 236/223/0 红/13 跳(基线 +3);
      变异 **6 发全中**(4 初轮 + 2 修复补验:删行/具名色均恰 023-1 红),逐发反向恢复零残留
- [x] 全量 EditMode + PlayMode 留痕 ✅ **2026-10-09** —— EditMode **3013/2966/0 红/46 跳/1 inc**;
      PlayMode **98/97/0 红/1 跳**(均 = 基线 +3,零回归)`editmode_full_20261009_form03.xml` /
      `playmode_full_20261009_form03.xml`
- [x] **双代理评审(恰一轮)→ 修复 → 复跑绿** ✅ **2026-10-09** ——
      评审原件 `production/qa/evidence/review-story-023-form-item-3-2026-10-09.md`
      (代码面 FIX-THEN-APPROVE 1B+4A · 测试面 FIX-THEN-APPROVE 1B+7A ⇒ **13 项同批修复全落**;
      BLOCKING 双修 = ① story AC 全量留痕时序虚报收窄 ② 023-1 属性集 ⊇ + 逐声明 var
      (原 ≥5 计数三分支假绿,补 MUT-5/6 恰红实证)⇒ 转 APPROVE)
- [x] 用户指令后提交 ✅ **2026-10-09**(用户流程明示「收口提交推送」)
