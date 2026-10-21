# 实现证据原件 —— clinic-machine / story-003(乘子求值 F-24-1/2)+ 24 三表 cooked 落盘接线

> **对象**:`production/epics/clinic-machine/story-003-modifier-evaluation.md`
> **日期**:2026-10-21
> **批次**:sprint-17 批 2(S17-2)
> **用户路径裁定**:「先补 cooked 落盘接线再做求值」—— 故本件分两段:① 三表烘焙 + 运行期接线(原非 S17-2 卡面,但求值器的输入是这三张表);② 求值器本体。
> **状态**:实现已落、证伪轮已过;**story-003 不据此转 Complete**(见 §五 未跑项)。

---

## 一、交付面(实测落盘)

### 1.1 烘焙产物(3 + 3 meta)

| 产物 | 大小 | 备注 |
|---|---|---|
| `unity/Assets/DataCooked/clinicmachine_context.cooked.bytes` | 见盘 | CONTEXT_TABLE,头导出 `K_CONTEXT_MAX`(F-24-3) |
| `unity/Assets/DataCooked/clinicmachine_adj.cooked.bytes` | 见盘 | ADJ_TABLE,头导出 `w_min` / `w_max` / `n_max` |
| `unity/Assets/DataCooked/clinicmachine_room.cooked.bytes` | 见盘 | ROOM_TABLE(子句集) |

- 烘焙入口:`unity/Assets/Editor.Tools.Bake/DataBakeMenu.cs:535` `[MenuItem("大医精诚/数据管线/烘焙 clinic-machine 三表(24 医馆)")] BakeClinicMachineTables()`;
  产物名常量 `:81/:84/:87`。
- `ConfigVersion = 0x4301F2B1`(内容哈希派生,ADR-014 §五)。
- **`FURN_TYPES` 闭集 = NOT-RUN** —— `O-24-1` / `TD-clinic-01` 未兑现,烘焙日志逐字打 `FURN_TYPES 闭集 = NOT-RUN — O-24-1 未兑现(TD-clinic-01)`。

### 1.2 运行期接线

| 文件 | 内容 |
|---|---|
| `unity/Assets/Sim/Clinic/ClinicMachineTables.cs`(新) | `ClinicContextTable` / `ClinicAdjTable` / `ClinicRoomTable` + 镜像枚举 + 构造期纵深拒收 |
| `unity/Assets/Gameplay.Presentation/ClinicMachineCookedCodec.cs`(新) | 三个 `Read*` 解码器 + `*DataSet` |
| `unity/Assets/Gameplay.Presentation/AddressablesDataProvider.cs`(改) | 三个 address 常量(:139 区)、三个 `Load<T>` 支路、三个 `Ensure*`、三处 `Preload()` 调用(:323-325) |
| `unity/Assets/Gameplay.Presentation/DataCorePreloader.cs`(改) | doc 注释 16 → 19 条 + 三条登记 |
| `unity/Assets/AddressableAssetsData/AssetGroups/data-core.asset`(改) | 新条目 ×3 ⇒ 组内地址 19 条 |
| `unity/Assets/Tests/EditMode/DataPipeline/provider_wiring_gate_test.cs`(改) | 门① 增三条 address ↔ baker 产物名比对 |

### 1.3 求值器

| 文件 | 内容 |
|---|---|
| `unity/Assets/Sim/Clinic/IClinicFurnitureTraitsSource.cs`(新) | `ClinicFurnitureTraits` + **三值** `ClinicCellLookup{Furniture, NonFurniture, Unknown}` + 查询缝 |
| `unity/Assets/Sim/Clinic/ClinicModifierEvaluator.cs`(新) | `ClinicAdjSum` / `ClinicEnvModOf`(零 clamp)/ `ClinicEquipModOf`(单 clamp);`EquipModCapRaw = 16384`;`FurnTraitsProductionReady => false` 哨兵 |

**三值而非常规 bool 的理由**:`NonFurniture`(确定不是家具)与 `Unknown`(不知道)必须可分 —— 前者跳过,后者硬失败。若合成 bool,属性源不完整会被静默读成「这一格不是家具」,`e_env` 与档位静默少算。

---

## 二、AC 逐条落点(已落 / 已证 双列)

| AC | Landed | Proven | 落点 | Proven-by |
|---|---|---|---|---|
| AC-24-01 EnvMod = 手算 F-24-1 逐位 | ✅ | ✅ | `ClinicModifierEvaluator.cs:97-117` | `test_clinic_envMod_gddWorkedExample_matches28000`(GDD 算例:28000 raw)· 反证 C3 |
| AC-24-01b 越界原样透传(24 侧不 clamp) | ✅ | ✅ | 同上(`:116` 唯一返回值) | `test_clinic_envMod_aboveEnvModMax_passesThroughUnclamped` / `..._allNegative_...` · **反证 C1:在 `:116` 加钳制 ⇒ 两条同时红** |
| AC-24-02 EquipMod 按 F-24-2,tier=0 不进和 | ✅ | ✅ | `ClinicModifierEvaluator.cs:131-160` | `test_clinic_equipMod_sumAboveCap_pinsToCap` / `test_clinic_equipMod_allZeroTier_returnsZero` · 反证 C4 |
| CAP<1 的二值语义(无中间值) | ✅ | ✅ | `:158` 单钳制 | `test_clinic_equipMod_positiveScores_haveNoIntermediateValue` · 反证 C4 |
| adj_sum 只 +x/+y,每对恰一次,n_max 内 O(1) | ✅ | ✅ | `:231-260` | `test_clinic_adjSum_adjacentPair_countedOnce` / `test_clinic_adjSum_missingPair_isZero` / `test_clinic_adjSum_singleCell_isZero` · **反证 C3:加 −x 反向 ⇒ 重复计** |
| n_max 越界拒收 | ✅ | ✅ | `:174-177` | `test_clinic_roomExceedsNMax_throws`(域界值 64 见 `nMaxBoundary_noWrap`) |
| 溢出安全:int64 内乘加 | ✅ | ✅ **限「实现选用 int64」** | `:150-159` / `:263-270` | `test_clinic_equipMod_largeScore_int64NoWrap` / `..._nMaxBoundary_noWrap`(**夹具越域**,见 §六 边界 3) |
| ROOM_NONE ⇒ 中性 (0,0) | ✅ | ✅ | `:104`(`ClinicEnvModOf`)等 | `test_clinic_roomNone_returnsNeutralZero` / `test_clinic_twoClusters_returnsNeutralZero` · **反证 C2** |
| 非家具格不计分不连通 | ✅ | ✅ | `:189-190` | `test_clinic_adjSum_nonFurnitureCellExcluded` · **反证 C5** |
| 属性未知 ⇒ 硬失败(禁静默跳过) | ✅ | ✅ | `:191-194` | `test_clinic_traitsUnknown_throwsInsteadOfSkipping` |
| 表只读视图不可强转改写 | ✅ | ✅ | `ClinicMachineTables.cs:98 / :362` | `test_clinic_contextTable_rows_notCastableToArray` / `test_clinic_roomTable_rooms_notCastableToArray` · **反证 R1/R2** |

**测试载体**:`unity/Assets/Tests/EditMode/ClinicMachine/clinic_modifier_eval_test.cs`(18 测)· `clinic_machine_cooked_codec_test.cs`(18 测,含 2 条只读视图)· `unity/Assets/Tests/EditMode/DataPipeline/provider_wiring_gate_test.cs`(11 测)。

---

## 三、回归(带三元组,禁借绿)

**基线溯源三元组**:截至时刻 `2026-10-21 09:02 CST` · 批次 `sprint-17 批 2(S17-2 收尾轮)` · commit `3a22dbf` + 未提交改动。

| 套件 | XML | 结果 | 对照基线(同带三元组) |
|---|---|---|---|
| EditMode(全量) | `unity/Logs/s17-2-clinic-full2.xml` | `total=4237 passed=4201 failed=0 skipped=36` | `4198 / 4162 / 0 / 36` @ `2026-10-21 08:02 CST` · `sprint-17 批 1` · `1dde232` |
| 漂移门(定向) | `unity/Logs/s17-2-drift-gate.xml` | `23 / 23 / 0` | — |
| Clinic(定向) | `unity/Logs/s17-2-clinic-after-touchup.xml` | `123 / 123 / 0` | — |

**集合差对账(禁借绿 —— 用 `fullname` 集合差,不用总数差)**:

```
基线 4198 → 本轮 4237      新增 39    消失 0
  + ClinicMachineCookedCodecTest × 18（含 2 条只读视图）
  + ClinicModifierEvalTest       × 18
  + cooked_drift_all_artifacts_test.drift[cooked.bytes] × 3（参数化用例，登记后生成）
```

`skipped` 恒 36(基线与本轮同一批 NOT-RUN 登记项,见 §五)。**判定:零回归。**

### 3.1 本轮全量首跑抓到两条**真回归**(已修,记录在案)

首跑 `unity/Logs/s17-2-clinic-full.xml` = `4234 / 4196 / 2 / 36`。两条红均为**真缺陷,不是夹具问题**:

| 失败 | 报文要点 | 根因 | 修复 |
|---|---|---|---|
| `cooked_drift_all_artifacts_test.test_sweep_declaredArtifactNames_allTriaged` | `源码声明的产物名 = 19,钉定值 = 16` | 三份 clinic 产物已声明、已落盘,但漂移门的 `pinnedDeclared` 仍写 16 | 16 → **19** + 注释写清增量来源 |
| `cooked_drift_all_artifacts_test.test_sweep_noGhostArtifact_onDiskButUnregistered` | `盘上存在未登记的产物(幽灵产物):[clinicmachine_adj, context, room]` | 同样三份未进 `BakedArtifacts` | 按门给的路径 ① 加进 `BakedArtifacts`,带现烘委托 `ClinicMachineTablesBake.BakeFromRepo(root).{ContextCooked,AdjCooked,RoomCooked}` |

- ⚠️ **这是本件最有价值的一步**:它把「补烘 ≠ 登记 ≠ 接线」三件事分开证了 —— 我此前已过 `provider_wiring_gate_test`
  (装载面接线),却**漏了漂移门登记面**。门没坏,是接线漏了一处。
- 复跑定向 = 23/23 绿;重跑全量 = 4237/4201/**0**/36。
- 文件头 `:23-24` 的「15 份」为 2026-10-20 当时实测值,**原文保留**,就地加注增补到 19。

---

## 四、证伪轮(Falsifiability Gate)—— 三份 spec / 9 个变异,全部恰如预期

驱动:`tools/falsify/unity_falsify.py`(行号 + 原文双锚定,施加与还原同一进程,SHA-256 逐字节校验,受保护路径指纹前后比对)。

### 4.1 求值 spec `tools/falsify/specs/s17-2-clinic-modifier-eval.json`(filter `Clinic`)

| 变异 | 靶点 | 停用什么 | 实测红集 | 判定 |
|---|---|---|---|---|
| C1-envUnclamped | `ClinicModifierEvaluator.cs:116` | `return new Fix(sum)` → 加 clamp | `envMod_aboveEnvModMax` / `envMod_allNegative` | ✅ 恰 2 条 |
| C2-roomNoneNeutral | `:104` | `return Fix.Zero` → `return baseEnv` | `roomNone_returnsNeutralZero` / `twoClusters_returnsNeutralZero` | ✅ 恰 2 条 |
| C3-adjTwoDirections | `:242` | 加 `−x` 反向邻居 | `adjSum_adjacentPair_countedOnce` / `envMod_gddWorkedExample_matches28000` | ✅ 恰 2 条 |
| C4-equipCapRemoved | `:158` | 删 CAP 上钳 | `equipMod_largeScore_int64NoWrap` / `..._nMaxBoundary_noWrap` / `..._positiveScores_haveNoIntermediateValue` / `..._sumAboveCap_pinsToCap` | ✅ 恰 4 条 |
| C5-nonFurnitureSkipped | `:189` | `NonFurniture` 塞进 `byCell` | `adjSum_nonFurnitureCellExcluded` | ✅ 恰 1 条 |

判据输出逐字:`══ 证伪轮通过:5 个变异全部恰如预期 ══`,`EXIT=0`,还原 `SHA-256 逐字节校验通过`。

⚠️ **本轮只传了 `--guard unity/Assets/DataCooked`**,`AddressableAssetsData` 未在该轮受保护(驱动只读命令行 `--guard`,不读 spec 内的 `guards` 字段)。五个变异均不触及 Addressables 路径,但**证书面只覆盖一个路径**。

### 4.2 接线 spec `tools/falsify/specs/s17-2-clinic-wiring.json`(filter `ProviderWiringGateTest`)

| 变异 | 靶点 | 停用什么 | 实测红集 | 判定 |
|---|---|---|---|---|
| W1-addressTypo | `AddressablesDataProvider.cs:139` | `clinicmachine_adj` → `clinicmachine_adjX` | `wiring_newArtifactAddresses_matchBakerArtifactNames` / `wiring_dataCoreGroup_containsEveryProviderAddress` | ✅ 恰 2 条 |
| W2-preloadMissingCall | `:323` | 停用 `Preload()` 里的 `EnsureClinicContext()` | `wiring_preload_invokesEveryEnsureMethod` | ✅ 恰 1 条 |

判据输出逐字:`══ 证伪轮通过:2 个变异全部恰如预期 ══`;受保护路径 = **两个目录**,指纹前后相同。

### 4.3 只读视图 spec `tools/falsify/specs/s17-2-clinic-readonly.json`(filter `Clinic`)

| 变异 | 靶点 | 停用什么 | 实测红集 | 判定 |
|---|---|---|---|---|
| R1-contextRowsBareArray | `ClinicMachineTables.cs:98` | `_rowView` → `_rows` | `contextTable_rows_notCastableToArray` | ✅ 恰 1 条 |
| R2-roomRoomsBareArray | `:362` | `_roomView` → `_rooms` | `roomTable_rooms_notCastableToArray` | ✅ 恰 1 条 |

判据输出逐字:`══ 证伪轮通过:2 个变异全部恰如预期 ══`。**交叉不误伤**:context 侧变异只红 context 一条,room 侧只红 room 一条。

---

## 五、未及核查 / 未跑项(如实登记,禁借绿)

1. **`FURN_TYPES` 闭集未兑现**(`O-24-1` / `TD-clinic-01`):生产侧**没有** `IClinicFurnitureTraitsSource` 的实现者。求值器只由测试注入夹具源;哨兵 `ClinicModifierEvaluator.FurnTraitsProductionReady => false` 恒 false,且 `ClinicRoomExtraction.FurnTypeClosureEnforced => false` 有一条 Sim 装配扫描测试(C11)使其**可检测**而不仅可观测。
   ⇒ **本件不主张「求值器已在生产链路上跑过」** —— 它跑在夹具源上。
2. **「布局 → 房间类型」匹配(ROOM_TABLE 求值)未实现**:`base_env` 由调用方经 `ClinicContextTable.TryGetBaseEnv` 解析后传入;求值器不假装房间匹配已存在。
3. **IL2CPP 三格 / 跨平台逐位**(ADR-012 矩阵):NOT-RUN,本机仅 Mono。
4. **数值**(`base_env` / 各 `e_env` / 各 `tier` 分布)未定稿 —— 归用户数值轮;夹具只用 GDD 算例与符号化小值验机制。
5. **story-003 的 AC 中「`env_score` 与 21a 的 clamp 配对」**(story-004 联测)未跑 —— 本件只测 24 侧「不 clamp」。

---

## 六、偏离与边界

1. **本件范围超出 story-003 卡面**:三表 baked 产物 + 运行期接线本属 story-001 面,但 S17-2 求值器无输入则不能开工。**用户已就路径裁定「先补 cooked 落盘接线再做求值」**,故按序执行;卡面归属的账留待 §七 story-001 的 Test Evidence 回填时如实登记。
2. **`AddressablesDataProvider` 未知类型错误串**已扩写为「+ 24 医馆三表(S17-2 前置:…)」—— 与既有支路同款 E-13 包装。
3. **两条越域夹具的证词收窄**:`equipMod_largeScore_int64NoWrap` 用 `tier = 100000`、`nMaxBoundary_noWrap` 用 `tier = 1000`,**均远超冻结的 `TIER_MAX = 3`**。域内最大档位和 = `n_max(64) × TIER_MAX(3) = 192`,`192 × 65536 = 12 582 912`,**int32 装得下** ⇒ 域内 int32 / int64 两实现在该夹具上**不可区分**。故这两条证明的是卡面 AC 第 6 条的**实现要求**(「乘加在 int64 内」),**不是**「域内值会溢出」—— 后者为假,不主张。域界本身由 story-001 的 schema 断言守。两处 doc 注释已就地写明。
4. **`ClinicAdjTable` 对称性计数修正**:首版按「有向条目数」比对自对,导致自对 `(bed,bed)` 被计 2 ⇒ 误报对称性缺失。已改为**按无序键首次插入时累加** `expectedDirected += isSelfPair ? 1 : 2`,并补「重复有向键」拒收。该缺陷由 `test_clinic_adjSum_nonFurnitureCellExcluded` 首跑暴露,非事后推断。

---

## 七、待回填 / 交接

- **story-001 的 Test Evidence**:三表产物 + 接线门本件已落,须回填至 story-001(`Status` 与 `Test Evidence` 两处),**不得**让 story-003 独占该证据。
- **story-003 转 `Complete` 的硬前置**:`FURN_TYPES` 闭集(§五 1)。在哨兵仍为 false 时,求值器在生产链路上无输入源。
