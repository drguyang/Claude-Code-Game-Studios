# S20-19 实施轮证据原件 —— 42 建造发起方接线(兑现 `O-23-6`)

| 项 | 值 |
|---|---|
| **对象** | `sprint-20` §十 S20-19(`production/sprints/sprint-20.md:677` 起) |
| **日期** | 2026-10-28 |
| **批次** | sprint-20 · S20-19 实施轮(单批) |
| **起始 commit** | `314bf6a`(**工作树含本批未提交改动** —— 本批未 commit,项目规矩:无用户指令不提交) |
| **用户裁定** | 2026-10-28 三项(范围 = 规格 + 逻辑接线 · 建造不占 `ModalId` · 本轮接扣减) |

> 本件是**报告原件**(`coding-standards.md` §Review Evidence Standards):
> 每条判定给出 **原判据 → 落点 → 验证命令**。无原件 ⇒ 不得转 `Complete`。

---

## §一 交付面(8 改 + 6 新建 + 1 资产)

### 新建(生产)

| 文件 | 作用 |
|---|---|
| `unity/Assets/Sim/World/BuildMaterialPlanner.cs` | 规则六 BL-23 兜底序的**唯一实现点**(写前路径与 fold 重建路径共用) |
| `unity/Assets/Gameplay.Presentation/Building/BuildController.cs` | 建造编排器:判定 → 选料 → 落流(**零判据实现**) |
| `unity/Assets/Gameplay.Presentation/Building/BuildStockQuery.cs` | `IStockQuery` 生产实装(判据 = 够 `cost(m)` 份) |
| `unity/Assets/Gameplay.Presentation/Building/BuildOwnerSlot.cs` | 扣料归属晚绑定槽(`BuildController` 与 `BuildStockQuery` **共享同一实例**) |
| `unity/Assets/Gameplay.Boot/BuildInputReader.cs` | 四动作的设备直读(承 `PlayerInputReader` 同构) |
| `unity/Assets/Gameplay.Boot/BuildInputDriver.cs` | 建造模式状态机 + 幽灵朝向循环 |

### 新建(夹具)

| 文件 | 测数 |
|---|---|
| `unity/Assets/Tests/EditMode/ModularBuilding/build_material_planner_test.cs` | **17**(`BuildMaterialPlannerTest`) |
| `unity/Assets/Tests/EditMode/ModularBuilding/build_controller_test.cs` | **16**(`BuildControllerTest`) |
| 同文件内 `BuildStockQueryTest` | **5** |
| **合计** | **38** |

### 改动

| 文件 | 改动 |
|---|---|
| `unity/Assets/Sim/ItemDatabase/InventoryFold.cs` | `ApplyBuildCost` 选料段改为**委托** `BuildMaterialPlanner.Plan`;`InventoryProjection` 增 `internal Rows`;删已无引用的 `OwnedIdsOf` |
| `unity/Assets/Sim/World/World.cs` | 显式实现 `IOccupancyQuery`(此前 `IOccupancyQuery` **零生产实现** ⇒ 判定器无生产构造点) |
| `unity/Assets/Sim/World/DemolishChecker.cs` | 新增 `public const int P0DemolishRefundRatio = 65536`(此前只有注释与测试字面量) |
| `unity/Assets/Gameplay.Boot/CompositionRoot.cs` | `world != null` 支内新增建造链条装配(`:906-926`),服务袋交出 `buildController`(`:1275`) |
| `unity/Assets/Gameplay.Boot/CompositionRootServices.cs` | 新增 `BuildController` 属性 |
| `unity/Assets/Gameplay.Boot/BootRoot.cs` | 4.45 段(`:355-381`):`BindOwner` + 默认幽灵模块 + `BuildInputDriver`;`Update` 转发 `OnFrame` |
| `unity/Assets/InputSystem_Actions.inputactions` | +4 动作 / +12 绑重(K&M / Gamepad / XR 各一),**纯插入 168 行、0 删除** |
| `unity/Assets/Tests/EditMode/InputSystem/action_asset_identity_test.cs` | P0 动作期望清单 8 → **12**(就地加注) |

---

## §二 AC 逐条结账(卡面原文 → 实测载体)

> `Landed` = 改动在盘上;`Proven` = 被可复算判据钉住;`Branch` = 多分支判定的分支覆盖。
> **本轮运行其判据才可标 `Proven ✅`。**

| # | 卡面原文(节录) | Landed | Proven | Branch | 载体(`file::test`) |
|---|---|---|---|---|---|
| **A-1** | `skeuomorphic-ui.md` 新增「建造交互」节 + `modular-building.md:511` 的 `O-23-6` 转 ✅ | ✅ | ✅ | — | 文档走查:`skeuomorphic-ui.md` §UI Requirements **三之二**(`:1351` 起)· `modular-building.md:511` 已转 ✅ · `input-system.md` 规则二表 +4 行。**非测试,不计入判据面** |
| **B-1** | 建造取格只经 `CellTransitionDetector.CellFromPosition`;生产树无第二处 float→格 floor | ✅ | ✅ | — | `build_controller_test.cs::test_acB1_buildPath_doesNotReQuantizeCells`(扫 3 文件 × 3 记号,剥注释) |
| **B-2** | 非法放置**零副作用**:`Place ≠ 通过` ⇒ `Place` 0 次、世界流 0 条 | ✅ | ✅ | ✅ 3/3 | `::test_acB2_placeOnNonSlotCell…` / `…OnOccupiedCell…` / `…WithUnboundOwner…`(三种拒绝源各一例) |
| **B-3** | 合法放置端到端:实例落库 + `IsEffectivelyWalkable == false` + 流 +1 | ✅ | ✅ | ✅ 2/2 | `::test_acB3_legalPlace_appendsOneStructurePlacedAndBlocksWalkability`(锚点格 ∧ **非锚点足迹格**两分支) |
| **B-4** | 材料**先验后写**(⚠️ 后半句已订正,见 §三①) | ✅ | ✅ | ✅ 2/2 | `::test_acB4_insufficientMaterial_rejectsWithoutTouchingWriter`(不足) · `::test_acB4_sufficientMaterial_appendsOnePlacedAndZeroDespawned` + `::test_acB4_foldDeductsCostFromSameStream`(足够) |
| **B-5** | 选料器:升序兜底 / 跨栈 N 条 / 不重复点名(⚠️ 「相邻 `Seq`」已订正,见 §三②) | ✅ | ✅ | ✅ 6/6 | `build_material_planner_test.cs` 的升序 / 跨栈 / 不重复点名 / 不足整手拒 / owner 过滤 / 零行契约六组 |
| **B-6** | 拆除:拒 ⇒ `Remove` 0 次;可拆 ⇒ 流 +1、返还按 P0 1.0 | ✅ | ⚠️ **部分** | ✅ 1/2 | 可拆半边:`::test_acB6_remove_appendsRemovedAndRefundAndFreesCells`;**「拒 ⇒ 零副作用」半边无独立判别力**(见 §六 之 ③) |
| **B-7** | 开 / 关建造模式前后 `IModalState.Modal` 恒 `None`(⚠️ 判据已订正,见 §三④) | ✅ | ✅ | — | `::test_acB7_buildPath_hasNoModalDependency`(源码面:5 文件剥注释后零 `IModalState`/`ModalId`) |
| **B-8** | 建造动作存在于 `.inputactions`,且绑重齐(⚠️ 「双绑重」已订正,见 §三③) | ✅ | ✅ | — | **既有门** `action_asset_identity_test::test_input_asset_structure_p0_actions_each_cover_three_binding_groups`(期望清单 8 → 12) |
| **B-9** | `PlaceableChecker` / `DemolishChecker` 生产构造点 **0 → 1**,且经服务袋暴露 | ✅ | ✅ | ⚠️ **1/2** | `::test_acB9_assembleWithWorld_yieldsBuildController`(正,**已被 M13 反证**)· `::test_acB9_assembleWithoutWorld_yieldsNullBuildController`(反,**无独立变异**) |

**计分(分母 = 10 条 AC)**:Proven ✅ **9 条**(A-1、B-1…B-5、B-7、B-8、B-9)· Proven ⚠️ 部分 **1 条**(B-6)。
其中 A-1 是**文档走查**(非测试),其余 8 条 ✅ 均由反证记录钉住。
**Branch 未满 2 处**:B-6 = 1/2、B-9 = 1/2。
不得把 B-6 读成整条绿,也不得把 B-9 读成两分支都已反证。

---

## §三 卡面四处前提订正(**原文不删**,已就地加注于 `sprint-20.md` §十 S20-19 末)

| # | 卡面原文 | 实施期实测 | 现行判据 |
|---|---|---|---|
| ① | **B-4** 后半句「消耗事件与 `StructurePlaced` **同一事务**落流」 | **前提错**:`InventoryFold.ApplyBuildCost`(`InventoryFold.cs:929`)已按 `cost(m)` 从 `StructurePlaced` **反推**扣减(2026-10-27 R16 甲案)⇒ 再写消耗事件 = **双重扣减** | 建造**唯一**流事件 = 一条 `StructurePlaced`;**恰一条** + **零** `DropDespawned` + fold 后余量恰少 `cost(m)` |
| ② | **B-5** 「跨栈 ⇒ N 条相邻 `Seq`」 | 同上 —— 无消耗事件 ⇒ **无 `Seq` 可数** | 载体改为 `BuildMaterialPlan.Lines`(N 条 `ConsumeLine` / 数组序 / 同一 id 只出现一次) |
| ③ | **B-8** 「K&M + Gamepad **双绑重齐**」 | 既有门要求**三组齐**(K&M / Gamepad / **XR**) | 四动作各配三组;XR 与 `Attack`(grip)/ `Emergency`(trigger)物理重叠 ⇒ 挂标 #9 |
| ④ | **B-7** 「`IModalState.Modal` 恒 `None`」 | **不可执行**:全仓**无** `IModalState` 聚合器(闭集 `ModalId` 只有枚举 + 各屏自述) | 源码面:建造链条五文件剥注释后零 `IModalState`/`ModalId` 记号 |

**「待实施期核的事项」三条结案**:

- `tick` 来源 ✅ —— `CompositionRoot.cs:925` 传 `() => tickDriver.CurrentTick`(`ITickProvider`),**零墙钟**。
- B-4「同一事务」判据 ✅ —— 随 ① 作废(不存在第二笔事件);改由「fold 从同一条流反推扣减」承载。
- 4 的交互系统实现状态 ✅ —— **未实现** ⇒ 建造为**键位直绑**(挂标 #2)。

---

## §四 回归(**基线溯源三元组**)

| 套件 | 结果 | 基线(三元组) | 差 |
|---|---|---|---|
| **EditMode** | `total=5639 passed=5604 failed=0 inconclusive=0 skipped=35` | `5601 / 5566 / 0 / 35` @ `2026-10-27T22:59Z` · sprint-20 S20-18 批 · `c784d88` | **+38 / +38** |
| **PlayMode** | `total=238 passed=234 failed=0 inconclusive=2 skipped=2` | `238 / 234 / 0 / 2 / 2` @ `2026-10-27T23:02Z` · sprint-20 S20-18 批 · `c784d88` | **0 / 0** |

**本次实跑三元组**(时刻取结果 XML 的 UTC mtime,可复算):

| 套件 | 结果 XML | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| 新增夹具单跑 | `$CLAUDE_JOB_DIR/tmp/s20-19-new-fixtures.xml` | `2026-10-28T00:28Z` | sprint-20 · S20-19 实施轮 | `314bf6a` + 未提交工作树 |
| EditMode 全量 | `$CLAUDE_JOB_DIR/tmp/s20-19-editmode-full.xml` | `2026-10-28T00:31Z` | 同上 | 同上 |
| PlayMode 全量 | `$CLAUDE_JOB_DIR/tmp/s20-19-playmode-full.xml` | `2026-10-28T00:32Z` | 同上 | 同上 |

复算时刻:`date -u -r <xml> +%Y-%m-%dT%H:%MZ`。

**判据等式**(`coding-standards.md` §借绿禁令):`5639 − 5601 = 38` = 本批新增测数(**恰等**,无「照抄陈旧全量」)。
**PlayMode 零增量**符合预期:本批**未新增** PlayMode 夹具(全部判据均为 EditMode 可判)。

**复算命令**:

```bash
python3 -I -c "
import xml.etree.ElementTree as ET
r=ET.parse('<xml>').getroot()
print(r.get('total'), r.get('passed'), r.get('failed'), r.get('inconclusive'), r.get('skipped'))"
```

⚠️ **一处如实说明**:`unity/Assets/Gameplay.Boot/BuildInputReader.cs` 在上述两次回归跑**之后**做过一次**纯注释**订正(把「B-7 断言模态恒 None」的错误自述改为「无该读面,只有源码面判据」)。该改动**零语义**(注释不在编译产物的行为面上),故未重跑;但**时序上**回归数对应的源码状态与该文件的当前内容差一次注释编辑 —— 登记于此,不掩盖。

---

## §五 证伪轮(S12-4)

**驱动器**:`tools/falsify/unity_falsify.py` · **规格**:`tools/falsify/specs/s20-19-build-initiator.json`
**受保护路径**:`unity/Assets/DataCooked` · `unity/Assets/AddressableAssetsData` —— 两轮均 `✅ 指纹未变`
**还原**:两轮均 `SHA-256 逐字节校验通过`;`grep -rn "FALSIFY-" unity/Assets` 零命中。

> ⚠️ **本节的数字分三次跑得出,不是一次跑的** —— 照实分列,不合账。
> 第一轮暴露的问题全部是**规格缺陷**(变异打错靶 / 预期推错 / 变异过宽),**没有一条是夹具假绿**;
> 每一处订正的原预期与改因,逐字留在 spec 对应变异的 `_round1_*` / `_round2_*` 字段里。

### §五之一 三轮结果(照实)

| 轮次 | 变异数 | exact | 超集 | 逃逸 | 未出判定 | 驱动器退出码 | 完成时刻 |
|---|---|---|---|---|---|---|---|
| **第一轮(主轮)** | 15 | **7** | **5** | **3** | 0 | **1** | `2026-10-28T00:42Z` |
| **第二轮** | 16(含负对照 M5b) | **15** | 0 | 0 | **1**(M5) | **1** | `2026-10-28T00:56Z` |
| **M5 单跑复验** | 1 | **1** | 0 | 0 | 0 | **0** | `2026-10-28T01:06Z` |

**合计**:16 条变异 **全部 exact**,但**分两批** —— 15 条出自第二轮同一次运行,M5 出自订正后的单跑。
**不得**把这句读成「一次 16 条全绿」。

⚠️ **退出码陷阱(第一轮实际踩到)**:后台命令尾部写了 `echo "EXIT=${PIPESTATUS[0]}"`,
外壳因此**恒退 0** ⇒ 任务通知报的是 `exit code 0`,而驱动器实际退出码是 **1**。
判据只认日志末行 `══ 证伪轮**未通过**(退出码 1)══`,**不认**外壳退出码。

### §五之二 第一轮三类失败的全部根因(逐条对源码复核)

| 变异 | 第一轮结果 | 根因 | 处置 |
|---|---|---|---|
| **M5** | 逃逸(0 红) | 打的是**不可达分支**。`PlaceableChecker.cs:100` 的第五条件与 `BuildController.cs:142` 的 step② 走**同一个** `BuildStockQuery → PlanForModule → IsComplete` 谓词 ⇒ 材料不足时 step① 已返回,142 行永不执行 | 改打共用谓词 `BuildMaterialPlanner.cs:195`;原守卫另立 **M5b 负对照** |
| **M11** | 逃逸(2 红) | **预期推错**。`BuildMaterialPlanner.cs:147` 的 `if (need <= 0)` 早退在循环**之前**,且与通用路径**行为等价**(`need ≤ 0 ⇒ available ≥ 0 ≥ need` ⇒ `available < need` 恒假 ⇒ 落到循环时 `remaining ≤ 0` ⇒ 零次)⇒ 该变异**不可能**让它红。原 `_why` 已自陈「打的不是早退分支」,却仍把它列为预期红 —— 自相矛盾 | 删该预期 |
| **M15** | 逃逸(0 红) | **变异打错靶**。门 `GroupsOf`(`action_asset_identity_test.cs:75-83`)读 binding 的 **`groups` 字段**;原变异只改 `path`,该 binding 仍声明 `groups: "XR"` ⇒ 三组仍齐 | 改打 `"groups"` 行(`:564`) |
| **M3** | 超集(4→9) | **变异过宽**。原变异把 `:134` 改成 `if (cond) { Place(...); return check; }` —— 而 `:135` `return check;` 本是**无花括号 if 的语句体**,加花括号后变成**无条件执行** ⇒ 成功路径被截断,`:146` 的 `Place` 永不执行 ⇒ 多红 5 条 | 收窄:在 `:133` 尾部追加独立 `if`,134/135 原样不动 |
| **M6** | 超集(3→4) | 双写使同一锚点落**两条** `StructurePlaced`(`StructureInstanceRegistry.Register` 每次分配**新 id**、按 id 存,不按锚点去重)⇒ 拆掉一条后 `StructureIdAt(0,0,0)` 仍反查到另一条 ⇒ `Is.LessThanOrEqualTo(0)` 红 | 补预期 |
| **M8** | 超集(2→3) | `otherOwner` 夹具:自己 1 份、需 2 份 ⇒ 停用「不足即整手拒绝」后仍产出半份计划(`Lines=[1:1]`)⇒ `Lines.Length == 0` 红 | 补预期 |
| **M9** | 超集(3→5) | ① `projectionAndRows` 断 `Show(plan)=="2:3,5:1"`,不夹 `remaining` 时第二栈取满 ⇒ `"2:3,5:3"`;② fold 的 `ApplyBuildCost` 走**同一个** `Plan` ⇒ 扣走整栈 8 份而非 `cost=4` ⇒ 余量 0 ≠ 4 | 补预期 |
| **M10** | 超集(3→4) | 归属未绑定 ⇒ owner = -1 ⇒ 去 owner 过滤后**所有人的栈都算作 -1 的** ⇒ `HasStock` 转 true ⇒ step① 放行 ⇒ `Place` 被调用 | 补预期 |

### §五之三 第二轮的一处规格笔误(如实登记)

M5 在第二轮**未出判定**:订正脚本用 `rel = m5["file"]` 算 `expect`,而 `file` 字段本身漏改
⇒ `expect` 从 **`BuildController.cs:195`(旋转守卫)** 读到原文,变异自洽地打在旋转守卫上
⇒ 下一行 `throw` 失去归属 ⇒ **编译失败**(`unity` 退出码 6,无结果 XML)。

**驱动器没有误报全绿**:`unity_falsify.py:264-267` 在 XML 缺失时 `verdict = max(verdict, EXIT_ESCAPE)`,
日志末行正确输出 `══ 证伪轮**未通过**(退出码 1)══`。订正 `file` 并重取 `expect` 后单跑 → exact。

### §五之四 逐条结账(`Landed` / `Proven` / `Branch` / `Proven-by`)

> 格式承 `code-review` skill Phase 8b。`Proven-by` 一律是**反证记录**(停用守卫 ⇒ 哪条红),
> 不是动作描述。`Branch` = 该 AC 的每个判定分支各有几例夹具被反证钉住。

| AC | Landed | Proven | Branch | 反证记录(停用 <守卫> ⇒ <测试> 红) |
|---|---|---|---|---|
| **A-1** | ✅ | ✅ | — | 文档走查(非测试):`skeuomorphic-ui.md:1351` · `modular-building.md:511` 已转 ✅ · `input-system.md:157-160` |
| **B-1** | ✅ | ✅ | ✅ 1/1 | **M1**:在 `TryPlace` 插一处 `Mathf.FloorToInt` ⇒ `test_acB1_buildPath_doesNotReQuantizeCells` 红 |
| **B-2** | ✅ | ✅ | ✅ 3/3 | **M3**:拒绝分支内加一次 `Place` ⇒ 三条 `test_acB2_*` **全红**(非槽位 / 已占 / 归属未绑定,三种拒绝源各一) |
| **B-3** | ✅ | ✅ | ✅ 2/2 | **M4**:`StructureKinds.cs:327` 只占锚点 ⇒ `test_acB3_legalPlace` 红(非锚点足迹格断言);**M6**:双写 ⇒ 同条红 |
| **B-4** | ✅ | ✅ | ✅ 2/2 | **M5**:把两处共用的 `plan.IsComplete` 钉成恒 true ⇒ `test_acB4_insufficientMaterial` + 4 条红;充足半边由 **M6** 钉(双写 ⇒ `test_acB4_sufficientMaterial` 与 `foldDeductsCost` 双红) |
| **B-4′**(step② 冗余) | ✅ | ✅ | — | **M5b 负对照**:停用 `:142` 的 step② 早退 ⇒ **预期零红,实得零红** ⇒ 机械证明「step② 与 step① 冗余」,不是靠散文断言 |
| **B-5** | ✅ | ✅ | ✅ 6/6 | **M7** 改降序 ⇒ 5 红 · **M8** 停「不足即整手拒绝」⇒ 3 红 · **M9** 不夹 `remaining` ⇒ 5 红 · **M10** 去 owner 过滤 ⇒ 4 红 · **M11** 去 `remaining>0` 终止 ⇒ 2 红 |
| **B-6** | ✅ | ⚠️ **部分** | ✗ 1/2 | 可拆半边:**M12** 返还比改 0 ⇒ `test_acB6_remove` 红。**「拒 ⇒ 零副作用」半边无独立变异**(见 §七.3) |
| **B-7** | ✅ | ✅ | ✅ 1/1 | **M2**:在 `TryPlace` 插一处 `IModalState` 记号 ⇒ `test_acB7_buildPath_hasNoModalDependency` 红 |
| **B-8** | ✅ | ✅ | ✅ 1/1 | **M15**:`BuildRotate` 那条 XR 绑重的 `groups` 改 `Keyboard&Mouse` ⇒ 三组门红 |
| **B-9** | ✅ | ✅ | ⚠️ **1/2** | 正例:**M13** 装配不交出 `buildController` ⇒ `test_acB9_assembleWithWorld` 红。**反例**(无 world ⇒ null)无独立变异 —— 见 §七.4 |

**计分(分母 = 10 条 AC)**:Proven ✅ **9 条**(A-1、B-1…B-5、B-7、B-8、B-9)· Proven ⚠️ 部分 **1 条**(B-6)。
其中 A-1 是**文档走查**(非测试),其余 8 条 ✅ 均由反证记录钉住。
**Branch 未满 2 处**:B-6 = 1/2、B-9 = 1/2。
另有 **B-4′ 负对照**一条(机械证明 step② 与 step① 冗余),**不计入 10 条 AC 的分母**。

### §五之五 可复算命令

```bash
# 干跑:只校验 16 条变异的锚点(纯读,零写盘)
python3 tools/falsify/unity_falsify.py \
  --spec tools/falsify/specs/s20-19-build-initiator.json --dry-run
# 期望末行:══ 证伪轮通过:16 个变异全部恰如预期 ══

# 真跑(约 25 分钟,16 条 × 一次完整 EditMode 批次)
python3 tools/falsify/unity_falsify.py \
  --spec tools/falsify/specs/s20-19-build-initiator.json \
  --guard unity/Assets/DataCooked --guard unity/Assets/AddressableAssetsData
# 判据 = 末行「══ 证伪轮通过 … ══」;**不认外壳退出码**(尾部 echo 会吞掉真实码)

# 残留检查(期望零输出)
grep -rn "FALSIFY-" unity/Assets --include=*.cs --include=*.inputactions
```

---

## §六 挂标不记绿(**9 条,一条未勾**)

1. **幽灵预览零渲染** —— 用户裁定 ①。42 只出「量化结果 + 合法性」的接口,不画材质 / 不画 world-space 标记。
2. **建造模式进入方式 = 键位直绑** —— 4 的交互系统**未实现** ⇒ 不是 ADR-009 §七 意义上的世界交互意图。
3. **`BuildSlotCatalog` 生产侧仍空** —— `RegisterSlot` **零生产调用点**,`MinimalWorldFactory.cs:99` 造空目录 ⇒ 生产 `PlaceableChecker.Check` **恒 `SlotNotFound`** ⇒ **玩家今天仍建不了东西**。本批兑现的是「接线 0 → 1」,**不是「可玩」**。解锁归关卡工具(ADR-022)导出 `buildslots` 烘焙产物。
4. **不可放置原因的 UI 呈现** —— 只留 `PlaceableResult` / `DemolishResult` 枚举,不画提示。
5. **客机建造上行** 归 45(P1b;`EmergencyAttempt` 走可靠通道的同族问题,ADR-001 §一之三)。
6. **`BuildPartQualityGate` 不参与放置判定** —— P0 建材 `quality` 恒 1,本批不重复施加。
7. **拆除返还物的落点格** 由 20 定,本批只触发(`DropEventWriter` 已封装)。
8. **建造音效 / cue 未接** 归 44。
9. **建造输入不查模态开集** —— 全仓无 `IModalState` 聚合器 ⇒ 建造模式开着且同时摊开脉案 / 出诊箱时,`Enter` 会**同时**被 UI `Submit` 与建造放置消费。**这是挂标,不是已修复**;修法 = 42 的模态聚合器落地轮。
   ⚠️ **B-7 的源码面判据证明「建造不占模态」,不证明「双触发不会发生」** —— 两者不可互替。

**同批另两条实现期事实(不改 AC,只登记)**:

- **玩家选料界面未落地** —— `BuildInputDriver.SelectedModuleId` 默认 = 目录**最小 module id**(`BootRoot.cs:372-379` 已就地声明「此默认值不是设计决定」)。
- **XR 组动作资产有绑重、运行期无读面** —— `BuildInputReader` 只读 K&M / Gamepad;XR 绑重与 `Sprint` 的 Adrift 状态同格。

---

## §七 未结事项

1. **本批未 commit**(无用户指令)。
2. **`sprint-status.yaml` 的 S20-19 状态** 未转 `done` —— 本件是**实施轮**证据;`/story-done`(含代码评审门)未跑。
3. **B-6 的「拒 ⇒ 零副作用」半边无独立判别力**(见 §五之四 的 B-6 行):`StructureWriter.Remove`(`StructureKinds.cs:369-376`)对未知 id 先 `return false`、**其后无任何 `Append`** ⇒ 「判定器放行 + 写者兜底」与「判定器拒绝」在副作用上不可分。该半边的判别力只落在**返回值断言**上,本件不为其编造变异。
4. **B-9 的反例半边无独立变异**:`test_acB9_assembleWithoutWorld_yieldsNullBuildController` 断「`world == null` ⇒ `BuildController` 为 null」,要打破它须把整段装配**移出** `world != null` 分支 —— 行替换做不到(不是改一行能表达的守卫)。Branch 记 1/2,**不借绿**。
5. **证伪规格本身有 4 处缺陷,已逐条登记在 spec 里**(承「原文不删」):第一轮 3 处(M5 打不可达分支 · M11 预期推错 · M15 打错靶)+ 5 处预期写窄(M3 另有变异过宽)+ 第二轮 1 处(`file` 字段漏改致编译失败)。原预期与改因逐字留在各变异的 `_round1_expectRed` / `_round1_result` / `_round1_note` / `_round2_bug` 字段。
   ⚠️ **这 8 处全部是规格缺陷,不是夹具缺陷** —— 本批新增的 38 条夹具**没有一条**被证伪轮判为假绿。
