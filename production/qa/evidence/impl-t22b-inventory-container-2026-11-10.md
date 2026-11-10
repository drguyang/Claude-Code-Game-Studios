# T22 批二 —— 药箱容器屏(`inventory-006`)证据原件

**日期**:2026-11-10
**批次**:T22 批二(用户「开工 T22 批二」;本批 = 丙 · 药箱容器屏)
**构建**:Development,`PlayerBuildCli.BuildDevelopment` 两次 `Result: Succeeded`
  (第一次 24.6 s / 第二次 26.1 s;第二次是为了验「补壳」后的真包形态)
**运行**:`DISPLAY=:0`,1280×800,`-tutorialdiag`;存档根走查前移开、走查后原样还原

---

## 1. 本批之前,这个屏是什么状态(实测,不是推断)

sprint-26 把 `inventory-006` 定为 Should Have、状态 `Ready`,而屏本体 `InventoryContainerScreen`
2026-11-02 就写好了 —— 于是先量了一遍它**在生产上到底缺什么**:

| # | 缺口 | 可跑判据 |
|---|---|---|
| ① | **屏没有壳**:格区与翻页两块纸面**直接挂在面板根上** | 真包按 `E` 开箱 ⇒ 屏上是**两条横贯全屏的空白纸带**(`b-02-box.png`),与 `TD-skeuo-11` 记过的设置壳同型 |
| ② | **`Band` 算了但没有一处呈现** | `grep -n "Band" InventoryContainerScreen.cs` ⇒ 只有计算与属性,**零 `AddToClassList`、零元素** ⇒ AC-20-10 的「满溢感可读」**没有对象** |
| ③ | **分档函数与已批准 spec 不符** | `inventory-container-20.md` §6 是逐档整数式;实现自造了 `ceil(cap/4)` 那道界 ⇒ `cap=10 / load=6` 实现报 `Half`,spec 报 `NearFull` |
| ④ | **`cap = 0` 且 `load > 0` 会报 `NearFull`** | 旧式子 `slack = -5 ≤ ceil(0/4)=0` ⇒「容量不知道」被报成「快满了」。生产侧传的是 `(0,0,false)` 所以看不见,但没有守卫 |

⇒ 本批把四件一起修掉,并补上 `carry_tier_monotonicity_test`(QA 计划 §S26-5 点名的那件,此前**不存在**)。

---

## 2. 落地

| 层 | 文件 | 改了什么 |
|---|---|---|
| 屏壳 | `InventoryContainerScreen.cs` | 补 `.pause-column` 列宽 + `.paper-page` 页框 + 标题「药箱」;格区 / 翻页住页里(内层 `item-grid-paper` 仍是 `.paper`,不套双层框) |
| 分档 | 同上 `BandOf` | 逐字用 §6 的整数式:`isFull → Full` · `cap==0 → Empty` · `load<=0 → Empty` · `2×load ≤ cap → Half` · 否则 `NearFull` |
| 四档呈现 | 同上 `ApplyBandGeometry` | **只走几何**(§6 明禁颜色梯度):格间隙 6 / 3 / 0 / −3 px;近满起出「沿口」;满档出「半盖」(顶住,不是盖满) |
| 容器柜可见 | `StructureVisualFactory` / `StructureVisualSpawner` / `CompositionRoot` | `world_containers` 的柜也出体块(深木色、柜形);与槽位**并存**不是替代 —— `TD-building-05` 的病句点名了「柜」,而容器**不在**建造槽位表里 |

**一条纪律落点**:`ApplyBandGeometry` **不读载重**,只读 `Band`;而 `Band` 的唯一来源是 `BandOf`
(BL-27:20 出谓词、42 出分档)。若在那里再算一次 `slack`,就是被禁的第二份判据。

---

## 3. 判据

### 3.1 `carry_tier_monotonicity_test`(EditMode,8 条——QA 计划点名的文件,此前不存在)

| # | 测试 | 钉的性质 |
|---|---|---|
| 1 | `test_band_isMonotonicNonIncreasing_inSlack` | 载重↑ ⇒ 档位只往更满走;并带**正控**(扫描必须真的跨过 ≥2 档,否则空真) |
| 2 | `test_band_boundaries_withCapTwelve_areLiteral` | 边界值**字面写死**(0/1/6/7/11/12),不照抄实现的常量 |
| 3 | `test_band_full_comesOnlyFromPredicate` | 谓词说不满 ⇒ 42 **不得**自报 `Full`(BL-27;F8 守门) |
| 4 | `test_band_smallCap_reachesNearFull_notStuckAtHalf` | 小箱不因整数比例写法退化 |
| 5 | `test_band_capZero_isEmpty_degradationNotFull` | 容量真值缺席 ⇒ 恒空档(`cap=0, load=5`) |
| 5′ | `test_bandGeometry_fourBandsDifferInGeometry_notInColour` | 四档在屏上**几何可辨**:间隙单调、沿口/半盖只在对应档出现 |
| 5″ | `test_bandGeometry_survivesPageTurn` | 翻页后几何仍在(带「真的翻到了第 2 页」前置断言) |
| 6 | `test_band_negativeInputs_throwLoud` | 负载重 / 负容量必须抛 |

### 3.2 `structure_visual_spawner_test` 新增 2 条(容器柜)

| # | 测试 | 钉的性质 |
|---|---|---|
| 7 | `test_spawnContainers_createsCabinetAtCell_nextToSlots` | 柜体与槽位体块**并存**;贴地;形状不与装饰件同 |
| 8 | `test_realArtifact_oneContainerCabinet` | 真产物 1 个容器 ⇒ 1 个柜体 |

### 3.3 既有 PlayMode(未新增,回归绿)

`inventory_container_content_test` **7/7** —— 其中翻页 / 焦点按 `instance_id` 还原 / 满档谓词三项
就是 AC-20-20 的逻辑腿;本批补壳后仍全绿。

---

## 4. 真包走查

| 判据 | 结果 | 证据 |
|---|---|---|
| 容器柜有体块 | ✅ 日志 `[结构视觉] 槽位体块 9 个 + 容器柜 1 个` | `p2.log` / `p3.log` |
| 走到柜前 `(5,0,5)` | ✅ `[tutorialdiag] cell=(5,0,5)` | `p3.log` |
| `E` 开药箱 → **屏壳正常** | ✅ 页框 + 标题「药箱」+ 格区 + 翻页条 | `b2-01-box.png` / `crop-box2.png` |
| 补壳前对照 | ❌ **两条横贯全屏的空白纸带** | `b-02-box.png` / `crop-box.png` |
| 零异常 | ✅ `grep -ac "Exception:"` = **0** | `p3.log` |

⚠️ **真包只看到「空箱」一档**(新角色身上零器物)。四档之间的差异由 §3.1 的 5′/5″ 两条
EditMode 判据覆盖(逻辑腿),**真包的另三档未跑**。

---

## 5. 证伪轮(2026-11-10 · 还原 = SHA-256 逐字节)

| 变异 | 改法 | 结果 |
|---|---|---|
| **M1** | 半满判据 `≤` 改 `<`(界上差一) | **1 红**:`boundaries_withCapTwelve_areLiteral` |
| **M2** | 删 `cap == 0 ⇒ Empty` 早退 | **1 红**:`capZero_isEmpty_degradationNotFull` |
| **M3** | 沿口永不出现 | **1 红**:`fourBandsDifferInGeometry_notInColour` |
| **M4(首枪·未红)** | 几何只在开屏时应用、翻页不重算 | **0 红** —— 见下(原因①) |
| **M4b(二枪·未红)** | 同上 | **0 红** —— 见下(原因②) |
| **M4c(三枪)** | 同上 | **1 红**:`bandGeometry_survivesPageTurn` |
| **M5** | 容器柜一行都不画 | **2 红**:两个容器夹具 |

### M4 打了三枪才红 —— 两次都是**我的判据**的问题,不是变异没打准

| 枪 | 变异 | 结果 | 真因 |
|---|---|---|---|
| **M4** | 几何移出 `RebuildPage` | 0 红 | **夹具只喂 2 件器物 ⇒ 屏只有 1 页** ⇒ `TurnPage(+1)` 在 `target >= _pageCount` 处早退,`RebuildPage` 根本不跑。断言是**空洞正控**(假绿形态 **F4**)。 |
| **M4b** | 同上(夹具已改喂 13 件 ⇒ 2 页) | 0 红 | 断言读的是 **`CellGapPx` 这个缓存字段** —— 变异只搬走了「把值写到元素上」那一步,字段照旧保留 ⇒ **自指判据**(**F3**):测的是「算过」,不是「画上了」。 |
| **M4c** | 同上(断言改为读**元素渲染值** `CellAt(0).style.marginTop`) | **1 红** | 正确转红。 |

修法落在两处:① 夹具改喂 **13 件**,并加两条**前置断言**(`PageCount == 2`、翻完 `Page == 1`);
② 新增 `RenderedGap(screen)` 读**元素样式**而非缓存字段,四档几何那条也一并改用它。

**这是本批最有价值的产出**:同一个变异打三次才红,而两次失败都不是「变异打歪」,
是**我的判据本身是空的 / 是自指的**。F4 与 F3 在同一处连发 —— 正是
`coding-standards.md` §假绿形态登记表 要求「写夹具前先扫一遍」的原因。

---

## 6. 回归(三元组)

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7409 / 7372 / 0 / 0 / 37` | `2026-11-10T13:02:33Z`–`13:04:36Z` | T22 批二 | `35d2962e` + 工作树 |
| PlayMode | `376 / 371 / 0 / 2 / 3` | `2026-11-10T13:05:17Z`–`13:05:39Z` | T22 批二 | `35d2962e` + 工作树 |

基线(T22 批一):EditMode `7399 / 7362 / 0 / 0 / 37` · PlayMode `376 / 371 / 0 / 2 / 3`。
**等式**:`7409 − 7399 = 10`(本批新增:`carry_tier_monotonicity_test` 8 条 + 容器柜 2 条);PlayMode **零变化**。

---

## 7. story 状态(诚实口径)

`inventory-006`:**`Ready` → `In Progress`**(不是 `Complete`)。

| AC | 本轮之后 |
|---|---|
| `AC-20-10` [L] 满溢感可读、零条零百分比 | **逻辑腿 PASS**(四档几何 8 条夹具);**人工签核 NOT-RUN**(真包只看到空箱一档) |
| `AC-20-11` [L] 库存音效零状态播报 | **NOT-RUN**(未听测) |
| `AC-20-20` [I] 超一屏手柄遍历 + 同键同目标 | **逻辑腿 PASS**(PlayMode 7 条);**真手柄 NOT-RUN** |
| `AC-20-23` [L] 色盲模拟 + 仅手柄区分品级 | **NOT-RUN** |
| 读数契约(四档形状) | **PASS** —— 本批的 8 条即该项判据 |

⇒ 四条 **[L]/[I] 的人工腿都要真手柄 + 真观感**,本批没有代签。

---

## 8. 遗留(如实登记)

1. **四档的真包另三档未看** —— 新角色零器物,凑到半满 / 近满 / 满需要先采集十几件。
2. **药箱的视觉仍粗糙** —— 格内文字是「器物#序号·品N·叠M」(21a 显示名未落地),页框内没有箱体造型;
   属美术 / UX 轮。
3. **「箱内」与「身上」今日不可分**(`OQ-20-11`)—— 屏画的是**玩家背包投影**,不是柜子里的东西。
   本批**未动**这条,它决定的是「药箱装什么」,不是「药箱长什么样」。
