# 实现原件 —— `O-18-R6` 落地:炮制屏(第九员)+ 准入接缝 + `Utensil` 接通

> **对象**:`O-18-R6`(18 → 42 的只读准入接缝)· `AC-18-10b`(呈现半边)· `TD-interaction-03` 的 `Utensil` 残余
> **批次**:`S23-24`(用户指令「两步一起」→「我倾向甲,但你需要实际操作进去看看效果是不是合理」)
> **截至时刻 / commit**:见 §六

---

## 一、开工前的事实勘察(改变了问题的性质)

债册写「`Utensil` 卡在 42 屏清单认领」。逐条核实后,**卡的是三件,且第一件在 18 侧其实已经做完了**:

| # | 结论 | 复算 |
|---|---|---|
| 1 | **18 侧供给已齐** —— `AdmissiblePredicate`(F-18.2 两道门)· `ProcessingCandidateSet`(三条件合取)· `CraftIgnitionTransaction`(五步事务)· `CraftEventWriter`(唯一写入口)全在库 | `grep -rn "class AdmissiblePredicate\|class ProcessingCandidateSet\|class CraftIgnitionTransaction" unity/Assets/Sim/Processing/` |
| 2 | **真正的缺口在类型面** —— `Gameplay.UI.asmdef` 的引用集 = `{Sim.Contracts, Gameplay.Presentation}`,**看不见 `Sim`**;而 `AdmissiblePredicate` / `ProcessingCandidateSet` 都住 `Sim` ⇒ 「18 提供只读准入谓词」此前**在类型面根本不存在** | `cat unity/Assets/Gameplay.UI/Gameplay.UI.asmdef` |
| 3 | **42 屏闭集无 Processing** —— `ModalId` 8 员无炮制;`Screens/` 无 `ProcessingScreen`;全库 `grep ProcessingScreen\|CraftPanel\|RecipeList` ⇒ 零命中 | 同上 |
| 4 | **GDD UI-18.3 自相矛盾** —— 它写「P0 无独立炮制面板,选料复用 20 出诊箱容器」,但 20 的 spec wireframe 里**只有** `[⧉叠角=炮制中]` 状态标记,**没有任何配方区** ⇒「复用 20」在 spec 层无落点 | `sed -n '120,145p' design/ux/inventory-container-20.md` |

⇒ 本轮范围据此重定:**立接口(18 侧真缺口)+ 立屏(42 侧闭集)+ 接通 `Utensil`**。

---

## 二、改了什么

| # | 文件 | 变更 |
|---|---|---|
| 1 | `Sim.Contracts/IProcessingAdmissionQuery.cs`(**新增**) | `O-18-R6` 的**接口半边** —— 只读准入查询,形状照 `AdmissiblePredicate.Judge` |
| 2 | `Sim/Processing/FurnaceBusyQuery.cs`(**新增**) | `ISingleFurnaceQuery` 的**首个生产实现**,转接 `FurnaceState.IsBusy` |
| 3 | `Gameplay.Boot/ProcessingAdmissionQuery.cs`(**新增**) | `IProcessingAdmissionQuery` 的生产实现,转接 `ProcessingCandidateSet.IsCandidate` |
| 4 | `Gameplay.UI/Skeuomorphic/Screens/Processing18Screen.cs`(**新增**) | 炮制屏(最小可操作:候选行 + 选中 + 点火) |
| 5 | `Gameplay.UI/Skeuomorphic/ModalId.cs` | 增第九员 `Processing = 9` |
| 6 | `Gameplay.UI/Skeuomorphic/SkeuoRuntimeDriver.cs` | `ModalScreenNames` + `ScreenFactories` 各增一条 |
| 7 | `Gameplay.Boot/CompositionRootServices.cs` | 增 `RecipeSource`(惰性 `Func`)+ `ProcessingAdmission` |
| 8 | `Gameplay.Boot/CompositionRoot.cs` | 装配三件(配方惰性源 / 技能等级 / 单炉查询) |
| 9 | `Gameplay.Boot/BootRoot.cs` | `enterProcessing` 由 `null` 改为真接线 |
| 10 | `Tests/EditMode/SkeuomorphicUI/dto_boundary_and_modal_test.cs` | 闭集门 9 值/8 员 → **10 值/9 员** |
| 11 | `Tests/EditMode/Interaction/modal_gate_test.cs` | `ClosedSetMembers` 补 `Processing` |
| 12 | `Tests/PlayMode/SkeuomorphicUI/processing18_screen_test.cs`(**新增**) | 5 条判据 |
| 13 | `docs/architecture/adr-013-skeuomorphic-ui-framework.md` | **§十-E Amendment D**(第九员) |

### 关键落点判断(逐条给理由)

**为什么接口住 `Sim.Contracts`**:`Gameplay.UI` 够不着 `Sim`(见 §一 第 2 条)。ADR-025 §① 的
装配清单封闭 ⇒ 只有 `Sim.Contracts` 能让两侧都看见,且不开新程序集边。

**为什么 `FurnaceBusyQuery` 住 `Sim` 而 `ProcessingAdmissionQuery` 住 `Gameplay.Boot`**:
- 前者**只**转接 `FurnaceState.IsBusy`,两者都在 `Sim` —— 零跨程序集需求。
  ⚠️ **曾短暂放 `Gameplay.Boot`,被 `test_ac1817b_saveComposerReferencesNoProcessingTypes` 判红**
  (该门扫 `Gameplay.Boot` 全目录要求零 18 类型引用)。**移回 `Sim` 是正确落点,不是为了让门变绿
  而改门** —— 那条门至今未动。
- 后者要拼 `ISkillLevelQuery` + `ISingleFurnaceQuery` 两个外部依赖 ⇒ 装配层职责;
  且它只引 `ProcessingCandidateSet`(不在该门表内)⇒ 合法。

**为什么配方源必须惰性**:早期版本写成 `RecipeDataSet?` 值属性并在装配期直接
`loadRecipesSeed()` 求值 —— **实测 24 条红**(`RealRegistry*` / `WTrendEps*` / `ReaderWiring*`)。
那些夹具在 `Assemble` 之后才注入 provider / 替换表 ⇒ 装配期提前读数据集**改变了装配时序**。
⇒ 改为 `Func<RecipeDataSet>` 包一层,只在屏真打开时求值。**与 `bakedInitialItemIds` 同款纪律:
注释说惰性,代码就必须真惰性。**

---

## 三、判据(5 条,逐条可证伪)

| # | 判据 | 测试名 |
|---|---|---|
| ① | **`AC-18-10b` 主判据**:不可发起 ⇒ **不建行**,且**不是灰置**(树上无该元素,`enabledSelf` / `disabled` 类两项都验) | `test_ac1810b_inadmissibleRecipe_hasNoRow` |
| ② | 可发起 ⇒ 建行,可获焦,文案 = `RecipeId` | `test_ac1810b_admissibleRecipe_rowShowsRecipeId` |
| ③ | 全不可发起 ⇒ 零行 + `FirstFocusable == null`(空屏不假装有落点) | `test_ac1810b_allInadmissible_zeroRows_noFocusTarget` |
| ④ | 选中 ⇒ 出口收到该配方的 `Ordinal`(42 只发意图,不判定) | `test_ac1810b_activate_emitsOrdinalToSink` |
| ⑤ | 准入查询为 null ⇒ `Bind` 抛(fail-closed) | `test_ac1810b_bindWithoutAdmission_throws` |

⚠️ **⑤ 的测试替身 `FixedAdmission` 是刻意的**:若直调生产 `AdmissiblePredicate`,变异
「屏自己合取」时两边一起动 ⇒ **零判别力**(自指判据)。替身让「屏问不问准入」与「准入怎么判」
两件事可分离。

---

## 四、证伪轮

| 变异 | 打什么 | 实测红 | 形态 |
|---|---|---|---|
| **P18-M1** | 不过滤(全部配方都建行) | **2 红**:`..._inadmissibleRecipe_hasNoRow` + `..._allInadmissible_zeroRows` | exact |
| **P18-M2** | 建行后 `SetEnabled(false)`(**灰置**而非不建) | **2 红**:`..._inadmissibleRecipe_hasNoRow` + `..._admissibleRecipe_rowShowsRecipeId` | exact |
| **P18-M3** | 删掉 `Bind` 的 null 守卫 | **1 红**:`..._bindWithoutAdmission_throws` | exact |
| **P18-M4** | 发**行序**而非 `Ordinal` | **1 红**:`..._activate_emitsOrdinalToSink` | exact |

**结果:4/4 转红,4/4 SHA-256 逐字节还原**(每轮 `RESTORE OK` + 收尾 `FINAL-OK`)。

⚠️ **P18-M2 是本轮特意加的变异**:它打的是「灰置 vs 不建行」这个**AC-18-10b 的字面区别**。
若只做 M1(全列出),「灰置」这条失败模式**不会被抓住** —— 而灰置恰恰是 GDD 逐字禁止的形态。

---

## 五、回归

| 套 | 读数 | 文件 / 时刻 |
|---|---|---|
| **本件过滤** | `5 / 5 / 0` | `unity/Logs/proc18i.xml` |
| **EditMode 全量** | **`6607 / 6570 / 0 / 0 / 37`** | `unity/Logs/p18-em-full.xml` @ `2026-11-02 17:13:44 CST` |
| **PlayMode 全量** | **`306 / 302 / 0 / 2 / 2`** | `unity/Logs/p18-pm-full2.xml` @ `2026-11-02 17:17:00 CST` |

**等式**:EditMode `6607 − 6606 = 1`(新增 `dto_boundary_and_modal_test` 的 `Processing` 点名断言口径变化,
无净新增测试)· PlayMode `306 − 301 = 5` = 本件新增 5 条 `[UnityTest]`。
⚠️ **PlayMode 首轮 `failed=1`** —— 见 §八 第 8 条(漏改第三处闭集门)。**该轮读数作废,不引用。**
⚠️ **`PM exit=2` 是本仓 PlayMode 常态退出码** ⇒ 判据取 XML 的 `failed` 计数。

---

## 六、⚠️ 实际效果:结构对,但**内容面是空的**(这是你问的核心)

**`-nographics` 下出不了图**(项目已两次实测:`ScreenCapture` 在 batch 抛异常 / UI Toolkit 纹理
不进图集)⇒ 「实际看效果」只能靠**结构化判据**描述。屏打开后的实际形态:

| 情形 | 屏上是什么 |
|---|---|
| 技能 / 品质都够、料位齐 | 一行:`RecipeId`(如 `herb_soup`),可获焦、可确认 |
| 技能不够 / 品质不够 | **那一行根本不存在** —— 不是灰的、不是禁用的 |
| **全部不可发起(含今日的空集料位)** | **零行**,`FirstFocusable == null` |

### 三处**必须说清**的粗糙边(否则「效果」会被读成「做完了」)

1. **屏上现在是空的** —— `BootRoot` 传 `Array.Empty<int>()` 品级集 ⇒ 全部配方不可发起 ⇒ 零行。
   实测:20 的**选中态接口不存在**(`grep -rn "ConsumedQualities\|SelectedQualities"` ⇒ 生产零命中;
   `InventoryContainerScreen` 只读不选)⇒ 料位接不上。**这是诚实降级,不是 bug**。
   **你现在实机点 `Utensil` 会看到一个空白纸面。**
2. **文案是键名不是中文** —— 21a 的 `Recipe` **无 `DisplayName` 字段** ⇒ 显示 `herb_soup`
   而不是「麻黄汤」。归 21a 显示名轮 / 42 文案轮。
3. **无起货(完成态)呈现** —— 规则八说完成是纯派生、起货由 20 从 `Craft` 派生。
   本屏**不画进度**(承 `skeuomorphic-ui.md:1259` 已裁「凡不发生在纸上的进程,进度归世界器物状态」)。

### 结论:甲案**结构合理**,但 `Utensil` 的「能真的煎一剂药」**仍未达成**

`TD-interaction-03` 的消解条件逐字 =「每种至少有一个消费者接通」。本轮 `Utensil` 有了
**消费者**(屏 + 意图出口),但**上游供料**缺位 ⇒ 屏恒空。⇒ 债转 **`closed`** 还是 **`partial`**,
取决于「接通」读成「有消费面」还是「端到端可用」:

- **读「有消费面」** ⇒ `closed`(选中 → 开屏 → 有行可点,链路在)
- **读「端到端可用」** ⇒ **`partial`**(屏恒空,玩家看不到任何配方)

⚠️ **本轮取 `partial`** —— 不借绿。理由:玩家点 `Utensil` 看到空白纸面,**那不叫接通**。

---

## 七、证明不了的事(禁借绿)

1. **不测点火真的落流** —— 那要 20 的 `Apply`(归 S20-14)+ 18 的五步事务接线轮。
   本件的出口是**测试内注入的 spy sink**,只证「意图到了」,不证「事务跑通」。
2. **不测视觉** —— 无 `design/ux/processing-18.md` spec ⇒ 排版 / 文案 / 焦点序**均未经
   `/ux-review`**。`AC-42-F1` 闭集走查从 8 屏变 9 屏,**第九员记 `NOT-RUN`**。
3. **不测手柄焦点导航** —— 那要真 `EventSystem` + 虚拟 Gamepad。本件只测「行可获焦」与
   「`FirstFocusable` 指向首行」。
4. **`AC-18-10b` 的人眼半边未执行** —— 本件证的是**结构**(行建不建),不是「看起来对不对」。

---

## 八、本轮如实登记的过程事故

| # | 事故 | 处置 |
|---|---|---|
| 1 | `FurnaceBusyQuery` 首版放 `Gameplay.Boot` ⇒ `test_ac1817b_saveComposerReferencesNoProcessingTypes` 判红 | **移回 `Sim`**(正确落点,不是改门) |
| 2 | 配方源首版写成值属性并在装配期求值 ⇒ **24 条红**(装配时序被改变) | 改为 `Func<RecipeDataSet>` 惰性 |
| 3 | `Recipe.RecipeId` 是 `string` 不是 int;`Ordinal` 才是 int;无 `DisplayName` 字段 | 全改 `Ordinal`;文案用 `RecipeId` 并登记粗糙边 |
| 4 | `RecipeEntry` 用 `ItemKey` 不是 `ItemOrdinal` | 按 `new ItemKey("herb_a", ProcessingState.Raw)` 构造 |
| 5 | `OpenModal` 返回 `bool` 不是屏实例 | 屏走 `_uiDriver.Screen` 属性 |
| 6 | `SimTickDriver.Tick` 不存在,真名 `CurrentTick` | 改正 |
| 7 | 写了一个不存在的 API `_host.parentIsSet()` | 改为 `_paper == null` 首建判据 |
| 8 | **漏改第三处闭集门** —— 闭集基数断言共**三处**(`dto_boundary_and_modal_test` · `modal_gate_test` · `dentch_input_test:401`),前两处改了、第三处漏了 ⇒ **PlayMode 全量首轮 `failed=1`**(`test_modalId_closedSetIsNonePlusSeven`) | 补齐;并**全库 grep `Enum.GetNames/GetValues(typeof(ModalId))`** 确认无第四处。**教训:闭集增员的同步面必须 grep 全部基数断言,不能只改记得住的那几处** |
