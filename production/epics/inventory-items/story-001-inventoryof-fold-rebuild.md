# Story 001: InventoryOf 世界流投影与清空重建等值

> **Epic**: 库存与物品
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-26

## Context

**GDD**: `design/gdd/inventory-and-items.md`(规则一 fold · 边例 · AC A 组重建行)
**Requirement**: TR-inventory-001(库存 = 世界流纯投影,快照 = 优化非真相)· TR-inventory-009(20 不接触战斗与病程,只交读数)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-009(主): 三态分类 —— 库存条目 = 模拟态进流,投影 = 派生态重建 · ADR-005(次): 事件流唯一真源 · ADR-010(次): 快照与三流冲突时以三流为准
**ADR Decision Summary**: `InventoryOf(player) = fold(世界流, {DropSpawned, DropClaimed, DropDespawned, StructureRemoved, Craft.ActualConsumed, StructurePlaced, ResourceHarvested})`;七类 Kind 全为既有,零追加;`ResourceHarvested` 并入为 17 侧 R-注⑧ 义务(否则品级重建不出);内存索引 = 优化非真相,清空重建须逐位相同;用药消耗走世界流 `DropDespawned`(实例级),**fold 域维持世界流**(OQ-20-1 已裁:处置事件住病史流不含 instance_id,不以其为载体)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: fold 为门 A 纯 C#(整数载荷 → 索引结构),零引擎 API;重建等值属确定性回归最小面,Mono 侧本故事可自证;跨平台逐位(承 ADR-012)不在本条判据内。

**Control Manifest Rules (this layer)**:
- Required: fold 纯函数(输入 = 流前缀 + 烘焙定义,零第三来源);投影结果只读(消费者不得回写);`InventoryOf` 是 20 对外唯一库存真值入口(零第二真源)
- Forbidden: 内存索引当真源(索引与 fold 冲突 ⇒ fold 赢 + 具名告警);读病史流/病例流条目入 fold 域(域 = 世界流);在 fold 内求值 EFF/QualityMod(那是 21a,AC-20-05 纪律)
- Guardrail: 前置债以 BLOCKED-BY 原样挂判据,禁借绿:AC-20-03 五前置(R2/R3/R10/R11/OQ-20-5;R10 已由 18 认领、R11 已闭合、R3 未落地);本故事交付「重建机制本体 + 可判伪的挂起项」

---

## Acceptance Criteria

*From GDD `design/gdd/inventory-and-items.md`, scoped to this story:*

- [ ] **AC-20-03** [A]: `GIVEN` 一份存档,`WHEN` 清空 20 的内存索引并从三流重建,`THEN` `InventoryOf` 逐位相同(规则一)。**⚠️ BLOCKED-BY-R2 / R3 / R10 / R11 / OQ-20-5 + R15 / R16 / R17 / R18**(九件)—— 前置未全回写前**不得记绿**;已闭合项(R10 认领落 18、R11 同步、D-21-28 裁[甲])在证据链逐条点名。**2026-10-26 实现轮实测**:重建**机制**面已验(全序/幂等/逐位等/乱序无关,四条夹具 + M1/M3/M4/M6 反证),但 `Craft` 产出 / 建造扣 / 建造返还 / 非 17 来源品级**四个面结构性不可重建**(R15–R18 载荷位本身不存在)⇒ **不勾**
- [ ] **AC-20-16** [I]: 一次用药消耗,经 7a 持久化往返 + 清空内存索引重建 ⇒ 该实例经**世界流 `DropDespawned`** 消失(非处置事件),fold 域仍是世界流。**⚠️ BLOCKED-BY-OQ-20-5 / R2**。**2026-10-26 实测**:fold **侧**已验(见 `test_ac2016_dropDespawned_isTheOnlyCarrierOfConsumption` 双向夹具 + M3/M4 反证);缺的是 **20 侧写者**(`DropDespawned` 无生产端)⇒ **不勾**
- [x] **AC-20-05** [A]: `GIVEN` 20 的代码路径,`WHEN` 检索 `EFF`/`QualityMod`/`quality_distribution` 求值,`THEN` 零(规则五:20 不求值 —— fold 只搬运上游算好的整数)。**✅ 2026-10-26 记绿:源面(剥注释后符号扫描)+ IL 面(能力断言:扫 `InventoryFold` 全部方法体的 `MethodReference`,零 21a 求值器调用;含 `scanned > 0` 反空集断言)双判**
- [x] **AC-20-17** [A]: 一次炮制/一次建造,检索世界流 ⇒ 零 `ItemConsumed`/`ItemTransferred` 追加 Kind;扣减由 `Craft.ActualConsumed` / `StructurePlaced.cost(m)` 唯一推出(OQ-20-1 · 规则一①)。**✅ 2026-10-26 记绿:枚举面(`Enum.IsDefined` 能力断言)+ 七谓词面(世界流内非七谓词 Kind 零投影变化,M5 反证)**

---

## Implementation Notes

*Derived from ADR-009 §二/§一(主)/ ADR-005:*

1. fold 状态机:按全序键 `(Tick, StreamPriority, Patient, Seq)` 升序扫世界流,对七类 Kind 施加纯函数转移(出生=DropSpawned 建实例条目;归属=DropClaimed 迁 owner;消失=DropDespawned 扣 qty;增益=Craft 的 `output_instance_ids`+`OutputQty/Quality`;扣料=Craft.ActualConsumed;建造扣=StructurePlaced.cost;返还=StructureRemoved;品级源=ResourceHarvested 绑 instance→quality)。
2. 索引结构 = `(instance_id → {item_key, quality, qty, owner})` 纯内存字典,派生视图(按玩家 / 按格)都从该 fold 读;存档/快照只存事件流(ADR-010),读档 = 从流重建 + 快照作起点优化(冲突时流赢 —— 该优先级写进断言)。
3. 品级载体:实例的 `quality` 只在 `ResourceHarvested`(采出)与 `Craft.OutputQuality`(制出)两条上出现,fold 一次性绑定;此后只读(承 21a 品级不抬高纪律的投影侧)。
4. AC-20-03 的可执行子集(不违禁借绿):~~交付「重建器本体 + 用**完整夹具流**跑通」—— 夹具流内构造 R2/R3 尚未定稿字段的合法占位(如 `DropDespawned` 按单事件形状),但 AC 行保持未勾 + `Ignore` 标注,真实定稿后摘除;`Ignore` 理由逐条点名五前置(镜像 processing Story 001 的 O-18-R1 处理形)~~。**⚠️ 2026-10-26 实测改判(用户裁定「先登记欠账,再写本体」)**:前置核实发现 **R15/R16/R17/R18 四处载荷位本身不存在** ⇒ 对它们「构造合法占位」= **发明一个生产端永不发出的字段**,跑通的只是夹具不是链路。**本件一件 `Ignore` 都没用**(实测 `grep -c "\[Ignore" = 0`),改为**更硬的形态**:缺载体一律 fail-loud 进 `Gaps`(具名 R 编号),`IsComplete` 二值可判 —— 缺口闭合那天这些断言**会转红**,由红转绿改写成逐位等。
5. 用药/跨栈消耗形状(BL-23④ 选栈点名 `[(instance_id, qty)]`)由 Story 005 落地;本故事只保证 fold 消费端能表达「按实例扣 qty」(✅ OQ-20-10 已于 2026-10-06 裁「相邻多条 N 条」⇒ 兜底读法不再是猜测,fold 侧直接按「逐实例 qty」实现)。
6. 性能门:溢出界 `|Inventory| × stack_max × max(weight) < 2^31` 是烘焙期常量 + 构建期断言(TR-inventory-010 partial,值班方未裁)—— 本故事在 fold 读入口做防御断言,值班方裁定外抛登记。**⚠️ 2026-10-26:本件未落地该项**(依赖 `stack_max` / `weight`,归 Story 003 的 `IModuleCatalog` / 物品表面)⇒ 登记为 Story 003 的入向义务。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002:id 铸造与高水位(fold 消费 id,不生产 id)
- Story 004/005:CanCarry 裁决与原子装卸(本故事只提供投影读数)
- Story 006:摆放序与药箱呈现
- 7a(persistence epic):codec 与段清单(本故事只断「流赢快照」的优先级语义)
- ADR-012 落地轮:跨平台重建逐位(不在本条判据)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-20-03**: 清空重建逐位等。
  - Given: 合成事件流夹具(七类 Kind 全覆盖,≥500 事件,含多玩家/多实例/品级绑定);内存索引已建。
  - When: 清空索引 → 从流重建。
  - Then: 重建投影与原投影逐位相同(实例集、owner、qty、quality 四张表全等);二次重建幂等。
  - Edge cases: 乱序输入(先喂后 tick 事件)⇒ fold 按全序键排序后同结果(排序在 fold 内,非调用方义务);空流 ⇒ 空投影非 null。真实定稿字段前挂 `Ignore("BLOCKED-BY R2/R3")` 于涉及未裁字段的子用例。
- **AC-20-16**: 用药走 DropDespawned。
  - Given: 实例 i 被用药消耗;7a round-trip 夹具(或流重放替代 codec 半边)。
  - When: 重建。
  - Then: i 经 `DropDespawned` 消失;断言 fold 域读集 ⊆ 世界流(反射/路由表断言:病史流 Kind 零消费)。
  - Edge cases: 处置事件(病史流)在场但无 DropDespawned ⇒ 实例仍在(证明载体正确);OQ-20-5/R2 未裁 ⇒ 子用例 Ignore,AC 行未勾。
- **AC-20-05**: 零结算求值。
  - Given: 20 程序集。
  - When: 符号扫描 EFF/QualityMod/quality_distribution。
  - Then: 零求值命中(载荷搬运不算);负样例注入必红。
  - Edge cases: quality 值的**搬运**(从 Craft 载荷抄入索引)与**求值**(算出 quality)以调用图区分(承 processing AC-18-02 边界口径)。
- **AC-20-17**: 零追加 Kind。
  - Given: `entities.yaml` Kind 闭集 + 20 的 Append 调用点全集。
  - When: 炮制/建造/用药/转移四类消耗转移各一次。
  - Then: 流增量只含既有七类;20 自身 Append 位 = 拾取/放下/用药类(Drop* 三支),零第四支。
  - Edge cases: 与 Story 005 的意图路径共测;扣减推导正确性由 fold 测试反证(不重复造轮子)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/Inventory/inventory_fold_test.cs` — must exist and pass(本体用例;BLOCKED-BY 子用例 Ignore 挂起)

**Status**: [x] Created and passing

**实际载体**(2026-10-26 回填):

| 项 | 值 |
|---|---|
| 实现 | `unity/Assets/Sim/ItemDatabase/InventoryFold.cs`(633 行 · 门 A 纯 C#) |
| 测试 | `unity/Assets/Tests/EditMode/Inventory/inventory_fold_test.cs`(710 行 · **28 个 `[Test]`**) |
| 级别 | EditMode(门 A 程序集,纯逻辑) |
| 装配 | 真 `InMemoryBlobPool` + 真 `PayloadEncoder` + 真 `PayloadDecoder` —— **不 mock 载荷层**(载荷 struct 与 codec 分派一旦脱节,这里就红) |
| `[Ignore]` 数 | **0**(见 §Implementation Notes 4 的改判说明) |

**AC → 载体映射**:

| AC | 载体 |
|---|---|
| AC-20-03(不勾) | `test_ac2003_dropLifecycle_foldsOwnerAndQty` · `..._qualityArrivesAfterSpawn_stillBound` · `..._unclaimedInstance_hasNoOwnerSentinel` · `..._despawnToZero_removesFromProjection` · `..._multiPlayerMultiInstance_ownerViewIsPerPlayer` · `..._shuffledInput_yieldsIdenticalProjection` · `..._sameStreamTwice_isIdempotent` · `..._emptyStream_emptyProjectionNotNull` |
| AC-20-16(不勾) | `test_ac2016_drugTreatmentAppliedInHistoryStream_doesNotTouchProjection`(反向:域外条目不动投影)· `test_ac2016_dropDespawned_isTheOnlyCarrierOfConsumption`(正向:补上即消失) |
| AC-20-05 ✅ | `test_ac2005_foldSource_neverEvaluatesEfficiencyOrQualityMod`(源面)· `test_ac2005_foldIL_neverCallsQualitySolvers`(IL 能力断言) |
| AC-20-17 ✅ | `test_ac2017_registryHasNoItemConsumedOrTransferredKind`(枚举面)· `test_ac2017_foldIgnoresWorldKindsOutsideTheSevenPredicates`(七谓词面) |
| R15 / R16 / R17 / R18 缺口 | `test_r15_craftOutputs_raiseGapAndDoNotInventItemKey` · `test_r16_structurePlaced_raisesGapWithNoActor` · `test_r17_structureRemoved_raisesGapWithNoRefundIdentity` · `test_r18_instanceWithNoQualitySource_raisesGapButKeepsRow` |
| 完整性守卫 | `test_harvestWithoutSpawn_isDangling_notSilentlyDropped` · `test_duplicateSpawnOnLiveInstance_raisesDuplicateInstanceId` · `test_secondClaim_raisesDuplicateClaim_andKeepsFirstOwner` · `test_despawnMoreThanHeld_raisesInvalidQuantity_andKeepsRow` · `test_craftConsumeOtherPlayersItem_raisesConsumptionExceedsHolding` · `test_craftZeroOutput_raisesNoCraftOutputGap` · `test_nullDecoder_throwsInsteadOfReturningEmptyProjection` · `test_requireComplete_onCleanStream_returnsSameProjection` |
| codec 边界 | `test_craftUnevenArrays_areRejectedAtCodecBoundary_beforeFoldEverSeesThem` |
| 门 A | `test_gateA_simAssemblyHasNoEngineReferenceAndFoldHasNoFloat` |

**Test Evidence 三元组**:

| 项 | 值 |
|---|---|
| 截至时刻 | 2026-10-26 09:39:54Z(本机) |
| 批次 | `inv-fold`(夹具级)· `s20-10-official-run2`(证伪轮) |
| commit | `40d9cfa`(本件改动**未提交**) |

| 级别 | total | passed | failed | skipped |
|---|---|---|---|---|
| 夹具级 | **28** | **28** | 0 | 0 |

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' unity/Logs/inv-fold.xml | head -1   # 28/28/0
grep -c "public void test_" unity/Assets/Tests/EditMode/Inventory/inventory_fold_test.cs       # 28
grep -c "\[Ignore" unity/Assets/Tests/EditMode/Inventory/inventory_fold_test.cs               # 0
```

**⚠️ 未跑全量回归**:本条是**纯新增文件**(`InventoryFold.cs` + 测试 + `.meta`,`git status` 全为 `??` 未跟踪),
不改任何既有文件 ⇒ 理论上零回归面,但**「理论上」不是实测**。**禁把它读成「全量绿」**。

**Falsifiability Gate(S12-4 · 强制)** —— 14 条变异,**官方驱动 `tools/falsify/unity_falsify.py` 独立复算,14/14 恰如预期**:

```bash
python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/s20-10-inventory-fold.json
# 判据二:受保护路径指纹未变 ✅ · ══ 证伪轮通过:14 个变异全部恰如预期 ══
```

| 命中形态 | 主要断言 | 反证:停用什么 ⇒ 哪条转红 |
|---|---|---|
| 定序 | 全序键重排(排序在 fold 内,非调用方义务) | M1 改按传入序 ⇒ `shuffledInput_yieldsIdenticalProjection` 红 |
| 定序 | 品级预扫(17 实发序 = 先出生后品级) | M2 停预扫 ⇒ `dropLifecycle` / `qualityArrivesAfterSpawn` **2 条**红 |
| 共存互斥 | 归零行出投影 | M3 停剔除 ⇒ `despawnToZero` / `onlyCarrierOfConsumption` **2 条**红 |
| 共存互斥 | `DropDespawned` 的 qty 扣减 | M4 停扣减 ⇒ **3 条**红(比 M3 更上游) |
| 定义域收窄 | 七谓词是 fold 全部输入面 | M5 加 `case ActorCellEntered` ⇒ `foldIgnoresWorldKindsOutsideTheSevenPredicates` 红 |
| 一次性/幂等 | 每次 `Fold` 新建行表(无跨调用状态) | M6 注入跨调用状态 ⇒ `sameStreamTwice` / `shuffledInput` 红(红面扩散,`exactRed:false`) |
| 阈值边界(R15) | 缺载体 ⇒ 报缺口 | M7 静默吞 ⇒ `r15_craftOutputs_raiseGap…` 红 |
| 阈值边界(二次 claim) | 转移须走新序列 | M8 停保护 ⇒ `secondClaim…` 红 |
| 阈值边界(扣超持有) | 畸形输入须拒且不扣成负 | M9 停越界判定 ⇒ `despawnMoreThanHeld…` 红 |
| 阈值边界(R16) | 建造扣料无 actor ⇒ 报缺口 | M10 静默吞 ⇒ `r16_structurePlaced…` 红 |
| 阈值边界(R17) | 返还物无身份 ⇒ 报缺口 | M11 静默吞 ⇒ `r17_structureRemoved…` 红 |
| 完整性(Dangling) | 有采集无出生 ⇒ 报缺口 | M12 停判据 ⇒ `harvestWithoutSpawn_isDangling…` 红 |
| fail-closed | null 解码器须抛,不得静默返回空投影 | M13 改 fail-open ⇒ `nullDecoder_throws…` 红 |
| 阈值边界(R18) | 无品级来源 ⇒ 报缺口但保留行 | M14 静默吞 ⇒ `r18_instanceWithNoQualitySource…` 红 |

**⚠️ 首轮 4 条假红逃逸已如实记录并修复(不掩盖)**:首跑官方驱动时 M4/M5/M6/M7 **一条都没红**。
根因**不是夹具假绿,而是我写的 spec 变异文本空转** —— M4/M5 只往行尾加注释;M6 引用未定义的静态字段 ⇒ **编译失败无 XML**;
M7 的守卫条件(`firstOutputId < 0`)在夹具下永假。修正后重跑 **14/14 全红**。
原始失败日志:.claude/jobs/181888b1/tmp/official-spec-run.log · 修正后:.claude/jobs/181888b1/tmp/official-run-2.log

**还原判据 = SHA-256 逐字节**(不是 grep 变异串):

```
1c64d1639ff15bb9187559a33d640211c4bcf103ae060393b020794dbfea0359  unity/Assets/Sim/ItemDatabase/InventoryFold.cs
1c64d1639ff15bb9187559a33d640211c4bcf103ae060393b020794dbfea0359  tools/falsify/baseline-invfold/InventoryFold.cs
```

**⚠️ 已知边界(登记的未覆盖面,勿读成已验)**:
1. **`MalformedPayloadPairing` 分支无夹具** —— codec 写侧即拒(`PayloadCodec.World.cs:200-208`「坏数据不进字节面」),
   无法用真 codec 构造该形态 ⇒ fold 侧该分支是**纵深防御**,`Proven = ✗`。`test_craftUnevenArrays_…` 反证的是 **codec 拒收**,不是 fold 分支。
2. **本 spec 全部为 EditMode(Mono)** —— 跨平台逐位(ADR-012)不在本条判据内。
3. **AC-20-03 的「7a 持久化往返」半条未验** —— 本件只验「从流重建」,不验存档 codec 往返(归 7a)。

---

## Dependencies

- Depends on: item-database story-001/002(Complete,载荷类型面),foraging Story 003 / processing Story 003(两支上游事件的产出形状),`entities.yaml` 七类既有 Kind(零 kindgen 变更)
- Unlocks: Story 002(高水位扫流需要 fold 的读集),Story 004(CarryLoad 消费投影),Story 005(原子性断言基线 = 前后投影比对),Story 006(摆放序输入)

---

## Completion Notes

**Completed**: 2026-10-26
**Criteria**: **2/4 记绿**(AC-20-05 · AC-20-17)· AC-20-03 / AC-20-16 **不勾**(逐条点名前置)

### 交付面

| 文件 | 性质 |
|---|---|
| `unity/Assets/Sim/ItemDatabase/InventoryFold.cs` | 新建 · 633 行 · 门 A 纯 C# |
| `unity/Assets/Tests/EditMode/Inventory/inventory_fold_test.cs` | 新建 · 710 行 · **28 条夹具** |
| `tools/falsify/specs/s20-10-inventory-fold.json` | 新建 · **14 条变异** spec |
| `tools/falsify/baseline-invfold/InventoryFold.cs` | 新建 · 证伪轮还原基线(逐字节副本) |
| `design/gdd/inventory-and-items.md` | 修改 · §涟漪登记 **R15–R18** + AC-20-03 前置 5 → 9 件 |

### 交付形状(**不得读成「库存功能可用」**)

卡面原设想是「为 R2/R3 未定稿字段构造**合法占位**跑通」。前置核实推翻了这个路径:**四处载荷位根本不存在**
(R15 `Craft` 无产出 `item_key` / R16 `StructurePlaced` 无 actor / R17 `StructureRemoved` 无返还身份 /
R18 `DropSpawned` 无 `quality`)。对不存在的位「占位」= 发明生产端永不发出的字段。

⇒ **用户 2026-10-26 裁定:先登记欠账,再按 fail-loud 交付本体**。本件的判据因此变成两条正向可判伪的:

| # | 判据 | 实测 |
|---|---|---|
| ① | **可重建子域真跑真断言**(`Drop*` 三支 + `ResourceHarvested` 品级绑定 + `Craft` 扣料侧) | 28/28 绿 |
| ② | **缺载体一律 fail-loud 进 `Gaps`**(不静默跳过、不造占位)⇒ `IsComplete` 二值 | R15–R18 各一条缺口夹具 + M7/M10/M11/M14 反证 |

⚠️ **零 `[Ignore]`** —— 这是与 `foraging-003`「`Ignore` 点名 BLOCKED-BY」口径的**刻意分歧**:缺口是**正向判据**
(R15–R18 闭合后这些断言会转红,那是**期待**),不是「暂时跳过」。

### 四条 AC 的处置(逐条点名前置)

| AC | 判定 | 前置 | 已做/未做 |
|---|---|---|---|
| **AC-20-03** | **不勾** | 九件:R2(reason 值域,轻)· R3(高水位接线,重)· **R15/R16/R17/R18(载荷缺位,重)**;已闭:R10 / R11 / OQ-20-5 / D-21-28[甲] | 重建**机制**面已验(全序/幂等/逐位等/乱序无关);**增益 / 归还 / 建造扣 / 非 17 品级四个面结构性不可重建** |
| **AC-20-16** | **不勾** | OQ-20-5 / R2 + **20 侧 `DropDespawned` 写者缺位** | fold **消费端**已验(双向夹具);**写端零生产实现** |
| **AC-20-05** | **✅ 记绿** | 无 | 源面(剥注释符号扫描)+ IL 面(能力断言,含 `scanned > 0` 反空集) |
| **AC-20-17** | **✅ 记绿** | 无 | 枚举面(`Enum.IsDefined`)+ 七谓词面(M5 反证) |

### 对下游的契约承诺(sprint-status 点名要求)

本件定的 fold 口径会向后传导到 **S20-11(003 堆叠/容器/折重)· S20-12(004 CanCarry)· S20-13(005 原子性)**:

| 承诺 | 内容 | 消费方须注意 |
|---|---|---|
| **C-1** | `InventoryOf = Fold(世界流, decoder)`,`IsComplete == false` 时 `Projection` 是**部分应用态** | 唯一出口 = `RequireComplete()`(不完整即抛)。003/004 **不得**直接读 `Projection` 后当完整库存用 |
| **C-2** | 堆叠键 = `((long)ItemOrdinal << 32) \| (uint)Quality`,**`ItemOrdinal` 用载荷里的 int,不做 ordinal → `ItemKey` 解析** | 003 若要 `weight` / `stack_max`,须**自己注入物品表**;不要把「读物品表」塞进 fold(会引入非流来源) |
| **C-3** | 品级**只从载荷抄**,永不求值 | 004 算 `CarryLoad` 时不得在此处引入任何 21a 求值 |
| **C-4** | `Owner == InventoryProjection.NoOwner = -1` 表示**未认领** | 004 的按玩家视图须先过滤哨兵 |
| **C-5** | `InstancesOf(owner)` 返回**升序 `IReadOnlyList<long>`** | 006 的摆放序(instance_id 升序确定性函数)可直接消费 |
| **C-6** | `Gaps` 非空 ⇒ 库存读数**不可信**,但行仍可见(部分态) | 42 呈现层**:必须先看 `IsComplete`**,否则会把「缺载体」显示成「库存就这么点」 |
| **C-7** | **性能门未落地**(见下) | 003 的入向义务 |

### 未落地项(本件明确不做 · 归下游)

| # | 项 | 归属 |
|---|---|---|
| 1 | 性能门 `\|Inventory\| × stack_max × max(weight) < 2^31` 防御断言 | **S20-11(003)** —— 依赖 `stack_max` / `weight`,本件拿不到 |
| 2 | 按格视图(§Implementation Notes 2 提的「按玩家 / 按格」两视图) | **S20-11 / 006** —— 按格视图需 `spawn_anchor`,本件只做按玩家 |
| 3 | `Max(instance_id)` 高水位扫流 | **S20-12 / Story 002** —— fold 消费 id,不生产 id |
| 4 | `DropDespawned` 的生产端(用药消耗写者) | **Story 005** —— 本件只证 fold 侧能吃 |
| **5** | ~~**`Fold` 零生产调用点;`IsComplete` 对任何真实流恒 `false` ⇒ `RequireComplete()` 必抛,契约 C-1/C-6 零兑现面**~~ **✅ 2026-10-27 已接线**(原文不删,就地加注) | **已完成** —— 见下方「修复轮接线」节。⚠️ 但 `IsComplete` 的**恒 false 性质未变**(R16/R17/R18 载荷缺位仍在),只是「有人调它」这一半兑现了 |

### 修复轮接线(2026-10-27 · 评审 S20-10 B-1 / S20-11 B-1·W-2)

> 用户裁定原话:「**不进21也不记债,现在完成**」+「**「装载库存实体」的链,现在建**」+「**在装配根加「读档后重建库存投影」**」。

**接线落点**:`unity/Assets/Gameplay.Boot/InventoryLoadService.cs`(新建)——
`Rebuild()` 一次跑完 Fold → 实体集 → `BuildPartQualityGate.RequireValid` → `CarryLoadSolver.Sum`;
`LoadSequencer.Load` 的第 4 步(`afterRestore` 钩子)在**读档完成后**调它。

**对 C-1…C-6 的更新(改的是「兑现面」,不是契约本身)**:

| 承诺 | 契约是否变 | 今日兑现面 |
|---|---|---|
| C-1 | 否 | ✅ 有生产调用点(`InventoryLoadService.Rebuild`);⚠️ `IsComplete` 仍恒 false |
| C-2 | 否 | fold **仍不解析** ordinal → `ItemKey`(本件本体零改动);解析发生在**装配层**(`InventoryLoadService`,它注入物品表)—— 这正是 C-2 要求的形态:「读物品表」没被塞进 fold |
| C-3 | 否 | ✅ 品级仍只从载荷抄;`InventoryLoadService` 只**读** `row.Quality` |
| C-4 | 否 | ✅ `CarryLoadSolver.Sum(projection, …, owner: InventoryProjection.NoOwner)` 求的就是未认领载重 |
| C-5 | 否 | ⬜ `StacksOf` / 摆放序仍零生产调用点(归 20b 呈现侧) |
| C-6 | 否 | ⚠️ `InventoryLoadResult.IsComplete` **转发了**这个契约,且文档明写「今天恒 false,下游必须读它」—— 但没有下游消费者读它(today) |
| C-7 | 否 | ⬜ 性能门仍未落地 |

**诚实边界**:本件接的是「**投影被重建出来**」,不是「**玩家看得到库存**」。20b 呈现侧零改动。

### 评审

**未做 `/code-review`** —— 本条为纯新增文件、零既有文件改动,且用户此前对同类件(foraging-003)的处置是
「评审 → 修复 → 收口」三步;本轮**未走评审**。⚠️ **如实登记**:`Status: Complete` 表示**实现与测试自证**
(28/28 绿 + 14/14 证伪),**不表示**经独立代码评审。若需评审,`/code-review` 的标的 = 上述 5 个文件。

### 证伪轮的两处过程记录(不掩盖)

1. **首轮 4 条假红**:M4/M5/M6/M7 在**官方驱动**下一条都没红 —— 根因是**我写的 spec 变异文本空转**
   (M4/M5 只加注释、M6 引用未定义字段致编译失败、M7 守卫永假)。**已修正并重跑 14/14 全红。**
   ⚠️ 教训:自写一次性脚本跑出的 14/14 **不能替代**官方驱动 —— 前者的变异文本与 spec 是两份,后者才是唯一真源。
2. **M6 的 `exactRed: false`** 是刻意的(破坏「无跨调用状态」会扩散红面),
   判据为「含幂等/乱序**必红**」;`emptyStream` 已移出 expectRed —— 它是否红取决于自己是第几个跑的。

### 计划外/待裁

- **`tools/falsify/baseline-invfold/` 是否入库待裁** —— 它是证伪轮的还原基线,当前未跟踪。
  `.gitignore` 已有一条针对「证伪轮变异备份目录」的规则,但窄 glob 只匹配 `/fz-s19*/`,不覆盖本目录。
- **`design/gdd/inventory-and-items.md` 的 R15–R18 登记件** 已落(本件前置),但登记时明确写了
  「只核了两行权威件、未展开全文」⇒ **R15③ 的「两处权威件类型冲突」仍是抽样结论**。
