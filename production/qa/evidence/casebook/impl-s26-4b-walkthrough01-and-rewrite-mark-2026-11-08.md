# 实现证据 —— S26-4 续:`TD-walkthrough-01` 收口 + 改判留痕元件(2026-11-08)

**Story**:`production/epics/casebook/story-005-page-layout-focus-navigation.md`
**权威**:`design/gdd/casebook.md` 规则六(改判留痕)· 规则七(焦点序)· `AC-39-11` / `AC-39-12`
**本轮三件**(用户 2026-11-08 指令「继续完成」):
① 修 `TD-walkthrough-01`(虚拟手柄 `start` + 横向轴)② 第二个病例(两页对照)③ 痕渲染元件(AC-39-12 的判据对象)

---

## 0. 结论表

| # | 件 | 结论 |
|---|---|---|
| ① | `TD-walkthrough-01` | **closed** —— `start` 是**工具发错码**(非 udev / 非 vendor 表);「横推不动」是**误判** |
| ② | 第二个病例 | **已跑通** —— 走到 `#76@(14,0,1)` 开例 2,竖推焦点序**与例 1 逐项相同** |
| ③ | 痕渲染元件 | **已落盘** —— 读时投影 + 能力接口 + 42 匿名元件;EditMode 9 · PlayMode 3 条判据 |
| — | 证伪轮 | 3 条变异**各自恰 1 红**,SHA-256 逐字节还原 |
| — | 回归 | 见 §4(三元组 + 等式) |

---

## 1. ① `start` 的真因:**能力声明顺序**,不是码名

`vpad.py` 原按码名对字面(`start` → `BTN_START`)。Unity 的 Linux evdev 后端在**本设备声明的
能力顺序**上建 `Gamepad` 布局 ⇒ 同一码落到别的 control。实测(`probe_map.py`,逐键 0.7 s):

| 意图 | 旧码 → 落点 | 新码 → 落点 |
|---|---|---|
| `start` | `BTN_START` → `leftStickPress` | **`BTN_TR2` → `start`** ✅ |
| `select` | `BTN_SELECT` → **无** | **`BTN_TL2` → `select`** ✅ |
| `l3` | `BTN_THUMBL` → 无 | `BTN_START` → `leftStickPress` ✅ |
| `r3` | `BTN_THUMBR` → 无 | `BTN_MODE` → `rightStickPress` ✅ |

**负面结果(排除一条候选)**:把 `vendor/product` 换到 `0x045E/0x028E`(Xbox 360)⇒ Unity
**根本不建 `Gamepad`** ⇒ 不是「按型号选表」,是能力顺序。(故 `VPAD_VENDOR` / `VPAD_PRODUCT`
环境变量保留,作为同类排查入口。)

## 2. 一条更重要的教训:**读数面造成的误判**

`手柄=` 列原来只挂在 **tick 边沿**的周期行上,而**模态一摊开 tick 就停**(ADR-013 §十-C)
⇒ 「按住 start 开出模态」那一刻之后**一行周期读数都不会再出现**,`T` 位**结构上永远打不出来**。
上一轮据此判「start 没送达 Unity」—— **错**,当时暂停壳其实已被 start 打开(焦点读数看得见)。
修法:`ReportGamepadIfChanged` 走**每帧 + 变化才打**(与 `ReportFocusIfChanged` 同因同法),
并在 `DescribeGamepad` 尾部补 `_P(<control>)` **落点转储**(只认已知面键时,落到别处是看不出来的)。

## 3. ③ 痕(改判留痕)元件

| 层 | 落点 | 关键决定 |
|---|---|---|
| Sim | `CaseStreamQuery.IsCurrentJudgmentRewritten` | 痕 = **读时投影**(最后一条判断是改判且 `lexicon_id != 0`);擦除不留痕;**必须**是投影,写侧 bool 读档即丢(`AC-39-13`) |
| 契约 | `ICasebookRewriteMarkSource`(`CasebookBinding.cs`) | **并列能力接口**,不给 `ICasebookDtoSource` 加成员 —— 后者有 7 个夹具,加成员 = 七处连坐;能力接口未实现 ⇒ 按「无痕」渲染(不崩、不造痕) |
| 装配 | `CasebookVitalsSource.HasRewriteMark` | 扫整条病例流(与 `InkAgeTicks` 同源:开案 + 载荷自带 `CaseId` 归属) |
| 42 | `CasebookScreen` 匿名 `VisualElement` + `.rewrite-mark` | **匿名**(不进 spec 面 name 序)· `focusable=false`(焦点环仍 K=10)· `pickingMode=Ignore` · `Visibility.Hidden`(出现/消失不重排)· **形状**承载(右边框 + 下边框 + 旋转 = 对勾),零文本零字号 |

**⚠️ 首版是 `Label("✓")`,被两条**既有**门各红一次**(如实登记 —— 门的牙齿在这里):
| 门 | 红的原因 | 处置 |
|---|---|---|
| `casebook_render_test.test_ac8_23_emptyRows_headerOnly_zeroPlaceholderText` | 断言右栏**文本面** ⊆ {病名, ?, 折};`✓` 多出一个字 | 改**不落文本** |
| `casebook_render_test.test_casebook_all_labels_resolve_to_sdf` | 断言屏内**全部文本** ≥ 24px;首版 18px | 同上(无文本即无字号) |

⇒ 两条门指向同一件事:**它不是文字,是记号**。改成「边框画对勾」后两条转绿,
而「非色单载」仍由**形状**承载(`AC-39-12` 要的正是这个)。这不是绕过门 —— 是门纠正了一个
概念错误(把记号当文本画)。

**为什么不是「顺手给 ICasebookDtoSource 加个 bool」**:那会把「本能力与既有 7 个夹具无关」
变成七处连坐的改动;而「有没有痕」正好是**装配层答得出、夹具答不出**的能力 —— 能力接口的退化路径
(未实现 ⇒ 无痕)本身就是被夹具断言的行为(PlayMode `test_sourceWithoutCapability_noMark_noThrow`)。

## 4. 判据与证伪

| 套件 | 条数 | 覆盖 |
|---|---|---|
| EditMode `CasebookGuards/casebook_rewrite_mark_test` | **9** | query 6(空流 / 只落笔 / 改判有痕 / 新起一笔清痕 / 擦除无痕 / 别案不串)+ 接线 3(生产实现存在 / 三态流转 / 未绑病人) |
| PlayMode `SkeuomorphicUI/casebook_rewrite_mark_test` | **3** | 有痕可见→无痕隐藏 / 源未实现能力不崩不造痕 / 元件不可聚焦且入环元素数仍为 10 |

**证伪轮(3 变异,各自恰 1 红)**:

| # | 变异 | 实测 | 还原 |
|---|---|---|---|
| M-A | `marked = p.LexiconId != 0` → `marked = true` | 恰 1 红(`test_revisedToEmpty_eraseNoMark`) | SHA-256 `14046c5b…` ✅ |
| M-B | 删掉屏里的 `visibility` 切换 | 恰 1 红(`test_rewriteMarked_visible_thenCleared_hidden`) | SHA-256 `1eeb8e7c…` ✅ |
| M-C | 装配层 `if (_patient.Value < 0) return false;` → `if (true) return false;` | 恰 1 红(`test_recordOnly_noMark_thenRevise_marks_thenErase_clears`) | SHA-256 `621a8cc7…` ✅ |

还原后复跑三条判据 **6/0 · 3/0 · 3/0** 全绿。

## 5. 回归三元组

| 套件 | 计数 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7166 / 7129 / 0 / 0 / 37` | `2026-11-08T05:11:39Z` | sprint-26 S26-4 续 | `6d82cdd9` + 工作树 |
| PlayMode | `369 / 365 / 0 / 2 / 2` | 同上 | 同上 | 同上 |

**基线**(本批开工前)`7157 / 7120` · PlayMode `366 / 362`。
**等式**:`7166 − 7157 = 9` = 新增 `[Test]` 条数 · `369 − 366 = 3` = 新增 `[UnityTest]` 条数。

```bash
grep -c "\[Test\]" unity/Assets/Tests/EditMode/CasebookGuards/casebook_rewrite_mark_test.cs       # ⇒ 9
grep -c "\[UnityTest\]" unity/Assets/Tests/PlayMode/SkeuomorphicUI/casebook_rewrite_mark_test.cs  # ⇒ 3
```

⚠️ **中间两次跑是红的,如实登记**(不是「一次就绿」):
① 首版 `Label("✓")` ⇒ PlayMode 恰 2 红(§3 表里那两条既有门);
② 改成边框画对勾后第一次跑 ⇒ `error CS0246: Color`(缺 `UnityEngine.` 前缀)⇒ **编译失败**,
   测出来的 2 红是**旧程序集**的结果,不能当判据(承「构建绿 ≠ 门跑了」的镜像:**编译红 ≠ 判据红**)。
修好后才是上表这一组。

## 6. 未做(不得借绿)

1. **像素半边的「灰度化后仍可辨」** —— 元件在,判据亦覆盖可见性;但真包里**落不了笔**
   (`病名册环 seen=0 pointable=0`)⇒ 改判不会发生 ⇒ 痕不会出现。**归 8 / 9 的查体→新颖度→词表链**,
   不归本 story。见 `story-005-ac39-12-paper-feel-walkthrough.md` §5.1。
2. **压痕声**未听测(44 侧)。
3. **主创签核**未做。
4. **长按横推会来回弹**(`面色 ⇄ 病名`)—— 如实登记,归手感/数值轮,未修。
