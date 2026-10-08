# Story 021: M2 形态件①(脉案线格 · 空行等重 · 焦点明度轴)

> **Epic**: 拟物 UI 框架
> **Status**: **Complete ✅**(2026-10-08 立 · 用户裁定承载 = 新开 story;**同日三步全交付** —— ① 线格挂行 · ② 空行等重声明层 + B2 转正 · ③ 贴图级明度轴 0.266 ≥ 0.12)
> **Layer**: Foundation
> **Type**: UI / Visual-Feel
> **Estimate**: 1.0 人日
> **Manifest Version**: 2026-10-08
> **Last Updated**: 2026-10-08

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`(AC-42-B5 · 规则十焦点可见)· `design/ux/casebook-39.md`(§6 空行四态 · AC `[A]` 视觉等重)
**Requirement**: `production/milestones/README.md §三` M2「4 项形态件」**① 脉案线格/空行/明度轴** ——
「判断链的读入形态:无血条无小地图,可读性全靠纸面物理形态」(`milestones:84`)
**ADR Governing Implementation**: ADR-013(拟物 UI 框架 · 自建 USS 元件库)
**判据权威**: `design/art/art-bible.md` **G1**(格线语义,:1233)+ **G3**(焦点可读性明度轴,:1235);
明度阈值 **≥12% = §4.6【本稿提案】值**,归数值轮(:25/:68)

**Engine**: Unity 6.3 LTS | **Risk**: LOW(纯 USS + EditMode 测试,零 post-cutoff API)
**Engine Notes**: 不涉引擎新 API;`Texture2D.LoadImage` 为长期稳定 BCL 面。

**Control Manifest Rules (this layer)**:
- Required: 格线线宽/色**走主题变量**(C4 门 `HardcodedSizeRegex` 抓 `border-*-width:\s*\d+px`);
  变量须在 `SkeuoThemeVariables.uss :root` 声明(C2 门);明度轴判据跑**真贴图**上
- Forbidden: USS 内联硬编码 px 线宽;为过判据填占位阈值;形态件① 追加新贴图(冻结件不重开)
- Guardrail: 本 story 只交**形态半** —— 数据四态 / 题签文案不进本批(见下方边界裁定)

### 边界裁定(2026-10-08 勘察轮定,不再逐条问用户)

1. **线格 = border 细线,零新图** —— 承 story-020 冻结件(形态件① 不追加贴图 ⇒ 冻结轮不重开);
   格线 = 行底一道淡墨线(art-bible §7.7「字段由线格划出,不画成按钮」)。
2. **空行只交形态半** —— 格线存在 / 零文本 / 视觉等重(声明层)归本 story;
   **数据四态**(#1 阳墨/阴墨/旧读数,触发经 8 的 `S-8.2` → `VitalsDto` 只读)归**数据绑定轮**。
3. **题签恒在矛盾不拍** —— story-011 B5「零文本」vs casebook-39 §113「题签恒在」口径冲突,
   归数据绑定轮一并裁(本 story 不改任何一侧行为)。
4. **明度轴分两级** —— 贴图级 EditMode 判据(本 story 交付)+ 截图级黑白截图夹具(归**桌面走查**,
   集群 `-nographics` 无渲染,不借绿)。
5. **等重断言 = 声明层** —— 两态行同 `min-height` 下限(USS 文本断言);PlayMode 布局探针
   (casebook-39 AC `[A]` 字面「行高/占位像素面积全等」)**归桌面走查**(PlayMode 现无 casebook
   测试基建,新建成本不值;如实登记不冒充)。
6. **焦点环 × 线格级联 = 接线前置裁定项**(2026-10-08 评审登记)—— `.focus-visible` 与
   `.ruled`/`.empty-row` 同写 `border-bottom-*`,同元素双单类时胜负由**未钉死**的 stylesheet
   加载序决定;今日焦点施加者零调用(`FocusVisibleStyle.ClassName` 全库零引用)⇒ **不可观测**,
   非缺陷。口径注已落 `SkeuoFocusVisible.uss` 头注;焦点接线前须裁定(甲:环子元素 / 乙:格线改伪元素)。

## Tasks

### 步① 线格(元件库 USS + 脉案行挂载)

- [x] **AC-021-1** ✅ **2026-10-08**: 格线类 `.ruled` 落 `SkeuoPaper.uss` —— `border-bottom` 走
      `var(--skeuo-shared-border-width)` + `var(--skeuo-paper-rule)`(色 = **提案值**
      `rgba(43,36,22,0.35)`,同 `--skeuo-paper-shadow` 色系深一档,**归数值轮**);
      `CasebookScreen.AddChannel` 行挂 `ruled` ⇒ 脉案五行 + 问诊栏行底各有淡墨线 =
      「满版纸线格」(`casebook-39:104`)的行级兑现
      **断言**: `test_ac021_1_casebook_rows_mount_ruled_class`(源真挂类,去注释匹配)
      —— **交付**:`SkeuoPaper.uss` `.ruled, .empty-row` 块 · `SkeuoThemeVariables.uss` +
      `--skeuo-paper-rule`(+property 枚举补 `rule`)· `CasebookScreen.cs` 挂载行

### 步② 空行形态半(AC-42-B5 转正)

- [x] **AC-021-2** ✅ **2026-10-08**: `.empty-row` 与 `.ruled` **同格线同 `min-height`**
      (同一声明块 —— 拆块即等重失守,由断言锁)下限 = `var(--skeuo-shared-row-height)`
      = **44px**(= AB-3 焦点落点 ≥44×44 的高半,新变量同批入 theme);
      `EmptyRowElement` 零文本(既有 `test_ac42b5_emptyRowNoText` 保持);
      **story-011 B2 转真** ✅ —— `test_ac021_2_ruled_and_empty_row_gridline_equal_weight_in_uss`
      正向断言(格线声明 + 两条 var + 同块等重 + 禁硬编码线宽),story-011 侧 B2 注改指针闭环
      **注**: `SaveSlotItem` 空槽复用 `.empty-row` ⇒ 存档空槽同获格线与等重下限(语义正合,未改其代码)

### 步③ 焦点亮度轴贴图级判据(G3)

- [x] **AC-021-3** ✅ **2026-10-08**: `test_ac021_3_focus_ring_luminance_delta_over_paper` 落
      `focus_visual_and_accessibility_test.cs` —— 源 = 黄铜环不透明像素(496,L=0.2756)vs
      纸面(`border_paper` 不透明像素 **面积 ≥1% 桶** ∪ 两主题底色从 theme 文件**现抽**
      —— 改色判据跟随,防第二真源);sRGB→gamma 线性化后 **Rec.709**;
      **实测 minΔ = 0.2656 ≥ 0.12**【提案,art-bible §4.6,归数值轮】(最近高频纸面 = L 0.0100
      深墨区;危险桶 Δ<0.12 = 空集,python 预演同口径复核);
      **环 vs 环自比对 = 0 < 0.12** 证公式非恒真(退化必红)
      **截图级**(黑白截图夹具)归桌面走查,登记不冒充

## Dependencies

- **Blocked-by**: story-020(冻结件 + 黄铜 2px 出图 + 绑定 —— 明度轴载体)✅(2026-10-08 已闭)·
  story-011(`EmptyRowElement` / `CasebookScreen` 结构)✅ · story-009(焦点可见样式契约)
- **Enables**: M2 Exit Criteria「4 项形态件」条 ① 本体 · art-bible **G1/G3** 两条灰盒不可验判据转可验
- **不阻塞**: 形态件 ②③④;数据四态 / 题签(归数据绑定轮)

## Acceptance Criteria

- [x] 三步 AC 全勾,且测试全绿(过滤 → 变异 → 全量 EditMode + PlayMode 零回归)✅ **2026-10-08**
- [x] 文档同批回刷:story-011(B5 勾 + B2 转正注)· casebook-39 AC `[A]`(◐ 注,布局探针归桌面)·
      `milestones:131` 形态件① ◐ 注(整条保持 `[ ]`)· sprint-04 0/4→**1/4** · EPIC + index ✅ **2026-10-08**
- [x] **零新图**(线格 = border;明度轴 = 读既有图)· 零硬编码线宽(C4/C2 绿)·
      阈值 0.12 常量带【提案·归数值轮】注(不冒充终值)✅ **2026-10-08**

## 评审轮记录(2026-10-08 · 恰一轮,双代理)

| 面 | 代理 | 判定 | 明细 |
|---|---|---|---|
| 代码面 | `lead-programmer` | **PASS** | 0 BLOCKING / 3 RECOMMENDED / 4 nit |
| 测试面 | `qa-lead` | **BLOCKING: 1** | B1 + R1-R4 + n1-n4 |

**修复清单(合并去重 · 全部本轮落)**:
- **B1**(BLOCKING)**贴图分支非空守卫** —— `paperBuckets` 提空后先断 `Is.Not.Empty`
  再自比对(贴图加载失败 ⇒ 空桶跳过 ⇒ 恒绿;现必红)→ `focus_visual_and_accessibility_test`
- **代码#1** 公式锚定三连 —— 白=1.0 / 黑=0 / 128灰=0.21586 先证公式非恒真,再跑业务断言
- **代码#3** `test_ac021_1` 锚定 `AddChannel` **方法体**(去注释全文 grep 的假绿面:挂载挪别处仍绿)
- **代码#2/#4** `SkeuoFocusVisible.uss` 头注 —— #4 明度轴口径更新(均值 vs 高频桶,
  原「贴图×主题色最亮像素」为绑定轮设想)+ #2 焦点环×线格**单类级联冲突**口径注(边界裁定 ⑥)
- **R1** 断言 theme `--skeuo-shared-row-height: 44px`(值是无障碍承诺,非提案 ⇒ 可锚;
  格线**色**为提案值 ⇒ 刻意不锚)
- **R2** `.ruled`/`.empty-row` 选择器全文**恰各出现一次**(防后挂第二覆盖块使等重失守)
- **R3** `empty-row` **挂载侧**断言(与 021-1 对称:`EmptyRowElement` + `SaveSlotItem` 两载体)
- **n1** focus 测试头注补门式读仓**例外声明**(照 `texture_binding_gate_test` 先例)
- **n2** USS 侧同步剥 `//` 行注释(USS 官方仅块注释,防御性对称)
- **n3** 正则容错:选择器顺序反序可匹配 + `:` 后 `\s*`
- **代码#5** 环均值注补 AA 不对称口径说明
- **n4/代码#7** story-011 B5 注补「**渲染级归桌面走查**」半句(与 casebook-39 宽严对齐)
- **R4**(贴图侧变异判别力)→ 由 **B1 非空断言**承接;evidence 登记

## DoD

- [x] 过滤测试绿 + 变异注入恰红 ✅ **2026-10-08** —— 过滤 **230/217/0 红/13 跳**(+3 全绿);
      三处变异(删格线色行 / 挂载类名 ruledX / paper-bg 改铜色)**恰 3 红 = 三条新断言全中**,
      python 反向锚定恢复 + 终态校验通过
- [x] 全量 EditMode + PlayMode 留痕 ✅ **2026-10-08** —— EditMode **3007/2960/0 红/46 跳/1 inc**
      (基线 3004/2957/0 +3 断言)· PlayMode **98/97/0 红/1 跳**(与基线一致)
      ⇒ 日志 `unity/Logs/editmode_full_20261008_form01.xml` · `playmode_full_20261008_form01.xml`
- [x] **双代理评审(恰一轮)→ 修复 → 复跑绿** ✅ **2026-10-08** ——
      评审原件 `production/qa/evidence/review-story-021-form-item-1-2026-10-08.md`
      (代码面 PASS / 测试面 BLOCKING:1;修复清单 14 项全落,见上方评审轮记录);
      **修复后复跑(r2)**:过滤 **230/217/0 红/13 跳**(三条新断言终态全 Passed)·
      EditMode **3007/2960/0 红/46 跳/1 inc** · PlayMode **98/97/0 红/1 跳**(与基线逐项一致,零回归)
      ⇒ 日志 `editmode_skeuo_final.xml` · `editmode_full_20261008_form01_r2.xml` · `playmode_full_20261008_form01_r2.xml`;
      **修复项判别力变异 5 发全中**(逐发独立注入、逐发 python 反向恢复、终态零 MUT 残留):
      ① 挂载挪出 AddChannel 方法体 → 021-1 红(#3)② SaveSlotItem 摘 empty-row → 021-2 红(R3)
      ③ 第二覆盖块 → 021-2 红「实见 2」(R2)④ 44px→20px → 021-2 红(R1)
      ⑤ minFraction→0.999 桶空集 → 021-3 红「BLOCKING B1」+ ⑥ gamma 2.4→2.2 → 021-3 红
      「公式锚定 128 灰」(B1 守卫 + 代码#1)
- [x] 用户指令后提交 ✅ **2026-10-08**(用户流程明示「收口提交推送」)
