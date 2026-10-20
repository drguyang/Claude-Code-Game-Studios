# 实现轮 —— 6 份欠账的运行期装载面接线 · 第二轮(剩 4 份)

**日期**: 2026-10-20
**用户裁定**:**「把剩下 4 份也接上」**
**前置**: `bdadfb3`(路 A 接 2 份 + 接线门 7 用例)· `08fb813`(6 份欠账补烘)
**系统归属**: 跨系统数据管线基建(`DataPipeline`),不属单一 epic

---

## §0 本件补的是哪条线

路 A 已把 6 份里的 2 份接进运行期装载面并立了接线门。剩下 4 份之所以被推后,原因是
**它们没有运行期解码器与数据集类型**,不是「不需要接线」:

| 产物 | 路 A 时的缺口 | 本轮补的口 |
|---|---|---|
| `prescription_actions` | 无 codec;`PrescriptionActionRow` 住 `Editor.Tools.Bake`(编辑期装配,不进玩家构建) | 新 codec + 自带同名只读行 |
| `disease_action_axis` | 无 codec;**写方自注「与 `Gameplay.Presentation.DiseaseActionAxisCookedCodec` 严格镜像」,而该类型从未存在** | 新 codec(**并修正写方的契约违例**,见 §2)+ 落盘该类型 |
| `53_outcome_table` | 无 codec;`Outcome53.Table` 住 `Sim`,其自注「运行期生产装载归装配轮」 | 新 codec,解码后直接物化 `Outcome53.Table` |
| `53_delay_table` | 无 codec;`DelayTable53.Table` 同上 | 新 codec,解码后直接物化 `DelayTable53.Table` |

---

## §1 改动落点

| # | 文件 | 改动 |
|---|---|---|
| D-1 | `unity/Assets/Gameplay.Presentation/CookedReadUtil.cs`(**新**) | 头部校验 / `i32 长度 + UTF-8` 字符串读 / 载荷耗尽断言的**共享件**(本轮 4 个 codec 共用) |
| D-2 | `unity/Assets/Gameplay.Presentation/PrescriptionActionsCookedCodec.cs`(**新**) | 处方表 codec + `PrescriptionActionCookedRow` + `PrescriptionActionsDataSet` |
| D-3 | `unity/Assets/Gameplay.Presentation/DiseaseActionAxisCookedCodec.cs`(**新**) | 处置轴 codec + `ActionAxisCookedRow` / `TreatableByCookedRow` + `DiseaseActionAxisDataSet` |
| D-4 | `unity/Assets/Gameplay.Presentation/OutcomeTableCookedCodec.cs`(**新**) | 判定表 codec + `OutcomeTable53DataSet`(解码后交 `Outcome53.CreateTable`) |
| D-5 | `unity/Assets/Gameplay.Presentation/DelayTableCookedCodec.cs`(**新**) | 延迟表 codec + `DelayTable53DataSet`(解码后交 `DelayTable53.CreateTable`) |
| D-6 | `unity/Assets/Editor.Tools.Bake/DiseaseActionAxisCookedWriter.cs` | **契约违例修正**:字符串由 `BinaryWriter.Write(string)`(7-bit 变长)改为 `i32 字节长 + UTF-8` |
| D-7 | `unity/Assets/DataCooked/disease_action_axis.cooked.bytes` | 因 D-6 **重烘**:**304 → 370 B**(含 20 B 头),`ConfigVersion = 0x6C83B441` |
| D-8 | `unity/Assets/Gameplay.Presentation/AddressablesDataProvider.cs` | 4 个 address 常量 · 4 个可空字段 · 4 个 `Load<T>` 分支 · 4 次 `Preload()` 调用 · 4 个 `Ensure*` 方法 · 未知类型错误串订正 |
| D-9 | `unity/Assets/Gameplay.Presentation/DataCorePreloader.cs` | `DataCoreAddresses` 11 → **15**;同步订正「11 次反射」「共 11 条」等陈旧计数 |
| D-10 | `unity/Assets/AddressableAssetsData/AssetGroups/data-core.asset`(**入库**) | 组条目 11 → **15**(按 GUID 序插入,`m_Address` 与产物 `.meta` 的 GUID 逐条对齐) |
| D-11 | `unity/Assets/Tests/EditMode/DataPipeline/provider_wiring_gate_test.cs` | 7 → **11** 用例(① 断言补 4 份、④ 的扫描键守卫下限 11 → 15、新增 4 条 `LoadBranch`) |
| D-12 | `unity/Assets/Tests/EditMode/DataPipeline/cooked_codec_roundtrip_test.cs`(**新**) | **读方 ↔ 写方镜像门**:4 份产物「盘上字节」与「现烘字节」解码后语义相等 |
| D-13 | `unity/Assets/Tests/PlayMode/precheck_allKeys_test.cs` | 注释里的地址条数 11 → 15(纯注释,无判据) |

> `Preload()` / `Load<T>` / `Ensure*` 三处**形状一字未改** —— 新增的是同形状的第 12–15 个实例,
> 没有引入新抽象。E-13 硬失败语义保持不变。

---

## §2 ⚠️ 本轮抓出的契约违例(不是本轮引入,是本轮**首次可见**)

**事实**:`DiseaseActionAxisCookedWriter` 用 `w.Write(row.Name ?? "")` 写字符串 ——
`BinaryWriter.Write(string)` 的编码是 **7-bit 变长前缀 + UTF-8**;
而 `Sim.Contracts/CookedFormat.cs` 明文规定载荷内字符串 = **`i32` 字节长 + UTF-8**(无终止符),
九个姊妹写方全部照此实现。

**为什么长期不可见**:该产物**此前全库零读方**(`DiseaseActionAxisCookedCodec` 从未存在),
而漂移门只比对「盘上字节 == 现烘字节」—— 只要写方自洽,字节就是「稳定但错」。
**它第一次暴露,正是因为本轮首次为它写读方**:按 `CookedFormat` 读 ⇒ 错位;
按 7-bit 读 ⇒ 0 剩余字节(实测)。

**处置(本次)**:修正**写方**(位于契约的下游,是它偏离了文档),然后**重烘**产物。
理由:若反过来让读方去实现 7-bit,等于把一处已明文记录的偏离固化成永久行为 ——
后续任何按 `CookedFormat` 写读方的实现都会静默失效。修写方能让三处(文档 / 写方 / 读方)重新同形。

**代价(如实登记)**:
- 产物字节变了(**304 → 370 B**,逐字节见 git diff):**旧 7-bit 产物与新读方不兼容** ——
  盘上产物已随修正重烘,`cooked_drift_all_artifacts_test` 的
  `drift[disease_action_axis.cooked.bytes]` 证明盘上与现烘一致。
- 全库仍**只有这一份**产物受此历史影响;其余 14 份写方均按 i32。

**回归夹具**:`CookedCodecRoundTripTest.test_roundTrip_diseaseActionAxis_diskMatchesFreshBake`
—— 把写方改回 7-bit ⇒ 该测**转红**(证伪轮 F7 实测)。

---

## §3 门真身

### 3.1 接线门扩展(`provider_wiring_gate_test.cs`,7 → 11 用例)

| 用例 | 判据 | 失效模式 |
|---|---|---|
| `test_wiring_newArtifactAddresses_matchBakerArtifactNames` | 6 组 address 字面串 == `DataBakeMenu.*CookedAssetName` | ① |
| `test_wiring_dataCoreGroup_containsEveryProviderAddress` | 每个 `*Address` 常量在组里**恰 1 条**(守卫下限升到 15) | ② |
| `test_wiring_dataCoreGroup_entriesResolveToExistingArtifacts` | 组每条 → `DataCooked/` 有真文件 | ② |
| `test_wiring_dataCoreGroup_guidsMatchArtifactMeta` | 组 `m_GUID` == 产物 `.meta` 的 guid | ③ |
| `test_wiring_preload_invokesEveryEnsureMethod` | `Ensure*` 集 == `Preload()` 调用集 == 地址表集 | ④ |
| `test_wiring_loadBranch_{interactionKinds,negativeConfidence,prescriptionActions,diseaseActionAxis,outcomeTable,delayTable}_exists` | `Load<T>` 走到装载分支(非「未知类型」兜底) | 分支缺失 |

### 3.2 新增:读方镜像门(`cooked_codec_roundtrip_test.cs`,4 用例)

对每份产物,分别解码**盘上已提交字节**与 **baker 现烘字节**,断言语义相等:

| 用例 | 断言面 |
|---|---|
| `test_roundTrip_prescriptionActions_diskMatchesFreshBake` | `DOSE_BASE` / `MAX_DOSE_DETENTS` / `single_dose_max` / `ConfigVersion` + 逐行三字段 |
| `test_roundTrip_diseaseActionAxis_diskMatchesFreshBake` | 处置轴逐行三字段 + treatable_by 逐行三字段 + `ConfigVersion` |
| `test_roundTrip_outcomeTable_diskMatchesFreshBake` | 表元数据 + **全键域逐点对拍**(18 组合 × 档数) |
| `test_roundTrip_delayTable_diskMatchesFreshBake` | 定义域三支的 `delay_ticks` 逐支相等 |

> **为什么漂移门不够**:漂移门只回答「字节是否等于现烘」,**不回答「有人读得懂」**。
> 本轮 §2 的违例正是「字节稳定、写读不同形」—— 只有镜像门能抓。
> ⚠️ 且 `OutcomeTable` 用例只比行数会漏掉「键错位但行数相等」,故取**全键域逐点对拍**。

---

## §4 证伪轮(改 → 跑 → 还原,同一命令内;SHA-256 逐字节校验)

脚本原件 `/tmp/fz-wiring-b.py`(临时件,不入库)。基线:`ProviderWiringGateTest 11/11` ·
`CookedCodecRoundTripTest 4/4`。

| # | 变异 | 期望 | 实测 | 还原 |
|---|---|---|---|---|
| **F1** | `PrescriptionActionsAddress` 字面串改一字(`prescription_actions` → `prescription_actions_XX`) | ① 红 | ✅ `9/2` —— `newArtifactAddresses` + `dataCoreGroup_containsEvery…` | sha256 一致 |
| **F2** | 组里删掉 `53_delay_table` 整条 entry | ② 红 | ✅ `10/1` —— `dataCoreGroup_containsEvery…` | sha256 一致 |
| **F3** | 组条目 GUID 末位 `c → d` | ③ 红 | ✅ `10/1` —— `dataCoreGroup_guidsMatchArtifactMeta` | sha256 一致 |
| **F4** | `Preload()` 删掉 `EnsureDelayTable();` | ④ 红 | ✅ `10/1` —— `preload_invokesEveryEnsureMethod` | sha256 一致 |
| **F5** | 删掉 `Load<T>` 的 `DelayTable53DataSet` 整块分支 | 分支缺失红 | ✅ `10/1` —— `loadBranch_delayTable_exists` | sha256 一致 |
| **F6** | `DataCoreAddresses` 删掉 `DelayTableAddress` | ④ 红 | ✅ `10/1` —— `preload_invokesEveryEnsureMethod` | sha256 一致 |
| **F7** | 写方回退 `BinaryWriter.Write(string)`(7-bit 变长) | 镜像门红 | ✅ `3/1` —— `roundTrip_diseaseActionAxis_diskMatchesFreshBake` | sha256 一致 |

**7/7 全部转红**;每例 `finally` 还原后 **sha256 逐字节一致**。

- **F1 红 2 条是预期**:它改坏字面串 ⇒ 组里自然查不到该 address,故 ① 与 ② 同红;
  其余六例各**仅**红 1 条 ⇒ 七条之间隔离良好。
- **F7 是本轮最有信息量的一例**:它直接复现 §2 的历史违例,证明镜像门对该形态**有判别力**,
  不是「跑通即绿」。

---

## §5 连带处理:本机 Addressables catalog 重建

组条目 11 → 15 ⇒ 旧 catalog 查不到 4 个新 address。已重建:

```
unity build <unity/> --target StandaloneLinux64 \
  --execute-method DaYiJingCheng.EditorTools.Bake.DataBakeMenu.BuildAddressablesContent
⇒ [大医精诚] Addressables 内容已构建 —— catalog 现含全部组条目。
⇒ Exiting batchmode successfully now!
```

重建后核对(`strings` 子串匹配 —— 相邻字节会粘在串尾,不能用行首行尾锚):

```
HIT ×15 / 15  (item_database_items · item_database_recipes · diagnosis_signs ·
               diagnosis_read_floor · diagnosis_negative_confidence · case_judgment_lexicon ·
               audio_events · audio_settings_registry · input_device_switch · telemetry_analytics ·
               interaction_kinds · prescription_actions · disease_action_axis ·
               53_outcome_table · 53_delay_table)
```

> ⚠️ `unity/Library/.../catalog.bin` 是**机器本地产物**(gitignored)。跨机复现前须先跑该菜单。

---

## §6 完成度(双列 —— Landed / Proven)

| # | 内容 | Landed | Proven | Proven-by |
|---|---|---|---|---|
| D-1 | 共享读工具 | ✅ | ✅ | 4 个 round-trip 用例全绿(每个都走它) |
| D-2..D-5 | 4 个 codec + 数据集类型 | ✅ | ✅ | `CookedCodecRoundTripTest` 4/4 |
| D-6 | 写方契约修正 | ✅ | ✅ | F7 转红(回退即红) |
| D-7 | 产物重烘 | ✅ | ✅ | `drift[disease_action_axis.cooked.bytes]` |
| D-8 | address / 字段 / 分支 / Preload / Ensure | ✅ | ✅ | F1 · F4 · F5 · F6 |
| D-9 | `DataCoreAddresses` 15 条 | ✅ | ✅ | F6 + `test_wiring_preload_invokesEveryEnsureMethod` |
| D-10 | 组条目 15 条 | ✅ | ✅ | F2 · F3 |
| D-11 | 接线门 11 用例 | ✅ | ✅ | 基线 11/11 + F1–F6 |
| D-12 | 镜像门 4 用例 | ✅ | ✅ | 基线 4/4 + F7 |
| D-13 | PlayMode 注释订正 | ✅ | ✗ | 纯注释,无判据(如实登记) |

---

## §7 ⚠️ 必须如实登记的边界(禁借绿)

### 7.1 4 个新数据集在运行期仍然**零读者**

实测(排除 `Tests/` 与类型自身定义):

```
grep -rn "Load<PrescriptionActionsDataSet>\|Load<DiseaseActionAxisDataSet>" unity/Assets --include=*.cs
grep -rn "Load<OutcomeTable53DataSet>\|Load<DelayTable53DataSet>" unity/Assets --include=*.cs
(均为空)
```

- `Outcome53.Judge.Evaluate` 在**生产侧零调用点**(只在测试里被驱动)——本件不改变这一点。
- `ConsequenceEmitter`(消费 `DelayTable53.Table`)仍**只在测试里被构造**。

⇒ **本轮做的是「可装载」,不是「已被使用」。** 读者归各 epic 的消费轮(53 归故事
`story-003` 的发射路径接线,11 归处方结算,9 归处置轴门)。

### 7.2 `has_default` 位不在载荷里

`53_outcome_table` 的载荷只有 `u_band_count` / `default_outcome` / `row_count` / 行集 ——
**没有 `has_default` 标志**。读方据此传 `hasDefault: true`,理由是
`OutcomeTableBinder.ValidateCoverage` 硬拒无 default 行的表 ⇒ 一切合法产物的该位恒真。
**这是从烘焙期不变量推出的,不是载荷携带的** —— 已在 codec 文件头与数据集注释里写明。
若要读方独立判别,须升 `CookedFormat.FormatVersion`(不在本件面内)。

### 7.3 catalog 是机器本地产物(见 §5)

### 7.4 IL2CPP 三格(ADR-012 矩阵)NOT-RUN —— 本机仅 Mono

---

## §8 回归

| 项 | 值 |
|---|---|
| 命令 | `unity test <unity/> --mode EditMode --output <绝对路径>` |
| 本轮 | **`total=3988 passed=3952 failed=0 inconclusive=0 skipped=36`**(exit **0**) |
| 前次基线 | `3980 / 3944 / 0 / 36`(`路 A 接线轮`,原件 `impl-data-core-wiring-a-2026-10-20.md` §8) |
| Δ | **`+8 / +8 / 0 / 0`** |
| 等式判据 | `total − 前次 = 新增数`(4 条 `LoadBranch` + 4 条镜像门 = 8)⇒ **✅ 精确成立,零回归** |

> **三元组(强制)**:截至时刻 `2026-10-20 11:30 本地`(03:28–03:30 UTC)· 批次
> `第二轮接线轮(全量 EditMode)` · commit `bdadfb3` + 未提交工作树
> (5 新 codec/工具 + 1 测试 + 6 生产改动 + 1 产物 + 1 账本)。
> 结果原件 `unity/Logs/wiring-b-full.xml`。
>
> ⚠️ **`+8` 是实跑出来的**,不是照抄 —— 前次 `3980` 亦实跑且带三元组(原件 §8 可复算)。
> ⚠️ 本轮**定向**另有两份:`unity/Logs/wiring-b-gate.xml`(11/11)与
> `unity/Logs/wiring-b-roundtrip.xml`(4/4)—— 与全量里这些用例**同结论**。
> ⚠️ 根属性 `result="Skipped:Ignored"` 是 NUnit3 对 36 条 `[Ignore]` 的既有形态,
> `failed=0` 为准;历轮同形。

---

## §9 定向与全量的分工

| 项 | 定向 | 全量 |
|---|---|---|
| 命令 | `--filter ProviderWiringGateTest` / `--filter CookedCodecRoundTripTest` | 无 filter |
| 结果 | **11/11** · **4/4** | **3988 / 3952 / 0 / 36** |
| 证明什么 | 本门自身可判、且证伪轮后仍绿 | 未打断任何既有测试 |

定向不能说「零回归」,全量不能说「本门有判别力」—— 两者不可互相顶替。
