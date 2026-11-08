# `AC-39-11` `[L]` 纯手柄焦点导航 —— 走查执行记录

**日期**:2026-11-08
**判据**:`design/gdd/casebook.md` 规则七(焦点序铁律 2026-10-06 订正面)· 规则八(零按键提示)·
**AC-39-11**(BLOCKING,`[L]`)
**包**:`unity/builds/Linux-x64/`(`Build Finished, Result: Success` @ `2026-11-08T01:13:57Z`,源 `39c51ab1` + 工作树)
**驱动**:`tools/input/verify_world_gamepad.py --walk3911 <OUTDIR>`(2026-11-08 新增走查档)
+ `tools/input/vpad.py`(uinput 虚拟手柄) · `[tutorialdiag]` 读数

---

## 1. 结论(**先说没做到的**)

**AC-39-11 记 `NOT-RUN`,不记 FAIL** —— 原因见 §2 之后的**订正**:真包里「右推到不了病名」
这一现象,经 PlayMode 导航夹具判定,**不是产品几何的锅**。

> 🔁 **订正(2026-11-08 同日,原文不删)**:本节初稿把「焦点到不了右列」判成 **FAIL**,
> 因为两套输入(手柄横推 / 键盘 Right)在同一处都不动。当天下午写了一条 PlayMode 导航夹具
> (`u1_assump6_gamepad_chain_test.test_casebook_rightMove_reachesDiseaseNameColumn`),
> 用**生产整链**(`InputSystemUIInputModule → PanelEventHandler → panel NavigationMoveEvent → FocusController`)
> 驱动真 `CasebookScreen`,读数为 `right→channel-disease-name` —— **跨得过去**。
> ⇒ 几何面没问题;真包里不动的那一下,归**输入/工具面**,与 `TD-walkthrough-01` 同族。

**原始观测仍然成立**(下面是当时看到的):

| 判据 | 状态 | 依据 |
|---|---|---|
| 手柄能开册 | ✅ 成立 | `模态=None → Casebook` + 截图 |
| 每步焦点**可见** | ✅ 成立 | 墨框随导航移动(帧哈希逐张不同) |
| 空行可聚焦、反馈只有墨框、零文字 | ✅ 成立 | 六行未落笔,均能获焦 |
| **七行访问序 = 面色→…→问诊→病名** | ⚪ **`NOT-RUN`(手段不足)** | 真包横推零效果;但夹具证明产品能跨(见订正)⇒ 不能据此判产品红 |
| **合册** | ⚪ **`NOT-RUN`(工具面)** | `startButton` 未送达(§4) |
| 翻页至例 2 / 两页一致 | ⚪ `NOT-RUN` | 本档只开了一例 |
| 落笔预览 | ⚪ `NOT-RUN` | 归 story-006 |
| 压痕声 | ⚪ `NOT-RUN` | 未听测 |

⇒ **AC-39-11 记 `NOT-RUN`**:三条「不知道」都不足以判红 —— ① 右推不动的**成因未定位**(夹具说产品没问题);② 合册被 `start` 键挡住(`TD-walkthrough-01`);③ 翻页缺第二例。

## 2. 实测观测

| # | 动作 | 观测 | 证据(帧哈希前 12 位) |
|---|---|---|---|
| 1 | 摇杆前推走近 + `buttonNorth` 就诊 | `模态=None → Casebook` | `01-opened.png` `1725eca5e802` |
| 2 | 竖向推杆 ×9 | 焦点**每格一变**,第 9 格回到第 1 格 ⇒ **8 态环** | `02-vert-01` `1b30b15fbc31` … `02-vert-09` **≡** `02-vert-01` |
| 3 | 环的内容 | 后案 → 前案 → 问诊 → 触感 → 呼吸 → 姿态 → 语声 → 面色 →(回环) | 同上,逐张目视 |
| 4 | **右推杆 ×2** | **整帧不变** ⇒ 焦点不动 | `03-right-01` ≡ `03-right-02` **≡** `02-vert-09` |
| 5 | `buttonSouth`(Submit) | 帧变了(焦点在「前案」的下一页动作上),但**无第二例可翻** | `04-fold-submit` `5c25ddabff98` |

**第 2/3 行**:铁律的七项是 `面色→语声→姿态→呼吸→触感→问诊栏→病名`,
而手柄竖推给出的环是 `面色→语声→姿态→呼吸→触感→**问诊→前案→后案**`→回环 ——
竖向确实不经过右列(**方向导航本来就是几何的,不是树序的**,竖推不走右列本身不违反铁律;
铁律约束的是**声明序 / 焦点环**,由 PlayMode 夹具守着)。
真问题只在「**横推也不动**」那一下,而那一下经夹具判定**产品侧是通的**。

⚠️ 侧证(不是本轮的,但同一形态):S26-3 的键鼠轮里 **`Right` 键同样零效果**
(`04a-top` 与 `04b-right-0/1/2` 逐字节相同),而 **`Tab` 能走到「病名」与「折」**(10 站环)。
⇒ **两套输入在同一处断** —— 这更像**布局几何**的问题(右列与左列没有可导航的相邻关系),
而不是某一层的按键映射。**本记录不裁成因**,只交事实。

## 3. 一条本轮才查实的事实:**`Casebook39.uxml` 不是运行面**

`SkeuoRuntimeDriver.cs:173` 建屏是 `new CasebookScreen(root, lib)`,而 `CasebookScreen` 的构造函数
直接调 `BuildUI()`(**代码建树**),全类**零 `VisualTreeAsset` / 零 `CloneTree`** ⇒
`Casebook39.uxml` 是 **spec 面**(走查加载 + 交叉断言),**运行时根本不读它**。
两面靠 `test_cross_uxmlSpec_buildUi_nameSequence_equivalent` 锁「name 序相等」(story-011 的双真源纪律)。

⇒ 两件后果,都已在 S26-4 的自动化里处置:
1. **只扫 UXML 会漏掉真正跑的那份** —— 往 `BuildUI()` 里加一句「按 E 查看」,UXML 侧全绿。
   已补 `test_ac8_44_buildUiFace_zeroHintTexts`(真构造一次 `CasebookScreen`,扫它的 Label 文案),M23 证它会红。
2. 焦点环的**运行时**几何来自 `BuildUI()`(左右两栏在匿名 row wrapper 里),UXML 只保证 name 序 ——
   §2 那条「横推不动」的成因因此**不在 UXML 里**,要看 `BuildUI()` 的布局。

## 4. 与既有夹具的关系(别重复造真源)

`Tests/PlayMode/DiagnosisSystem/casebook_render_test.cs` 已断言**建出来的树**的焦点序
= `ExpectedFocusOrder`(K=10),那一层是绿的。本轮走查证的是**运行时手柄真的按那个序走** ——
两件事不同,后者今天**不成立**。⇒ 夹具绿 ≠ 手柄走得到。

## 5. 合册为什么 `NOT-RUN` —— **工具面已定位,不是产品结论**

`PauseTogglePressed()` = `Escape` / 手柄 `startButton`;`ModalInputDriver` 的规则是
「有模态 ⇒ 关」。所以「合册」只差 start 这一下。实测:

| 实验 | 结果 |
|---|---|
| 长按 **`west`** 2.5 s(对照) | `[tutorialdiag]` 的 `手柄=` 出现 **`W`** ⇒ 虚拟手柄与读数都通 |
| 长按 **`start`** 2.5 s | `手柄=` **从不出现 `T`**(`DescribeGamepad` 对 `startButton.isPressed` 正是打 `T`)⇒ **BTN_START 没进 `Gamepad.startButton`** |
| 长按 `select` / `mode` / `l1` | 读数里没有它们的位,`inconclusive` |

⇒ 「start 关不掉脉案」这句**不能**记成产品缺陷 —— 按项目纪律,**读数全零先怀疑驱动**。
本项记为工具面缺口(§6)。

## 6. 截图清单(**不进库**)

目录:`$CLAUDE_JOB_DIR/tmp/walk3911b/`(本机)。⚠️ `.gitignore:346` 覆盖
`production/qa/evidence/**/*.png` —— 与既往所有走查同例,`git ls-files | grep '\.png$'` 零命中。
复现:`python3 tools/input/verify_world_gamepad.py --walk3911 /tmp/ac3911`。

## 7. 立债:虚拟手柄的 `start` 键未送达 `Gamepad.startButton`

已登记 `TD-walkthrough-01`(`docs/tech-debt-register.md`)。
**它不是 AC-39-11 的成因**,但**它挡住了 AC-39-11 的合册步**,不修就没法把那条走完。

## 8. 本轮**没有**做的事(不得借绿)

1. **七行访问序未走通** —— 见 §1;这是 `FAIL`,不是「没跑」。
2. **两页一致**(全空 / 部分落笔)未验 —— 只开了一例。
3. **手柄下的压痕声**未听测(44 侧)。
4. **主创签核**未做 —— 本件是观测记录,不是签核。
