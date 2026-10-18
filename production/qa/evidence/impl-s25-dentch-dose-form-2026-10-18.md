# 证据原件 —— story-025 戥子剂量读取形态元素(实现轮)

> **日期**:2026-10-18
> **对象**:`production/epics/skeuomorphic-ui/story-025-dentch-dose-form-element.md`
> **类型**:UI / Visual-Feel · **层级**:Feature(42 呈现层)
> **权威**:ADR-013(42 **只渲染,永不持有游戏状态** · 两栈元件库)· `AC-42-F3`(材质分支)
> · `AC-023-2`(零数字角标门)· `TD-prescription-01`(处置 = **须补**)
> **判据权威**:`AC-11-12`(拟物载体可读半)· `AC-11-13`(相邻档无色条件下可区分)
> **结论**:`AC-025-1…6` 全部落地并由夹具钉住;证伪轮 **15/15 RED-OK**;两棵树全量**新增测上零回归**。
> **⚠️ 本件不判定 `AC-11-12` / `AC-11-13` 转绿** —— 承 story §边界裁定 5,走查重跑归收口轮。

---

## 一、交付面(5 文件)

| # | 文件 | 动作 | 内容 |
|---|---|---|---|
| 1 | `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoThemeVariables.uss` | 改 | 黄铜层追加 **7 个** `--skeuo-brass-detent-*` 变量(几何值唯一出处) |
| 2 | `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoBrass.uss` | 改 | 追加 `.brass-detent-band` / `.brass-detent` / `.brass-detent--focused`;**附带**:`.brass-scale` 的 `height` → `min-height` |
| 3 | `unity/Assets/Gameplay.UI/Skeuomorphic/Brass/BrassScaleElement.cs` | 改 | 新增 `SetDetents(int count, int focusedOrdinal)` + `DetentCount` + `FocusedDetentOrdinal`;`TickCount` 原样保留 |
| 4 | `unity/Assets/Gameplay.UI/Skeuomorphic/Screens/CasebookScreen.cs` | 改 | `RefreshDentchDoses()` 每行由 `Label("戥子档")` 改为**该行自己的** `BrassScaleElement.SetDetents(档数, rowOrdinal)` |
| 5 | `unity/Assets/Tests/EditMode/SkeuomorphicUI/dentch_dose_form_test.cs` | **新建** | 23 测(该目录自有 asmdef `SkeuomorphicUI.Tests` 引 `Gameplay.UI` ⇒ 直构 42 侧类型,不走反射) |

### 形态设计(逐档形态 = 几何可分,颜色不变)

| 档 | 宽 | 高 | 上边距 | 边框宽 | 颜色 |
|---|---|---|---|---|---|
| 常态 `.brass-detent` | `--skeuo-brass-detent-width` | `--skeuo-brass-detent-height` | — | `--skeuo-brass-detent-border-width` | `--skeuo-brass-scale-color` |
| 落焦 `.brass-detent--focused` | `--skeuo-brass-detent-focused-width` | `--skeuo-brass-detent-focused-height` | `--skeuo-brass-detent-focused-lift` | `--skeuo-brass-detent-focused-border-width` | **不声明** ⇒ 承常态 |

**落焦块内零颜色声明** ⇒ `AC-025-1` ②「非颜色」在 USS 文本层**可判真**(由 `test_ac025_1_focusedForm_carriesNoColorDeclaration` 钉住)。

### ⚠️ 附带改动:`height` → `min-height`(须评审)

`.brass-scale` 原为 `height: var(--skeuo-brass-scale-height)`(2px 蚀刻刻度条)。自本件起该元件**同时承载逐档形态子件**,硬高会把子件夹扁(UI Toolkit 默认 `overflow: visible`,形态会溢出到相邻行)。改 `min-height` 后基线仍在、形态有容身之处。
**改前已 grep 复核**:`--skeuo-brass-scale-height` 全库仅 `SkeuoThemeVariables.uss:103` 声明 + `SkeuoBrass.uss:31` 引用,**零测试断言该值**。

---

## 二、AC ↔ 测试载体映射(23 测 · 全绿)

| AC | 子条 | 测试 |
|---|---|---|
| **AC-025-1** | ① 按 count 生成 N 个形态子件 | `test_ac025_1_setDetents_createsOneFormPerDetent` |
| | (恰一档落焦 · 共存互斥) | `test_ac025_1_setDetents_exactlyOneDetentIsFocused` |
| | ② 几何可分(非颜色) | `test_ac025_1_focusedForm_geometricallyDistinctFromNormalForm` |
| | ② 反控:可分性不得由颜色承载 | `test_ac025_1_focusedForm_carriesNoColorDeclaration` |
| | ③ `count ≤ 0` fail-loud | `test_ac025_1_setDetents_countNotPositive_throws` |
| | ④ 越界 fail-loud(禁钳位) | `test_ac025_1_setDetents_focusedOrdinalOutOfRange_throwsNotClamps` |
| | 几何值全走主题变量 | `test_ac025_1_detentGeometry_comesFromBrassLayerVariables` |
| **AC-025-2** | 入参取代而非缓存 | `test_ac025_2_setDetents_doesNotCacheFocusedOrdinal` |
| | 同入参 ⇒ 同形态(幂等) | `test_ac025_2_setDetents_sameArgs_sameForm` |
| | 结构判据:零可写剂量字段 | `test_ac025_2_element_exposesNoMutableDoseState` |
| **AC-025-3** | ① 新类存在 + 黄铜层注释 | `test_ac025_3_uss_declaresDetentClassesInBrassLayer` |
| | ② 零裸色值 / 零裸尺寸 | `test_ac025_3_detentBlocks_haveNoBareColorAndNoBareSize` |
| | ③ 块内零数字文本 | `test_ac025_3_detentBlocks_haveNoNumericText` |
| **AC-025-4** | ① 行序 = 档位序(第 k 行 ⇒ 第 k 档形态) | `test_ac025_4_eachRowCarriesItsOwnOrdinalForm` |
| | (形态子件不污染行集) | `test_ac025_4_rowsRemainTheOnlyIntUserDataCarriers` |
| | ④ 提交链零改动(转发显式 ordinal) | `test_ac025_4_rowSubmitStillForwardsExplicitOrdinal` |
| | ③ 幂等短路保留 | `test_ac025_4_detentCountUnchanged_idempotentShortCircuitHolds` |
| | ③ 反向半边:档数变了必须真重建 | `test_ac025_4_detentCountChanged_rowsRebuiltWithNewForms` |
| **AC-025-5** | ① 行内文本无十进制数字 | `test_ac025_5_dosePageText_carriesNoDecimalDigit` |
| | ② 元件零 string 读数 API | `test_ac025_5_element_exposesNoStringNumericReadout` |
| | ③ 黄铜 USS 全域零 `N/M` | `test_ac025_5_wholeBrassUss_hasNoFractionReadout` |
| **AC-025-6** | 形态载体归 brass 非 ink | `test_ac025_6_formCarrier_staysInBrassBranch_notInk` |
| | 墨侧 USS 不得引形态族类 | `test_ac025_6_detentClasses_areNotInkSideVocabulary` |

**过滤命令**:`unity test unity --mode EditMode --filter dentch_dose_form_test --output unity/Logs/s25-editmode-filtered.xml`
**结果**:`total=23 passed=23 failed=0`。

---

## 三、证伪轮(S12-4 · BLOCKING 门)—— `RED-OK 15/15`

脚本:`/tmp/fz-s25.py`(逐例**改 → 跑 → 还原在同一命令内**完成;`finally` 还原后对 3 个受保护文件做 **SHA-256 逐字节校验**,不符即脚本 `exit 3`)。**SHA-256 还原校验:全部通过。**

| # | 命中形态 | 停用/改坏的守卫 | 反证:哪条转红 |
|---|---|---|---|
| F1 | 定义域收窄 | `SetDetents` 的 `count <= 0` 守卫 | `test_ac025_1_setDetents_countNotPositive_throws` |
| F2 | 定义域收窄 | 落焦档序越界守卫(改成钳位式宽容) | `test_ac025_1_setDetents_focusedOrdinalOutOfRange_throwsNotClamps` |
| F3 | 共存互斥 | `ordinal == focusedOrdinal`(改成全档落焦) | `..._exactlyOneDetentIsFocused` + `..._doesNotCacheFocusedOrdinal` + `..._detentCountChanged_rowsRebuiltWithNewForms` + `..._eachRowCarriesItsOwnOrdinalForm` |
| F4 | 阈值边界 | 落焦档四条几何轴全部拉平为常态值 | `test_ac025_1_focusedForm_geometricallyDistinctFromNormalForm` |
| F5 | 共存互斥 | 落焦块「零颜色声明」(注入 `background-color`) | `..._focusedForm_carriesNoColorDeclaration` + `..._detentBlocks_haveNoBareColorAndNoBareSize` |
| F6 | 定义域收窄 | 几何值走主题变量(落焦高度改内联裸值) | `..._detentGeometry_comesFromBrassLayerVariables` + `..._detentBlocks_haveNoBareColorAndNoBareSize` |
| F7 | 一次性幂等 | `_detentBand.Clear()`(改成累积) | `test_ac025_2_setDetents_doesNotCacheFocusedOrdinal` |
| F8 | 一次性幂等 | `FocusedDetentOrdinal` 由形态反查(改成缓存字段) | `..._element_exposesNoMutableDoseState` + `..._doesNotCacheFocusedOrdinal` |
| F9 | 定序 | 每行携带该行自己的形态(改成恒第一档) | `..._detentCountChanged_rowsRebuiltWithNewForms` + `..._eachRowCarriesItsOwnOrdinalForm` |
| F10 | 一次性幂等 | `_builtDetentCount` 幂等短路(整段删除) | `test_ac025_4_detentCountUnchanged_idempotentShortCircuitHolds` |
| F11 | 阈值边界 | 幂等短路的档数比较(改成恒短路) | `test_ac025_4_detentCountChanged_rowsRebuiltWithNewForms` |
| F12 | 定义域收窄 | 零数字铁律(行内注入带档序的文本) | `test_ac025_5_dosePageText_carriesNoDecimalDigit` |
| F13 | 共存互斥 | 形态子件不带整数 `userData`(给子件挂 ordinal) | `..._rowsRemainTheOnlyIntUserDataCarriers` + `..._eachRowCarriesItsOwnOrdinalForm` + `..._detentCountChanged_rowsRebuiltWithNewForms` + `..._rowSubmitStillForwardsExplicitOrdinal` |
| F14 | 定义域收窄 | 元件不暴露 string 读数面(加一个 string 读数属性) | `test_ac025_5_element_exposesNoStringNumericReadout` |
| F15 | 定义域收窄 | 黄铜分支归属(撤掉 `brass` 类) | `test_ac025_6_formCarrier_staysInBrassBranch_notInk` |

### ⚠️ 首轮 F1 = **假绿**,夹具已修(证伪轮抓到自己的夹具缺陷)

首版 `test_ac025_1_setDetents_countNotPositive_throws` **只断异常类型**。撤掉「档数 ≤ 0」守卫后,入参 `(0, 1)` / `(-3, 1)` 仍被**档序守卫**(`focusedOrdinal > count`)**抛出同一类型** ⇒ 断言被**掩蔽**,仍绿(实得 `实红 0`)。

**修法**:改断 **`ParamName == "count"`** ⇒ 两个守卫各自可观测。修后 F1 复跑 **RED-OK**(基线 23/23 绿 + 变异恰红 1/1)。
**教训(可复用)**:当两条守卫抛**同型**异常时,「断类型」的夹具对其中一条**零判别力** —— 须断 `ParamName`(或消息)把两条分开。

---

## 四、回归对账(等式判据)

| 树 | 基线 | 本轮 | 等式 |
|---|---|---|---|
| EditMode | `s18-full-editmode-2` 3709/3664/0/45 | `s25-full-editmode-2` **3732/3687/0/45** | `+23 = +23` ✅ |
| PlayMode | `s18-full-playmode` 197/197/0/0 | `s25-full-playmode-2` **197/197/0/0** | `+0 = +0` ✅ |

> ⚠️ **口径**:准确表述是「**两棵树在本轮全部新增测上零回归**」,非无限定的「零回归」。
> 本轮**未新增 PlayMode 测**(本件无接线面),PlayMode 走「结果不变」判据。

### 首轮回归抓到 1 条**本件引入**的红(已修)

`s25-full-editmode`(首轮)实得 `3732/3685/**2**/45`,两条红:

| 红 | 归属 | 处置 |
|---|---|---|
| `MovementFeedTest.test_move_afterFreeFall_horizontalDisplacementBoundedByConfig` | **非本件引入** —— 已知间歇红。失败信息逐字:`expectedDx = 0.00099 < CharacterController.minMoveDistance (0.001)` ⇒ 位移被引擎吞。根因 = `AC-1-33` ③ 未落地的下游症状(`TD-player-01`)。 | 无(承 `TD-player-01`) |
| `NoHealthbarFeedbackTest.test_markRegistry_noPaperTear_noTearFields` | **本件引入** —— 破纸词表 `{tear, rip, break, split, fracture}` 扫的是**成员名**(不扫值),`DetentStripClass` / `_detentStrip` 里的 **`strip` 含 `rip`** ⇒ 误判为「破纸字段」 | **已修**:`strip` → `band`(`DetentBandClass` / `_detentBand` / `.brass-detent-band`) |

> ⚠️ 该红是**项目自有的门抓到的**,不是我的夹具 —— 说明 `no_healthbar_feedback_test` 的词表扫的是**成员标识符名**,命名时须避让 `rip` 子串(新登记事实,见 §五)。

---

## 五、实现轮发现(2 条)

1. **42 侧成员命名须避让 `rip` 子串** —— `NoHealthbarFeedbackTest` 的破纸词表按**成员名小写子串**匹配且**限引用类型**(`IsClass || IsInterface`)。
   实测:`const string DetentStripClass`(string 是引用类型)与 `private readonly VisualElement _detentStrip` **双双命中**。
   ⇒ 任何 `strip` / `grip` / `trip` 命名都会撞上;本件改 `band`。
   **该事实此前全库无登记**(现有代码未撞过),建议入 `docs/tech-debt-register.md` 或 `.claude/docs/coding-standards.md` 的命名避让表。

2. **同型异常掩蔽**(见 §三 F1)—— 证伪轮第二次在本项目抓到「夹具比 AC 弱」。

---

## 六、NOT-RUN 登记(禁借绿)

| 项 | 为何 NOT-RUN | 何时可判 |
|---|---|---|
| ~~**N 档截图**~~ ✅ **已跑(2026-10-18,见 §十)——两轮迭代后判绿** | **batch 出不了图是结构性事实**(① `ScreenCapture` 无渲染宿主抛异常;② batch 下纹理不进图集)⇒ 判据只能来自编辑器 Play | ~~你在编辑器 Play 里 F12~~ **已出图并判读** |
| ~~**`AC-11-12` 形态半 / `AC-11-13` 转绿**~~ ✅ **已由走查重跑判定(2026-10-18,见 §十一)** | 承 story §边界裁定 5:**本件不得自行判定** —— 判定权在走查 | ~~`S13-7` 走查重跑~~ **已跑:A3 ✅ 可读 / B2 ✅ 可辨(真人目视,主创+医学从业双签)** |
| **色盲模拟通道下的相邻档可分** | `TD-prescription-05` 结构性前置不可满足(UI 面板 ScreenSpaceOverlay ⇒ 相机后处理色盲模拟盖不到 UI) | 49 无障碍 / ADR-013 修订(**未排期**) |

> ⚠️ 本件只交「**形态存在且相邻可分**」的**机械**证据;截图**不替代** `S13-7` 真人走查(story §Test Evidence 逐字)。

---

## 七、判据与复算命令

```bash
# ① 过滤测试(23 测)
unity test unity --mode EditMode --filter dentch_dose_form_test --output unity/Logs/s25-editmode-filtered.xml
# ② 证伪轮(15 例,改→跑→还原同命令内 + SHA-256 校验)
python3 -u /tmp/fz-s25.py            # 期望: RED-OK 15/15 · SHA-256 还原校验 全部通过
# ③ 两棵树全量
unity test unity --mode EditMode --output unity/Logs/s25-full-editmode-2.xml
unity test unity --mode PlayMode --output unity/Logs/s25-full-playmode-2.xml
# ④ 42 侧三条机械门(本地预跑,零命中)
#    AC-42-F3 词门 / C4 裸值门 / AC-023-2 零数字 USS 门
```

### 边界裁定的落实(逐条)

| 裁定 | 落实 |
|---|---|
| ① **只交形态半** —— 剂量数值语义归 11 | `DentchDoseSelector` / `DoseValueOf` **零改动**(全件未触碰) |
| ② **不新增状态** | `BrassScaleElement` 零剂量字段;`FocusedDetentOrdinal` 由形态**反查** |
| ③ **不新增贴图 / 色** | 零新 `.png`;唯一新增颜色引用 = 复用既有 `--skeuo-brass-scale-color` |
| ④ 色盲模拟不在本件 | 未触碰(§六 NOT-RUN) |
| ⑤ 走查重跑归收口轮 | 本件不判 `AC-11-12`/`AC-11-13`(§六 NOT-RUN) |

### ⚠️ 一处需评审裁定的解释(未自行定论)

story §Context 的 `Control Manifest Rules (this layer)` 写「绑定点**唯一** = `DentchDoseSelector.LoadInto(BrassScaleElement)`」。
本件按 `AC-025-4`(逐字要求 `CasebookScreen.RefreshDentchDoses()` 每行携带该行自己的形态)在 `CasebookScreen` 内**新增了一个调用点**。
**理由**:`DentchDoseSelector` 刻意零可变状态、不持有「当前档」,**无法**驱动逐行形态(它只知道总档数);逐行档序的真源是行自身的 `userData`。
**读法**:该行描述的是**改动前**的既有状态(当时唯一绑定点);`LoadInto` 仍是**唯一的 selector→element 绑定点**(本件未新增第二条 selector 绑定)。`CasebookScreen` 走的是「行 → 形态」的另一条轴。
**留待评审/用户裁定**是否需就地改注该行。

---

## 八、判据结论

- `AC-025-1…6` **全部落地**,23 测全绿,逐条有反证(15/15 RED-OK)。
- 两棵树全量**新增测上零回归**(等式 `+23 = +23` / `+0 = +0`)。
- 首轮回归抓到的 1 条本件引入红(`strip`/`rip` 撞词表)已修并复验。
- 2 条 NOT-RUN 如实登记,未借绿。
- ⚠️ **本件不判定 story-025 转 `Complete`** —— 截图 NOT-RUN + 走查重跑未执行;且按项目流程须先 `/code-review` 再 `/story-done`。
- ⚠️ **未 commit**(无用户指令)。

---

## 九、评审修复轮附录(2026-10-18 · 评审轮 `/code-review`)

> **本节订正上文 §二 / §三 / §七 的**过时引用** —— 评审修复改动了夹具,故那些数字/测名不再代表当前状态。
> 评审判定与结账见 `production/qa/evidence/review-story-025-2026-10-18.md`(本文件**不重述**判定)。
> **上文原样保留作闭环记录,不改写。**

### 9.1 上文过时处(逐条)

| 上文位置 | 原写 | 现为 |
|---|---|---|
| §二 映射表 `AC-025-5` ③ 行 | `test_ac025_5_wholeBrassUss_hasNoFractionReadout` | **`test_ac025_5_wholeBrassUss_hasNoNumericContent`**(R-10:主断言对齐 AC 的 `content:` 数字面;`N/M` 面降为**附加面**) |
| §二 映射表 `AC-025-6` 两行 | 断「黄铜分支」 | ⚠️ **仅类名级代理** —— AC 原文要求的**三枚举分类器全库不存在**(R-1)。`AC-025-6` **已撤勾**,缺口转 `TD-prescription-07` |
| §三 证伪轮 | `RED-OK 15/15` · 脚本 `/tmp/fz-s25.py` · 3 个受保护文件 | **`RED-OK 17/17`** · 脚本 **`/tmp/fz-s25-r1.py`** · **4 个**受保护文件(新增 `SkeuoThemeVariables.uss`) |
| §七 命令 ② | `python3 -u /tmp/fz-s25.py` | `python3 -u /tmp/fz-s25-r1.py`(期望 `RED-OK 17/17`) |

### 9.2 修复轮新增两例(判别力已证)

| # | 钉住的评审项 | 变异 | 反证结果 |
|---|---|---|---|
| **F16** | **R-5**(白名单死码) | 把 `public int TickCount { get; set; }` 换成**显式**背衬字段 `_tickCountBacking` ⇒ 字段名不再是 `<TickCount>k__BackingField` | `test_ac025_2_element_exposesNoMutableDoseState` **转红** ✅ |
| **F17** | **R-6**(层归属只用全文件 `Contains`) | 把 `--skeuo-brass-detent-width` 的**声明行**从黄铜层搬到器具档层(全文件 `Contains` 仍为真) | `test_ac025_1_detentGeometry_comesFromBrassLayerVariables` **转红** ✅ |

⇒ 两条新守卫**非空洞**:旧夹具对这两类漂移**测不出**,新夹具测得出。

### 9.3 修复轮判据(复算)

| 判据 | 状态 |
|---|---|
| 过滤测 `--filter dentch_dose_form_test` | ✅ **23/23 绿**(夹具改后复跑,不误伤) |
| 证伪轮 `/tmp/fz-s25-r1.py` | ✅ **`RED-OK 17/17`** · **SHA-256 还原校验 全部通过** |
| 两棵树全量(EditMode / PlayMode) | ✅ **`3732/3687/0/45`** · **`197/197/0/0`** —— 与实现轮(`3732/3687/0/45` / `197/197/0/0`)**逐位同值** ⇒ `+0 = +0`(修复轮未新增 `[Test]`)。⚠️ 本轮**零红**:实现轮首轮曾出现的那条 `MovementFeedTest` 间歇红(`TD-player-01` 下游)本轮**未复现** —— **不得**读成「已修」 |

### 9.4 修复轮仍留白(禁借绿)

- **R-1** —— AC 重锚 + 落债已落,**分类器本身仍未实现**(归 `TD-prescription-07`);
- **R-4** —— `TickCount` write-only **未修**(归 `TD-prescription-08`);
- **R-8** —— **✅ 已就地改注**(用户 2026-10-18 裁定):该行限定为「selector → element 绑定点唯一」,
  并声明「行 → 形态」是另一条轴;见 story-025 §Context。
- **三专家全部未运行 Unity** —— 几何 / Yoga 结论为静态推导(见评审原件 §五)。

---

## 十、截图轮(2026-10-18 · 编辑器 Play · F12 · 1x)—— 两轮迭代,判据**通过**

> 评审原件 §五 未及核查项 **#5(「档块底色与所在板同色的可辨性 —— 需人眼读数」)被首图实测命中**,
> 本件登记其修复与复验。**截图轮的发现不是 R-1…R-10 的任何一条** —— 它是 23 条自动化夹具
> **结构性测不到**的一类缺陷:夹具断的是 USS 文本层 / resolved style 值,渲染层零对比度照样全绿。

### 10.1 首图判红(修复前)

- 图:`production/qa/evidence/skeuomorphic-ui/dentch-form-2026-10-18-before-fix.png`
  (`unity/Logs/shots/20261018_090548_1x.png`,593×429)
- **判红依据(双通道交叉)**:
  ① `/image-vision`(SenseNova)判读:「5 行高度、宽度、颜色、描边、块尺寸均一致,
     没有哪一行的铜块明显更大/更高/更宽;唯一差异是第 1 行叠了焦点高亮」;
  ② 本地逐像素扫描(PIL,numpy):行 2–5 铜条带(y 146/168/191/213)像素剖面**完全相同**;
     `#8C5A2B` 棕色掩码全行连续、无任何分段。
- **通过的一半**:全画面零数字 / 零分数 / 零百分比(行内文本已随 `AC-025-4` 改造消失 ⇒
  同时证明截图出自**本件后的新构建**)。
- **根因(机械定位)**:`.brass-detent` 的底色 `--skeuo-brass-scale-color` `#8C5A2B` =
  描边 `--skeuo-brass-border` `#8C5A2B` = 所在板 `.brass-scale` 底色(**同 scale-color**)——
  **三色全同** ⇒ 档块溶进铜条。`TD-prescription-01`(「除焦点框外行内容逐像素相同」)的病根
  在渲染层**依旧成立**;23 测测不到,因其断言对象是样式声明文本与 resolved style,非可见性。

### 10.2 修复(甲案,用户 2026-10-18 裁定;倍率维持 1x)

- 改动 **1 行**:`SkeuoBrass.uss` `.brass-detent` 的
  `background-color: var(--skeuo-brass-scale-color)` → **`var(--skeuo-brass-bg)`**(`#B8863B`,
  art-bible §4.1 权威黄铜金,`:root` 作用域可继承)。注释登记失败实证链(图名 + 三色全同 + §五 #5 命中)。
- **不变量复核(静态)**:
  - 落焦块**仍零颜色声明** ⇒ `test_ac025_1_focusedForm_carriesNoColorDeclaration` 不破坏;
  - `var(...)` 非裸 `#`/`rgb(` ⇒ C4 裸色门 / `..._haveNoBareColorAndNoBareSize` 不破坏
    (去注释正则与夹具同口径预跑:全文件裸色零命中);
  - 选择器与变量行未移动 ⇒ 层归属行区间判据(R-6/F17)不破坏;
  - 零新增色、零新贴图 —— 唯一新增引用 = 复用既有 `--skeuo-brass-bg`。

### 10.3 复图判绿(修复后 · 退出 Play 重进后截)

- 图:**`production/qa/evidence/skeuomorphic-ui/dentch-form-2026-10-18.png`**
  (`unity/Logs/shots/20261018_092936_1x.png`,593×429)
- **像素判据(我方可复算)**:每行铜条上**恰一个金色凸起**(`|RGB − (184,134,59)| ≤ 40` 精确簇,
  列内 ≥3 行金),位置逐行**阶梯**:

  | 行 | 金档块 x | 档位 |
  |---|---|---|
  | 1 | 18–19 | 第 1 档 |
  | 2 | 22–23 | 第 2 档 |
  | 3 | 26–27 | 第 3 档 |
  | 4 | 30–31 | 第 4 档 |
  | 5 | 34–35 | 第 5 档 |

  步进 4px = 一个档块节距 ⇒ **「行序 = 档位序」(`AC-025-4`)首次在渲染层可见**。
  旧图同扫描 = **零金色簇**(因果闭环:修复前后唯一变量是那行 USS)。
- **Vision 交叉确认**(独立通道):5 行 / 「各行凸起位置不相同」/「没有数字、分数、百分比」/
  焦点高亮在第 1 行。
- ⚠️ **如实登记的观感限制**:Game view 缩放后常态档(6×12 逻辑 px)落在条带内仅约 2px,
  1px 棕描边吃满 ⇒ **常态档在条带上近乎不可见,每行实际只有落焦档(20px 高上溢出条带)的金芯露出**。
  满足「相邻行可区分」判据(金块位置阶梯),但这是**低密度读数** ——
  是否够用归 `S13-7` 走查人眼裁定;若要增强,调档块尺寸 / 节距属 44 呈现调参旋钮(数值归用户),
  **不在本件范围**。

### 10.4 截图轮判据(复算命令)

```bash
# ① USS 改动后夹具复跑(编辑器须关,batch 独占)
unity test unity --mode EditMode --filter dentch_dose_form_test \
  --output unity/Logs/s25-ussfix-editmode-filtered.xml
# ② 像素判据(只读,可反复跑)
python3 -I - <<'PY'
from PIL import Image; import numpy as np
a = np.array(Image.open("production/qa/evidence/skeuomorphic-ui/dentch-form-2026-10-18.png").convert("RGB")).astype(int)
gold = (np.abs(a - np.array([184,134,59])).sum(axis=2) <= 40)
for (y0,y1,lab) in [(123,129,"行1"),(145,151,"行2"),(167,174,"行3"),(190,196,"行4"),(212,218,"行5")]:
    xs = np.where(gold[y0:y1+1].sum(axis=0) >= 3)[0]
    print(lab, xs.tolist())
PY
# ③ USS 静态门预检(裸色,夹具同口径)
python3 -I -c "
import re; src=open('unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoBrass.uss').read()
nc=re.sub(r'/\*.*?\*/','',src,flags=re.S)
print(re.findall(r'(background-color|color|border-color)\s*:\s*(rgb\(|#)', nc))"
```

### 10.5 全量回归(2026-10-18 · USS 改动后 · batch 实跑)

| 树 | 基线 | 本轮 | 等式 / 判定 |
|---|---|---|---|
| 过滤测 | `23/23` | `unity/Logs/s25-ussfix-editmode-filtered.xml` = **23/23/0** | ✅ |
| EditMode 全量 | `3732/3687/0/45` | `unity/Logs/s25-ussfix-full-editmode.xml` = **`3732/3686/1/45`** | total `+0 = +0` ✅;**唯一红 = `MovementFeedTest`**(见下) |
| PlayMode 全量 | `197/197/0/0` | `unity/Logs/s25-ussfix-full-playmode.xml` = **`197/197/0/0`** | ✅ **逐位同值** |
| `MovementFeedTest` 单跑 | — | `unity/Logs/s25-ussfix-movementfeed-rerun.xml` = **5/5 绿** | 全量红 / 单跑绿 ⇒ **间歇红坐实** |

- **唯一红的归属(机械判定,非归因修辞)**:`DaYiJingCheng.Tests.Boot.MovementFeedTest.
  test_move_afterFreeFall_horizontalDisplacementBoundedByConfig` —— 失败信息
  `expected 0.0008741 +/- 0.0001874, But was 0.0`(位移被 `CharacterController.minMoveDistance` 吞,
  与实现轮 §四 那条**同型同文**)。已知间歇红(`TD-player-01` 下游):实现轮首轮红 →
  修复轮未复现 → 本轮全量复现、**单跑即绿**。⚠️ **不得读成「已修」**,亦**不得读成本轮 USS 改动引入**
  (USS 属 42 呈现层,该测在 Boot 树;total 与基线**逐位同值**,红项非新增)。
- ⚠️ 操作性记录:EditMode + PlayMode 两条 batch **串在同一条后台命令**时,第二条以
  Unity exit 2 起不来(第一条已完成、XML 在盘,非测试红);**拆开单跑即成功** —— 后续全量回归勿串联。

---

## 十一、走查重跑结清(2026-10-18 · S13-7 重跑批)

- **执行**:用户真人目视(编辑器 Play · 含 story-025 + 甲案 USS 修复的构建 · 方笺页);
  agent 回填。报告 `production/playtests/playtest-2026-10-18-s13-7-rerun-dentch-form.md`。
- **裁定**(AskUserQuestion 四问,全部真人答复):
  - **腿 A3**(`AC-11-12` 拟物载体可读半)= **✅ 可读**;
  - **腿 B2**(`AC-11-13` 仅凭形态分辨相邻两档)= **✅ 可辨**(`ELSE` 分支不触发);
  - 观感限制(低密度读数:常态档近乎不可见,仅落焦档金芯露出)= **「够用,不增强」**
    ⇒ 不触发 44 调参增强债(数值归用户,裁量已行使并留痕);
  - 签核 = **主创 + 医学从业双签**(solo 同签,项目规约允许)。
- **改判依据的证伪性**(§8 报告):10-16 批 🔴 与本轮 ✅ 之间**唯一机械变量** =
  形态元件(story-025)+ 对比度修复(甲案 1 行 USS),且目视结论与像素扫描、SenseNova vision
  **三向一致** —— 非「重测一遍期望变绿」。
- **债结转**:`TD-prescription-01`(根因)/ `-02` / `-03` **✅ 结案**;
  `-04`(腿 C,归 S14-10)/ `-05`(49 通道,豁免≠满足)/ `-06`(美术轮)**维持 open**。
- **留证口径如实登记**:本批未新落 F12 图(复用同日截图轮同一构建的两图,覆盖成立;
  与原批 §0「每腿至少 2 张」相比密度更低 —— 登记而非掩饰)。
- **本件判据义务就此全部达成**(①/code-review ②/截图 ③/走查)⇒ story-025 可进 `/story-done`。
