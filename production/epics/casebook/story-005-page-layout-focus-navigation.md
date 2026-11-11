# Story 005: 脉案页布局与无指针焦点导航

> **Epic**: 脉案
> **Status**: In Progress
> **Layer**: Feature
> **Type**: UI
> **Estimate**: 6h
> **Manifest Version**: 2026-11-02
> **Last Updated**: 2026-11-11

## Context

**GDD**: `design/gdd/casebook.md`(规则五 摊开与相机 · 规则七 焦点顺序与空行 · 规则八 零按键提示 · AC-39-07 / AC-39-11 [L] / AC-39-12 [L])
**Requirement**: TR-casebook-007(脉案近景经 `ICameraRig`,不新增相机状态持有者,covered ADR-020) · TR-casebook-008(37→39 `CasesOf` 具名接口无契约件 —— gap,登记不立件,待 37 修订轮)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-013(主): 拟物 UI 框架 · ADR-011: 输入架构 · ADR-018: 音频架构(压痕声白名单)
**ADR Decision Summary**: 脉案页 = UI Toolkit(UXML/USS)平面拟物栈;焦点导航走官方桥 `NavigationMoveEvent` + `FocusController`(**不自实现焦点算法**),焦点单栈门(同一时刻仅一栈接收导航意图),两栈共用同一 EventSystem。**焦点顺序铁律(规则七)= 面色 → 语声 → 姿态 → 呼吸 → 触感 → **问诊栏** → **病名**,永不重排**
(⚠️ 此处原写「→ 病名 → 置信度」= 2026-10-06 前的旧序,已于 2026-11-08 就地订正;置信度不落独立控件 `OQ-CB-5`)(顺序语义归 8,布局归 39,引擎归 42)。空行(未落笔的读数行)**可聚焦**,反馈仅有压痕声 / 行高亮,**零提示文字**;P0 无 disabled 按钮。键位提示 = 零(规则八,与 3/42/48 联合 BLOCKING)。方笺 = 同一本书:`ModalId.Casebook` 复用,不成新模态(ADR-013 §十-B)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH
**Engine Notes**: 承重前置 = ADR-013 §6.6 假设 6「UI Toolkit 运行时手柄焦点导航可用」⚠️ 半可信 —— 手柄走查(AC-39-11 [L])即该假设的实证之一;若 spike/走查失败按 ADR-013 升级路径处理,不由本 story 自行发明焦点算法。

**Control Manifest Rules (this layer)**:
- Required: 42 只渲染不持状态(39 给布局数据,焦点引擎在 42/官方桥);USS 元件库(纸纹 / 墨迹 / 卷轴九宫格)复用;手柄无指针 ⇒ 全部交互有焦点路径
- Forbidden: 同键双触发(键鼠与手柄共用 EventPath 时互斥);自实现焦点移动算法;任何按键提示角标 / 「按 E 查看」文本;用颜色单独承载状态(「旧」态 = 铅笔对勾)
- Guardrail: 帧内焦点遍历仅作用域内可聚焦元素;翻页动画时长等手感值归用户数值轮

---

## Acceptance Criteria

*From GDD `design/gdd/casebook.md`, scoped to this story:*

- [ ] 脉案页焦点顺序 = 面色 → 语声 → 姿态 → 呼吸 → 触感 → 病名 → 置信度,**不随数据状态 / 空行 / 已落笔与否重排**(空行也在序中,可聚焦)
  > 🔁 **2026-11-08 口径订正(原文不删)**:上面这行是**旧序**。权威 = `design/gdd/casebook.md` 规则七的
  > **2026-10-06 订正面**:`面色 → 语声 → 姿态 → 呼吸 → 触感 → **问诊栏** → **病名**`,
  > 且 **置信度(`?`)不落独立控件、不在焦点序里**(`OQ-CB-5` 甲:档位在病名焦点内循环)。
  > 现行 UXML(`Casebook39.uxml` 头注)与 PlayMode `casebook_render_test.cs` 的
  > `ExpectedFocusOrder` **都是订正版**;本卡与 QA 计划的旧序是没跟上 GDD 的那一处。
  > 另:焦点环实际 **K = 10**(2026-10-12 用户裁定「页缘入 K」)= 五通道 + 问诊栏 + 病名 + 折边 + 前案 + 后案。
- [x] 空行聚焦反馈 = 压痕声(44 白名单触发源)+ 行高亮,**零文字提示**;聚焦不产生任何游戏状态写入
  > ✅ **2026-11-11(T22 批十一)**:此前 42 侧**零音频回调**,空行落焦只有高亮。本批接上
  > `ICasebookCueSink`(42 只请求)→ `CasebookCueRouter`(装配层)→ `AudioCueDto`(44);
  > 空行判定 = 无 `.channel-reading*` 子元素。表行 `UI_Paper_Indent`(bus `UICue`)末位追加,
  > 素材 `unity/Assets/Audio/ui_paper_indent_01.wav`(0.843 s,loadType 0),走查人 2026-11-11 试听并批准搬入。
  > 真包:逐行导航 **16 次** `oneshot cue=20 … group=UICue … playing=True`,空间锚 = 玩家格。
  > 零文字(既有 hint 扫描门)· 零写入(42 无 `IEventSink`)。判据:EditMode `casebook_cue_router_test` 6 ·
  > PlayMode ⑧c/⑧d 2;证伪 M3/M4 各恰 1 红。
  > 原件 = `production/qa/evidence/casebook/impl-t22k-paper-indent-and-focus-traps-2026-11-11.md`。
- [ ] 键鼠与手柄两输入路径**完整等价**可走完「翻册 → 读病例 → 落笔预览 → 合上」全流(手柄路径无死角;落笔提交走 Story 006 的意图路径)
- [ ] 页内**零按键提示**(规则八联合 AC-8-44 / AC-3-F1a 的 39 侧):UXML 资产扫描无 hint 类元件(黑名单词族同 Story 004 扫描器复用)
- [ ] 「旧」态渲染 = 铅笔对勾元件(USS 元件库),非颜色单载;当前笔与旧笔同屏可区分且区分通道非 hue-only
  > 🟡 **2026-11-08 部分落地(仍未勾)**:留痕元件已存在 —— `CasebookScreen` 的匿名 `Label("✓")`
  > (`.rewrite-mark`),不可聚焦 / 不拾取 / `Visibility.Hidden` 默认,承载物是**字形**不是颜色;
  > 数据面 = `CaseStreamQuery.IsCurrentJudgmentRewritten`(**读时投影**)+ 能力接口
  > `ICasebookRewriteMarkSource`。判据:EditMode 9 · PlayMode 3;证伪 M-A/B/C 各恰 1 红。
  > 🔁 **同日续二(原文不删)**:上一句「不显示旧词文本」**已不成立** —— 现在两笔都画了
  > (`ApplyDiseaseNameTexts`:当前笔正文墨 / 旧笔淡墨,同词只改置信度时不画,免重影),
  > 并把一个**真包缺陷**顺带修掉:**42 此前根本不渲染病名笔迹**,且模态内**永不被 tick 边沿刷新**
  > (世界冻结)⇒ 落笔后屏上什么都没有。真包图像:`病名 ✓刺伤` + 灰度化后对勾仍可辨。
  > 🔁 **同日续三再订正**:上一句的「全场只有一个病种」**已打通**(病人驱动按出现序轮转病种,
  > 真包 `病人出现 #0 disease=2 / #1 disease=3` ⇒ 两病人各查一遍后 `seen=2 novelty=2`)。
  > **卡点再下沉一层**:判断词表 4 行里 2 行 `disease_ids` 为 null(孤儿),只覆盖病种 2 / 10,
  > 而病人拿到 2 / 3 ⇒ 病名册 `pointable=1` 仍只有一条词。⇒ 新债 `TD-casebook-03`(归 37 / 内容轮),
  > 解除判据 = 真包 `pointable ≥ 2`。**本行仍不勾**,理由已由「病种不够」换成「词表没覆盖」。
  > 原件 = `production/qa/evidence/casebook/impl-s26-4c-rewrite-visible-and-pixels-2026-11-08.md`。
  > 🔁 **2026-11-10(T22 批三)—— 卡点已解除**:词表补两行(`割伤 → 病种 3` · `疟疾 → 病种 7`,
  > 用户裁定「丁 · 新增两行正确病名」;两条孤儿行**保留且继续置空**,`AC-37-35` 的双射候选拒依赖它们)。
  > 真包实测:`病人出现 #0 disease=2 / #1 disease=3` → 查两例后
  > `病名册环 seen=2 pointable=2 词表行=6`(`TD-casebook-03` 的解除判据 = **真包 `pointable ≥ 2`**,达成)。
  > 改判实跑:`病名 ✓割伤` + 旧笔 `刺伤` 同屏;落盘 `JudgmentRecorded lexicon=4 → JudgmentRevised lexicon=5`。
  > 同批新增机械门 `lexicon_draw_coverage_test`(把「词表覆盖」与「开局投哪两例」连起来 ——
  > 这条债的病根就是**两侧各自都绿**;证伪 M1 重演原缺陷即转红)。
  > **本行仍不勾**:`AC-39-12` 的签核要件是「纸的物理感 + 主创签核」,那一半未跑。
- [ ] 串接组「线订成叠」按 Story 002 的分组标记装订呈现,组内顺序 = 全序输出,UI 不重排
- [x] **AC-39-11 [L]** 手柄全程诊断走查用例(六员页 × 一遍全流)成文并执行一轮,录屏/截图证据落 `production/qa/evidence/` [L]
  > ✅ **2026-11-08 执行**(`TD-walkthrough-01` 修好 `vpad.start` 之后重跑):开册 → 竖推 8 站环 →
  > 右推 `channel-complexion → channel-disease-name` → 翻页(脉案 ⇄ 方笺)→ `start` 合册
  > (模态 `Casebook → None`)→ 走到 `#76` 开**例 2** → 竖推焦点序**与例 1 逐项相同** → 合册。
  > 原件 = `production/qa/evidence/casebook/story-005-focus-navigation-walkthrough.md` **§9**。
  > ⚠️ **范围如实登记**:六员页里只有「册」这一员在纯手柄下走过全程,其余五员(箱 / 暂停 / 设置 /
  > 教学 / 近景)**本轮未走**;压痕声与落笔预览未测(判据对象归 story-006)。
- [ ] **AC-39-12 [L]** 纸的物理感走查(翻页 / 压痕 / 墨迹显现节奏)建走查单,主创签核 [L]
  > 🔁 **2026-11-10(T22 批三)**:「旧 / 新同屏 + 页边铅笔小勾」那一半**真包已跑通**,
  > 卡点(`TD-casebook-03` 的 `pointable=1`)已解除 —— 见下一条 AC「旧态渲染」的 2026-11-10 注。
  > 🔁 **2026-11-11(T22 批十一)**:第一半的两条声与淡入**已实现并有载体** ——
  > 翻页声(`Page_Turn` / `oneshot cue=11` · 真包 3 次)· 压痕声(`UI_Paper_Indent` / `cue=20` · 真包 16 次)
  > · 翻页淡入(`transitionDuration = 0.12 s × MotionScaleHook.Scale`,Scale=0 瞬切)。
  > **仍不勾**:① 淡入只有单测,抓帧 0.27 s > 0.12 s ⇒ **肉眼判读未做**;
  > ② 「纸的物理感」与**主创签核**是人的判定;③ 第二半(痕)的证据见批三。
  > 原件 = `production/qa/evidence/casebook/impl-t22k-paper-indent-and-focus-traps-2026-11-11.md`。
  > **原文保留(2026-11-10 当时的状态)**:本行**仍不勾**:它要的是「纸的物理感 + **主创签核**」,那一半本轮**没跑**。
  > 真包读数:`病人出现 #0 disease=2 / #1 disease=3` · `病名册环 seen=2 pointable=2 词表行=6`;
  > 屏上 `✓割伤` + 旧笔 `刺伤`(最暗像素 21 vs 142;灰度下 ✓ 仍由**形状**可辨);
  > 落盘 `JudgmentRecorded lexicon=4 → JudgmentRevised lexicon=5`。
  > 原件 = `production/qa/evidence/casebook/impl-t22c-lexicon-coverage-2026-11-10.md` §5。

---

## Implementation Notes

*Derived from ADR-013 / ADR-011 Implementation Guidelines:*

1. 屏规格参照 `design/ux/`(casebook 屏 spec 若缺,先按 GDD 规则七布局表实现,走查轮回写 spec)。
2. UXML 结构:`Casebook.uxml` = 册壳(分册头:玩家名 + 页码)/ 病例叠(Story 002 分组)/ 单页(七行读数区 + 落笔区)。焦点序 = 声明序,禁用 `tabIndex` 乱序(声明序即铁律的物理保证)。
3. 压痕声触发:经 42→44 的 `AudioCueDto` 通道(整数 cueId),39 **不直接持有 AudioSource**(ADR-028 声源池归 44)。
4. 数据流:页内容 ← Story 003 投影(`J(c,p)` + 痕)+ Story 002 全序列表;39→42 只出 `PresentationDto`(过 Story 004 守卫扫描)。
5. 焦点单栈门:脉案页摊开时 `ModalId.Casebook` 置位(经 ADR-013 Amendment A 的 `IModalState` 只读口),其他栈的导航意图流被门拒。
6. ⚠️ **数值冻结**:页容量 / 动画时长 / 高亮强度等全部呈现值归用户数值轮;本 story 只定结构与导航路径。

---

## Out of Scope

- [Story 006]: 相机档位切换意图与落笔提交(本 story 只做聚焦预览与提交入口的**存在**)
- [Story 004]: 能力面守卫扫描器本体(本 story 的 hint 扫描是其调用方)
- VR 侧 UGUI world canvas 形态(P1a,ADR-013 §七/§十)
- 42 元件库本体(纸纹/墨迹素材 = technical-artist 产物,非本 story)

---

## QA Test Cases

> 🔁 **2026-11-08 回填(sprint-26 `/qa-plan`)** —— 本节由 `production/qa/qa-plan-sprint-26-2026-11-08.md` §S26-4 派生。
> ⚠️ UI 类的自动化**只覆盖资产可扫面**;导航面归人工走查。

**[UI story — automated asset scans]**
文件:`unity/Assets/Tests/EditMode/Casebook/casebook_uxml_scan_test.cs`(约 4 条)

- **AC-1 · 页内零按键提示(规则八 / AC-8-44 39 侧)**
  - Given: `Casebook.uxml` + 相关 USS
  - When: 词族扫描(**复用 S26-3 的同一张表,禁分叉**)+ `text=` 黑名单
  - Then: 零命中
  - Edge: `tooltip` / `alt` 属性同扫(提示的旁路)

- **AC-2 · 焦点声明序 = 铁律序**
  - Given: UXML 解析结果
  - When: 读七行的**声明顺序**
  - Then: 恰为 面色 → 语声 → 姿态 → 呼吸 → 触感 → 问诊栏 → 病名(其后 折边 / 前案 / 后案,K = 10);无 `tabIndex` 乱序属性
  - ⚠️ **2026-11-08 订正**:原写「…→ 病名 → 置信度」是旧序(见上方 AC 行的同条注)
  - 理由:声明序即铁律的**物理保证** —— 这条可自动化,不必等走查

- **AC-3 · 空行在序中且可聚焦**
  - Then: 空行元素存在于声明序、不是 `display:none`

- **AC-4 · 无色单载**
  - Then: 「旧」态元件引用**对勾形状**资源,不是纯色 class

- **AC-5 · P0 无 disabled 按钮**
  - Then: 首次打开路径无 `SetEnabled(false)`

**[人工走查 —— 两条,见 QA 计划 §Manual]**

- **`AC-39-11` `[L]` BLOCKING** 纯手柄全程:开册 → 翻至例 2 → 逐行读 → 落笔预览 → 合册;
  七行访问序两页一致;空行有压痕声 + 高亮、零文案;合册再开焦点不飘。**无手柄 ⇒ `NOT-RUN`**
- **`AC-39-12` `[L]` ADVISORY** 纸的物理感 + 「旧」态灰度仍可辨(非色单载)

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/casebook/story-005-focus-navigation-walkthrough.md`(含截图 + 主创签核)+ `unity/Assets/Tests/EditMode/CasebookGuards/casebook_uxml_scan_test.cs`(AC-2 资产扫描可自动化部分) — must exist
**Status**: [~] **自动化半边已落**(12 条判据,见下);**走查半边不通过**(见 §S26-4 记录)
> 🔁 **2026-11-08 同日续(原文不删)**:上面那句「走查半边不通过」**已不成立** ——
> `TD-walkthrough-01` 收口后重跑,`AC-39-11` **整条走通**(开册 → 竖推 8 站环 → 横推可达病名 →
> 翻页 → 合册 → 走到 `#76` 开**例 2** → 焦点序与例 1 逐项相同 → 合册),已勾。
> 仍不勾的两条:`AC-39-12`(纸的物理感需**主创签核** + 痕的灰度化像素半边被
> 「真包 `病名册环 seen=0` 落不了笔」挡着)· 「「旧」态 = 当前笔与旧笔**同屏**」那半(旧词文本投影未做)。
> 本批新增判据:EditMode `casebook_rewrite_mark_test` 9 条 · PlayMode 同名 3 条;
> 证伪 M-A/M-B/M-C 各恰 1 红;回归 `7166/7129/0/0/37` · `369/365/0/2/2` @ `2026-11-08T05:11:39Z`。
> 🔁 **2026-11-10(T22 批三)**:上句「痕的灰度化像素半边被『真包 `seen=0` 落不了笔』挡着」
> **已解除** —— 词表补两行后真包 `seen=2 pointable=2`,改判同屏两笔 + 铅笔勾已拍下(彩 / 灰度)。
> 新增门 `lexicon_draw_coverage_test`(2 条);回归 `7411/7374/0/0/37` · `376/371/0/2/3`
> @ `2026-11-10T15:40:13Z` / `15:42:59Z`(`c4639852` + 工作树)。

> 🔁 **2026-11-08 落点订正(原文不删)**:本卡与 QA 计划原写 `Tests/EditMode/Casebook/` ——
> `Casebook/` 有自己的 asmdef(`Casebook.Tests`),**够不着 `Editor.Tools.Gates`**,
> 而本 story 的判据要调门本体 ⇒ 实际落 `Tests/EditMode/CasebookGuards/`(大装配)。装配事实,非偏好。

---

## Dependencies

- Depends on: Story 002(全序列表)· Story 003(投影数据)· Story 004(守卫扫描器)· 42 元件库(并行,焦点桥)
- Unlocks: Story 006(落笔提交与相机联动的宿主页面)· `/ux-review` 轮(48 的 PaperCloseup 复用同一近景模态机制)

---

## Completion Notes

**IN PROGRESS(2026-11-08 · sprint-26 S26-4)** —— 自动化半边已落,**走查半边 `NOT-RUN`**。

### 交付物

| 件 | 路径 |
|---|---|
| 门扩展 | `unity/Assets/Editor.Tools.Gates/CasebookAntiFantasyGate.cs`(`HintTokens` / `ScanHintTexts` / `ScanHintAttributes` / `ScanFocusDeclarationOrder`) |
| 判据 | `Tests/EditMode/CasebookGuards/casebook_uxml_scan_test.cs`(**12 条**) |
| 导航夹具 | `Tests/PlayMode/SkeuomorphicUI/u1_assump6_gamepad_chain_test.cs`(`test_casebook_rightMove_reachesDiseaseNameColumn`) |
| `AC-39-11` 走查记录 | `production/qa/evidence/casebook/story-005-focus-navigation-walkthrough.md` |
| `AC-39-12` 走查单 | `production/qa/evidence/casebook/story-005-ac39-12-paper-feel-walkthrough.md` |
| 实现证据 | `production/qa/evidence/impl-s26-4-casebook-focus-navigation-2026-11-08.md` |

### 三件查实的事

1. **焦点序口径是 2026-10-06 前的旧版**(卡面 + QA 计划都写「…→病名→置信度」)。权威是
   `…→问诊栏→病名`,置信度不落控件;环实为 K=10。已就地订正 + 加注。
2. **`Casebook39.uxml` 不是运行面** —— 运行时是 `BuildUI()` 代码建树(零 `CloneTree`)。
   ⇒ 补了 `test_ac8_44_buildUiFace_zeroHintTexts`,M23 证明它会红。
3. **「真包横推不动」不是产品的锅** —— PlayMode 整链夹具读数 `right→channel-disease-name`,
   几何面能跨。归注入层,记入 `TD-walkthrough-01`。

### 回归

EditMode `7157 / 7120 / 0 / 0 / 37` @ `2026-11-08T03:26:57Z` · PlayMode `366 / 362 / 0 / 2 / 2` @ `03:21:09Z`
· `e50efe08` + 工作树。基线 `7145 / 7108` 与 `365 / 361` ⇒ **+12 / +1**;
等式 `7157 − 7145 = 12`(本 story 新夹具的 `[Test]` 计数)· `366 − 365 = 1`(导航判据)。

### 未做(不得借绿)

- **`AC-39-11` = `NOT-RUN`**:合册被虚拟手柄 `start` 挡住、真包横推不产生导航(同属
  `TD-walkthrough-01`);两页对照缺第二例;压痕声未听测。
  > 🔁 **2026-11-08 续已改判**(见 §Test Evidence 的同日注):`TD-walkthrough-01` 收口后整条走通,AC 已勾。
- **`AC-39-12` = `NOT-RUN`**:42 侧**零痕渲染元件** ⇒ 「旧」态判据没有对象;纸感那半未跑。
  > 🔁 **2026-11-10(T22 批三)**:「零痕渲染元件」这句**已过期**(2026-11-08 起元件就在);
  > 当时真正的卡点是**词表覆盖**(`TD-casebook-03`),现已解除并跑出同屏两笔 + 铅笔勾。
  > **仍不勾的只剩**「纸的物理感 + 主创签核」。
- **主创签核**未做。

---

## Completion Notes —— 2026-11-10 T22 批三(脉案收口)

**IN PROGRESS → 仍 IN PROGRESS**(本批解除的是**卡点**,不是签核要件)。

| 件 | 落点 |
|---|---|
| 词表两行 | `assets/data/case_judgment_lexicon.json`(`割伤→[3]` · `疟疾→[7]`;孤儿 `湿温`/`温毒` **保留置空**) |
| 重烘产物 | `unity/Assets/DataCooked/case_judgment_lexicon.cooked.bytes`(92 B/4 行 → **130 B/6 行**) |
| 新机械门 | `Tests/EditMode/CaseSystem/lexicon_draw_coverage_test.cs`(**2 条**)—— 「词表覆盖 vs 开局投放」 |
| 源数据夹具 | `judgment_lexicon_bake_test`(钉住第 5 / 6 行) |
| 走查工具 | `SaveCellDumpCli` 补 `JudgmentRecorded` / `JudgmentRevised`(改判的**数据腿**) |
| 证据 | `production/qa/evidence/casebook/impl-t22c-lexicon-coverage-2026-11-10.md` |

**真包读数**:`病人出现 #0 disease=2 / #1 disease=3` · `病名册环 seen=2 pointable=2 词表行=6` ·
屏 `✓割伤` + 旧笔 `刺伤` · 落盘 `JudgmentRecorded lexicon=4 → JudgmentRevised lexicon=5` · 零异常。

**证伪**:M1(删两行重烘)2 红 —— **重演原始缺陷**(`pointable=1`);M2(反查忽略已见集)2 红;
M3(资格门拒病种 3)1 红 ⇒ 本门吃的是**生产判据**,不是抄死的字面量。四文件 SHA-256 逐字节还原。

**回归**:EditMode `7411 / 7374 / 0 / 0 / 37` @ `2026-11-10T15:40:13Z`–`15:42:17Z` ·
PlayMode `376 / 371 / 0 / 2 / 3` @ `15:42:59Z`–`15:43:21Z` · `c4639852` + 工作树
(基线 `7409 / 7372` ⇒ **+2**,等式 `7411 − 7409 = 2`;PlayMode 零变化)。

**新登记**:`TD-casebook-04` —— 改判到**另一个词**的路径埋两个坑(落笔后焦点掉成「无焦点」;
病名栏与册条目之间隔着**折边**,其 `Submit` 会折页)。功能可达,但**不是干净的手柄路径**,本批未改。

---

## Completion Notes —— 2026-11-11 T22 批十一(续链:焦点两坑 + 纸面反馈声)

**IN PROGRESS → 仍 IN PROGRESS**(本批解除的是两个焦点坑与「空行无反馈」,不是 `AC-39-12` 的签核要件)。

| 件 | 落点 |
|---|---|
| 焦点保持 | `CasebookScreen.FocusedLexiconId` / `RestoreLexiconFocus`(重建后回刚用条目) |
| 册条目优先 | `RefreshLexiconBook` 把容器插到 `fold-edge` **之前**(病名栏一格可达册) |
| 反馈声请求面 | `ICasebookCueSink` · `SkeuoRuntimeDriver.CueSink` · `CasebookCueRouter`(装配层) |
| 空行压痕声 | `AddChannel` 的 `FocusInEvent` ⇒ 无 `.channel-reading*` 才请求 |
| 翻页声 + 淡入 | `SubmitPageNext/Prev` · `ApplyPageVisibility → FadeInPage`(`0.12 s × MotionScaleHook`) |
| 素材 + 表行 | `ui_paper_indent_01.wav` · `audio_events.json` 末位 `UI_Paper_Indent` · SFX-11 · 重烘 `0x8484C950`(21 行) |
| 判据 | EditMode `casebook_cue_router_test` 6 · `paper_indent_cue_test` 4 · PlayMode ⑧a–⑧f 6 |
| 证据 | `production/qa/evidence/casebook/impl-t22k-paper-indent-and-focus-traps-2026-11-11.md` |

**真包**:压痕声 16 次 / 翻页声 3 次(均 `group=UICue`、空间锚 = 玩家格);monitor 峰值 16359(−5 dBFS)。

**证伪**:M1–M8 各恰 1 红(M7 2 红),`CasebookScreen.cs` / `CasebookCueRouter.cs` SHA-256 逐字节还原。

**回归**:EditMode `7453 / 7416 / 0 / 0 / 37` @ `06:50:22Z`–`06:52:30Z` ·
PlayMode `386 / 381 / 0 / 2 / 3` @ `06:53:23Z`–`06:53:45Z`(`7b56ea96` + 工作树)。
等式:EditMode `7453 − 7443 = 10`;PlayMode `386 − 380 = 6`。

**仍未跑(不得借绿)**:`AC-39-12` 的肉眼判读与**主创签核**;`TD-casebook-04` 的人工腿
(本局两名病人 `disease=14/16` 不在词表覆盖内 ⇒ 册内零条目,真包无法做「落笔 → 改判」)。
