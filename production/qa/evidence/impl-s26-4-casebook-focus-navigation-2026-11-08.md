# 实现证据 —— S26-4 `casebook-005` 脉案页布局与无指针焦点导航(自动化半边)

**日期**:2026-11-08
**Story**:`production/epics/casebook/story-005-page-layout-focus-navigation.md`
**权威**:`design/gdd/casebook.md` 规则七(焦点序铁律 2026-10-06 订正面)· 规则八(零按键提示)·
AC-8-23 / AC-8-44 的 39 侧落点
**落点**:`unity/Assets/Editor.Tools.Gates/CasebookAntiFantasyGate.cs`(扩)+
`unity/Assets/Tests/EditMode/CasebookGuards/casebook_uxml_scan_test.cs`(新,12 条)

---

## 0. 结论

| 项 | 结果 |
|---|---|
| 焦点**声明序** = 铁律序(UXML 面) | ✅ |
| 零按键提示(文案面 + `tooltip`/`alt` 旁路 + **代码构建面**) | ✅ |
| 无 `tabIndex` / 无 `SetEnabled(false)` / 空行在序且可聚焦 | ✅ |
| 证伪轮 **M18–M24** | ✅ 各自转红,SHA-256 逐字节还原 |
| `AC-39-11` 手柄全程走查 | ⚪ **`NOT-RUN`**(见走查件;两条工具缺口) |
| `AC-39-12`「旧」态非色单载 | ⚪ **`NOT-RUN`** —— 42 侧今日**零痕渲染对象** |

## 1. 先订正一处口径:焦点序是 2026-10-06 版

卡面与 QA 计划都写「…→ 病名 → 置信度」,那是**旧序**。权威 = GDD 规则七订正面:
`面色 → 语声 → 姿态 → 呼吸 → 触感 → 问诊栏 → 病名`,**置信度不落独立控件**(`OQ-CB-5` 甲)。
焦点环实为 **K = 10**(2026-10-12「页缘入 K」)。三处已就地订正 + 加注,原文不删。

## 2. 一条查实的事实:`Casebook39.uxml` **不是运行面**

`SkeuoRuntimeDriver.cs:173` 建屏 = `new CasebookScreen(root, lib)` → 构造函数直接 `BuildUI()`
(**代码建树**),全类**零 `VisualTreeAsset` / 零 `CloneTree`**。
UXML 是 **spec 面**,与代码面靠 `casebook_render_test.test_cross_uxmlSpec_buildUi_nameSequence_equivalent`
锁「name 序相等」。

⇒ 后果与处置:**只扫 UXML 会漏掉真正跑的那份**。已补
`test_ac8_44_buildUiFace_zeroHintTexts` —— 真构造一次 `CasebookScreen`,扫它 `BuildUI()` 出来的 Label 文案;
M23(往 `BuildUI()` 里插一句「按 E 查看」)证明它会红。

## 3. 新增判据(12 条)

| 组 | 判据 |
|---|---|
| 规则七 · 声明序 | 真 UXML 铁律序零违例 · 换两格 ⇒ 红 · 焦点环缺员 ⇒ 红 · 无 `tabIndex`(UXML + 代码) · 六行皆 `focusable="true"` 且无 `display` 覆写 |
| 规则八 · 文案面 | 真 UXML 零提示词 · 「按 E 查看」⇒ 红 · **代码构建面**零提示词 |
| 规则八 · 旁路 | 真 UXML 无 `tooltip`/`alt` · 带 `tooltip` 的合成件 ⇒ 红 · 空 UXML ⇒ 红不静默 |
| P0 无 disabled | `CasebookScreen` 源码零 `SetEnabled(false)` |

⚠️ **不重复既有覆盖**:焦点环的**运行时**顺序、五行固定成序、「?」位置、UI-8.4 负向声明、
UXML ↔ BuildUI 双真源等价 —— 已由 PlayMode `casebook_render_test.cs`(diagnosis story-006)覆盖。
本件补的是它看不见的三块:声明序、文案面、提示旁路。

## 4. 证伪轮(7 条 · 本件新增)

| # | 变异 | 实测 |
|---|---|---|
| **M18** | 关掉 `tooltip`/`alt` 扫描 | ✅ 恰 1 红 |
| **M19** | 关掉焦点序比对 | ✅ 恰 1 红 |
| **M20** | 清空提示词表 | ✅ 恰 1 红 |
| **M21** | 关掉「焦点环缺员」守卫 | ✅ 恰 1 红 |
| **M22** | 往真 UXML 插一句「按 E 查看」 | ✅ **3 红**(含走 `RunAll` 那条 ⇒ 构建期强制点成立) |
| **M23** | 往 `BuildUI()` 插一句「按 E 查看」 | ✅ 恰 1 红(**代码面**判据有牙齿) |
| **M24** | 让「病名」不可聚焦 | ✅ 证明 PlayMode 导航夹具会红(见 §5) |

还原 = 备份 + `cp` 回 + SHA-256 逐字节:`CasebookAntiFantasyGate.cs` `4bc18986…` ·
`Casebook39.uxml` `ee298948…` · `CasebookScreen.cs` `1fcef101…`。

## 5. 一条把结论**推翻**的夹具(写它是对的)

真包走查看到「手柄横推不改变焦点」,初判为产品几何问题。为此在
`u1_assump6_gamepad_chain_test`(ADR-013 假设 6 的整链驱动,`InputSystemUIInputModule →
PanelEventHandler → panel NavigationMoveEvent → FocusController`)加了一条:

`test_casebook_rightMove_reachesDiseaseNameColumn` —— 真 `CasebookScreen`,从「面色」右推。

读数:`right→channel-disease-name` · 边界 `面色=(0,0,438,0)` / `病名=(462,0,178,0)`(同一 y 带右侧)。

⇒ **产品能跨**。「真包里横推不动」因此归**注入层**,不归布局。若无这条夹具,该结论会被写反。

⚠️ **该夹具不可判竖向**:它的 `PanelSettings` 无主题样式表,面板高度为 0(读数里三个元素的
`height` 全 0)⇒ 竖向几何不成立。故本夹具**只断横向**,竖向留日志不断言(拿它判竖向是越权)。

## 6. 回归三元组

| 套件 | 计数 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7157 / 7120 / 0 / 0 / 37` | `2026-11-08T03:26:57Z` | sprint-26 S26-4 | `e50efe08` + 工作树 |
| PlayMode | `366 / 362 / 0 / 2 / 2` | `2026-11-08T03:21:09Z` | 同上 | 同上 |

**基线**(本 story 开工前,`e50efe08`)`7145 / 7108` · PlayMode `365 / 361`。
**等式**:EditMode `7157 − 7145 = 12` = `casebook_uxml_scan_test.cs` 的 `[Test]` 计数;
PlayMode `366 − 365 = 1` = 新增的 `test_casebook_rightMove_reachesDiseaseNameColumn`。

```bash
grep -c "\[Test\]" unity/Assets/Tests/EditMode/CasebookGuards/casebook_uxml_scan_test.cs   # ⇒ 12
```

## 7. 未做(不得借绿)

1. **`AC-39-11` 手柄全程 = `NOT-RUN`** —— 合册被 `start` 键挡住、横推在真包里不产生导航(两者同属
   `TD-walkthrough-01`);两页对照缺第二例。
2. **`AC-39-12`「旧」态非色单载 = `NOT-RUN`** —— 42 侧今日**零痕渲染**:S26-2 只交整数投影,
   「铅笔对勾」元件**还不存在**,所以没有可扫的对象,也没有可走查的形态。**不是「已通过」。**
3. **主创签核**未做。
