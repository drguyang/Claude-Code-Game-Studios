# T20 第二批 —— 结案页(独立屏 + 纸面墨勾)证据原件

**日期**:2026-11-10
**批次**:T20(用户裁定:丙 = 独立结案页 + 纸面墨勾;入口 = C 改为开屏;拒绝 = 一行淡墨字)
**commit**:`998692e2` + 工作树(未提交)
**引擎**:Unity 6.3 LTS(6000.3.24f1)batch EditMode / PlayMode

---

## 1. 本批补的断点

`AC-37-04`(BLOCKING)要求「玩家勾选『已处置』」这个**自述动作存在且可判**。
P0 里它一直没有载体:39 的 §7 动作表 / §8 Events Fired 从未收下结案动作,
42 的 `ICasebookCommandSink` 六个命令零结案命令 ⇒ 该动作登记为 `OQ-CB-7`(2026-10-20)。
T19 把 `C` 直接接到写者(`isChecked: true`),那是**隐式自述** —— 与 GDD 的「勾选」不等。

## 2. 形态裁定(`OQ-CB-7` → 丙)

| 争点 | 裁定 |
|---|---|
| 「勾选」vs §5.3 禁「复选框」 | 取**纸面墨勾** —— 墨侧允许手法(与「折边」「铅笔小勾」同族),不新增控件族;屏上**没有复选框元素**,只有字形 |
| 独立屏 vs 脉案内一行 | **独立屏**:脉案五通道焦点序已排满(K=6),且结案是不可撤销动作,独立页给得起确认距离 |
| 入口 | `C` / `leftShoulder` **改为开结案页**(T19 的「一键即结」退役为无屏路径的缺省行为) |
| 拒绝反馈 | **一行淡墨字**(用户裁定) |

## 3. 落地

| 层 | 文件 |
|---|---|
| 闭集 | `ModalId.CaseClose = 10`(第十员,接在末尾) |
| 42 表 | `SkeuoRuntimeDriver.ModalScreenNames["CaseClose"]` + `ScreenFactories["CaseClose"]` |
| 屏 | `Screens/CaseCloseScreen.cs`(墨勾行 + 结案行 + 淡墨行;公开 `ActivateInkRow`/`ActivateSubmitRow`) |
| 装配 | `Gameplay.Boot/CaseClosePresenter.cs`(开屏 / 提交 / 合屏;`ReasonOf` 把结果码翻成文案) |
| 接线 | `BootRoot`:创建设置器;`routeClosePress` 改为 `TryOpen` |
| 驱动 | `CaseCloseDriver.TryClose(patient, bool isChecked = true)` —— 缺省仍 `true`,不打断既有夹具 |
| 文档 | `docs/architecture/adr-013…md` §十-F Amendment E · `design/gdd/skeuomorphic-ui.md`(闭集九→十 + `AC-42-F1` ⑩)· **新 spec** `design/ux/case-close-42.md` |

## 4. 判据

`unity/Assets/Tests/EditMode/CaseSystem/case_close_screen_test.cs` —— 7 条:

| # | 测试 | 属性 |
|---|---|---|
| 1 | `test_open_withoutOpenCase_doesNotOpen` | 无开着的病例 ⇒ **不开空页** |
| 2 | `test_open_withOpenCase_opensAndBindsScreen` | 开屏 + 绑定 + `Patient` 就位 |
| 3 | `test_inkRow_submit_flipsInkState` | 墨勾可落可擦(显式自述存在) |
| 4 | `test_submit_withoutInk_rejectsAndKeepsPageOpen` | 未勾 ⇒ 写者拒 + 淡墨行「未勾「已处置」」+ 页不关 + 零写入 |
| 5 | `test_submit_withInk_andTreatment_closesAndDismisses` | 勾 + 有处置 ⇒ `CaseClosed` 恰一条 + 合页 |
| 6 | `test_cancel_dismissesWithoutWriting` | 取消 ⇒ 合页、零写入、病例仍开 |
| 7 | `test_open_whenAnotherModalIsOpen_stacks_thenCancelRestoresPrevious` | 叠栈语义:取消回到被压住的模态 |

## 5. 回归(三元组)

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7392 / 7355 / 0 / 0 / 37` | `2026-11-10 09:11:26Z`–`09:13:31Z` | T20 第二批 | `998692e2` + 工作树 |
| PlayMode | `376 / 371 / 0 / 2 / 3` | `2026-11-10 09:16:44Z`–`09:17:05Z` | T20 第二批 | `998692e2` + 工作树 |

基线(T20 第一批):EditMode `7385 / 7348` · PlayMode `376 / 371`。
**等式**:`7392 − 7385 = 7`(新夹具 7 条);PlayMode 零变化(本批无新 PlayMode 件)。

## 6. 闭集同步面 = **四处**,不是三处(本批第二次踩同一坑)

`dentch_input_test.cs` 的注释逐字写着「闭集门共**三处**,本轮漏改过本处」——
本批我**又漏了本处**,由 PlayMode 全量实测 `failed=1` 抓出,补后转绿。

⇒ 现行口径修正为:**闭集基数断言共四处** —— `dto_boundary_and_modal_test` ·
`modal_gate_test` · `dentch_input_test`(两处 `Enum.GetNames` 断言)·
`modal_toggle_closure_test`(枚举驱动,无基数,不需改)。
已在本批把「四处」写进 `dentch_input_test` 的就地注释,避免第三次。

## 7. 证伪轮

窗口:`2026-11-10 09:19Z`–`09:31Z`;窗口内不编辑被变异文件;还原判据 = SHA-256(两件「成功」)。

| 变异 | 改法 | 结果 |
|---|---|---|
| **M1** | 提交时忽略墨勾态(恒 `isChecked: true`) | `6/7` 通过,**1 红**(未勾被拒那条) |
| **M2** | 被拒时也合页 | `6/7` 通过,**1 红**(页须留着说原因) |
| **M3** | 墨勾行不可擦除(恒 `true`) | `6/7` 通过,**1 红**(勾→擦除) |
| **M4** | 开屏前不查「有没有开着的病例」 | `6/7` 通过,**1 红**(不开空页) |

四条各自只红一条 —— 偏薄,但每条都精确对位(不是共享 token 借绿):四个变异打在四个不同判据上。

## 8. 未覆盖(禁借绿)

1. **UI 事件绑定本身未验**:EditMode 下 42 的 UI 根无 PanelSettings(`panel == null`)⇒
   `SendEvent` **不派发**(实测:直接向行发 `NavigationSubmitEvent`,回调零次触发)。
   ⇒ 夹具改走屏上的公开入口(`ActivateInkRow` / `ActivateSubmitRow`,与回调同一实现体);
   **`RegisterCallback` 那两行 = 真包走查**,目前**无走查件**。
2. **`AC-42-F1` ⑩ 记 `NOT-RUN`**:`case-close-42.md` 是**本轮新写、未经 `/ux-review`** 的 spec,
   按该 AC 的纪律阻塞签核(与 ⑨ 炮制屏同一处境)。
3. **`AC-CC-01…08` 全 `NOT-RUN`**:01/02/04/05/06 有逻辑腿,03/08 需要真事件 / 真手柄。
4. **视觉未看**:墨勾字形、淡墨行位置、页框都在逻辑层成立,包内观感未截图(承「真包截图再调」纪律)。
5. **`C` 键语义变更未在真包验证**:T19 起它是「一键即结」,本批改为「开屏」;
   行为差异只有真包能体会(EditMode 只测到 presenter)。
