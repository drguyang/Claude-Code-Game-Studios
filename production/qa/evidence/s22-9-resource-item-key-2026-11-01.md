# S22-9(第一半)· 资源点导出契约补 `item_key`(schema 1 → 2)

| 字段 | 值 |
|---|---|
| **卡** | sprint-22 `S22-9`(甲案「补接线」的前置半) |
| **起因** | 采前闸算不出 `qty` 与单件重量 ⇒ `CanCarry` / `CanHarvest` 两侧**都没有输入** |
| **执行轮** | 2026-11-01 · commit `e69016b5` + 本批未提交改动 |
| **判据载体** | `EditMode/LevelTool/poi_and_resource_load_test.cs`(+2)等 4 个 LevelTool 夹具 |

---

## 1. 病因(可复算)

`design/gdd/foraging.md:148` 把资源节点定义写死为四元:

> `node_id` · `ecosystem` · **`item_key`(品种)** · `cell`

而导出产物 `assets/data/world_resources.json` 的行只有 `resource_id / cell / ecozone_id`:

```bash
git show HEAD:assets/data/world_resources.json | head -8
# {"resource_id": 1, "cell": [2, 0, 2], "ecozone_id": 0}
```

⇒ **GDD 已裁的字段,导出契约里根本没有**。后果不是「少个字段」:
`qty_per_node` 与单件重量都挂在**品种**上(`item_database_items.json` 的 `gather_profile`
与 `weight`),没有品种 ⇒ 采前闸的两侧(`CarryLoadSolver.CanCarry` / `NodeRemaining.CanHarvest`)
**同时无输入**。

全库既无 `resource_id → item_key` 映射表,也无生态区 → 品种表
(`world_ecozones.json` 只有 `clinic_ward` / `herb_courtyard` 两个**房间**语义区)。

## 2. 逐层改动(6 层,全部同批)

| 层 | 文件 | 改动 |
|---|---|---|
| 作者态 | `assets/data/world_resources.json` | 每行 +`item_key`;**schema_version 1 → 2**(由关卡工具导出,不手写) |
| 关卡工具 | `Editor.Tools.Level/ResourceSource.cs` | `ResourceSpec` +`ItemKey`(**必填**,`< 0` ⇒ 构造抛) |
| 关卡工具 | `Editor.Tools.Level/P0ClinicLevel.cs` | 四行资源点补品种;并把**帐篷槽**正式产出(见 §3) |
| 关卡工具 | `Editor.Tools.Level/LevelExporter.cs` | `ResourcesSchemaVersion = 2u`;行尾写 `item_key` |
| 烘焙 | `Editor.Tools.Bake/WorldResourcesBinder.cs` | 白名单 +`item_key`;**必填**,缺失/为负 ⇒ 拒(不给缺省值) |
| 烘焙 | `Editor.Tools.Bake/WorldResourcesCookedWriter.cs` | 行 20 → **24 B**(行尾补 `i32 item_key`) |
| 运行期 | `Gameplay.Presentation/WorldResourcesCookedCodec.cs` | `ExpectedSchemaVersion = 2u`;读 `item_key` 并入 `WorldResourceRow.ItemKey`;`< 0` ⇒ 拒 |

**编码口径 = 裸 ordinal(21a 物品表)**,与 `build_modules.json` 的 `build_item_key` 同族:
运行期 `ResourceHarvested.item_key` 本身就是 ordinal,存字符串会在每个消费处多一次解析,
且解析表 = 第二真源。

**品种取了什么(临时内容,归关卡设计/数值轮)**:resource 1 → 柳树皮(`willow_bark`,ordinal 0),
resource 2 → 毛地黄(`foxglove`,ordinal 2)。**只证明「有这个字段且可解」,不主张物种分布定稿。**

## 3. 同批抓到并修掉的：帐篷槽其实是**手写进 JSON** 的

S22-8 落帐篷时,`world_buildslots.json` 的那一行是**直接编辑产物文件**写进去的 ——
违反 ADR-022 §二「关卡工具是唯一作者」。本轮跑真导出即暴露:导出器只产出 13 槽,
重导会**静默删掉**帐篷。

已修:`P0ClinicLevel.Define()` 正式产出 `(12,0,12)` 的 `SlotType.Tent` 槽
(`TentCellX/TentCellZ` 常量 + 一段具名注释)。**再导出后 `world_buildslots.json` 逐字节不变**
(`sha256 3bcb664f…`)⇒ 手写值恰好等于工具产出,但现在是**可复现**的。

## 4. 三条哨兵当场转红(已按「契约演进」逐条处置)

| 哨兵 | 症状 | 处置 |
|---|---|---|
| `level_source_generalization_test.test_ac1_canonicalLevel_keepsItsSixteenByOneBySixteenSize` | 期望 13 槽、实得 14 | 13 → **14**,报文改「9 Decor + 4 Shell + 1 Tent」 |
| `level_tool_consistency_test`(同款计数) | 同上 | 同上 |
| `level_source_generalization_test.test_ac3_newDispatchPath_bytesEqualPreRefactorGolden` | 金标准 SHA-256 不符(1249 B → 1340 B) | **按该常量自身的纪律重签**(它写着「P0 种子合法变更 ⇒ 与产物同批重签」),新值 `3bcb664f…`,并在注释里具名登记「这是内容变更,不是重构回归」 |

## 5. 回归(三元组)

**截至 `2026-11-01 10:41:50 CST` · 批次 `S22-9` · commit `e69016b5` + 未提交工作树**

| 平台 | 本次 | 前次(sprint-22 收口) | 差 | 等式 |
|---|---|---|---|---|
| EditMode | `6347 / 6310 / 0 / 37` | `6345 / 6308 / 0 / 37` | **+2** | `6347 − 6345 = 2` = 新增 2 条(`missingItemKey_isRejected` · `exportSchemaVersion_isTwo`)✅ |
| PlayMode | `273 / 269 / 0 / 2 / 2` | 同 | 0 | 零变化 ✅ |

`error CS` = **0**。中间一次**非终态**跑(`6347 / 6307 / 3`)正是第 4 节的哨兵,修后复跑全绿。

## 6. 这一半解决了什么 / 没解决什么(**下一半的入口**)

**解决**:资源点现在带品种 ⇒ `qty`(`qty_per_node × GatherMul(level)`)与单件重量**可算**。

**没解决(三条运行期通道,全部实测为零)**:

```bash
grep -rn "new ForagingConstants(" unity/Assets --include=*.cs | grep -v /Tests/ | wc -l   # 0
ls unity/Assets/DataCooked/inventory_constants.cooked.bytes                                # 不存在
grep -c "quality_distribution" assets/data/item_database_items.json                        # 0
```

| # | 缺什么 | 影响 |
|---|---|---|
| ① | **`ForagingConstants` 运行期实例** —— `foraging_constants.cooked.bytes` **产物在**,但**无 loader**(全仓 `new ForagingConstants(` 生产 = 0) | `CanHarvest` / `HarvestQty` / `OutQuality` 全部要它 |
| ② | **`CARRY_CAP` 运行期通道** —— `inventory_constants.json` 无 cooked 产物;生产 `new CarryCapacity(` = **0**(唯一一处是编辑期门 `CarryCapExistenceGate`) | `CanCarry` 要它 |
| ③ | **`quality_distribution` 数据通道** —— 21a `gather_profile` 里**没有**该字段,`QualityDistributionShape.cs` 文件头已登记「数据通道全库不存在」 | 成功路径的最后一块:没有分布就算不出 `out_quality` |

⇒ **采前闸要接通,还差 ①②;采集动作要能完成,还要 ③。** 三处都是**数据管线**改动,
不是接线手势 —— 已在 sprint-22 收口判定记录的 Exit 缺口下顺延为本卡的下一半。

## 7. 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "import json;d=json.load(open('assets/data/world_resources.json'));print(d['schema_version'], [r['item_key'] for r in d['resources']])"   # 2 [0, 0, 2, 2]
ls -l unity/Assets/DataCooked/world_resources.cooked.bytes    # 120 B = 20 头 + 4 count + 4×24
sha256sum assets/data/world_buildslots.json                   # 3bcb664f… （= 金标准常量）
python3 -c "import json;d=json.load(open('assets/data/world_buildslots.json'));print(len(d['slots']))"   # 14
```
