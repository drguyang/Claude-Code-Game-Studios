# 修复轮原件 —— S20-19 **A-5**(建造扣料归因)2026-10-28

> 判定来源 = `production/qa/evidence/review-s20-19-2026-10-28.md` §5.5「A-5 升级版」+ §七 结论
> (该件余下的**唯一 BLOCK**)。用户 2026-10-28 指令:「处理 A-5:修掉 SolePlayer 赋值」。
>
> 本件与评审原件**分家**的理由:`review-*` 是「判定 + 原判据核账」,本件是「一条 BLOCK 的
> 修复 → 反证 → 回归」的完整闭环(承 `coding-standards.md` §评审报告原件要求:
> 报告须含 **原判定 → 修复落点 → 验证命令**)。

---

## §一 病灶(两条,第二条是修 A-5 时顺带暴露的)

**主病灶(A-5 本体)** —— `CompositionRoot.cs:407`(修前)构造 `InventoryLoadService` 只传
**3 个形参**,第 4 形参 `foldContext` 缺省 `null` ⇒ `InventoryLoadService.Rebuild` 落到
`InventoryFoldContext.None` ⇒ `SolePlayer` 恒 `null` ⇒
`InventoryFold.ApplyBuildCost`(`InventoryFold.cs:944`)每次都从**「归因不可判」出口**返回
⇒ **建造扣料在玩家路径上从不发生**(材料凭空不减,`InventoryLoadResult.IsComplete` 恒 false)。

**连带病灶(修 A-5 时必须一并修,否则修了也没用)** —— `InventoryFoldContext.None` 的三份表
(`Items` / `Recipes` / `Modules`)**全是默认值**。即使把 `SolePlayer` 补上:
- `Modules == null` ⇒ `InventoryFold.cs:953` 仍走**同一条** `BuildCostAttributionUnresolved`
  (报文换成「模块不在目录」)——**扣料仍然发生不了**;
- `Recipes` 空 ⇒ 每条有产出的 `Craft` 报 `CraftRecipeNotInTable`(R15);
- `Items` 空 ⇒ R18 的 `build_part` 判定无源。

⇒ 本轮的修法**不能只做赋值**:三份表 + 归属快照必须一起到位,否则「解掉一条 BLOCK」
只是把缺口换了个报文。

---

## §二 修法 —— 单实例两读者,不是「补一个赋值」

| 要素 | 做法 |
|---|---|
| 归属来源 | `BuildOwnerSlot` **一个实例**,三个读者:`BuildController` / `BuildStockQuery` / fold 的 `SolePlayer` |
| 构造次序 | 槽的构造点上移到 `InventoryLoadService` **之前**(`CompositionRoot.cs:421`)—— `foldContext` 是闭包,按引用捕获 |
| 绑定 | 不变:`BootRoot.cs:369` 在玩家 actorId 铸造后 `build.BindOwner(actorId)`(后置绑定) |
| 未绑定期 | `Owner = -1` ⇒ fold 拿 `null` ⇒ **fail-closed 的显式缺口**(不静默扣错人,也不静默放行) |
| 三份表 | `items` / `recipes` / `modules` 惰性 + **单次解码**;缓存的是**版本化烘焙表**(不是派生态投影 —— 投影每次 `Rebuild` 全量重算) |
| 模块目录 | 声明从 `if (structures != null)` 内提升到同一段(`:427`),**仍是唯一实例**(`:902` 赋值),不新增第二份目录 |

**为什么不是「在 `BootRoot` 里给 fold 塞一个 id」**:那会造出**两个各自绑定的 int** ——
正是 A-5 原判定描述的「库存门按 actor 5 放行、fold 从 actor 0 扣 ⇒ 材料凭空消失」的分叉,
只是从「两个变量」变成「两个来源」。单实例槽让该分叉在**结构上**不可表达。

---

## §三 落点(全部已入库 `85f248e8`)

| 文件 | 位置 | 改动 |
|---|---|---|
| `unity/Assets/Gameplay.Boot/CompositionRoot.cs` | `:421` | `var buildOwner = new BuildOwnerSlot();`(构造点上移) |
| 同上 | `:427` / `:902` | `BuildModulesCatalog modulesCatalog = null;` 声明提升;原 `var` 局部改**赋值**(唯一实例不变) |
| 同上 | `:433` / `:438` | `loadItemsSeed` / `loadRecipesSeed` 惰性单次解码闭包 |
| 同上 | `:442-453` | `InventoryLoadService` 第 4 形参 = `() => new InventoryFoldContext(items, recipes, modules, owner)` |
| 同上 | `:946` 附近 | 原「此处 `new BuildOwnerSlot()`」删除,就地加注指向新落点 |
| `unity/Assets/Gameplay.Presentation/DataCookedDisk.cs` | `:109` | 新增 `LoadRecipes()`(`item_database_recipes.cooked.bytes`) |
| `unity/Assets/Tests/EditMode/Boot/inventory_fold_sole_player_wiring_test.cs` | 全文(2 测) | 装配级回归夹具,见 §四 |
| `tools/falsify/specs/s20-19-a5-sole-player.json` | 2 变异 | 见 §五 |

---

## §四 夹具 —— 为什么必须在这一层

`Tests/EditMode/Boot/inventory_fold_sole_player_wiring_test.cs`:

| # | 测名 | 判据 |
|---|---|---|
| ① | `test_a5_beforeOwnerBound_foldReportsAttributionGapAndDeductsNothing` | 绑定**前**:`gaps` 含 `BuildCostAttributionUnresolved`,且 owner 名下材料**一份不少** |
| ② | `test_a5_afterOwnerBoundViaBuildController_foldDeductsExactlyCost` | 经 `BuildController.BindOwner` 绑定**后**:同类缺口**消失**、无 `BuildCostExceedsHolding`,材料**恰少 `cost(m)`** |

**为什么不在 `InventoryFold` 的单元夹具里测**:fold 的 R16 逻辑本来就对(它老实报了缺口)——
A-5 的病在**装配**。同理,只测 `BuildController` 也抓不到:库存门用的是另一个 int。
**唯一能抓到它的位置 = 装配之后**:同一个 `CompositionRootServices` 里「绑定 → 重建 → 材料是否真少了」。

夹具走**真装配 + 真烘焙产物**(`MinimalWorldFactory.Create()` + `DataCookedDisk` 的
`build_modules` / `items` / `recipes`),**零手搓 `InventoryFoldContext`** —— 手搓就只测到
「我构造的那个上下文」,与装配是否接线无关,正是本件要抓的病。

---

## §五 证伪轮(2 变异,**2/2 exact**)

```
python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/s20-19-a5-sole-player.json \
        --guard unity/Assets/DataCooked --guard unity/Assets/AddressableAssetsData
```

| 变异 | 打在 | 预期红 | 实得 |
|---|---|---|---|
| `M1-solePlayerDropped`(退回 `(int?)null`,即 A-5 原状) | `CompositionRoot.cs:453` | ② `..._foldDeductsExactlyCost` | ✅ **exact**(`total=2 passed=1 failed=1`,RED 恰为该条) |
| `M2-failClosedGuardDropped`(去掉 `IsBound` 守卫,未绑定也当归属) | 同上 `:453` | ① `..._AttributionGapAndDeductsNothing` | ✅ **exact**(`total=2 passed=1 failed=1`,RED 恰为该条) |

- **一属性一条变异**:M1 = 「绑定后扣料」那一面;M2 = 「绑定前 fail-closed」那一面。
  两条打**同一行**、替换不同 —— 该行同时承载两个语义,而两个语义各需独立反证。
- 还原 = **SHA-256 逐字节**(`cade5ba5…` 前后一致),`FALSIFY-` 串残留 `grep` 零命中;
- 受保护路径(`unity/Assets/DataCooked` · `unity/Assets/AddressableAssetsData`)**指纹未变**
  (证伪轮不留痕)。

⚠️ **M2 的补充说明**:去掉守卫后材料**仍不会被扣**(owner `-1` 名下无栈),所以 ① 的
「一份不少」断言**仍绿** —— 转红的是「缺口类别」那一条。这正是 fail-closed 与「扣错人」的
区别,本变异钉的是前者。

---

## §六 回归(三元组)

**截至时刻** `2026-10-28T02:49Z` · **批次** A-5 修复轮全量 · **commit** `85f248e8`(工作树干净)

| 套件 | 退出码 | 实得 | 基线(同带三元组) | 差值 |
|---|---|---|---|---|
| EditMode | 0 | `total=5678 passed=5643 failed=0 inconclusive=0 skipped=35` | `5676/5641/0/0/35` @ `2026-10-28T02:30Z` · S20-20 修复轮全量 · `3ae5599e` + 未提交工作树 | **+2 / +2** |
| PlayMode | 2 | `total=238 passed=234 failed=0 inconclusive=2 skipped=2` | `238/234/0/2/2` @ 同上 | **0 / 0** |

**等式核账**(承 `coding-standards.md` §借绿禁令):`5678 − 5676 = 2` = 本轮新增夹具**恰 2 条**
(`inventory_fold_sole_player_wiring_test` 的 ① ②);通过数同量 `5643 − 5641 = 2`。
PlayMode `exit=2` **不是失败**:`failed=0`,两条 `inconclusive` 是**既有**的
`test_u1assump6_judgement{1,2}_notRun`(S20-20 修复轮同一状态,逐字相同)。

**可复算命令**:

```bash
cd unity && ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode -testResults "$PWD/Logs/a5-full-editmode.xml" \
  -logFile "$PWD/Logs/a5-full-editmode.log"
python3 - <<'PY'
import xml.etree.ElementTree as ET
tc=list(ET.parse('unity/Logs/a5-full-editmode.xml').getroot().iter('test-case'))
print(len(tc), sum(1 for c in tc if c.get('result')=='Passed'))   # 期望 5678 5643
PY
```

---

## §七 本件**证明不了**的事(禁借绿)

1. **「槽里的 id 就是玩家」** —— 夹具里 `Owner = 42` 是**夹具值**。生产上该等式由
   `BootRoot.cs:369` 的铸造序保证(`BindOwner(actorId)` 与 `player.Initialize(…, actorId)`
   同源),**不在本夹具面内**,也无机械门钉住它(候选:将来加一条「BootRoot 绑定点唯一」的
   文本级断言,本轮未做)。
2. **Addressables 路径未覆盖** —— 夹具走 `dataProvider == null` 的 `DataCookedDisk` 兜底;
   `dataProvider != null` 那半(`dataProvider.Load<RecipeDataSet>()`)今天**零夹具**。
   两条路径同源是靠「同一 codec、同一产物」的既有论证,不是靠本件实测。
3. **`Rebuild` 时机的窗口** —— 生产上 `Rebuild` 只在 (a) 建造落流后的 `onCommitted`
   (b) `BuildStockQuery` 首次查询且 `Current == null` 时发生。今天**没有**「绑定前已 Rebuild 且
   此后不再 Rebuild」的路径(`grep -rn "InventoryLoad" unity/Assets | grep -v /Tests/` ⇒
   仅这两处);**将来若加读档后立即 Rebuild,须同时在绑定后补一次** —— 否则存档里的
   `StructurePlaced` 会以 `SolePlayer = null` 折叠一次并被缓存。
4. **联机多玩家(乙案)** —— 载荷补 actor 位那条路不在本件面内(`BuildOwnerSlot` 头注已登记
   「届时本槽作废」)。

---

## §八 判据来源索引

- `review-s20-19-2026-10-28.md` §5.5(原判定)· §五结账表 `A-5` 行(本轮已改)
- `design/gdd/inventory-and-items.md` 规则六 + R16 甲案(2026-10-27 用户裁定:P0 单机扣料恒归唯一玩家)
- `InventoryFold.cs:944-993`(`ApplyBuildCost` 的三条出口)
- `BuildOwnerSlot.cs` 头注(晚绑定槽的存在理由与 fail-closed 口径)
