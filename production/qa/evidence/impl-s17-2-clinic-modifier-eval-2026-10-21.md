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

**测试载体**:`unity/Assets/Tests/EditMode/ClinicMachine/clinic_modifier_eval_test.cs`(~~**25 测**~~ **28 用例位** = `[Test]=24` + `[TestCase]=4`)· `clinic_machine_cooked_codec_test.cs`(18 测,含 2 条只读视图)· `unity/Assets/Tests/EditMode/DataPipeline/provider_wiring_gate_test.cs`(11 测)。
> 🔁 **2026-10-21 QA 签核轮补记**:**25 → 28** —— 补 `CheckedAdd` 守卫本体三条载体
> (`sumOverflowUpper_throws` / `sumOverflowLower_throws` / `sumExactlyMax_doesNotThrow`),
> 结清本件自陈的「`CheckedAdd` 守卫零载体」。证伪 **CA-M1 / CA-M2 / CA-M3 三条全 EXACT**
> (CA-M3 为 2026-10-21 补跑:`ca-m3.xml` = 28/27/1 恰红 `sumExactlyMax_doesNotThrow`,
> 还原后 28/28/0,还原 sha256 逐字节一致)。
> ⚠️ 本件是**实现轮证据原件**,补记不改其原判定 —— 三条新测属**签核轮**动作,另见
> `production/qa/qa-signoff-sprint-17-2026-10-21.md` §2。
> 🔁 **18 → 25(2026-10-21 性质测试补写轮)**:评审 BLOCKING Q7 的处置(用户裁定「选 C」)。
> 新增 7 个用例位,见 §八 之二。原文 18 保留作沿革。

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

### 3.2 性质测试补写轮的全量回归(2026-10-21 10:55 CST)

**基线溯源三元组**:截至时刻 `2026-10-21 10:55 CST` · 批次 `性质测试补写轮(用户裁定 C)` · commit `15fcda8` + 未提交改动。

| 套件 | XML | 结果 | 对照基线(同带三元组) |
|---|---|---|---|
| EditMode(全量) | `unity/Logs/s17-2-property-full.xml` | `total=4272 passed=4236 failed=0 skipped=36` | `4265 / 4229 / 0 / 36` @ `2026-10-21 10:17 CST` · `S17-4 收尾轮` · `15fcda8` |

**集合差对账(用 `fullname` 集合差,不用总数差)**:

```
基线 4265 → 本轮 4272      新增 7    消失 0
  + ClinicModifierEvalTest.test_clinic_envMod_property_anyInput_equalsLinearSum_noClamp
  + ClinicModifierEvalTest.test_clinic_envMod_property_anyMinMax_outputStillUnclamped(-1,1)
  + …                                                                    (-1000,1000)
  + …                                                                    (-32768,32768)
  + …                                                                    (-1000000,1000000)
  + ClinicModifierEvalTest.test_clinic_equipMod_property_anyTierVector_inClosedRange
  + ClinicModifierEvalTest.test_clinic_equipMod_negativeTier_excludedFromScore
```

`failed=0` · `skipped=36` 与基线同值 · **消失 0** ⇒ **判定:零回归**。

**收尾定向复跑**(注释与常量定稿后,非增量):`unity/Logs/s17-2-property-final.xml` = **`25 / 25 / 0 / 0`**。
复算命令:`unity test unity --mode EditMode --output <xml> --filter ClinicModifierEvalTest`。
(注意:这里用的是 10:17 的 S17-4 基线,**不是** §三 表里 09:02 的 S17-2 基线 —— 两者之间夹了 S17-4 的 5 个新测,
拿旧基线对账会把别人的增量算成自己的。)

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

### 4.4 性质测试补写轮(2026-10-21 · `tools/falsify/s17-2-property-round.sh`)

> **为什么另起一个脚本而不复用 `unity_falsify.py`**:本轮变异靶点(CLAMP 前后、`tier <= 0`)是**新增测试的直接宾语**,
> 而 `unity_falsify.py` 的 spec 锚点当时已 6/13 过期(评审 D8)。用独立脚本 = 变异与还原在同一进程、
> fail-closed 前置校验、还原后 SHA-256 逐字节比对,与驱动同款纪律,但不依赖过期 spec。

**驱动**:`tools/falsify/s17-2-property-round.sh`(备份 → 单点变异 → 跑 filter `ClinicModifierEvalTest` → `cp` 还原 → SHA-256 校验,同一进程内完成)。

| 变异 | 靶点 | 停用什么 | **预写预期** | **实测红集** | 判定 |
|---|---|---|---|---|---|
| M1-envClamp | `ClinicModifierEvaluator.cs:121` | `return new Fix(sum)` 前加 `±32768` 钳制 | 7 条 | **7 条**(见下) | ✅ 恰如预期 |
| M2-equipNoUpperClamp | `:163` | 删 `if (raw > EquipModCapRaw) raw = EquipModCapRaw;` | 6 条 | **6 条** | ✅ 恰如预期 |
| M3-tierLtToEq | `:150` | `tier <= 0` → `tier == 0` | 1 条 | **1 条** | ✅ 恰如预期 |

**M1 实测红集**(7):`envMod_aboveEnvModMax_passesThroughUnclamped` · `envMod_allNegative_passesThroughUnclamped` ·
`envMod_property_anyInput_equalsLinearSum_noClamp` · `envMod_property_anyMinMax_outputStillUnclamped(-1,1)` ·
`…(-1000,1000)` · `…(-32768,32768)` · `…(-1000000,1000000)`

**M2 实测红集**(6):`equipMod_largeScore_int64NoWrap` · `equipMod_negativeTier_excludedFromScore` ·
`equipMod_nMaxBoundary_noWrap` · `equipMod_positiveScores_haveNoIntermediateValue` ·
`equipMod_property_anyTierVector_inClosedRange` · `equipMod_sumAboveCap_pinsToCap`

**M3 实测红集**(1):`equipMod_negativeTier_excludedFromScore`

**还原**:三轮 `RESTORED_OK sha256=af9a8f51…d11ea47`(与 `ORIG_SHA` 逐字节相同)· 收尾 `FINAL_OK` 同值 · `FALSIFY_residue=0`。

#### ⚠️ 首轮实测暴露一处**真缺口**,已修并复跑 —— 这是本轮最有价值的产出

首轮(修复前)M1 **只红 5 条**,预写是 7。差的正是 `anyMinMax` 的 `(-1,1)` 与 `(-1000,1000)` 两组:

| 组 | 修复前 `expected = min + 3·max` | 是否落在冻结界 `±32768` 外 | 对「钳到 ±32768」可观测? |
|---|---|---|---|
| (-1, 1) | **2** | ✗ | **✗ 不可观测** |
| (-1000, 1000) | **2000** | ✗ | **✗ 不可观测** |
| (-32768, 32768) | 65536 | ✓ | ✓ |
| (-1e6, 1e6) | 2000000 | ✓ | ✓ |

**根因**:那两组样本和只有 2 / 2000,而实现里唯一可能的钳制**恰好就是钳到冻结界**
⇒ 「`MIN < MAX` 采样」这条测对这**两组**毫无判别力 —— 它们是**假绿**(S12-4 所定义的形态)。

**修法**:加一格常量贡献 `MinMaxSlackRaw = 100000`(`C3`),使**任何** (MIN, MAX) 下的和
① 严格越过本组 MAX(原命题)② 落在冻结界之外(对 M1 有判别力)。修后四组 = 100002 / 102000 / 165536 / 2100000,
**逐组都满足两条件**(算术核对已跑,见下)。

**复跑 M1 ⇒ 7 红,四组 anyMinMax 全红** —— 缺口闭合。

⚠️ **这处缺口是我写的测试自身的缺陷,由证伪轮抓出,不是被评审抓出。** 登记在此以证:
「性质测试」这一栏的绿,在本轮之前有一半是空的。

### 4.5 评审 D8 修复后的复跑(2026-10-21 11:2x CST · `--keep-logs`)

> **为什么有这一轮**:§4.4 的 M1/M2/M3 用的是独立脚本(`s17-2-property-round.sh`),
> 而**原三份 spec** 当时因锚点过期(驱动 exit 2)跑不了。评审 D8 修完后,spec 恢复可跑
> ⇒ 用**驱动**复跑。

⚠️ **本轮复跑的覆盖面 = eval(C1–C5)+ readonly(R1–R2),共 7 个变异。
`wiring` spec(W1/W2)本轮**未跑** —— 它的两个锚点已由 `--check-only` 证实对得上,
但**「锚点对得上」不等于「判别力已复验」**。W1/W2 的判别力证据仍是 §4.2 那一轮的实测
(`W1 红 2 · W2 红 1`),**不因本轮而更新**。禁借绿:此处不把 W1/W2 并进「本轮已复跑」。

| spec | 变异 | 预写 `expectRed` | 实测 | 判定 |
|---|---|---|---|---|
| eval | C1-envUnclamped | 7 | **7** | ✅ |
| eval | C2-roomNoneNeutral | 2 | **2** | ✅ |
| eval | C3-adjTwoDirections | 4 | **4** | ✅ |
| eval | C4-equipCapRemoved | 7 | **7** | ✅ |
| eval | C5-nonFurnitureSkipped | 1 | **1** | ✅ |
| readonly | R1-contextRowsBareArray | 1 | **1** | ✅ |
| readonly | R2-roomRoomsBareArray | 1 | **1** | ✅ |

**C3 / C4 的 `expectRed` 从 3 / 6 改为 4 / 7 —— 依据是先论证,不是照抄实测**:
两处多出的红都是 **S17-4(2026-10-21)新加的** `test_traitsSource_feedsEvaluator_endToEnd`
(`clinic_furniture_catalogue_test.cs`)。该测断言
`env.Raw == Fix.OneRaw/8`(C3 加 −x 反向 ⇒ adj 翻倍 ⇒ 必红)与
`equip.Raw == EquipModCapRaw`(C4 删上钳 ⇒ raw 留 65536 ⇒ 必红)。
论证写在 spec 的 `_expectRed_justification` 字段里,**不是把实得抄进 `exactRed`**
(`tools/falsify/README.md` 坑 2)。

**还原**:每轮 `──── 还原:SHA-256 逐字节校验通过 ────` · 受保护路径
(`unity/Assets/DataCooked` + `unity/Assets/AddressableAssetsData`)指纹前后未变。

**驱动的 verdict 逐字(不合并两轮)**:

| 轮 | spec | 末行 | exit |
|---|---|---|---|
| 复跑 A | `modifier-eval` | `══ 证伪轮**未通过**(退出码 1)══` | 1 —— **C3 预期当时还是旧 3 条** |
| 复跑 A | `readonly` | `══ 证伪轮通过:2 个变异全部恰如预期 ══` | 0 |
| 复跑 B | `modifier-eval` | `══ 证伪轮通过:5 个变异全部恰如预期 ══` | **0** |

复跑 A 的 exit 1 **不抹掉**(它是「改预期前实跑」的真实记录);
改完 C3 预期后补跑复跑 B,5/5 恰如预期 ⇒ **C3 的 4 条现在是实跑一致,不是推断**。

---

## 五、未及核查 / 未跑项(如实登记,禁借绿)

1. ~~**`FURN_TYPES` 闭集未兑现**(`O-24-1` / `TD-clinic-01`):生产侧**没有** `IClinicFurnitureTraitsSource` 的实现者。求值器只由测试注入夹具源;哨兵 `ClinicModifierEvaluator.FurnTraitsProductionReady => false` 恒 false,且 `ClinicRoomExtraction.FurnTypeClosureEnforced => false` 有一条 Sim 装配扫描测试(C11)使其**可检测**而不仅可观测。
   ⇒ **本件不主张「求值器已在生产链路上跑过」** —— 它跑在夹具源上。~~
   > 🔁 **2026-10-21 稍后已兑现(S17-4)**:`O-24-1` 落地,生产属性源 =
   > `Sim/Clinic/ClinicFurnitureTraitsSource.cs`,哨兵翻 **true**。
   > 本条**作为当时状态的记录保留**;现状见
   > `production/qa/evidence/impl-s17-4-furniture-catalogue-2026-10-21.md`。
   > ⚠️ `FurnTypeClosureEnforced` **仍为 false**(默认入口走代理判据,装配层未选真判据)—— 那一条不变。
2. **「布局 → 房间类型」匹配(ROOM_TABLE 求值)未实现**:`base_env` 由调用方经 `ClinicContextTable.TryGetBaseEnv` 解析后传入;求值器不假装房间匹配已存在。
3. **IL2CPP 三格 / 跨平台逐位**(ADR-012 矩阵):NOT-RUN,本机仅 Mono。<br>⚠️ **2026-10-21 订正:本句「本机仅 Mono」为假** —— ADR-012 §Validation 2026-10-17 已逐格实测:Linux-x64-Mono ✅ 09-24(3/3)· **Linux-x64-IL2CPP ✅ 09-24**(StandaloneLinux64 player 3/3,与独立 Python 金标准 + Mono 三方 19/19 逐位相同)· Linux-ARM64-IL2CPP ❌ **目标不存在**(pinned 工具链 46 个 BuildTarget 无任何 arm 目标;用户裁定长期未达)· CI 常驻 ⚠️ job 已建但从未跑(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET`)。⇒ **矩阵级,非本件个体判据**。原文保留不删。
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

> 🔁 **2026-10-21 补记:本条硬前置 ① 已于同日由 S17-4 兑现**(`FurnTraitsProductionReady` 翻 true)。
> 原文保留作沿革;story-003 的当前状态见 §八之二。

- **story-001 的 Test Evidence**:三表产物 + 接线门本件已落,须回填至 story-001(`Status` 与 `Test Evidence` 两处),**不得**让 story-003 独占该证据。
- **story-003 转 `Complete` 的硬前置**:`FURN_TYPES` 闭集(§五 1)。在哨兵仍为 false 时,求值器在生产链路上无输入源。
