# 实现原件 —— 20 容器屏的内容面(料位供给)

> **对象**:`inventory-items/story-006`(内容面半边)· `TD-interaction-03` 的最后一块
> **批次**:`S23-25`(用户指令「提交。然后接着做要让 Utensil 真的可用,还差料位供给」→ 范围裁定 **乙**)
> **截至时刻 / commit**:2026-11-02 18:55:02 · `S23-25` · `a39ea0b2` + 工作树(读数见 §5.3)

---

## 一、开工前的实测(为什么范围比「料位」大)

用户裁定乙案前的勘察结论:**料位供给的前置是「20 的屏是空壳」**。

| # | 事实 | 复算 |
|---|---|---|
| 1 | 20 屏此前**只建两个纸面容器** —— 零内容、零交互、零 DTO 源 | `git show HEAD:unity/Assets/Gameplay.UI/Skeuomorphic/Screens/InventoryContainerScreen.cs \| wc -l` ⇒ **48**;`… \| grep -c "Label\|focusable\|Bind"` ⇒ **0** |
| 2 | `inventory-items/story-006` 的 AC 全是 `[L]` 且全 `[ ]` | `grep -n "^- \[ \]" production/epics/inventory-items/story-006-medicine-case-ui-walkthrough.md` |
| 3 | 「料位选中态」的归属 GDD **已裁** —— `R14` 逐字「逐实例消耗的交互 + 手柄焦点路径 —— 『点名哪一栈』在二维摆放上如何选中」,归 **42**(方向 = BL-23 ④,无需再裁) | `design/gdd/inventory-and-items.md` 的 R14 行 |
| 4 | `InventoryProjection.InstancesOf(owner)` 的契约是**按 `instance_id` 升序** ⇒ 正合 §5.2 的落位序 | `unity/Assets/Sim/ItemDatabase/InventoryFold.cs:341-343` |

---

## 二、改了什么

| # | 文件 | 变更 |
|---|---|---|
| 1 | `Sim.Contracts/InventoryContainerDto.cs`(**新增**) | `InventoryContainerDto` / `ContainerItemDto` / `CarryBand`(只读数据面)。`CarryLoad` 为 **`long`**(与 20 的 `CarryRatioDto` 逐字一致,禁 int32 收窄回绕);`IsFull` = **20 的谓词结果**由装配层算好传入 |
| 2 | `Gameplay.UI/Skeuomorphic/Screens/InventoryContainerScreen.cs`(**重写**) | 物品格 + 翻页 + 选中 + 四档(此前 48 行空壳 → 现 ~320 行)。**满档只消费 `IsFull`,不自行求值** |
| 3 | `Gameplay.Boot/ContainerSelectionStore.cs`(**新增**) | 料位选中态的唯一持有者(装配层) |
| 4 | `Gameplay.Boot/InventoryLoadService.cs` | 增 `LoadOf(projection, owner)`(按**任意** owner 的载重读面)+ `MinItemWeight()`(`W_MIN` 读面) |
| 5 | `Gameplay.Boot/CompositionRootServices.cs` | 增 `ContainerSelection` / `CarryCap`(后者的 `null` = 未装配,禁默认容量兜底) |
| 6 | `Gameplay.Boot/CompositionRoot.cs` | 装配 store;把品级集回调从**空集**改接 store;捕获 `harvestProvider.CarryCap` 进服务袋 |
| 7 | `Gameplay.Boot/BootRoot.cs` | 容器屏喂 DTO(真载重对 + `IsFull`)+ 接选中出口;炮制屏从 store 取品级集 |
| 8 | `Tests/PlayMode/SkeuomorphicUI/inventory_container_content_test.cs`(**新增**) | 5 条判据 |

### 关键落点判断

**为什么 `ContainerSelectionStore` 住装配层**:20 屏持 ⇒ 关屏后选中态消失;18 屏持 ⇒
42 持游戏状态,破 ADR-013 §9 C3。装配层本来就是跨屏时序的裁定处。

**为什么它「不是游戏状态」**:不进流、不存档、不参与确定性重放 —— 它是**玩家意图的暂存**
(承 BL-23 ④)。关游戏即丢失是**正确**行为。

**为什么 `CarryBand` 由 42 自算而非装配层预先分档**:BL-27 分工逐字「20 出谓词、42 出分档」。
装配层预先分档 = 把分档函数搬进装配层 = 第二处阈值。

**为什么「满档」这一档例外地由装配层给结果**(见 §四 缺陷 C):谓词本体住 `Sim`
(`CarryLoadSolver.IsFullForAnyAddition`),而 `Gameplay.UI` 看不见 `Sim` ⇒ 42 若要自己判满档,
就**只能再写一遍** `load > cap − W_MIN`。那不是「42 出分档」,那是第二份谓词。

---

## 三、判据(5 条)

| # | 判据 | 测试名 |
|---|---|---|
| ① | 落位序 = `instance_id` 升序、每页 ≤12、格可获焦 | `test_ac2020_gridOrder_matchesInstanceIdAscending_andPageCap` |
| ② | 翻页序号对;首/末页铜扣**卸为平整铜片**(非置灰非隐藏) | `test_ac2020_pageTurn_secondPageAndDetentUnload` |
| ③ | 选中:点 ⇒ 选中集变 + 出口收到 + **格带 `selected` 类**;**再点 ⇒ 取消且摘类** | `test_selection_togglesAndReportsToSink` |
| ④ | 满档**只认** 20 的谓词结果(本屏不重算);余下三档单调不增、小箱不退化 | `test_carryBand_fullComesFromNamedPredicate_monotonicAbove` |
| ⑤ | 空箱 ⇒ 零格 + `FirstFocusable == null` + 1 页 | `test_emptyContainer_zeroCells_noFocusTarget_onePage` |

---

## 四、⚠️ 本轮抓到的**五个缺陷**(两个是实现的,三个是我的)

夹具抓到的真实实现缺陷(A/B)、自查出的结构分叉(C)、证伪轮假存活暴露的夹具空洞(D)、
以及**首版对残余的事实判断本身就是错的**(E)。逐条如实登记 —— 五条全部**改的是本体**,
没有一条是靠改断言迁就的。

**缺陷 A(我的断言写错)**:`cap=10, W_MIN=1` 时我断言 `load = cap−1 = 9` ⇒ Full。
实际谓词是 `load > cap − W_MIN` ⇒ `9 > 9` **假** ⇒ NearFull。**GDD `AC-20-18` 逐字**:
「若 `W_MIN > 1`,在 `cap − 1` 处也已『一律放不进』」⇒ `cap−1` 是否 Full **取决于 W_MIN**,
不是恒真。⇒ **实现是对的,断言错了**。已按 GDD 语义改写。

**缺陷 B(实现真的错,夹具抓出来了)**:`cap = 3` 时 `load = 2` 应得 NearFull,
实现返回 **Half**。根因:`nearFullAt = carryCap / 4` **整除** ⇒ `3/4 = 0`
⇒ `slack <= 0` 永不成立 ⇒ **小箱永远到不了 NearFull**。这是「小箱档位退化」形态,不是取舍。
⇒ 改为整数**上取整** `(cap + 3) / 4`。修后 5/5 绿。

⚠️ **这一条证明夹具不是自证摆设** —— 它抓出了一个我没预见到的真实缺陷。

**缺陷 C(结构分叉 —— 我写了两份满档谓词;F8 形态,自查发现)**

首版 `BandOf(load, cap, wMin)` 自己求 `load > cap − wMin`,而**同一判据在生产里早有一份**:

```
grep -rn "IsFullForAnyAddition" unity/Assets --include=*.cs | grep -v /Tests/
```
⇒ 唯一定义点 = `unity/Assets/Sim/ItemDatabase/CarryLoad.cs:356`
(`CarryLoadSolver.IsFullForAnyAddition`,注释里逐字写着「禁简化成 `load == cap`」——即 BL-26 的那条)。
两式**逐字相同**,于是夹具全绿、行为一致 —— 这正是 `coding-standards.md` **F8** 描述的形态:
「两份判据今天碰巧同值,任一侧改口径就静默分叉,而断言因行为一致抓不住」。

⇒ 改为:谓词**只在一处求值**(装配层调 `CarryLoadSolver` 本体),结果经
`InventoryContainerDto.IsFull` 交给 42;`BandOf` 签名从 `(load, cap, wMin)` 变为
`(load, cap, isFull)`,**满档不再由本屏计算**。夹具 ④ 相应改成两条:
(a) 委派判别(`isFull=false` + `load==cap` ⇒ 不得 Full);
(b) **与真谓词逐点联动的扫描**(cap ∈ {10,3,60,7} × wMin ∈ {3,1,1,2} × 全 load 域),
两处口径一旦分叉即红。

**缺陷 D(证伪轮 M3 假存活 —— 夹具漏了 `selected` 类)**

第一轮实跑的 `M3` 变异(`ApplySelectedClass` 只加不删)**全绿逃逸**。归因:测试 ③ 只断言
「选中集」与「出口收到」,**没有任何断言看格上的 `selected` 类** —— 而那个类是玩家**唯一**
能看见「这一栈被点了」的东西(§11 品性通道走质地/药签,选中走类)。

⇒ 补 5 条类断言(选中后有、未选时无、取消后摘掉、取消第 0 格不连带清第 1 格)。
⚠️ 类**属于结构面**、`-nographics` 下可读,不是视觉判据 —— 补它**不**越过「不测视觉」的边界。

**缺陷 E(首版对残余的判断本身是错的 —— 事实订正)**

首版 §六 写「载重对今日传 `(0,0)`,因为 `InventoryLoadService` 没有暴露载重读面」。
**这个理由不成立**:`CarryLoadSolver.Sum` 早在生产里跑
(`HarvestWorldQuery.cs:112` / `InventoryTransaction.cs:220`),`InventoryLoadService.Rebuild`
也算载重。真实的缺口是两件更小的事:① 没有**按任意 owner** 的读面(只有
`InventoryLoadResult.UnclaimedLoad`,是 `NoOwner` 一侧);② 容量真值没进服务袋。
两件本轮均已补(见 §二 #4/#5/#6)。⇒ **残余消失,不是被绕过。**

---

## 五、回归与证伪

### 5.1 定向夹具

```
Unity -batchmode -projectPath unity -runTests -testPlatform PlayMode \
      -testFilter DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI.inventory_container_content_test \
      -testResults unity/Logs/inv20-verify.xml -nographics
```
⇒ `total 5  passed 5  failed 0`(2026-11-02 · `-nographics`)。

### 5.2 证伪轮(5 条变异,**全红**;SHA-256 逐字节还原 5/5)

脚本:`falsify_inv20.py`(每轮「改 → 跑 → 还原」**同进程**,`finally` 内回写原文并逐字节比对)。

| 变异 | 打的是什么 | 结果 |
|---|---|---|
| `M1` | `int start = _page * CellsPerPage` → `= 0`(忽略页号) | **红** `test_ac2020_pageTurn_secondPageAndDetentUnload` |
| `M2` | `if (isFull) return Full` → `if (carryLoad >= carryCap) return Full`(**自己重算满档**) | **红** `test_carryBand_fullComesFromNamedPredicate_monotonicAbove` |
| `M3` | `selected` 类**只加不删** | **红** `test_selection_togglesAndReportsToSink` |
| `M4` | 铜扣 `canPrev/canNext` 恒 `true`(永不卸形) | **红** `test_ac2020_pageTurn_secondPageAndDetentUnload` |
| `M5` | 切点整除 `cap/4`、`cap/2`(小箱退化) | **红** `test_carryBand_fullComesFromNamedPredicate_monotonicAbove` |

`FINAL-OK`(汇总 SHA-256 == 基线)· `SUMMARY failures=none` · 退出码 `0`。

⚠️ **两轮才走到全红,两处假存活都如实登记**:
- **首轮 `M2` 假存活** —— 当时 `wMin` 硬编码为 `1`,`load > cap−1` 与 `load >= cap` **完全等价**
  ⇒ 断言零判别力。**它暴露的是真缺陷**(硬编码违反「gameplay 值须数据驱动」,且让具名谓词
  不可测),故当轮改的是**实现**(参数化 `wMin`),不是改夹具迁就。
- **首轮 `M3` 假存活** —— 夹具**没有断言看 `selected` 类**(见 §四 缺陷 D)。
- 首轮 `M2` 参数化后的 `wMin`,后来又被 §四 缺陷 C 的整体重构吸收(`wMin` 从本屏移走,谓词归 20)。
  ⇒ 两轮之间实现变了两次,**每次都是本体变、夹具随之重钉**,没有一次是「改期望凑绿」。

### 5.3 全量回归

**截至时刻 · 批次 · commit**:2026-11-02 · `S23-25` · `a39ea0b2` + 工作树。

| 平台 | 读数 | 时刻 | 基线(`S23-24`) | 等式 |
|---|---|---|---|---|
| EditMode | `6607 / 6570 / 0 / 0 / 37` | 18:54:20 | `6607 / 6570 / 0 / 0 / 37` | `+0` |
| PlayMode | `311 / 307 / 0 / 2 / 2` | 18:55:02 | `306 / 302 / 0 / 2 / 2` | **`+5` = 新增夹具文件 `inventory_container_content_test.cs` 的 `[UnityTest]` 计数** |

⚠️ **这两行是**在本件**全部改动落盘后重跑**的读数。此前 18:49/18:50 那一轮跑完之后,
我还订正了 `BootRoot` 的两处**注释**(遗留的「传 `(0,0)`」措辞)⇒ 旧读数对应的树与最终树不同,
按「数字须带三元组」纪律**作废重跑**(18:54/18:55),不拿旧数字冒充。

三元组为「total / passed / failed / inconclusive / skipped」。`PM_EXIT=2` 是本仓 PlayMode 常态
退出码(判据取 XML 的 `failed` 计数,不取退出码 ⇒ `failed=0`)。

等式复算:
```bash
grep -c "\[UnityTest\]" unity/Assets/Tests/PlayMode/SkeuomorphicUI/inventory_container_content_test.cs   # 期望 5
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/reg-pm-s2325.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'))"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/reg-em-s2325.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'))"
```

---

## 六、证明不了的事(禁借绿)

1. **`AC-20-10`(满溢感可读,禁数字)/ `AC-20-11`(零状态播报音)/ `AC-20-23`(色盲非颜色通道)**
   三条 `[L]` **仍 `NOT-RUN`** —— `-nographics` 下无像素,听测 / 色盲 / 目视均未执行。
   本件只覆盖**结构半边**(格 / 翻页 / 选中 / 档位函数)。
2. **载重对已接真值,但只在真 provider 下成立** —— `CompositionRootServices.CarryCap == null`
   (夹具假 `IDataProvider` / 未装配)⇒ 仍传 `(0, 0, false)`,四档恒 `Empty`。
   `AC-20-13` 明令禁 null 兜底与默认容量 ⇒ **不拿默认 cap 冒充**。这是诚实降级
   (`BandOf(0,0,false)` = Empty 与空箱视觉一致),不是假四档。
   ⚠️ **本件证明不了「生产装配下四档会真的变化」** —— 那需要真 provider + 真存档,
   `-nographics` 下没有这条链路。**登记为 `NOT-RUN`。**
3. **「箱内」与「身上」今日不可分** —— 投影没有「在哪个容器里」这一维 ⇒ `InstancesOf(actorId)`
   返回玩家名下**全部**实例,不只该箱内的。残余归 20 的容器语义轮(`OQ-20-11`)。
4. **器物文案是序号不是名称** —— `器物#N·品M`,21a 显示名未落地(与 `Recipe` 无 `DisplayName` 同款)。
5. **不测手柄焦点导航** —— 那要真 `EventSystem` + 虚拟 Gamepad;本件只测「格可获焦」位。

---

## 七、本轮如实登记的过程事故

| # | 事故 | 处置 |
|---|---|---|
| 1 | `InventoryContainerDto` 是 `readonly struct`,写了 `container == null` ⇒ CS0019 | 删掉;null 检查归 DTO 构造器(`Items == null` ⇒ 抛) |
| 2 | `InventoryProjection` / `InventoryRow` 的命名空间是 `DaYiJingCheng.Sim`,不是 `Sim.ItemDatabase` | 改正两处 using / 全限定名 |
| 3 | **注释里写了「排序」二字** ⇒ `AC-42-F3` 词门判红(该门扫 `Skeuomorphic/**/*.cs` **全文含注释**) | 改「次序 / 定序」;**连说明该禁词的那句也绕开了**(禁词本身在注释里也算命中) |
| 4 | 夹具 `④` 两处断言与 GDD 语义不符 | 见 §四(缺陷 A 是断言错、缺陷 B 是实现错) |
| 5 | **本件与 sprint 记录里写「改前 60 行」** —— 实测 **48 行**,且未给复算命令 | 按「数字须带可跑复算命令」改正,补 `git show HEAD:… \| wc -l` ⇒ 48 |
| 6 | **把「没有读面」当成了「没有实现」** | 见 §四 缺陷 E;`CarryLoadSolver.Sum` 早就在生产里跑,我少查了一层 |
