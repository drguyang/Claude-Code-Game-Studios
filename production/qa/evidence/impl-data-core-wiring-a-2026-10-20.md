# 实现轮 —— 6 份欠账的「运行期装载面接线」路 A(2/6)

**日期**: 2026-10-20
**用户裁定**:**「把那 6 份接到运行期装载面」→ 路 A**(只接已具备全部前置的 2 份)
**前置**: `08fb813`(6 份欠账补烘)· `tests/integration/item_database/README.md` §6 份欠账的发现与补烘
**系统归属**: 跨系统数据管线基建(`DataPipeline`),不属单一 epic

---

## §0 本件补的是什么线 —— 「补烘 ≠ 接线」

`08fb813` 把 6 份「有 baker 无产物」的欠账全部补烘落盘(盘上 9 → 15),**字节正确**
(`ConfigVersion` 全部非零 = 内容哈希链路生效)。但补烘只解决**产物存在**。

**运行期装载是另一条线,由四处共同成立,少一处即静默失效**:

| # | 要素 | 失了的症状 |
|---|---|---|
| ① | `AddressablesDataProvider` 的 `*Address` 常量 | 无处可查 |
| ② | `data-core` Addressables 组条目 | 启动预载 `FetchBytes` 直接 **E-13 硬失败**,整链起不来 |
| ③ | 组条目 `m_GUID` ↔ 产物 `.meta` | 条目指向虚空(YAML 合法、address 对得上,**只有打包时才炸**) |
| ④ | `Preload()` 调用 + `DataCorePreloader.DataCoreAddresses` | 方法存在但缓存永不填充,首次 `Load` 在**远处**炸 |

⇒ 补烘前,这 6 份**① ② ④ 三条全缺**、③ 不适用(盘上无产物)。所谓「5 个 story 全部
`Complete`」里有一件名字就叫 `story-007-interaction-kinds-bake-wiring` ——
**那个「烘焙接线」指的是烘焙管线接线,不是运行期装载**。两者是不同的失效面,不可互相顶替。

---

## §1 路 A 的范围与选择理由

**用户裁定「路 A」** —— 只接**已具备全部前置**的 2 份:

| 产物 | 已有运行期解码器 | 已有数据集类型 | 本轮状态 |
|---|---|---|---|
| `interaction_kinds` | ✅ `InteractionKindCookedCodec.Read` | ✅ `InteractionKindDataSet` | ✅ **已接线** |
| `diagnosis_negative_confidence` | ✅ `DiagnosisNegativeConfidenceCookedCodec.Read` | ✅ `DiagnosisNegativeConfidenceDataSet` | ✅ **已接线** |
| `prescription_actions` | ✗ | ✗(`PrescriptionActionRow` 住 `Editor.Tools.Bake`) | ⏸ 另开一轮 |
| `disease_action_axis` | ✗ | ✗(`ActionAxisRow` 同上) | ⏸ 另开一轮 |
| `53_outcome_table` | ✗ | ✗(`Outcome53.Table` 住 `Sim`) | ⏸ 另开一轮 |
| `53_delay_table` | ✗ | ✗(`DelayTable53.Table` 同上) | ⏸ 另开一轮 |

后 4 份各需**新的运行期数据集类型 + 解码器**(每份约 120–220 行),且
`Outcome53.CreateTable` 的自注即写「运行期生产装载归装配轮」⇒ 单独一轮。

---

## §2 改动落点

| # | 文件 | 改动 |
|---|---|---|
| C-1 | `unity/Assets/Gameplay.Presentation/AddressablesDataProvider.cs` | 新增 `InteractionKindsAddress` / `DiagnosisNegativeConfidenceAddress` 两常量(字面串 + 注明为何不能引 `Editor.Tools.Bake`:那是编辑期装配,不进玩家构建) |
| C-2 | 同上 | 新增 `_interactionKinds` / `_negativeConfidence` 两个可空字段 |
| C-3 | 同上 | `Load<T>` 新增 2 个分支;未知类型错误串同步订正(补枚举这两个类型) |
| C-4 | 同上 | `Preload()` 追加 `EnsureInteractionKinds(); EnsureDiagnosisNegativeConfidence();` |
| C-5 | 同上 | 新增 `EnsureInteractionKinds()` / `EnsureDiagnosisNegativeConfidence()`,严格照既有 `[E-13]` try/catch 形状 |
| C-6 | `unity/Assets/Gameplay.Presentation/DataCorePreloader.cs` | `DataCoreAddresses` 9 → **11** 条(补两个新地址);同步订正「共 9 条」「代价 = 7 次反射」等陈旧计数 |
| C-7 | `unity/Assets/AddressableAssetsData/AssetGroups/data-core.asset`(**入库**) | 新增 2 条 group entry(按 GUID 序插入各自的 `m_GUID` / `m_Address` / `m_ReadOnly: 0` / `m_SerializedLabels: [data-core]` / `FlaggedDuringContentUpdateRestriction: 0`) |
| C-8 | `unity/Assets/Tests/EditMode/DataPipeline/provider_wiring_gate_test.cs`(**新**) | 7 用例接线门(见 §3) |
| C-9 | `unity/Assets/Tests/PlayMode/precheck_allKeys_test.cs` | 订正注释里的地址条数 9 → 11 |

> `Preload()` / `Load<T>` / `Ensure*` 三处**形状一字未改** —— 新增的是同形状的第三/第四个实例,
> 没有引入新抽象。E-13 硬失败语义保持不变。

---

## §3 门真身(7 用例 · `provider_wiring_gate_test.cs`)

| 用例 | 判据 | 对应 §0 的失效模式 |
|---|---|---|
| `test_wiring_newArtifactAddresses_matchBakerArtifactNames` | address 字面串 == `DataBakeMenu.*CookedAssetName` | ① |
| `test_wiring_dataCoreGroup_containsEveryProviderAddress` | 每个 `*Address` 常量在组里**恰 1 条** | ② |
| `test_wiring_dataCoreGroup_entriesResolveToExistingArtifacts` | 组每条 → `DataCooked/` 有真文件 | ② |
| `test_wiring_dataCoreGroup_guidsMatchArtifactMeta` | 组 `m_GUID` == 产物 `.meta` 的 `guid` | ③ |
| `test_wiring_preload_invokesEveryEnsureMethod` | `Ensure*` 方法集 == `Preload()` 调用集 == `DataCoreAddresses` 集 | ④ |
| `test_wiring_loadBranch_interactionKinds_exists` | `Load<T>` 走到装载分支 | 分支缺失 |
| `test_wiring_loadBranch_negativeConfidence_exists` | 同上 | 同上 |

### 3.1 两个 `LoadBranch` 用例的判别法(不依赖装载成功)

EditMode 下**无 Addressables locator** ⇒ `Load<T>` 必失败。若断言「不抛」,则本测在 EditMode
**不可实现**;若断言「抛了就算」,则**零判别力**(未知类型兜底也抛)。

⇒ 判别落在**失败报文**上,两条路径互斥:

| 路径 | 报文特征 |
|---|---|
| 走到装载分支 | 含产物**基名**(`FetchBytes` 报全文 address;codec 报 `[E-13] <基名> cooked:…`) |
| 落到未知类型兜底 | 含「未知数据集类型」 |

`LogAssert.ignoreFailingMessages` 只关**日志门**,不断言执行 —— 断言仍逐个跑。

---

## §4 证伪轮(停用 X ⇒ 断言必须转红;同命令内还原 + SHA-256)

**纪律**:禁 `git checkout` / `git stash`(会抹未提交改动)⇒ 备份 → 单点变异 → 跑 → `cp` 回 →
`sha256` 逐字节比对。`unity test --output` **按 cwd 解析** ⇒ 一律绝对路径。
脚本原件 `/tmp/fz-wiring.py`(临时件,不入库)。

**基线**:`7 / 7 passed`,0 failed。

| # | 变异 | 期望 | 实测 |
|---|---|---|---|
| **F1** | `InteractionKindsAddress` 字面串改一字(`interaction_kinds` → `interaction_kinds_XX`) | ①红 | ✅ `5/2` —— `newArtifactAddresses` + `dataCoreGroup_containsEvery…` **2 红** |
| **F2** | 组里删掉 `diagnosis_negative_confidence` 整条 entry | ②红 | ✅ `6/1` —— `dataCoreGroup_containsEvery…` |
| **F3** | 组条目 GUID 末位 `8 → 9` | ③红 | ✅ `6/1` —— `dataCoreGroup_guidsMatchArtifactMeta` |
| **F4** | `Preload()` 删掉 `EnsureInteractionKinds();` | ④红 | ✅ `6/1` —— `preload_invokesEveryEnsureMethod` |
| **F5** | 删掉 `Load<T>` 的 `InteractionKindDataSet` 整块分支 | 分支缺失红 | ✅ `6/1` —— `loadBranch_interactionKinds_exists` |
| **F6** | `DataCoreAddresses` 删掉 `InteractionKindsAddress` 一行 | ④红 | ✅ `6/1` —— `preload_invokesEveryEnsureMethod` |

**6/6 全部转红**;每个 `finally` 还原后 `sha256` **逐字节一致**,`git status` 与变异前同形。

**F1 红 2 条是预期,不是误伤**:它改的是 address 字面串 ⇒ 组里自然查不到那个 address,
故 ① 与 ② 同红。其余五例各**仅**红 1 条,证明七条之间隔离性好。

---

## §5 连带处理:本机 Addressables catalog 必须重建

`unity/Library/com.unity.addressables/aa/Linux/catalog.bin` 是**机器本地产物**
(`.gitignore:61` `[Ll]ibrary/`),不入库。组条目加了 2 条 ⇒ 旧 catalog 查不到新 address。

**实测证据(重建前)**:

```
MISS interaction_kinds.cooked.bytes
MISS diagnosis_negative_confidence.cooked.bytes
HIT  diagnosis_signs.cooked.bytes
HIT  audio_events.cooked.bytes
```

⇒ 不重建的话,PlayMode 的 `test_precheck_liveCatalog_allRealAddressesLocatable` 会**判死**
(该测的设计意图正是「组条目 ↔ 真 catalog」的一致性)。

**重建**:`unity build <unity/> --target StandaloneLinux64 --execute-method
DaYiJingCheng.EditorTools.Bake.DataBakeMenu.BuildAddressablesContent`
⇒ 判决行 `[大医精诚] Addressables 内容已构建 —— catalog 现含全部组条目。`
· `Exiting batchmode successfully now!` · exit **0**

**重建后核对**:catalog 内 `*.cooked.bytes` address **11 条**,含新接线的 2 条:

```
audio_events · audio_settings_registry · case_judgment_lexicon
diagnosis_negative_confidence ✅ · diagnosis_read_floor · diagnosis_signs
input_device_switch · interaction_kinds ✅ · item_database_items
item_database_recipes · telemetry_analytics
```

> ⚠️ **这不是缺陷,是 E-13 的设计行为**:跨机复现前须先跑该菜单。本机已重建。

---

## §6 注册的完成度(双列 —— Landed / Proven)

| # | 内容 | Landed(已落) | Proven(已证) | Proven-by |
|---|---|---|---|---|
| C-1 | address 常量 ×2 | ✅ | ✅ | F1 转红:`newArtifactAddresses_matchBakerArtifactNames` |
| C-2 | 可空字段 ×2 | ✅ | ✅ | 由 C-3/C-5 联合覆盖(编译 + F5) |
| C-3 | `Load<T>` 分支 ×2 | ✅ | ✅ | F5 转红:`loadBranch_interactionKinds_exists` |
| C-4 | `Preload()` 追加调用 | ✅ | ✅ | F4 转红:`preload_invokesEveryEnsureMethod` |
| C-5 | `Ensure*` 方法 ×2 | ✅ | ⚠️ **部分** | F4/F6 证明**被调用**;方法体内的 E-13 形状由既有 9 个同形实例承(未单独反证) |
| C-6 | `DataCoreAddresses` 11 条 | ✅ | ✅ | F6 转红 + `test_preloader_list_sources_matchPreloadedProducts` |
| C-7 | 组条目 ×2 | ✅ | ✅ | F2/F3 转红 |
| C-8 | 接线门 7 用例 | ✅ | ✅ | 基线 7/7 + F1–F6 6/6 |
| C-9 | PlayMode 注释订正 | ✅ | ✗ | 纯注释,无判据(如实登记) |

---

## §7 ⚠️ 必须如实登记的三条边界(禁借绿)

### 7.1 两个新数据集在**运行期零读者**

实测:

```
grep -rn "Load<InteractionKindDataSet>\|Load<DiagnosisNegativeConfidenceDataSet>" unity/Assets/ --include=*.cs | grep -v /Tests/
(空)
grep -rn "DiagnosisNegativeConfidenceTable" unity/Assets/ --include=*.cs | grep -v /Tests/ | grep -v 本类型/本 codec
(空)
```

更具体的一条:`KindPriorityTable.Priorities`(交互系统 F-4.1 第二键的运行期表)至今是
**硬编码 `Dictionary`**,注释自陈「值归数值轮 —— 此处仅为接线可测」——
它**不消费**任何 cooked 数据。`story-007 ## Dependencies` 里写的
「Unlocks: 运行期 `ImmutableInteractionKindTable`」在仓库里**不存在**。

⇒ **本轮做的是「可装载」,不是「已被使用」。** 读者归各 epic 的消费轮。
不得以本件声称 AC 已闭环。

### 7.2 catalog 是机器本地产物(见 §5)

新检出 / 未重建 catalog 的机器上,PlayMode `test_precheck_liveCatalog_allRealAddressesLocatable`
会判死。**这是设计行为,不是回归** —— 但它意味着「本件在别处全绿」不可假定。

### 7.3 其余 4 份仍未接线

见 §1 表。它们各自需要新的运行期数据集类型 + 解码器,另开一轮。
本件**不解除**那 4 份的接线义务。

---

## §8 回归

| 项 | 值 |
|---|---|
| 命令 | `unity test <unity/> --mode EditMode --output <绝对路径>` |
| 本轮 | **`total=3980 passed=3944 failed=0 inconclusive=0 skipped=36`**(exit **0**) |
| 前次基线 | `3973 / 3937 / 0 / 36`(`unity/Logs/s15-bake6-full.xml`,6 份欠账补烘轮) |
| Δ | **`+7 / +7 / 0 / 0`** |
| 等式判据 | `total − 前次 = 新增数`(7 条接线测)⇒ **✅ 精确成立,零回归** |

> **三元组(强制)**:截至时刻 `2026-10-20 11:01 本地` · 批次 `路 A 接线轮(全量 EditMode)` ·
> commit `08fb813` + 未提交工作树(2 生产 + 1 组定义 + 2 测试 + 1 账本)。
>
> ⚠️ **`+7` 是实跑出来的**,不是照抄 —— 前次 `3973` 亦实跑且带三元组
> (`2026-10-20 10:42`,`s15-bake6-full.xml`),可复算。
> ⚠️ 本轮的**定向**结果另有两份:`wiring-a1.xml`(首轮基线 7/7)与
> `wiring-base2.xml`(证伪还原后复跑 7/7)—— 与全量里这 7 条**同结论**。

---

## §9 定向与全量的分工(为什么两者都要)

| 项 | 定向 | 全量 |
|---|---|---|
| 命令 | `--filter ProviderWiringGateTest` | 无 filter |
| 结果 | **7 / 7 passed** ×2 次 | **3980 / 3944 / 0 / 36** |
| 证明什么 | 本门自身可判、且证伪轮后仍绿 | 未打断任何既有测试 |

定向不能说「零回归」,全量不能说「本门有判别力」—— 两者不可互相顶替。

