# 实现原件 —— `AC-4-22`(开档门 / 柜可交互)

> **对象**:`design/gdd/interaction-system.md` `AC-4-22`
> **批次**:`S23-17`(sprint-23 追加;用户指令「修 AC-4-22(门柜可交互)」)
> **截至时刻**:`2026-11-02 11:47 CST`(三元组见 §六)· **commit**:`43f1d2f3`
> **结论**:门与柜**均接通到真实消费面**;`AC-4-22` 的「可被选出并路由」在 **EditMode 的真装配**
> (真 `World` + 真 `StructureWriter` + 真驱动器)上可证。**未跑**实机按 E 的走查(见 §七.1)。

---

## 一、勘察结论(先于改动)

`AC-4-22` 逐字要求「诊所**开档自带**的门与柜(23 的 `BakedInitial`,**无任何流事件**),
`WHEN` 玩家站到其格旁按交互,`THEN` **可被选出并路由**」。本轮之前,四个事实各自成立且互相独立:

| # | 事实 | 复算 |
|---|---|---|
| ① | `CandidateSources.BakedInitial` 在生产装配里恒 `null` | `grep -rn "bakedInitial: null" unity/Assets/Gameplay.Boot/CompositionRoot.cs`(改前 1 命中) |
| ② | `IBakedInitialSource` 只有接口 + 测试替身,零生产实现 | `grep -rn ": IBakedInitialSource" unity/Assets --include=*.cs \| grep -v /Tests/`(改前零命中) |
| ③ | **门没有数据行**:`build_modules.json` 只有 Bed/Table/Cabinet/Decor/Shell/Tent;门槽不存在 | `python3 -c "...Counter(m['module_type']...)"`(改前无 Door) |
| ④ | **柜没有载体**:GDD 定 `Container` 的 `StableId = instance_id`(「容器本身是一件掉落」),而 20 今日**没有**「这是容器吗」的判据载体 | `design/gdd/inventory-and-items.md:595-597` 自陈 |

⇒ 不是「少接一根线」,是**数据 + 载体 + 装配**三件同时缺。

---

## 二、两项用户裁定(本轮的边界)

| # | 问题 | 用户裁定 |
|---|---|---|
| 1 | 门是否影响可通行性(而不是纯表现)? | **C:门影响可通行性,状态进世界流**(可重放 / 可存档) |
| 2 | 柜用哪种模型? | **B:柜 = 容器物品实例**(`instance_id`,忠于 GDD 规则二) |

---

## 三、改了什么

### 3.1 门(结构件 + 状态进流)

| # | 文件 | 变更 |
|---|---|---|
| 1 | `Sim/World/BuildSlotCatalog.cs` | `SlotType.Door = 8`(不复用 `Shell`:可通行性随开合变) |
| 2 | `assets/data/build_modules.json` | 模块 11 = `Door`(单格足迹、`furn_type = null`、`refundable = false`) |
| 3 | `Editor.Tools.Level/P0ClinicLevel.cs` | 门槽 `(5,0,1)`(墙线末端,`baked_initial = true`,模块 11) |
| 4 | `Sim/World/World.cs` | **门格先于占用表分流**:门格走 `¬门关`,不参与「占用 = 挡住」;新增 `BaselineModuleCatalog` 只读出口 |
| 5 | `Gameplay.Boot/DoorInteractionDriver.cs` | **新**:`Toggle(structureId)` = 读 `Variant` 取反 → `StructureWriter.Modify`(**只接受 `SlotType.Door`**) |
| 6 | `Gameplay.Boot/InteractionPressRouter.cs` | `case Door` → toggle;`case Container` → 开容器屏 |
| 7 | `Gameplay.Boot/BootRoot.cs` | 装配 `DoorInteractionDriver`(world + writer 齐才建)并接入路由 |
| 8 | `assets/data/world_buildslots.json` + 两份 cooked | 关卡工具重导 + 重烘(15 槽) |

**门的语义(本轮就地裁定,未经 UX 轮复审)**:
- 开 = `Variant 1`,关 = `Variant 0`(**基线初值 = 关**)。
- 状态**只有一处**(`StructureInstance.Variant`);切换**不新开 Kind**,走既有的 `StructureModified`
  ⇒ 重放 / 存档 / ADR-033 的 id 平移全部自动覆盖。**不发明第二个门状态字段**。
- ⚠️ **门格必须在占用表之前分流**:基线应用与放置都会把门格标为「已占用」,
  若先判占用,门格**永远不可走** ⇒ 开 / 关在可通行性上零差异(本轮的全部意义消失)。
  这条是首版实现的真缺陷,已在写夹具前发现并改正。

### 3.2 柜(容器物品实例 · 加性第 8 类关卡数据集)

| # | 文件 | 变更 |
|---|---|---|
| 9 | `Editor.Tools.Level/ContainerSource.cs` | **新**:声明式容器集(id 重复 / 同格两条 ⇒ 构造期拒) |
| 10 | `Editor.Tools.Level/LevelSource.cs` | 追加可选 `containers` 形参(加性,不动既有调用方) |
| 11 | `Editor.Tools.Level/WorldDataSetKind.cs` | `Containers = 7`(**追加在末尾**,序数是取值的一部分) |
| 12 | `Editor.Tools.Level/LevelExporter.cs` | `ExportContainers`(按 id 排序输出;越界 / 不可通行 ⇒ 硬失败) |
| 13 | `Editor.Tools.Bake/WorldContainersBinder.cs` | **新**:五条域门(空表 / 重复 id / 同格 / 越界 / 负 id) |
| 14 | `Editor.Tools.Bake/WorldContainersCookedWriter.cs` | **新**:i64 id + i32 格;行序 = id 升序 |
| 15 | `Editor.Tools.Bake/WorldContainersBake.cs` | **新**:两阶段入口(ConfigVersion = 源内容哈希) |
| 16 | `Editor.Tools.Bake/DataBakeMenu.cs` | 新菜单 + `BakeWorldContainersCore`(batch 可调) |
| 17 | `Gameplay.Presentation/WorldContainersCookedCodec.cs` | **新**:读侧(空表 / 重复 / 越界 ⇒ 启动期硬失败) |
| 18 | `Gameplay.Presentation/DataCookedDisk.cs` / `AddressablesDataProvider.cs` / `DataCorePreloader.cs` | 装载面三处(磁盘兜底 / Addressables / 预载地址表) |
| 19 | `assets/data/world_containers.json` + cooked | 关卡工具重导 + 重烘:`{instance_id: 1, cell: [5,0,5]}` |
| 20 | `Gameplay.Presentation/Interaction/SourceDtos.cs` | `ContainerDto(WorldPos, long InstanceId)` |
| 21 | `Gameplay.Presentation/Interaction/CandidateSources.cs` | `IBakedInitialSource.Containers` |
| 22 | `Gameplay.Presentation/Interaction/BakedInitialSource.cs` | **新**:第四源生产实装(结构基线按 `SlotType` 分类 + 容器透传) |
| 23 | `Gameplay.Presentation/Interaction/CandidateSetLoader.cs` | 容器候选行(`StableId = instance_id`) |
| 24 | `Gameplay.Boot/CompositionRoot.cs` | 装配第四源;容器 id 经 `ReconstructItemHighWater` **抬升高水位** |

**柜的语义**:容器是**开档既有、无流事件**的物品实例(关卡烘焙);它在 `instance_id` 空间里
(不是 `structure_id`),故 `StableId` 与 GDD 逐字一致。**内容(装什么)刻意未做** ——
20 今日没有容器判据载体与搜箱时长(`OQ-4-4` / `OQ-20-11`),写 `contents` 字段就是无人消费的第二真源。

---

## 四、受影响的既有 pin(逐条按「契约演进」重签,不是改期望凑绿)

| 文件 | 原值 → 新值 | 依据 |
|---|---|---|
| `slot_type_vocabulary_test` | 七对 → **八对**;`ClosedSetText` 加 `Door` | 闭集扩员;本条自陈「扩员前跑即红」 |
| `world_buildslots_codec_validation_test` | 上界 `255 → 511`;越界负载 `256 → 512`;新增 Door 位正向例 | 上界**由 `SlotType` 派生**,枚举扩员即上移 |
| `level_source_generalization_test` | 闭集 `7 → 8`;金标准 SHA `3bcb664f… → 91e4e27e…`(1340 → **1426 B**) | 加性第 8 类 + P0 种子新增门槽 |
| `clinic_room_matching_e2e_test` | `build_modules` 行数 `10 → 11` | 模块目录新增 Door |
| `cooked_drift_all_artifacts_test` | `pinnedDeclared 38 → 39` + 登记 `world_containers` | 新产物必须入漂移门 |

---

## 五、证伪轮(9 条变异 · 改→跑→还原同一命令内 · SHA-256 逐字节还原 9/9)

| 变异 | 打什么 | 实测 |
|---|---|---|
| **M1** | 门格不判门(恒走占用表) | 见下 |
| **M2** | 门开合读反(`closed = Variant == 1`) | 见下 |
| **M3** | toggle 永远写「关」 | 见下 |
| **M4** | 去掉「只接受门」的模块守卫 | 见下 |
| **M5** | 第四源不裁基线号段 | 见下 |
| **M6** | 门分类改成 `Decor` | 见下 |
| **M7** | 容器不进候选集 | 见下 |
| **M8** | 容器 id 重复门停用 | 见下 |
| **M9** | 容器行不排序 | 见下 |

| 变异 | 打什么 | 实测红 | 形态 |
|---|---|---|---|
| **M1** | 门格不判门(`if (false && IsDoorCell(…))`) | 1 红:`test_toggle_opensDoor_writesExactlyOneModify` | **exact** |
| **M2** | 门开合读反(`closed = Variant == 1`) | 3 红(closed 挡路 / 开门可走 / 再合挡住) | **超集**(同一分支的三条断言一起红) |
| **M3** | toggle 永远写「关」 | 2 红(开门 / 两次 toggle) | **超集** |
| **M4** | 去掉「只接受门」的模块守卫 | 1 红:`test_toggle_nonDoorStructure_isRejected` | **exact** |
| **M5** | 第四源不裁基线号段 | 1 红:`test_doors_baselineDoor_isIncluded_playerBuiltDoor_isNot` | **exact** |
| **M6** | 门分类改成 `Decor` | 2 红(基线门被漏 / Decor 被当成门) | **超集** |
| **M7** | 容器不进候选集 | 2 红(容器路由 / 同格优先级) | **超集** |
| **M8** | 容器 id 重复门停用 | 1 红:`test_duplicateInstanceId_isRejected` | **exact** |
| **M9** | 容器行不排序 | 1 红:`test_cookedRoundTrip_preservesRowsSortedById` | **exact** |

**结果:9/9 转红,9/9 SHA-256 逐字节还原**(`RESTORE-OK` 全文见运行输出)。
⚠️ **一处如实登记**:`world_containers_bake_test::test_bake_isDeterministic` **不是** M9 的靶
(它比的是同源两次烘焙,不排序也两次相同)—— M9 由排序断言捕获,确定性断言另有判别面
(改编码器字段序会红,本轮未做该变异)。

---

## 六、回归(三元组 + 等式)

| 套件 | 本批 | 基线(`S23-16`) | 等式 |
|---|---|---|---|
| EditMode | `6581 / 6544 / 0 / 0 / 37` | `6554 / 6517 / 0 / 0 / 37` | `+27 = 25(新夹具)+ 1(Door 位正向例)+ 1(漂移门参数化用例 `world_containers`)` ✅ |
| PlayMode | `297 / 293 / 0 / 2 / 2` | `297 / 293 / 0 / 2 / 2` | `+0`(本批无 PlayMode 改动) ✅ |

`@ 2026-11-02 11:45:43 CST`(EditMode)· `11:47:06 CST`(PlayMode)· 批次 **`S23-17`** ·
commit `43f1d2f3`。PlayMode `inconclusive=2` / `skipped=2` 与基线同形。

**复算**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "
import xml.etree.ElementTree as ET
for f in ['unity/Logs/ac422-em-final.xml','unity/Logs/ac422-pm-final.xml']:
    r=ET.parse(f).getroot()
    print(f, {k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})
"
# 期望: EditMode 6581/6544/0/0/37 · PlayMode 297/293/0/2/2
```

**新增用例逐类**(复算:`grep -c` 同口径的 `fullname` 前缀):

```bash
python3 -c "
import xml.etree.ElementTree as ET
r=ET.parse('unity/Logs/ac422-em-final.xml').getroot()
n=[t.get('fullname','') for t in r.iter('test-case')]
for k in ['door_interaction_test','baked_initial_source_test','interaction_press_door_container_test','world_containers_bake_test']:
    print(k, sum(k in x for x in n))
"
# 期望: 5 / 4 / 5 / 11
```

---

## 七、证明不了的事(禁借绿)

1. **实机按 E 的走查未跑**。本轮证据是 EditMode 的**真装配**(真 `World` + 真 `StructureWriter`
   + 真 `DoorInteractionDriver` / 真 `CandidateSetLoader`),不是「玩家站在门前按键」的端到端。
   后者的载体 = PlayMode 启动序件,本轮未新增。
2. **`Switch` / `Utensil` / `ClinicPanel` 仍不可交互** —— 三者无 `SlotType`、无数据行、无消费面
   (18 / 24 的装配轮);`BakedInitialSource` 的对应切片恒空。`TD-interaction-03` 维持 **`partial`**。
3. **玩家新建的门不在第四源里** —— 第四源按定义是「开档既有、无流事件」,玩家放置的门有
   `StructurePlaced`。⇒ 玩家新建的门**今天选不中**(它需要第五个源 / 流侧门分类)。
   登记为残余(§八)。
4. **容器的内容 / 搜箱未实现** —— 20 无容器判据载体(`ItemCategory` 无 `container`),
   搜箱时长未登记(`OQ-4-4` / `OQ-20-11`)。本轮只接「打开容器屏」。
5. **容器 id 的高水位只在开档路径抬升** —— 读档路径的 `ItemInstanceHighWaterCollector`
   仍只扫三流(**未并基线 id**)。今日零真实存档 ⇒ 无可观测后果;第一次发存档前须补。
   归 `ItemInstanceHighWaterCollector` 的 R3 半边。
6. **`world_containers` 是 ADR-022 §三 之外的加性第 8 类** —— ADR 没有为它写过导出契约;
   本轮的合法性依据 = 「契约点名的 7 类一字未改 + 加性扩展」(与多关卡 `_{level_id}` 后缀同一先例)。
   **未**另开 ADR。

---

## 八、本轮新登记 / 应登记的残余

| 项 | 内容 | 建议归属 |
|---|---|---|
| R1 | 玩家新建的门 / 柜不在候选集(第四源只覆盖基线) | 4 的管线后续轮 / ADR 级别裁定 |
| R2 | 读档路径的物品高水位未并基线容器 id | 7a 持久化 · `ItemInstanceHighWaterCollector` 的 R3 半边 |
| R3 | 容器内容 / 搜箱时长 | 20 · `OQ-4-4` / `OQ-20-11` |
| R4 | `world_containers` 无 ADR(ADhoc 加性扩展) | 架构轮(若要长期保留,宜并入 ADR-022 §三) |
