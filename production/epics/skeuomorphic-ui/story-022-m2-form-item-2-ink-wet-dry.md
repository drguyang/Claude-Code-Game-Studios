# Story 022: M2 形态件②(墨乾湿两态 —— 主题色明度轴 · 双态接图 · 洇开语义)

> **Epic**: 拟物 UI 框架
> **Status**: **Complete ✅**(2026-10-08 立 · 用户指令「开始做形态件②」;**同日三步全交付** —— ① 两态主题色明度轴判据 · ② `.ink-wet`/`.ink-dry` 接冻结图 · ③ 洇开覆盖方向判据)
> **Layer**: Foundation
> **Type**: UI / Visual-Feel
> **Estimate**: 1.0 人日
> **Manifest Version**: 2026-10-08
> **Last Updated**: 2026-10-08

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`(规则十三 笔迹与墨龄 · V-4 墨迹湿→干)
**Requirement**: `production/milestones/README.md §三` M2「4 项形态件」**② 墨乾湿两态** ——
「落笔反馈 = 手感本体(§8.11.1 明列『手感不得用灰盒验证』)」(`milestones:83`)
**ADR Governing Implementation**: ADR-013(拟物 UI 框架 · 自建 USS 元件库)
**判据权威**: `design/art/art-bible.md` **G4**(手感本体,:1236)+ **§4.5 墨龄**(:336-338):
「湿墨(边缘洇开)→ 干墨(边缘收干、**色沉**),**只走明度轴、不色相漂移** —— 墨龄因此天然色盲安全。
⚠️ 具体两值 hex 系本稿提案」

**Engine**: Unity 6.3 LTS | **Risk**: LOW(纯 USS + theme + EditMode 测试,零 post-cutoff API)
**Engine Notes**: 不涉引擎新 API;`Texture2D.LoadImage` 为长期稳定 BCL 面(story-021 先例)。

**Control Manifest Rules (this layer)**:
- Required: 湿/干墨色**走主题变量**(C2 门);两张切图接线与**冻结件一致**(C8 门 slice=0 双侧);
  「只走明度轴」机械化为 EditMode 判据
- Forbidden: USS 内联色值;为过判据填占位色;形态件② 追加新贴图(冻结件不重开 —— ink_wet/ink_dry 已在库已冻结)
- Guardrail: 本 story 只交**形态半** —— 切换时机(墨龄数据路径)与落笔交互不进本批(见下方边界裁定)

### 边界裁定(2026-10-08 勘察轮定,不再逐条问用户)

1. **两态 = 状态类,非注册变体** —— `.ink-wet`/`.ink-dry` 与 `.focus-visible` 同构(时间状态,
   随数据切换),**不进 `SkeuoComponentRegistry`**(变体 = 离散形态选择,如 `-faded`;状态 ≠ 变体)。
2. **只交形态半** —— 两态**形态对**(主题色 + 接图 + 判据)归本 story;
   **切换时机**(`墨龄 = 当前 tick − 落笔 tick`,规则十三 + `AC-42-G6④` 纯函数)与落笔交互归
   **数据绑定轮 / 39·37 实现轮**(本 story 不建墨龄求值,不持任何计时器,不发明笔迹系统)。
3. **洇开过渡烘归资产轮** —— V-4「洇开过程必须『烘』」的过渡动画资产不在本批
   (两态静态形态先立;无落笔交互则动态半边无处可挂,如实登记不冒充)。
4. **湿/干色锚定口径分置**(评审修复订正 —— 原写「两值均提案、判据不锚 hex」与
   §4.1/§4.5 分层冲突):**干态 hex = 提案**(`:338` 明文),带【提案·归数值轮】注,
   判据**不锚其值**只锁结构关系;**湿态 hex = art-bible §4.1 权威浓墨(非提案)**,
   判据**可锚**(改权威值须先改 art-bible,同 44px 行高先例)。
5. **色相阈值 = uint8 量化噪声的结构界** —— 「不色相漂移」在 8bit RGB 下受量化噪声污染
   (实测同色相降明度对 ΔH ≈ 3–7°);阈值提案 ≤12°,归数值轮。**方向判据**(干 < 湿)不设阈值。

## Tasks

### 步① 两态主题色 + 明度轴判据(art-bible §4.5 机械化)

- [x] **AC-022-1** ✅ **2026-10-08**: theme ink 层 +`--skeuo-ink-fg-wet`(湿 = art-bible §4.1
      权威浓墨 `#26241f`)+ `--skeuo-ink-fg-dry`(干 = 同色相降明度【提案】`#191714`);
      `test_ac022_1_ink_wet_dry_theme_colors_luminance_axis_only` 从 theme **现抽**(改色判据跟随,
      防第二真源):① ΔH ≤ 12°【提案·归数值轮】(「不色相漂移」;uint8 量化噪声结构界)——
      实测 ΔH = 6.86°;② L(干) < L(湿)(「色沉」方向)—— 实测 0.0087 < 0.0177;
      ③ 锚定 `--skeuo-ink-fg-wet` = art-bible 权威浓墨(湿态非提案,可锚)
      **交付**: `SkeuoThemeVariables.uss` ink 层两变量 + 测试

### 步② 两态类接冻结图(C8 一致)

- [x] **AC-022-2** ✅ **2026-10-08**: `SkeuoInk.uss` +`.ink-wet`(background-image =
      `ink_wet-final.png` guid `4ccb158d…`)+ `.ink-dry`(`ink_dry-final.png` guid `fbae6e46…`),
      各挂对应 `color: var(--skeuo-ink-fg-…)`;**无 `-unity-slice-*` 行**(冻结件 slice = 0,
      两图实测有内容边界但冻结裁定 0 —— 承 `nine-slice-freeze-2026-10-08.md:53/:54`);
      单槽语义显式登记:湿/干态 background-image **有意**顶掉 `.ink` 的 ink_light 渐变底
      (墨渍形态替代渐变底 = 该状态的正确语义,非闪烁缺陷);
      `test_ac022_2_ink_wet_dry_classes_bound_to_frozen_textures` 锁 guid + 无 slice + 颜色 var 引用
      **冻结记录回刷**: `nine-slice-freeze-2026-10-08.md:53/:54` USS 落点「—」→ `SkeuoInk.uss`
      (承 story-020 绑定 brass 时更新 :17 行先例 —— 「引用却未登记」失效模式不重演)

### 步③ 洇开语义判据(贴图级方向锁)

- [x] **AC-022-3** ✅ **2026-10-08**: `test_ac022_3_wet_ink_spread_covers_more_than_dry`
      读**真贴图**(ink_wet/ink_dry,`Texture2D.LoadImage` + DestroyImmediate,照 story-021 基建):
      湿图墨像素(L < 0.2【提案·归数值轮】)覆盖率 **>** 干图(严格大,评审收严 —— 实测余量 0.969 vs 0.646)—— 「边缘洇开」⇒ 覆盖大、
      「边缘收干」⇒ 覆盖小的方向锁(**只锁方向不锚数值** —— 两图是形态差非色差);
      **实测 wet 0.969 > dry 0.646**;python 预演同口径复核

## Dependencies

- **Blocked-by**: story-020(五族切图冻结 —— ink_wet/ink_dry 入库冻结)✅ ·
  story-019(`.ink` 元件接图结构)✅ · story-021(明度轴判据基建先例)✅
- **Enables**: M2 Exit Criteria「4 项形态件」条 ② 本体 · art-bible **G4** / §4.5 两处判据转可验
- **不阻塞**: 形态件 ③④;墨龄数据路径 / 落笔交互(归数据绑定轮)

## Acceptance Criteria

- [x] 三步 AC 全勾,且测试全绿(过滤 → 变异 → 全量 EditMode + PlayMode 零回归)✅ **2026-10-08**
      —— 终态实数:过滤 **233/220/0 红/13 跳**;全量 EditMode **3010/2963/0 红/46 跳/1 inc**;
      PlayMode **98/97/0 红/1 跳**(均 = 基线 +3 新测,零回归);**变异 6 发全中**(4 发初轮 + 2 发修复补验)
- [x] 文档同批回刷:`milestones:131` 形态件② ◐ 注 · sprint-04(形态件 1/4→**2/4**)·
      EPIC + index + 冻结记录 :53/:54 ✅ **2026-10-08**
- [x] **零新图**(ink_wet/ink_dry 已冻结)· 零内联色值(C4/C2 绿)·
      阈值与两 hex 带【提案·归数值轮】注(不冒充终值)✅ **2026-10-08**

## DoD

- [x] 过滤测试绿 + 变异注入恰红 ✅ **2026-10-08**
- [x] 全量 EditMode + PlayMode 留痕 ✅ **2026-10-08**
- [x] **双代理评审(恰一轮)→ 修复 → 复跑绿** ✅ **2026-10-08** ——
      评审原件 `production/qa/evidence/review-story-022-form-item-2-2026-10-08.md`
      (代码面 FIX-THEN-APPROVE 1B+3A · 测试面 FIX-THEN-APPROVE 1B+10A ⇒ **12 项同批修复全落**;
      BLOCKING 双修 = ① 原件落盘(流程)② `ExtractThemeColor` 剥注释+钉唯一(补 MUT-5 恰红实证);
      补 MUT-6 color 恰一断言;复跑三套零回归后 **转 APPROVE**)
- [x] 用户指令后提交 ✅ **2026-10-08**(用户流程明示「收口提交推送」)
