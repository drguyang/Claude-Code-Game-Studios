# Story 005: 脉案页布局与无指针焦点导航

> **Epic**: 脉案
> **Status**: In Progress
> **Layer**: Feature
> **Type**: UI
> **Estimate**: 6h
> **Manifest Version**: 2026-11-02
> **Last Updated**: 2026-11-08

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
- [ ] 空行聚焦反馈 = 压痕声(44 白名单触发源)+ 行高亮,**零文字提示**;聚焦不产生任何游戏状态写入
- [ ] 键鼠与手柄两输入路径**完整等价**可走完「翻册 → 读病例 → 落笔预览 → 合上」全流(手柄路径无死角;落笔提交走 Story 006 的意图路径)
- [ ] 页内**零按键提示**(规则八联合 AC-8-44 / AC-3-F1a 的 39 侧):UXML 资产扫描无 hint 类元件(黑名单词族同 Story 004 扫描器复用)
- [ ] 「旧」态渲染 = 铅笔对勾元件(USS 元件库),非颜色单载;当前笔与旧笔同屏可区分且区分通道非 hue-only
- [ ] 串接组「线订成叠」按 Story 002 的分组标记装订呈现,组内顺序 = 全序输出,UI 不重排
- [ ] **AC-39-11 [L]** 手柄全程诊断走查用例(六员页 × 一遍全流)成文并执行一轮,录屏/截图证据落 `production/qa/evidence/` [L]
- [ ] **AC-39-12 [L]** 纸的物理感走查(翻页 / 压痕 / 墨迹显现节奏)建走查单,主创签核 [L]

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
- **`AC-39-12` = `NOT-RUN`**:42 侧**零痕渲染元件** ⇒ 「旧」态判据没有对象;纸感那半未跑。
- **主创签核**未做。
